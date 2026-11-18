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
    int lastWagonNumber;
    string currentCityId;
    readonly HashSet<string> visitedCities = new HashSet<string>(StringComparer.Ordinal);
    readonly List<TavernRumor> tavernRumors = new List<TavernRumor>();
    System.Random stockRandom = new System.Random();

    internal readonly struct TavernRumor
    {
        public readonly PuzzleRumorKind Kind;
        public readonly string TargetCityId;
        public readonly string SourceCityId;
        public readonly string Message;
        // 특산 소문이 언급하는 품목 ID.
        public readonly string ItemId;
        // 희귀 소문은 수요 태그, 특산 소문은 대상 마을의 특산 태그다.
        public readonly string Tag;
        public readonly string ActionId;
        public readonly long RecordedTurns;

        public TavernRumor(PuzzleRumorKind _kind, string _targetCityId, string _sourceCityId, string _message,
            string _itemId, string _tag, string _actionId, long _recordedTurns)
        {
            Kind = _kind;
            TargetCityId = _targetCityId;
            SourceCityId = _sourceCityId;
            Message = _message;
            ItemId = _itemId ?? string.Empty;
            Tag = _tag ?? string.Empty;
            ActionId = _actionId ?? string.Empty;
            RecordedTurns = _recordedTurns;
        }
    }

    internal IReadOnlyList<TavernRumor> TavernRumors => tavernRumors;

    internal bool HasRumorForCity(string _cityId)
    {
        return tavernRumors.Any(_rumor => string.Equals(_rumor.TargetCityId, _cityId, StringComparison.Ordinal));
    }

    // 지도 배지는 소문 기록이 있고 아직 방문하지 않은 마을에만 보인다. 기록 자체는 수첩·요약·중복 방지용으로 남긴다.
    internal bool HasUnvisitedRumorForCity(string _cityId)
    {
        return HasRumorForCity(_cityId) && !HasVisitedCity(_cityId);
    }

    // 소문을 들을 때마다 남긴다. 문구는 이미 결정된 후보에서 만들어 함께 저장한다.
    // 같은 도시의 소문은 후보 단계(IsRumorTarget)에서 걸러 중복 기록하지 않는다.
    internal void RecordTavernRumor(PuzzleRumorCandidate _candidate, string _message)
    {
        tavernRumors.Add(new TavernRumor(_candidate.Kind, _candidate.CityId, CurrentCityId, _message,
            _candidate.ItemId, _candidate.Tag, _candidate.ActionId, Time.ElapsedTurns));
        GameEvents.RaiseTavernRumorRecorded(this, CurrentCityId, _candidate.CityId, _message,
            Time.ElapsedTurns, _candidate.Tag, _candidate.Kind, _candidate.ItemId, _candidate.ActionId);
    }

    internal bool HasVisitedCity(string _cityId)
    {
        return visitedCities.Contains(_cityId);
    }

    readonly List<PuzzleInventory> trainWagons = new List<PuzzleInventory>();
    Dictionary<(int First, int Second), BreedingState> breedingProgress = new();
    // 동물 인스턴스 ID별 배설 진행. 대기 상태를 포함해 씬 안에서만 살며 플레이어에게 표시하지 않는다.
    Dictionary<int, WasteState> wasteProgress = new();
    // 조합 진행. 두 입력 인스턴스 ID를 작은 쪽 먼저 정렬한 키로 관리하며 씬 안에서만 산다.
    Dictionary<(int First, int Second), long> combinationProgress = new();
    // 동물 인스턴스 ID별 곡물 섭취 진행(섭취 주기 안의 경과 턴). 배설과 같이 씬 안에서만 산다.
    Dictionary<int, long> feedingProgress = new();
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
    // 증차 상한. 기본 상한이 0 이하이면 무제한이고, 평판 단계의 추가 상한은 무제한에 더하지 않는다.
    internal int MaxWagonCount
    {
        get
        {
            int baseCount = tables.PuzzleTrade.maxWagonCount;
            return baseCount <= 0 ? baseCount : baseCount + WagonLimitBonus;
        }
    }
    // 도달한 평판 단계 중 가장 큰 증차 상한 보너스.
    internal int WagonLimitBonus => tables.Quest.reputationLevels
        .Where(_level => _level != null && _level.reputation <= Reputation)
        .Select(_level => _level.wagonLimitBonus)
        .DefaultIfEmpty(0)
        .Max();
    // 다음 정산 게이지 예상치용. 실제 6시간 정산(ApplySynergyActivations)이 읽는 평가(Value)와 같은 값을 쓴다.
    internal decimal TrainSynergyDelta => trainWagons.Sum(_wagon => _wagon.Value.FinalValue - _wagon.Value.BaseValue);
    public bool CanAddWagon => RunStatus == PuzzleRunStatus.InProgress
        && (MaxWagonCount <= 0 || trainWagons.Count < MaxWagonCount)
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
        string modificationId = PuzzleWagonRefrigeration.ModificationId;
        string board = Train != null ? BoardId(Train) : null;
        string failure = RunStatus != PuzzleRunStatus.InProgress ? "run_finished"
            : Train == null ? "unavailable"
            : HasWagonRefrigeration ? "already_installed"
            : Funds < WagonRefrigerationCost ? "insufficient_funds"
            : null;
        if (failure == null && !TryInstallWagonModification(Train,
                PuzzleWagonModificationRegistry.Create(modificationId, tables)))
        {
            failure = "install_failed";
        }
        if (failure != null)
        {
            RecordAction(PuzzleActionKind.InstallWagonModification, failure, _modificationId: modificationId,
                _board: board);
            return false;
        }

        long previousFunds = Funds;
        Funds -= WagonRefrigerationCost;
        GameEvents.RaiseFundsChanged(this, previousFunds);
        RecordAction(PuzzleActionKind.InstallWagonModification, _modificationId: modificationId, _board: board);
        return true;
    }

    // 분석 로그의 작업대 보드 ID 접두사. 작업대 보드는 workbench:<cityId>:<actionId>다.
    internal const string WORKBENCH_BOARD_PREFIX = "workbench:";

    internal string BoardId(PuzzleInventory _inventory)
    {
        if (IsTrainWagon(_inventory))
        {
            return "train:" + (_inventory.WagonNumber - 1);
        }
        if (_inventory == Market)
        {
            return "market:" + CurrentCityId;
        }

        var butchery = ButcheryStateOf(_inventory);
        if (butchery != null)
        {
            return WorkbenchBoardId(butchery.CityId, butchery.Action);
        }
        var modification = ModificationStateOf(_inventory);
        if (modification != null)
        {
            return WorkbenchBoardId(modification.CityId, modification.Action);
        }
        var refining = RefiningStateOf(_inventory);
        return refining != null ? WorkbenchBoardId(refining.CityId, refining.Action) : null;
    }

    static string WorkbenchBoardId(string _cityId, PuzzleCityActionData _action)
    {
        return WORKBENCH_BOARD_PREFIX + _cityId + ":" + _action.id;
    }

    // 분석 로그가 관측하는 모든 도시의 작업대 보드. 상태 사전을 읽기만 하므로 완료 판정을 일으키지 않는다.
    internal IEnumerable<PuzzleInventory> WorkbenchBoards =>
        butcheries.Values.Select(_state => _state.Board)
            .Concat(modifications.Values.Select(_state => _state.Board))
            .Concat(refinings.Values.Select(_state => _state.Board));

    void RecordAction(PuzzleActionKind _action, string _failure = null, string _targetCityId = null,
        long _requestedTurns = 0, string _actionId = null, string _modeId = null, string _productId = null,
        string _modificationId = null, string _board = null, string[] _wagonOrder = null)
    {
        GameEvents.RaisePuzzleActionCommitted(new GameEvents.PuzzleActionInfo(this, _action, _failure,
            _targetCityId, _requestedTurns: _requestedTurns, _actionId: _actionId, _modeId: _modeId,
            _productId: _productId, _modificationId: _modificationId, _board: _board, _wagonOrder: _wagonOrder));
    }

    void OnDestroy()
    {
        HandleRunStatusChanged();
        if (Time != null)
        {
            GameEvents.RaisePuzzleRunEnded(this, "scene_unloaded");
        }
    }

    void HandleRunStatusChanged()
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            if (RoutePlan != null)
            {
                EndRoutePlan(PuzzleRouteEndReason.RunEnded);
            }
            IsPassingThrough = false;
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
        // 제자리 놓기는 상태 변화가 없는 조작이므로 기록하지 않는다.
        if (_sourceIndex == _destinationIndex)
        {
            return false;
        }
        string failure = RunStatus != PuzzleRunStatus.InProgress ? "run_finished"
            : _sourceIndex < 0 || _sourceIndex >= trainWagons.Count ||
                _destinationIndex < 0 || _destinationIndex >= trainWagons.Count ? "invalid_index"
            : null;
        if (failure != null)
        {
            RecordAction(PuzzleActionKind.ReorderWagon, failure);
            return false;
        }

        var selected = Train;
        var wagon = trainWagons[_sourceIndex];
        trainWagons.RemoveAt(_sourceIndex);
        trainWagons.Insert(_destinationIndex, wagon);
        CurrentWagonIndex = trainWagons.IndexOf(selected);
        RecordAction(PuzzleActionKind.ReorderWagon, _wagonOrder: trainWagons.Select(BoardId).ToArray());
        return true;
    }

    internal bool TryAddWagon(out string _failureReason)
    {
        // 시뮬레이터가 쓰는 증차 경로. 경제 관찰만 하므로 턴을 소모하지 않는다.
        return TryPurchaseWagon(NextWagonCost, null, out _failureReason, _spendsTurn: false);
    }

    internal string WagonSaleFailure(int _index)
    {
        if (RunStatus != PuzzleRunStatus.InProgress || _index < 0 || _index >= trainWagons.Count)
        {
            return "saleUnavailable";
        }
        if (trainWagons.Count <= 1)
        {
            return "saleLastWagon";
        }
        return trainWagons[_index].OccupiedCells > 0 ? "saleLoaded" : null;
    }

    internal bool TrySellWagon(int _index, out string _failure)
    {
        _failure = WagonSaleFailure(_index);
        if (_failure != null)
        {
            RecordAction(PuzzleActionKind.SellWagon, _failure);
            return false;
        }

        var wagon = trainWagons[_index];
        string board = BoardId(wagon);
        long previousFunds = Funds;
        long saleFunds = checked(Funds + wagon.WagonSalePrice);
        trainWagons.RemoveAt(_index);
        if (CurrentWagonIndex >= _index)
        {
            CurrentWagonIndex = CurrentWagonIndex == _index
                ? Math.Min(_index, trainWagons.Count - 1)
                : CurrentWagonIndex - 1;
        }
        Funds = saleFunds;
        GameEvents.RaiseFundsChanged(this, previousFunds);
        RecordAction(PuzzleActionKind.SellWagon, _productId: wagon.WagonProduct?.id, _board: board,
            _wagonOrder: trainWagons.Select(BoardId).ToArray());
        return true;
    }

    internal bool TryPurchaseWagon(string _productId, out string _failureReason)
    {
        var product = tables.PuzzleTrade.wagonProducts.FirstOrDefault(
            _product => string.Equals(_product.id, _productId, StringComparison.Ordinal));
        if (product == null)
        {
            _failureReason = BasicData.Text("rules.wagon.unknownProduct");
            RecordAction(PuzzleActionKind.AddWagon, "unknown_wagon_product", _productId: _productId);
            return false;
        }

        return TryPurchaseWagon(product.price, product, out _failureReason, _spendsTurn: true);
    }

    // 객차를 산다. 모든 검증을 통과한 뒤 역 행동 1턴을 보내고, 턴을 실제로 넘긴 다음에 객차를 추가하고 자금을 뺀다.
    bool TryPurchaseWagon(long _cost, PuzzleWagonProductData _product, out string _failureReason, bool _spendsTurn)
    {
        string _productId = _product?.id;
        string _modificationId = _product?.modificationId;
        _failureReason = null;
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            _failureReason = BasicData.Text("rules.wagon.inProgressOnly");
            RecordAction(PuzzleActionKind.AddWagon, "run_finished", _productId: _productId,
                _modificationId: _modificationId);
            return false;
        }
        int maxWagons = MaxWagonCount;
        if (maxWagons > 0 && trainWagons.Count >= maxWagons)
        {
            _failureReason = BasicData.Format("rules.wagon.maxReached", maxWagons);
            RecordAction(PuzzleActionKind.AddWagon, "max_wagons", _productId: _productId,
                _modificationId: _modificationId);
            return false;
        }
        // 상품에 달린 평판 해금. 시뮬레이터로 넘겨지는 구매(product이 없는 TryAddWagon)는 상품 검사 대상이 아니다.
        if (_product != null && Reputation < _product.requiredReputation)
        {
            _failureReason = BasicData.Format("rules.wagon.locked", _product.requiredReputation);
            RecordAction(PuzzleActionKind.AddWagon, "locked", _productId: _productId,
                _modificationId: _modificationId);
            return false;
        }
        if (Funds < _cost)
        {
            _failureReason = BasicData.Format("rules.wagon.insufficientFunds", _cost);
            RecordAction(PuzzleActionKind.AddWagon, "insufficient_funds", _productId: _productId,
                _modificationId: _modificationId);
            return false;
        }

        var trainRules = tables.SynergiesFor(PuzzleSynergyScope.Train);
        int nextInstanceId = lastInstanceId;
        var newWagon = new PuzzleInventory(tables.Puzzle.train, Array.Empty<PuzzleStartingPlacementData>(), definitions, trainRules,
            ref nextInstanceId);
        newWagon.SynergyComboBonusPercent = SynergyComboBonusPercent;
        if (!string.IsNullOrEmpty(_modificationId) &&
            (!PuzzleWagonModificationRegistry.Contains(_modificationId) ||
             !newWagon.TryInstallWagonModification(PuzzleWagonModificationRegistry.Create(_modificationId, tables))))
        {
            _failureReason = BasicData.Text("rules.wagon.modificationFailed");
            RecordAction(PuzzleActionKind.AddWagon, "wagon_modification_failed", _productId: _productId,
                _modificationId: _modificationId);
            return false;
        }

        // 검증이 끝났다. 기한을 넘겨 1턴을 낼 수 없으면 구매하지 않는다.
        if (_spendsTurn && !CanSpendStationTurn)
        {
            _failureReason = BasicData.Text("rules.wagon.turnDeadline");
            RecordAction(PuzzleActionKind.AddWagon, "deadline", _productId: _productId,
                _modificationId: _modificationId);
            return false;
        }
        // 턴을 먼저 보낸다. 커밋이 실패하거나 턴 뒤 게임이 끝났으면 객차를 받지 않는다.
        if (_spendsTurn && (!AdvanceTurnsFor(StationActionTurns, PuzzleActionKind.AddWagon, null) ||
            RunStatus != PuzzleRunStatus.InProgress))
        {
            _failureReason = BasicData.Text("rules.wagon.turnDeadline");
            return false;
        }

        long previousFunds = Funds;
        newWagon.WagonNumber = ++lastWagonNumber;
        newWagon.WagonProduct = _product ?? tables.PuzzleTrade.wagonProducts.FirstOrDefault(
            _entry => string.IsNullOrEmpty(_entry.modificationId) && _entry.price == _cost);
        newWagon.WagonSalePrice = newWagon.WagonProduct?.salePrice ?? 0;
        trainWagons.Add(newWagon);
        Funds -= _cost;
        lastInstanceId = nextInstanceId;
        CurrentWagonIndex = trainWagons.Count - 1;
        GameEvents.RaiseFundsChanged(this, previousFunds);
        // 턴을 보냈다는 기록은 AdvanceTurnsFor가 남긴다. 여기서는 산 객차의 상품과 개조를 덧붙인다.
        RecordAction(PuzzleActionKind.AddWagon, _productId: _productId, _modificationId: _modificationId);
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
    // 시장 입장 기록. 다른 역 행동을 고르거나(ForgetPaidFacility), 다른 이유로 시간이 흐르거나,
    // 다른 역으로 떠나면 사라진다. 모든 시설 진입이 턴 무료라 이 기록은 시장 거래 허용 판정에만 쓴다.
    internal StationFacility PaidFacility { get; private set; }
    // 시장 입장이 유효한지. 시장에 들어갈 때만 밤인지 확인하므로, 시계가 밤이 되어도 이번 입장 동안은 거래할 수 있다.
    public bool MarketEntered => PaidFacility == StationFacility.Market;
    public bool CanTradeInMarket => MarketEntered && RunStatus == PuzzleRunStatus.InProgress;
    // 역 행동(소문 청취·휴식·작업대·화차 구매)이 쓰는 턴.
    internal long StationActionTurns => tables.PuzzleTime.stationActionTurns;
    // 역 행동 1턴을 쓸 수 있는지. 기한을 넘기는 행동은 막는다.
    internal bool CanSpendStationTurn => RunStatus == PuzzleRunStatus.InProgress && Time != null &&
        Time.CanArriveBeforeDeadline(StationActionTurns);

    // 시장에 들어간다. 밤이면 들어가지 못한다. 진입은 턴을 쓰지 않는다.
    internal bool TryEnterMarket()
    {
        if (MarketEntered)
        {
            return RunStatus == PuzzleRunStatus.InProgress;
        }
        if (!IsMarketOpen)
        {
            RecordAction(PuzzleActionKind.EnterMarket, "market_closed");
            return false;
        }
        PaidFacility = StationFacility.Market;
        return true;
    }

    internal bool TryEnterTavern()
    {
        // 술집 진입은 턴을 쓰지 않는다. 대신 낸 시장 입장을 잊는다.
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return false;
        }
        ForgetPaidFacility();
        return true;
    }

    internal bool TryEnterWagonFacility()
    {
        // 화차 시설 진입은 턴을 쓰지 않는다. 구매 자체(TryPurchaseWagon)가 1턴을 쓴다.
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return false;
        }
        ForgetPaidFacility();
        return true;
    }

    // 다른 역 행동(휴식·작업대 화면 등)을 골랐다. 이후 시장에 다시 들어가면 입장을 다시 연다.
    internal void ForgetPaidFacility()
    {
        PaidFacility = StationFacility.None;
    }

    public string PreviewCityId { get; private set; }
    // 미리보기 목적지로의 이동 턴. 이동은 항상 1턴이다.
    internal long? PreviewTurns => !string.IsNullOrEmpty(PreviewCityId) &&
        tables.TryGetRoute(CurrentCityId, PreviewCityId, out _) ? 1L : (long?)null;

    long SynergyIntervalTurns => (long)tables.PuzzleSynergy.activationIntervalTurns;

    internal long SynergyActivationsFor(long _turns)
    {
        return Time.IntervalCountAfter(_turns, SynergyIntervalTurns);
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
        return AdvanceTurns(Time.TurnsUntilDaylight, PuzzleActionKind.WaitForDaylight);
    }
    public string DebtCityId => tables.PuzzleTrade.debtCityId;
    public bool CanClearGame => tables != null && RunStatus == PuzzleRunStatus.InProgress && Funds >= GameClearCost
        && !Time.IsOverdue && (string.IsNullOrEmpty(DebtCityId) || CurrentCityId == DebtCityId);
    public int CityMoveCount { get; private set; }
    public PuzzleRunStatus RunStatus { get; private set; }
    internal string CurrentCityName => CityData.displayName;
    internal bool CanTravel => tables != null && !string.IsNullOrEmpty(currentCityId) && tables.RoutesFrom(currentCityId).Count > 0;
    internal long ElapsedTurns => Time.ElapsedTurns;
    internal PuzzleRegionData RegionData => tables.Region(CityData.regionId);
    internal long MarketCapacity { get; private set; }
    long marketStockCount;
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
        Time = new PuzzleTimeManager(tables.PuzzleTrade.deadlineTurns, tables.PuzzleTime);
        lastInstanceId = 0;
        var trainRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Train);
        marketRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Market);
        var train = new PuzzleInventory(validatedTables.StartingTrainBoard, validatedTables.TrainStartingPlacements, definitions, trainRules,
            ref lastInstanceId);
        train.SynergyComboBonusPercent = SynergyComboBonusPercent;
        // 시작 지급 화물도 처음부터 소유 중이므로 게임 시작을 보유 시간의 기준으로 삼는다.
        foreach (var item in train.Placements)
        {
            item.SetOwned(true);
        }
        var cityMarkets = new Dictionary<string, PuzzleInventory>(StringComparer.Ordinal);
        long marketCapacity = 0;
        marketStockCount = 0;
        foreach (var city in validatedTables.Cities)
        {
            var market = CreateMarket(city.id, ref lastInstanceId);
            cityMarkets.Add(city.id, market);
            marketStockCount += market.Placements.Count;
            marketCapacity = Math.Max(marketCapacity, (long)market.Width * market.Height);
        }

        markets = cityMarkets;
        ItemDefinitions = definitions.Values;
        MarketCapacity = marketCapacity;
        currentCityId = tables.Puzzle.startCity;
        visitedCities.Clear();
        visitedCities.Add(currentCityId);
        tavernRumors.Clear();
        ResetQuests();
        ResetProficiency();
        RoutePlan = null;
        IsPassingThrough = false;
        Funds = tables.PuzzleTrade.startingFunds;
        CityMoveCount = 0;
        RunStatus = PuzzleRunStatus.InProgress;
        trainWagons.Clear();
        lastWagonNumber = train.WagonNumber = 1;
        train.WagonSalePrice = validatedTables.PuzzleTrade.startingWagonSalePrice;
        trainWagons.Add(train);
        breedingProgress.Clear();
        wasteProgress.Clear();
        combinationProgress.Clear();
        feedingProgress.Clear();
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
        var market = new PuzzleInventory(city.market, placements, definitions, marketRules,
            ref _lastInstanceId);
        market.SynergyComboBonusPercent = SynergyComboBonusPercent;
        return market;
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
        if (!TryTravelCost(route.cost, route.turns, tables.PuzzleRoute.costPerTurn,
            cells, tables.PuzzleRoute.costPerCell, out long cost, out long distanceCost))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.CostOverflow, _fromCityId: fromCityId, _toCityId: _cityId,
                _baseCost: route.cost, _cells: cells, _distance: route.turns);
        }

        // 이동은 노선 거리와 무관하게 항상 1턴이다. route.turns는 요금 거리 항으로만 쓴다.
        long travelTime = 1;
        try
        {
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
            if (CityMoveCount == int.MaxValue)
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
            _distance: route.turns, _distanceCost: distanceCost, _travelTurns: travelTime, _fundsAfter: Funds >= cost ? Funds - cost : Funds,
            _arrivalClockTurns: (int)((Time.TurnOfDay + travelTime % PuzzleTimeManager.TURNS_PER_DAY) % PuzzleTimeManager.TURNS_PER_DAY),
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
    internal static bool TryTravelCost(long _baseCost, int _distance, int _costPerTurn,
        int _cells, int _costPerCell, out long _cost, out long _distanceCost)
    {
        _cost = 0;
        _distanceCost = 0;
        try
        {
            _distanceCost = checked((long)_distance * _costPerTurn);
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
        HandleRunStatusChanged();
        GameEvents.RaiseFundsChanged(this, previousFunds);
        RecordAction(PuzzleActionKind.Repay);
        return true;
    }

    public bool AdvanceTurns(long _turns, PuzzleActionKind _action = PuzzleActionKind.AdvanceTime)
    {
        return AdvanceTurnsFor(_turns, _action, null);
    }

    // 작업대 작업이 끝날 때까지 남은 시간을 보낸다. 어느 작업을 기다렸는지 actionId로 남긴다.
    internal bool WaitForWorkbench(string _actionId, long _turns)
    {
        return AdvanceTurnsFor(_turns, PuzzleActionKind.WaitForWorkbench, _actionId);
    }

    bool AdvanceTurnsFor(long _turns, PuzzleActionKind _action, string _actionId)
    {
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            RecordAction(_action, "time_blocked", _requestedTurns: _turns, _actionId: _actionId);
            return false;
        }
        if (_turns == 0)
        {
            return true;
        }

        TimeAdvancePlan plan;
        PuzzleTravelFailure failure;
        try
        {
            failure = PrepareTimeAdvance(_turns, out plan);
        }
        catch (ArgumentOutOfRangeException)
        {
            RecordAction(_action, "invalid_duration", _requestedTurns: _turns, _actionId: _actionId);
            throw;
        }
        if (failure != PuzzleTravelFailure.None)
        {
            RecordAction(_action, "time_overflow", _requestedTurns: _turns, _actionId: _actionId);
            return false;
        }

        // 시간이 흐르면 낸 시장 입장이 사라진다. 시간 진행 이벤트보다 먼저 정리한다.
        PaidFacility = StationFacility.None;
        CommitTimeAdvance(_turns, plan);
        RecordAction(_action, _requestedTurns: _turns, _actionId: _actionId);
        return true;
    }

    internal decimal? PreviewValueAfter(PuzzlePlacement _item, long _turns)
    {
        int wagonIndex = trainWagons.IndexOf(_item.Owner);
        if (wagonIndex < 0 ||
            PrepareTimeAdvance(_turns, out var plan) != PuzzleTravelFailure.None)
        {
            return null;
        }

        // 미리보기 진행에서 곡물 섭취·조합으로 소모된 품목은 미래 가치가 없다. 원본은 건드리지 않는다.
        PuzzleInventory staged = plan.wagons[wagonIndex];
        return staged.HasPlacement(_item.InstanceId) ? staged.FreshnessAdjustedValueOf(_item.InstanceId) : (decimal?)null;
    }

    sealed class TimeAdvancePlan
    {
        internal readonly PuzzleInventory[] wagons;
        internal Dictionary<(int First, int Second), BreedingState> breedingProgress;
        internal Dictionary<int, WasteState> wasteProgress;
        internal Dictionary<(int First, int Second), long> combinationProgress;
        internal Dictionary<int, long> feedingProgress;
        internal int lastInstanceId;
        internal long marketRestocks;
        internal decimal? cargoValueBeforeSettlement;
        internal decimal? cargoValueBeforeFreshnessLoss;

        internal TimeAdvancePlan(PuzzleInventory[] _wagons,
            Dictionary<(int First, int Second), BreedingState> _breedingProgress,
            Dictionary<int, WasteState> _wasteProgress,
            Dictionary<(int First, int Second), long> _combinationProgress,
            Dictionary<int, long> _feedingProgress, int _lastInstanceId)
        {
            wagons = _wagons;
            breedingProgress = _breedingProgress;
            wasteProgress = _wasteProgress;
            combinationProgress = _combinationProgress;
            feedingProgress = _feedingProgress;
            lastInstanceId = _lastInstanceId;
        }
    }

    PuzzleTravelFailure PrepareTimeAdvance(long _turns, out TimeAdvancePlan _plan)
    {
        _plan = null;
        try
        {
            long arrival = Time.ArrivalAt(_turns);
            // 시간 진행이 거부되거나 미리보기인 경우 원본 객차와 번식·배설·조합 상태를 바꾸지 않는다.
            var wagons = trainWagons.Select(_wagon => _wagon.CopyForTimeAdvance()).ToArray();
            var plan = new TimeAdvancePlan(wagons,
                new Dictionary<(int First, int Second), BreedingState>(breedingProgress),
                new Dictionary<int, WasteState>(wasteProgress),
                new Dictionary<(int First, int Second), long>(combinationProgress),
                new Dictionary<int, long>(feedingProgress), lastInstanceId);
            var animals = tables.PuzzleSynergy.animals;
            var waste = definitions[animals.wastePieceId];
            var expiredFood = definitions[ExpiredFoodPieceId];
            long animalEventsThrough = Time.HasDeadline ? Math.Min(arrival, Time.DeadlineTurns) : arrival;
            plan.marketRestocks = Time.DaylightOpeningsThrough(animalEventsThrough);
            long breedingTurns = (long)animals.breedingTurns;
            long wasteTurns = (long)animals.wasteIntervalTurns;
            long holdTurns = (long)animals.completionHoldTurns;
            long feedIntervalTurns = (long)animals.feedIntervalTurns;
            long synergyInterval = SynergyIntervalTurns;
            var freshness = tables.PuzzleSynergy.freshness;
            var preservation = CreatePreservation(freshness);
            long cursor = Time.ElapsedTurns;
            var growth = new List<PuzzleGrowth>();
            var selectedWagon = wagons[CurrentWagonIndex];
            decimal[] initialWagonValues = wagons
                .Select(_wagon => _wagon.Placements.Sum(_item => _item.FreshnessAdjustedValue))
                .ToArray();
            bool settledSelectedWagon = false;
            decimal freshnessDelta = 0;
            // 이전 행동으로 공간이 확보된 경우 첫 경계를 기다리지 않고 대기를 푼다. 마지막 대기 시간은 이번 진행부터 다시 흐른다.
            ResumeBlockedStates(plan, wagons, animals, waste, growth, breedingTurns, wasteTurns, holdTurns);

            while (cursor < arrival)
            {
                EnsureWasteStates(plan, wagons, animals, wasteTurns, cursor);
                EnsureFeedingStates(plan, wagons, animals, feedIntervalTurns);
                var pairs = SelectBreedingPairs(wagons, plan.breedingProgress);
                var combinations = SelectCombinationPairs(wagons, plan.combinationProgress);
                long step = Math.Min(arrival - cursor, TurnsUntilCargoChange(wagons, preservation));
                step = Math.Min(step, TurnsUntilBirth(pairs, breedingTurns, animalEventsThrough - cursor));
                step = Math.Min(step, TurnsUntilWaste(wagons, plan.wasteProgress, animals.animalTag, wasteTurns,
                    animalEventsThrough - cursor));
                step = Math.Min(step, TurnsUntilCombination(combinations));
                step = Math.Min(step, TurnsUntilFeeding(wagons, plan.feedingProgress, animals.animalTag,
                    feedIntervalTurns, animalEventsThrough - cursor));
                long boundary = cursor + step;
                // 정산·배설·만료·조합 뒤 섭취하며 각 가속의 완료는 즉시 반영한다. 나머지 성장·출산은 그 뒤 처리한다.
                long activations = boundary / synergyInterval - cursor / synergyInterval;
                foreach (var wagon in wagons)
                {
                    if (wagon.ApplySynergyActivations(activations) && wagon == selectedWagon)
                    {
                        settledSelectedWagon = true;
                    }
                    foreach (var item in wagon.Placements)
                    {
                        bool trackFreshness = wagon == selectedWagon && item.RemainingShelfLifeTurns.HasValue;
                        decimal valueBefore = trackFreshness ? item.FreshnessAdjustedValue : 0;
                        item.ApplyElapsedTurns(step, preservation.DecayRate(wagon, item));
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
                    AdvanceWaste(plan, wagons, animals, waste, wasteTurns, holdTurns, step);
                }

                // 만료를 섭취보다 먼저 처리해 부패로 바뀐 곡물을 먹지 않게 한다.
                foreach (var wagon in wagons)
                {
                    decimal valueBeforeExpiry = wagon == selectedWagon
                        ? wagon.Placements.Sum(_item => _item.FreshnessAdjustedValue) : 0;
                    wagon.ReplaceExpiredFood(expiredFood);
                    if (wagon == selectedWagon)
                    {
                        freshnessDelta += wagon.Placements.Sum(_item => _item.FreshnessAdjustedValue) - valueBeforeExpiry;
                    }
                }

                // 만료로 입력이 바뀐 조합은 처리하지 않는다. 같은 시각의 절임 완료를 섭취보다 먼저 반영한다.
                AdvanceCombinations(plan, combinations, step);

                // 곡물 섭취와 가속은 성장·출산보다 먼저 처리한다. 섭취로 빈 칸이 성장 자리로 쓰인다.
                if (animalEvents)
                {
                    // 자연 경과를 먼저 더해 두 부모의 섭취가 같은 경계의 쌍 진행에 누적되게 한다.
                    AdvanceBreedingProgress(plan, pairs, step);
                    AdvanceFeeding(plan, wagons, animals, step, feedIntervalTurns, breedingTurns,
                        holdTurns, growth);
                }

                // 공간이 없는 성장은 전체 진행을 거절하지 않고 그 품목만 대기한다.
                growth.Clear();
                foreach (var wagon in wagons)
                {
                    wagon.PlanGrowth(growth);
                }
                foreach (var change in growth)
                {
                    change.Item.Owner.ApplyGrowth(change);
                }

                if (animalEvents)
                {
                    CompleteBreeding(plan, SelectBreedingPairs(wagons, plan.breedingProgress), breedingTurns,
                        holdTurns);
                }
                cursor = boundary;
            }

            for (int i = 0; i < wagons.Length; i++)
            {
                if (wagons[i].Placements.Sum(_item => _item.FreshnessAdjustedValue) != initialWagonValues[i])
                {
                    wagons[i].HasUnreadEventNotice = true;
                }
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
            if (marketStockCount > 0 && plan.marketRestocks > (int.MaxValue - plan.lastInstanceId) / marketStockCount)
            {
                return PuzzleTravelFailure.TimeOverflow;
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
        PuzzleItemDefinition _waste, List<PuzzleGrowth> _growth, long _breedingTurns, long _wasteTurns,
        long _holdTurns)
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
                _plan.breedingProgress[pair.key] = new BreedingState(_breedingTurns - _holdTurns, false);
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
                    _plan.wasteProgress[animal.InstanceId] = new WasteState(_wasteTurns - _holdTurns, false);
                }
            }
        }
    }

    static long TurnsUntilCargoChange(PuzzleInventory[] _wagons, PuzzleShelfLifePreservation _preservation)
    {
        long turns = long.MaxValue;
        foreach (var wagon in _wagons)
        {
            foreach (var item in wagon.Placements)
            {
                // 성장 품목은 다음 완료 시각을, 자리를 대기 중이면 재시도 시각을 경계로 제시한다.
                turns = Math.Min(turns, Math.Max(1, item.TurnsUntilGrowthCompletion));
                // 유통기한은 턴마다 정수로 소모하고 냉장 접촉도 턴마다 바뀔 수 있으므로 한 턴씩 진행한다.
                if (item.OwnedTurns >= 0 && item.RemainingShelfLifeTurns > 0 &&
                    item.TurnsUntilShelfLifeExpires(_preservation.DecayRate(wagon, item)) != long.MaxValue)
                {
                    turns = 1;
                }
            }
        }
        return turns;
    }

    internal long? TurnsUntilNextFreshnessBand(PuzzlePlacement _item)
    {
        var freshness = tables.PuzzleSynergy.freshness;
        decimal decayRate = IsTrainWagon(_item.Owner) ? CreatePreservation(freshness).DecayRate(_item.Owner, _item) : 1m;
        return _item.TurnsUntilNextFreshnessBand(decayRate);
    }

    internal int AppliedPreservationPercent(PuzzlePlacement _item)
    {
        if (!IsTrainWagon(_item.Owner) || _item.RemainingShelfLifeTurns is not > 0)
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

    // 출산까지의 진행 턴과 공간 부족 대기 여부. 대기 중에는 진행이 멈추고 자리가 돌아오면 남은 시간을 흘려보낸다.
    readonly struct BreedingState
    {
        internal readonly long ElapsedTurns;
        internal readonly bool SpaceBlocked;

        internal BreedingState(long _elapsedTurns, bool _spaceBlocked)
        {
            ElapsedTurns = _elapsedTurns;
            SpaceBlocked = _spaceBlocked;
        }
    }

    // 동물 한 마리의 배설 진행. 출산과 같은 대기 규칙을 쓰지만 화면에는 표시하지 않는다.
    readonly struct WasteState
    {
        internal readonly long ElapsedTurns;
        internal readonly bool SpaceBlocked;

        internal WasteState(long _elapsedTurns, bool _spaceBlocked)
        {
            ElapsedTurns = _elapsedTurns;
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
            first.Owner.HasUnreadEventNotice = true;
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
    internal bool TryGetBreedingProgress(PuzzlePlacement _placement, out long _elapsedTurns, out bool _spaceBlocked)
    {
        _elapsedTurns = 0;
        _spaceBlocked = false;
        if (_placement == null || !trainWagons.Contains(_placement.Owner))
        {
            return false;
        }

        foreach (var pair in SelectBreedingPairs(trainWagons.ToArray(), breedingProgress))
        {
            if (pair.key.First == _placement.InstanceId || pair.key.Second == _placement.InstanceId)
            {
                _elapsedTurns = pair.state.ElapsedTurns;
                _spaceBlocked = pair.state.SpaceBlocked;
                return true;
            }
        }

        return false;
    }

    // 표시가 읽는 조합 진행. 쌍 선택 규칙을 그대로 쓰므로 같은 쌍의 두 입력은 같은 규칙과 경과 시간을 본다.
    // 규칙을 함께 돌려 여러 규칙에 걸린 품목은 실제로 맺힌 쌍의 설명에만 진행도를 붙인다.
    internal bool TryGetCombinationProgress(PuzzlePlacement _placement, out PuzzleCombinationRuleData _rule,
        out long _elapsedTurns)
    {
        _rule = null;
        _elapsedTurns = 0;
        if (_placement == null || !trainWagons.Contains(_placement.Owner))
        {
            return false;
        }

        foreach (var pair in SelectCombinationPairs(trainWagons.ToArray(), combinationProgress))
        {
            if (pair.key.First == _placement.InstanceId || pair.key.Second == _placement.InstanceId)
            {
                _rule = pair.rule;
                _elapsedTurns = pair.elapsedTurns;
                return true;
            }
        }

        return false;
    }

    static long TurnsUntilBirth(List<BreedingPair> _pairs, long _breedingTurns, long _maxRemainingTurns)
    {
        long next = long.MaxValue;
        foreach (var pair in _pairs)
        {
            // 대기 중인 쌍도 남은 시간이 같으므로 같은 경계에서 자리를 다시 본다.
            long remaining = _breedingTurns - pair.state.ElapsedTurns;
            if (remaining < next && remaining <= _maxRemainingTurns)
            {
                next = remaining;
            }
        }
        return Math.Max(1, next);
    }

    static void AdvanceBreedingProgress(TimeAdvancePlan _plan, List<BreedingPair> _pairs, long _step)
    {
        var progress = new Dictionary<(int First, int Second), BreedingState>();
        foreach (var pair in _pairs)
        {
            progress.Add(pair.key, pair.state.SpaceBlocked
                ? pair.state
                : new BreedingState(checked(pair.state.ElapsedTurns + _step), false));
        }
        _plan.breedingProgress = progress;
    }

    void CompleteBreeding(TimeAdvancePlan _plan, List<BreedingPair> _pairs, long _breedingTurns,
        long _holdTurns)
    {
        var progress = new Dictionary<(int First, int Second), BreedingState>();
        foreach (var pair in _pairs)
        {
            progress.Add(pair.key, CompleteBreedingPair(_plan, pair, pair.state.ElapsedTurns,
                _breedingTurns, _holdTurns));
        }
        _plan.breedingProgress = progress;
    }

    static BreedingState CompleteBreedingPair(TimeAdvancePlan _plan, BreedingPair _pair, long _elapsed,
        long _breedingTurns, long _holdTurns)
    {
        if (_pair.state.SpaceBlocked)
        {
            return _pair.wagon.CanAutoPlaceAll(_pair.offspring, _pair.offspringCount, _allowRotation: true)
                ? new BreedingState(_breedingTurns - _holdTurns, false)
                : _pair.state;
        }

        while (_elapsed >= _breedingTurns)
        {
            if (!_pair.wagon.TryAutoPlaceAll(_pair.offspring, _pair.offspringCount, ref _plan.lastInstanceId,
                _allowRotation: true, _markUnread: true))
            {
                _pair.RaiseSpaceNotice();
                return new BreedingState(_breedingTurns - _holdTurns, true);
            }
            _elapsed -= _breedingTurns;
        }
        return new BreedingState(_elapsed, false);
    }

    // 인접 두 품목의 조합 진행. 결과는 자리를 이어받는 입력의 칸에 그대로 들어가므로 공간 대기가 없고
    // 경과 턴만 이어간다. 키는 두 입력의 인스턴스 ID를 정렬해 쌍의 순서와 무관하게 본다.
    readonly struct CombinationPair
    {
        internal readonly (int First, int Second) key;
        internal readonly PuzzleCombinationRuleData rule;
        internal readonly PuzzlePlacement source;
        internal readonly PuzzlePlacement partner;
        internal readonly PuzzleInventory wagon;
        internal readonly PuzzleItemDefinition result;
        internal readonly long requiredTurns;
        internal readonly long elapsedTurns;

        internal CombinationPair(PuzzlePlacement _source, PuzzlePlacement _partner, PuzzleInventory _wagon,
            PuzzleCombinationRuleData _rule, PuzzleItemDefinition _result, long _elapsedTurns)
        {
            key = CombinationKey(_source.InstanceId, _partner.InstanceId);
            rule = _rule;
            source = _source;
            partner = _partner;
            wagon = _wagon;
            result = _result;
            requiredTurns = (long)_rule.requiredTurns;
            elapsedTurns = _elapsedTurns;
        }
    }

    static (int First, int Second) CombinationKey(int _left, int _right)
    {
        return _left <= _right ? (First: _left, Second: _right) : (First: _right, Second: _left);
    }

    // 규칙과 두 품목 ID로 조합 가능한 쌍을 고른다. 번식과 같이 기존 쌍을 먼저, 나머지는 인스턴스 ID순으로
    // 두고 한 품목이 여러 쌍에 동시에 들어가지 않게 한다. 인접 판정도 냉장과 같은 조건을 쓴다.
    List<CombinationPair> SelectCombinationPairs(PuzzleInventory[] _wagons,
        Dictionary<(int First, int Second), long> _previous)
    {
        var candidates = new List<CombinationPair>();
        foreach (var wagon in _wagons)
        {
            var items = wagon.Placements.OrderBy(_item => _item.InstanceId).ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                var first = items[i];
                for (int j = i + 1; j < items.Length; j++)
                {
                    var second = items[j];
                    if (!tables.TryGetCombinationBetween(first.Definition.Id, second.Definition.Id, out var rule,
                        out bool firstIsSource) || !Touches(first, second))
                    {
                        continue;
                    }

                    var source = firstIsSource ? first : second;
                    var partner = firstIsSource ? second : first;
                    _previous.TryGetValue(CombinationKey(source.InstanceId, partner.InstanceId), out long elapsed);
                    candidates.Add(new CombinationPair(source, partner, wagon, rule, definitions[rule.resultPieceId],
                        elapsed));
                }
            }
        }

        var pairedIds = new HashSet<int>();
        var pairs = new List<CombinationPair>();
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

    static long TurnsUntilCombination(List<CombinationPair> _pairs)
    {
        long next = long.MaxValue;
        foreach (var pair in _pairs)
        {
            long remaining = pair.requiredTurns - pair.elapsedTurns;
            if (remaining < next)
            {
                next = remaining;
            }
        }

        return Math.Max(1, next);
    }

    // 조합 완료 시각에 두 입력을 소모한다. 만료·대체로 입력이 바뀌었다면 조합하지 않고 진행을 처음부터 세고,
    // 소모에 성공한 쌍은 진행을 남기지 않는다.
    void AdvanceCombinations(TimeAdvancePlan _plan, List<CombinationPair> _pairs, long _step)
    {
        var progress = new Dictionary<(int First, int Second), long>();
        foreach (var pair in _pairs)
        {
            long elapsed = pair.elapsedTurns + _step;
            if (elapsed >= pair.requiredTurns)
            {
                if (!pair.wagon.TryCombine(pair.source, pair.partner, pair.result, ref _plan.lastInstanceId))
                {
                    elapsed = 0;
                }
            }

            if (elapsed < pair.requiredTurns)
            {
                progress[pair.key] = elapsed;
            }
        }

        _plan.combinationProgress = progress;
    }

    // 배설 진행도가 없는 동물은 이번 시각 이후 처음 맞는 주기 경계(경과 턴이 주기의 배수)에 첫 발생을 맞춘다.
    void EnsureWasteStates(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        long _cycleTurns, long _atTurn)
    {
        long untilBoundary = Time.NextIntervalAt(_atTurn, _animals.wasteIntervalTurns) - _atTurn;
        foreach (var wagon in _wagons)
        {
            foreach (var animal in wagon.Placements)
            {
                if (animal.Definition.Tags.Contains(_animals.animalTag) &&
                    !_plan.wasteProgress.ContainsKey(animal.InstanceId))
                {
                    _plan.wasteProgress.Add(animal.InstanceId, new WasteState(_cycleTurns - untilBoundary, false));
                }
            }
        }
    }

    static long TurnsUntilWaste(PuzzleInventory[] _wagons, Dictionary<int, WasteState> _progress, string _animalTag,
        long _cycleTurns, long _maxRemainingTurns)
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
                long remaining = _cycleTurns - state.ElapsedTurns;
                if (remaining < next && remaining <= _maxRemainingTurns)
                {
                    next = remaining;
                }
            }
        }
        return Math.Max(1, next);
    }

    void AdvanceWaste(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        PuzzleItemDefinition _waste, long _cycleTurns, long _holdTurns, long _step)
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
                        ? new WasteState(_cycleTurns - _holdTurns, false)
                        : state;
                    continue;
                }

                long elapsed = state.ElapsedTurns + _step;
                bool blocked = false;
                if (elapsed >= _cycleTurns)
                {
                    if (wagon.TryAutoPlaceAll(_waste, _animals.wasteCountPerAnimal, ref _plan.lastInstanceId,
                        _markUnread: true))
                    {
                        elapsed %= _cycleTurns;
                    }
                    else
                    {
                        elapsed = _cycleTurns - _holdTurns;
                        blocked = true;
                    }
                }
                progress[animal.InstanceId] = new WasteState(elapsed, blocked);
            }
        }
        _plan.wasteProgress = progress;
    }

    // 곡물 섭취는 소유 시각에서 3시간 주기로 돌아간다. 진행도가 없는 개체는 현재 보유 시간으로 주기를 맞춘다.
    void EnsureFeedingStates(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        long _cycleTurns)
    {
        foreach (var wagon in _wagons)
        {
            foreach (var animal in wagon.Placements)
            {
                if (animal.Definition.Tags.Contains(_animals.animalTag) &&
                    !_plan.feedingProgress.ContainsKey(animal.InstanceId))
                {
                    _plan.feedingProgress.Add(animal.InstanceId, Math.Max(0, animal.OwnedTurns) % _cycleTurns);
                }
            }
        }
    }

    static long TurnsUntilFeeding(PuzzleInventory[] _wagons, Dictionary<int, long> _progress, string _animalTag,
        long _cycleTurns, long _maxRemainingTurns)
    {
        long next = long.MaxValue;
        foreach (var wagon in _wagons)
        {
            foreach (var animal in wagon.Placements)
            {
                if (!animal.Definition.Tags.Contains(_animalTag) ||
                    !_progress.TryGetValue(animal.InstanceId, out long elapsed))
                {
                    continue;
                }

                long remaining = _cycleTurns - elapsed;
                if (remaining < next && remaining <= _maxRemainingTurns)
                {
                    next = remaining;
                }
            }
        }
        return Math.Max(1, next);
    }

    // 경계에 선 동물은 객차 순서, 좌측 하단 좌표, 인스턴스 ID 순으로 같은 객차의 첫 곡물을 하나 소비한다.
    void AdvanceFeeding(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, PuzzleAnimalSynergyData _animals,
        long _step, long _cycleTurns, long _breedingTurns, long _holdTurns, List<PuzzleGrowth> _growth)
    {
        long breedingReductionTurns = (long)_animals.breedingFeedReductionTurns;
        long growthReductionTurns = (long)_animals.growthFeedReductionTurns;
        var candidates = new List<(int wagonIndex, int instanceId, Vector2Int bottomLeft)>();
        var progress = new Dictionary<int, long>();
        for (int i = 0; i < _wagons.Length; i++)
        {
            foreach (var animal in _wagons[i].Placements)
            {
                if (!animal.Definition.Tags.Contains(_animals.animalTag))
                {
                    continue;
                }

                _plan.feedingProgress.TryGetValue(animal.InstanceId, out long elapsed);
                bool fires = elapsed + _step >= _cycleTurns;
                progress[animal.InstanceId] = fires ? 0 : elapsed + _step;
                if (fires)
                {
                    candidates.Add((i, animal.InstanceId, BottomLeft(animal)));
                }
            }
        }

        foreach (var candidate in candidates
            .OrderBy(_candidate => _candidate.wagonIndex)
            .ThenBy(_candidate => _candidate.bottomLeft.y)
            .ThenBy(_candidate => _candidate.bottomLeft.x)
            .ThenBy(_candidate => _candidate.instanceId))
        {
            // 앞선 섭취로 성장·출산한 뒤의 배치와 쌍을 다시 읽는다.
            var wagon = _wagons[candidate.wagonIndex];
            var animal = wagon.Placements.FirstOrDefault(_item => _item.InstanceId == candidate.instanceId);
            var currentPairs = SelectBreedingPairs(_wagons, _plan.breedingProgress);
            if (animal == null || !CanConsumeGrain(animal, currentPairs) ||
                !TryConsumeGrain(wagon, _animals.grainTag))
            {
                continue;
            }

            if (animal.Definition.IsBaby)
            {
                animal.GrowthElapsedTurns = checked(animal.GrowthElapsedTurns + growthReductionTurns);
                _growth.Clear();
                wagon.PlanGrowth(_growth);
                foreach (var change in _growth)
                {
                    wagon.ApplyGrowth(change);
                }
                continue;
            }

            foreach (var pair in currentPairs)
            {
                if (pair.key.First == animal.InstanceId || pair.key.Second == animal.InstanceId)
                {
                    _plan.breedingProgress[pair.key] = CompleteBreedingPair(_plan, pair,
                        checked(pair.state.ElapsedTurns + breedingReductionTurns), _breedingTurns, _holdTurns);
                    break;
                }
            }
        }

        _plan.feedingProgress = progress;
    }

    static bool CanConsumeGrain(PuzzlePlacement _placement, IReadOnlyList<BreedingPair> _pairs)
    {
        if (_placement.OwnedTurns < 0)
        {
            return false;
        }

        if (_placement.Definition.IsBaby)
        {
            return _placement.Definition.GrowthTurns > 0 && !_placement.GrowthSpaceBlocked;
        }

        foreach (var pair in _pairs)
        {
            if (!pair.state.SpaceBlocked &&
                (pair.key.First == _placement.InstanceId || pair.key.Second == _placement.InstanceId))
            {
                return true;
            }
        }
        return false;
    }

    static bool TryConsumeGrain(PuzzleInventory _wagon, string _grainTag)
    {
        foreach (var grain in _wagon.Placements
            .Where(_item => _item.Definition.Tags.Contains(_grainTag))
            .OrderBy(_item => BottomLeft(_item).y)
            .ThenBy(_item => BottomLeft(_item).x)
            .ThenBy(_item => _item.InstanceId))
        {
            if (_wagon.Remove(grain))
            {
                return true;
            }
        }
        return false;
    }

    static Vector2Int BottomLeft(PuzzlePlacement _placement)
    {
        Vector2Int result = _placement.Pose.Cells[0].Position;
        foreach (var cell in _placement.Pose.Cells)
        {
            if (cell.Position.y < result.y || (cell.Position.y == result.y && cell.Position.x < result.x))
            {
                result = cell.Position;
            }
        }
        return result;
    }

    void CommitTimeAdvance(long _turns, TimeAdvancePlan _plan)
    {
        long previous = Time.ElapsedTurns;
        for (int i = 0; i < trainWagons.Count; i++)
        {
            trainWagons[i].CommitTimeAdvance(_plan.wagons[i]);
        }
        breedingProgress = _plan.breedingProgress;
        wasteProgress = _plan.wasteProgress;
        combinationProgress = _plan.combinationProgress;
        feedingProgress = _plan.feedingProgress;
        PruneAnimalStates();
        lastInstanceId = _plan.lastInstanceId;
        for (long opening = 0; opening < _plan.marketRestocks; opening++)
        {
            foreach (var city in tables.Cities)
            {
                markets[city.id] = CreateMarket(city.id, ref lastInstanceId);
            }
        }
        Time.Advance(_turns);
        if (Time.IsOverdue)
        {
            RunStatus = PuzzleRunStatus.Lost;
            HandleRunStatusChanged();
        }
        CheckQuestFailures();
        if (_turns > 0)
        {
            CheckButcheryReady();
            CheckRefiningReady();
            CheckModificationReady();
            GameEvents.RaiseTimeAdvanced(previous, Time.ElapsedTurns, this, _plan.cargoValueBeforeSettlement,
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
        foreach (var id in feedingProgress.Keys.Where(_id => !alive.Contains(_id)).ToArray())
        {
            feedingProgress.Remove(id);
        }
    }

    internal PuzzleTravelResult TravelLeg(string _cityId, bool _passThrough)
    {
        var evaluation = EvaluateTravel(_cityId, out var plan);
        if (!evaluation.IsSuccess)
        {
            RecordAction(PuzzleActionKind.Travel, evaluation.Failure.ToString(), _targetCityId: _cityId);
            return evaluation;
        }

        long duration = evaluation.TravelTurns;
        long previousFunds = Funds;
        Funds -= evaluation.Cost;
        currentCityId = evaluation.ToCityId;
        PaidFacility = StationFacility.None;
        // 떠난 도시의 도축대는 조작 대상이 아니다. 도착 후 패널을 다시 열 때 열어 준다.
        IsButcheryOpen = false;
        IsModificationOpen = false;
        IsRefiningOpen = false;
        bool previewChanged = PreviewCityId != null;
        PreviewCityId = null;
        CityMoveCount++;
        CommitTimeAdvance(duration, plan);
        if (previewChanged)
        {
            GameEvents.RaiseTravelPreviewChanged(this);
        }

        if (_passThrough)
        {
            IsPassingThrough = true;
            RecordAction(PuzzleActionKind.Travel, _targetCityId: _cityId);
            GameEvents.RaiseFundsChanged(this, previousFunds);
        }
        else
        {
            IsPassingThrough = false;
            visitedCities.Add(CurrentCityId);
            RecordAction(PuzzleActionKind.Travel, _targetCityId: _cityId);
            GameEvents.RaiseCityArrived(CurrentCityId, CityMoveCount);
            GameEvents.RaiseFundsChanged(this, previousFunds);
            CompleteArrivedQuests();
        }

        var arrival = new PuzzleTravelResult(PuzzleTravelFailure.None, evaluation.Cost, evaluation.FromCityId,
            evaluation.ToCityId, Funds, evaluation.BaseCost, evaluation.Cells, evaluation.Distance, evaluation.DistanceCost,
            evaluation.TravelTurns, evaluation.ArrivalClockTurns, evaluation.ArrivalIsNight);
        return arrival;
    }

    internal PuzzleTravelResult Travel(string _cityId)
    {
        var arrival = TravelLeg(_cityId, _passThrough: false);
        if (!arrival.IsSuccess)
        {
            return arrival;
        }

        if (RoutePlan != null)
        {
            if (string.Equals(_cityId, RoutePlan.NextCityId, StringComparison.Ordinal))
            {
                RoutePlan.Advance();
                if (string.Equals(_cityId, RoutePlan.DestinationCityId, StringComparison.Ordinal))
                {
                    EndRoutePlan(PuzzleRouteEndReason.Completed);
                }
                else
                {
                    GameEvents.RaiseRoutePlanChanged(new GameEvents.RoutePlanInfo(this));
                }
            }
            else
            {
                EndRoutePlan(PuzzleRouteEndReason.Deviated);
            }
        }

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
        if ((source == market || _destination == market) && !CanTradeInMarket)
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
        return ApplyRegionPenalty(_city, CityTradeRate(_city, _item));
    }

    // 지역 불이익을 빼고 도시·태그 규칙만 합산한 비율. 표 기준을 비교하는 소문이 같은 경로를 쓴다.
    internal PuzzleTradeRate CityTradeRate(PuzzleCityData _city, PuzzleItemDefinition _item)
    {
        var rules = new List<PuzzleCityTagPriceData>();
        foreach (string tag in _item.Tags)
        {
            if (tables.TryGetCityTagPrice(_city.id, tag, out var rule))
            {
                rules.Add(rule);
            }
        }

        return PuzzleTradeRate.Combine(_city, rules);
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
        var modification = ModificationStateOf(_original.Owner);
        if ((IsButchery(_original.Owner) || modification != null || IsRefining(_original.Owner)) &&
            IsTrainWagon(_destination))
        {
            RecordLocalOrigin(_original.InstanceId);
            if (modification != null && modification.Phase == PuzzleModificationPhase.Ready &&
                modification.Board.Placements.Count == 0)
            {
                modification.Phase = PuzzleModificationPhase.Idle;
            }
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
                // 판매한 의뢰 화물은 이 역 시장 보드에 남는 동안 되살 수 있고, 처분(음수 가치)으로 사라졌으면 실패한다.
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
        board.SynergyComboBonusPercent = SynergyComboBonusPercent;
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
    internal long ButcheryRemainingTurns(string _cityId = null)
    {
        var state = EnsureButchery(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        if (state == null || state.Phase != PuzzleButcheryPhase.Working || Time == null)
        {
            return -1;
        }
        return Math.Max(0, state.ReadyAtTurns - Time.ElapsedTurns);
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
        return TryEvaluateButchery(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId, out _, out _, out _);
    }

    // 확인 버튼의 조건과 사유를 한 곳에서 정한다. View가 독자적으로 판단하지 않는다.
    // _failure는 분석 로그용 코드이고 _reason은 화면 문구다.
    bool TryEvaluateButchery(string _cityId, out IReadOnlyList<string> _outputs, out string _reason,
        out string _failure)
    {
        _outputs = null;
        _reason = null;
        _failure = null;
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            _failure = "blocked";
            _reason = BasicData.Text("rules.butchery.blocked");
            return false;
        }

        var state = EnsureButchery(_cityId);
        if (state == null)
        {
            _failure = "unavailable";
            _reason = BasicData.Text("rules.butchery.unavailable");
            return false;
        }
        if (state.Phase == PuzzleButcheryPhase.Working)
        {
            _failure = "in_progress";
            _reason = BasicData.Text("rules.butchery.inProgress");
            return false;
        }
        if (state.Phase == PuzzleButcheryPhase.Ready)
        {
            _failure = "awaiting_pickup";
            _reason = BasicData.Text("rules.butchery.awaitingPickup");
            return false;
        }
        if (state.Board.Placements.Count == 0)
        {
            _failure = "empty";
            _reason = BasicData.Text("rules.butchery.empty");
            return false;
        }

        var outputs = new List<string>();
        foreach (var placement in state.Board.Placements)
        {
            if (placement.Definition.IsBaby)
            {
                _failure = "baby";
                _reason = Presentation.PuzzleBabyCannotButcher;
                return false;
            }
            if (!placement.Definition.Tags.Contains(state.Action.inputTag))
            {
                _failure = "invalid_item";
                _reason = BasicData.Format("rules.butchery.notAnimal", placement.Definition.DisplayName);
                return false;
            }
            if (!TryCollectOutputs(state.Action, placement.Definition.Id, outputs))
            {
                _failure = "no_conversion";
                _reason = BasicData.Format("rules.butchery.noConversion", placement.Definition.DisplayName);
                return false;
            }
        }

        if (!TryFitsOutputs(state.Action, outputs))
        {
            _failure = "no_space";
            _reason = BasicData.Text("rules.butchery.noSpace");
            return false;
        }

        if (Funds < state.Action.cost)
        {
            _failure = "insufficient_funds";
            _reason = Presentation.PuzzleButcheryInsufficientFunds(state.Action.cost);
            return false;
        }

        _outputs = outputs;
        return true;
    }

    internal bool TryConfirmButchery(out string _reason)
    {
        bool confirmed = ConfirmButchery(out _reason, out string failure);
        RecordAction(PuzzleActionKind.WorkbenchConfirm, confirmed ? null : failure, _actionId: ButcheryAction()?.id);
        return confirmed;
    }

    bool ConfirmButchery(out string _reason, out string _failure)
    {
        if (!TryEvaluateButchery(CurrentCityId, out var outputs, out _reason, out _failure))
        {
            return false;
        }
        if (!IsButcheryOpen)
        {
            _failure = "blocked";
            _reason = BasicData.Text("rules.butchery.blocked");
            return false;
        }

        var state = EnsureButchery(CurrentCityId);
        if (state == null)
        {
            _failure = "unavailable";
            _reason = BasicData.Text("rules.butchery.unavailable");
            return false;
        }

        // 확인 직전에 비용·잔액과 완료 시각을 다시 계산한다. 여기까지 실패하면 보드·상태·자금을 하나도 바꾸지 않는다.
        long cost = state.Action.cost;
        if (Funds < cost)
        {
            _failure = "insufficient_funds";
            _reason = Presentation.PuzzleButcheryInsufficientFunds(cost);
            return false;
        }

        long readyAtTurns;
        try
        {
            readyAtTurns = checked(Time.ElapsedTurns + state.Action.workTurns);
        }
        catch (OverflowException)
        {
            _failure = "time_overflow";
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
        state.ReadyAtTurns = readyAtTurns;
        state.Phase = PuzzleButcheryPhase.Working;
        NotifyButchery(state);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
    }

    // 입력 화물 하나의 산출물을 목록에 더한다. 변환 규칙이 없으면 false.
    static bool TryCollectOutputs(PuzzleCityActionData _action, string _pieceId, List<string> _outputs)
    {
        foreach (var conversion in _action.conversions ?? Array.Empty<PuzzleActionConversionData>())
        {
            if (conversion == null || conversion.outputCount <= 0 || string.IsNullOrEmpty(conversion.outputPieceId) ||
                !string.Equals(conversion.inputPieceId, _pieceId, StringComparison.Ordinal))
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
        probe.SynergyComboBonusPercent = SynergyComboBonusPercent;
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
            Time.ElapsedTurns < _state.ReadyAtTurns)
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
            _state.ReadyAtTurns = -1;
            changed = becameReady = true;
        }
        if (changed)
        {
            NotifyButchery(_state);
        }
        if (becameReady)
        {
            GameEvents.RaiseWorkbenchReady(this, _state.Action.id, _state.CityId, _state.OutputCount);
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
            return RefreshModificationReady(existing);
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

    internal PuzzleModificationPhase ModificationPhase(string _cityId = null)
    {
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return state?.Phase ?? PuzzleModificationPhase.Idle;
    }

    // 작업 중에는 남은 턴, 그 외에는 -1을 반환한다.
    internal long ModificationRemainingTurns(string _cityId = null)
    {
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        if (state == null || state.Phase != PuzzleModificationPhase.Working || Time == null)
        {
            return -1;
        }

        return Math.Max(0, state.ReadyAtTurns - Time.ElapsedTurns);
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
            state.Phase != PuzzleModificationPhase.Idle ||
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
        if (state == null || RunStatus != PuzzleRunStatus.InProgress ||
            state.Phase == PuzzleModificationPhase.Working)
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
        return state == null || state.Phase != PuzzleModificationPhase.Idle || state.Board.Placements.Count == 0;
    }

    internal bool ModificationConfirmable(string _cityId = null)
    {
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        return TryPlanModificationSwaps(state, out _, out _, out _);
    }

    internal bool TryConfirmModification(out int _convertedCount, out PuzzleModificationFailure _failure,
        string _cityId = null)
    {
        var state = EnsureModification(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        // 확정이 모드 선택을 비우므로 기록할 모드를 먼저 잡는다.
        string modeId = state?.SelectedModeId;
        bool confirmed = ConfirmModification(state, out _convertedCount, out _failure);
        RecordAction(PuzzleActionKind.WorkbenchConfirm, confirmed ? null : ModificationFailureCode(_failure),
            _actionId: state?.Action.id, _modeId: modeId);
        return confirmed;
    }

    static string ModificationFailureCode(PuzzleModificationFailure _failure)
    {
        switch (_failure)
        {
            case PuzzleModificationFailure.None:
                return null;
            case PuzzleModificationFailure.Blocked:
                return "blocked";
            case PuzzleModificationFailure.Unavailable:
                return "unavailable";
            case PuzzleModificationFailure.NoMode:
                return "no_mode";
            case PuzzleModificationFailure.Empty:
                return "empty";
            case PuzzleModificationFailure.InvalidItem:
                return "invalid_item";
            case PuzzleModificationFailure.NoConversion:
                return "no_conversion";
            case PuzzleModificationFailure.AlreadyModified:
                return "already_modified";
            case PuzzleModificationFailure.InvalidResult:
                return "invalid_result";
            case PuzzleModificationFailure.InsufficientFunds:
                return "insufficient_funds";
            default:
                return _failure.ToString();
        }
    }

    bool ConfirmModification(PuzzleModificationState _state, out int _convertedCount,
        out PuzzleModificationFailure _failure)
    {
        _convertedCount = 0;
        var state = _state;
        if (!TryPlanModificationSwaps(state, out var swaps, out long cost, out _failure))
        {
            return false;
        }

        long readyAtTurns;
        try
        {
            readyAtTurns = checked(Time.ElapsedTurns + state.Action.workTurns);
        }
        catch (OverflowException)
        {
            _failure = PuzzleModificationFailure.Blocked;
            return false;
        }

        // 입력은 작업대에 잠그고 확정된 정의 교체를 완료 때 적용한다.
        state.PendingSwaps = swaps;
        state.ReadyAtTurns = readyAtTurns;
        state.Phase = PuzzleModificationPhase.Working;
        state.SelectedModeId = null;
        long previousFunds = Funds;
        Funds -= cost;
        _convertedCount = swaps.Count;
        NotifyModification(state);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
    }

    void CheckModificationReady()
    {
        foreach (var state in modifications.Values.ToArray())
        {
            RefreshModificationReady(state);
        }
    }

    PuzzleModificationState RefreshModificationReady(PuzzleModificationState _state)
    {
        if (_state.Phase != PuzzleModificationPhase.Working || Time == null ||
            Time.ElapsedTurns < _state.ReadyAtTurns ||
            !_state.Board.TrySwapDefinitions(_state.PendingSwaps, out _))
        {
            return _state;
        }

        int outputCount = _state.PendingSwaps.Count;
        _state.PendingSwaps = null;
        _state.ReadyAtTurns = -1;
        _state.Phase = PuzzleModificationPhase.Ready;
        NotifyModification(_state);
        GameEvents.RaiseWorkbenchReady(this, _state.Action.id, _state.CityId, outputCount);
        return _state;
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
        if (state == null || !string.Equals(state.CityId, CurrentCityId, StringComparison.Ordinal))
        {
            return false;
        }

        bool fromBoard = ReferenceEquals(state.Board, _source);
        bool toBoard = ReferenceEquals(state.Board, _destination);
        switch (state.Phase)
        {
            case PuzzleModificationPhase.Idle:
                return fromBoard && toBoard || (toBoard ? IsTrainWagon(_source) : IsTrainWagon(_destination));
            case PuzzleModificationPhase.Ready:
                return fromBoard && (toBoard || IsTrainWagon(_destination));
            default:
                return false;
        }
    }

    bool TryPlanModificationSwaps(PuzzleModificationState _state, out List<PuzzleDefinitionSwap> _swaps, out long _cost,
        out PuzzleModificationFailure _failure)
    {
        _swaps = null;
        _cost = 0;
        _failure = PuzzleModificationFailure.None;
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            _failure = PuzzleModificationFailure.Blocked;
            return false;
        }

        if (_state == null)
        {
            _failure = PuzzleModificationFailure.Unavailable;
            return false;
        }
        if (!IsModificationOpen || _state.Phase != PuzzleModificationPhase.Idle ||
            !string.Equals(_state.CityId, CurrentCityId, StringComparison.Ordinal))
        {
            _failure = PuzzleModificationFailure.Blocked;
            return false;
        }
        if (!TryFindSelectedMode(_state, out var mode) || string.IsNullOrEmpty(mode.inputTag) ||
            string.IsNullOrEmpty(mode.outputTag) || string.Equals(mode.inputTag, mode.outputTag, StringComparison.Ordinal))
        {
            _failure = PuzzleModificationFailure.NoMode;
            return false;
        }
        if (_state.Board.Placements.Count == 0)
        {
            _failure = PuzzleModificationFailure.Empty;
            return false;
        }

        var swaps = new List<PuzzleDefinitionSwap>(_state.Board.Placements.Count);
        foreach (var placement in _state.Board.Placements)
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

        if (!_state.Board.TryPlanDefinitionSwaps(swaps))
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
        _cost = mode.cost;
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
        board.SynergyComboBonusPercent = SynergyComboBonusPercent;
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
    internal long RefiningRemainingTurns(string _cityId = null)
    {
        var state = EnsureRefining(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId);
        if (state == null || state.Phase != PuzzleRefiningPhase.Working || Time == null)
        {
            return -1;
        }
        return Math.Max(0, state.ReadyAtTurns - Time.ElapsedTurns);
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
        return TryEvaluateRefining(string.IsNullOrEmpty(_cityId) ? CurrentCityId : _cityId, out _, out _);
    }

    // 확인 버튼의 조건과 사유를 한 곳에서 정한다. 반복 조회해도 상태·돈은 바뀌지 않는다.
    // 고정 산출물이 빈 작업 보드에 모두 들어가는지 검사한다. _failure는 분석 로그용 코드이고 _reason은 화면 문구다.
    bool TryEvaluateRefining(string _cityId, out string _reason, out string _failure)
    {
        _reason = null;
        _failure = null;
        if (RunStatus != PuzzleRunStatus.InProgress || Time == null)
        {
            _failure = "blocked";
            _reason = BasicData.Text("rules.refining.blocked");
            return false;
        }

        var state = EnsureRefining(_cityId);
        if (state == null)
        {
            _failure = "unavailable";
            _reason = BasicData.Text("rules.refining.unavailable");
            return false;
        }
        if (state.Phase == PuzzleRefiningPhase.Working)
        {
            _failure = "in_progress";
            _reason = BasicData.Text("rules.refining.inProgress");
            return false;
        }
        if (state.Phase == PuzzleRefiningPhase.Ready)
        {
            _failure = "awaiting_pickup";
            _reason = BasicData.Text("rules.refining.awaitingPickup");
            return false;
        }
        if (state.Board.Placements.Count == 0)
        {
            _failure = "empty";
            _reason = BasicData.Text("rules.refining.empty");
            return false;
        }

        var outputs = new List<string>();
        foreach (var placement in state.Board.Placements)
        {
            // 입력 허용은 변환표에 등록한 품목 ID가 기준이다. Ore 태그를 갖지만 입력으로 등록되지 않은
            // 금속 주괴는 noConversion으로, 태그 자체가 없는 화물은 notOre로 막는다.
            if (!TryCollectOutputs(state.Action, placement.Definition.Id, outputs))
            {
                bool tagged = placement.Definition.Tags.Contains(state.Action.inputTag);
                _failure = tagged ? "no_conversion" : "invalid_item";
                _reason = tagged
                    ? BasicData.Format("rules.refining.noConversion", placement.Definition.DisplayName)
                    : BasicData.Format("rules.refining.notOre", placement.Definition.DisplayName);
                return false;
            }
        }

        // 모든 입력의 고정 산출 합이 빈 보드에 모두 들어갈 때만 확인을 허용한다.
        if (!TryFitsOutputs(state.Action, outputs))
        {
            _failure = "no_space";
            _reason = BasicData.Text("rules.refining.noSpace");
            return false;
        }

        if (Funds < state.Action.cost)
        {
            _failure = "insufficient_funds";
            _reason = BasicData.Format("rules.refining.insufficientFunds", state.Action.cost);
            return false;
        }

        return true;
    }

    internal bool TryConfirmRefining(out string _reason)
    {
        bool confirmed = ConfirmRefining(out _reason, out string failure);
        RecordAction(PuzzleActionKind.WorkbenchConfirm, confirmed ? null : failure, _actionId: RefiningAction()?.id);
        return confirmed;
    }

    bool ConfirmRefining(out string _reason, out string _failure)
    {
        if (!TryEvaluateRefining(CurrentCityId, out _reason, out _failure))
        {
            return false;
        }
        if (!IsRefiningOpen)
        {
            _failure = "blocked";
            _reason = BasicData.Text("rules.refining.blocked");
            return false;
        }

        var state = EnsureRefining(CurrentCityId);
        if (state == null)
        {
            _failure = "unavailable";
            _reason = BasicData.Text("rules.refining.unavailable");
            return false;
        }

        // 확인 직전에 비용·잔액과 완료 시각을 다시 계산한다. 여기까지 실패하면 보드·상태·자금을 하나도 바꾸지 않는다.
        long cost = state.Action.cost;
        if (Funds < cost)
        {
            _failure = "insufficient_funds";
            _reason = BasicData.Format("rules.refining.insufficientFunds", cost);
            return false;
        }

        long readyAtTurns;
        try
        {
            readyAtTurns = checked(Time.ElapsedTurns + state.Action.workTurns);
        }
        catch (OverflowException)
        {
            _failure = "time_overflow";
            _reason = BasicData.Text("rules.refining.timeOverflow");
            return false;
        }

        // 인스턴스 ID순으로 테이블의 고정 산출물을 예약한다.
        var inputs = state.Board.Placements.OrderBy(_placement => _placement.InstanceId).ToArray();
        var outputs = new List<string>();
        foreach (var placement in inputs)
        {
            if (!TryCollectOutputs(state.Action, placement.Definition.Id, outputs))
            {
                _failure = "no_conversion";
                _reason = BasicData.Format("rules.refining.noConversion", placement.Definition.DisplayName);
                return false;
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
        state.ReadyAtTurns = readyAtTurns;
        state.Phase = PuzzleRefiningPhase.Working;
        NotifyRefining(state);
        GameEvents.RaiseFundsChanged(this, previousFunds);
        return true;
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
            Time.ElapsedTurns < _state.ReadyAtTurns)
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

        bool becameReady = false;
        if (_state.PendingOutputs.Count == 0)
        {
            _state.Phase = PuzzleRefiningPhase.Ready;
            _state.ReadyAtTurns = -1;
            changed = becameReady = true;
        }
        if (changed)
        {
            NotifyRefining(_state);
        }
        if (becameReady)
        {
            GameEvents.RaiseWorkbenchReady(this, _state.Action.id, _state.CityId, _state.OutputCount);
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
    internal long TravelTurns { get; }
    internal int ArrivalClockTurns { get; }
    internal bool ArrivalIsNight { get; }
    internal long DistanceCost { get; }
    internal int Cells { get; }
    internal long FundsAfter { get; }
    internal string FromCityId { get; }
    internal string ToCityId { get; }
    internal bool IsSuccess => Failure == PuzzleTravelFailure.None;

    internal PuzzleTravelResult(PuzzleTravelFailure _failure, long _cost = 0, string _fromCityId = null,
        string _toCityId = null, long _fundsAfter = 0, long _baseCost = 0, int _cells = 0,
        int _distance = 0, long _distanceCost = 0, long _travelTurns = 0, int _arrivalClockTurns = 0, bool _arrivalIsNight = false)
    {
        Failure = _failure;
        Cost = _cost;
        FromCityId = _fromCityId;
        ToCityId = _toCityId;
        FundsAfter = _fundsAfter;
        BaseCost = _baseCost;
        Distance = _distance;
        TravelTurns = _travelTurns;
        ArrivalClockTurns = _arrivalClockTurns;
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

// 역 시설. 진입 턴을 쓰는 곳은 시장뿐이고, 술집·화차 시설은 진입이 무료다(화차 구매 등 개별 행동이 턴을 쓴다).
internal enum StationFacility
{
    None,
    Market,
    Tavern,
    WagonFacility
}
