using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleStationActionsView : PanelBase
{
    internal bool IsWagonShopOpen => IsOpen && wagonShopContent.activeInHierarchy;
    internal bool IsWagonShopTarget(GameObject _target) =>
        IsWagonShopOpen && _target &&
        _target.transform.IsChildOf(wagonShopContent.transform);
    [SerializeField] GameObject actionsList;
    [SerializeField] GameObject marketContent;
    [SerializeField] GameObject restContent;
    [SerializeField] RectTransform tavernContent;
    [SerializeField] GameObject butcheryContent;
    [SerializeField] GameObject modificationContent;
    [SerializeField] GameObject refiningContent;
    [SerializeField] GameObject wagonShopContent;
    [SerializeField] Button wagonShopButton;
    [SerializeField] Button wagonShopNextPage;
    [SerializeField] TextMeshProUGUI wagonShopPageLabel;
    [SerializeField] PuzzleWagonProductRow[] wagonProductRows;
    [SerializeField] PuzzleWagonDetailView wagonDetailView;
    [SerializeField] TextMeshProUGUI title;
    [SerializeField] TextMeshProUGUI marketLabel;
    [SerializeField] TextMeshProUGUI tavernLabel;
    [SerializeField] TextMeshProUGUI restLabel;
    [SerializeField] TextMeshProUGUI travelLabel;
    [SerializeField] TextMeshProUGUI backLabel;
    [SerializeField] TextMeshProUGUI restDurationLabel;
    [SerializeField] TextMeshProUGUI restHint;
    [SerializeField] TextMeshProUGUI butcheryLabel;
    [SerializeField] TextMeshProUGUI butcheryDescription;
    [SerializeField] TextMeshProUGUI butcheryHint;
    [SerializeField] TextMeshProUGUI butcheryConfirmLabel;
    [SerializeField] TextMeshProUGUI modificationLabel;
    [SerializeField] TextMeshProUGUI modificationDescription;
    [SerializeField] TextMeshProUGUI modificationHint;
    [SerializeField] TextMeshProUGUI modificationModeLabel;
    [SerializeField] TextMeshProUGUI modificationConfirmLabel;
    [SerializeField] TextMeshProUGUI refiningLabel;
    [SerializeField] TextMeshProUGUI refiningDescription;
    [SerializeField] TextMeshProUGUI refiningHint;
    [SerializeField] TextMeshProUGUI refiningConfirmLabel;
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
    [SerializeField] Button refiningButton;
    [SerializeField] Button refiningConfirmButton;
    [SerializeField] GameObject noFacilityCard;
    [SerializeField] TextMeshProUGUI noFacilityLabel;
    [SerializeField] CanvasGroup interaction;

    internal event Action MarketRequested;
    internal event Action WagonShopRequested;
    internal event Action TavernRequested;
    internal event Action RestRequested;
    internal event Action TravelRequested;
    internal event Action RestTimeRequested;
    internal event Action ButcheryRequested;
    internal event Action ButcheryConfirmed;
    internal event Action ModificationRequested;
    internal event Action ModificationModeRequested;
    internal event Action ModificationConfirmed;
    internal event Action RefiningRequested;
    internal event Action RefiningConfirmed;
    internal event Action ActionBackRequested;
    public RectTransform TavernContent => tavernContent;
    // 도축 UI는 버튼·라벨·content·hint·확인 버튼이 모두 배선됐을 때만 동작한다. 일부만 있으면 기능이 꺼지고 로그만 남긴다.
    bool ButcheryWired => butcheryButton && butcheryLabel && butcheryDescription && butcheryContent && butcheryHint &&
        butcheryConfirmButton && butcheryConfirmLabel;
    bool ModificationWired => modificationButton && modificationLabel && modificationDescription &&
        modificationContent && modificationHint &&
        modificationModeButton && modificationModeLabel && modificationConfirmButton && modificationConfirmLabel;
    // 정제 UI는 버튼·라벨·content·hint·확인 버튼이 모두 배선됐을 때만 동작한다. 일부만 있으면 기능이 꺼지고 로그만 남긴다.
    bool RefiningWired => refiningButton && refiningLabel && refiningDescription && refiningContent && refiningHint &&
        refiningConfirmButton && refiningConfirmLabel;
    public bool HasButcheryButton => ButcheryWired;
    public bool HasModificationButton => ModificationWired;
    public bool HasRefiningButton => RefiningWired;
    internal bool ShowingButchery => butcheryContent && butcheryContent.activeSelf;
    internal bool ShowingModification => modificationContent && modificationContent.activeSelf;
    internal bool ShowingRefining => refiningContent && refiningContent.activeSelf;
    public bool ShowingSubscreen => isActiveAndEnabled && actionsList && !actionsList.activeSelf;
    PuzzleWagonProductData[] wagonProducts;
    int wagonShopPage;
    // 화차 상점은 선택 없음으로 시작하고, 카드를 누르거나 끌기 시작하면 선택된다. 같은 카드를 다시 누르거나 상점을 나가면 해제한다.
    PuzzleWagonProductRow selectedWagonRow;
    // 마지막으로 기록한 하위 화면 이름. 같은 화면을 다시 그릴 때 station_screen을 중복 기록하지 않는다.
    string currentScreen;

    protected override void Awake()
    {
        tavernLabel.text = Presentation.StationTurnCost(Presentation.StationTavern);
        restLabel.text = Presentation.StationRest;
        travelLabel.text = Presentation.StationTravel;
        backLabel.text = Presentation.StationBack;
        restHint.text = Presentation.StationRestHint;
        wagonShopButton.GetComponentInChildren<TextMeshProUGUI>().text = Presentation.StationTurnCost(Presentation.StationWagonShop);
        tavernContent.gameObject.SetActive(false);
        if (butcheryButton && !ButcheryWired)
        {
            Debug.LogError("도축 버튼이 있는 View에는 행동 라벨·설명·작업 화면·상태 라벨·확인 버튼과 확인 라벨 참조가 모두 필요합니다.", this);
        }
        if (ButcheryWired)
        {
            butcheryLabel.text = Presentation.StationButchery;
            butcheryConfirmLabel.text = Presentation.StationButcheryConfirm;
            butcheryContent.SetActive(false);
        }
        if (modificationButton && !ModificationWired)
        {
            Debug.LogError("개조 버튼이 있는 View에는 행동 라벨·설명·작업 화면·상태 라벨·모드 및 확인 버튼과 각 라벨 참조가 모두 필요합니다.", this);
        }
        if (ModificationWired)
        {
            modificationLabel.text = Presentation.StationModification;
            modificationConfirmLabel.text = Presentation.StationModificationConfirm;
            modificationContent.SetActive(false);
        }
        if (refiningButton && !RefiningWired)
        {
            Debug.LogError("정제 버튼이 있는 View에는 행동 라벨·설명·작업 화면·상태 라벨·확인 버튼과 확인 라벨 참조가 모두 필요합니다.", this);
        }
        if (RefiningWired)
        {
            refiningLabel.text = Presentation.StationRefining;
            refiningConfirmLabel.text = Presentation.StationRefiningConfirm;
            refiningContent.SetActive(false);
        }
        if (noFacilityLabel)
        {
            noFacilityLabel.text = Presentation.StationNoFacility;
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
        wagonShopButton.onClick.AddListener(OnWagonShop);
        wagonShopNextPage.onClick.AddListener(NextWagonShopPage);
        if (wagonProductRows != null)
        {
            for (int i = 0; i < wagonProductRows.Length; i++)
            {
                if (wagonProductRows[i] != null)
                {
                    wagonProductRows[i].Clicked += OnWagonRowClicked;
                    wagonProductRows[i].DragStarted += OnWagonRowDragStarted;
                }
            }
        }
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
        if (RefiningWired)
        {
            refiningButton.onClick.AddListener(OnRefining);
            refiningConfirmButton.onClick.AddListener(OnRefiningConfirm);
        }
    }

    protected override void OnDisable()
    {
        if (wagonShopContent && wagonShopContent.activeSelf)
        {
            // 비활성화 중에는 하위 오브젝트를 켜고 끄지 않는다. 다시 열면 RefreshWagonShopPage가 선택 없음으로 그린다.
            selectedWagonRow = null;
            wagonShopContent.SetActive(false);
            GameEvents.RaisePanelVisibility(Presentation.PUZZLE_WAGON_SHOP_PANEL_NAME, false);
        }
        currentScreen = null;
        marketButton.onClick.RemoveListener(OnMarket);
        tavernButton.onClick.RemoveListener(OnTavern);
        restButton.onClick.RemoveListener(OnRest);
        travelButton.onClick.RemoveListener(OnTravel);
        backButton.onClick.RemoveListener(OnBack);
        restDurationButton.onClick.RemoveListener(OnRestTime);
        wagonShopButton.onClick.RemoveListener(OnWagonShop);
        wagonShopNextPage.onClick.RemoveListener(NextWagonShopPage);
        if (wagonProductRows != null)
        {
            for (int i = 0; i < wagonProductRows.Length; i++)
            {
                if (wagonProductRows[i] != null)
                {
                    wagonProductRows[i].Clicked -= OnWagonRowClicked;
                    wagonProductRows[i].DragStarted -= OnWagonRowDragStarted;
                }
            }
        }
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
        if (RefiningWired)
        {
            refiningButton.onClick.RemoveListener(OnRefining);
            refiningConfirmButton.onClick.RemoveListener(OnRefiningConfirm);
        }
        base.OnDisable();
    }

    // _marketOpen: 지금 시장에 들어갈 수 있는지(밤이 아니고 턴을 쓸 수 있음). _marketEntered: 이미 시장에 들어가 있는지.
    // _canSpendTurn: 역 행동 1턴을 쓸 수 있는지(기한). 턴을 쓰는 버튼만 막고 휴식 화면·이동 버튼은 그대로 둔다.
    internal void Refresh(bool _marketOpen, bool _marketEntered, bool _inProgress, bool _canSpendTurn, long _restTurns,
        bool _butcheryAvailable, bool _butcheryEnabled, bool _butcheryConfirmEnabled, string _butcheryHint,
        long _butcheryCost, string _butcheryDescription)
    {
        marketLabel.text = _marketOpen || _marketEntered
            ? Presentation.StationTurnCost(Presentation.StationMarket)
            : Presentation.StationMarketClosed;
        marketButton.interactable = _marketOpen || _marketEntered;
        restButton.interactable = travelButton.interactable = _inProgress;
        tavernButton.interactable = restDurationButton.interactable = _inProgress && _canSpendTurn;
        restDurationLabel.text = Presentation.StationRestDuration(_restTurns);
        wagonShopButton.interactable = _inProgress && _canSpendTurn;
        if (!_inProgress && wagonShopContent.activeSelf)
        {
            ShowHome();
        }
        if (ButcheryWired)
        {
            butcheryButton.gameObject.SetActive(_butcheryAvailable && _inProgress);
            butcheryButton.interactable = _butcheryEnabled;
            butcheryDescription.text = _butcheryDescription;
            butcheryHint.text = _butcheryHint;
            butcheryConfirmLabel.text = _butcheryCost >= 0
                ? Presentation.StationButcheryConfirmCost(_butcheryCost)
                : Presentation.StationButcheryConfirm;
            butcheryConfirmButton.interactable = _butcheryConfirmEnabled;
        }
        if (!_marketEntered && marketContent.activeSelf)
        {
            ShowHome();
        }
    }

    internal void RefreshModification(bool _inProgress, bool _available, PuzzleModificationPhase _phase, bool _modeSelected,
        bool _confirmEnabled, string _modeDisplayName, string _hint, long _modificationCost, string _description)
    {
        if (!ModificationWired)
        {
            return;
        }
        modificationButton.gameObject.SetActive(_available && _inProgress);
        modificationButton.interactable = _inProgress && _available && _phase != PuzzleModificationPhase.Working;
        modificationDescription.text = _description;
        modificationModeButton.interactable = _inProgress && _available && _phase == PuzzleModificationPhase.Idle;
        modificationModeLabel.text = _modeDisplayName;
        modificationModeLabel.color = _modeSelected ? Presentation.PuzzleTimeAccent : Presentation.PuzzleHeading;
        modificationHint.text = _hint;
        modificationConfirmLabel.text = _modificationCost >= 0
            ? Presentation.StationModificationConfirmCost(_modificationCost)
            : Presentation.StationModificationConfirm;
        modificationConfirmButton.interactable = _confirmEnabled;
    }

    internal void RefreshRefining(bool _inProgress, bool _available, bool _enabled, bool _confirmEnabled,
        string _hint, long _refiningCost, string _description)
    {
        if (!RefiningWired)
        {
            return;
        }
        refiningButton.gameObject.SetActive(_available && _inProgress);
        refiningButton.interactable = _enabled;
        refiningDescription.text = _description;
        refiningHint.text = _hint;
        refiningConfirmLabel.text = _refiningCost >= 0
            ? Presentation.StationRefiningConfirmCost(_refiningCost)
            : Presentation.StationRefiningConfirm;
        refiningConfirmButton.interactable = _confirmEnabled;
    }

    internal void UpdateFacilityCard(bool _hasFacility)
    {
        if (noFacilityCard)
        {
            noFacilityCard.SetActive(!_hasFacility);
        }
    }

    // 닫기를 막거나 확인이 실패한 사유를 임시로 보여 준다. 다음 Refresh가 상태 문구로 되돌린다.
    internal void ShowButcheryNotice(string _message)
    {
        if (butcheryHint)
        {
            butcheryHint.text = _message;
        }
    }

    public void ShowHome()
    {
        ShowMode(actionsList, Presentation.StationActionsTitle);
    }

    public void ShowMarket()
    {
        ShowMode(marketContent, Presentation.StationMarket);
    }

    internal void ShowWagonShop()
    {
        wagonProducts = TableManager.Instance.PuzzleTrade.wagonProducts;
        wagonShopPage = 0;
        ShowMode(wagonShopContent, Presentation.StationWagonShop);
        RefreshWagonShopPage();
    }

    void NextWagonShopPage()
    {
        int pageCount = (wagonProducts.Length + wagonProductRows.Length - 1) / wagonProductRows.Length;
        wagonShopPage = (wagonShopPage + 1) % pageCount;
        RefreshWagonShopPage();
    }

    void OnWagonRowClicked(PuzzleWagonProductRow _row)
    {
        SelectWagonRow(selectedWagonRow == _row ? null : _row);
    }

    void OnWagonRowDragStarted(PuzzleWagonProductRow _row)
    {
        SelectWagonRow(_row);
    }

    void SelectWagonRow(PuzzleWagonProductRow _row)
    {
        selectedWagonRow = _row != null && _row.Product != null ? _row : null;
        if (wagonDetailView != null)
        {
            wagonDetailView.Bind(selectedWagonRow != null ? selectedWagonRow.Product : null,
                selectedWagonRow != null ? selectedWagonRow.ArtworkSprite : null);
        }
        if (wagonProductRows != null)
        {
            for (int i = 0; i < wagonProductRows.Length; i++)
            {
                if (wagonProductRows[i] != null)
                {
                    wagonProductRows[i].SetSelected(wagonProductRows[i] == selectedWagonRow);
                }
            }
        }
    }

    // 쪽을 넘기면 카드 내용이 바뀌므로 선택을 해제한다.
    void RefreshWagonShopPage()
    {
        int pageCount = (wagonProducts.Length + wagonProductRows.Length - 1) / wagonProductRows.Length;
        int firstIndex = wagonShopPage * wagonProductRows.Length;
        for (int i = 0; i < wagonProductRows.Length; i++)
        {
            int index = firstIndex + i;
            var prod = index < wagonProducts.Length ? wagonProducts[index] : null;
            wagonProductRows[i].Bind(prod);
        }
        wagonShopPageLabel.text = Presentation.StationWagonShopPage(wagonShopPage + 1, pageCount);
        wagonShopNextPage.gameObject.SetActive(pageCount > 1);
        SelectWagonRow(null);
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

    internal void ShowRefining()
    {
        ShowMode(refiningContent, Presentation.StationRefining);
    }

    internal void ShowModificationNotice(string _message)
    {
        if (modificationHint)
        {
            modificationHint.text = _message;
        }
    }

    // 닫기를 막거나 확인이 실패한 사유를 임시로 보여 준다. 다음 Refresh가 상태 문구로 되돌린다.
    internal void ShowRefiningNotice(string _message)
    {
        if (refiningHint)
        {
            refiningHint.text = _message;
        }
    }

    internal void SetInteraction(bool _enabled)
    {
        interaction.interactable = interaction.blocksRaycasts = _enabled;
        if (!_enabled && wagonShopContent.activeSelf)
        {
            ShowHome();
        }
    }

    void ShowMode(GameObject _content, string _title)
    {
        actionsList.SetActive(_content == actionsList);
        marketContent.SetActive(_content == marketContent);
        bool wagonShopOpen = _content == wagonShopContent;
        bool wagonShopChanged = wagonShopContent.activeSelf != wagonShopOpen;
        wagonShopContent.SetActive(wagonShopOpen);
        // 화차 상점 열림 변화는 PanelVisibility로 흘린다. 화물칸 바가 다음 잠긴 칸 강조에 쓴다.
        if (wagonShopChanged)
        {
            if (!wagonShopOpen)
            {
                SelectWagonRow(null);
            }
            GameEvents.RaisePanelVisibility(Presentation.PUZZLE_WAGON_SHOP_PANEL_NAME, wagonShopOpen);
        }
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
        if (refiningContent)
        {
            refiningContent.SetActive(_content == refiningContent);
        }
        backButton.interactable = true;
        backButton.gameObject.SetActive(_content != actionsList);
        title.text = _title;

        // 하위 화면 전환은 분석 로그(station_screen)로만 흘린다. 같은 화면을 다시 그리는 것은 전환이 아니다.
        string screen = ScreenName(_content);
        if (screen != currentScreen)
        {
            currentScreen = screen;
            GameEvents.RaiseStationScreenChanged(screen);
        }
    }

    // 분석 로그의 하위 화면 이름. 화면 문구가 아니라 고정 식별자다.
    string ScreenName(GameObject _content)
    {
        if (_content == actionsList)
        {
            return "home";
        }
        if (_content == marketContent)
        {
            return "market";
        }
        if (_content == wagonShopContent)
        {
            return "wagon_shop";
        }
        if (_content == restContent)
        {
            return "rest";
        }
        if (_content == tavernContent.gameObject)
        {
            return "tavern";
        }
        if (butcheryContent && _content == butcheryContent)
        {
            return "butchery";
        }
        if (modificationContent && _content == modificationContent)
        {
            return "modification";
        }
        if (refiningContent && _content == refiningContent)
        {
            return "refining";
        }
        return _content ? _content.name : string.Empty;
    }

    protected override void OnShow()
    {
        ShowHome();
    }

    protected override void OnHide()
    {
        ShowHome();
        // 다시 열 때 첫 화면을 전환으로 기록한다.
        currentScreen = null;
    }

    void OnMarket()
    {
        MarketRequested?.Invoke();
    }
    void OnWagonShop()
    {
        WagonShopRequested?.Invoke();
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

    void OnRefining()
    {
        RefiningRequested?.Invoke();
    }

    void OnRefiningConfirm()
    {
        RefiningConfirmed?.Invoke();
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
