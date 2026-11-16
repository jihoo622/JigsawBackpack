using System;
using LogKit;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// LogKit 기동 훅. Preload 씬의 AppBootstrap이 LogKit 자동 부트스트랩(AfterSceneLoad)과 GameLogger.Init 이전에 부른다.
// 수집·전송 동작(콘솔 수집, 주기·종료 전송, 재시도, 제한 시간)의 원본은 Preload 씬 LogKit 오브젝트의 GameLogger Inspector다.
// 여기서는 그 값을 Configure로 덮지 않는다. Preload를 거치지 않은 시작(AppBootstrap 안전망)은 패키지 코드 기본값을 쓴다.
// Debug.Log 계열 로그는 LogKit이 자동 수집해 전송하므로 개별 호출이 필요 없다.
// 게임 분석 사건은 GameEvents → TelemetryRecorder → TelemetryLog에서 LogKit Data로 보낸다.
public static class LogKitSetup
{
    // LogKit이 큐를 닫기 전에 반드시 한 번 실행해야 하는 작업(판 종료 로그 등)을 여기 등록한다.
    public static event Action BeforeShutdown;

    static bool initialized;

    // 서버 주소·업로드 키의 우선순위: GameLogger Inspector > 환경 변수(에디터만) > LogKitBuildConfig > 패키지 기본값.
    //   Inspector: 값이 있으면 그대로 쓴다. 주소가 패키지 기본값이거나 키가 빈 값이면 "비어 있음"으로 본다.
    //   에디터: Inspector가 비어 있는 항목만 LOGKIT_SERVER_URL, LOGKIT_UPLOAD_KEY로 채운다.
    //   빌드: 실행 시 환경 변수는 읽지 않는다. LogKit 빌드 단계가 빌드 PC(CI)의 같은 변수로 LogKitBuildConfig를 넣고,
    //         GameLogger가 비어 있는 자리에 그 값을 채운다.
    //   여러 게임을 같은 PC에서 작업하면 환경 변수는 사용자 범위가 아니라 에디터를 띄우는 셸에서만 설정한다(TELEMETRY.md).
    const string SERVER_URL_VARIABLE = "LOGKIT_SERVER_URL";
    const string UPLOAD_KEY_VARIABLE = "LOGKIT_UPLOAD_KEY";
    // LogKit 패키지의 기본 주소(GameLogger 내부 상수와 같은 값). Inspector가 이 값이면 비어 있는 것으로 본다.
    public const string PACKAGE_DEFAULT_SERVER_URL = "http://localhost:5000/api/logs";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShutdownHooks()
    {
        RemoveShutdownHooks();
        BeforeShutdown = null;
        initialized = false;
    }

    // 앱 기동 중 한 번. 다시 불러도 훅과 설정을 겹쳐 넣지 않는다.
    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;
        // GameLogger의 AfterSceneLoad 부트스트랩보다 먼저 wantsToQuit에 붙는다.
        Application.wantsToQuit += PrepareQuit;
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += PrepareEditorQuit;

        // 에디터 테스트용: Inspector가 비어 있는 항목만 이 PC의 환경 변수로 채운다.
        // 빌드에서도 읽으면 실행 PC의 환경 변수(예: 다른 게임의 dev-log-key)가 빌드 키를 덮어 다른 프로젝트로 섞인다.
        string serverUrl = Environment.GetEnvironmentVariable(SERVER_URL_VARIABLE);
        string apiKey = Environment.GetEnvironmentVariable(UPLOAD_KEY_VARIABLE);
        if (!string.IsNullOrWhiteSpace(serverUrl) || !string.IsNullOrWhiteSpace(apiKey))
        {
            // 씬의 GameLogger가 초기화(빌드 설정 적용)되기 전에 읽어야 Inspector에 저장된 값을 본다. PreloadController가 먼저 실행된다.
            // SerializedObject는 네이티브 핸들을 남겨 Play 종료 뒤 해제 시점에 에디터를 멈출 수 있으므로 JsonUtility로 직렬화 값만 복사한다.
            var logger = UnityEngine.Object.FindFirstObjectByType<GameLogger>(FindObjectsInactive.Include);
            var inspector = logger ? JsonUtility.FromJson<InspectorValues>(JsonUtility.ToJson(logger)) : null;
            GameLogger.Configure(
                developmentServerUrl: FillIfUnset(inspector?._developmentServerUrl, serverUrl, true),
                releaseServerUrl: FillIfUnset(inspector?._releaseServerUrl, serverUrl, true),
                apiKey: FillIfUnset(inspector?._apiKey, apiKey, false));
        }
#endif
    }

#if UNITY_EDITOR
    // GameLogger Inspector의 주소·키 사본. 필드 이름은 패키지의 직렬화 이름과 같아야 JsonUtility가 채운다(스타일 예외).
    [Serializable]
    sealed class InspectorValues
    {
        public string _developmentServerUrl;
        public string _releaseServerUrl;
        public string _apiKey;
    }

    // Inspector 항목이 비어 있으면(주소는 패키지 기본값 포함) 환경 변수 값을, 아니면 null(Inspector 유지)을 돌려준다.
    // 씬에 GameLogger가 없으면 모든 항목이 비어 있는 것으로 본다.
    static string FillIfUnset(string _current, string _variable, bool _isServerUrl)
    {
        if (string.IsNullOrWhiteSpace(_variable))
        {
            return null;
        }
        bool unset = string.IsNullOrWhiteSpace(_current) || (_isServerUrl && _current == PACKAGE_DEFAULT_SERVER_URL);
        return unset ? _variable : null;
    }
#endif

    static bool PrepareQuit()
    {
        BeforeShutdown?.Invoke();
        return true;
    }

    static void RemoveShutdownHooks()
    {
        Application.wantsToQuit -= PrepareQuit;
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.quitting -= PrepareEditorQuit;
#endif
    }

#if UNITY_EDITOR
    static void PrepareEditorQuit()
    {
        PrepareQuit();
    }
    static void OnPlayModeChanged(PlayModeStateChange _change)
    {
        if (_change != PlayModeStateChange.ExitingPlayMode)
        {
            return;
        }
        PrepareQuit();
        RemoveShutdownHooks();
    }
#endif
}
