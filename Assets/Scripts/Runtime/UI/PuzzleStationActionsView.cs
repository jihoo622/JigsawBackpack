using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleStationActionsView : PanelBase
{
    [SerializeField] GameObject actionsList;
    [SerializeField] GameObject marketContent;
    [SerializeField] GameObject restContent;
    [SerializeField] RectTransform tavernContent;
    [SerializeField] GameObject butcheryContent;
    [SerializeField] GameObject modificationContent;
    [SerializeField] TextMeshProUGUI title;
    [SerializeField] TextMeshProUGUI marketLabel;
    [SerializeField] TextMeshProUGUI tavernLabel;
    [SerializeField] TextMeshProUGUI restLabel;
    [SerializeField] TextMeshProUGUI travelLabel;
    [SerializeField] TextMeshProUGUI backLabel;
    [SerializeField] TextMeshProUGUI restDurationLabel;
    [SerializeField] TextMeshProUGUI restHint;
    [SerializeField] TextMeshProUGUI butcheryLabel;
    [SerializeField] TextMeshProUGUI butcheryHint;
    [SerializeField] TextMeshProUGUI butcheryConfirmLabel;
    [SerializeField] TextMeshProUGUI modificationLabel;
    [SerializeField] TextMeshProUGUI modificationHint;
    [SerializeField] TextMeshProUGUI modificationModeLabel;
    [SerializeField] TextMeshProUGUI modificationConfirmLabel;
    [SerializeField] Button marketButton;
    [SerializeField] Button tavernButton;
    [SerializeField] Button restButton;
    [SerializeField] Button travelButton;
    [SerializeField] Button backButton;
    [SerializeField] Button restDurationButton;
    [SerializeField] Button butcheryButton;
    [SerializeField] Button butcheryConfirmButton;
    [SerializeField] Button modificationButton;
    [SerializeField] Button modificationModeButton;
    [SerializeField] Button modificationConfirmButton;
    [SerializeField] CanvasGroup interaction;

    internal event Action MarketRequested;
    internal event Action TavernRequested;
    internal event Action RestRequested;
    internal event Action TravelRequested;
    internal event Action RestTimeRequested;
    internal event Action ButcheryRequested;
    internal event Action ButcheryConfirmed;
    internal event Action ModificationRequested;
    internal event Action ModificationModeRequested;
    internal event Action ModificationConfirmed;
    internal event Action ActionBackRequested;
    public RectTransform TavernContent => tavernContent;
    // 도축 UI는 버튼·라벨·content·hint·확인 버튼이 모두 배선됐을 때만 동작한다. 일부만 있으면 기능이 꺼지고 로그만 남긴다.
    bool ButcheryWired => butcheryButton && butcheryLabel && butcheryContent && butcheryHint &&
        butcheryConfirmButton && butcheryConfirmLabel;
    bool ModificationWired => modificationButton && modificationLabel && modificationContent && modificationHint &&
        modificationModeButton && modificationModeLabel && modificationConfirmButton && modificationConfirmLabel;
    public bool HasButcheryButton => ButcheryWired;
    public bool HasModificationButton => ModificationWired;
    internal bool ShowingButchery => butcheryContent && butcheryContent.activeSelf;
    internal bool ShowingModification => modificationContent && modificationContent.activeSelf;

    protected override void Awake()
    {
        tavernLabel.text = Presentation.StationTavern;
        restLabel.text = Presentation.StationRest;
        travelLabel.text = Presentation.StationTravel;
        backLabel.text = Presentation.StationBack;
        restHint.text = Presentation.StationRestHint;
        tavernContent.gameObject.SetActive(false);
        if (butcheryButton && !ButcheryWired)
        {
            Debug.LogError("도축 버튼이 있는 View에는 행동 라벨·작업 화면·상태 라벨·확인 버튼과 확인 라벨 참조가 모두 필요합니다.", this);
        }
        if (ButcheryWired)
        {
            butcheryLabel.text = Presentation.StationButchery;
            butcheryConfirmLabel.text = Presentation.StationButcheryConfirm;
            butcheryContent.SetActive(false);
        }
        if (modificationButton && !ModificationWired)
        {
            Debug.LogError("개조 버튼이 있는 View에는 행동 라벨·작업 화면·상태 라벨·모드 및 확인 버튼과 각 라벨 참조가 모두 필요합니다.", this);
        }
        if (ModificationWired)
        {
            modificationLabel.text = Presentation.StationModification;
            modificationConfirmLabel.text = Presentation.StationModificationConfirm;
            modificationContent.SetActive(false);
        }
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        marketButton.onClick.AddListener(OnMarket);
        tavernButton.onClick.AddListener(OnTavern);
        restButton.onClick.AddListener(OnRest);
        travelButton.onClick.AddListener(OnTravel);
        backButton.onClick.AddListener(OnBack);
        restDurationButton.onClick.AddListener(OnRestTime);
        if (ButcheryWired)
        {
            butcheryButton.onClick.AddListener(OnButchery);
            butcheryConfirmButton.onClick.AddListener(OnButcheryConfirm);
        }
        if (ModificationWired)
        {
            modificationButton.onClick.AddListener(OnModification);
            modificationModeButton.onClick.AddListener(OnModificationMode);
            modificationConfirmButton.onClick.AddListener(OnModificationConfirm);
        }
    }

    protected override void OnDisable()
    {
        marketButton.onClick.RemoveListener(OnMarket);
        tavernButton.onClick.RemoveListener(OnTavern);
        restButton.onClick.RemoveListener(OnRest);
        travelButton.onClick.RemoveListener(OnTravel);
        backButton.onClick.RemoveListener(OnBack);
        restDurationButton.onClick.RemoveListener(OnRestTime);
        if (ButcheryWired)
        {
            butcheryButton.onClick.RemoveListener(OnButchery);
            butcheryConfirmButton.onClick.RemoveListener(OnButcheryConfirm);
        }
        if (ModificationWired)
        {
            modificationButton.onClick.RemoveListener(OnModification);
            modificationModeButton.onClick.RemoveListener(OnModificationMode);
            modificationConfirmButton.onClick.RemoveListener(OnModificationConfirm);
        }
        base.OnDisable();
    }

    internal void Refresh(bool _marketOpen, bool _inProgress, long _restMinutes, bool _butcheryAvailable,
        bool _butcheryEnabled, bool _butcheryConfirmEnabled, string _butcheryHint, long _butcheryCost)
    {
        marketLabel.text = _marketOpen ? Presentation.StationMarket : Presentation.StationMarketClosed;
        marketButton.interactable = _marketOpen;
        tavernButton.interactable = restButton.interactable = travelButton.interactable = restDurationButton.interactable = _inProgress;
        restDurationLabel.text = Presentation.StationRestDuration(_restMinutes);
        if (ButcheryWired)
        {
            butcheryButton.gameObject.SetActive(_butcheryAvailable && _inProgress);
            butcheryButton.interactable = _butcheryEnabled;
            butcheryHint.text = _butcheryHint;
            butcheryConfirmLabel.text = _butcheryCost >= 0
                ? Presentation.StationButcheryConfirmCost(_butcheryCost)
                : Presentation.StationButcheryConfirm;
            butcheryConfirmButton.interactable = _butcheryConfirmEnabled;
        }
        if (!_marketOpen && marketContent.activeSelf)
        {
            ShowHome();
        }
    }

    internal void RefreshModification(bool _inProgress, bool _available, bool _modeSelected,
        bool _confirmEnabled, string _modeDisplayName, string _hint, long _modificationCost)
    {
        if (!ModificationWired)
        {
            return;
        }
        modificationButton.gameObject.SetActive(_available && _inProgress);
        modificationButton.interactable = _available;
        modificationModeButton.interactable = _available;
        modificationModeLabel.text = _modeDisplayName;
        modificationModeLabel.color = _modeSelected ? Presentation.PuzzleTimeAccent : Presentation.PuzzleHeading;
        modificationHint.text = _hint;
        modificationConfirmLabel.text = _modificationCost >= 0
            ? Presentation.StationModificationConfirmCost(_modificationCost)
            : Presentation.StationModificationConfirm;
        modificationConfirmButton.interactable = _confirmEnabled;
    }

    // 닫기를 막거나 확인이 실패한 사유를 임시로 보여 준다. 다음 Refresh가 상태 문구로 되돌린다.
    internal void ShowButcheryNotice(string _message)
    {
        if (butcheryHint)
        {
            butcheryHint.text = _message;
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
        ShowRestNotice(Presentation.StationRestHint);
        ShowMode(restContent, Presentation.StationRest);
    }

    internal void ShowRestNotice(string _message)
    {
        restHint.text = _message;
    }

    internal void ShowTavern()
    {
        ShowMode(tavernContent.gameObject, Presentation.StationTavern);
    }

    internal void ShowButchery()
    {
        ShowMode(butcheryContent, Presentation.StationButchery);
    }

    internal void ShowModification()
    {
        ShowMode(modificationContent, Presentation.StationModification);
    }

    internal void ShowModificationNotice(string _message)
    {
        if (modificationHint)
        {
            modificationHint.text = _message;
        }
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
        if (butcheryContent)
        {
            butcheryContent.SetActive(_content == butcheryContent);
        }
        if (modificationContent)
        {
            modificationContent.SetActive(_content == modificationContent);
        }
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

    void OnButchery()
    {
        ButcheryRequested?.Invoke();
    }

    void OnButcheryConfirm()
    {
        ButcheryConfirmed?.Invoke();
    }

    void OnModification()
    {
        ModificationRequested?.Invoke();
    }

    void OnModificationMode()
    {
        ModificationModeRequested?.Invoke();
    }

    void OnModificationConfirm()
    {
        ModificationConfirmed?.Invoke();
    }

    // 뒤로 버튼은 Presenter가 닫기 가능 여부를 판단할 수 있게 올린다. 구독자가 없으면 그대로 목록으로 돌아간다.
    void OnBack()
    {
        if (ActionBackRequested == null)
        {
            ShowHome();
            return;
        }
        ActionBackRequested.Invoke();
    }
}
