using UnityEngine;
using UnityEngine.SceneManagement;

// 앱 기동 준비의 단일 진입점: LogKit 설정 → 분석 로그 세션·기록기 → 게임 테이블 로드·검증.
// Preload 씬(빌드 첫 씬)의 PreloadController가 Awake에서 부른다. 다시 불러도 한 번만 준비한다.
public static class AppBootstrap
{
    static bool initialized;

    // 테이블 검증 결과. false면 Preload가 다음 씬으로 넘어가지 않는다.
    public static bool TablesValid { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        initialized = false;
        TablesValid = false;
    }

    public static bool Initialize()
    {
        if (initialized)
        {
            return TablesValid;
        }
        initialized = true;
        // GameLogger.Init(분석 세션 열기) 전에 주소·키·수집 설정을 넣는다.
        LogKitSetup.Initialize();
        TelemetryLog.Initialize();
        TelemetryRecorder.Initialize();
        // 테이블 오류는 생성 중 항목별로 콘솔에 보고된다.
        TablesValid = TableManager.Initialize().IsValid;
        return TablesValid;
    }

    // 안전망: 빌드 첫 씬(Preload)이 아닌 씬으로 시작했으면(Play From Preload를 끈 에디터 등) 첫 씬 Awake 전에 같은 준비를 한다.
    // 정상 흐름에서는 Preload가 준비하므로 여기서는 아무것도 하지 않는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureWithoutPreload()
    {
        if (initialized || SceneManager.GetActiveScene().buildIndex == 0)
        {
            return;
        }
        Debug.LogWarning($"Preload를 거치지 않고 '{SceneManager.GetActiveScene().name}' 씬으로 시작했습니다. 같은 기동 준비를 대신 실행합니다.");
        Initialize();
    }
}
