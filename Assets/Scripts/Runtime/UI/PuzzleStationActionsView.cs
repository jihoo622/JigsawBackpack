using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleStationActionsView : PanelBase
{
    [SerializeField] GameObject actionsList;
    [SerializeField] GameObject marketContent;
    [SerializeField] GameObject restContent;
    [SerializeField] RectTransform tavernContent;
    [SerializeField] TextMeshProUGUI title;
    [SerializeField] TextMeshProUGUI marketLabel;
    [SerializeField] TextMeshProUGUI tavernLabel;
    [SerializeField] TextMeshProUGUI restLabel;
    [SerializeField] TextMeshProUGUI travelLabel;
    [SerializeField] TextMeshProUGUI backLabel;
    [SerializeField] TextMeshProUGUI restDurationLabel;
    [SerializeField] TextMeshProUGUI restHint;
    [SerializeField] Button marketButton;
    [SerializeField] Button tavernButton;
    [SerializeField] Button restButton;
    [SerializeField] Button travelButton;
    [SerializeField] Button backButton;
    [SerializeField] Button restDurationButton;
    [SerializeField] CanvasGroup interaction;

    internal event Action MarketRequested;
    internal event Action TavernRequested;
    internal event Action RestRequested;
    internal event Action TravelRequested;
    internal event Action RestTimeRequested;
    public RectTransform TavernContent => tavernContent;

    protected override void Awake()
    {
        tavernLabel.text = Presentation.StationTavern;
        restLabel.text = Presentation.StationRest;
        travelLabel.text = Presentation.StationTravel;
        backLabel.text = Presentation.StationBack;
        restHint.text = Presentation.StationRestHint;
        tavernContent.gameObject.SetActive(false);
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        marketButton.onClick.AddListener(OnMarket);
        tavernButton.onClick.AddListener(OnTavern);
        restButton.onClick.AddListener(OnRest);
        travelButton.onClick.AddListener(OnTravel);
        backButton.onClick.AddListener(ShowHome);
        restDurationButton.onClick.AddListener(OnRestTime);
    }

    protected override void OnDisable()
    {
        marketButton.onClick.RemoveListener(OnMarket);
        tavernButton.onClick.RemoveListener(OnTavern);
        restButton.onClick.RemoveListener(OnRest);
        travelButton.onClick.RemoveListener(OnTravel);
        backButton.onClick.RemoveListener(ShowHome);
        restDurationButton.onClick.RemoveListener(OnRestTime);
        base.OnDisable();
    }

    internal void Refresh(bool _marketOpen, bool _inProgress, long _restMinutes)
    {
        marketLabel.text = _marketOpen ? Presentation.StationMarket : Presentation.StationMarketClosed;
        marketButton.interactable = _marketOpen;
        tavernButton.interactable = restButton.interactable = travelButton.interactable = restDurationButton.interactable = _inProgress;
        restDurationLabel.text = Presentation.StationRestDuration(_restMinutes);
        if (!_marketOpen && marketContent.activeSelf)
        {
            ShowHome();
        }
    }

    internal void ShowHome()
    {
        ShowMode(actionsList, Presentation.StationActionsTitle);
    }

    internal void ShowMarket()
    {
        ShowMode(marketContent, Presentation.StationMarket);
    }

    internal void ShowRest()
    {
        ShowMode(restContent, Presentation.StationRest);
    }

    internal void ShowTavern()
    {
        ShowMode(tavernContent.gameObject, Presentation.StationTavern);
    }

    internal void SetInteraction(bool _enabled)
    {
        interaction.interactable = interaction.blocksRaycasts = _enabled;
    }

    void ShowMode(GameObject _content, string _title)
    {
        actionsList.SetActive(_content == actionsList);
        marketContent.SetActive(_content == marketContent);
        restContent.SetActive(_content == restContent);
        tavernContent.gameObject.SetActive(_content == tavernContent.gameObject);
        backButton.interactable = true;
        backButton.gameObject.SetActive(_content != actionsList);
        title.text = _title;
    }

    protected override void OnShow()
    {
        ShowHome();
    }

    protected override void OnHide()
    {
        ShowHome();
    }

    void OnMarket()
    {
        MarketRequested?.Invoke();
    }
    void OnRest()
    {
        RestRequested?.Invoke();
    }
    void OnTavern()
    {
        TavernRequested?.Invoke();
    }
    void OnTravel()
    {
        TravelRequested?.Invoke();
    }
    void OnRestTime()
    {
        RestTimeRequested?.Invoke();
    }
}
