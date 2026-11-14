using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

public enum PuzzleNotebookTab
{
    Rumors,
    Quests,
    Proficiency
}

// 사전 배치한 스크롤 본문·숙련 패널·탭·접기 버튼만 제어한다. 기록 데이터는 Presenter가 전달한다.
// 머리 줄(제목·개수·오른쪽 위 접기 버튼) 아래에 소문/의뢰/숙련 탭이 3등분 폭으로 있고, 선택한 탭은 배경·글자색·밑줄로 구분한다.
// 숙련 탭에서는 스크롤 본문 대신 사전 배치한 숙련 패널을 켜고, 머리 줄 개수 표시는 숨긴다(평판은 패널 요약에 있다).
// 접혔을 때는 화면 오른쪽 여백의 세로 '정보' 탭만 남고, 새 소식이 있으면 탭 위쪽에 작은 배지를 켠다.
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
    [SerializeField] Image rumorTabMark;
    [SerializeField] Image questTabMark;
    [SerializeField] Button proficiencyTabButton;
    [SerializeField] TextMeshProUGUI proficiencyTabLabel;
    [SerializeField] Image proficiencyTabMark;
    [SerializeField] TrainventoryProficiencyPanelView proficiencyPanel;
    [SerializeField] Image newBadge;
    [SerializeField] TextMeshProUGUI newBadgeLabel;
    bool scrollPending;

    internal event Action<PuzzleNotebookTab> TabSelected;
    internal event Action Shown;

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
        proficiencyTabButton.onClick.AddListener(SelectProficiency);
    }

    protected override void OnDisable()
    {
        toggleButton.onClick.RemoveListener(ToggleLog);
        collapseButton.onClick.RemoveListener(Close);
        rumorTabButton.onClick.RemoveListener(SelectRumors);
        questTabButton.onClick.RemoveListener(SelectQuests);
        proficiencyTabButton.onClick.RemoveListener(SelectProficiency);
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

    void SelectProficiency()
    {
        TabSelected?.Invoke(PuzzleNotebookTab.Proficiency);
    }

    // 숙련 탭 패널 내용. 숙련 탭일 때 Render 직후 같은 갱신에서 호출한다.
    internal void RenderProficiency(int _reputation, int _maxActiveQuests, bool _hasNextStage, int _nextReputation,
        IReadOnlyList<TrainventoryProficiencyStanding> _standings)
    {
        proficiencyPanel.Render(_reputation, _maxActiveQuests, _hasNextStage, _nextReputation, _standings);
    }

    // _unseenCount: 수첩을 마지막으로 연 뒤 생긴 소문·의뢰 변화·숙련 레벨 상승 수. 0이면 접힌 탭의 배지를 숨긴다.
    internal void Render(PuzzleNotebookTab _tab, string _text, int _rumorCount, int _questCount, int _unseenCount)
    {
        if (!logText || !scroll || !toggleButton || !collapseButton || !toggleLabel ||
            !titleLabel || !countLabel || !collapseLabel || !rumorTabButton || !rumorTabLabel ||
            !questTabButton || !questTabLabel || !rumorTabMark || !questTabMark || !proficiencyTabButton ||
            !proficiencyTabLabel || !proficiencyTabMark || !proficiencyPanel || !newBadge || !newBadgeLabel)
        {
            throw new InvalidOperationException("수첩 패널의 본문·스크롤·숙련 패널·탭·탭 밑줄·접기 버튼·새 소식 배지를 연결하세요.");
        }
        bool proficiency = _tab == PuzzleNotebookTab.Proficiency;
        scroll.gameObject.SetActive(!proficiency);
        proficiencyPanel.gameObject.SetActive(proficiency);
        logText.text = proficiency ? string.Empty : _text;
        titleLabel.text = Presentation.RumorLogTitle;
        countLabel.gameObject.SetActive(!proficiency);
        countLabel.text = Presentation.RumorLogCount(_tab == PuzzleNotebookTab.Rumors ? _rumorCount : _questCount);
        toggleLabel.text = Presentation.RumorLogCollapsed;
        newBadge.color = Presentation.RumorLogBadgeColor;
        newBadgeLabel.color = Presentation.RumorLogBadgeText;
        newBadgeLabel.text = Presentation.RumorLogBadge(_unseenCount);
        newBadge.gameObject.SetActive(_unseenCount > 0);
        collapseLabel.text = Presentation.RumorLogCollapse;
        rumorTabLabel.text = Presentation.RumorLogRumorTab;
        questTabLabel.text = Presentation.RumorLogQuestTab;
        proficiencyTabLabel.text = Presentation.RumorLogProficiencyTab;
        ApplyTabState(rumorTabButton, rumorTabLabel, rumorTabMark, _tab == PuzzleNotebookTab.Rumors);
        ApplyTabState(questTabButton, questTabLabel, questTabMark, _tab == PuzzleNotebookTab.Quests);
        ApplyTabState(proficiencyTabButton, proficiencyTabLabel, proficiencyTabMark, proficiency);
        scrollPending = IsOpen && !proficiency;
    }

    // 선택한 탭은 눌러도 바뀌지 않으므로 입력을 막고, 색 틴트 대신 배경·글자색·밑줄로 선택 상태를 보인다.
    static void ApplyTabState(Button _button, TextMeshProUGUI _label, Image _mark, bool _selected)
    {
        _button.interactable = !_selected;
        if (_button.targetGraphic)
        {
            _button.targetGraphic.color = _selected ? Presentation.RumorLogTabSelected : Presentation.RumorLogTabNormal;
        }
        _label.color = _selected ? Presentation.RumorLogTabSelectedText : Presentation.RumorLogTabNormalText;
        _mark.color = Presentation.PuzzleWagonSelectedBorder;
        _mark.gameObject.SetActive(_selected);
    }

    protected override void OnShow()
    {
        toggleButton.gameObject.SetActive(false);
        scrollPending = true;
        Shown?.Invoke();
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
        if (!scrollPending || !IsOpen || !scroll.gameObject.activeInHierarchy)
        {
            return;
        }
        scrollPending = false;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.verticalNormalizedPosition = 0f;
    }
}
