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
    [SerializeField] Button candidateToggleButton;
    [SerializeField] TextMeshProUGUI candidateToggleLabel;
    [SerializeField] Button resetLayoutButton;
    [SerializeField] TextMeshProUGUI resetLayoutLabel;
    [SerializeField] Button relayoutToggleButton;
    [SerializeField] TextMeshProUGUI relayoutToggleLabel;

    float previousTimeScale;
    bool paused;
    bool restoreInventoryInput;
    GameObject previousSelection;
    InputActionAsset actions;
    InputAction cancel;
    InputAction back;
    InputAction rightClick;
    int cancelConsumedFrame = -1;

    FloatingWindowManager windowManager;
    RunEndView runEndView;
    PuzzleMapPresenter mapPresenter;
    PuzzleMapTravelView travelView;
    PuzzleTrainBarView trainBarView;
    PuzzleSynergyChartView synergyChartView;
    PuzzleTradeNotebookView notebookView;
    PuzzleStationActionsPresenter stationPresenter;
    EntityContextMenuPresenter linkMenu;
    MapSearchPresenter searchPresenter;
    PuzzleObjectiveView objectiveView;

    protected override bool CanOpen
    {
        get
        {
            var endView = Resolve(ref runEndView);
            return !endView || !endView.IsOpen;
        }
    }

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
            back = actions.FindAction("UI/Back", true);
            rightClick = actions.FindAction("UI/RightClick", true);
        }
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (candidateToggleButton)
        {
            candidateToggleButton.onClick.AddListener(OnCandidateToggleClicked);
        }
        if (resetLayoutButton)
        {
            resetLayoutButton.onClick.AddListener(OnResetLayoutClicked);
        }
        if (resetLayoutLabel)
        {
            resetLayoutLabel.text = Presentation.ResetWindowLayouts;
        }
        if (relayoutToggleButton)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            relayoutToggleButton.gameObject.SetActive(true);
            relayoutToggleButton.onClick.AddListener(OnRelayoutToggleClicked);
            RefreshRelayoutToggle();
#else
            relayoutToggleButton.gameObject.SetActive(false);
#endif
        }
        PuzzleLensSettings.Changed += RefreshCandidateToggle;
        RefreshCandidateToggle();
        if (cancel != null)
        {
            cancel.performed += OnCancel;
            cancel.Enable();
        }
        if (back != null)
        {
            back.performed += OnBack;
            back.Enable();
        }
        if (rightClick != null)
        {
            rightClick.performed += OnRightClickBack;
            rightClick.Enable();
        }
    }

    protected override void OnDisable()
    {
        PuzzleLensSettings.Changed -= RefreshCandidateToggle;
        if (candidateToggleButton)
        {
            candidateToggleButton.onClick.RemoveListener(OnCandidateToggleClicked);
        }
        if (resetLayoutButton)
        {
            resetLayoutButton.onClick.RemoveListener(OnResetLayoutClicked);
        }
        if (relayoutToggleButton)
        {
            relayoutToggleButton.onClick.RemoveListener(OnRelayoutToggleClicked);
        }
        if (cancel != null)
        {
            cancel.performed -= OnCancel;
            cancel.Disable();
        }
        if (back != null)
        {
            back.performed -= OnBack;
            back.Disable();
        }
        if (rightClick != null)
        {
            rightClick.performed -= OnRightClickBack;
            rightClick.Disable();
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
        if (TryClaimFrame())
        {
            TryConsumeCancelStep();
        }
    }

    // 마우스 뒤로가기 버튼은 Esc와 같은 단계별 닫기 체인을 타되, 닫을 화면이 없으면 일시정지를 열지 않는다.
    void OnBack(InputAction.CallbackContext _context)
    {
        if (TryClaimFrame())
        {
            TryConsumeBackStep();
        }
    }

    // UI/RightClick은 누름·해제 모두 performed가 온다. 누를 때만 뒤로가고 화물 드래그 중에는 회전 입력이 처리한다.
    void OnRightClickBack(InputAction.CallbackContext _context)
    {
        if (!_context.ReadValueAsButton() || PuzzleInventoryPresenter.HasActiveDrag)
        {
            return;
        }
        if (TryClaimFrame())
        {
            TryConsumeBackStep();
        }
    }

    // Esc·뒤로가기·우클릭이 같은 프레임에 눌려도 한 단계만 소비한다.
    bool TryClaimFrame()
    {
        if (cancelConsumedFrame == Time.frameCount)
        {
            return false;
        }
        cancelConsumedFrame = Time.frameCount;
        return true;
    }

    public bool TryConsumeCancelStep()
    {
        return TryConsumeStep(true);
    }

    public bool TryConsumeBackStep()
    {
        return TryConsumeStep(false);
    }

    bool TryConsumeStep(bool _pauseWhenIdle)
    {
        var endView = Resolve(ref runEndView);
        if (endView && endView.IsOpen)
        {
            return false;
        }

        // 일시정지가 열려 있으면 닫기
        if (IsOpen || paused)
        {
            Toggle();
            return true;
        }

        if (TryConsumeScreenStep())
        {
            return true;
        }

        // 7. 아무것도 없으면 일시정지 토글 (뒤로가기 버튼은 소비하지 않음)
        if (!_pauseWhenIdle)
        {
            return false;
        }
        Toggle();
        return true;
    }

    bool TryConsumeScreenStep()
    {
        var objective = Resolve(ref objectiveView);
        if (objective && objective.IsOpen)
        {
            objective.Close();
            return true;
        }

        // 지도 검색 칸 -> 비우고 닫고 포커스 해제
        var search = Resolve(ref searchPresenter);
        if (search && search.TryConsumeCancel())
        {
            return true;
        }

        // 0. 고유명사 우클릭 메뉴 -> 닫기
        var menu = Resolve(ref linkMenu);
        if (menu && menu.TryConsumeCancel())
        {
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

        // 2.5. 열린 부동 창 -> 최상위 창 닫기 (닫았으면 일시정지를 열지 않는다)
        var wm = Resolve(ref windowManager);
        if (wm && wm.TryConsumeCancel())
        {
            return true;
        }

        // 3. 수첩 상성표 탭(상성표) -> 상성표와 수첩을 함께 접기
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
        return TryConsumeStationSubscreen();
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

    protected override void OnShow()
    {
        RefreshCandidateToggle();
        if (paused)
        {
            return;
        }
        paused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
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
            EventSystem.current.SetSelectedGameObject(restartButton ? restartButton.gameObject : null);
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

    void OnResetLayoutClicked()
    {
        var wm = Resolve(ref windowManager);
        wm?.ResetAllLayouts();
    }

    void OnRelayoutToggleClicked()
    {
        RelayoutSettings.Enabled = !RelayoutSettings.Enabled;
        RefreshRelayoutToggle();
    }

    void RefreshRelayoutToggle()
    {
        if (relayoutToggleLabel)
        {
            relayoutToggleLabel.text = Presentation.RelayoutToggle(RelayoutSettings.Enabled);
        }
    }

    void OnCandidateToggleClicked()
    {
        PuzzleLensSettings.ShowCandidates = !PuzzleLensSettings.ShowCandidates;
    }

    void RefreshCandidateToggle()
    {
        if (candidateToggleLabel)
        {
            candidateToggleLabel.text = Presentation.LensCandidatesToggle(PuzzleLensSettings.ShowCandidates);
        }
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
