using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 에디터 Play를 항상 빌드 첫 씬(Preload)에서 시작한다. 기동 준비(LogKit·분석 로그·테이블)는 Preload만 맡는다.
// Play 직전에 열려 있던 씬은 Preload가 준비를 마친 뒤 그대로 연다(PreloadController.EDITOR_ENTRY_SCENE_KEY).
// Tools/MyJG/Play From Preload로 끌 수 있다. 끄면 AppBootstrap의 안전망이 첫 씬 Awake 전에 같은 준비를 하고 경고를 남긴다.
[InitializeOnLoad]
public static class PreloadPlayMode
{
    public const string MENU = "Tools/MyJG/Play From Preload";
    const string ENABLED_KEY = "MyJG.PlayFromPreload";

    static PreloadPlayMode()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorBuildSettings.sceneListChanged += Apply;
        // 빌드 씬 목록이 읽힌 뒤 적용한다.
        EditorApplication.delayCall += Apply;
    }

    public static bool Enabled
    {
        get
        {
            return EditorPrefs.GetBool(ENABLED_KEY, true);
        }
    }

    [MenuItem(MENU)]
    static void Toggle()
    {
        EditorPrefs.SetBool(ENABLED_KEY, !Enabled);
        Apply();
    }

    [MenuItem(MENU, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MENU, Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    // 빌드 목록의 첫 활성 씬이 시작 씬(Preload)이다.
    static SceneAsset StartScene()
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrEmpty(scene.path))
            {
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            }
        }
        return null;
    }

    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        EditorSceneManager.playModeStartScene = Enabled ? StartScene() : null;
    }

    static void OnPlayModeChanged(PlayModeStateChange _state)
    {
        if (_state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(PreloadController.EDITOR_ENTRY_SCENE_KEY);
            Apply();
            return;
        }
        if (_state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        SessionState.EraseString(PreloadController.EDITOR_ENTRY_SCENE_KEY);
        var start = EditorSceneManager.playModeStartScene;
        if (!Enabled || !start)
        {
            return;
        }
        // Play 중에는 씬을 디스크에서 다시 읽으므로 저장하지 않은 변경을 먼저 묻는다. 취소하면 Play도 취소한다.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorApplication.isPlaying = false;
            return;
        }
        string active = SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(active) && active != AssetDatabase.GetAssetPath(start))
        {
            SessionState.SetString(PreloadController.EDITOR_ENTRY_SCENE_KEY, active);
        }
    }
}
