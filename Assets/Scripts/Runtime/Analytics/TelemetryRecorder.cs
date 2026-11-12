using UnityEngine;

// 게임 도메인 이벤트를 분석 파일로 흘리는 단 한 곳의 구독자.
// 개별 시스템이 TelemetryLog를 직접 호출하면 이벤트 이름과 필드가 흩어진다. GameEvents를 추가하고 여기서 받아 쓴다.
// TelemetryLog가 같은 원본을 로컬에 보관하고 LogKit Data로 전송한다.
public sealed class TelemetryRecorder : MonoBehaviour
{
    PuzzleInventoryService activeRun;
    TelemetryPayload.PuzzleState puzzleState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var host = new GameObject("Telemetry recorder");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<TelemetryRecorder>();
    }

    // 구독은 OnEnable/OnDisable 대칭(해제하지 않으면 정적 이벤트에 갇혀 재진입 시 중복 기록된다).
    void OnEnable()
    {
        GameEvents.SceneChanged += OnSceneChanged;
        GameEvents.UpgradeGranted += OnUpgradeGranted;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.PuzzleRunStarted += OnPuzzleRunStarted;
        GameEvents.PuzzleRunEnded += OnPuzzleRunEnded;
        GameEvents.PuzzleActionCommitted += OnPuzzleAction;
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        TelemetryLog.BeforeClose += OnSessionClosing;
    }

    void OnDisable()
    {
        FinishRun("recorder_disabled");
        GameEvents.SceneChanged -= OnSceneChanged;
        GameEvents.UpgradeGranted -= OnUpgradeGranted;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.PuzzleRunStarted -= OnPuzzleRunStarted;
        GameEvents.PuzzleRunEnded -= OnPuzzleRunEnded;
        GameEvents.PuzzleActionCommitted -= OnPuzzleAction;
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        TelemetryLog.BeforeClose -= OnSessionClosing;
    }

    void OnSceneChanged(GameEvents.SceneInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.SCENE_CHANGE, TelemetryPayload.Scene(_info));
    }

    void OnUpgradeGranted(GameEvents.UpgradeInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.UPGRADE_GRANTED, TelemetryPayload.Upgrade(_info));
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.PANEL_VISIBILITY, TelemetryPayload.Panel(_info));
    }

    void OnPuzzleRunStarted(GameEvents.PuzzleRunInfo _info)
    {
        if (!TelemetryLog.IsOpen)
        {
            return;
        }
        FinishRun("replaced");
        activeRun = _info.session;
        puzzleState = new TelemetryPayload.PuzzleState();
        TelemetryLog.Write(TelemetryEvents.RUN_START, puzzleState.Start(activeRun));
    }

    void OnPuzzleRunEnded(GameEvents.PuzzleRunInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            FinishRun(_info.reason);
        }
    }

    void OnPuzzleAction(GameEvents.PuzzleActionInfo _info)
    {
        if (!ReferenceEquals(activeRun, _info.session) || puzzleState == null || !TelemetryLog.IsOpen)
        {
            return;
        }
        var payload = puzzleState.Action(_info);
        // 같은 위치에 그대로 놓은 성공은 상태 변화가 아니므로 기록하지 않는다.
        bool unchangedPlacement = (_info.action == PuzzleActionKind.Place || _info.action == PuzzleActionKind.QuickTransfer)
            && string.IsNullOrEmpty(payload.failure) && payload.moneyDelta == 0
            && payload.introduced.Length == 0 && payload.moved.Length == 0
            && string.IsNullOrEmpty(payload.cityId) && payload.wagonDelta == 0
            && payload.removed.Length == 0 && payload.values.Length == 0 && payload.durability.Length == 0;
        if (!unchangedPlacement)
        {
            TelemetryLog.Write(TelemetryEvents.PUZZLE_ACTION, payload);
        }
        if (_info.session.RunStatus != PuzzleRunStatus.InProgress)
        {
            FinishRun(_info.session.RunStatus == PuzzleRunStatus.Won ? "won" : "deadline");
        }
    }

    void OnRumorRecorded(GameEvents.TavernRumorInfo _info)
    {
        if (ReferenceEquals(activeRun, _info.session))
        {
            TelemetryLog.Write(TelemetryEvents.RUMOR_LEARNED, new PuzzleRumorPayload
            {
                targetCityId = _info.targetCityId,
                tag = _info.tag,
                elapsedMinutes = _info.recordedMinutes
            });
        }
    }

    void OnSessionClosing()
    {
        FinishRun("shutdown");
    }

    void FinishRun(string _reason)
    {
        if (puzzleState == null)
        {
            return;
        }
        activeRun = null;
        puzzleState = null;
        if (TelemetryLog.IsOpen)
        {
            TelemetryLog.Write(TelemetryEvents.RUN_END, new PuzzleRunEndPayload
            {
                reason = _reason
            });
        }
    }
}
