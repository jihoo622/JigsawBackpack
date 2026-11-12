using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryService : MonoBehaviour
{
    const int PERCENT_SCALE = 100;
    static string AnimalTag => BasicData.Text("rules.PuzzleInventoryService.animalTag");
    static string AnimalWastePieceId => BasicData.Text("rules.PuzzleInventoryService.animalWastePieceId");
    static string ExpiredFoodPieceId => BasicData.Text("rules.PuzzleInventoryService.expiredFoodPieceId");

    TableManager tables;
    Dictionary<string, PuzzleInventory> markets;
    Dictionary<string, PuzzleItemDefinition> definitions;
    IReadOnlyList<PuzzleSynergyRuleData> marketRules;
    int lastInstanceId;
    string currentCityId;
    readonly HashSet<string> visitedCities = new HashSet<string>(StringComparer.Ordinal);
    readonly List<TavernRumor> tavernRumors = new List<TavernRumor>();
    int lastRumorVisit = -1;
    readonly System.Random stockRandom = new System.Random();

    internal readonly struct TavernRumor
    {
        public readonly string TargetCityId;
        public readonly string SourceCityId;
        public readonly string Message;
        public readonly long RecordedMinutes;

        public TavernRumor(string _targetCityId, string _sourceCityId, string _message, long _recordedMinutes)
        {
            TargetCityId = _targetCityId;
            SourceCityId = _sourceCityId;
            Message = _message;
            RecordedMinutes = _recordedMinutes;
        }
    }

    internal IReadOnlyList<TavernRumor> TavernRumors => tavernRumors;

    internal bool HasRumorForCity(string _cityId)
    {
        return tavernRumors.Any(_rumor => string.Equals(_rumor.TargetCityId, _cityId, StringComparison.Ordinal));
    }

    internal void RecordTavernRumor(string _targetCityId, string _message, string _tag)
    {
        if (lastRumorVisit != CityMoveCount)
        {
            tavernRumors.Add(new TavernRumor(_targetCityId, CurrentCityId, _message, Time.ElapsedMinutes));
            lastRumorVisit = CityMoveCount;
            GameEvents.RaiseTavernRumorRecorded(this, CurrentCityId, _targetCityId, _message, Time.ElapsedMinutes, _tag);
        }
    }

    internal bool HasVisitedCity(string _cityId)
    {
        return visitedCities.Contains(_cityId);
    }

    readonly List<PuzzleInventory> trainWagons = new List<PuzzleInventory>();
    public IReadOnlyList<PuzzleInventory> TrainWagons => trainWagons;
    public int CurrentWagonIndex { get; private set; }
    public PuzzleInventory CurrentTrainWagon => (trainWagons.Count > CurrentWagonIndex && CurrentWagonIndex >= 0) ? trainWagons[CurrentWagonIndex] : null;
    public PuzzleInventory Train => CurrentTrainWagon;
    public int WagonCount => trainWagons.Count;
    public long NextWagonCost => tables.PuzzleTrade.wagonAddCostBase;
    public bool CanAddWagon => RunStatus == PuzzleRunStatus.InProgress && Funds >= NextWagonCost;

    public int TotalTrainOccupiedCells
    {
        get
        {
            int total = 0;
            for (int i = 0; i < trainWagons.Count; i++)
            {
                total += trainWagons[i].OccupiedCells;
            }
            return total;
        }
    }

    internal bool IsTrainWagon(PuzzleInventory _inventory)
    {
        return _inventory != null && trainWagons.Contains(_inventory);
    }

    internal string BoardId(PuzzleInventory _inventory)
    {
        int index = trainWagons.IndexOf(_inventory);
        return index >= 0 ? "train:" + index : _inventory == Market ? "market:" + CurrentCityId : null;
    }

    void RecordAction(PuzzleActionKind _action, string _failure = null, string _targetCityId = null,
        long _requestedMinutes = 0)
    {
        GameEvents.RaisePuzzleActionCommitted(new GameEvents.PuzzleActionInfo(this, _action, _failure,
            _targetCityId, _requestedMinutes: _requestedMinutes));
    }

    void OnDestroy()
    {
        if (Time != null)
        {
            GameEvents.RaisePuzzleRunEnded(this, "scene_unloaded");
        }
    }

    internal bool SelectWagon(int _index)
    {
        if (_index < 0 || _index >= trainWagons.Count || _index == CurrentWagonIndex)
        {
            return false;
        }
        CurrentWagonIndex = _index;
        return true;
    }

    internal bool TryAddWagon(out string _failureReason)
    {
        _failureReason = null;
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            _failureReason = BasicData.Text("rules.wagon.inProgressOnly");
            RecordAction(PuzzleActionKind.AddWagon, "run_finished");
            return false;
        }
        long cost = NextWagonCost;
        if (Funds < cost)
        {
            _failureReason = BasicData.Format("rules.wagon.insufficientFunds", cost);
            RecordAction(PuzzleActionKind.AddWagon, "insufficient_funds");
            return false;
        }

        Funds -= cost;
        var trainRules = tables.SynergiesFor(PuzzleSynergyScope.Train);
        var newWagon = new PuzzleInventory(tables.Puzzle.train, Array.Empty<PuzzleStartingPlacementData>(), definitions, trainRules,
            ref lastInstanceId);
        trainWagons.Add(newWagon);
        CurrentWagonIndex = trainWagons.Count - 1;
        RecordAction(PuzzleActionKind.AddWagon);
        return true;
    }
    public PuzzleInventory Market => markets[CurrentCityId];
    public string CurrentCityId => currentCityId;
    internal PuzzleCityData CityData => tables.CityPrice(CurrentCityId);
    public long Funds { get; private set; }
    public long GameClearCost => tables.PuzzleTrade.gameClearCost;
    /// <summary>이 세션의 게임 시계. Initialize 이후 조회 가능하며 씬 재시작 시 초기화된다.</summary>
    public PuzzleTimeManager Time { get; private set; }
    public bool IsMarketOpen => Time != null && !Time.IsNight && RunStatus == PuzzleRunStatus.InProgress;
    public bool CanWaitForDaylight => Time != null && Time.IsNight && RunStatus == PuzzleRunStatus.InProgress;
    public string PreviewCityId { get; private set; }
    internal long? PreviewMinutes => !string.IsNullOrEmpty(PreviewCityId) &&
        tables.TryGetRoute(CurrentCityId, PreviewCityId, out var route)
        ? PuzzleTimeManager.TravelMinutes(route, tables.PuzzleTime) : (long?)null;

    internal long SynergyActivationsFor(long _minutes)
    {
        long interval = (long)tables.PuzzleSynergy.activationIntervalHours * PuzzleTimeManager.MINUTES_PER_HOUR;
        return Time.IntervalCountAfter(_minutes, interval);
    }

    internal void SetPreviewDestination(string _cityId)
    {
        if (_cityId != null && !tables.HasRoute(CurrentCityId, _cityId))
        {
            return;
        }
        if (PreviewCityId == _cityId)
        {
            return;
        }
        PreviewCityId = _cityId;
        GameEvents.RaiseTravelPreviewChanged(this);
    }

    internal bool WaitForDaylight()
    {
        if (!CanWaitForDaylight)
        {
            RecordAction(PuzzleActionKind.WaitForDaylight, "unavailable");
            return false;
        }
        return AdvanceMinutes(Time.MinutesUntilDaylight, PuzzleActionKind.WaitForDaylight);
    }
    public string DebtCityId => tables.PuzzleTrade.debtCityId;
    public bool CanClearGame => tables != null && RunStatus == PuzzleRunStatus.InProgress && Funds >= GameClearCost
        && !Time.IsOverdue && (string.IsNullOrEmpty(DebtCityId) || CurrentCityId == DebtCityId);
    public int CityMoveCount { get; private set; }
    public PuzzleRunStatus RunStatus { get; private set; }
    internal string CurrentCityName => CityData.displayName;
    internal bool CanTravel => tables != null && !string.IsNullOrEmpty(currentCityId) && tables.RoutesFrom(currentCityId).Count > 0;
    internal long ElapsedTime => Time.ElapsedMinutes;
    internal PuzzleRegionData RegionData => tables.Region(CityData.regionId);
    internal long MarketCapacity { get; private set; }
    internal IReadOnlyCollection<PuzzleItemDefinition> ItemDefinitions { get; private set; }

    internal void Initialize()
    {
        if (Train != null)
        {
            return;
        }

        var validatedTables = TableManager.Instance.RequireValid();
        definitions = new Dictionary<string, PuzzleItemDefinition>(StringComparer.Ordinal);
        foreach (var item in validatedTables.Pieces)
        {
            definitions.Add(item.id, new PuzzleItemDefinition(item));
        }

        tables = validatedTables;
        Time = new PuzzleTimeManager(tables.PuzzleTrade.deadlineHours, tables.PuzzleTime);
        lastInstanceId = 0;
        var trainRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Train);
        marketRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Market);
        var train = new PuzzleInventory(validatedTables.Puzzle.train, validatedTables.TrainStartingPlacements, definitions, trainRules,
            ref lastInstanceId);
        // 시작 지급 화물도 처음부터 소유 중이므로 게임 시작을 보유 시간의 기준으로 삼는다.
        foreach (var item in train.Placements)
        {
            item.SetOwned(true);
        }
        var cityMarkets = new Dictionary<string, PuzzleInventory>(StringComparer.Ordinal);
        long marketCapacity = 0;
        foreach (var city in validatedTables.Cities)
        {
            var market = CreateMarket(city.id);
            cityMarkets.Add(city.id, market);
            marketCapacity = Math.Max(marketCapacity, (long)market.Width * market.Height);
        }

        markets = cityMarkets;
        ItemDefinitions = definitions.Values;
        MarketCapacity = marketCapacity;
        currentCityId = tables.Puzzle.startCity;
        visitedCities.Clear();
        visitedCities.Add(currentCityId);
        tavernRumors.Clear();
        lastRumorVisit = -1;
        Funds = tables.PuzzleTrade.startingFunds;
        CityMoveCount = 0;
        RunStatus = PuzzleRunStatus.InProgress;
        trainWagons.Clear();
        trainWagons.Add(train);
        CurrentWagonIndex = 0;
        GameEvents.RaisePuzzleRunStarted(this);
    }

    // 도시 시장의 유일한 생성 경로. 처음 만들 때와 떠난 도시를 되돌릴 때 같은 경로를 쓴다.
    // 시작 배치로 새 인스턴스를 발급하므로 인스턴스 ID는 세션 동안 계속 증가하고 과거 ID는 재사용하지 않는다.
    PuzzleInventory CreateMarket(string _cityId)
    {
        var city = tables.CityPrice(_cityId);
        var placements = new List<PuzzleStartingPlacementData>(tables.MarketStartingPlacements(_cityId));
        AddStock(_cityId, tables.StockProfile(city.regionalStockId), placements);
        AddStock(_cityId, tables.StockProfile(city.generalStockId), placements);
        return new PuzzleInventory(city.market, placements, definitions, marketRules,
            ref lastInstanceId);
    }

    void AddStock(string _cityId, PuzzleMarketStockData _stock, List<PuzzleStartingPlacementData> _placements)
    {
        var candidates = new List<string>(_stock.pieceIds);
        candidates.Sort(StringComparer.Ordinal);
        var slots = new List<PuzzleMarketStockSlotData>(_stock.slots);
        slots.Sort((_left, _right) => StringComparer.Ordinal.Compare(_left.id, _right.id));
        foreach (var slot in slots)
        {
            _placements.Add(new PuzzleStartingPlacementData
            {
                id = _cityId + "." + slot.id,
                pieceId = candidates[stockRandom.Next(candidates.Count)],
                x = slot.x,
                y = slot.y,
                clockwiseTurns = 0
            });
        }
    }

    // 기본 요금·거리 요금·출발 직전 전체 기차 적재 요금을 같은 경로에서 평가한다.
    internal PuzzleTravelResult EvaluateTravel(string _cityId)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.Blocked);
        }

        if (!CanTravel)
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.NotAvailable);
        }

        string fromCityId = CurrentCityId;
        if (string.IsNullOrEmpty(_cityId) || !tables.HasCity(_cityId))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.InvalidCity, _fromCityId: fromCityId, _toCityId: _cityId);
        }

        if (string.Equals(_cityId, fromCityId, StringComparison.Ordinal))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.SameCity, _fromCityId: fromCityId, _toCityId: _cityId);
        }

        if (!tables.TryGetRoute(fromCityId, _cityId, out var route))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.MissingRoute, _fromCityId: fromCityId, _toCityId: _cityId);
        }

        int cells = TotalTrainOccupiedCells;
        if (!TryTravelCost(route.cost, route.distance, tables.PuzzleRoute.costPerDistance,
            cells, tables.PuzzleRoute.costPerCell, out long cost, out long distanceCost))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.CostOverflow, _fromCityId: fromCityId, _toCityId: _cityId,
                _baseCost: route.cost, _cells: cells, _distance: route.distance);
        }

        long travelTime;
        try
        {
            travelTime = PuzzleTimeManager.TravelMinutes(route, tables.PuzzleTime);
            Time.ArrivalAt(travelTime);
        }
        catch (OverflowException)
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.TimeOverflow);
        }
        foreach (var wagon in trainWagons)
        {
            if (!wagon.CanApplySynergyActivations(SynergyActivationsFor(travelTime)))
            {
                return new PuzzleTravelResult(PuzzleTravelFailure.TimeOverflow);
            }
            if (wagon.Placements.Any(_item => _item.OwnedMinutes >= 0 && _item.OwnedMinutes > long.MaxValue - travelTime))
            {
                return new PuzzleTravelResult(PuzzleTravelFailure.TimeOverflow);
            }
        }

        return new PuzzleTravelResult(Funds < cost ? PuzzleTravelFailure.InsufficientFunds : PuzzleTravelFailure.None,
            cost, fromCityId, _cityId, _baseCost: route.cost, _cells: cells,
            _distance: route.distance, _distanceCost: distanceCost, _travelMinutes: travelTime, _fundsAfter: Funds >= cost ? Funds - cost : Funds,
            _arrivalClockMinutes: (int)((Time.MinuteOfDay + travelTime % PuzzleTimeManager.MINUTES_PER_DAY) % PuzzleTimeManager.MINUTES_PER_DAY),
            _arrivalIsNight: Time.IsNightAt(Time.ArrivalAt(travelTime)));
    }

    // 이동 버튼에 표시할 목적지 노선의 비용. 기차 보유 칸이 바뀌면 값도 바뀐다.
    internal long? NextRouteCost(string _cityId)
    {
        var evaluation = EvaluateTravel(_cityId);
        return evaluation.Failure == PuzzleTravelFailure.None || evaluation.Failure == PuzzleTravelFailure.InsufficientFunds
            ? (long?)evaluation.Cost
            : null;
    }

    // 기본 요금 + 거리 × 거리 단가 + 전체 기차 점유 칸 × 칸당 단가.
    static bool TryTravelCost(long _baseCost, int _distance, int _costPerDistance,
        int _cells, int _costPerCell, out long _cost, out long _distanceCost)
    {
        _cost = 0;
        _distanceCost = 0;
        try
        {
            _distanceCost = checked((long)_distance * _costPerDistance);
            _cost = checked(_baseCost + _distanceCost + (long)_cells * _costPerCell);
        }
        catch (OverflowException)
        {
            return false;
        }

        return true;
    }

    internal bool TryClearGame()
    {
        if (!CanClearGame)
        {
            string failure = RunStatus != PuzzleRunStatus.InProgress ? "run_finished"
                : Time.IsOverdue ? "overdue"
                : !string.IsNullOrEmpty(DebtCityId) && CurrentCityId != DebtCityId ? "wrong_city"
                : "insufficient_funds";
            RecordAction(PuzzleActionKind.Repay, failure);
            return false;
        }

        Funds -= GameClearCost;
        RunStatus = PuzzleRunStatus.Won;
        RecordAction(PuzzleActionKind.Repay);
        return true;
    }

    /// <summary>
    /// 행동 확정 후 게임 시간을 진행하고 기한 초과 시 패배를 확정한 뒤 TimeAdvanced를 발행한다.
    /// 이동은 목적지를 확정한 뒤 호출하며, 보유 시간을 갱신한 후 이벤트를 발행한다.
    /// </summary>
    /// <param name="_minutes">추가 경과 분. 0은 이벤트를 발행하지 않는다.</param>
    /// <returns>진행 중이며 시계가 초기화됐으면 true, 그 외에는 변경 없이 false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">진행 가능한 상태에서 시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">누적 시간이 long 범위를 초과할 때 발생한다.</exception>
    public bool AdvanceMinutes(long _minutes, PuzzleActionKind _action = PuzzleActionKind.AdvanceTime)
    {
        bool advanced;
        try
        {
            advanced = ApplyMinutes(_minutes);
        }
        catch (ArgumentOutOfRangeException)
        {
            RecordAction(_action, "invalid_duration", _requestedMinutes: _minutes);
            throw;
        }
        catch (OverflowException)
        {
            RecordAction(_action, "time_overflow", _requestedMinutes: _minutes);
            throw;
        }
        if (_minutes > 0 || !advanced)
        {
            RecordAction(_action, advanced ? null : "time_blocked", _requestedMinutes: _minutes);
        }
        return advanced;
    }

    bool ApplyMinutes(long _minutes)
    {
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            return false;
        }
        long previous = Time.ElapsedMinutes;
        Time.ArrivalAt(_minutes);
        long activations = SynergyActivationsFor(_minutes);
        foreach (var wagon in trainWagons)
        {
            if (!wagon.CanApplySynergyActivations(activations))
            {
                return false;
            }
            foreach (var item in wagon.Placements)
            {
                if (item.OwnedMinutes >= 0)
                {
                    _ = checked(item.OwnedMinutes + _minutes);
                }
            }
        }
        var currentWagon = Train;
        decimal? cargoValueBeforeSettlement = activations > 0
            ? currentWagon.Placements.Sum(item => item.FreshnessAdjustedValue) : null;
        foreach (var wagon in trainWagons)
        {
            if (!wagon.ApplySynergyActivations(activations) && wagon == currentWagon)
            {
                cargoValueBeforeSettlement = null;
            }
        }
        Time.Advance(_minutes);
        definitions.TryGetValue(ExpiredFoodPieceId, out var waste);
        foreach (var wagon in trainWagons)
        {
            foreach (var item in wagon.Placements)
            {
                item.ApplyElapsedMinutes(_minutes);
            }
            if (_minutes > 0 && waste != null)
            {
                wagon.ReplaceExpiredFood(waste);
            }
        }
        if (Time.IsOverdue)
        {
            RunStatus = PuzzleRunStatus.Lost;
        }
        if (_minutes > 0)
        {
            SpawnAnimalWaste(previous, Time.ElapsedMinutes);
            GameEvents.RaiseTimeAdvanced(previous, Time.ElapsedMinutes, this, cargoValueBeforeSettlement);
        }
        return true;
    }

    /// <summary>
    /// 하루가 넘어갔을 때 기차 인벤토리의 Animal 태그 마리수만큼 똥을 빈 칸에 생성한다.
    /// 여러 날을 한 번에 넘어도 이번 호출에서는 1회만 생성하며, 빈 칸이 없으면 생성하지 않는다.
    /// </summary>
    void SpawnAnimalWaste(long _previousMinutes, long _elapsedMinutes)
    {
        if (RunStatus != PuzzleRunStatus.InProgress ||
            Time.DayAt(_elapsedMinutes) == Time.DayAt(_previousMinutes))
        {
            return;
        }
        if (!definitions.TryGetValue(AnimalWastePieceId, out var waste))
        {
            return;
        }

        int animals = 0;
        for (int i = 0; i < trainWagons.Count; i++)
        {
            foreach (var item in trainWagons[i].Placements)
            {
                if (item.Definition.Tags.Contains(AnimalTag))
                {
                    animals++;
                }
            }
        }

        for (int i = 0; i < animals; i++)
        {
            bool placed = false;
            for (int wagon = 0; wagon < trainWagons.Count; wagon++)
            {
                if (trainWagons[wagon].TryAutoPlace(waste, checked(++lastInstanceId), out _))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                break;
            }
        }
    }

    /// <summary>정수 시간 호출자를 위한 분 단위 시계 어댑터다.</summary>
    public bool AdvanceHours(long _hours)
    {
        return AdvanceMinutes(checked(_hours * PuzzleTimeManager.MINUTES_PER_HOUR));
    }

    internal PuzzleTravelResult Travel(string _cityId)
    {
        var evaluation = EvaluateTravel(_cityId);
        if (!evaluation.IsSuccess)
        {
            RecordAction(PuzzleActionKind.Travel, evaluation.Failure.ToString(), _targetCityId: _cityId);
            return evaluation;
        }

        long duration = evaluation.TravelMinutes;
        // 잘못된 시간이나 오버플로는 자금·배치·도시 변경 전에 검사한다.
        Time.ArrivalAt(duration);
        // 이동 비용은 출발 전에 낸다. 기차는 출발 직전까지의 배치로 계산한다. 이후의 도시 도착·턴 증가·가치 변화는 한 번에 확정한다.
        Funds -= evaluation.Cost;
        // 떠난 도시의 시장은 테이블의 시작 배치로 되돌린다. 기차는 그대로 두고, 목적지 시장은 손대지 않는다.
        markets[evaluation.FromCityId] = CreateMarket(evaluation.FromCityId);
        currentCityId = evaluation.ToCityId;
        CityMoveCount++;
        visitedCities.Add(CurrentCityId);
        Market.RecalculateValue();
        ApplyMinutes(duration);
        RecordAction(PuzzleActionKind.Travel, _targetCityId: _cityId);
        SetPreviewDestination(null);
        GameEvents.RaiseCityArrived(CurrentCityId, CityMoveCount);

        return new PuzzleTravelResult(PuzzleTravelFailure.None, evaluation.Cost, evaluation.FromCityId,
            evaluation.ToCityId, Funds, evaluation.BaseCost, evaluation.Cells, evaluation.Distance, evaluation.DistanceCost,
            evaluation.TravelMinutes, evaluation.ArrivalClockMinutes, evaluation.ArrivalIsNight);
    }

    internal PuzzleDropResult EvaluateDrop(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination));
        }

        var source = _original?.Owner;
        var market = Market;
        bool sourceIsTrain = IsTrainWagon(source);
        bool destIsTrain = IsTrainWagon(_destination);
        if ((source == market || _destination == market) && !IsMarketOpen)
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.MarketClosed),
                _tradeFailure: PuzzleTradeFailure.MarketClosed);
        }
        if ((!sourceIsTrain && source != market) || (!destIsTrain && _destination != market))
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination));
        }

        var placement = source.EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (source == _destination || (sourceIsTrain && destIsTrain))
        {
            return new PuzzleDropResult(placement);
        }

        bool buying = source == market;
        var direction = buying ? PuzzleTradeDirection.Buy : PuzzleTradeDirection.Sell;
        if (placement.Failure == PuzzlePlacementFailure.StaleSource)
        {
            return new PuzzleDropResult(placement, direction);
        }

        var city = tables.CityPrice(CurrentCityId);
        bool enabled = buying ? city.buyEnabled : city.sellEnabled;
        var rate = RateFor(city, _original.Definition);
        int percent = rate.PercentFor(direction);
        string tag = rate.Tag;
        long price;
        if (!TryQuoteTrade(_original, direction, out price))
        {
            return new PuzzleDropResult(placement, direction, _tradeFailure: PuzzleTradeFailure.PriceOverflow,
                _percent: percent, _tag: tag);
        }

        var failure = PuzzleTradeFailure.None;
        if (!enabled)
        {
            failure = PuzzleTradeFailure.Disabled;
        }
        else if (buying)
        {
            if (price > Funds)
            {
                failure = PuzzleTradeFailure.InsufficientFunds;
            }
        }
        else if (price < 0)
        {
            // 음수 판매는 처분 비용이다. 지불할 자금이 부족하면 판매할 수 없다.
            if (Funds < -price)
            {
                failure = PuzzleTradeFailure.InsufficientFunds;
            }
        }
        else if (Funds > long.MaxValue - price)
        {
            failure = PuzzleTradeFailure.BalanceOverflow;
        }
        return new PuzzleDropResult(placement, direction, price, failure, percent, tag);
    }

    // 현재 도시에서 이 아이템에 적용되는 태그 규칙과 구매·판매 비율.
    internal PuzzleTradeRate RateFor(PuzzleItemDefinition _item)
    {
        return RateFor(tables.CityPrice(CurrentCityId), _item);
    }

    PuzzleTradeRate RateFor(PuzzleCityData _city, PuzzleItemDefinition _item)
    {
        var best = BestTagRule(_item);
        return best == null
            ? new PuzzleTradeRate(null, _city.buyPercentOfBaseValue, _city.sellPercentOfFinalValue)
            : new PuzzleTradeRate(best.tag, best.buyPercent, best.sellPercent);
    }

    // 아이템 태그와 맞는 규칙 중 priority가 가장 높은 하나를 고른다. 맞는 규칙이 없으면 null.
    PuzzleCityTagPriceData BestTagRule(PuzzleItemDefinition _item)
    {
        PuzzleCityTagPriceData best = null;
        foreach (string tag in _item.Tags)
        {
            if (tables.TryGetCityTagPrice(CurrentCityId, tag, out var rule) &&
                (best == null || rule.priority > best.priority))
            {
                best = rule;
            }
        }
        return best;
    }

    // 툴팁과 실제 거래가 공유한다. 배치·자금 검사 없이 현재 도시의 정확한 금액만 산출한다.
    internal bool TryQuoteTrade(PuzzlePlacement _item, PuzzleTradeDirection _direction, out long _price)
    {
        _price = 0;
        if (_item == null || _direction == PuzzleTradeDirection.None)
        {
            return false;
        }
        var rate = RateFor(_item.Definition);
        decimal value = _direction == PuzzleTradeDirection.Buy ? _item.Definition.BaseValue : _item.FreshnessAdjustedValue;
        return TryGetTradePrice(value, rate.PercentFor(_direction), RegionData.pricePercent, out _price);
    }

    internal decimal PreviewCargoValue(PuzzlePlacement _item, long _minutes, long _activations)
    {
        int? freshness = _item.FreshnessPercentAfter(_minutes);
        if (freshness == 0 && _item.RemainingShelfLifeMinutes.HasValue)
        {
            return definitions[ExpiredFoodPieceId].BaseValue;
        }
        var value = _item.Owner.ValueOf(_item.InstanceId);
        decimal settledValue = checked(_item.ActualValue + (value.FinalValue - value.BaseValue) * _activations);
        return PuzzlePlacement.ApplyFreshnessToValue(settledValue, freshness);
    }

    static bool TryGetTradePrice(decimal _value, int _percent, int _regionPercent, out long _price)
    {
        _price = 0;
        // 음수 값(처분 비용)도 허용한다. 비율은 양수만 유효하다.
        if (_percent <= 0 || _regionPercent <= 0)
        {
            return false;
        }

        decimal rate = (decimal)_percent / PERCENT_SCALE * _regionPercent / PERCENT_SCALE;
        if (Math.Abs(_value) > ((decimal)long.MaxValue + 1) / rate)
        {
            return false;
        }

        decimal rounded = Math.Round(_value * rate, MidpointRounding.AwayFromZero);
        if (rounded > long.MaxValue || rounded < long.MinValue)
        {
            return false;
        }

        _price = (long)rounded;
        return true;
    }

    // 현재 회전을 우선하고, 맞지 않으면 시계 방향으로 나머지 회전을 검사한다.
    // 후보 탐색은 상태를 바꾸지 않으며 확정은 드래그와 같은 거래 경로를 사용한다.
    internal PuzzleDropResult QuickTransfer(PuzzlePlacement _original)
    {
        if (_original == null)
        {
            return QuickTransferTo(null, null, 0);
        }

        var destination = IsTrainWagon(_original.Owner) ? Market : Train;
        return QuickTransferTo(_original, destination, _original.Pose.ClockwiseTurns);
    }

    internal PuzzleDropResult QuickTransferToWagon(PuzzlePlacement _original, int _wagonIndex, int _preferredClockwiseTurns)
    {
        if (_wagonIndex < 0 || _wagonIndex >= trainWagons.Count)
        {
            return QuickTransferTo(_original, null, _preferredClockwiseTurns);
        }

        return QuickTransferTo(_original, trainWagons[_wagonIndex], _preferredClockwiseTurns);
    }

    internal PuzzleDropResult QuickTransferTo(PuzzlePlacement _original, PuzzleInventory _destination, int _preferredClockwiseTurns)
    {
        var result = FindQuickTransfer(_original, _destination, _preferredClockwiseTurns);
        RecordDropAction(PuzzleActionKind.QuickTransfer, result, _original, _destination,
            Vector2Int.zero, Vector2Int.zero, _preferredClockwiseTurns);
        return result;
    }

    PuzzleDropResult FindQuickTransfer(PuzzlePlacement _original, PuzzleInventory _destination, int _preferredClockwiseTurns)
    {
        if (_original == null)
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.StaleSource));
        }
        if (_destination == null)
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination));
        }

        int startRotation = ((_preferredClockwiseTurns % PuzzleRotation.TURN_COUNT) + PuzzleRotation.TURN_COUNT) % PuzzleRotation.TURN_COUNT;
        var grabbedCell = _original.Definition.Cells[0];
        var evaluation = EvaluateDrop(_original, _destination, grabbedCell, Vector2Int.zero, startRotation);
        if (evaluation.TradeFailure != PuzzleTradeFailure.None ||
            evaluation.Placement.Failure == PuzzlePlacementFailure.StaleSource ||
            evaluation.Placement.Failure == PuzzlePlacementFailure.InvalidDestination)
        {
            return evaluation;
        }

        for (int turn = 0; turn < PuzzleRotation.TURN_COUNT; turn++)
        {
            int rotation = (startRotation + turn) % PuzzleRotation.TURN_COUNT;
            var offset = new PuzzleRotation(_original.Definition.Cells, rotation).GetBoardOffset(grabbedCell);
            for (int y = _destination.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < _destination.Width; x++)
                {
                    var targetCell = new Vector2Int(x, y) + offset;
                    var candidate = EvaluateDrop(_original, _destination, grabbedCell, targetCell, rotation);
                    if (candidate.CanDrop)
                    {
                        return ApplyDrop(_original, _destination, grabbedCell, targetCell, rotation);
                    }
                }
            }
        }

        return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.NoSpace), evaluation.Direction,
            evaluation.Price, _percent: evaluation.AppliedPercent, _tag: evaluation.AppliedTag);
    }

    internal PuzzleDropResult Drop(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        var result = ApplyDrop(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        RecordDropAction(PuzzleActionKind.Place, result, _original, _destination, _grabbedCell,
            _targetCell, _clockwiseTurns);
        return result;
    }

    void RecordDropAction(PuzzleActionKind _action, PuzzleDropResult _result, PuzzlePlacement _original,
        PuzzleInventory _destination, Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        string failure = _result.CanDrop ? null
            : _result.TradeFailure != PuzzleTradeFailure.None ? "trade." + _result.TradeFailure
            : "placement." + _result.Placement.Failure;
        GameEvents.RaisePuzzleActionCommitted(new GameEvents.PuzzleActionInfo(this, _action, failure,
            _instanceId: _original?.InstanceId ?? 0, _destination: BoardId(_destination),
            _targetCell: _targetCell, _grabbedCell: _grabbedCell, _clockwiseTurns: _clockwiseTurns));
    }

    PuzzleDropResult ApplyDrop(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        var result = EvaluateDrop(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (!result.CanDrop)
        {
            return result;
        }

        var move = _original.Owner.Move(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (!move.CanPlace)
        {
            return new PuzzleDropResult(move, result.Direction, result.Price, _percent: result.AppliedPercent,
                _tag: result.AppliedTag);
        }

        if (result.Direction != PuzzleTradeDirection.None)
        {
            // Move는 새 배치 객체를 만들므로 이동 완료된 인스턴스의 구매 시계를 갱신한다.
            _destination.TryGetCell(move.Candidate.Cells[0].Position, out var movedItem, out _);
            movedItem.SetOwned(result.Direction == PuzzleTradeDirection.Buy);
            Funds += result.Direction == PuzzleTradeDirection.Buy ? -result.Price : result.Price;
            GameEvents.RaiseTradeCompleted(new GameEvents.TradeInfo(CurrentCityId, _original.Definition.Id,
                _original.InstanceId, result.Direction, result.Price, Funds, move.Candidate.Origin,
                move.Candidate.ClockwiseTurns));
        }
        return result;
    }
}

public enum PuzzleRunStatus
{
    InProgress,
    Won,
    Lost
}

internal enum PuzzleTravelFailure
{
    None,
    Blocked,
    NotAvailable,
    SameCity,
    InvalidCity,
    MissingRoute,
    InsufficientFunds,
    CostOverflow,
    TimeOverflow
}

// 이동 미리보기와 확정의 공통 결과. 실패하면 도시·턴·시장·자금 어느 것도 바뀌지 않는다.
internal readonly struct PuzzleTravelResult
{
    internal PuzzleTravelFailure Failure { get; }
    internal long Cost { get; }
    // 미리보기와 확정에서 공유하는 비용 내역.
    internal long BaseCost { get; }
    internal int Distance { get; }
    internal long TravelMinutes { get; }
    internal decimal TravelTime => TravelMinutes / (decimal)PuzzleTimeManager.MINUTES_PER_HOUR;
    internal int ArrivalClockMinutes { get; }
    internal bool ArrivalIsNight { get; }
    internal long DistanceCost { get; }
    internal int Cells { get; }
    internal long FundsAfter { get; }
    internal string FromCityId { get; }
    internal string ToCityId { get; }
    internal bool IsSuccess => Failure == PuzzleTravelFailure.None;

    internal PuzzleTravelResult(PuzzleTravelFailure _failure, long _cost = 0, string _fromCityId = null,
        string _toCityId = null, long _fundsAfter = 0, long _baseCost = 0, int _cells = 0,
        int _distance = 0, long _distanceCost = 0, long _travelMinutes = 0, int _arrivalClockMinutes = 0, bool _arrivalIsNight = false)
    {
        Failure = _failure;
        Cost = _cost;
        FromCityId = _fromCityId;
        ToCityId = _toCityId;
        FundsAfter = _fundsAfter;
        BaseCost = _baseCost;
        Distance = _distance;
        TravelMinutes = _travelMinutes;
        ArrivalClockMinutes = _arrivalClockMinutes;
        ArrivalIsNight = _arrivalIsNight;
        DistanceCost = _distanceCost;
        Cells = _cells;
    }
}

public enum PuzzleTradeDirection
{
    None,
    Buy,
    Sell
}

internal enum PuzzleTradeFailure
{
    None,
    Disabled,
    InsufficientFunds,
    PriceOverflow,
    BalanceOverflow,
    MarketClosed
}

internal readonly struct PuzzleTradeRate
{
    internal string Tag { get; }
    internal int BuyPercent { get; }
    internal int SellPercent { get; }
    internal PuzzleTradeRate(string _tag, int _buyPercent, int _sellPercent)
    {
        Tag = _tag;
        BuyPercent = _buyPercent;
        SellPercent = _sellPercent;
    }

    internal int PercentFor(PuzzleTradeDirection _direction)
    {
        return _direction == PuzzleTradeDirection.Buy ? BuyPercent : SellPercent;
    }
}

internal readonly struct PuzzleDropResult
{
    internal PuzzlePlacementResult Placement { get; }
    internal PuzzleTradeDirection Direction { get; }
    internal long Price { get; }
    internal PuzzleTradeFailure TradeFailure { get; }
    // 적용된 도시·태그 비율. 태그가 없으면 기본 비율이다.
    internal int AppliedPercent { get; }
    internal string AppliedTag { get; }
    internal bool CanDrop => Placement.CanPlace && TradeFailure == PuzzleTradeFailure.None;

    internal PuzzleDropResult(PuzzlePlacementResult _placement, PuzzleTradeDirection _direction = PuzzleTradeDirection.None,
        long _price = 0, PuzzleTradeFailure _tradeFailure = PuzzleTradeFailure.None, int _percent = 0, string _tag = null)
    {
        Placement = _placement;
        Direction = _direction;
        Price = _price;
        TradeFailure = _tradeFailure;
        AppliedPercent = _percent;
        AppliedTag = _tag;
    }
}
