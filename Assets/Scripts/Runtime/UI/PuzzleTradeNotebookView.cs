using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

// 사전 배치한 스크롤 본문과 접기 버튼만 제어한다. 기록 데이터는 Presenter가 전달한다.
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
    bool scrollPending;

    protected override void OnEnable()
    {
        base.OnEnable();
        toggleButton.onClick.AddListener(ToggleLog);
        collapseButton.onClick.AddListener(Close);
    }

    protected override void OnDisable()
    {
        toggleButton.onClick.RemoveListener(ToggleLog);
        collapseButton.onClick.RemoveListener(Close);
        base.OnDisable();
    }

    void ToggleLog()
    {
        Toggle();
    }

    internal void Render(string _text, int _count)
    {
        if (!logText || !scroll || !toggleButton || !collapseButton || !toggleLabel ||
            !titleLabel || !countLabel || !collapseLabel)
        {
            throw new InvalidOperationException("소문 로그 패널의 본문·스크롤·접기 버튼을 연결하세요.");
        }
        logText.text = _text;
        titleLabel.text = Presentation.RumorLogTitle;
        countLabel.text = Presentation.RumorLogCount(_count);
        toggleLabel.text = Presentation.RumorLogCollapsed(_count);
        collapseLabel.text = Presentation.RumorLogCollapse;
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
