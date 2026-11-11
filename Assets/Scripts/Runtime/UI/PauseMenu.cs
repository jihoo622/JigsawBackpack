using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    bool travelPanelOpen;
    int travelPanelChangedFrame = -1;

    internal bool CanShowCompletion => isActiveAndEnabled && !IsOpen && restartButton && panelTitle && panelHint;

    protected override void Awake()
    {
        actions = Instantiate(inventoryInput.ActionsAsset);
        cancel = actions.FindAction("UI/Cancel", true);
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        cancel.performed += OnCancel;
        cancel.Enable();
        GameEvents.PanelVisibility += OnPanelVisibility;
    }

    protected override void OnDisable()
    {
        GameEvents.PanelVisibility -= OnPanelVisibility;
        cancel.performed -= OnCancel;
        cancel.Disable();
        base.OnDisable();
    }

    void OnDestroy()
    {
        if (actions)
        {
            Destroy(actions);
        }
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == Presentation.PUZZLE_TRAVEL_PANEL_NAME)
        {
            travelPanelOpen = _info.open;
            travelPanelChangedFrame = Time.frameCount;
        }
    }

    void OnCancel(InputAction.CallbackContext _context)
    {
        if (!showingCompletion && !travelPanelOpen && travelPanelChangedFrame != Time.frameCount)
        {
            if (inventoryInput && inventoryInput.TryConsumeCancel())
            {
                return;
            }
            Toggle();
        }
    }

    internal void ShowCompletion(long _funds)
    {
        showingCompletion = true;
        panelTitle.text = Presentation.PuzzleClearTitle;
        panelHint.text = Presentation.PuzzleClearSummary(_funds);
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
