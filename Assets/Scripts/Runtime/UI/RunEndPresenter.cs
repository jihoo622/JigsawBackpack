using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// 런 종료 모달의 상태·입력 차단·무한 모드 계속·재시작·종료 흐름을 담당한다.
public sealed class RunEndPresenter : MonoBehaviour
{
    [SerializeField] RunEndView view;
    [SerializeField] PuzzleInventoryInput inventoryInput;

    PuzzleInventoryService activeSession;
    float previousTimeScale = 1f;
    bool pausedByModal;
    bool restoreInventoryInput;
    GameObject previousSelection;

    internal bool CanShow => isActiveAndEnabled && view && !view.IsOpen;

    void Awake()
    {
        if (!view)
        {
            view = GetComponent<RunEndView>();
        }
        if (view)
        {
            view.BindButtons(ContinueEndless, RestartGame, QuitGame);
        }
    }

    void OnDisable()
    {
        RestoreRuntimeState();
    }

    internal void Show(PuzzleInventoryService _session)
    {
        if (_session == null || _session.RunStatus == PuzzleRunStatus.InProgress || !CanShow)
        {
            return;
        }
        var pause = SceneServices.Find<PauseMenu>(gameObject.scene);
        if (pause && pause.IsOpen)
        {
            pause.Close();
        }

        activeSession = _session;
        long elapsedTurns = _session.Time != null ? _session.Time.ElapsedTurns : 0L;
        int completedQuests = _session.Quests != null
            ? _session.Quests.Count(_quest => _quest.State == TrainventoryQuestState.Completed)
            : 0;

        PauseRuntimeState();
        view.ShowResult(_session.RunStatus, _session.Funds, _session.TargetFunds, elapsedTurns, completedQuests);
        SelectDefaultButton();
    }

    public void ContinueEndless()
    {
        var session = activeSession ?? SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session == null || session.RunStatus != PuzzleRunStatus.Won || !session.TryContinueEndless())
        {
            return;
        }
        activeSession = null;
        if (view)
        {
            view.Close();
        }
        RestoreRuntimeState();
    }

    public void RestartGame()
    {
        var session = activeSession ?? SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        GameEvents.RaisePuzzleRunEnded(session, "restart");
        activeSession = null;
        if (view)
        {
            view.Close();
        }
        RestoreRuntimeState();
        SceneManager.LoadScene(gameObject.scene.path, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        activeSession = null;
        if (view)
        {
            view.Close();
        }
        RestoreRuntimeState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PauseRuntimeState()
    {
        if (pausedByModal)
        {
            return;
        }
        pausedByModal = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        var input = inventoryInput ? inventoryInput : SceneServices.Find<PuzzleInventoryInput>(gameObject.scene);
        inventoryInput = input;
        restoreInventoryInput = input && input.enabled;
        if (restoreInventoryInput)
        {
            input.enabled = false;
        }
    }

    void RestoreRuntimeState()
    {
        if (!pausedByModal)
        {
            return;
        }
        pausedByModal = false;
        Time.timeScale = previousTimeScale;
        if (restoreInventoryInput && inventoryInput)
        {
            inventoryInput.enabled = true;
        }
        restoreInventoryInput = false;
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(
                previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        previousSelection = null;
    }

    void SelectDefaultButton()
    {
        if (!EventSystem.current || !view)
        {
            return;
        }
        previousSelection = EventSystem.current.currentSelectedGameObject;
        var target = view.DefaultSelectedObject;
        if (target)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
