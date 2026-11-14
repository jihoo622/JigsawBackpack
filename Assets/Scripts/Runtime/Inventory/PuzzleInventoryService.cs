using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class PuzzleInventoryService : MonoBehaviour
{
    const int PERCENT_SCALE = 100;
    const string FARM_REGION_ID = "region.farm";
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
    System.Random stockRandom = new System.Random();
    // 정제 산출 수량 전용 난수원. 시장 재고의 stockRandom 순서를 바꾸지 않는다.
    System.Random refiningRandom = new System.Random();

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
    Dictionary<(int First, int Second), BreedingState> breedingProgress = new();
    // 동물 인스턴스 ID별 배설 진행. 대기 상태를 포함해 씬 안에서만 살며 플레이어에게 표시하지 않는다.
    Dictionary<int, WasteState> wasteProgress = new();
    // 도시별 도축대. 행동이 없는 도시는 항목을 만들지 않는다.
    readonly Dictionary<string, PuzzleButcheryState> butcheries = new Dictionary<string, PuzzleButcheryState>(StringComparer.Ordinal);
    // 도시별 정제대. 도축과 독립적인 보드·진행 상태를 가지며 행동이 없는 도시는 항목을 만들지 않는다.
    readonly Dictionary<string, PuzzleRefiningState> refinings = new Dictionary<string, PuzzleRefiningState>(StringComparer.Ordinal);
    readonly Dictionary<string, PuzzleModificationState> modifications = new Dictionary<string, PuzzleModificationState>(StringComparer.Ordinal);
    public IReadOnlyList<PuzzleInventory> TrainWagons => trainWagons;
    public int CurrentWagonIndex { get; private set; }
    public PuzzleInventory CurrentTrainWagon => (trainWagons.Count > CurrentWagonIndex && CurrentWagonIndex >= 0) ? trainWagons[CurrentWagonIndex] : null;
    public PuzzleInventory Train => CurrentTrainWagon;
    public int WagonCount => trainWagons.Count;
    public long NextWagonCost => tables.PuzzleTrade.wagonAddCostBase;
    public bool CanAddWagon => RunStatus == PuzzleRunStatus.InProgress
        && (tables.PuzzleTrade.maxWagonCount <= 0 || trainWagons.Count < tables.PuzzleTrade.maxWagonCount)
        && Funds >= NextWagonCost;
    internal long WagonRefrigerationCost => tables.PuzzleTrade.wagonRefrigerationCost;
    internal bool HasWagonRefrigeration => Train?.HasWagonModification(PuzzleWagonRefrigeration.ModificationId) == true;
    internal bool CanInstallWagonRefrigeration => RunStatus == PuzzleRunStatus.InProgress && Train != null &&
        !HasWagonRefrigeration && Funds >= WagonRefrigerationCost;

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

    internal bool TryInstallWagonModification(PuzzleInventory _wagon, PuzzleWagonModification _modification)
    {
        return RunStatus == PuzzleRunStatus.InProgress && IsTrainWagon(_wagon) && _modification != null &&
            _wagon.TryInstallWagonModification(_modification);
    }

    internal bool TryInstallWagonRefrigeration()
    {
        if (!CanInstallWagonRefrigeration ||
            !TryInstallWagonModification(Train,
                PuzzleWagonModificationRegistry.Create(PuzzleWagonRefrigeration.ModificationId, tables)))
        {
            return false;
        }

        long previousFunds = Funds;
        Funds -= WagonRefrigerationCost;
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
    }

    internal string BoardId(PuzzleInventory _inventory)
    {
        return IsTrainWagon(_inventory) ? "train:" + (_inventory.WagonNumber - 1)
            : _inventory == Market ? "market:" + CurrentCityId : null;
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

    internal bool TryMoveWagon(int _sourceIndex, int _destinationIndex)
    {
        if (RunStatus != PuzzleRunStatus.InProgress || _sourceIndex == _destinationIndex ||
            _sourceIndex < 0 || _sourceIndex >= trainWagons.Count ||
            _destinationIndex < 0 || _destinationIndex >= trainWagons.Count)
        {
            return false;
        }

        var selected = Train;
        var wagon = trainWagons[_sourceIndex];
        trainWagons.RemoveAt(_sourceIndex);
        trainWagons.Insert(_destinationIndex, wagon);
        CurrentWagonIndex = trainWagons.IndexOf(selected);
        return true;
    }

    internal bool TryAddWagon(out string _failureReason)
    {
        return TryPurchaseWagon(NextWagonCost, null, out _failureReason);
    }

    internal bool TryPurchaseWagon(string _productId, out string _failureReason)
    {
        var product = tables.PuzzleTrade.wagonProducts.FirstOrDefault(
            _product => string.Equals(_product.id, _productId, StringComparison.Ordinal));
        if (product == null)
        {
            _failureReason = BasicData.Text("rules.wagon.unknownProduct");
            RecordAction(PuzzleActionKind.AddWagon, "unknown_wagon_product");
            return false;
        }

        return TryPurchaseWagon(product.price, product.modificationId, out _failureReason);
    }

    bool TryPurchaseWagon(long _cost, string _modificationId, out string _failureReason)
    {
        _failureReason = null;
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            _failureReason = BasicData.Text("rules.wagon.inProgressOnly");
            RecordAction(PuzzleActionKind.AddWagon, "run_finished");
            return false;
        }
        int maxWagons = tables.PuzzleTrade.maxWagonCount;
        if (maxWagons > 0 && trainWagons.Count >= maxWagons)
        {
            _failureReason = BasicData.Format("rules.wagon.maxReached", maxWagons);
            RecordAction(PuzzleActionKind.AddWagon, "max_wagons");
            return false;
        }
        if (Funds < _cost)
        {
            _failureReason = BasicData.Format("rules.wagon.insufficientFunds", _cost);
            RecordAction(PuzzleActionKind.AddWagon, "insufficient_funds");
            return false;
        }

        var trainRules = tables.SynergiesFor(PuzzleSynergyScope.Train);
        int nextInstanceId = lastInstanceId;
        var newWagon = new PuzzleInventory(tables.Puzzle.train, Array.Empty<PuzzleStartingPlacementData>(), definitions, trainRules,
            ref nextInstanceId);
        if (!string.IsNullOrEmpty(_modificationId) &&
            (!PuzzleWagonModificationRegistry.Contains(_modificationId) ||
             !newWagon.TryInstallWagonModification(PuzzleWagonModificationRegistry.Create(_modificationId, tables))))
        {
            _failureReason = BasicData.Text("rules.wagon.modificationFailed");
            RecordAction(PuzzleActionKind.AddWagon, "wagon_modification_failed");
            return false;
        }

        long previousFunds = Funds;
        newWagon.WagonNumber = trainWagons.Count + 1;
        trainWagons.Add(newWagon);
        Funds -= _cost;
        lastInstanceId = nextInstanceId;
        CurrentWagonIndex = trainWagons.Count - 1;
        GameEvents.RaiseFundsChanged(this, previousFunds);
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

    long SynergyIntervalMinutes => (long)tables.PuzzleSynergy.activationIntervalHours * PuzzleTimeManager.MINUTES_PER_HOUR;

    internal long SynergyActivationsFor(long _minutes)
    {
        return Time.IntervalCountAfter(_minutes, SynergyIntervalMinutes);
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

    // 경제 분석이 같은 시장 재고를 재현할 때 Initialize 전에 호출한다.
    internal void UseStockSeed(int _seed)
    {
        stockRandom = new System.Random(_seed);
        questRandom = new System.Random(_seed);
    }

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
            var market = CreateMarket(city.id, ref lastInstanceId);
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
        ResetQuests();
        ResetProficiency();
        Funds = tables.PuzzleTrade.startingFunds;
        CityMoveCount = 0;
        RunStatus = PuzzleRunStatus.InProgress;
        trainWagons.Clear();
        train.WagonNumber = 1;
        trainWagons.Add(train);
        breedingProgress.Clear();
        wasteProgress.Clear();
        CurrentWagonIndex = 0;
        butcheries.Clear();
        modifications.Clear();
        refinings.Clear();
        IsButcheryOpen = false;
        IsModificationOpen = false;
        IsRefiningOpen = false;
        GameEvents.RaisePuzzleRunStarted(this);
    }

    // 도시 시장의 유일한 생성 경로. 처음 만들 때와 떠난 도시를 되돌릴 때 같은 경로를 쓴다.
    // 시작 배치로 새 인스턴스를 발급하므로 인스턴스 ID는 세션 동안 계속 증가하고 과거 ID는 재사용하지 않는다.
    PuzzleInventory CreateMarket(string _cityId, ref int _lastInstanceId)
    {
        var city = tables.CityPrice(_cityId);
        var placements = new List<PuzzleStartingPlacementData>(tables.MarketStartingPlacements(_cityId));
        AddStock(_cityId, tables.StockProfile(city.regionalStockId), placements);
        AddStock(_cityId, tables.StockProfile(city.generalStockId), placements);
        return new PuzzleInventory(city.market, placements, definitions, marketRules,
            ref _lastInstanceId);
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
        return EvaluateTravel(_cityId, out _);
    }

    PuzzleTravelResult EvaluateTravel(string _cityId, out TimeAdvancePlan _plan)
    {
        _plan = null;
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
        var failure = PrepareTimeAdvance(travelTime, out _plan);
        if (failure == PuzzleTravelFailure.TimeOverflow)
        {
            return new PuzzleTravelResult(failure);
        }

        if (failure == PuzzleTravelFailure.None)
        {
            long replacementStockCount = (long)tables.MarketStartingPlacements(fromCityId).Count +
                tables.StockProfile(CityData.regionalStockId).slots.Length +
                tables.StockProfile(CityData.generalStockId).slots.Length;
            if (CityMoveCount == int.MaxValue || _plan.lastInstanceId + replacementStockCount > int.MaxValue)
            {
                return new PuzzleTravelResult(PuzzleTravelFailure.TimeOverflow);
            }
            if (Funds < cost)
            {
                failure = PuzzleTravelFailure.InsufficientFunds;
            }
        }

        return new PuzzleTravelResult(failure,
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
    internal static bool TryTravelCost(long _baseCost, int _distance, int _costPerDistance,
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

        long previousFunds = Funds;
        Funds -= GameClearCost;
        RunStatus = PuzzleRunStatus.Won;
        GameEvents.RaiseFundsChanged(this, previousFunds);
        RecordAction(PuzzleActionKind.Repay);
        return true;
    }

    public bool AdvanceMinutes(long _minutes, PuzzleActionKind _action = PuzzleActionKind.AdvanceTime)
    {
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            RecordAction(_action, "time_blocked", _requestedMinutes: _minutes);
            return false;
        }
        if (_minutes == 0)
        {
            return true;
        }

        TimeAdvancePlan plan;
        PuzzleTravelFailure failure;
        try
        {
            failure = PrepareTimeAdvance(_minutes, out plan);
        }
        catch (ArgumentOutOfRangeException)
        {
            RecordAction(_action, "invalid_duration", _requestedMinutes: _minutes);
            throw;
        }
        if (failure != PuzzleTravelFailure.None)
        {
            RecordAction(_action, "time_overflow", _requestedMinutes: _minutes);
            return false;
        }

        CommitTimeAdvance(_minutes, plan);
        RecordAction(_action, _requestedMinutes: _minutes);
        return true;
    }

    internal decimal? PreviewValueAfter(PuzzlePlacement _item, long _minutes)
    {
        int wagonIndex = trainWagons.IndexOf(_item.Owner);
        if (wagonIndex < 0 ||
            PrepareTimeAdvance(_minutes, out var plan) != PuzzleTravelFailure.None)
        {
            return null;
        }

        return plan.wagons[wagonIndex].FreshnessAdjustedValueOf(_item.InstanceId);
    }

    sealed class TimeAdvancePlan
    {
        internal readonly PuzzleInventory[] wagons;
        internal Dictionary<(int First, int Second), BreedingState> breedingProgress;
        internal Dictionary<int, WasteState> wasteProgress;
        internal int lastInstanceId;
        internal decimal? cargoValueBeforeSettlement;
        internal decimal? cargoValueBeforeFreshnessLoss;

        internal TimeAdvancePlan(PuzzleInventory[] _wagons,
            Dictionary<(int First, int Second), BreedingState> _breedingProgress,
            Dictionary<int, WasteState> _wasteProgress, int _lastInstanceId)
        {
            wagons = _wagons;
            breedingProgress = _breedingProgress;
            wasteProgress = _wasteProgress;
            lastInstanceId = _lastInstanceId;
        }
    }

    PuzzleTravelFailure PrepareTimeAdvance(long _minutes, out TimeAdvancePlan _plan)
    {
        _plan = null;
        try
        {
            long arrival = Time.ArrivalAt(_minutes);
            // 시간 진행이 거부되거나 미리보기인 경우 원본 객차와 번식·배설 상태를 바꾸지 않는다.
            var wagons = trainWagons.Select(_wagon => _wagon.CopyForTimeAdvance()).ToArray();
            var plan = new TimeAdvancePlan(wagons,
                new Dictionary<(int First, int Second), BreedingState>(breedingProgress),
                new Dictionary<int, WasteState>(wasteProgress), lastInstanceId);
            var animals = tables.PuzzleSynergy.animals;
            var waste = definitions[animals.wastePieceId];
            var expiredFood = definitions[ExpiredFoodPieceId];
            long animalEventsThrough = Time.HasDeadline ? Math.Min(arrival, Time.DeadlineMinutes) : arrival;
            long breedingMinutes = (long)animals.breedingHours * PuzzleTimeManager.MINUTES_PER_HOUR;
            long wasteMinutes = (long)animals.wasteIntervalDays * PuzzleTimeManager.MINUTES_PER_DAY;
            long holdMinutes = (long)animals.completionHoldHours * PuzzleTimeManager.MINUTES_PER_HOUR;
            long synergyInterval = SynergyIntervalMinutes;
            var freshness = tables.PuzzleSynergy.freshness;
            var preservation = CreatePreservation(freshness);
            long freshnessInterval = (long)freshness.settlementIntervalHours * PuzzleTimeManager.MINUTES_PER_HOUR;
            long cursor = Time.ElapsedMinutes;
            var growth = new List<PuzzleGrowth>();
            var selectedWagon = wagons[CurrentWagonIndex];
            bool settledSelectedWagon = false;
            decimal freshnessDelta = 0;
            // 이전 행동으로 공간이 확보된 경우 첫 경계를 기다리지 않고 대기를 푼다. 마지막 대기 시간은 이번 진행부터 다시 흐른다.
            ResumeBlockedStates(plan, wagons, animals, waste, growth, breedingMinutes, wasteMinutes, holdMinutes);

            while (cursor < arrival)
            {
                EnsureWasteStates(plan, wagons, animals, wasteMinutes, cursor);
                var pairs = SelectBreedingPairs(wagons, plan.breedingProgress);
                long step = Math.Min(arrival - cursor, MinutesUntilCargoChange(wagons, preservation));
                step = Math.Min(step, MinutesUntilBirth(pairs, breedingMinutes, animalEventsThrough - cursor));
                step = Math.Min(step, MinutesUntilWaste(wagons, plan.wasteProgress, animals.animalTag, wasteMinutes,
                    animalEventsThrough - cursor));
                long boundary = cursor + step;
                // 같은 시각에는 정산, 배설, 식품 만료, 성장, 출산 순으로 처리한다.
                long activations = boundary / synergyInterval - cursor / synergyInterval;
                foreach (var wagon in wagons)
                {
                    if (wagon.ApplySynergyActivations(activations) && wagon == selectedWagon)
                    {
                        settledSelectedWagon = true;
                    }
                    foreach (var item in wagon.Placements)
                    {
                        bool trackFreshness = wagon == selectedWagon && item.RemainingShelfLifeMinutes.HasValue;
                        decimal valueBefore = trackFreshness ? item.FreshnessAdjustedValue : 0;
                        item.ApplyElapsedMinutes(step, preservation.DecayRate(wagon, item), freshnessInterval);
                        if (trackFreshness)
                        {
                            freshnessDelta += item.FreshnessAdjustedValue - valueBefore;
                        }
                    }
                }

                // 기한을 지난 경계에서는 동물 사건을 되살리지 않는다. 대기 상태도 그대로 둔다.
                bool animalEvents = boundary <= animalEventsThrough;
                if (animalEvents)
                {
                    AdvanceWaste(plan, wagons, animals, waste, wasteMinutes, holdMinutes, step);
                }

                growth.Clear();
                foreach (var wagon in wagons)
                {
                    decimal valueBeforeExpiry = wagon == selectedWagon
                        ? wagon.Placements.Sum(_item => _item.FreshnessAdjustedValue) : 0;
                    wagon.ReplaceExpiredFood(expiredFood);
                    if (wagon == selectedWagon)
                    {
                        freshnessDelta += wagon.Placements.Sum(_item => _item.FreshnessAdjustedValue) - valueBeforeExpiry;
                    }
                    // 공간이 없는 성장은 전체 진행을 거절하지 않고 그 품목만 대기한다.
                    wagon.PlanGrowth(growth);
                }
                foreach (var change in growth)
                {
                    change.Item.Owner.ApplyGrowth(change);
                }

                if (animalEvents)
                {
                    AdvanceBreeding(plan, pairs, step, breedingMinutes, holdMinutes);
                }
                cursor = boundary;
            }

            if (settledSelectedWagon)
            {
                plan.cargoValueBeforeSettlement = Train.Placements.Sum(_item => _item.FreshnessAdjustedValue);
            }
            if (freshnessDelta < 0)
            {
                plan.cargoValueBeforeFreshnessLoss =
                    selectedWagon.Placements.Sum(_item => _item.FreshnessAdjustedValue) - freshnessDelta;
            }
            _plan = plan;
            return PuzzleTravelFailure.None;
        }
        catch (OverflowException)
        {
            return PuzzleTravelFailure.TimeOverflow;
        }
    }

    // 공간 부족으로 대기 중이던 성장·번식·배설을 이번 행동의 시작 시각에서 다시 본다. 자리를 찾으면
    // 완료 시점을 그대로 넘기지 않고 마지막 대기 시간만큼을 이번 진행부터 다시 흘려보낸다.
    void ResumeBlockedStates(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        PuzzleItemDefinition _waste, List<PuzzleGrowth> _growth, long _breedingMinutes, long _wasteMinutes,
        long _holdMinutes)
    {
        foreach (var wagon in _wagons)
        {
            wagon.PlanGrowth(_growth);
        }
        foreach (var change in _growth)
        {
            change.Item.Owner.ApplyGrowth(change);
        }
        _growth.Clear();

        foreach (var pair in SelectBreedingPairs(_wagons, _plan.breedingProgress))
        {
            if (pair.state.SpaceBlocked &&
                pair.wagon.CanAutoPlaceAll(pair.offspring, pair.offspringCount, _allowRotation: true))
            {
                _plan.breedingProgress[pair.key] = new BreedingState(_breedingMinutes - _holdMinutes, false);
            }
        }

        foreach (var wagon in _wagons)
        {
            foreach (var animal in wagon.Placements)
            {
                if (animal.Definition.Tags.Contains(_animals.animalTag) &&
                    _plan.wasteProgress.TryGetValue(animal.InstanceId, out var state) && state.SpaceBlocked &&
                    wagon.CanAutoPlaceAll(_waste, _animals.wasteCountPerAnimal))
                {
                    _plan.wasteProgress[animal.InstanceId] = new WasteState(_wasteMinutes - _holdMinutes, false);
                }
            }
        }
    }

    static long MinutesUntilCargoChange(PuzzleInventory[] _wagons, PuzzleShelfLifePreservation _preservation)
    {
        long minutes = long.MaxValue;
        foreach (var wagon in _wagons)
        {
            foreach (var item in wagon.Placements)
            {
                // 성장 품목은 다음 완료 시각을, 자리를 대기 중이면 재시도 시각을 경계로 제시한다.
                minutes = Math.Min(minutes, Math.Max(1, item.MinutesUntilGrowthCompletion));
                if (item.OwnedMinutes >= 0 && item.RemainingShelfLifeMinutes > 0)
                {
                    minutes = Math.Min(minutes, item.MinutesUntilShelfLifeExpires(_preservation.DecayRate(wagon, item)));
                }
            }
        }
        return minutes;
    }

    internal long? MinutesUntilNextFreshnessBand(PuzzlePlacement _item)
    {
        var freshness = tables.PuzzleSynergy.freshness;
        decimal decayRate = IsTrainWagon(_item.Owner) ? CreatePreservation(freshness).DecayRate(_item.Owner, _item) : 1m;
        long interval = (long)freshness.settlementIntervalHours * PuzzleTimeManager.MINUTES_PER_HOUR;
        return _item.MinutesUntilNextFreshnessBand(decayRate, interval);
    }

    internal int AppliedPreservationPercent(PuzzlePlacement _item)
    {
        if (!IsTrainWagon(_item.Owner) || _item.RemainingShelfLifeMinutes is not > 0)
        {
            return 0;
        }

        decimal decayRate = CreatePreservation(tables.PuzzleSynergy.freshness).DecayRate(_item.Owner, _item);
        return (int)((1m - decayRate) * PuzzleInventoryValuation.BASE_PERCENT);
    }

    static PuzzleShelfLifePreservation CreatePreservation(PuzzleFreshnessData _freshness)
    {
        return new PuzzleShelfLifePreservation(new IPuzzlePreservationEffect[]
        {
            new PuzzleAdjacentPreservation(_freshness)
        });
    }

    // 출산까지의 진행 분과 공간 부족 대기 여부. 대기 중에는 진행이 멈추고 자리가 돌아오면 남은 시간을 흘려보낸다.
    readonly struct BreedingState
    {
        internal readonly long ElapsedMinutes;
        internal readonly bool SpaceBlocked;

        internal BreedingState(long _elapsedMinutes, bool _spaceBlocked)
        {
            ElapsedMinutes = _elapsedMinutes;
            SpaceBlocked = _spaceBlocked;
        }
    }

    // 동물 한 마리의 배설 진행. 출산과 같은 대기 규칙을 쓰지만 화면에는 표시하지 않는다.
    readonly struct WasteState
    {
        internal readonly long ElapsedMinutes;
        internal readonly bool SpaceBlocked;

        internal WasteState(long _elapsedMinutes, bool _spaceBlocked)
        {
            ElapsedMinutes = _elapsedMinutes;
            SpaceBlocked = _spaceBlocked;
        }
    }

    readonly struct BreedingPair
    {
        internal readonly (int First, int Second) key;
        internal readonly PuzzlePlacement first;
        internal readonly PuzzlePlacement second;
        internal readonly PuzzleInventory wagon;
        internal readonly PuzzleItemDefinition offspring;
        internal readonly int offspringCount;
        internal readonly BreedingState state;

        internal BreedingPair(PuzzlePlacement _first, PuzzlePlacement _second, PuzzleInventory _wagon,
            PuzzleItemDefinition _offspring, int _offspringCount, BreedingState _state)
        {
            key = (_first.InstanceId, _second.InstanceId);
            first = _first;
            second = _second;
            wagon = _wagon;
            offspring = _offspring;
            offspringCount = _offspringCount;
            state = _state;
        }

        // 두 부모가 같은 쌍의 대기를 공유하므로 알림도 함께 남긴다.
        internal void RaiseSpaceNotice()
        {
            first.HasUnreadSpaceNotice = true;
            second.HasUnreadSpaceNotice = true;
        }
    }

    List<BreedingPair> SelectBreedingPairs(PuzzleInventory[] _wagons,
        Dictionary<(int First, int Second), BreedingState> _previous)
    {
        var candidates = new List<BreedingPair>();
        string animalTag = tables.PuzzleSynergy.animals.animalTag;
        foreach (var wagon in _wagons)
        {
            var animals = wagon.Placements.Where(_item => !_item.Definition.IsBaby && _item.Definition.Tags.Contains(animalTag))
                .OrderBy(_item => _item.InstanceId).ToArray();
            for (int i = 0; i < animals.Length; i++)
            {
                var first = animals[i];
                if (!tables.TryGetAnimalBreeding(first.Definition.Id, out var rule))
                {
                    continue;
                }
                for (int j = i + 1; j < animals.Length; j++)
                {
                    var second = animals[j];
                    if (first.Definition.Id == second.Definition.Id && Touches(first, second))
                    {
                        var key = (first.InstanceId, second.InstanceId);
                        _previous.TryGetValue(key, out var state);
                        candidates.Add(new BreedingPair(first, second, wagon, definitions[rule.offspringPieceId],
                            rule.offspringCount, state));
                    }
                }
            }
        }

        var pairedIds = new HashSet<int>();
        var pairs = new List<BreedingPair>();
        foreach (var candidate in candidates.OrderByDescending(_pair => _previous.ContainsKey(_pair.key))
            .ThenBy(_pair => _pair.key.First).ThenBy(_pair => _pair.key.Second))
        {
            if (pairedIds.Contains(candidate.key.First) || pairedIds.Contains(candidate.key.Second))
            {
                continue;
            }
            pairedIds.Add(candidate.key.First);
            pairedIds.Add(candidate.key.Second);
            pairs.Add(candidate);
        }
        return pairs;
    }

    // 상하좌우 접촉은 냉장 표시와 같은 원본 판정을 쓴다.
    static bool Touches(PuzzlePlacement _first, PuzzlePlacement _second)
    {
        return PuzzleRefrigeration.TryGetContact(_first, _second, out _);
    }

    // 표시가 읽는 번식 진행. 짝 선택 규칙을 그대로 쓰므로 두 부모는 항상 같은 쌍 상태를 공유한다.
    internal bool TryGetBreedingProgress(PuzzlePlacement _placement, out long _elapsedMinutes, out bool _spaceBlocked)
    {
        _elapsedMinutes = 0;
        _spaceBlocked = false;
        if (_placement == null || !trainWagons.Contains(_placement.Owner))
        {
            return false;
        }

        foreach (var pair in SelectBreedingPairs(trainWagons.ToArray(), breedingProgress))
        {
            if (pair.key.First == _placement.InstanceId || pair.key.Second == _placement.InstanceId)
            {
                _elapsedMinutes = pair.state.ElapsedMinutes;
                _spaceBlocked = pair.state.SpaceBlocked;
                return true;
            }
        }

        return false;
    }

    static long MinutesUntilBirth(List<BreedingPair> _pairs, long _breedingMinutes, long _minutesUntilDeadline)
    {
        long next = long.MaxValue;
        foreach (var pair in _pairs)
        {
            // 대기 중인 쌍도 남은 시간이 같으므로 같은 경계에서 자리를 다시 본다.
            long remaining = _breedingMinutes - pair.state.ElapsedMinutes;
            if (remaining < next && remaining <= _minutesUntilDeadline)
            {
                next = remaining;
            }
        }
        return Math.Max(1, next);
    }

    void AdvanceBreeding(TimeAdvancePlan _plan, List<BreedingPair> _pairs, long _step, long _breedingMinutes,
        long _holdMinutes)
    {
        var progress = new Dictionary<(int First, int Second), BreedingState>();
        foreach (var pair in _pairs)
        {
            if (pair.state.SpaceBlocked)
            {
                // 자리가 돌아올 때까지는 완료 직전 진행도를 유지하고, 자리를 찾으면 마지막 1시간을 다시 흘려보낸다.
                progress.Add(pair.key,
                    pair.wagon.CanAutoPlaceAll(pair.offspring, pair.offspringCount, _allowRotation: true)
                        ? new BreedingState(_breedingMinutes - _holdMinutes, false)
                        : pair.state);
                continue;
            }

            long elapsed = pair.state.ElapsedMinutes + _step;
            bool blocked = false;
            if (elapsed >= _breedingMinutes)
            {
                // 같은 시각의 배설·성장이 끝난 배치에 출산하고, 산출물이 전부 들어갈 때만 진행을 넘긴다.
                if (pair.wagon.TryAutoPlaceAll(pair.offspring, pair.offspringCount, ref _plan.lastInstanceId,
                    _allowRotation: true))
                {
                    elapsed %= _breedingMinutes;
                }
                else
                {
                    elapsed = _breedingMinutes - _holdMinutes;
                    blocked = true;
                    pair.RaiseSpaceNotice();
                }
            }
            progress.Add(pair.key, new BreedingState(elapsed, blocked));
        }
        _plan.breedingProgress = progress;
    }

    // 배설 진행도가 없는 동물은 이번 시각 이후 처음 맞는 달력 경계에 첫 발생을 맞춘다.
    void EnsureWasteStates(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        long _cycleMinutes, long _atMinute)
    {
        long untilBoundary = Time.NextDayIntervalAt(_atMinute, _animals.wasteIntervalDays) - _atMinute;
        foreach (var wagon in _wagons)
        {
            foreach (var animal in wagon.Placements)
            {
                if (animal.Definition.Tags.Contains(_animals.animalTag) &&
                    !_plan.wasteProgress.ContainsKey(animal.InstanceId))
                {
                    _plan.wasteProgress.Add(animal.InstanceId, new WasteState(_cycleMinutes - untilBoundary, false));
                }
            }
        }
    }

    static long MinutesUntilWaste(PuzzleInventory[] _wagons, Dictionary<int, WasteState> _progress, string _animalTag,
        long _cycleMinutes, long _minutesUntilDeadline)
    {
        long next = long.MaxValue;
        foreach (var wagon in _wagons)
        {
            foreach (var animal in wagon.Placements)
            {
                if (!animal.Definition.Tags.Contains(_animalTag) ||
                    !_progress.TryGetValue(animal.InstanceId, out var state))
                {
                    continue;
                }
                long remaining = _cycleMinutes - state.ElapsedMinutes;
                if (remaining < next && remaining <= _minutesUntilDeadline)
                {
                    next = remaining;
                }
            }
        }
        return Math.Max(1, next);
    }

    void AdvanceWaste(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        PuzzleItemDefinition _waste, long _cycleMinutes, long _holdMinutes, long _step)
    {
        var progress = new Dictionary<int, WasteState>();
        foreach (var wagon in _wagons)
        {
            var animals = wagon.Placements
                .Where(_item => _item.Definition.Tags.Contains(_animals.animalTag))
                .ToArray();
            foreach (var animal in animals)
            {
                _plan.wasteProgress.TryGetValue(animal.InstanceId, out var state);
                if (state.SpaceBlocked)
                {
                    // 공간이 없는 동물만 대기한다. 자리를 찾으면 마지막 1시간을 다시 흘려보낸 뒤 배설한다.
                    progress[animal.InstanceId] = wagon.CanAutoPlaceAll(_waste, _animals.wasteCountPerAnimal)
                        ? new WasteState(_cycleMinutes - _holdMinutes, false)
                        : state;
                    continue;
                }

                long elapsed = state.ElapsedMinutes + _step;
                bool blocked = false;
                if (elapsed >= _cycleMinutes)
                {
                    if (wagon.TryAutoPlaceAll(_waste, _animals.wasteCountPerAnimal, ref _plan.lastInstanceId))
                    {
                        elapsed %= _cycleMinutes;
                    }
                    else
                    {
                        elapsed = _cycleMinutes - _holdMinutes;
                        blocked = true;
                    }
                }
                progress[animal.InstanceId] = new WasteState(elapsed, blocked);
            }
        }
        _plan.wasteProgress = progress;
    }

    void CommitTimeAdvance(long _minutes, TimeAdvancePlan _plan)
    {
        long previous = Time.ElapsedMinutes;
        for (int i = 0; i < trainWagons.Count; i++)
        {
            trainWagons[i].CommitTimeAdvance(_plan.wagons[i]);
        }
        breedingProgress = _plan.breedingProgress;
        wasteProgress = _plan.wasteProgress;
        PruneAnimalStates();
        lastInstanceId = _plan.lastInstanceId;
        Time.Advance(_minutes);
        if (Time.IsOverdue)
        {
            RunStatus = PuzzleRunStatus.Lost;
        }
        CheckQuestFailures();
        if (_minutes > 0)
        {
            CheckButcheryReady();
            CheckRefiningReady();
            GameEvents.RaiseTimeAdvanced(previous, Time.ElapsedMinutes, this, _plan.cargoValueBeforeSettlement,
                _plan.cargoValueBeforeFreshnessLoss);
        }
    }

    // 기차에 남지 않은 개체의 대기 상태는 다음 진행에 읽히지 않으므로 정리만 한다.
    void PruneAnimalStates()
    {
        var alive = new HashSet<int>(trainWagons.SelectMany(_wagon => _wagon.Placements)
            .Select(_placement => _placement.InstanceId));
        foreach (var id in wasteProgress.Keys.Where(_id => !alive.Contains(_id)).ToArray())
        {
            wasteProgress.Remove(id);
        }
        foreach (var key in breedingProgress.Keys.Where(_key =>
            !alive.Contains(_key.First) || !alive.Contains(_key.Second)).ToArray())
        {
            breedingProgress.Remove(key);
        }
    }

    /// <summary>정수 시간 호출자를 위한 분 단위 시계 어댑터다.</summary>
    public bool AdvanceHours(long _hours)
    {
        return AdvanceMinutes(checked(_hours * PuzzleTimeManager.MINUTES_PER_HOUR));
    }

    internal PuzzleTravelResult Travel(string _cityId)
    {
        var evaluation = EvaluateTravel(_cityId, out var plan);
        if (!evaluation.IsSuccess)
        {
            RecordAction(PuzzleActionKind.Travel, evaluation.Failure.ToString(), _targetCityId: _cityId);
            return evaluation;
        }

        long duration = evaluation.TravelMinutes;
        var replacementMarket = CreateMarket(evaluation.FromCityId, ref plan.lastInstanceId);
        long previousFunds = Funds;
        Funds -= evaluation.Cost;
        markets[evaluation.FromCityId] = replacementMarket;
        currentCityId = evaluation.ToCityId;
        // 떠난 도시의 도축대는 조작 대상이 아니다. 도착 후 패널을 다시 열 때 열어 준다.
        IsButcheryOpen = false;
        IsModificationOpen = false;
        IsRefiningOpen = false;
        bool previewChanged = PreviewCityId != null;
        PreviewCityId = null;
        CityMoveCount++;
        visitedCities.Add(CurrentCityId);
        CommitTimeAdvance(duration, plan);
        if (previewChanged)
        {
            GameEvents.RaiseTravelPreviewChanged(this);
        }
        RecordAction(PuzzleActionKind.Travel, _targetCityId: _cityId);
        GameEvents.RaiseCityArrived(CurrentCityId, CityMoveCount);
        GameEvents.RaiseFundsChanged(this, previousFunds);

        var arrival = new PuzzleTravelResult(PuzzleTravelFailure.None, evaluation.Cost, evaluation.FromCityId,
            evaluation.ToCityId, Funds, evaluation.BaseCost, evaluation.Cells, evaluation.Distance, evaluation.DistanceCost,
            evaluation.TravelMinutes, evaluation.ArrivalClockMinutes, evaluation.ArrivalIsNight);
        // 운임 정산 결과를 확정한 뒤 목적지 의뢰를 완료한다. 의뢰 보상은 별도 행동으로 기록한다.
        CompleteArrivedQuests();
        return arrival;
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
        // 도축대가 관계한 이동은 거래가 아니다. 상태가 허용하는 방향만 받아 배치 결과만 돌려준다.
        if (IsButchery(source) || IsButchery(_destination))
        {
            if (sourceIsTrain && _original.Definition.IsBaby)
            {
                return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.BabyCannotButcher));
            }
            var butcheryPlacement = source != null && CanMoveToButchery(source, _destination)
                ? source.EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns)
                : new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination);
            return new PuzzleDropResult(butcheryPlacement);
        }
        if (IsModification(source) || IsModification(_destination))
        {
            var modificationPlacement = source != null && CanMoveToModification(source, _destination)
                ? source.EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns)
                : new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination);
            return new PuzzleDropResult(modificationPlacement);
        }
        // 정제대도 거래가 아니다. 기차↔정제대·보드 내부 이동만 받고 상태가 허용하는 방향만 통과한다.
        if (IsRefining(source) || IsRefining(_destination))
        {
            var refiningPlacement = source != null && CanMoveToRefining(source, _destination)
                ? source.EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns)
                : new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination);
            return new PuzzleDropResult(refiningPlacement);
        }
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
        return new PuzzleDropResult(placement, direction, price, failure, percent, tag,
            !buying && IsPurchaseCancel(_original, out _));
    }

    // 현재 도시에서 이 아이템에 적용되는 태그 규칙과 구매·판매 비율.
    internal PuzzleTradeRate RateFor(PuzzleItemDefinition _item)
    {
        return RateFor(tables.CityPrice(CurrentCityId), _item);
    }

    // 경제 분석용. 현재 도시가 아닌 도시의 규칙을 같은 경로로 조회한다.
    internal PuzzleTradeRate RateAt(string _cityId, PuzzleItemDefinition _item)
    {
        return RateFor(tables.CityPrice(_cityId), _item);
    }

    // 아이템 태그와 맞는 규칙을 모두 합산한다. 맞는 규칙이 없으면 도시 기본 배율이다.
    PuzzleTradeRate RateFor(PuzzleCityData _city, PuzzleItemDefinition _item)
    {
        var rules = new List<PuzzleCityTagPriceData>();
        foreach (string tag in _item.Tags)
        {
            if (tables.TryGetCityTagPrice(_city.id, tag, out var rule))
            {
                rules.Add(rule);
            }
        }
        return ApplyRegionPenalty(_city, PuzzleTradeRate.Combine(_city, rules));
    }

    // 툴팁과 실제 거래가 공유한다. 배치·자금 검사 없이 현재 도시의 정확한 금액만 산출한다.
    // 구매·판매 모두 신선도를 반영한 실제 가치가 기준이다. 새 재고는 실제 가치가 기본 가치와 같고,
    // 판매되어 시장에 남은 화물은 누적된 실제 가치 그대로 다시 거래된다.
    internal bool TryQuoteTrade(PuzzlePlacement _item, PuzzleTradeDirection _direction, out long _price)
    {
        _price = 0;
        if (_item == null || _direction == PuzzleTradeDirection.None)
        {
            return false;
        }
        return TryQuoteTradeAt(CurrentCityId, _item.Definition, _item.FreshnessAdjustedValue, _direction, out _price) &&
            TryApplyProficiency(_item, _direction, ref _price);
    }

    // 지정 도시에서 주어진 가치의 거래 금액. 경제 분석이 다른 도시를 같은 공식으로 견적한다.
    internal bool TryQuoteTradeAt(string _cityId, PuzzleItemDefinition _definition, decimal _value,
        PuzzleTradeDirection _direction, out long _price)
    {
        _price = 0;
        if (_definition == null || _direction == PuzzleTradeDirection.None)
        {
            return false;
        }
        var city = tables.CityPrice(_cityId);
        var rate = RateFor(city, _definition);
        decimal effectiveValue = _value;
        string wasteTag = tables.PuzzleSynergy?.animals?.wasteTag;
        // 농업 지역에서는 음수 가치의 거름(Waste)을 팔 때만 양수 가격으로 매입한다.
        if (_direction == PuzzleTradeDirection.Sell &&
            effectiveValue < 0 &&
            string.Equals(city.regionId, FARM_REGION_ID, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(wasteTag) &&
            PuzzlePieceEffects.HasAnyTag(_definition.Tags, wasteTag))
        {
            effectiveValue = -effectiveValue;
        }
        return TryGetTradePrice(effectiveValue, rate.PercentFor(_direction), tables.Region(city.regionId).pricePercent, out _price);
    }

    static bool TryGetTradePrice(decimal _value, int _percent, int _regionPercent, out long _price)
    {
        _price = 0;
        // 음수 가치(처분)와 0 이하 배율(판매 시 돈을 냄)도 허용한다. 지역 물가만 양수여야 한다.
        if (_regionPercent <= 0)
        {
            return false;
        }

        decimal rate = (decimal)_percent / PERCENT_SCALE * _regionPercent / PERCENT_SCALE;
        if (rate != 0 && Math.Abs(_value) > ((decimal)long.MaxValue + 1) / Math.Abs(rate))
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
            evaluation.Placement.Failure == PuzzlePlacementFailure.InvalidDestination ||
            evaluation.Placement.Failure == PuzzlePlacementFailure.BabyCannotButcher)
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

        // 작업대 산출물을 기차로 옮기면 이 마을에서 얻은 화물로 기록한다(판매 숙련 경험치 판정).
        if ((IsButchery(_original.Owner) || IsModification(_original.Owner) || IsRefining(_original.Owner)) &&
            IsTrainWagon(_destination))
        {
            RecordLocalOrigin(_original.InstanceId);
        }

        // 객차에서 사라진 동물의 대기 상태는 다음 진행까지 기다리지 않고 정리한다.
        if (IsTrainWagon(_original.Owner) || IsTrainWagon(_destination))
        {
            PruneAnimalStates();
        }

        if (result.Direction != PuzzleTradeDirection.None)
        {
            // Move는 새 배치 객체를 만들므로 이동 완료된 인스턴스의 구매 시계를 갱신한다.
            _destination.TryGetCell(move.Candidate.Cells[0].Position, out var movedItem, out _);
            movedItem.SetOwned(result.Direction == PuzzleTradeDirection.Buy);
            // 가치가 음수인 화물(처분)은 판매 즉시 사라져 다시 살 수 없다.
            // 등장 풀에서 제외된 품목은 판매해도 시장이 다시 만들어지기(재방문) 전까지 보드에 남아 다시 살 수 있다.
            if (result.Direction == PuzzleTradeDirection.Sell && movedItem.FreshnessAdjustedValue < 0)
            {
                _destination.Remove(movedItem);
            }
            long previousFunds = Funds;
            Funds += result.Direction == PuzzleTradeDirection.Buy ? -result.Price : result.Price;
            GameEvents.RaiseTradeCompleted(new GameEvents.TradeInfo(CurrentCityId, _original.Definition.Id,
                _original.InstanceId, result.Direction, result.Price, Funds, move.Candidate.Origin,
                move.Candidate.ClockwiseTurns));
            GameEvents.RaiseFundsChanged(this, previousFunds);
            if (result.Direction == PuzzleTradeDirection.Buy)
            {
                RecordPurchase(_original, result.Price);
            }
            if (result.Direction == PuzzleTradeDirection.Sell)
            {
                GrantSaleProficiency(_original, result.Price);
                // 판매한 의뢰 화물은 기차에서 사라지므로 그 의뢰는 실패한다.
                CheckQuestFailures();
            }
        }
        return result;
    }

    #region 마을별 행동 · 도축

    // 도축 패널이 열려 보드 조작을 받는 여부. 반입·반출 허용 범위는 CanMoveToButchery가 결정한다.
    internal bool IsButcheryOpen { get; private set; }

    // 현재 도시(또는 지정한 도시)의 도축 행동 정의. 없는 도시면 null.
    internal PuzzleCityActionData ButcheryAction(string _cityId = null)
    {
        return tables != null &&
            tables.TryGetCityAction(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId,
                PuzzleCityActionType.Butchery, out var action)
            ? action
            : null;
    }

    internal bool HasButchery(string _cityId = null)
    {
        return ButcheryAction(_cityId) != null;
    }

    // 확인 1회당 도축 비용. 표의 행동 cost가 원본이며 없으면 -1.
    internal long ButcheryCost(string _cityId = null)
    {
        var action = ButcheryAction(_cityId);
        return action != null ? action.cost : -1;
    }

    // 도축대를 처음 다룰 때 만든다. 기차·시장과 같은 인스턴스 발급 경로를 쓰기 위해 지연 생성한다.
    PuzzleButcheryState EnsureButchery(string _cityId)
    {
        if (definitions == null || tables == null || string.IsNullOrEmpty(_cityId) ||
            !tables.TryGetCityAction(_cityId, PuzzleCityActionType.Butchery, out var action))
        {
            return null;
        }
        if (butcheries.TryGetValue(_cityId, out var existing))
        {
            return RefreshButcheryReady(existing);
        }

        var board = new PuzzleInventory(action.board, Array.Empty<PuzzleStartingPlacementData>(), definitions,
            marketRules, ref lastInstanceId);
        var state = new PuzzleButcheryState(_cityId, action, board);
        butcheries.Add(_cityId, state);
        return state;
    }

    internal PuzzleInventory ButcheryBoard(string _cityId = null)
    {
        var state = EnsureButchery(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return state?.Board;
    }

    internal PuzzleButcheryPhase ButcheryPhase(string _cityId = null)
    {
        var state = EnsureButchery(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return state == null ? PuzzleButcheryPhase.Idle : state.Phase;
    }

    // 작업 중일 때 remaining, 그 외에는 -1.
    internal long ButcheryRemainingMinutes(string _cityId = null)
    {
        var state = EnsureButchery(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        if (state == null || state.Phase != PuzzleButcheryPhase.Working || Time == null)
        {
            return -1;
        }
        return Math.Max(0, state.ReadyAtMinutes - Time.ElapsedMinutes);
    }

    internal bool IsButchery(PuzzleInventory _inventory)
    {
        return ButcheryStateOf(_inventory) != null;
    }

    // 보드가 어느 도시의 도축대인지 되찾는다. 기차·시장은 null.
    PuzzleButcheryState ButcheryStateOf(PuzzleInventory _board)
    {
        if (_board == null)
        {
            return null;
        }
        foreach (var state in butcheries.Values)
        {
            if (ReferenceEquals(state.Board, _board))
            {
                return state;
            }
        }

        return null;
    }

    internal bool OpenButchery()
    {
        var state = EnsureButchery(CurrentCityId);
        if (state == null || RunStatus != PuzzleRunStatus.InProgress ||
            state.Phase == PuzzleButcheryPhase.Working)
        {
            IsButcheryOpen = false;
            return false;
        }

        IsButcheryOpen = true;
        NotifyButchery(state);
        return true;
    }

    internal void CloseButchery()
    {
        IsButcheryOpen = false;
    }

    // 닫을 수 있는지. Ready는 산출물을 도시에 남긴 채 닫고, Idle은 보드를 비운 뒤에만 닫는다.
    internal bool TryLeaveButchery(out string _reason)
    {
        _reason = null;
        var state = EnsureButchery(CurrentCityId);
        if (state == null || state.Phase == PuzzleButcheryPhase.Ready)
        {
            return true;
        }
        if (state.Board.Placements.Count > 0)
        {
            _reason = BasicData.Text("rules.butchery.returnItems");
            return false;
        }

        return true;
    }

    internal bool ButcheryConfirmable(string _cityId = null)
    {
        return TryEvaluateButchery(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId, out _, out _);
    }

    // 확인 버튼의 조건과 사유를 한 곳에서 정한다. View가 독자적으로 판단하지 않는다.
    bool TryEvaluateButchery(string _cityId, out IReadOnlyList<string> _outputs, out string _reason)
    {
        _outputs = null;
        _reason = null;
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            _reason = BasicData.Text("rules.butchery.blocked");
            return false;
        }

        var state = EnsureButchery(_cityId);
        if (state == null)
        {
            _reason = BasicData.Text("rules.butchery.unavailable");
            return false;
        }
        if (state.Phase == PuzzleButcheryPhase.Working)
        {
            _reason = BasicData.Text("rules.butchery.inProgress");
            return false;
        }
        if (state.Phase == PuzzleButcheryPhase.Ready)
        {
            _reason = BasicData.Text("rules.butchery.awaitingPickup");
            return false;
        }
        if (state.Board.Placements.Count == 0)
        {
            _reason = BasicData.Text("rules.butchery.empty");
            return false;
        }

        var outputs = new List<string>();
        foreach (var placement in state.Board.Placements)
        {
            if (placement.Definition.IsBaby)
            {
                _reason = Presentation.PuzzleBabyCannotButcher;
                return false;
            }
            if (!placement.Definition.Tags.Contains(state.Action.inputTag))
            {
                _reason = BasicData.Format("rules.butchery.notAnimal", placement.Definition.DisplayName);
                return false;
            }
            if (!TryCollectOutputs(state.Action, placement.Definition.Id, outputs))
            {
                _reason = BasicData.Format("rules.butchery.noConversion", placement.Definition.DisplayName);
                return false;
            }
        }

        if (!TryFitsOutputs(state.Action, outputs))
        {
            _reason = BasicData.Text("rules.butchery.noSpace");
            return false;
        }

        if (Funds < state.Action.cost)
        {
            _reason = Presentation.PuzzleButcheryInsufficientFunds(state.Action.cost);
            return false;
        }

        _outputs = outputs;
        return true;
    }

    internal bool TryConfirmButchery(out string _reason)
    {
        if (!TryEvaluateButchery(CurrentCityId, out var outputs, out _reason))
        {
            return false;
        }
        if (!IsButcheryOpen)
        {
            _reason = BasicData.Text("rules.butchery.blocked");
            return false;
        }

        var state = EnsureButchery(CurrentCityId);
        if (state == null)
        {
            _reason = BasicData.Text("rules.butchery.unavailable");
            return false;
        }

        // 확인 직전에 비용·잔액과 완료 시각을 다시 계산한다. 여기까지 실패하면 보드·상태·자금을 하나도 바꾸지 않는다.
        long cost = state.Action.cost;
        if (Funds < cost)
        {
            _reason = Presentation.PuzzleButcheryInsufficientFunds(cost);
            return false;
        }

        long readyAtMinutes;
        try
        {
            readyAtMinutes = checked(Time.ElapsedMinutes + state.Action.workMinutes);
        }
        catch (OverflowException)
        {
            _reason = BasicData.Text("rules.butchery.timeOverflow");
            return false;
        }

        var inputs = state.Board.Placements.ToArray();
        // 화물 제거·산출물 예약·상태 변경·비용 차감을 한 확정 단위로 처리하고, 그 뒤에만 이벤트를 발행한다.
        foreach (var placement in inputs)
        {
            _ = state.Board.Remove(placement);
        }

        long previousFunds = Funds;
        Funds -= cost;
        state.PendingOutputs.Clear();
        state.PendingOutputs.AddRange(outputs);
        state.OutputCount = outputs.Count;
        state.ReadyAtMinutes = readyAtMinutes;
        state.Phase = PuzzleButcheryPhase.Working;
        NotifyButchery(state);
        GameEvents.RaiseButcheryCommitted(state.Action.id, state.CityId, inputs.Length, state.ReadyAtMinutes);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
    }

    // 입력 화물 하나의 산출물을 목록에 더한다. 변환 규칙이 없으면 false.
    static bool TryCollectOutputs(PuzzleCityActionData _action, string _pieceId, List<string> _outputs)
    {
        foreach (var conversion in _action.conversions ?? Array.Empty<PuzzleActionConversionData>())
        {
            if (conversion == null || !string.Equals(conversion.inputPieceId, _pieceId, StringComparison.Ordinal))
            {
                continue;
            }
            for (int i = 0; i < conversion.outputCount; i++)
            {
                _outputs.Add(conversion.outputPieceId);
            }
            return true;
        }

        return false;
    }

    // 빈 작업 보드에 산출물을 전부 놓을 수 있는지 미리 본다. 확인 직후 실패가 없도록 검증만 하고 상태는 바꾸지 않는다.
    bool TryFitsOutputs(PuzzleCityActionData _action, List<string> _outputs)
    {
        int probeId = 0;
        var probe = new PuzzleInventory(_action.board, Array.Empty<PuzzleStartingPlacementData>(), definitions,
            marketRules, ref probeId);
        foreach (string pieceId in _outputs)
        {
            if (!definitions.TryGetValue(pieceId, out var definition) ||
                !probe.TryAutoPlace(definition, checked(--probeId), out _))
            {
                return false;
            }
        }

        return true;
    }

    // 시간이 흐른 뒤 완료된 도축을 처리한다. 조회 시점에도 같은 판정을 하므로 지연 판정이다.
    void CheckButcheryReady()
    {
        // 완료 발행 중 구독자가 도축대를 새로 만들 수 있어 스냅샷을 순회한다.
        foreach (var state in butcheries.Values.ToArray())
        {
            RefreshButcheryReady(state);
        }
    }

    PuzzleButcheryState RefreshButcheryReady(PuzzleButcheryState _state)
    {
        if (_state.Phase != PuzzleButcheryPhase.Working || Time == null ||
            Time.ElapsedMinutes < _state.ReadyAtMinutes)
        {
            return _state;
        }

        bool changed = false;
        foreach (string pieceId in _state.PendingOutputs.ToArray())
        {
            if (lastInstanceId == int.MaxValue)
            {
                break;
            }
            if (definitions.TryGetValue(pieceId, out var definition) &&
                _state.Board.TryAutoPlace(definition, ref lastInstanceId))
            {
                _state.PendingOutputs.Remove(pieceId);
                changed = true;
            }
        }

        bool becameReady = false;
        if (_state.PendingOutputs.Count == 0)
        {
            _state.Phase = PuzzleButcheryPhase.Ready;
            _state.ReadyAtMinutes = -1;
            changed = becameReady = true;
        }
        if (changed)
        {
            NotifyButchery(_state);
        }
        if (becameReady)
        {
            GameEvents.RaiseButcheryReady(_state.Action.id, _state.CityId, _state.OutputCount);
        }
        return _state;
    }

    // 도축 보드가 관계하는 이동의 허용 범위. 거래는 없고 상태별 방향만 검사한다.
    internal bool CanMoveToButchery(PuzzleInventory _source, PuzzleInventory _destination)
    {
        if (!IsButcheryOpen)
        {
            return false;
        }
        var state = ButcheryStateOf(_source) ?? ButcheryStateOf(_destination);
        if (state == null || !ReferenceEquals(state.Action, ButcheryAction()))
        {
            return false;
        }

        bool sameBoard = ReferenceEquals(state.Board, _source) && ReferenceEquals(state.Board, _destination);
        switch (state.Phase)
        {
            case PuzzleButcheryPhase.Idle:
                // 맡기러 넣는 이동과 기차로 되 빼내는 이동, 보드 안 재배치를 모두 허용한다.
                return sameBoard || (ReferenceEquals(state.Board, _destination)
                    ? IsTrainWagon(_source)
                    : IsTrainWagon(_destination));
            case PuzzleButcheryPhase.Ready:
                // 산출물 회수와 보드 안 재배치만 허용한다. 새 화물은 넣지 못한다.
                return sameBoard || ReferenceEquals(state.Board, _source) && IsTrainWagon(_destination);
            default:
                return false;
        }
    }

    // 드래그 확정 후 호출한다. Ready에서 산출물을 모두 꺼내면 다시 맡길 수 있는 Idle로 돌아간다.
    internal void RefreshButcheryAfterMove(PuzzleInventory _board)
    {
        var state = ButcheryStateOf(_board);
        if (state == null)
        {
            return;
        }

        RefreshButcheryReady(state);
        if (state.Phase == PuzzleButcheryPhase.Ready && state.Board.Placements.Count == 0)
        {
            state.Phase = PuzzleButcheryPhase.Idle;
        }
        NotifyButchery(state);
    }

    // 도축대는 UI가 상태를 조회해 스스로 그린다. 변화가 있을 때 발행지 한 곳에서만 알린다.
    void NotifyButchery(PuzzleButcheryState _state)
    {
        GameEvents.RaiseButcheryChanged(this, _state.CityId, _state.Phase,
            IsButcheryOpen && string.Equals(_state.CityId, CurrentCityId, StringComparison.Ordinal));
    }

    #endregion

    #region 마을별 행동 · 개조

    internal bool IsModificationOpen { get; private set; }

    internal PuzzleCityActionData ModificationAction(string _cityId = null)
    {
        return tables != null &&
            tables.TryGetCityAction(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId,
                PuzzleCityActionType.Modification, out var action)
            ? action
            : null;
    }

    internal bool HasModification(string _cityId = null)
    {
        return ModificationAction(_cityId) != null;
    }

    PuzzleModificationState EnsureModification(string _cityId)
    {
        if (definitions == null || tables == null || string.IsNullOrEmpty(_cityId) ||
            !tables.TryGetCityAction(_cityId, PuzzleCityActionType.Modification, out var action))
        {
            return null;
        }
        if (modifications.TryGetValue(_cityId, out var existing))
        {
            return existing;
        }

        var board = new PuzzleInventory(action.board, Array.Empty<PuzzleStartingPlacementData>(), definitions,
            Array.Empty<PuzzleSynergyRuleData>(), ref lastInstanceId);
        var state = new PuzzleModificationState(_cityId, action, board);
        modifications.Add(_cityId, state);
        return state;
    }

    internal PuzzleInventory ModificationBoard(string _cityId = null)
    {
        return EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId)?.Board;
    }

    internal IReadOnlyList<PuzzleActionModeData> ModificationModes(string _cityId = null)
    {
        var action = ModificationAction(_cityId);
        return tables == null || action == null
            ? Array.Empty<PuzzleActionModeData>()
            : tables.ActionModesOf(action);
    }

    internal bool TryGetSelectedModificationMode(out PuzzleActionModeData _mode, string _cityId = null)
    {
        _mode = null;
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return state != null && TryFindSelectedMode(state, out _mode);
    }

    internal bool TryToggleModificationMode(string _modeId, out bool _selected, string _cityId = null)
    {
        _selected = false;
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        if (state == null || RunStatus != PuzzleRunStatus.InProgress || !IsModificationOpen ||
            !string.Equals(state.CityId, CurrentCityId, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(_modeId) || !TryFindActionMode(state.Action, _modeId, out _))
        {
            return false;
        }

        bool enabled = !string.Equals(state.SelectedModeId, _modeId, StringComparison.Ordinal);
        state.SelectedModeId = enabled ? _modeId : null;
        _selected = enabled;
        NotifyModification(state);
        return true;
    }

    internal bool IsModification(PuzzleInventory _inventory)
    {
        return ModificationStateOf(_inventory) != null;
    }

    PuzzleModificationState ModificationStateOf(PuzzleInventory _board)
    {
        if (_board == null)
        {
            return null;
        }
        foreach (var state in modifications.Values)
        {
            if (ReferenceEquals(state.Board, _board))
            {
                return state;
            }
        }

        return null;
    }

    internal bool OpenModification()
    {
        var state = EnsureModification(CurrentCityId);
        if (state == null || RunStatus != PuzzleRunStatus.InProgress)
        {
            IsModificationOpen = false;
            return false;
        }

        state.SelectedModeId = null;
        IsModificationOpen = true;
        NotifyModification(state);
        return true;
    }

    internal void CloseModification()
    {
        IsModificationOpen = false;
    }

    internal bool CanLeaveModification(string _cityId = null)
    {
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return state == null || state.Board.Placements.Count == 0;
    }

    internal bool ModificationConfirmable(string _cityId = null)
    {
        return TryPlanModificationSwaps(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId, out _, out _);
    }

    internal bool TryConfirmModification(out int _convertedCount, out PuzzleModificationFailure _failure,
        string _cityId = null)
    {
        _convertedCount = 0;
        string cityId = string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId;
        if (!TryPlanModificationSwaps(cityId, out var swaps, out _failure))
        {
            return false;
        }

        var state = EnsureModification(cityId);
        if (state == null)
        {
            _failure = PuzzleModificationFailure.Unavailable;
            return false;
        }
        if (!TryFindSelectedMode(state, out var mode))
        {
            _failure = PuzzleModificationFailure.NoMode;
            return false;
        }

        // 정의 교체가 성공한 뒤에만 딱 한 번 차감한다. 잔액을 다시 확인해 초과 차감을 막는다.
        long cost = mode.cost;
        if (Funds < cost)
        {
            _failure = PuzzleModificationFailure.InsufficientFunds;
            return false;
        }
        if (!state.Board.TrySwapDefinitions(swaps, out var replacements))
        {
            _failure = PuzzleModificationFailure.InvalidResult;
            return false;
        }

        long previousFunds = Funds;
        Funds -= cost;
        _convertedCount = replacements.Count;
        NotifyModification(state);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
    }

    internal void RefreshModificationAfterMove(PuzzleInventory _board)
    {
        var state = ModificationStateOf(_board);
        if (state != null)
        {
            NotifyModification(state);
        }
    }

    void NotifyModification(PuzzleModificationState _state)
    {
        GameEvents.RaiseModificationChanged(this, _state.CityId, !string.IsNullOrEmpty(_state.SelectedModeId),
            IsModificationOpen && string.Equals(_state.CityId, CurrentCityId, StringComparison.Ordinal));
    }

    internal bool CanMoveToModification(PuzzleInventory _source, PuzzleInventory _destination)
    {
        if (!IsModificationOpen)
        {
            return false;
        }
        var state = ModificationStateOf(_source) ?? ModificationStateOf(_destination);
        if (state == null || !ReferenceEquals(state.Action, ModificationAction()))
        {
            return false;
        }

        bool sameBoard = ReferenceEquals(state.Board, _source) && ReferenceEquals(state.Board, _destination);
        return sameBoard ||
            (ReferenceEquals(state.Board, _destination) ? IsTrainWagon(_source) : IsTrainWagon(_destination));
    }

    bool TryPlanModificationSwaps(string _cityId, out List<PuzzleDefinitionSwap> _swaps,
        out PuzzleModificationFailure _failure)
    {
        _swaps = null;
        _failure = PuzzleModificationFailure.None;
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            _failure = PuzzleModificationFailure.Blocked;
            return false;
        }

        var state = EnsureModification(_cityId);
        if (state == null)
        {
            _failure = PuzzleModificationFailure.Unavailable;
            return false;
        }
        if (!IsModificationOpen || !string.Equals(state.CityId, CurrentCityId, StringComparison.Ordinal))
        {
            _failure = PuzzleModificationFailure.Blocked;
            return false;
        }
        if (!TryFindSelectedMode(state, out var mode) || string.IsNullOrEmpty(mode.inputTag) ||
            string.IsNullOrEmpty(mode.outputTag) || string.Equals(mode.inputTag, mode.outputTag, StringComparison.Ordinal))
        {
            _failure = PuzzleModificationFailure.NoMode;
            return false;
        }
        if (state.Board.Placements.Count == 0)
        {
            _failure = PuzzleModificationFailure.Empty;
            return false;
        }

        var swaps = new List<PuzzleDefinitionSwap>(state.Board.Placements.Count);
        foreach (var placement in state.Board.Placements)
        {
            if (!placement.Definition.Tags.Contains(mode.inputTag))
            {
                _failure = PuzzleModificationFailure.InvalidItem;
                return false;
            }
            if (placement.Definition.Tags.Contains(mode.outputTag))
            {
                _failure = PuzzleModificationFailure.AlreadyModified;
                return false;
            }
            if (!TryFindModeConversion(mode, placement.Definition.Id, out string resultPieceId))
            {
                _failure = PuzzleModificationFailure.NoConversion;
                return false;
            }
            if (!definitions.TryGetValue(resultPieceId, out var result) || result == null)
            {
                _failure = PuzzleModificationFailure.InvalidResult;
                return false;
            }

            swaps.Add(new PuzzleDefinitionSwap(placement, result));
        }

        if (!state.Board.TryPlanDefinitionSwaps(swaps))
        {
            _failure = PuzzleModificationFailure.InvalidResult;
            return false;
        }

        if (Funds < mode.cost)
        {
            _failure = PuzzleModificationFailure.InsufficientFunds;
            return false;
        }

        _swaps = swaps;
        return true;
    }

    bool TryFindSelectedMode(PuzzleModificationState _state, out PuzzleActionModeData _mode)
    {
        _mode = null;
        return _state != null && !string.IsNullOrEmpty(_state.SelectedModeId) &&
            TryFindActionMode(_state.Action, _state.SelectedModeId, out _mode);
    }

    bool TryFindActionMode(PuzzleCityActionData _action, string _modeId, out PuzzleActionModeData _mode)
    {
        _mode = null;
        if (_action == null || string.IsNullOrEmpty(_modeId) || tables == null)
        {
            return false;
        }

        foreach (var mode in tables.ActionModesOf(_action))
        {
            if (mode != null && string.Equals(mode.id, _modeId, StringComparison.Ordinal))
            {
                _mode = mode;
                return true;
            }
        }

        return false;
    }

    static bool TryFindModeConversion(PuzzleActionModeData _mode, string _pieceId, out string _resultPieceId)
    {
        _resultPieceId = null;
        foreach (var conversion in _mode.conversions ?? Array.Empty<PuzzleActionConversionData>())
        {
            if (conversion != null && !string.IsNullOrEmpty(conversion.outputPieceId) &&
                conversion.outputCount == 1 &&
                string.Equals(conversion.inputPieceId, _pieceId, StringComparison.Ordinal))
            {
                _resultPieceId = conversion.outputPieceId;
                return true;
            }
        }

        return false;
    }

    #endregion

    #region 마을별 행동 · 정제

    // 정제 패널이 열려 보드 조작을 받는 여부. 반입·반출 허용 범위는 CanMoveToRefining이 결정한다.
    internal bool IsRefiningOpen { get; private set; }

    // 현재 도시(또는 지정한 도시)의 정제 행동 정의. 없는 도시면 null.
    internal PuzzleCityActionData RefiningAction(string _cityId = null)
    {
        return tables != null &&
            tables.TryGetCityAction(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId,
                PuzzleCityActionType.Refining, out var action)
            ? action
            : null;
    }

    internal bool HasRefining(string _cityId = null)
    {
        return RefiningAction(_cityId) != null;
    }

    // 확인 1회당 정제 비용. 표의 행동 cost가 원본이며 없으면 -1.
    internal long RefiningCost(string _cityId = null)
    {
        var action = RefiningAction(_cityId);
        return action != null ? action.cost : -1;
    }

    // 정제대를 처음 다룰 때 만든다. 기차·시장과 같은 인스턴스 발급 경로를 쓰기 위해 지연 생성한다.
    PuzzleRefiningState EnsureRefining(string _cityId)
    {
        if (definitions == null || tables == null || string.IsNullOrEmpty(_cityId) ||
            !tables.TryGetCityAction(_cityId, PuzzleCityActionType.Refining, out var action))
        {
            return null;
        }
        if (refinings.TryGetValue(_cityId, out var existing))
        {
            return RefreshRefiningReady(existing);
        }

        var board = new PuzzleInventory(action.board, Array.Empty<PuzzleStartingPlacementData>(), definitions,
            marketRules, ref lastInstanceId);
        var state = new PuzzleRefiningState(_cityId, action, board);
        refinings.Add(_cityId, state);
        return state;
    }

    internal PuzzleInventory RefiningBoard(string _cityId = null)
    {
        return EnsureRefining(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId)?.Board;
    }

    internal PuzzleRefiningPhase RefiningPhase(string _cityId = null)
    {
        var state = EnsureRefining(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return state == null ? PuzzleRefiningPhase.Idle : state.Phase;
    }

    // 작업 중일 때 remaining, 그 외에는 -1.
    internal long RefiningRemainingMinutes(string _cityId = null)
    {
        var state = EnsureRefining(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        if (state == null || state.Phase != PuzzleRefiningPhase.Working || Time == null)
        {
            return -1;
        }
        return Math.Max(0, state.ReadyAtMinutes - Time.ElapsedMinutes);
    }

    internal bool IsRefining(PuzzleInventory _inventory)
    {
        return RefiningStateOf(_inventory) != null;
    }

    // 보드가 어느 도시의 정제대인지 되찾는다. 기차·시장은 null.
    PuzzleRefiningState RefiningStateOf(PuzzleInventory _board)
    {
        if (_board == null)
        {
            return null;
        }
        foreach (var state in refinings.Values)
        {
            if (ReferenceEquals(state.Board, _board))
            {
                return state;
            }
        }

        return null;
    }

    internal bool OpenRefining()
    {
        var state = EnsureRefining(CurrentCityId);
        if (state == null || RunStatus != PuzzleRunStatus.InProgress ||
            state.Phase == PuzzleRefiningPhase.Working)
        {
            IsRefiningOpen = false;
            return false;
        }

        IsRefiningOpen = true;
        NotifyRefining(state);
        return true;
    }

    internal void CloseRefining()
    {
        IsRefiningOpen = false;
    }

    // 닫을 수 있는지. Ready는 산출물을 도시에 남긴 채 닫고, Idle은 보드를 비운 뒤에만 닫는다.
    internal bool TryLeaveRefining(out string _reason)
    {
        _reason = null;
        var state = EnsureRefining(CurrentCityId);
        if (state == null || state.Phase == PuzzleRefiningPhase.Ready)
        {
            return true;
        }
        if (state.Board.Placements.Count > 0)
        {
            _reason = BasicData.Text("rules.refining.returnItems");
            return false;
        }

        return true;
    }

    internal bool RefiningConfirmable(string _cityId = null)
    {
        return TryEvaluateRefining(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId, out _);
    }

    // 확인 버튼의 조건과 사유를 한 곳에서 정한다. 반복 조회해도 상태·돈·난수는 바뀌지 않는다.
    // 공간 검사는 최대 산출 기준으로 해서 완료 후 산출물을 잃는 상황을 막는다.
    bool TryEvaluateRefining(string _cityId, out string _reason)
    {
        _reason = null;
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            _reason = BasicData.Text("rules.refining.blocked");
            return false;
        }

        var state = EnsureRefining(_cityId);
        if (state == null)
        {
            _reason = BasicData.Text("rules.refining.unavailable");
            return false;
        }
        if (state.Phase == PuzzleRefiningPhase.Working)
        {
            _reason = BasicData.Text("rules.refining.inProgress");
            return false;
        }
        if (state.Phase == PuzzleRefiningPhase.Ready)
        {
            _reason = BasicData.Text("rules.refining.awaitingPickup");
            return false;
        }
        if (state.Board.Placements.Count == 0)
        {
            _reason = BasicData.Text("rules.refining.empty");
            return false;
        }

        var maxOutputs = new List<string>();
        foreach (var placement in state.Board.Placements)
        {
            // 입력 허용은 변환표에 등록한 품목 ID가 기준이다. Ore 태그를 갖지만 입력으로 등록되지 않은
            // 금속 주괴는 noConversion으로, 태그 자체가 없는 화물은 notOre로 막는다.
            if (!TryFindRefiningConversion(state.Action, placement.Definition.Id, out var conversion))
            {
                _reason = placement.Definition.Tags.Contains(state.Action.inputTag)
                    ? BasicData.Format("rules.refining.noConversion", placement.Definition.DisplayName)
                    : BasicData.Format("rules.refining.notOre", placement.Definition.DisplayName);
                return false;
            }
            for (int i = 0; i < conversion.maxOutputCount; i++)
            {
                maxOutputs.Add(conversion.outputPieceId);
            }
        }

        // 모든 입력의 최대 산출 합이 빈 보드에 모두 들어갈 때만 확인을 허용한다.
        if (!TryFitsOutputs(state.Action, maxOutputs))
        {
            _reason = BasicData.Text("rules.refining.noSpace");
            return false;
        }

        if (Funds < state.Action.cost)
        {
            _reason = BasicData.Format("rules.refining.insufficientFunds", state.Action.cost);
            return false;
        }

        return true;
    }

    internal bool TryConfirmRefining(out string _reason)
    {
        if (!TryEvaluateRefining(CurrentCityId, out _reason))
        {
            return false;
        }
        if (!IsRefiningOpen)
        {
            _reason = BasicData.Text("rules.refining.blocked");
            return false;
        }

        var state = EnsureRefining(CurrentCityId);
        if (state == null)
        {
            _reason = BasicData.Text("rules.refining.unavailable");
            return false;
        }

        // 확인 직전에 비용·잔액과 완료 시각을 다시 계산한다. 여기까지 실패하면 보드·상태·자금을 하나도 바꾸지 않는다.
        long cost = state.Action.cost;
        if (Funds < cost)
        {
            _reason = BasicData.Format("rules.refining.insufficientFunds", cost);
            return false;
        }

        long readyAtMinutes;
        try
        {
            readyAtMinutes = checked(Time.ElapsedMinutes + state.Action.workMinutes);
        }
        catch (OverflowException)
        {
            _reason = BasicData.Text("rules.refining.timeOverflow");
            return false;
        }

        // 성공 확인 후에만 난수를 쓴다. 인스턴스 ID의 결정적 순서로 품목별 범위에서 독립적으로 한 번씩 추첨한다.
        var inputs = state.Board.Placements.OrderBy(_placement => _placement.InstanceId).ToArray();
        var outputs = new List<string>();
        foreach (var placement in inputs)
        {
            if (!TryFindRefiningConversion(state.Action, placement.Definition.Id, out var conversion))
            {
                _reason = BasicData.Format("rules.refining.noConversion", placement.Definition.DisplayName);
                return false;
            }
            int count = refiningRandom.Next(conversion.minOutputCount, conversion.maxOutputCount + 1);
            for (int i = 0; i < count; i++)
            {
                outputs.Add(conversion.outputPieceId);
            }
        }

        // 입력 제거·산출물 예약·상태 변경·비용 차감을 한 확정 단위로 처리한다.
        foreach (var placement in inputs)
        {
            _ = state.Board.Remove(placement);
        }

        long previousFunds = Funds;
        Funds -= cost;
        state.PendingOutputs.Clear();
        state.PendingOutputs.AddRange(outputs);
        state.OutputCount = outputs.Count;
        state.ReadyAtMinutes = readyAtMinutes;
        state.Phase = PuzzleRefiningPhase.Working;
        NotifyRefining(state);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
    }

    // 입력 품목 ID의 정제 변환. 변환표 등록이 허용 기준이며 산출 금속은 입력으로 등록되지 않는다.
    static bool TryFindRefiningConversion(PuzzleCityActionData _action, string _pieceId,
        out PuzzleActionConversionData _conversion)
    {
        foreach (var conversion in _action.conversions ?? Array.Empty<PuzzleActionConversionData>())
        {
            if (conversion != null && conversion.minOutputCount > 0 &&
                conversion.maxOutputCount >= conversion.minOutputCount &&
                !string.IsNullOrEmpty(conversion.outputPieceId) &&
                string.Equals(conversion.inputPieceId, _pieceId, StringComparison.Ordinal))
            {
                _conversion = conversion;
                return true;
            }
        }

        _conversion = null;
        return false;
    }

    // 시간이 흐른 뒤 완료된 정제를 처리한다. 조회 시점에도 같은 판정을 하므로 지연 판정이다.
    void CheckRefiningReady()
    {
        // 완료 발행 중 구독자가 정제대를 새로 만들 수 있어 스냅샷을 순회한다.
        foreach (var state in refinings.Values.ToArray())
        {
            RefreshRefiningReady(state);
        }
    }

    PuzzleRefiningState RefreshRefiningReady(PuzzleRefiningState _state)
    {
        if (_state.Phase != PuzzleRefiningPhase.Working || Time == null ||
            Time.ElapsedMinutes < _state.ReadyAtMinutes)
        {
            return _state;
        }

        bool changed = false;
        foreach (string pieceId in _state.PendingOutputs.ToArray())
        {
            if (lastInstanceId == int.MaxValue)
            {
                break;
            }
            // 예약한 결과는 재추첨하지 않는다. 배치에 실패하면 예약을 보존해 다음 판정 때 다시 놓는다.
            if (definitions.TryGetValue(pieceId, out var definition) &&
                _state.Board.TryAutoPlace(definition, ref lastInstanceId))
            {
                _state.PendingOutputs.Remove(pieceId);
                changed = true;
            }
        }

        if (_state.PendingOutputs.Count == 0)
        {
            _state.Phase = PuzzleRefiningPhase.Ready;
            _state.ReadyAtMinutes = -1;
            changed = true;
        }
        if (changed)
        {
            NotifyRefining(_state);
        }
        return _state;
    }

    // 정제대가 관계하는 이동의 허용 범위. 거래는 없고 상태별 방향만 검사한다.
    internal bool CanMoveToRefining(PuzzleInventory _source, PuzzleInventory _destination)
    {
        if (!IsRefiningOpen)
        {
            return false;
        }
        var state = RefiningStateOf(_source) ?? RefiningStateOf(_destination);
        if (state == null || !ReferenceEquals(state.Action, RefiningAction()))
        {
            return false;
        }

        bool sameBoard = ReferenceEquals(state.Board, _source) && ReferenceEquals(state.Board, _destination);
        switch (state.Phase)
        {
            case PuzzleRefiningPhase.Idle:
                // 맡기러 넣는 이동과 기차로 되 빼내는 이동, 보드 안 재배치를 모두 허용한다.
                return sameBoard || (ReferenceEquals(state.Board, _destination)
                    ? IsTrainWagon(_source)
                    : IsTrainWagon(_destination));
            case PuzzleRefiningPhase.Ready:
                // 산출물 회수와 보드 안 재배치만 허용한다. 새 화물은 넣지 못한다.
                return sameBoard || ReferenceEquals(state.Board, _source) && IsTrainWagon(_destination);
            default:
                return false;
        }
    }

    // 드래그 확정 후 호출한다. Ready에서 산출물을 모두 꺼내면 다시 맡길 수 있는 Idle로 돌아간다.
    internal void RefreshRefiningAfterMove(PuzzleInventory _board)
    {
        var state = RefiningStateOf(_board);
        if (state == null)
        {
            return;
        }

        RefreshRefiningReady(state);
        if (state.Phase == PuzzleRefiningPhase.Ready && state.Board.Placements.Count == 0)
        {
            state.Phase = PuzzleRefiningPhase.Idle;
        }
        NotifyRefining(state);
    }

    // 정제대는 UI가 상태를 조회해 스스로 그린다. 변화가 있을 때 발행지 한 곳에서만 알린다.
    void NotifyRefining(PuzzleRefiningState _state)
    {
        GameEvents.RaiseRefiningChanged(this, _state.CityId, _state.Phase,
            IsRefiningOpen && string.Equals(_state.CityId, CurrentCityId, StringComparison.Ordinal));
    }

    #endregion
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
    internal const string TAG_SEPARATOR = "+";
    const int BASE_PERCENT = 100;

    // 적용된 태그를 priority 내림차순으로 이은 표기. 일반(규칙 없음)이면 null.
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

    // 합연산: 도시 기본 배율 + Σ(일치한 태그 규칙 배율 − 100). 구매·판매를 따로 더하며 0 이하도 허용한다.
    // 태그 행의 sellPercent는 TableManager가 판매 등급 단일 원본(sellGrades)으로 해결한 실효 값이다.
    // 희귀는 별도 공식을 쓰지 않고 같은 합산 경로로 +50%가 반영된다.
    internal static PuzzleTradeRate Combine(PuzzleCityData _city, IEnumerable<PuzzleCityTagPriceData> _rules)
    {
        var ordered = _rules.OrderByDescending(_rule => _rule.priority).ToList();
        int buy = _city.buyPercentOfBaseValue + ordered.Sum(_rule => _rule.buyPercent - BASE_PERCENT);
        int sell = _city.sellPercentOfFinalValue + ordered.Sum(_rule => _rule.sellPercent - BASE_PERCENT);
        string tag = ordered.Count == 0 ? null : string.Join(TAG_SEPARATOR, ordered.Select(_rule => _rule.tag));
        return new PuzzleTradeRate(tag, buy, sell);
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
    // 같은 방문 중 산 마을에 되파는 구매 취소. 판매가는 낸 금액 그대로다.
    internal bool PurchaseCancel { get; }
    internal bool CanDrop => Placement.CanPlace && TradeFailure == PuzzleTradeFailure.None;

    internal PuzzleDropResult(PuzzlePlacementResult _placement, PuzzleTradeDirection _direction = PuzzleTradeDirection.None,
        long _price = 0, PuzzleTradeFailure _tradeFailure = PuzzleTradeFailure.None, int _percent = 0, string _tag = null,
        bool _purchaseCancel = false)
    {
        PurchaseCancel = _purchaseCancel;
        Placement = _placement;
        Direction = _direction;
        Price = _price;
        TradeFailure = _tradeFailure;
        AppliedPercent = _percent;
        AppliedTag = _tag;
    }
}
