using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 행선지 렌즈 패널. 문구는 Presenter가 Presentation으로 만들어 넘기고, 이 View는 배치·탭 상태만 맡는다.
// 참조가 비어 있어도 예외 없이 동작하도록 모든 접근을 null 검사한다.
public sealed class PuzzleRouteLensView : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI tradeLabel;
    [SerializeField] TextMeshProUGUI placementLabel;
    [SerializeField] TextMeshProUGUI destinationLabel;
    [SerializeField] TextMeshProUGUI footnoteLabel;
    [SerializeField] Button[] tabButtons;
    [SerializeField] TextMeshProUGUI[] tabLabels;

    UnityAction[] tabCallbacks;
    PuzzleLensTab activeTab;
    bool tabsInteractable;
    bool showing;

    internal event Action<PuzzleLensTab> TabSelected;

    internal bool IsShowing => showing && Panel.activeInHierarchy;

    // root가 없으면 이 컴포넌트가 붙은 오브젝트 자체를 패널로 본다.
    GameObject Panel => root ? root : gameObject;

    void Awake()
    {
        TextLinkHandler.Attach(destinationLabel);
    }

    void OnEnable()
    {
        if (tabButtons == null)
        {
            return;
        }

        EnsureTabCallbacks();
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i])
            {
                tabButtons[i].onClick.AddListener(tabCallbacks[i]);
            }
        }
    }

    void OnDisable()
    {
        if (tabButtons == null || tabCallbacks == null)
        {
            return;
        }

        int count = Mathf.Min(tabButtons.Length, tabCallbacks.Length);
        for (int i = 0; i < count; i++)
        {
            if (tabButtons[i])
            {
                tabButtons[i].onClick.RemoveListener(tabCallbacks[i]);
            }
        }
    }

    internal void Show(string _title, string _trade, string _placement, string _destination, string _footnote,
        PuzzleLensTab _activeTab, bool _tabsInteractable)
    {
        SetText(titleLabel, _title);
        SetText(tradeLabel, _trade);
        SetText(placementLabel, _placement);
        SetText(destinationLabel, _destination);
        SetText(footnoteLabel, _footnote);
        activeTab = _activeTab;
        tabsInteractable = _tabsInteractable;
        RenderTabs();
        showing = true;
        Panel.SetActive(true);
    }

    // 드래그 중 매 프레임 바뀌는 자리 문구만 갈아 끼운다.
    internal void SetPlacement(string _placement)
    {
        SetText(placementLabel, _placement);
    }

    internal void SetDestination(string _destination, string _footnote, PuzzleLensTab _activeTab)
    {
        SetText(destinationLabel, _destination);
        SetText(footnoteLabel, _footnote);
        activeTab = _activeTab;
        RenderTabs();
    }

    internal void Hide()
    {
        showing = false;
        Panel.SetActive(false);
    }

    void EnsureTabCallbacks()
    {
        int count = tabButtons.Length;
        if (tabCallbacks != null && tabCallbacks.Length == count)
        {
            return;
        }

        tabCallbacks = new UnityAction[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            tabCallbacks[i] = () => OnTabClicked(index);
        }
    }

    void OnTabClicked(int _index)
    {
        if (Enum.IsDefined(typeof(PuzzleLensTab), _index))
        {
            TabSelected?.Invoke((PuzzleLensTab)_index);
        }
    }

    // 선택한 탭은 누를 수 없고 배경·글자색으로 표시한다. 드래그 중에는 모든 탭을 잠근다.
    void RenderTabs()
    {
        int count = tabButtons != null ? tabButtons.Length : 0;
        for (int i = 0; i < count; i++)
        {
            bool selected = i == (int)activeTab;
            var button = tabButtons[i];
            if (button)
            {
                button.interactable = tabsInteractable && !selected;
                if (button.targetGraphic)
                {
                    button.targetGraphic.color = selected ? Presentation.RumorLogTabSelected : Presentation.RumorLogTabNormal;
                }
            }

            if (tabLabels == null || i >= tabLabels.Length || !tabLabels[i] || !Enum.IsDefined(typeof(PuzzleLensTab), i))
            {
                continue;
            }
            tabLabels[i].text = Presentation.RouteLensTab((PuzzleLensTab)i);
            tabLabels[i].color = selected ? Presentation.RumorLogTabSelectedText : Presentation.RumorLogTabNormalText;
        }
    }

    static void SetText(TMP_Text _label, string _text)
    {
        if (_label)
        {
            _label.text = _text ?? string.Empty;
        }
    }
}
