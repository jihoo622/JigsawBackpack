using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Preload 씬의 진입점. Awake에서 기동 준비(AppBootstrap)를 하고, Start에서 진행 표(FlowTableData)의 다음 씬으로 넘어간다.
// 테이블이 올바르지 않으면 넘어가지 않는다(잘못된 기본값으로 동작하지 않게).
// 에디터에서는 Play를 누를 때 열려 있던 씬으로 돌아간다(PreloadPlayMode가 경로를 넘긴다).
// 같은 씬의 GameLogger보다 먼저 Awake해야 LogKitSetup이 Inspector에 저장된 주소·키를 초기화 전에 읽는다.
[DefaultExecutionOrder(-1000)]
public sealed class PreloadController : MonoBehaviour
{
    // 에디터 전용: Play 직전에 열려 있던 씬 경로. Preload가 한 번 읽고 지운다.
    public const string EDITOR_ENTRY_SCENE_KEY = "MyJG.PlayEntryScene";

    void Awake()
    {
        AppBootstrap.Initialize();
    }

    // Awake 다음 프레임 직전에 넘어가 AfterSceneLoad 훅(씬 진입 기록, LogKit 자동 부트스트랩)이 먼저 돈다.
    void Start()
    {
        if (!AppBootstrap.TablesValid)
        {
            Debug.LogError("게임 테이블이 올바르지 않아 Preload에서 멈췄습니다. 콘솔의 테이블 오류를 먼저 고치세요.");
            return;
        }

#if UNITY_EDITOR
        string entry = SessionState.GetString(EDITOR_ENTRY_SCENE_KEY, string.Empty);
        SessionState.EraseString(EDITOR_ENTRY_SCENE_KEY);
        if (!string.IsNullOrEmpty(entry) && entry != gameObject.scene.path)
        {
            EditorSceneManager.LoadSceneInPlayMode(entry, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif

        string next = TableManager.Instance.NextSceneOf(gameObject.scene.name);
        if (string.IsNullOrEmpty(next))
        {
            Debug.LogError($"FlowTableData에 '{gameObject.scene.name}'의 다음 씬이 없습니다.");
            return;
        }
        SceneManager.LoadScene(next, LoadSceneMode.Single);
    }
}
