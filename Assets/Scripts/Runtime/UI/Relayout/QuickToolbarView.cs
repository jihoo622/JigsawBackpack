using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// S2: 좌측 상단 빠른 도구줄(26, 82, 510, 38). 수첩(N), 노선도(M), 배경만 보기, 창 배치 초기화, 단축키 안내.
[DisallowMultipleComponent]
public sealed class QuickToolbarView : MonoBehaviour
{
    [SerializeField] Button notebookButton;
    [SerializeField] TextMeshProUGUI notebookLabel;
    [SerializeField] Button mapButton;
    [SerializeField] TextMeshProUGUI mapLabel;
    [SerializeField] Button backgroundOnlyButton;
    [SerializeField] TextMeshProUGUI backgroundOnlyLabel;
    [SerializeField] Button resetLayoutButton;
    [SerializeField] TextMeshProUGUI resetLayoutLabel;
    [SerializeField] TextMeshProUGUI shortcutHintLabel;
    [SerializeField] PuzzleTradeNotebookView notebookView;
    [SerializeField] FloatingWindowManager windowManager;
    [SerializeField] PuzzleInventoryPresenter inventoryPresenter;

    bool textInputFocused;

    void OnEnable()
    {
        if (notebookButton) notebookButton.onClick.AddListener(OnNotebookClicked);
        if (mapButton) mapButton.onClick.AddListener(OnMapClicked);
        if (backgroundOnlyButton) backgroundOnlyButton.onClick.AddListener(OnBackgroundOnlyClicked);
        if (resetLayoutButton) resetLayoutButton.onClick.AddListener(OnResetLayoutClicked);
        GameEvents.TextInputFocusChanged += OnTextInputFocusChanged;
        RefreshLabels();
    }

    void OnDisable()
    {
        if (notebookButton) notebookButton.onClick.RemoveListener(OnNotebookClicked);
        if (mapButton) mapButton.onClick.RemoveListener(OnMapClicked);
        if (backgroundOnlyButton) backgroundOnlyButton.onClick.RemoveListener(OnBackgroundOnlyClicked);
        if (resetLayoutButton) resetLayoutButton.onClick.RemoveListener(OnResetLayoutClicked);
        GameEvents.TextInputFocusChanged -= OnTextInputFocusChanged;
        textInputFocused = false;
    }

    void Update()
    {
        if (!RelayoutSettings.Enabled || textInputFocused)
        {
            return;
        }
        var inv = inventoryPresenter ? inventoryPresenter : SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        if (!inv || inv.AreShortcutsBlocked())
        {
            return;
        }
        var kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }
        if (kb.nKey.wasPressedThisFrame)
        {
            OnNotebookClicked();
        }
        else if (kb.mKey.wasPressedThisFrame)
        {
            OnMapClicked();
        }
    }

    void OnTextInputFocusChanged(GameEvents.TextInputFocusInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle)
        {
            textInputFocused = _info.focused;
        }
    }

    void RefreshLabels()
    {
        if (notebookLabel) notebookLabel.text = Presentation.QuickToolbarNotebook;
        if (mapLabel) mapLabel.text = Presentation.QuickToolbarMap;
        if (backgroundOnlyLabel) backgroundOnlyLabel.text = Presentation.QuickToolbarBackgroundOnly;
        if (resetLayoutLabel) resetLayoutLabel.text = Presentation.ResetWindowLayouts;
        if (shortcutHintLabel) shortcutHintLabel.text = Presentation.QuickToolbarShortcutHint;
    }

    void OnNotebookClicked()
    {
        var nb = notebookView ? notebookView : SceneServices.Find<PuzzleTradeNotebookView>(gameObject.scene);
        nb?.Toggle();
    }

    void OnMapClicked()
    {
        GameEvents.RaiseMapToggleRequested(gameObject.scene.handle);
    }

    void OnBackgroundOnlyClicked()
    {
        var wm = windowManager ? windowManager : SceneServices.Find<FloatingWindowManager>(gameObject.scene);
        wm?.ToggleBackgroundOnly();
    }

    void OnResetLayoutClicked()
    {
        var wm = windowManager ? windowManager : SceneServices.Find<FloatingWindowManager>(gameObject.scene);
        wm?.ResetAllLayouts();
    }
}
