using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

public enum PuzzleNotebookTab
{
    Rumors,
    Quests
}

// 사전 배치한 스크롤 본문·탭·접기 버튼만 제어한다. 기록 데이터는 Presenter가 전달한다.
public sealed class PuzzleTradeNotebookView : PanelBase
{
    [SerializeField] Button toggleButton;
    [SerializeField] Button collapseButton;
    [SerializeField] TextMeshProUGUI toggleLabel;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI countLabel;
    [SerializeField] TextMeshProUGUI collapseLabel;
    [SerializeField] TextMeshProUGUI logText;
    [SerializeField] ScrollRect scroll;
    [SerializeField] Button rumorTabButton;
    [SerializeField] TextMeshProUGUI rumorTabLabel;
    [SerializeField] Button questTabButton;
    [SerializeField] TextMeshProUGUI questTabLabel;
    bool scrollPending;

    internal event Action<PuzzleNotebookTab> TabSelected;

    public void OpenTab(PuzzleNotebookTab _tab)
    {
        TabSelected?.Invoke(_tab);
        SetOpen(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        toggleButton.onClick.AddListener(ToggleLog);
        collapseButton.onClick.AddListener(Close);
        rumorTabButton.onClick.AddListener(SelectRumors);
        questTabButton.onClick.AddListener(SelectQuests);
    }

    protected override void OnDisable()
    {
        toggleButton.onClick.RemoveListener(ToggleLog);
        collapseButton.onClick.RemoveListener(Close);
        rumorTabButton.onClick.RemoveListener(SelectRumors);
        questTabButton.onClick.RemoveListener(SelectQuests);
        base.OnDisable();
    }

    public bool TryConsumeCancel()
    {
        if (IsOpen)
        {
            Close();
            return true;
        }
        return false;
    }

    void ToggleLog()
    {
        Toggle();
    }

    void SelectRumors()
    {
        TabSelected?.Invoke(PuzzleNotebookTab.Rumors);
    }

    void SelectQuests()
    {
        TabSelected?.Invoke(PuzzleNotebookTab.Quests);
    }

    internal void Render(PuzzleNotebookTab _tab, string _text, int _rumorCount, int _questCount)
    {
        if (!logText || !scroll || !toggleButton || !collapseButton || !toggleLabel ||
            !titleLabel || !countLabel || !collapseLabel || !rumorTabButton || !rumorTabLabel ||
            !questTabButton || !questTabLabel)
        {
            throw new InvalidOperationException("수첩 패널의 본문·스크롤·탭·접기 버튼을 연결하세요.");
        }
        logText.text = _text;
        titleLabel.text = Presentation.RumorLogTitle;
        countLabel.text = Presentation.RumorLogCount(_tab == PuzzleNotebookTab.Rumors ? _rumorCount : _questCount);
        toggleLabel.text = Presentation.RumorLogCollapsed(_rumorCount, _questCount);
        collapseLabel.text = Presentation.RumorLogCollapse;
        rumorTabLabel.text = Presentation.RumorLogRumorTab;
        questTabLabel.text = Presentation.RumorLogQuestTab;
        // 선택한 탭은 눌러도 바뀌지 않으므로 비활성으로 표시한다.
        rumorTabButton.interactable = _tab != PuzzleNotebookTab.Rumors;
        questTabButton.interactable = _tab != PuzzleNotebookTab.Quests;
        scrollPending = IsOpen;
    }

    protected override void OnShow()
    {
        toggleButton.gameObject.SetActive(false);
        scrollPending = true;
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(collapseButton.gameObject);
        }
    }

    protected override void OnHide()
    {
        scrollPending = false;
        toggleButton.gameObject.SetActive(true);
        if (Active && gameObject.activeInHierarchy && EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(toggleButton.gameObject);
        }
    }

    void LateUpdate()
    {
        if (!scrollPending || !IsOpen)
        {
            return;
        }
        scrollPending = false;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.verticalNormalizedPosition = 0f;
    }
}
