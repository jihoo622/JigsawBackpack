using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 도메인 이벤트의 단일 발행 지점.
// 규칙:
//   1) 이벤트는 이 파일에 struct 페이로드와 함께 추가한다. 개별 컴포넌트가 서로를 직접 참조해 알리지 않는다.
//   2) 생산자는 Raise*만 호출한다. 구독자 예외가 게임 흐름을 멈추지 않도록 여기서 삼킨다.
//   3) 구독은 OnEnable/OnDisable에서 대칭으로 한다(OnDisable에서 해제하지 않으면 정적 이벤트에 갇힌다).
//   4) 페이로드는 struct로 불변 data only로 두고, 시간·좌표처럼 분석에 쓰는 값은 여기서 함께 보낸다.
public static class GameEvents
{
    /// <summary>게임 시간 진행 결과. 모든 값은 세션 시작 기준 시간 단위다.</summary>
    public readonly struct TimeAdvanceInfo
    {
        /// <summary>시간을 진행한 세션. 다른 씬의 이벤트를 구분하는 데 사용한다.</summary>
        public readonly PuzzleInventoryService session;
        /// <summary>진행 전 누적 경과 시간.</summary>
        public readonly long previousHours;
        /// <summary>진행 후 누적 경과 시간.</summary>
        public readonly long elapsedHours;
        /// <summary>이번 행동으로 경과한 시간. 신선도·내구도 갱신에 사용한다.</summary>
        public long DeltaHours => elapsedHours - previousHours;


        /// <summary>시간 변화 전후의 누적 시간을 보관한다.</summary>
        public TimeAdvanceInfo(long _previousHours, long _elapsedHours, PuzzleInventoryService _session = null)
        {
            session = _session;
            previousHours = _previousHours;
            elapsedHours = _elapsedHours;
        }
    }

    /// <summary>세션 시간·기한 판정 확정 후 발행된다. OnEnable/OnDisable에서 대칭으로 구독·해제한다.</summary>
    public static event Action<TimeAdvanceInfo> TimeAdvanced;

    /// <summary>확정된 게임 시간 변화를 알린다. 시간을 직접 진행하지 않으며 구독자 예외는 격리한다.</summary>
    /// <param name="_previousHours">진행 전 누적 경과 시간.</param>
    /// <param name="_elapsedHours">진행 후 누적 경과 시간.</param>
    /// <param name="_session">시간을 확정한 세션. 소비자는 자신이 속한 세션인지 확인한다.</param>
    public static void RaiseTimeAdvanced(long _previousHours, long _elapsedHours, PuzzleInventoryService _session = null)
    {
        Emit(TimeAdvanced, new TimeAdvanceInfo(_previousHours, _elapsedHours, _session));
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
    public readonly struct RunInfo
    {
        public readonly string scene;
        public readonly double time;
        public readonly string reason;
        public RunInfo(string _scene, double _time, string _reason = null)
        {
            this.scene = _scene;
            this.time = _time;
            this.reason = _reason;
        }
    }
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

    // ── 이벤트 ──
    public static event Action<RunInfo> RunStarted;
    public static event Action<RunInfo> RunEnded;
    public static event Action<SceneInfo> SceneChanged;
    public static event Action<UpgradeInfo> UpgradeGranted;
    public static event Action<PanelInfo> PanelVisibility;
    public static event Action<TradeInfo> TradeCompleted;

    // ── 발행 ──
    public static void RaiseRunStarted(string _scene, double _time)
    {
        Emit(RunStarted, new RunInfo(_scene, _time));
    }
    public static void RaiseRunEnded(string _scene, double _time, string _reason)
    {
        Emit(RunEnded, new RunInfo(_scene, _time, _reason));
    }
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
        RunStarted = null;
        RunEnded = null;
        SceneChanged = null;
        UpgradeGranted = null;
        PanelVisibility = null;
        TradeCompleted = null;
        CityArrived = null;
        TimeAdvanced = null;
        sequence = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
        Emit(SceneChanged, new SceneInfo(SceneManager.GetActiveScene().name, true));
    }

    static void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        Emit(SceneChanged, new SceneInfo(_scene.name, _mode != LoadSceneMode.Additive));
    }
}
