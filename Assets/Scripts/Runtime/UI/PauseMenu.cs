using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine;

// UI hierarchy and button events are serialized in Game.unity.
public sealed class PauseMenu : PanelBase
{
    [SerializeField] PuzzleInventoryInput inventoryInput;
    [SerializeField] Button restartButton;
    [SerializeField] TextMeshProUGUI panelTitle;
    [SerializeField] TextMeshProUGUI panelHint;

    float previousTimeScale;
    bool paused;
    bool restoreInventoryInput;
    bool showingCompletion;
    GameObject previousSelection;
    InputActionAsset actions;
    InputAction cancel;
    int cancelConsumedFrame = -1;

    PuzzleMapPresenter mapPresenter;
    PuzzleMapTravelView travelView;
    PuzzleTrainBarView trainBarView;
    PuzzleSynergyChartView synergyChartView;
    PuzzleTradeNotebookView notebookView;
    PuzzleStationActionsPresenter stationPresenter;

    internal bool CanShowCompletion => isActiveAndEnabled && !IsOpen && restartButton && panelTitle && panelHint;

    T Resolve<T>(ref T _field) where T : Component
    {
        if (!_field)
        {
            _field = SceneServices.Find<T>(gameObject.scene);
        }
        return _field;
    }

    protected override void Awake()
    {
        var input = inventoryInput ? inventoryInput : SceneServices.Find<PuzzleInventoryInput>(gameObject.scene);
        if (input && input.ActionsAsset)
        {
            actions = Instantiate(input.ActionsAsset);
            cancel = actions.FindAction("UI/Cancel", true);
        }
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (cancel != null)
        {
            cancel.performed += OnCancel;
            cancel.Enable();
        }
    }

    protected override void OnDisable()
    {
        if (cancel != null)
        {
            cancel.performed -= OnCancel;
            cancel.Disable();
        }
        base.OnDisable();
    }

    void OnDestroy()
    {
        if (actions)
        {
            Destroy(actions);
        }
    }

    void OnCancel(InputAction.CallbackContext _context)
    {
        if (cancelConsumedFrame == Time.frameCount)
        {
            return;
        }
        cancelConsumedFrame = Time.frameCount;
        TryConsumeCancelStep();
    }

    public bool TryConsumeCancelStep()
    {
        if (showingCompletion)
        {
            return false;
        }

        // 일시정지가 열려 있으면 닫기
        if (IsOpen || paused)
        {
            Toggle();
            return true;
        }

        // 1. 이동 확인 창 -> 취소
        if (TryConsumeTravelConfirmation())
        {
            return true;
        }

        // 2. 화물·화차 드래그 중 -> 드래그 취소
        if (TryConsumeDrag())
        {
            return true;
        }

        // 3. 상성표 크게 보기 -> 닫기
        if (TryConsumeSynergyChart())
        {
            return true;
        }

        // 4. 지도 -> 닫기 (이동 연출 중에는 닫지 않고 소비만)
        if (TryConsumeMap())
        {
            return true;
        }

        // 5. 수첩 펼침 -> 접기
        if (TryConsumeTradeNotebook())
        {
            return true;
        }

        // 6. 역 하위 화면(시장·화차 상점·술집·휴식·도축·개조·정제) -> 행동 목록으로
        if (TryConsumeStationSubscreen())
        {
            return true;
        }

        // 7. 아무것도 없으면 일시정지 토글
        Toggle();
        return true;
    }

    bool TryConsumeTravelConfirmation()
    {
        var map = Resolve(ref mapPresenter);
        if (map && map.TryConsumeTravelCancel())
        {
            return true;
        }
        var travel = Resolve(ref travelView);
        if (travel && travel.TryConsumeCancel())
        {
            return true;
        }
        return false;
    }

    bool TryConsumeDrag()
    {
        var input = inventoryInput ? inventoryInput : Resolve(ref inventoryInput);
        if (input && input.TryConsumeCancel())
        {
            return true;
        }
        var trainBar = Resolve(ref trainBarView);
        if (trainBar && trainBar.TryConsumeCancel())
        {
            return true;
        }
        if (PuzzleWagonProductRow.TryConsumeCancel())
        {
            return true;
        }
        return false;
    }

    bool TryConsumeSynergyChart()
    {
        var chart = Resolve(ref synergyChartView);
        return chart && chart.TryConsumeCancel();
    }

    bool TryConsumeMap()
    {
        var map = Resolve(ref mapPresenter);
        return map && map.TryConsumeMapCancel();
    }

    bool TryConsumeTradeNotebook()
    {
        var notebook = Resolve(ref notebookView);
        return notebook && notebook.TryConsumeCancel();
    }

    bool TryConsumeStationSubscreen()
    {
        var station = Resolve(ref stationPresenter);
        return station && station.TryConsumeCancel();
    }

    internal void ShowCompletion(PuzzleRunStatus _status, long _funds)
    {
        if (_status == PuzzleRunStatus.InProgress || showingCompletion)
        {
            return;
        }
        showingCompletion = true;
        panelTitle.text = Presentation.PuzzleRunStatusText(_status);
        panelHint.text = _status == PuzzleRunStatus.Won
            ? Presentation.PuzzleClearSummary(_funds)
            : Presentation.PuzzleLossSummary(_funds);
        Open();
    }

    protected override void OnShow()
    {
        if (paused)
        {
            return;
        }
        paused = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        restoreInventoryInput = inventoryInput && inventoryInput.enabled;
        if (restoreInventoryInput)
        {
            // Disabling also cancels an active inventory drag and tooltip hover.
            inventoryInput.enabled = false;
        }
        if (EventSystem.current)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }
    }

    protected override void OnHide()
    {
        if (!paused)
        {
            return;
        }
        paused = false;
        Time.timeScale = previousTimeScale;
        if (restoreInventoryInput && inventoryInput)
        {
            inventoryInput.enabled = true;
        }
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy
                ? previousSelection : null);
        }
        previousSelection = null;
    }

    public void RestartGame()
    {
        GameEvents.RaisePuzzleRunEnded(SceneServices.For<PuzzleInventoryService>(gameObject.scene), "restart");
        Close();
        SceneManager.LoadScene(gameObject.scene.path, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Close();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
