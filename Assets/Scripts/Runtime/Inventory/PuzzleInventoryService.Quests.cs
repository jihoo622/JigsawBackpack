using System;
using System.Collections.Generic;
using System.Linq;

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

        var hours = ShortestTravelHours(CurrentCityId);
        for (int i = 0; i < tables.Quest.offersPerTavern; i++)
        {
            if (TryCreateCourierOffer(hours, out var offer))
            {
                questOffers.Add(offer);
            }
        }
    }

    bool TryCreateCourierOffer(Dictionary<string, long> _hours, out TrainventoryQuestOffer _offer)
    {
        _offer = null;
        // 1. 목적지 후보가 있는 의뢰 틀만 가중치 추첨 대상으로 남긴다.
        var candidates = new List<(TrainventoryCourierQuestData Template, List<string> Cities)>();
        foreach (var template in tables.CourierQuests)
        {
            var cities = _hours
                .Where(_pair => _pair.Value >= template.minTravelHours && _pair.Value <= template.maxTravelHours)
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
        long travelHours = _hours[targetCityId];
        long reward = checked(selected.Template.baseReward + (long)selected.Template.rewardPerCellHour * piece.Cells.Count * travelHours);
        long deadlineHours = checked((travelHours * selected.Template.deadlinePercent + 99) / 100 + selected.Template.deadlineExtraHours);
        var penalty = tables.QuestPenalty(selected.Template.penaltyId);
        long deposit = PenaltyType(penalty) == TrainventoryQuestPenaltyType.Deposit
            ? checked((reward * penalty.depositPercentOfReward + 99) / 100)
            : 0;
        _offer = new TrainventoryQuestOffer(selected.Template.id, piece.Id, CurrentCityId, targetCityId, penalty.id,
            travelHours, reward, deposit, deadlineHours);
        return true;
    }

    // 현재 도시에서 다른 도시까지의 최단 이동 시간(시간). 노선 소요 시간은 이동과 같은 TravelMinutes를 쓴다.
    Dictionary<string, long> ShortestTravelHours(string _fromCityId)
    {
        var minutes = new Dictionary<string, long>(StringComparer.Ordinal) { [_fromCityId] = 0 };
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            string current = null;
            long best = long.MaxValue;
            foreach (var pair in minutes)
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
                long arrival = best + PuzzleTimeManager.TravelMinutes(route, tables.PuzzleTime);
                if (!minutes.TryGetValue(route.toCityId, out long known) || arrival < known)
                {
                    minutes[route.toCityId] = arrival;
                }
            }
        }

        var hours = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var pair in minutes)
        {
            if (!string.Equals(pair.Key, _fromCityId, StringComparison.Ordinal))
            {
                hours.Add(pair.Key, (pair.Value + PuzzleTimeManager.MINUTES_PER_HOUR - 1) / PuzzleTimeManager.MINUTES_PER_HOUR);
            }
        }
        return hours;
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

    // 의뢰 화물을 현재 화물칸부터 빈자리에 싣고 보증금을 받는다. 실패하면 상태를 바꾸지 않는다.
    internal TrainventoryQuestAcceptFailure TryAcceptQuest(TrainventoryQuestOffer _offer)
    {
        var failure = EvaluateQuestAccept(_offer);
        if (failure != TrainventoryQuestAcceptFailure.None)
        {
            RecordAction(PuzzleActionKind.AcceptQuest, "quest." + failure);
            return failure;
        }

        var definition = definitions[_offer.PieceId];
        var wagons = new List<PuzzleInventory> { Train };
        wagons.AddRange(trainWagons.Where(_wagon => _wagon != Train));
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
        var quest = new TrainventoryQuest(++lastQuestId, _offer, instanceId, Time.ElapsedMinutes,
            checked(Time.ElapsedMinutes + _offer.DeadlineHours * PuzzleTimeManager.MINUTES_PER_HOUR));
        quests.Add(quest);
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
            quest.Finish(TrainventoryQuestState.Completed, TrainventoryQuestFailure.None, payout, Time.ElapsedMinutes);
            GrantQuestProficiency(quest);
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

    // 기한이 지났거나 화물을 잃은(판매·만료·변환) 진행 중 의뢰를 실패 처리한다.
    void CheckQuestFailures()
    {
        if (Time == null)
        {
            return;
        }
        foreach (var quest in quests.Where(_quest => _quest.State == TrainventoryQuestState.Active).ToList())
        {
            if (Time.ElapsedMinutes > quest.DeadlineMinutes)
            {
                FailQuest(quest, TrainventoryQuestFailure.Overdue);
            }
            else if (!TryFindQuestCargo(quest, out _))
            {
                FailQuest(quest, TrainventoryQuestFailure.CargoLost);
            }
        }
    }

    void FailQuest(TrainventoryQuest _quest, TrainventoryQuestFailure _failure)
    {
        if (TryFindQuestCargo(_quest, out var cargo))
        {
            cargo.Owner.Remove(cargo);
            PruneAnimalStates();
        }
        _quest.Finish(TrainventoryQuestState.Failed, _failure, 0, Time.ElapsedMinutes);
        var penalty = tables.QuestPenalty(_quest.PenaltyId);
        if (PenaltyType(penalty) == TrainventoryQuestPenaltyType.RegionTrade)
        {
            string regionId = tables.CityPrice(_quest.SourceCityId).regionId;
            long until = checked(Time.ElapsedMinutes + (long)penalty.durationHours * PuzzleTimeManager.MINUTES_PER_HOUR);
            if (!regionPenalties.TryGetValue(regionId, out var current) || current.UntilMinutes < until)
            {
                regionPenalties[regionId] = new TrainventoryRegionPenalty(penalty.buyPercentDelta, penalty.sellPercentDelta, until);
            }
        }
        GameEvents.RaiseQuestChanged(this, _quest);
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
            Time.ElapsedMinutes < _penalty.UntilMinutes)
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
    Failed
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
    internal string TemplateId { get; }
    internal string PieceId { get; }
    internal string SourceCityId { get; }
    internal string TargetCityId { get; }
    internal string PenaltyId { get; }
    internal long TravelHours { get; }
    internal long Reward { get; }
    internal long Deposit { get; }
    internal long DeadlineHours { get; }

    internal TrainventoryQuestOffer(string _templateId, string _pieceId, string _sourceCityId, string _targetCityId,
        string _penaltyId, long _travelHours, long _reward, long _deposit, long _deadlineHours)
    {
        TemplateId = _templateId;
        PieceId = _pieceId;
        SourceCityId = _sourceCityId;
        TargetCityId = _targetCityId;
        PenaltyId = _penaltyId;
        TravelHours = _travelHours;
        Reward = _reward;
        Deposit = _deposit;
        DeadlineHours = _deadlineHours;
    }
}

internal sealed class TrainventoryQuest
{
    internal int Id { get; }
    internal string TemplateId { get; }
    internal string PieceId { get; }
    internal string SourceCityId { get; }
    internal string TargetCityId { get; }
    internal string PenaltyId { get; }
    internal long Reward { get; }
    internal long Deposit { get; }
    internal int CargoInstanceId { get; }
    internal long AcceptedMinutes { get; }
    internal long DeadlineMinutes { get; }
    internal TrainventoryQuestState State { get; private set; }
    internal TrainventoryQuestFailure Failure { get; private set; }
    internal long Payout { get; private set; }
    internal long FinishedMinutes { get; private set; }

    internal TrainventoryQuest(int _id, TrainventoryQuestOffer _offer, int _cargoInstanceId, long _acceptedMinutes,
        long _deadlineMinutes)
    {
        Id = _id;
        TemplateId = _offer.TemplateId;
        PieceId = _offer.PieceId;
        SourceCityId = _offer.SourceCityId;
        TargetCityId = _offer.TargetCityId;
        PenaltyId = _offer.PenaltyId;
        Reward = _offer.Reward;
        Deposit = _offer.Deposit;
        CargoInstanceId = _cargoInstanceId;
        AcceptedMinutes = _acceptedMinutes;
        DeadlineMinutes = _deadlineMinutes;
        State = TrainventoryQuestState.Active;
        FinishedMinutes = -1;
    }

    internal void Finish(TrainventoryQuestState _state, TrainventoryQuestFailure _failure, long _payout, long _finishedMinutes)
    {
        State = _state;
        Failure = _failure;
        Payout = _payout;
        FinishedMinutes = _finishedMinutes;
    }
}

internal readonly struct TrainventoryRegionPenalty
{
    internal int BuyPercentDelta { get; }
    internal int SellPercentDelta { get; }
    internal long UntilMinutes { get; }

    internal TrainventoryRegionPenalty(int _buyPercentDelta, int _sellPercentDelta, long _untilMinutes)
    {
        BuyPercentDelta = _buyPercentDelta;
        SellPercentDelta = _sellPercentDelta;
        UntilMinutes = _untilMinutes;
    }
}
