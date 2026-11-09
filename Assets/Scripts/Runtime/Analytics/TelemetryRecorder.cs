using UnityEngine;

// 게임 도메인 이벤트를 분석 파일로 흘리는 단 한 곳의 구독자.
// 개별 시스템이 TelemetryLog를 직접 호출하면 이벤트 이름과 필드가 흩어진다. GameEvents를 추가하고 여기서 받아 쓴다.
// LogKit의 콘솔·오류 수집과 달리 이 경로는 서버로 자동 전송되지 않으므로 파일 자체가 기록의 원본이다.
public sealed class TelemetryRecorder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var host = new GameObject("Telemetry recorder");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<TelemetryRecorder>();
    }

    // 구독은 OnEnable/OnDisable 대칭(해제하지 않으면 정적 이벤트에 갇혀 재진입 시 중복 기록된다).
    void OnEnable()
    {
        GameEvents.RunStarted += OnRunStarted;
        GameEvents.RunEnded += OnRunEnded;
        GameEvents.SceneChanged += OnSceneChanged;
        GameEvents.UpgradeGranted += OnUpgradeGranted;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.TradeCompleted += OnTradeCompleted;
    }

    void OnDisable()
    {
        GameEvents.RunStarted -= OnRunStarted;
        GameEvents.RunEnded -= OnRunEnded;
        GameEvents.SceneChanged -= OnSceneChanged;
        GameEvents.UpgradeGranted -= OnUpgradeGranted;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.TradeCompleted -= OnTradeCompleted;
    }

    void OnRunStarted(GameEvents.RunInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.RUN_START, TelemetryPayload.Run(_info));
    }

    void OnRunEnded(GameEvents.RunInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.RUN_END, TelemetryPayload.Run(_info));
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

    void OnTradeCompleted(GameEvents.TradeInfo _info)
    {
        TelemetryLog.Write(TelemetryEvents.TRADE_COMPLETED, TelemetryPayload.Trade(_info));
    }
}
