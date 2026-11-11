using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleCityView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI cityName;
    [SerializeField] TextMeshProUGUI timeLabel;
    [SerializeField] TextMeshProUGUI funds;
    [SerializeField] TextMeshProUGUI tradeStatus;
    [SerializeField] Button clearButton;
    [SerializeField] TextMeshProUGUI clearLabel;

    [SerializeField] CanvasGroup marketPage;
    [SerializeField] CanvasGroup townMainPage;
    [SerializeField] CanvasGroup tavernPage;
    [SerializeField] Button townButton;
    [SerializeField] Button marketButton;
    [SerializeField] Button tavernButton;
    [SerializeField] Button talkButton;
    [SerializeField] Button backButton;
    [SerializeField] TextMeshProUGUI townTitle;
    [SerializeField] TextMeshProUGUI tavernTitle;
    [SerializeField] TextMeshProUGUI rumorLabel;

    enum Page { Market, Town, Tavern }

    Page page;
    bool mapOpen;
    Func<string> getRumor;
    string heardRumor;
    string unreadRumorText;

    internal bool IsTownOpen => page != Page.Market;

    internal void Initialize(long _clearCost)
    {
        if (!timeLabel || !timeLabel.font || !cityName || !cityName.font ||
            !funds || !funds.font || !tradeStatus || !tradeStatus.font ||
            !clearButton || !clearButton.targetGraphic || !clearLabel || !clearLabel.font)
        {
            throw new InvalidOperationException("도시 View에 도시 이름, 시간, 자금과 거래 표시 참조가 필요합니다.");
        }

        clearLabel.text = Presentation.PuzzleClearButton(_clearCost);
    }

    internal void InitializeTown(Func<string> _getRumor)
    {
        if (!marketPage || !townMainPage || !tavernPage || !townButton || !marketButton ||
            !tavernButton || !talkButton || !backButton || !townTitle || !tavernTitle || !rumorLabel)
        {
            throw new InvalidOperationException("도시 View에 마을 메인과 술집의 씬 참조가 필요합니다.");
        }

        if (getRumor == null)
        {
            unreadRumorText = rumorLabel.text;
            townButton.onClick.AddListener(OpenTown);
            marketButton.onClick.AddListener(OpenMarket);
            tavernButton.onClick.AddListener(OpenTavern);
            talkButton.onClick.AddListener(HearRumor);
            backButton.onClick.AddListener(OpenTown);
        }
        getRumor = _getRumor;
        mapOpen = false;
        ShowPage(Page.Market);
    }

    internal void SetTown(string _name, bool _available)
    {
        townTitle.text = _name;
        tavernTitle.text = $"{_name} 술집";
        heardRumor = null;
        rumorLabel.text = unreadRumorText;
        townButton.gameObject.SetActive(_available);
        if (!_available)
        {
            ShowPage(Page.Market);
        }
    }

    internal void OpenTown()
    {
        ShowPage(Page.Town);
        Select(marketButton);
    }

    internal void SetTownInteraction(bool _enabled)
    {
        mapOpen = !_enabled;
        UpdatePageInteraction();
    }

    void OpenMarket()
    {
        ShowPage(Page.Market);
        Select(townButton);
    }

    void OpenTavern()
    {
        ShowPage(Page.Tavern);
        Select(talkButton);
    }

    void ShowPage(Page _page)
    {
        page = _page;
        marketPage.alpha = page == Page.Market ? 1f : 0f;
        townMainPage.gameObject.SetActive(page == Page.Town);
        tavernPage.gameObject.SetActive(page == Page.Tavern);
        UpdatePageInteraction();
    }

    void UpdatePageInteraction()
    {
        marketPage.interactable = marketPage.blocksRaycasts = !mapOpen && page == Page.Market;
        townMainPage.interactable = townMainPage.blocksRaycasts = !mapOpen && page == Page.Town;
        tavernPage.interactable = tavernPage.blocksRaycasts = !mapOpen && page == Page.Tavern;
    }

    #region 술집 대화 표시

    // 이번 방문의 첫 대화에서 소식을 받아 저장하고, 다시 말을 걸면 저장한 소식을 표시한다.
    void HearRumor()
    {
        // 1. 이번 방문에서 아직 소식을 듣지 않았다면 새 소식을 받아 저장한다.
        heardRumor ??= getRumor();

        // 2. 저장된 소식을 대화 영역에 표시한다.
        rumorLabel.text = heardRumor;
    }

    #endregion

    static void Select(Button _button)
    {
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(_button.gameObject);
        }
    }

    internal void ShowCity(string _name)
    {
        cityName.text = _name;
    }

    internal void ShowTime(PuzzleTimeManager _time)
    {
        timeLabel.text = Presentation.PuzzleTime(_time);
    }

    internal void ShowFunds(long _funds, bool _canClear)
    {
        funds.text = Presentation.PuzzleFunds(_funds);
        clearButton.interactable = _canClear;
    }

    internal void ShowTradeStatus(string _status)
    {
        tradeStatus.text = _status;
    }
}
