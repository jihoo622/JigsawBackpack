using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 화면 진행(Presenter). 시작은 진행 표(FlowTableData)의 다음 씬으로, 종료는 앱을 끝낸다.
// 테이블·LogKit 준비는 Preload가 끝낸 상태로 들어온다.
public sealed class TitlePresenter : MonoBehaviour
{
    [SerializeField] TitleView view;

    bool leaving;

    void OnEnable()
    {
        view.StartRequested += OnStartRequested;
        view.QuitRequested += OnQuitRequested;
    }

    void OnDisable()
    {
        view.StartRequested -= OnStartRequested;
        view.QuitRequested -= OnQuitRequested;
    }

    void Start()
    {
        view.SetTexts(Presentation.TitleGameName, Presentation.TitleStart, Presentation.TitleQuit);
        view.SetInteractable(true);
        view.FocusStart();
    }

    void OnStartRequested()
    {
        if (leaving)
        {
            return;
        }
        string next = TableManager.Instance.NextSceneOf(gameObject.scene.name);
        if (string.IsNullOrEmpty(next))
        {
            Debug.LogError($"FlowTableData에 '{gameObject.scene.name}'의 다음 씬이 없습니다.");
            return;
        }
        leaving = true;
        view.SetInteractable(false);
        SceneManager.LoadScene(next, LoadSceneMode.Single);
    }

    void OnQuitRequested()
    {
        if (leaving)
        {
            return;
        }
        leaving = true;
        view.SetInteractable(false);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
