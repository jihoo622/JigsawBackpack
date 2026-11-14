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
    Proficiency,
    Synergy
}

// 사전 배치한 스크롤 본문·숙련 패널·탭·접기 버튼만 제어한다. 기록 데이터는 Presenter가 전달한다.
// 머리 줄(제목·개수·오른쪽 위 접기 버튼) 아래에 소문/의뢰/숙련 탭이 3등분 폭으로 있고, 선택한 탭은 배경·글자색·밑줄로 구분한다.
// 숙련 탭에서는 스크롤 본문 대신 사전 배치한 숙련 패널을 켜고, 머리 줄 개수 표시는 숨긴다(평판은 패널 요약에 있다).
// 여는 곳은 두 개다: 하단 고정 UI 지도 버튼 아래의 '정보' 버튼(FixedBottomBar 프리팹, GameCanvas에서 연결)과
// 화면 오른쪽 가장자리의 세로 손잡이(이 프리팹, 위아래로 끌어 옮길 수 있음). 둘 다 펼친 동안에도 보이며 누르면 접고,
// 새 소식이 있으면 둘 다 작은 배지를 켠다.
// 상성표 탭을 고르면 패널이 넓은 폭(widePanelLeft·widePanelWidth, 프리팹 값)으로 펼쳐지고 본문은 비운다.
// 표 자체는 상성표 패널(별도 Canvas, 수첩 위)이 그 자리에 그린다.
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
    [SerializeField] Button synergyTabButton;
    [SerializeField] TextMeshProUGUI synergyTabLabel;
    [SerializeField] Image synergyTabMark;
    [SerializeField] RectTransform panel;
    [SerializeField] float widePanelLeft = 40f;
    [SerializeField] float widePanelWidth = 1840f;
    [SerializeField] TrainventoryProficiencyPanelView proficiencyPanel;
    [SerializeField] Image newBadge;
    [SerializeField] TextMeshProUGUI newBadgeLabel;
    [SerializeField] Button handleButton;
    [SerializeField] TextMeshProUGUI handleLabel;
    [SerializeField] Image handleBadge;
    [SerializeField] TextMeshProUGUI handleBadgeLabel;
    bool scrollPending;
    bool normalCaptured;
    float normalPanelLeft;
    float normalPanelWidth;

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
        handleButton.onClick.AddListener(ToggleLog);
        collapseButton.onClick.AddListener(Close);
        rumorTabButton.onClick.AddListener(SelectRumors);
        questTabButton.onClick.AddListener(SelectQuests);
        proficiencyTabButton.onClick.AddListener(SelectProficiency);
        synergyTabButton.onClick.AddListener(SelectSynergy);
    }

    protected override void OnDisable()
    {
        toggleButton.onClick.RemoveListener(ToggleLog);
        handleButton.onClick.RemoveListener(ToggleLog);
        collapseButton.onClick.RemoveListener(Close);
        rumorTabButton.onClick.RemoveListener(SelectRumors);
        questTabButton.onClick.RemoveListener(SelectQuests);
        proficiencyTabButton.onClick.RemoveListener(SelectProficiency);
        synergyTabButton.onClick.RemoveListener(SelectSynergy);
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

    void SelectSynergy()
    {
        TabSelected?.Invoke(PuzzleNotebookTab.Synergy);
    }

    // 상성표 탭만 넓은 폭. 좁은 폭은 처음 배치(프리팹)를 기억해 되돌린다.
    void ApplyPanelWidth(bool _wide)
    {
        if (!normalCaptured)
        {
            normalPanelLeft = panel.anchoredPosition.x - panel.pivot.x * panel.sizeDelta.x;
            normalPanelWidth = panel.sizeDelta.x;
            normalCaptured = true;
        }
        float width = _wide ? widePanelWidth : normalPanelWidth;
        float left = _wide ? widePanelLeft : normalPanelLeft;
        panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);
        panel.anchoredPosition = new Vector2(left + panel.pivot.x * width, panel.anchoredPosition.y);
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
            !proficiencyTabLabel || !proficiencyTabMark || !proficiencyPanel || !newBadge || !newBadgeLabel ||
            !handleButton || !handleLabel || !handleBadge || !handleBadgeLabel ||
            !synergyTabButton || !synergyTabLabel || !synergyTabMark || !panel)
        {
            throw new InvalidOperationException("수첩 패널의 본문·스크롤·숙련 패널·탭·탭 밑줄·접기 버튼·정보 버튼·가장자리 손잡이·새 소식 배지를 연결하세요.");
        }
        bool proficiency = _tab == PuzzleNotebookTab.Proficiency;
        bool synergy = _tab == PuzzleNotebookTab.Synergy;
        ApplyPanelWidth(synergy);
        scroll.gameObject.SetActive(!proficiency && !synergy);
        proficiencyPanel.gameObject.SetActive(proficiency);
        logText.text = proficiency || synergy ? string.Empty : _text;
        titleLabel.text = Presentation.RumorLogTitle;
        countLabel.gameObject.SetActive(!proficiency && !synergy);
        countLabel.text = Presentation.RumorLogCount(_tab == PuzzleNotebookTab.Rumors ? _rumorCount : _questCount);
        toggleLabel.text = Presentation.RumorLogCollapsed;
        // 하단 상성표 버튼과 같은 색(Presentation 단일 출처)
        toggleLabel.color = Presentation.PuzzleHeading;
        if (toggleButton.targetGraphic)
        {
            toggleButton.targetGraphic.color = Presentation.PuzzleBackground;
        }
        handleLabel.text = Presentation.RumorLogHandle;
        RenderBadge(newBadge, newBadgeLabel, _unseenCount);
        RenderBadge(handleBadge, handleBadgeLabel, _unseenCount);
        collapseLabel.text = Presentation.RumorLogCollapse;
        rumorTabLabel.text = Presentation.RumorLogRumorTab;
        questTabLabel.text = Presentation.RumorLogQuestTab;
        proficiencyTabLabel.text = Presentation.RumorLogProficiencyTab;
        synergyTabLabel.text = Presentation.RumorLogSynergyTab;
        ApplyTabState(rumorTabButton, rumorTabLabel, rumorTabMark, _tab == PuzzleNotebookTab.Rumors);
        ApplyTabState(questTabButton, questTabLabel, questTabMark, _tab == PuzzleNotebookTab.Quests);
        ApplyTabState(proficiencyTabButton, proficiencyTabLabel, proficiencyTabMark, proficiency);
        ApplyTabState(synergyTabButton, synergyTabLabel, synergyTabMark, synergy);
        scrollPending = IsOpen && !proficiency && !synergy;
    }

    static void RenderBadge(Image _badge, TextMeshProUGUI _label, int _count)
    {
        _badge.color = Presentation.RumorLogBadgeColor;
        _label.color = Presentation.RumorLogBadgeText;
        _label.text = Presentation.RumorLogBadge(_count);
        _badge.gameObject.SetActive(_count > 0);
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
