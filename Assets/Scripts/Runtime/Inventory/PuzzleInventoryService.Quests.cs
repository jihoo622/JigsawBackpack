using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 술집 운송 의뢰. 의뢰 생성·수락·완료·실패와 지역 거래 불이익을 세션 상태로 가진다. 평판은 숙련 레벨 합이다.
// 규칙 수치는 TrainventoryQuestTableData가 원본이고, 여기에는 진행 상태만 둔다.
public sealed partial class PuzzleInventoryService
{
    readonly List<TrainventoryQuest> quests = new List<TrainventoryQuest>();
    readonly List<TrainventoryQuestOffer> questOffers = new List<TrainventoryQuestOffer>();
    readonly Dictionary<string, TrainventoryRegionPenalty> regionPenalties =
        new Dictionary<string, TrainventoryRegionPenalty>(StringComparer.Ordinal);
    int questOfferVisit = -1;
    int lastQuestId;
    System.Random questRandom = new System.Random();

    internal IReadOnlyList<TrainventoryQuest> Quests => quests;
    internal int ActiveQuestCount => quests.Count(_quest => _quest.State == TrainventoryQuestState.Active);

    // 현재 평판 이하의 단계 중 가장 높은 단계의 동시 진행 수.
    internal int MaxActiveQuests => tables.Quest.reputationLevels
        .Where(_level => _level.reputation <= Reputation)
        .Select(_level => _level.maxActiveQuests)
        .DefaultIfEmpty(0)
        .Max();

    // 동시 진행 수가 지금보다 늘어나는 가장 가까운 평판 단계. 없으면(최대 단계) false다.
    internal bool TryGetNextQuestReputationLevel(out int _reputation, out int _maxActiveQuests)
    {
        int reputation = Reputation;
        int current = MaxActiveQuests;
        var next = tables.Quest.reputationLevels
            .Where(_level => _level.reputation > reputation && _level.maxActiveQuests > current)
            .OrderBy(_level => _level.reputation)
            .FirstOrDefault();
        _reputation = next?.reputation ?? 0;
        _maxActiveQuests = next?.maxActiveQuests ?? current;
        return next != null;
    }

    // 이번 방문의 의뢰 목록. 같은 방문에서는 처음 만든 목록을 유지하고 수락한 의뢰만 빠진다.
    internal IReadOnlyList<TrainventoryQuestOffer> QuestOffers
    {
        get
        {
            EnsureQuestOffers();
            return questOffers;
        }
    }

    void ResetQuests()
    {
        quests.Clear();
        questOffers.Clear();
        regionPenalties.Clear();
        questOfferVisit = -1;
        lastQuestId = 0;
    }

    internal bool IsQuestCargo(PuzzleItemDefinition _definition)
    {
        return _definition != null && _definition.Tags.Contains(tables.Quest.cargoTag);
    }

    #region 의뢰 생성

    void EnsureQuestOffers()
    {
        if (questOfferVisit == CityMoveCount)
        {
            return;
        }
        questOfferVisit = CityMoveCount;
        questOffers.Clear();
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        var distances = ShortestTravel(CurrentCityId, _route => _route.distance);
        // 이동은 노선마다 같은 턴을 쓰므로 최단 이동 턴은 구간 수 × 이동 턴이다.
        long legTurns = tables.PuzzleTime.travelTurns;
        var turns = ShortestTravel(CurrentCityId, _route => legTurns);
        for (int i = 0; i < tables.Quest.offersPerTavern; i++)
        {
            if (TryCreateCourierOffer(distances, turns, out var offer))
            {
                questOffers.Add(offer);
            }
        }
    }

    bool TryCreateCourierOffer(Dictionary<string, long> _distances, Dictionary<string, long> _turns,
        out TrainventoryQuestOffer _offer)
    {
        _offer = null;
        // 1. 목적지 후보가 있는 의뢰 틀만 가중치 추첨 대상으로 남긴다.
        var candidates = new List<(TrainventoryCourierQuestData Template, List<string> Cities)>();
        foreach (var template in tables.CourierQuests)
        {
            var cities = _distances
                .Where(_pair => _pair.Value >= template.minTravelDistance && _pair.Value <= template.maxTravelDistance)
                .Select(_pair => _pair.Key)
                .OrderBy(_id => _id, StringComparer.Ordinal)
                .ToList();
            if (cities.Count > 0)
            {
                candidates.Add((template, cities));
            }
        }
        if (candidates.Count == 0)
        {
            return false;
        }

        // 2. 의뢰 틀, 목적지, 화물을 차례로 고른다.
        int roll = questRandom.Next(candidates.Sum(_candidate => _candidate.Template.weight));
        var selected = candidates[0];
        foreach (var candidate in candidates)
        {
            if (roll < candidate.Template.weight)
            {
                selected = candidate;
                break;
            }
            roll -= candidate.Template.weight;
        }
        string targetCityId = selected.Cities[questRandom.Next(selected.Cities.Count)];
        var pieceIds = selected.Template.pieceIds.OrderBy(_id => _id, StringComparer.Ordinal).ToArray();
        var piece = definitions[pieceIds[questRandom.Next(pieceIds.Length)]];

        // 3. 보상·기한·보증금을 테이블 계수로 계산한다.
        // 보상·후보 범위는 노선 거리, 기한은 최단 이동 턴 × deadlinePercent(턴으로 올림) + 여유 시간이다.
        long travelDistance = _distances[targetCityId];
        long travelTurns = _turns[targetCityId];
        long reward = checked(selected.Template.baseReward + (long)selected.Template.rewardPerCellDistance * piece.Cells.Count * travelDistance);
        long deadlineHours = checked(PuzzleTimeManager.HoursOf((travelTurns * selected.Template.deadlinePercent + 99) / 100) +
            selected.Template.deadlineExtraHours);
        // 4. 완료 때 경험치를 받을 숙련 태그 하나와 경험치(보상 × 비율)를 정해 의뢰에 보여 준다.
        string proficiencyTag = PickQuestProficiencyTag(selected.Template, piece, targetCityId);
        var rewardWagon = tables.PuzzleTrade.wagonProducts.FirstOrDefault(_product => _product.id == selected.Template.rewardWagonProductId);
        long rewardValue = checked(reward + (rewardWagon?.price ?? 0));
        long proficiencyXp = checked(rewardValue * tables.Quest.proficiencyXpPercentOfReward / 100);
        var penalty = tables.QuestPenalty(selected.Template.penaltyId);
        long deposit = PenaltyType(penalty) == TrainventoryQuestPenaltyType.Deposit
            ? checked((rewardValue * penalty.depositPercentOfReward + 99) / 100)
            : 0;
        _offer = new TrainventoryQuestOffer(selected.Template.id, piece.Id, CurrentCityId, targetCityId, penalty.id,
            travelTurns, reward, deposit, deadlineHours, proficiencyTag, proficiencyXp, selected.Template.rewardWagonProductId);
        return true;
    }

    // 의뢰 틀의 후보 → 화물의 생산 태그 → 목적지 지역의 생산 태그 → 전체 숙련 태그 순으로 첫 후보 묶음에서 하나를 고른다.
    string PickQuestProficiencyTag(TrainventoryCourierQuestData _template, PuzzleItemDefinition _piece, string _targetCityId)
    {
        var candidates = (_template.proficiencyTags ?? Array.Empty<string>())
            .Where(_tag => tables.ProficiencyTags.Contains(_tag)).ToList();
        if (candidates.Count == 0)
        {
            candidates = ProficiencyTagsOf(_piece).ToList();
        }
        if (candidates.Count == 0)
        {
            candidates = (tables.Region(tables.CityPrice(_targetCityId).regionId).productionTags ?? Array.Empty<string>())
                .Where(_tag => tables.ProficiencyTags.Contains(_tag)).ToList();
        }
        if (candidates.Count == 0)
        {
            candidates = tables.ProficiencyTags.ToList();
        }
        var ordered = candidates.Distinct(StringComparer.Ordinal).OrderBy(_tag => _tag, StringComparer.Ordinal).ToArray();
        return ordered[questRandom.Next(ordered.Length)];
    }

    // 현재 도시에서 다른 도시까지 _weight(노선 거리 또는 이동 분)의 최단 합.
    Dictionary<string, long> ShortestTravel(string _fromCityId, Func<PuzzleRouteData, long> _weight)
    {
        var distances = new Dictionary<string, long>(StringComparer.Ordinal) { [_fromCityId] = 0 };
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            string current = null;
            long best = long.MaxValue;
            foreach (var pair in distances)
            {
                if (!done.Contains(pair.Key) && (pair.Value < best ||
                    (pair.Value == best && StringComparer.Ordinal.Compare(pair.Key, current) < 0)))
                {
                    current = pair.Key;
                    best = pair.Value;
                }
            }
            if (current == null)
            {
                break;
            }
            done.Add(current);
            foreach (var route in tables.RoutesFrom(current))
            {
                long arrival = best + _weight(route);
                if (!distances.TryGetValue(route.toCityId, out long known) || arrival < known)
                {
                    distances[route.toCityId] = arrival;
                }
            }
        }

        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var pair in distances)
        {
            if (!string.Equals(pair.Key, _fromCityId, StringComparison.Ordinal))
            {
                result.Add(pair.Key, pair.Value);
            }
        }
        return result;
    }

    static TrainventoryQuestPenaltyType PenaltyType(TrainventoryQuestPenaltyData _penalty)
    {
        return (TrainventoryQuestPenaltyType)Enum.Parse(typeof(TrainventoryQuestPenaltyType), _penalty.type);
    }

    #endregion

    #region 수락

    internal TrainventoryQuestAcceptFailure EvaluateQuestAccept(TrainventoryQuestOffer _offer)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return TrainventoryQuestAcceptFailure.RunFinished;
        }
        if (_offer == null || !QuestOffers.Contains(_offer))
        {
            return TrainventoryQuestAcceptFailure.Unavailable;
        }
        if (ActiveQuestCount >= MaxActiveQuests)
        {
            return TrainventoryQuestAcceptFailure.ActiveLimit;
        }
        if (Funds < _offer.Deposit)
        {
            return TrainventoryQuestAcceptFailure.InsufficientFunds;
        }
        var definition = definitions[_offer.PieceId];
        if (!trainWagons.Any(_wagon => _wagon.CanAutoPlaceAll(definition, 1, true)))
        {
            return TrainventoryQuestAcceptFailure.NoSpace;
        }
        return TrainventoryQuestAcceptFailure.None;
    }

    // H19: 의뢰 화물을 지정한 화차·칸·회전에 직접 배치해 수락한다.
    internal TrainventoryQuestAcceptFailure EvaluateQuestAcceptAt(TrainventoryQuestOffer _offer,
        PuzzleInventory _wagon, Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return TrainventoryQuestAcceptFailure.RunFinished;
        }
        if (_offer == null || !QuestOffers.Contains(_offer))
        {
            return TrainventoryQuestAcceptFailure.Unavailable;
        }
        if (ActiveQuestCount >= MaxActiveQuests)
        {
            return TrainventoryQuestAcceptFailure.ActiveLimit;
        }
        if (Funds < _offer.Deposit)
        {
            return TrainventoryQuestAcceptFailure.InsufficientFunds;
        }
        if (_wagon == null || !IsTrainWagon(_wagon) || !definitions.TryGetValue(_offer.PieceId, out var definition) ||
            !_wagon.CanPlaceAt(definition, _grabbedCell, _targetCell, _clockwiseTurns))
        {
            return TrainventoryQuestAcceptFailure.NoSpace;
        }
        return TrainventoryQuestAcceptFailure.None;
    }

    internal TrainventoryQuestAcceptFailure TryAcceptQuestAt(TrainventoryQuestOffer _offer,
        PuzzleInventory _wagon, Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        var failure = EvaluateQuestAcceptAt(_offer, _wagon, _grabbedCell, _targetCell, _clockwiseTurns);
        if (failure != TrainventoryQuestAcceptFailure.None)
        {
            RecordAction(PuzzleActionKind.AcceptQuest, "quest." + failure);
            return failure;
        }

        var definition = definitions[_offer.PieceId];
        int nextId = lastInstanceId + 1;
        if (!_wagon.TryPlaceAt(definition, _grabbedCell, _targetCell, _clockwiseTurns, nextId, out var cargo))
        {
            RecordAction(PuzzleActionKind.AcceptQuest, "quest.NoSpace");
            return TrainventoryQuestAcceptFailure.NoSpace;
        }
        lastInstanceId = nextId;
        int instanceId = cargo.InstanceId;
        cargo.SetOwned(true);
        RecordLocalOrigin(instanceId);
        CurrentWagonIndex = trainWagons.IndexOf(_wagon);

        long previousFunds = Funds;
        Funds -= _offer.Deposit;
        questOffers.Remove(_offer);
        var quest = new TrainventoryQuest(++lastQuestId, _offer, instanceId, Time.ElapsedHours,
            checked(Time.ElapsedHours + _offer.DeadlineHours));
        quest.AcceptedMoveCount = CityMoveCount;
        quests.Add(quest);
        RevealRouteTo(_offer.TargetCityId);
        RecordAction(PuzzleActionKind.AcceptQuest);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        GameEvents.RaiseQuestChanged(this, quest);
        return TrainventoryQuestAcceptFailure.None;
    }

    // 의뢰 화물을 현재 화물칸부터 빈자리에 싣고 보증금을 받는다. 실패하면 상태를 바꾸지 않는다.
    // _preferredWagon이 있으면 그 화차부터 빈자리를 찾는다(Ctrl+클릭: 마지막으로 포커스한 화차 창).
    internal TrainventoryQuestAcceptFailure TryAcceptQuest(TrainventoryQuestOffer _offer, PuzzleInventory _preferredWagon = null)
    {
        var failure = EvaluateQuestAccept(_offer);
        if (failure != TrainventoryQuestAcceptFailure.None)
        {
            RecordAction(PuzzleActionKind.AcceptQuest, "quest." + failure);
            return failure;
        }

        var definition = definitions[_offer.PieceId];
        var first = _preferredWagon != null && IsTrainWagon(_preferredWagon) ? _preferredWagon : Train;
        var wagons = new List<PuzzleInventory> { first };
        wagons.AddRange(trainWagons.Where(_wagon => _wagon != first));
        var wagon = wagons.First(_candidate => _candidate.CanAutoPlaceAll(definition, 1, true));
        wagon.TryAutoPlace(definition, ref lastInstanceId, true);
        int instanceId = lastInstanceId;
        var cargo = wagon.Placements.First(_placement => _placement.InstanceId == instanceId);
        cargo.SetOwned(true);
        // 의뢰 화물은 일반 화물처럼 받은 마을 출신으로 기록한다(그 마을에서 팔면 숙련 경험치 없음).
        RecordLocalOrigin(instanceId);
        CurrentWagonIndex = trainWagons.IndexOf(wagon);

        long previousFunds = Funds;
        Funds -= _offer.Deposit;
        questOffers.Remove(_offer);
        var quest = new TrainventoryQuest(++lastQuestId, _offer, instanceId, Time.ElapsedHours,
            checked(Time.ElapsedHours + _offer.DeadlineHours));
        quest.AcceptedMoveCount = CityMoveCount;
        quests.Add(quest);
        // 의뢰 목적지의 위치와 거기까지의 최단 경로 노선이 드러난다. 경유역 정보는 드러나지 않는다.
        RevealRouteTo(_offer.TargetCityId);
        RecordAction(PuzzleActionKind.AcceptQuest);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        GameEvents.RaiseQuestChanged(this, quest);
        return TrainventoryQuestAcceptFailure.None;
    }

    #endregion

    #region 완료·실패

    // 도착한 도시가 목적지인 의뢰를 완료한다. 화물을 내리고 보상 + 화물 실제 가치 + 보증금을 지급하며
    // 의뢰 화물의 생산 태그에 숙련 경험치를 준다.
    void CompleteArrivedQuests()
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }
        var arrived = quests.Where(_quest => _quest.State == TrainventoryQuestState.Active &&
            string.Equals(_quest.TargetCityId, CurrentCityId, StringComparison.Ordinal)).ToList();
        var completed = new List<TrainventoryQuest>();
        long previousFunds = Funds;
        foreach (var quest in arrived)
        {
            if (!TryFindQuestCargo(quest, out var cargo))
            {
                FailQuest(quest, TrainventoryQuestFailure.CargoLost);
                continue;
            }
            decimal value = Math.Round(cargo.FreshnessAdjustedValue, MidpointRounding.AwayFromZero);
            long cargoValue = value > 0 ? (long)value : 0;
            long payout = checked(quest.Reward + cargoValue + quest.Deposit);
            cargo.Owner.Remove(cargo);
            Funds = checked(Funds + payout);
            quest.Finish(TrainventoryQuestState.Completed, TrainventoryQuestFailure.None, payout, Time.ElapsedHours);
            GrantQuestProficiency(quest);
            ReceivePendingQuestWagons(false);
            completed.Add(quest);
        }
        if (completed.Count == 0)
        {
            return;
        }

        PruneAnimalStates();
        RecordAction(PuzzleActionKind.CompleteQuest, _targetCityId: CurrentCityId);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        foreach (var quest in completed)
        {
            GameEvents.RaiseQuestChanged(this, quest);
        }
    }

    // H19: 목적지 술집 의뢰 게시판에 의뢰 화물을 놓아 완료한다.
    internal bool CanCompleteQuestCargoHere(PuzzlePlacement _cargo, out TrainventoryQuest _quest)
    {
        _quest = null;
        if (RunStatus != PuzzleRunStatus.InProgress || _cargo == null)
        {
            return false;
        }
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            if (q.State == TrainventoryQuestState.Active && q.CargoInstanceId == _cargo.InstanceId &&
                string.Equals(q.TargetCityId, CurrentCityId, StringComparison.Ordinal))
            {
                _quest = q;
                return true;
            }
        }
        return false;
    }

    // 화차의 의뢰 화물을 의뢰 게시판에 놓았다. 목적지면 완료, 의뢰를 받은 도시면 취소, 그 밖의 도시면 거부한다.
    // _slot은 게시판에 놓은 자리(잡은 칸·놓은 칸·회전). 취소로 의뢰가 게시판에 다시 걸릴 때 그 자리에 놓는다(안 맞으면 빈자리).
    internal bool TryReturnQuestCargo(PuzzlePlacement _cargo, out bool _cancelled, out string _failureReason,
        (Vector2Int GrabbedCell, Vector2Int TargetCell, int ClockwiseTurns)? _slot = null)
    {
        _cancelled = false;
        var quest = ActiveQuestForCargo(_cargo);
        if (quest != null && !string.Equals(quest.TargetCityId, CurrentCityId, StringComparison.Ordinal) &&
            string.Equals(quest.SourceCityId, CurrentCityId, StringComparison.Ordinal))
        {
            _cancelled = TryCancelQuest(quest, _cargo, out _failureReason, _slot);
            return _cancelled;
        }
        return TryCompleteQuestWithCargo(_cargo, out _failureReason);
    }

    // 끌어 놓기 미리보기: 이 화물을 지금 의뢰 게시판에 놓으면 완료되거나(목적지) 취소되는지(의뢰를 받은 도시).
    internal bool CanReturnQuestCargoHere(PuzzlePlacement _cargo)
    {
        if (CanCompleteQuestCargoHere(_cargo, out _))
        {
            return true;
        }
        var quest = ActiveQuestForCargo(_cargo);
        return quest != null && RunStatus == PuzzleRunStatus.InProgress &&
            string.Equals(quest.SourceCityId, CurrentCityId, StringComparison.Ordinal) &&
            TryFindQuestCargo(quest, out var cargo) && ReferenceEquals(cargo, _cargo);
    }

    TrainventoryQuest ActiveQuestForCargo(PuzzlePlacement _cargo)
    {
        if (_cargo == null)
        {
            return null;
        }
        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest.State == TrainventoryQuestState.Active && quest.CargoInstanceId == _cargo.InstanceId)
            {
                return quest;
            }
        }
        return null;
    }

    // 의뢰를 받은 도시에서 의뢰 화물을 내려놓아 취소한다. 화물을 내리고 보증금을 돌려주며 지역 불이익은 없다.
    // 같은 방문 안이면 그 의뢰를 게시판에 되돌려 다시 받을 수 있다(다른 방문에서는 게시판 목록이 새로 만들어졌으므로 되돌리지 않는다).
    internal bool TryCancelQuest(TrainventoryQuest _quest, PuzzlePlacement _cargo, out string _failureReason,
        (Vector2Int GrabbedCell, Vector2Int TargetCell, int ClockwiseTurns)? _slot = null)
    {
        _failureReason = null;
        if (RunStatus != PuzzleRunStatus.InProgress || _quest == null || _quest.State != TrainventoryQuestState.Active)
        {
            _failureReason = "unavailable";
            return false;
        }
        if (!string.Equals(_quest.SourceCityId, CurrentCityId, StringComparison.Ordinal))
        {
            _failureReason = Presentation.QuestDropWrongCity(Presentation.QuestCityName(_quest.TargetCityId));
            return false;
        }
        if (!TryFindQuestCargo(_quest, out var cargo) || !ReferenceEquals(cargo, _cargo))
        {
            _failureReason = Presentation.QuestDropNotQuestCargo;
            return false;
        }

        long previousFunds = Funds;
        cargo.Owner.Remove(cargo);
        PruneAnimalStates();
        Funds = checked(Funds + _quest.Deposit);
        _quest.Finish(TrainventoryQuestState.Cancelled, TrainventoryQuestFailure.None, 0, Time.ElapsedHours);
        if (_quest.Offer != null && _quest.AcceptedMoveCount == CityMoveCount && questOfferVisit == CityMoveCount &&
            !questOffers.Contains(_quest.Offer))
        {
            questOffers.Add(_quest.Offer);
            if (_slot.HasValue)
            {
                preferredOfferSlots[_quest.Offer] = _slot.Value;
            }
        }
        RecordAction(PuzzleActionKind.CancelQuest, _targetCityId: CurrentCityId);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        GameEvents.RaiseQuestChanged(this, _quest);
        return true;
    }

    internal bool TryCompleteQuestWithCargo(PuzzlePlacement _cargo, out string _failureReason)
    {
        _failureReason = null;
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            _failureReason = Presentation.PuzzleRunStatusText(RunStatus);
            return false;
        }
        if (_cargo == null)
        {
            _failureReason = "invalid_cargo";
            return false;
        }
        TrainventoryQuest activeQuest = null;
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            if (q.State == TrainventoryQuestState.Active && q.CargoInstanceId == _cargo.InstanceId)
            {
                activeQuest = q;
                break;
            }
        }
        if (activeQuest == null)
        {
            _failureReason = Presentation.QuestDropNotQuestCargo;
            return false;
        }
        if (!string.Equals(activeQuest.TargetCityId, CurrentCityId, StringComparison.Ordinal))
        {
            _failureReason = Presentation.QuestDropWrongCity(Presentation.QuestCityName(activeQuest.TargetCityId));
            return false;
        }
        return TryCompleteQuest(activeQuest, out _failureReason);
    }

    internal bool TryCompleteQuest(TrainventoryQuest _quest, out string _failureReason)
    {
        _failureReason = null;
        if (RunStatus != PuzzleRunStatus.InProgress || _quest == null || _quest.State != TrainventoryQuestState.Active)
        {
            _failureReason = "unavailable";
            return false;
        }
        if (!string.Equals(_quest.TargetCityId, CurrentCityId, StringComparison.Ordinal))
        {
            _failureReason = Presentation.QuestDropWrongCity(Presentation.QuestCityName(_quest.TargetCityId));
            return false;
        }
        if (!TryFindQuestCargo(_quest, out var cargo))
        {
            FailQuest(_quest, TrainventoryQuestFailure.CargoLost);
            _failureReason = "cargo_lost";
            return false;
        }

        long previousFunds = Funds;
        decimal value = Math.Round(cargo.FreshnessAdjustedValue, MidpointRounding.AwayFromZero);
        long cargoValue = value > 0 ? (long)value : 0;
        long payout = checked(_quest.Reward + cargoValue + _quest.Deposit);
        cargo.Owner.Remove(cargo);
        Funds = checked(Funds + payout);
        _quest.Finish(TrainventoryQuestState.Completed, TrainventoryQuestFailure.None, payout, Time.ElapsedHours);
        GrantQuestProficiency(_quest);
        PruneAnimalStates();
        RecordAction(PuzzleActionKind.CompleteQuest, _targetCityId: CurrentCityId);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        GameEvents.RaiseQuestChanged(this, _quest);
        return true;
    }

    PuzzleInventory cachedOfferBoard;
    readonly Dictionary<int, TrainventoryQuestOffer> cachedOfferByInstanceId = new();
    readonly List<TrainventoryQuestOffer> cachedOfferSnapshot = new();
    int cachedOfferBoardWidth;
    int cachedOfferBoardHeight;
    int cachedOfferSkippedCount;

    // 취소로 게시판에 되돌아오는 의뢰를 놓을 자리(플레이어가 내려놓은 곳).
    readonly Dictionary<TrainventoryQuestOffer, (Vector2Int GrabbedCell, Vector2Int TargetCell, int ClockwiseTurns)> preferredOfferSlots = new();

    bool TryUpdateQuestOfferBoard(IReadOnlyList<TrainventoryQuestOffer> _offers, int _width, int _height)
    {
        if (cachedOfferBoard == null || cachedOfferBoardWidth != _width || cachedOfferBoardHeight != _height ||
            !_offers.Any(_offer => cachedOfferSnapshot.Contains(_offer)))
        {
            return false;
        }

        // 빠진 의뢰의 화물을 게시판에서 내린다.
        foreach (var pair in cachedOfferByInstanceId.ToList())
        {
            if (_offers.Contains(pair.Value))
            {
                continue;
            }
            var placement = cachedOfferBoard.Placements.FirstOrDefault(_placement => _placement.InstanceId == pair.Key);
            if (placement != null)
            {
                cachedOfferBoard.Remove(placement);
            }
            cachedOfferByInstanceId.Remove(pair.Key);
        }

        // 새로 걸린 의뢰(취소로 되돌아온 의뢰 등)만 빈자리에 놓는다.
        int skipped = 0;
        foreach (var offer in _offers)
        {
            if (cachedOfferByInstanceId.ContainsValue(offer))
            {
                continue;
            }
            int nextId = lastInstanceId + 1;
            PuzzlePlacement placed = null;
            bool hasDefinition = definitions.TryGetValue(offer.PieceId, out var definition);
            if (hasDefinition && preferredOfferSlots.Remove(offer, out var slot))
            {
                cachedOfferBoard.TryPlaceAt(definition, slot.GrabbedCell, slot.TargetCell, slot.ClockwiseTurns, nextId, out placed);
            }
            if (hasDefinition && (placed != null || cachedOfferBoard.TryAutoPlace(definition, nextId, out placed)))
            {
                lastInstanceId = nextId;
                cachedOfferByInstanceId[placed.InstanceId] = offer;
            }
            else
            {
                skipped++;
            }
        }

        cachedOfferSnapshot.Clear();
        cachedOfferSnapshot.AddRange(_offers);
        cachedOfferSkippedCount = skipped;
        return true;
    }

    internal PuzzleInventory BuildQuestOfferBoard(int _width, int _height,
        out Dictionary<int, TrainventoryQuestOffer> _offerByInstanceId, out int _skippedCount)
    {
        var offers = QuestOffers;
        bool same = cachedOfferBoard != null &&
            cachedOfferBoardWidth == _width &&
            cachedOfferBoardHeight == _height &&
            cachedOfferSnapshot.Count == offers.Count;
        if (same)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (!ReferenceEquals(cachedOfferSnapshot[i], offers[i]))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
        {
            _offerByInstanceId = new Dictionary<int, TrainventoryQuestOffer>(cachedOfferByInstanceId);
            _skippedCount = cachedOfferSkippedCount;
            return cachedOfferBoard;
        }

        // 같은 게시 목록에서 몇 개만 빠지거나(수락) 다시 걸린(취소) 경우에는 보드를 다시 만들지 않는다.
        // 플레이어가 게시판 안에서 옮겨 둔 다른 의뢰의 자리를 지키기 위해서다. 새 방문처럼 겹치는 의뢰가 없으면 새로 만든다.
        if (TryUpdateQuestOfferBoard(offers, _width, _height))
        {
            _offerByInstanceId = new Dictionary<int, TrainventoryQuestOffer>(cachedOfferByInstanceId);
            _skippedCount = cachedOfferSkippedCount;
            return cachedOfferBoard;
        }

        var boardData = new PuzzleBoardData { width = _width, height = _height };
        var board = new PuzzleInventory(boardData, Array.Empty<PuzzleStartingPlacementData>(), definitions, Array.Empty<PuzzleSynergyRuleData>(), ref lastInstanceId);
        cachedOfferByInstanceId.Clear();
        cachedOfferSnapshot.Clear();
        int skipped = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            cachedOfferSnapshot.Add(offer);
            if (!definitions.TryGetValue(offer.PieceId, out var def))
            {
                skipped++;
                continue;
            }
            int nextId = lastInstanceId + 1;
            if (board.TryAutoPlace(def, nextId, out var placed))
            {
                lastInstanceId = nextId;
                cachedOfferByInstanceId[placed.InstanceId] = offer;
            }
            else
            {
                skipped++;
            }
        }
        cachedOfferBoard = board;
        cachedOfferBoardWidth = _width;
        cachedOfferBoardHeight = _height;
        cachedOfferSkippedCount = skipped;

        _offerByInstanceId = new Dictionary<int, TrainventoryQuestOffer>(cachedOfferByInstanceId);
        _skippedCount = skipped;
        return board;
    }

    // 기한이 지났거나 화물을 잃은(판매·만료·변환) 진행 중 의뢰를 실패 처리한다.
    // 판매한 의뢰 화물이 지금 역 시장 보드에 남아 있으면 되살 수 있으므로 실패를 미룬다.
    // 역을 떠나거나 시장이 재입고되어 보드에서 사라지면 다음 판정에서 실패한다.
    void CheckQuestFailures()
    {
        if (Time == null)
        {
            return;
        }
        foreach (var quest in quests.Where(_quest => _quest.State == TrainventoryQuestState.Active).ToList())
        {
            if (Time.ElapsedHours > quest.DeadlineHours)
            {
                FailQuest(quest, TrainventoryQuestFailure.Overdue);
            }
            else if (!TryFindQuestCargo(quest, out _) && !IsQuestCargoOnSaleHere(quest))
            {
                FailQuest(quest, TrainventoryQuestFailure.CargoLost);
            }
        }
    }

    // 한도가 찬 보상은 완료된 의뢰에 보관하고, 판매·숙련 상승으로 자리가 생기면 순서대로 연결한다.
    void ReceivePendingQuestWagons(bool _notify = true)
    {
        foreach (var quest in quests.Where(_quest => _quest.State == TrainventoryQuestState.Completed &&
                     !string.IsNullOrEmpty(_quest.RewardWagonProductId) && !_quest.RewardWagonReceived))
        {
            if (trainWagons.Count >= MaxWagonCount)
            {
                break;
            }
            var product = tables.PuzzleTrade.wagonProducts.First(_product => _product.id == quest.RewardWagonProductId);
            var wagon = new PuzzleInventory(tables.WagonBoardOf(product), Array.Empty<PuzzleStartingPlacementData>(),
                definitions, tables.SynergiesFor(PuzzleSynergyScope.Train), ref lastInstanceId);
            wagon.SynergyComboBonusPercent = SynergyComboBonusPercent;
            wagon.WagonNumber = ++lastWagonNumber;
            wagon.WagonProduct = product;
            wagon.WagonSalePrice = tables.WagonSalePrice(product.price);
            trainWagons.Add(wagon);
            quest.RewardWagonReceived = true;
            if (_notify)
                GameEvents.RaiseQuestChanged(this, quest);
        }
    }

    void FailQuest(TrainventoryQuest _quest, TrainventoryQuestFailure _failure)
    {
        if (TryFindQuestCargo(_quest, out var cargo))
        {
            cargo.Owner.Remove(cargo);
            PruneAnimalStates();
        }
        _quest.Finish(TrainventoryQuestState.Failed, _failure, 0, Time.ElapsedHours);
        var penalty = tables.QuestPenalty(_quest.PenaltyId);
        if (PenaltyType(penalty) == TrainventoryQuestPenaltyType.RegionTrade)
        {
            string regionId = tables.CityPrice(_quest.SourceCityId).regionId;
            long until = checked(Time.ElapsedHours + (long)penalty.durationHours);
            if (!regionPenalties.TryGetValue(regionId, out var current) || current.UntilHours < until)
            {
                regionPenalties[regionId] = new TrainventoryRegionPenalty(penalty.buyPercentDelta, penalty.sellPercentDelta, until);
            }
        }
        GameEvents.RaiseQuestChanged(this, _quest);
    }

    bool IsQuestCargoOnSaleHere(TrainventoryQuest _quest)
    {
        if (IsPassingThrough || !markets.TryGetValue(CurrentCityId, out var market))
        {
            return false;
        }
        return market.Placements.Any(_placement => _placement.InstanceId == _quest.CargoInstanceId &&
            string.Equals(_placement.Definition.Id, _quest.PieceId, StringComparison.Ordinal));
    }

    // 기차와 작업대 어디에든 같은 인스턴스·같은 품목으로 남아 있어야 의뢰 화물이다. 만료로 똥이 되면 품목이 바뀐다.
    bool TryFindQuestCargo(TrainventoryQuest _quest, out PuzzlePlacement _cargo)
    {
        var boards = trainWagons
            .Concat(butcheries.Values.Select(_state => _state.Board))
            .Concat(modifications.Values.Select(_state => _state.Board));
        foreach (var board in boards)
        {
            foreach (var placement in board.Placements)
            {
                if (placement.InstanceId == _quest.CargoInstanceId)
                {
                    _cargo = string.Equals(placement.Definition.Id, _quest.PieceId, StringComparison.Ordinal) ? placement : null;
                    return _cargo != null;
                }
            }
        }
        _cargo = null;
        return false;
    }

    internal bool TryGetQuestForCargo(PuzzlePlacement _placement, out TrainventoryQuest _quest)
    {
        _quest = _placement == null ? null : quests.FirstOrDefault(_candidate =>
            _candidate.State == TrainventoryQuestState.Active && _candidate.CargoInstanceId == _placement.InstanceId &&
            string.Equals(_candidate.PieceId, _placement.Definition.Id, StringComparison.Ordinal));
        return _quest != null;
    }

    #endregion

    #region 지역 거래 불이익

    internal bool TryGetRegionPenalty(string _regionId, out TrainventoryRegionPenalty _penalty)
    {
        if (Time != null && !string.IsNullOrEmpty(_regionId) && regionPenalties.TryGetValue(_regionId, out _penalty) &&
            Time.ElapsedHours < _penalty.UntilHours)
        {
            return true;
        }
        _penalty = default;
        return false;
    }

    PuzzleTradeRate ApplyRegionPenalty(PuzzleCityData _city, PuzzleTradeRate _rate)
    {
        if (!TryGetRegionPenalty(_city.regionId, out var penalty))
        {
            return _rate;
        }
        return new PuzzleTradeRate(_rate.Tag, _rate.BuyPercent + penalty.BuyPercentDelta,
            _rate.SellPercent + penalty.SellPercentDelta);
    }

    #endregion
}

internal enum TrainventoryQuestState
{
    Active,
    Completed,
    Failed,
    // 의뢰를 받은 도시에서 화물을 게시판에 되돌려 놓아 취소했다. 보증금을 돌려받고 불이익은 없다.
    Cancelled
}

internal enum TrainventoryQuestFailure
{
    None,
    // 판매·유통기한 만료 등으로 의뢰 화물이 기차에서 사라졌다.
    CargoLost,
    Overdue
}

internal enum TrainventoryQuestAcceptFailure
{
    None,
    RunFinished,
    Unavailable,
    ActiveLimit,
    InsufficientFunds,
    NoSpace
}

// 술집에 걸린 의뢰. 수락하면 같은 값으로 TrainventoryQuest가 된다.
internal sealed class TrainventoryQuestOffer
{
    internal string RewardWagonProductId { get; }
    internal string TemplateId { get; }
    internal string PieceId { get; }
    internal string SourceCityId { get; }
    internal string TargetCityId { get; }
    internal string PenaltyId { get; }
    // 최단 이동 턴.
    internal long TravelTurns { get; }
    internal long Reward { get; }
    internal long Deposit { get; }
    internal long DeadlineHours { get; }
    // 완료 때 경험치를 받는 숙련 태그와 경험치.
    internal string ProficiencyTag { get; }
    internal long ProficiencyXp { get; }

    internal TrainventoryQuestOffer(string _templateId, string _pieceId, string _sourceCityId, string _targetCityId,
        string _penaltyId, long _travelTurns, long _reward, long _deposit, long _deadlineHours, string _proficiencyTag,
        long _proficiencyXp, string _rewardWagonProductId = null)
    {
        RewardWagonProductId = _rewardWagonProductId;
        ProficiencyTag = _proficiencyTag;
        ProficiencyXp = _proficiencyXp;
        TemplateId = _templateId;
        PieceId = _pieceId;
        SourceCityId = _sourceCityId;
        TargetCityId = _targetCityId;
        PenaltyId = _penaltyId;
        TravelTurns = _travelTurns;
        Reward = _reward;
        Deposit = _deposit;
        DeadlineHours = _deadlineHours;
    }
}

internal sealed class TrainventoryQuest
{
    internal string RewardWagonProductId { get; }
    internal bool RewardWagonReceived { get; set; }
    internal int Id { get; }
    internal string TemplateId { get; }
    internal string PieceId { get; }
    internal string SourceCityId { get; }
    internal string TargetCityId { get; }
    internal string PenaltyId { get; }
    internal long Reward { get; }
    internal long Deposit { get; }
    internal string ProficiencyTag { get; }
    internal long ProficiencyXp { get; }
    internal int CargoInstanceId { get; }
    internal long AcceptedHours { get; }
    internal long DeadlineHours { get; }
    internal TrainventoryQuestState State { get; private set; }
    internal TrainventoryQuestFailure Failure { get; private set; }
    internal long Payout { get; private set; }
    internal long FinishedHours { get; private set; }

    // 수락한 게시 의뢰와 수락한 방문(CityMoveCount). 같은 방문에서 취소하면 그 의뢰를 게시판에 되돌린다.
    internal TrainventoryQuestOffer Offer { get; }
    internal int AcceptedMoveCount { get; set; } = -1;

    internal TrainventoryQuest(int _id, TrainventoryQuestOffer _offer, int _cargoInstanceId, long _acceptedHours,
        long _deadlineHours)
    {
        Id = _id;
        Offer = _offer;
        RewardWagonProductId = _offer.RewardWagonProductId;
        TemplateId = _offer.TemplateId;
        PieceId = _offer.PieceId;
        SourceCityId = _offer.SourceCityId;
        TargetCityId = _offer.TargetCityId;
        PenaltyId = _offer.PenaltyId;
        Reward = _offer.Reward;
        Deposit = _offer.Deposit;
        ProficiencyTag = _offer.ProficiencyTag;
        ProficiencyXp = _offer.ProficiencyXp;
        CargoInstanceId = _cargoInstanceId;
        AcceptedHours = _acceptedHours;
        DeadlineHours = _deadlineHours;
        State = TrainventoryQuestState.Active;
        FinishedHours = -1;
    }

    internal void Finish(TrainventoryQuestState _state, TrainventoryQuestFailure _failure, long _payout, long _finishedHours)
    {
        State = _state;
        Failure = _failure;
        Payout = _payout;
        FinishedHours = _finishedHours;
    }
}

internal readonly struct TrainventoryRegionPenalty
{
    internal int BuyPercentDelta { get; }
    internal int SellPercentDelta { get; }
    internal long UntilHours { get; }

    internal TrainventoryRegionPenalty(int _buyPercentDelta, int _sellPercentDelta, long _untilHours)
    {
        BuyPercentDelta = _buyPercentDelta;
        SellPercentDelta = _sellPercentDelta;
        UntilHours = _untilHours;
    }
}
