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
        public readonly long requestedHours;
        public readonly Vector2Int targetCell, grabbedCell;
        // 작업대 행동 ID·개조 모드·화차 상품·화차 개조·대상 보드·화차 순서. 해당 행동이 아니면 비어 있다.
        public readonly string actionId, modeId, productId, modificationId, board;
        public readonly string[] wagonOrder;

        public PuzzleActionInfo(PuzzleInventoryService _session, PuzzleActionKind _action, string _failure = null,
            string _targetCityId = null, int _instanceId = 0,
            string _destination = null, Vector2Int _targetCell = default, Vector2Int _grabbedCell = default,
            int _clockwiseTurns = 0, long _requestedHours = 0, string _actionId = null, string _modeId = null,
            string _productId = null, string _modificationId = null, string _board = null, string[] _wagonOrder = null)
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
            requestedHours = _requestedHours;
            actionId = _actionId;
            modeId = _modeId;
            productId = _productId;
            modificationId = _modificationId;
            board = _board;
            wagonOrder = _wagonOrder;
        }
    }

    public static event Action<PuzzleRunInfo> PuzzleRunStarted;
    public static event Action<PuzzleRunInfo> PuzzleRunEnded;
    public static event Action<PuzzleActionInfo> PuzzleActionCommitted;
    // 영업 기간이 끝나 결산했다(승패 확정). 상태·잔액은 세션에서 읽는다.
    public static event Action<PuzzleRunInfo> PuzzleRunSettled;

    public static void RaisePuzzleRunSettled(PuzzleInventoryService _session)
    {
        Emit(PuzzleRunSettled, new PuzzleRunInfo(_session, null));
    }

    // 승리 결산 뒤 무한 모드로 판을 이어 간다. 마감이 사라지고 진행 중 상태로 돌아온다.
    public static event Action<PuzzleRunInfo> PuzzleRunContinued;

    public static void RaisePuzzleRunContinued(PuzzleInventoryService _session)
    {
        Emit(PuzzleRunContinued, new PuzzleRunInfo(_session, null));
    }

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

    /// <summary>게임 시간 진행 결과. 누적 값과 차이는 세션 시작 기준 게임 시간(1시간) 단위다.</summary>
    public readonly struct TimeAdvanceInfo
    {
        /// <summary>시간을 진행한 세션. 다른 씬의 이벤트를 구분하는 데 사용한다.</summary>
        public readonly PuzzleInventoryService session;
        /// <summary>진행 전 누적 경과 시간.</summary>
        public readonly long previousHours;
        /// <summary>진행 후 누적 경과 시간.</summary>
        public readonly long elapsedHours;
        internal readonly decimal? cargoValueBeforeSettlement;
        internal readonly decimal? cargoValueBeforeFreshnessLoss;
        /// <summary>이번 행동으로 경과한 시간. 신선도·내구도 갱신에 사용한다.</summary>
        public long DeltaHours => elapsedHours - previousHours;


        /// <summary>시간 변화 전후의 누적 시간을 보관한다.</summary>
        public TimeAdvanceInfo(long _previousHours, long _elapsedHours, PuzzleInventoryService _session = null,
            decimal? _cargoValueBeforeSettlement = null, decimal? _cargoValueBeforeFreshnessLoss = null)
        {
            session = _session;
            previousHours = _previousHours;
            elapsedHours = _elapsedHours;
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
        public readonly PuzzleRumorKind kind;
        public readonly string sourceCityId, targetCityId, message, tag, itemId, actionId, wagonProductId;
        public readonly long recordedHours;

        public TavernRumorInfo(PuzzleInventoryService _session, string _sourceCityId, string _targetCityId,
            string _message, long _recordedHours, string _tag, PuzzleRumorKind _kind, string _itemId, string _actionId,
            string _wagonProductId = null)
        {
            session = _session;
            sourceCityId = _sourceCityId;
            targetCityId = _targetCityId;
            message = _message;
            recordedHours = _recordedHours;
            tag = _tag;
            kind = _kind;
            itemId = _itemId ?? string.Empty;
            actionId = _actionId ?? string.Empty;
            wagonProductId = _wagonProductId ?? string.Empty;
        }
    }
    public static event Action<TavernRumorInfo> TavernRumorRecorded;

    // 의뢰 하나의 상태가 바뀔 때(수락·완료·실패) 발행한다. 값은 변화 직후의 의뢰 상태다.
    public readonly struct QuestInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly int questId;
        public readonly string templateId, pieceId, sourceCityId, targetCityId, penaltyId, state, failure;
        public readonly long reward, deposit, payout, deadlineHours, elapsedHours;
        public readonly int cargoInstanceId;

        internal QuestInfo(PuzzleInventoryService _session, TrainventoryQuest _quest, long _elapsedHours)
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
            deadlineHours = _quest.DeadlineHours;
            elapsedHours = _elapsedHours;
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
        public readonly long gainedXp, xp, elapsedHours;
        public readonly int previousLevel, level, instanceId, questId;

        internal ProficiencyInfo(PuzzleInventoryService _session, string _tag, string _source, long _gainedXp, long _xp,
            int _previousLevel, int _level, int _instanceId, int _questId, long _elapsedHours)
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
            elapsedHours = _elapsedHours;
        }

        public bool LeveledUp => level > previousLevel;
    }
    public static event Action<ProficiencyInfo> ProficiencyChanged;

    internal static void RaiseProficiencyChanged(ProficiencyInfo _info)
    {
        Emit(ProficiencyChanged, _info);
    }

    public static void RaiseTavernRumorRecorded(PuzzleInventoryService _session, string _sourceCityId,
        string _targetCityId, string _message, long _recordedHours, string _tag, PuzzleRumorKind _kind, string _itemId,
        string _actionId, string _wagonProductId = null)
    {
        Emit(TavernRumorRecorded, new TavernRumorInfo(_session, _sourceCityId, _targetCityId, _message,
            _recordedHours, _tag, _kind, _itemId, _actionId, _wagonProductId));
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

    // 수첩의 상성표 탭. 같은 씬의 상성표 패널이 보이기/숨기기로 응답한다.
    public readonly struct SynergyChartShowInfo
    {
        public readonly int sceneHandle;
        public readonly bool show;
        public SynergyChartShowInfo(int _sceneHandle, bool _show)
        {
            sceneHandle = _sceneHandle;
            show = _show;
        }
    }
    public static event Action<SynergyChartShowInfo> SynergyChartShowRequested;
    public static void RaiseSynergyChartShowRequested(int _sceneHandle, bool _show)
    {
        Emit(SynergyChartShowRequested, new SynergyChartShowInfo(_sceneHandle, _show));
    }
    // 고유명사 하이퍼링크(FM식). 링크가 있는 TMP 글자의 TextLinkHandler가 발행하고, 씬 로컬 라우터·미리보기 카드가 구독한다.
    // 링크 ID 형식은 "<종류>:<테이블 ID>"이며 만들고 읽는 곳은 Presentation.EntityLink / Presentation.TryParseEntityLink 한 곳이다.
    public readonly struct EntityLinkInfo
    {
        public readonly int sceneHandle;
        public readonly EntityLinkKind kind;
        public readonly string id;
        public readonly Vector2 screenPosition;
        public EntityLinkInfo(int _sceneHandle, EntityLinkKind _kind, string _id, Vector2 _screenPosition)
        {
            sceneHandle = _sceneHandle;
            kind = _kind;
            id = _id;
            screenPosition = _screenPosition;
        }
    }
    public static event Action<EntityLinkInfo> EntityLinkClicked;
    public static event Action<EntityLinkInfo> EntityLinkContextRequested;
    public static event Action<EntityLinkInfo> EntityLinkHovered;
    public static event Action<EntityLinkInfo> EntityLinkHoverMoved;
    public static event Action<int> EntityLinkHoverEnded;
    public static void RaiseEntityLinkClicked(EntityLinkInfo _info)
    {
        Emit(EntityLinkClicked, _info);
    }
    public static void RaiseEntityLinkContextRequested(EntityLinkInfo _info)
    {
        Emit(EntityLinkContextRequested, _info);
    }
    public static void RaiseEntityLinkHovered(EntityLinkInfo _info)
    {
        Emit(EntityLinkHovered, _info);
    }
    public static void RaiseEntityLinkHoverEnded(int _sceneHandle)
    {
        Emit(EntityLinkHoverEnded, _sceneHandle);
    }

    public static void RaiseEntityLinkHoverMoved(EntityLinkInfo _info) => Emit(EntityLinkHoverMoved, _info);

    // 링크 이동 요청: 지도에서 도시(또는 지역)를 보이기, 상성표에서 태그·품목을 강조하기.
    public readonly struct MapFocusInfo
    {
        public readonly int sceneHandle;
        public readonly string cityId;
        public readonly string regionId;
        public MapFocusInfo(int _sceneHandle, string _cityId, string _regionId)
        {
            sceneHandle = _sceneHandle;
            cityId = _cityId;
            regionId = _regionId;
        }
    }
    public static event Action<MapFocusInfo> MapFocusRequested;
    public static void RaiseMapFocusRequested(MapFocusInfo _info)
    {
        Emit(MapFocusRequested, _info);
    }

    // 경로 계획(예약)이 만들어지거나·진행되거나·정차역이 바뀌거나·끝났다. 현재 계획은 session.RoutePlan(없으면 null).
    public readonly struct RoutePlanInfo
    {
        public readonly PuzzleInventoryService session;
        public RoutePlanInfo(PuzzleInventoryService _session)
        {
            session = _session;
        }
    }
    public static event Action<RoutePlanInfo> RoutePlanChanged;
    public static void RaiseRoutePlanChanged(RoutePlanInfo _info)
    {
        Emit(RoutePlanChanged, _info);
    }

    // 분석 로그용 경로 사건. reason 값은 PLAY_LOG.md가 원본.
    public readonly struct RoutePlannedInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string fromCityId;
        public readonly string toCityId;
        public readonly int legs;
        public readonly long totalTurns;
        public readonly int stops;
        public RoutePlannedInfo(PuzzleInventoryService _session, string _fromCityId, string _toCityId, int _legs, long _totalTurns, int _stops)
        {
            session = _session;
            fromCityId = _fromCityId;
            toCityId = _toCityId;
            legs = _legs;
            totalTurns = _totalTurns;
            stops = _stops;
        }
    }
    public static event Action<RoutePlannedInfo> RoutePlanned;
    public static void RaiseRoutePlanned(RoutePlannedInfo _info)
    {
        Emit(RoutePlanned, _info);
    }

    // reason: completed / canceled / deviated / run_ended
    public readonly struct RouteEndedInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string reason;
        public readonly string toCityId;
        public RouteEndedInfo(PuzzleInventoryService _session, string _reason, string _toCityId)
        {
            session = _session;
            reason = _reason;
            toCityId = _toCityId;
        }
    }
    public static event Action<RouteEndedInfo> RouteEnded;
    public static void RaiseRouteEnded(RouteEndedInfo _info)
    {
        Emit(RouteEnded, _info);
    }

    // reason: stop_reached / insufficient_funds / run_ended / user_stopped / failed
    public readonly struct AutoTravelStoppedInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string reason;
        public readonly string cityId;
        public AutoTravelStoppedInfo(PuzzleInventoryService _session, string _reason, string _cityId)
        {
            session = _session;
            reason = _reason;
            cityId = _cityId;
        }
    }
    public static event Action<AutoTravelStoppedInfo> AutoTravelStopped;
    public static void RaiseAutoTravelStopped(AutoTravelStoppedInfo _info)
    {
        Emit(AutoTravelStopped, _info);
    }

    // 플레이어가 계획한 경로의 경유역 정차 여부를 바꿨다.
    public readonly struct RouteStopChangedInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string cityId, toCityId;
        public readonly bool stop;
        public RouteStopChangedInfo(PuzzleInventoryService _session, string _cityId, bool _stop, string _toCityId)
        {
            session = _session;
            cityId = _cityId;
            stop = _stop;
            toCityId = _toCityId;
        }
    }
    public static event Action<RouteStopChangedInfo> RouteStopChanged;
    public static void RaiseRouteStopChanged(RouteStopChangedInfo _info)
    {
        Emit(RouteStopChanged, _info);
    }

    // 역 행동 패널 안의 하위 화면(home·market·wagon_shop·rest·tavern·butchery·modification·refining) 전환.
    public readonly struct StationScreenInfo
    {
        public readonly string screen;
        public StationScreenInfo(string _screen)
        {
            screen = _screen;
        }
    }
    public static event Action<StationScreenInfo> StationScreenChanged;
    public static void RaiseStationScreenChanged(string _screen)
    {
        Emit(StationScreenChanged, new StationScreenInfo(_screen));
    }

    // 지도 밖(검색 결과 등)에서 그 역까지 경로 계획을 만들어 달라는 요청. 지도 경로 계획 Presenter가 받는다.
    public readonly struct RoutePlanRequestInfo
    {
        public readonly int sceneHandle;
        public readonly string cityId;
        public RoutePlanRequestInfo(int _sceneHandle, string _cityId)
        {
            sceneHandle = _sceneHandle;
            cityId = _cityId;
        }
    }
    public static event Action<RoutePlanRequestInfo> RoutePlanRequested;
    public static void RaiseRoutePlanRequested(RoutePlanRequestInfo _info)
    {
        Emit(RoutePlanRequested, _info);
    }

    // 글자 입력 칸(검색 등)이 포커스를 얻거나 잃었다. 켜져 있는 동안 키보드 게임 입력(회전·단축키)을 막는다.
    public readonly struct TextInputFocusInfo
    {
        public readonly int sceneHandle;
        public readonly bool focused;
        public TextInputFocusInfo(int _sceneHandle, bool _focused)
        {
            sceneHandle = _sceneHandle;
            focused = _focused;
        }
    }
    public static event Action<TextInputFocusInfo> TextInputFocusChanged;
    public static void RaiseTextInputFocusChanged(TextInputFocusInfo _info)
    {
        Emit(TextInputFocusChanged, _info);
    }
    public readonly struct SynergyHighlightInfo
    {
        public readonly int sceneHandle;
        public readonly string[] tags;
        public readonly string itemId;
        public SynergyHighlightInfo(int _sceneHandle, string[] _tags, string _itemId)
        {
            sceneHandle = _sceneHandle;
            tags = _tags;
            itemId = _itemId;
        }
    }
    public static event Action<SynergyHighlightInfo> SynergyHighlightRequested;
    public static void RaiseSynergyHighlightRequested(SynergyHighlightInfo _info)
    {
        Emit(SynergyHighlightRequested, _info);
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
    /// <param name="_previousHours">진행 전 누적 경과 시간.</param>
    /// <param name="_elapsedHours">진행 후 누적 경과 시간.</param>
    /// <param name="_session">시간을 확정한 세션. 소비자는 자신이 속한 세션인지 확인한다.</param>
    public static void RaiseTimeAdvanced(long _previousHours, long _elapsedHours, PuzzleInventoryService _session = null,
        decimal? _cargoValueBeforeSettlement = null, decimal? _cargoValueBeforeFreshnessLoss = null)
    {
        Emit(TimeAdvanced, new TimeAdvanceInfo(_previousHours, _elapsedHours, _session, _cargoValueBeforeSettlement,
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
        // 호버 툴팁처럼 포인터를 따라 자주 열리고 닫히는 패널. 분석 로그는 이 변화를 수집하지 않는다.
        public readonly bool transient;
        public PanelInfo(string _panel, bool _open, bool _transient = false)
        {
            this.panel = _panel;
            this.open = _open;
            this.transient = _transient;
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

    /// <summary>작업대(도축·개조·정제) 완료 사건. 산출물이 작업대 보드에 놓이거나 개조가 적용돼 회수 대기가 된 시점이다.</summary>
    public readonly struct WorkbenchReadyInfo
    {
        public readonly PuzzleInventoryService session;
        public readonly string actionId, cityId;
        public readonly int outputCount;
        public readonly long elapsedHours;

        public WorkbenchReadyInfo(PuzzleInventoryService _session, string _actionId, string _cityId, int _outputCount,
            long _elapsedHours)
        {
            session = _session;
            actionId = _actionId;
            cityId = _cityId;
            outputCount = _outputCount;
            elapsedHours = _elapsedHours;
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
    public static event Action<WorkbenchReadyInfo> WorkbenchReady;

    // ── 발행 ──
    public static void RaiseUpgradeGranted(UpgradeData _upgrade)
    {
        if (_upgrade == null)
        {
            return;
        }
        Emit(UpgradeGranted, new UpgradeInfo(_upgrade.id, string.IsNullOrEmpty(_upgrade.displayName) ? _upgrade.id : _upgrade.displayName));
    }
    public static void RaisePanelVisibility(string _panelName, bool _open, bool _transient = false)
    {
        Emit(PanelVisibility, new PanelInfo(_panelName, _open, _transient));
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
    public static void RaiseWorkbenchReady(PuzzleInventoryService _session, string _actionId, string _cityId,
        int _outputCount)
    {
        Emit(WorkbenchReady, new WorkbenchReadyInfo(_session, _actionId, _cityId, _outputCount,
            _session.Time != null ? _session.Time.ElapsedHours : 0));
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
        PuzzleRunSettled = null;
        PuzzleRunContinued = null;
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
        SynergyChartShowRequested = null;
        NotebookOpenRequested = null;
        EntityLinkClicked = null;
        EntityLinkContextRequested = null;
        EntityLinkHovered = null;
        EntityLinkHoverMoved = null;
        EntityLinkHoverEnded = null;
        MapFocusRequested = null;
        RoutePlanChanged = null;
        RoutePlanned = null;
        RouteEnded = null;
        AutoTravelStopped = null;
        RouteStopChanged = null;
        StationScreenChanged = null;
        RoutePlanRequested = null;
        TextInputFocusChanged = null;
        SynergyHighlightRequested = null;
        ButcheryChanged = null;
        ModificationChanged = null;
        RefiningChanged = null;
        WorkbenchReady = null;
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
