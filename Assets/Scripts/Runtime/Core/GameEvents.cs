using System;
using UnityEngine.SceneManagement;
using UnityEngine;

// 게임 도메인 이벤트의 단일 발행 지점.
// 규칙:
//   1) 이벤트는 이 파일에 struct 페이로드와 함께 추가한다. 개별 컴포넌트가 서로를 직접 참조해 알리지 않는다.
//   2) 생산자는 Raise*만 호출한다. 구독자 예외가 게임 흐름을 멈추지 않도록 여기서 삼킨다.
//   3) 구독은 OnEnable/OnDisable에서 대칭으로 한다(OnDisable에서 해제하지 않으면 정적 이벤트에 갇힌다).
//   4) 페이로드는 struct로 불변 data only로 두고, 시간·좌표처럼 분석에 쓰는 값은 여기서 함께 보낸다.
public static class GameEvents
{
    public readonly struct PuzzleRunInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string reason;

        public PuzzleRunInfo(PuzzleInventoryService _session, string _reason)
        {
            session = _session;
            reason = _reason;
        }
    }

    // 확정 조작만 발행한다. Evaluate 계열이나 호버에서는 발행하지 않는다.
    public readonly struct PuzzleActionInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly PuzzleActionKind action;
        public readonly string failure, targetCityId, destination;
        public readonly int instanceId, clockwiseTurns;
        public readonly long requestedMinutes;
        public readonly Vector2Int targetCell, grabbedCell;

        public PuzzleActionInfo(PuzzleInventoryService _session, PuzzleActionKind _action, string _failure = null,
            string _targetCityId = null, int _instanceId = 0,
            string _destination = null, Vector2Int _targetCell = default, Vector2Int _grabbedCell = default,
            int _clockwiseTurns = 0, long _requestedMinutes = 0)
        {
            session = _session;
            action = _action;
            failure = _failure;
            targetCityId = _targetCityId;
            instanceId = _instanceId;
            destination = _destination;
            targetCell = _targetCell;
            grabbedCell = _grabbedCell;
            clockwiseTurns = _clockwiseTurns;
            requestedMinutes = _requestedMinutes;
        }
    }

    public static event Action<PuzzleRunInfo> PuzzleRunStarted;
    public static event Action<PuzzleRunInfo> PuzzleRunEnded;
    public static event Action<PuzzleActionInfo> PuzzleActionCommitted;

    public static void RaisePuzzleRunStarted(PuzzleInventoryService _session)
    {
        Emit(PuzzleRunStarted, new PuzzleRunInfo(_session, null));
    }

    public static void RaisePuzzleRunEnded(PuzzleInventoryService _session, string _reason)
    {
        Emit(PuzzleRunEnded, new PuzzleRunInfo(_session, _reason));
    }

    public static void RaisePuzzleActionCommitted(PuzzleActionInfo _info)
    {
        Emit(PuzzleActionCommitted, _info);
    }

    /// <summary>게임 시간 진행 결과. 누적 값과 차이는 세션 시작 기준 분 단위다.</summary>
    public readonly struct TimeAdvanceInfo
    {
        /// <summary>시간을 진행한 세션. 다른 씬의 이벤트를 구분하는 데 사용한다.</summary>
        public readonly PuzzleInventoryService session;
        /// <summary>진행 전 누적 경과 시간.</summary>
        public readonly long previousMinutes;
        /// <summary>진행 후 누적 경과 시간.</summary>
        public readonly long elapsedMinutes;
        internal readonly decimal? cargoValueBeforeSettlement;
        internal readonly decimal? cargoValueBeforeFreshnessLoss;
        /// <summary>이번 행동으로 경과한 시간. 신선도·내구도 갱신에 사용한다.</summary>
        public long DeltaMinutes => elapsedMinutes - previousMinutes;
        public decimal DeltaHours => DeltaMinutes / (decimal)PuzzleTimeManager.MINUTES_PER_HOUR;


        /// <summary>시간 변화 전후의 누적 시간을 보관한다.</summary>
        public TimeAdvanceInfo(long _previousMinutes, long _elapsedMinutes, PuzzleInventoryService _session = null,
            decimal? _cargoValueBeforeSettlement = null, decimal? _cargoValueBeforeFreshnessLoss = null)
        {
            session = _session;
            previousMinutes = _previousMinutes;
            elapsedMinutes = _elapsedMinutes;
            cargoValueBeforeSettlement = _cargoValueBeforeSettlement;
            cargoValueBeforeFreshnessLoss = _cargoValueBeforeFreshnessLoss;
        }
    }

    /// <summary>세션 시간·기한 판정 확정 후 발행된다. OnEnable/OnDisable에서 대칭으로 구독·해제한다.</summary>
    public static event Action<TimeAdvanceInfo> TimeAdvanced;
    public readonly struct TravelPreviewInfo
    {
        public readonly PuzzleInventoryService session;
        public TravelPreviewInfo(PuzzleInventoryService _session)
        {
            session = _session;
        }
    }
    public static event Action<TravelPreviewInfo> TravelPreviewChanged;
    public readonly struct TavernRumorInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string sourceCityId, targetCityId, message, tag;
        public readonly long recordedMinutes;

        public TavernRumorInfo(PuzzleInventoryService _session, string _sourceCityId, string _targetCityId,
            string _message, long _recordedMinutes, string _tag)
        {
            session = _session;
            sourceCityId = _sourceCityId;
            targetCityId = _targetCityId;
            message = _message;
            recordedMinutes = _recordedMinutes;
            tag = _tag;
        }
    }
    public static event Action<TavernRumorInfo> TavernRumorRecorded;

    // 의뢰 하나의 상태가 바뀔 때(수락·완료·실패) 발행한다. 값은 변화 직후의 의뢰 상태다.
    public readonly struct QuestInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly int questId;
        public readonly string templateId, pieceId, sourceCityId, targetCityId, penaltyId, state, failure;
        public readonly long reward, deposit, payout, deadlineMinutes, elapsedMinutes;
        public readonly int cargoInstanceId;

        internal QuestInfo(PuzzleInventoryService _session, TrainventoryQuest _quest, long _elapsedMinutes)
        {
            session = _session;
            questId = _quest.Id;
            templateId = _quest.TemplateId;
            pieceId = _quest.PieceId;
            sourceCityId = _quest.SourceCityId;
            targetCityId = _quest.TargetCityId;
            penaltyId = _quest.PenaltyId;
            state = _quest.State.ToString();
            failure = _quest.Failure == TrainventoryQuestFailure.None ? null : _quest.Failure.ToString();
            reward = _quest.Reward;
            deposit = _quest.Deposit;
            payout = _quest.Payout;
            deadlineMinutes = _quest.DeadlineMinutes;
            elapsedMinutes = _elapsedMinutes;
            cargoInstanceId = _quest.CargoInstanceId;
        }
    }
    public static event Action<QuestInfo> QuestChanged;

    internal static void RaiseQuestChanged(PuzzleInventoryService _session, TrainventoryQuest _quest)
    {
        Emit(QuestChanged, new QuestInfo(_session, _quest, _session.ElapsedTime));
    }

    // 태그 하나의 숙련 경험치가 늘었다. 판매(source=Sale)는 거래 직후, 의뢰(Quest)는 완료 처리 중에 발행한다.
    public readonly struct ProficiencyInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string tag, source;
        public readonly long gainedXp, xp, elapsedMinutes;
        public readonly int previousLevel, level, instanceId, questId;

        internal ProficiencyInfo(PuzzleInventoryService _session, string _tag, string _source, long _gainedXp, long _xp,
            int _previousLevel, int _level, int _instanceId, int _questId, long _elapsedMinutes)
        {
            session = _session;
            tag = _tag;
            source = _source;
            gainedXp = _gainedXp;
            xp = _xp;
            previousLevel = _previousLevel;
            level = _level;
            instanceId = _instanceId;
            questId = _questId;
            elapsedMinutes = _elapsedMinutes;
        }

        public bool LeveledUp => level > previousLevel;
    }
    public static event Action<ProficiencyInfo> ProficiencyChanged;

    internal static void RaiseProficiencyChanged(ProficiencyInfo _info)
    {
        Emit(ProficiencyChanged, _info);
    }

    public static void RaiseTavernRumorRecorded(PuzzleInventoryService _session, string _sourceCityId,
        string _targetCityId, string _message, long _recordedMinutes, string _tag)
    {
        Emit(TavernRumorRecorded, new TavernRumorInfo(_session, _sourceCityId, _targetCityId, _message, _recordedMinutes, _tag));
    }

    public readonly struct MapRequestInfo
    {
        public readonly int sceneHandle;
        public MapRequestInfo(int _sceneHandle)
        {
            sceneHandle = _sceneHandle;
        }
    }
    public static event Action<MapRequestInfo> MapRequested;
    public static void RaiseMapRequested(int _sceneHandle)
    {
        Emit(MapRequested, new MapRequestInfo(_sceneHandle));
    }

    // 고정 UI의 지도 토글 버튼. 같은 씬의 PuzzleMapPresenter가 열려 있으면 닫고, 닫혀 있으면 연다.
    public readonly struct MapToggleInfo
    {
        public readonly int sceneHandle;
        public MapToggleInfo(int _sceneHandle)
        {
            sceneHandle = _sceneHandle;
        }
    }
    public static event Action<MapToggleInfo> MapToggleRequested;
    public static void RaiseMapToggleRequested(int _sceneHandle)
    {
        Emit(MapToggleRequested, new MapToggleInfo(_sceneHandle));
    }

    // 고정 UI의 상성표 보기 버튼. 같은 씬의 상성표 패널이 토글 응답한다.
    public readonly struct SynergyChartToggleInfo
    {
        public readonly int sceneHandle;
        public SynergyChartToggleInfo(int _sceneHandle)
        {
            sceneHandle = _sceneHandle;
        }
    }
    public static event Action<SynergyChartToggleInfo> SynergyChartToggleRequested;
    public static void RaiseSynergyChartToggleRequested(int _sceneHandle)
    {
        Emit(SynergyChartToggleRequested, new SynergyChartToggleInfo(_sceneHandle));
    }
    public static event Action<PuzzleNotebookTab> NotebookOpenRequested;
    public static void RaiseNotebookOpenRequested(PuzzleNotebookTab _tab)
    {
        Emit(NotebookOpenRequested, _tab);
    }
    public static void RaiseTravelPreviewChanged(PuzzleInventoryService _session)
    {
        Emit(TravelPreviewChanged, new TravelPreviewInfo(_session));
    }

    /// <summary>확정된 게임 시간 변화를 알린다. 시간을 직접 진행하지 않으며 구독자 예외는 격리한다.</summary>
    /// <param name="_previousMinutes">진행 전 누적 경과 시간.</param>
    /// <param name="_elapsedMinutes">진행 후 누적 경과 시간.</param>
    /// <param name="_session">시간을 확정한 세션. 소비자는 자신이 속한 세션인지 확인한다.</param>
    public static void RaiseTimeAdvanced(long _previousMinutes, long _elapsedMinutes, PuzzleInventoryService _session = null,
        decimal? _cargoValueBeforeSettlement = null, decimal? _cargoValueBeforeFreshnessLoss = null)
    {
        Emit(TimeAdvanced, new TimeAdvanceInfo(_previousMinutes, _elapsedMinutes, _session, _cargoValueBeforeSettlement,
            _cargoValueBeforeFreshnessLoss));
    }

    public readonly struct CityArrivalInfo
    {
        public readonly string cityId;
        public readonly int moveCount;
        public readonly double time;

        public CityArrivalInfo(string _cityId, int _moveCount, double _time)
        {
            cityId = _cityId;
            moveCount = _moveCount;
            time = _time;
        }
    }

    public static event Action<CityArrivalInfo> CityArrived;

    public static void RaiseCityArrived(string _cityId, int _moveCount)
    {
        Emit(CityArrived, new CityArrivalInfo(_cityId, _moveCount, TelemetryLog.GameTime));
    }

    // ── 페이로드 ──
    public readonly struct SceneInfo
    {
        public readonly string scene;
        public readonly bool loaded;
        public SceneInfo(string _scene, bool _loaded)
        {
            this.scene = _scene;
            this.loaded = _loaded;
        }
    }
    public readonly struct UpgradeInfo
    {
        public readonly string id, displayName;
        public UpgradeInfo(string _id, string _displayName)
        {
            this.id = _id;
            this.displayName = _displayName;
        }
    }
    public readonly struct PanelInfo
    {
        public readonly string panel;
        public readonly bool open;
        public PanelInfo(string _panel, bool _open)
        {
            this.panel = _panel;
            this.open = _open;
        }
    }
    public readonly struct TradeInfo
    {
        public readonly string cityId, itemId;
        public readonly int instanceId, clockwiseTurns;
        public readonly PuzzleTradeDirection direction;
        public readonly long price, fundsAfter;
        public readonly Vector2Int destination;

        public TradeInfo(string _cityId, string _itemId, int _instanceId, PuzzleTradeDirection _direction,
            long _price, long _fundsAfter, Vector2Int _destination, int _clockwiseTurns)
        {
            cityId = _cityId;
            itemId = _itemId;
            instanceId = _instanceId;
            direction = _direction;
            price = _price;
            fundsAfter = _fundsAfter;
            destination = _destination;
            clockwiseTurns = _clockwiseTurns;
        }
    }

    public readonly struct FundsInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly long funds;

        public FundsInfo(PuzzleInventoryService _session, long _funds)
        {
            session = _session;
            funds = _funds;
        }
    }

    /// <summary>마을 도축대의 상태·화물 변화. 확인·완료·회수 직후 발행된다.</summary>
    public readonly struct ButcheryInfo
    {
        /// <summary>도축대를 소유한 세션. 다른 씬의 이벤트를 구분하는 데 사용한다.</summary>
        public readonly PuzzleInventoryService session;
        /// <summary>도축대가 있는 도시.</summary>
        public readonly string cityId;
        /// <summary>변화 후 상태.</summary>
        public readonly PuzzleButcheryPhase phase;
        /// <summary>변화 후 보드 조작을 받는 여부.</summary>
        public readonly bool open;

        public ButcheryInfo(PuzzleInventoryService _session, string _cityId, PuzzleButcheryPhase _phase, bool _open)
        {
            session = _session;
            cityId = _cityId;
            phase = _phase;
            open = _open;
        }
    }

    /// <summary>마을 개조대의 상태·화물 변화. 모드 선택·확정 직후 발행된다.</summary>
    public readonly struct ModificationInfo
    {
        /// <summary>개조대를 소유한 세션. 다른 씬의 이벤트를 구분하는 데 사용한다.</summary>
        public readonly PuzzleInventoryService session;
        /// <summary>개조대가 있는 도시.</summary>
        public readonly string cityId;
        /// <summary>변화 후 개조 모드가 선택된 상태인지 여부.</summary>
        public readonly bool modeSelected;
        /// <summary>변화 후 보드 조작을 받는 여부.</summary>
        public readonly bool open;

        public ModificationInfo(PuzzleInventoryService _session, string _cityId, bool _modeSelected, bool _open)
        {
            session = _session;
            cityId = _cityId;
            modeSelected = _modeSelected;
            open = _open;
        }
    }

    /// <summary>마을 정제대의 상태·화물 변화. 확인·완료·회수 직후 발행된다.</summary>
    public readonly struct RefiningInfo
    {
        /// <summary>정제대를 소유한 세션. 다른 씬의 이벤트를 구분하는 데 사용한다.</summary>
        public readonly PuzzleInventoryService session;
        /// <summary>정제대가 있는 도시.</summary>
        public readonly string cityId;
        /// <summary>변화 후 상태.</summary>
        public readonly PuzzleRefiningPhase phase;
        /// <summary>변화 후 보드 조작을 받는 여부.</summary>
        public readonly bool open;

        public RefiningInfo(PuzzleInventoryService _session, string _cityId, PuzzleRefiningPhase _phase, bool _open)
        {
            session = _session;
            cityId = _cityId;
            phase = _phase;
            open = _open;
        }
    }

    /// <summary>도축 확정 사건. 입력 화물을 소진하고 작업이 시작된 시점의 스냅샷이다.</summary>
    public readonly struct ButcheryCommittedInfo
    {
        public readonly string actionId, cityId;
        public readonly int inputCount;
        public readonly long readyAtMinutes;
        public readonly double time;

        public ButcheryCommittedInfo(string _actionId, string _cityId, int _inputCount, long _readyAtMinutes, double _time)
        {
            actionId = _actionId;
            cityId = _cityId;
            inputCount = _inputCount;
            readyAtMinutes = _readyAtMinutes;
            time = _time;
        }
    }

    /// <summary>도축 완료 사건. 산출물이 보드에 놓여 회수 대기 상태가 된 시점의 스냅샷이다.</summary>
    public readonly struct ButcheryReadyInfo
    {
        public readonly string actionId, cityId;
        public readonly int outputCount;
        public readonly double time;

        public ButcheryReadyInfo(string _actionId, string _cityId, int _outputCount, double _time)
        {
            actionId = _actionId;
            cityId = _cityId;
            outputCount = _outputCount;
            time = _time;
        }
    }

    // ── 이벤트 ──
    public static event Action<SceneInfo> SceneChanged;
    public static event Action<UpgradeInfo> UpgradeGranted;
    public static event Action<PanelInfo> PanelVisibility;
    public static event Action<TradeInfo> TradeCompleted;
    public static event Action<FundsInfo> FundsChanged;
    public static event Action<ButcheryInfo> ButcheryChanged;
    public static event Action<ModificationInfo> ModificationChanged;
    public static event Action<RefiningInfo> RefiningChanged;
    public static event Action<ButcheryCommittedInfo> ButcheryCommitted;
    public static event Action<ButcheryReadyInfo> ButcheryReady;

    // ── 발행 ──
    public static void RaiseUpgradeGranted(UpgradeData _upgrade)
    {
        if (_upgrade == null)
        {
            return;
        }
        Emit(UpgradeGranted, new UpgradeInfo(_upgrade.id, string.IsNullOrEmpty(_upgrade.displayName) ? _upgrade.id : _upgrade.displayName));
    }
    public static void RaisePanelVisibility(string _panelName, bool _open)
    {
        Emit(PanelVisibility, new PanelInfo(_panelName, _open));
    }
    public static void RaiseTradeCompleted(TradeInfo _info)
    {
        Emit(TradeCompleted, _info);
    }
    public static void RaiseFundsChanged(PuzzleInventoryService _session, long _previousFunds)
    {
        if (_session.Funds != _previousFunds)
        {
            Emit(FundsChanged, new FundsInfo(_session, _session.Funds));
        }
    }
    public static void RaiseButcheryChanged(PuzzleInventoryService _session, string _cityId,
        PuzzleButcheryPhase _phase, bool _open)
    {
        Emit(ButcheryChanged, new ButcheryInfo(_session, _cityId, _phase, _open));
    }
    public static void RaiseModificationChanged(PuzzleInventoryService _session, string _cityId,
        bool _modeSelected, bool _open)
    {
        Emit(ModificationChanged, new ModificationInfo(_session, _cityId, _modeSelected, _open));
    }
    public static void RaiseRefiningChanged(PuzzleInventoryService _session, string _cityId,
        PuzzleRefiningPhase _phase, bool _open)
    {
        Emit(RefiningChanged, new RefiningInfo(_session, _cityId, _phase, _open));
    }
    public static void RaiseButcheryCommitted(string _actionId, string _cityId, int _inputCount, long _readyAtMinutes)
    {
        Emit(ButcheryCommitted, new ButcheryCommittedInfo(_actionId, _cityId, _inputCount, _readyAtMinutes, TelemetryLog.GameTime));
    }
    public static void RaiseButcheryReady(string _actionId, string _cityId, int _outputCount)
    {
        Emit(ButcheryReady, new ButcheryReadyInfo(_actionId, _cityId, _outputCount, TelemetryLog.GameTime));
    }

    // 구독자 하나가 예외를 던져도 나머지 구독자와 게임 진행은 멈추지 않는다.
    static void Emit<T>(Action<T> _handlers, T _payload)
    {
        if (_handlers == null)
        {
            return;
        }
        foreach (var handler in _handlers.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler).Invoke(_payload);
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogError($"이벤트 구독자 예외({handler.Method.Name}): {error.Message}");
            }
        }
    }

    // ── 진단용 순번 ──
    // 로그에서 같은 공격·판정을 대조하기 위한 증가 번호. 배열 순서나 Object instance ID에 의존하지 않는다.
    static int sequence;
    public static int NextId()
    {
        return ++sequence;
    }

    // ── 씬 진입/진행을 자동으로 흘린다(별도 설치 불필요) ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        PuzzleRunStarted = null;
        PuzzleRunEnded = null;
        PuzzleActionCommitted = null;
        SceneChanged = null;
        UpgradeGranted = null;
        PanelVisibility = null;
        TradeCompleted = null;
        FundsChanged = null;
        CityArrived = null;
        TimeAdvanced = null;
        TravelPreviewChanged = null;
        MapRequested = null;
        MapToggleRequested = null;
        SynergyChartToggleRequested = null;
        NotebookOpenRequested = null;
        ButcheryChanged = null;
        ModificationChanged = null;
        RefiningChanged = null;
        ButcheryCommitted = null;
        ButcheryReady = null;
        TavernRumorRecorded = null;
        QuestChanged = null;
        ProficiencyChanged = null;
        sequence = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        hooked = false;
    }

    static bool hooked;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallSceneHook()
    {
        if (hooked)
        {
            return;
        }
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        Emit(SceneChanged, new SceneInfo(SceneManager.GetActiveScene().name, true));
    }

    static void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        Emit(SceneChanged, new SceneInfo(_scene.name, true));
    }

    static void OnSceneUnloaded(Scene _scene)
    {
        Emit(SceneChanged, new SceneInfo(_scene.name, false));
    }
}
