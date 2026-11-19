using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S6: 술집 부동 창. 무료 소문 듣기 + 9×5 의뢰 게시판(화물 모양 조각 드래그로 수락, 완료 화물 드롭으로 인도).
[DisallowMultipleComponent]
public sealed class TavernWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button rumorButton;
    [SerializeField] TextMeshProUGUI rumorButtonLabel;
    [SerializeField] TextMeshProUGUI rumorText;
    [SerializeField] PuzzleInventoryView questBoardView;
    [SerializeField] TextMeshProUGUI hintText;

    string heardRumor;
    int overflowCount;

    public FloatingWindow FloatingWindow => floatingWindow;
    public PuzzleInventoryView QuestBoardView => questBoardView;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    public event Action RumorRequested;
    public event Action Opened;

    void OnEnable()
    {
        if (rumorButton)
        {
            rumorButton.onClick.AddListener(OnRumorClicked);
        }
        GameEvents.PuzzleRunSettled += OnRunChanged;
        GameEvents.PuzzleRunEnded += OnRunChanged;
        GameEvents.PuzzleRunContinued += OnRunChanged;
        RefreshLabels();
    }

    void OnDisable()
    {
        if (rumorButton)
        {
            rumorButton.onClick.RemoveListener(OnRumorClicked);
        }
        GameEvents.PuzzleRunSettled -= OnRunChanged;
        GameEvents.PuzzleRunEnded -= OnRunChanged;
        GameEvents.PuzzleRunContinued -= OnRunChanged;
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
        if (_open)
        {
            RefreshLabels();
            Opened?.Invoke();
        }
    }

    internal void ResetCity(string _cityName)
    {
        heardRumor = null;
        if (titleText)
        {
            titleText.text = Presentation.TavernWindowTitle(_cityName ?? string.Empty);
        }
        if (rumorText)
        {
            rumorText.text = Presentation.TavernUnreadRumor;
        }
        if (rumorButton)
        {
            rumorButton.interactable = IsRunInProgress();
        }
        RefreshLabels();
    }

    internal void SetRumor(string _message)
    {
        heardRumor = _message;
        if (rumorText)
        {
            rumorText.text = string.IsNullOrEmpty(_message) ? Presentation.TavernNoRumor : _message;
        }
    }

    internal void SetOverflowCount(int _count)
    {
        overflowCount = Mathf.Max(0, _count);
        if (hintText)
        {
            hintText.text = overflowCount > 0
                ? Presentation.QuestMoreOffers(overflowCount)
                : Presentation.TavernWindowBoardHint;
        }
    }

    internal void SetHint(string _hint)
    {
        if (hintText)
        {
            hintText.text = !string.IsNullOrEmpty(_hint)
                ? _hint
                : (overflowCount > 0 ? Presentation.QuestMoreOffers(overflowCount) : Presentation.TavernWindowBoardHint);
        }
    }

    bool IsRunInProgress()
    {
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        return session != null && session.RunStatus == PuzzleRunStatus.InProgress;
    }

    void RefreshLabels()
    {
        if (rumorButton)
        {
            rumorButton.interactable = IsRunInProgress();
        }
        if (rumorButtonLabel)
        {
            rumorButtonLabel.text = Presentation.TavernWindowRumorButton;
        }
        if (hintText)
        {
            hintText.text = overflowCount > 0
                ? Presentation.QuestMoreOffers(overflowCount)
                : Presentation.TavernWindowBoardHint;
        }
        if (rumorText && string.IsNullOrEmpty(heardRumor))
        {
            rumorText.text = Presentation.TavernUnreadRumor;
        }
    }

    void OnRunChanged(GameEvents.PuzzleRunInfo _info) => RefreshLabels();

    void OnRumorClicked()
    {
        if (!IsRunInProgress())
        {
            return;
        }
        RumorRequested?.Invoke();
    }
}
