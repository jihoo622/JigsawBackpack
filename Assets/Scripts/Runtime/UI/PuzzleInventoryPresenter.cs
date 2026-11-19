using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed partial class PuzzleInventoryPresenter : MonoBehaviour
{
    [Header("Drag Settings")]
    [SerializeField] float wagonDragHoverDelay = 0.5f;

    [SerializeField] PuzzleInventoryView trainView;
    [SerializeField] TextMeshProUGUI wagonEffectsLabel;
    [SerializeField] Button wagonRefrigerationButton;
    [SerializeField] TextMeshProUGUI wagonRefrigerationLabel;
    [SerializeField] PuzzleInventoryView marketView;
    [SerializeField] PuzzleInventoryView butcheryView;
    [SerializeField] PuzzleInventoryView modificationView;
    [SerializeField] PuzzleInventoryView refiningView;
    [SerializeField] PuzzleCityView cityView;
    [SerializeField] PuzzleObjectiveView objectiveView;
    [SerializeField] PuzzleItemTooltipView tooltipView;
    [SerializeField] PuzzleItemTooltipView wagonTooltipView;
    readonly List<(string label, string value)> wagonTooltipStats = new();
    readonly List<string> wagonTooltipEffects = new();
    PuzzleInventory hoveredTooltipWagon;
    PuzzleStationActionsView stationActions;
    int? wagonSalePreviewIndex;
    [SerializeField] PuzzleItemTooltipView keywordTooltipView;
    [SerializeField] PuzzleTrainBarView trainBarView;
    [SerializeField] PuzzleInventoryView relayoutTrainView;
    [SerializeField] PuzzleInventoryView relayoutMarketView;
    [SerializeField] RailStripView railStripView;
    [SerializeField] TrainWindowView trainWindowView;
    [SerializeField] TrainWindowManager trainWindowManager;
    [SerializeField] MarketWindowView marketWindowView;
    [SerializeField] LensWindowView lensWindowView;
    [SerializeField] TavernWindowView tavernWindowView;
    [SerializeField] StationHallWindowView stationHallWindowView;
    [SerializeField] WorkshopWindowView workshopWindowView;
    [SerializeField] TownSceneView townSceneView;
    [SerializeField] StatusBannerView statusBannerView;
    [SerializeField] NotebookWindowView notebookWindowView;
    [SerializeField] PauseMenu pauseMenu;
    [SerializeField] RunEndPresenter runEndPresenter;

    bool UseMultiTrainWindows => RelayoutSettings.Enabled && trainWindowManager;
    PuzzleInventoryView ActiveTrainView => RelayoutSettings.Enabled && relayoutTrainView ? relayoutTrainView : trainView;
    PuzzleInventoryView ActiveMarketView => RelayoutSettings.Enabled && relayoutMarketView ? relayoutMarketView : marketView;
    PuzzleInventoryView ActiveQuestBoardView => RelayoutSettings.Enabled && tavernWindowView ? tavernWindowView.QuestBoardView : null;
    PuzzleInventoryView ActiveButcheryView => RelayoutSettings.Enabled && workshopWindowView && workshopWindowView.ButcheryBoardView ? workshopWindowView.ButcheryBoardView : butcheryView;
    PuzzleInventoryView ActiveModificationView => RelayoutSettings.Enabled && workshopWindowView && workshopWindowView.ModificationBoardView ? workshopWindowView.ModificationBoardView : modificationView;
    PuzzleInventoryView ActiveRefiningView => RelayoutSettings.Enabled && workshopWindowView && workshopWindowView.RefiningBoardView ? workshopWindowView.RefiningBoardView : refiningView;

    [SerializeField] PuzzleInventoryInput input;
    PuzzleInventoryService session;
    PuzzleInventory questOfferBoard;
    readonly Dictionary<int, TrainventoryQuestOffer> questOfferByInstanceId = new();
    // 아직 상태 줄에 띄우지 않은 숙련 레벨 상승 알림.
    string pendingProficiencyNotice;
    PuzzleInventoryDrag drag;
    PuzzleInventoryView dragView;
    Vector2 pointerPosition;
    Vector2? hoverPosition;
    int? currentHoveredInstanceId;
    string currentHoveredKeywordId;
    int? hoveredWagonIndex;
    PuzzleItemTooltipView KeywordTooltipView => keywordTooltipView ? keywordTooltipView : tooltipView;
    float hoveredWagonStartTime;
    bool initialized;
    bool isMapOpen;
    bool butcheryConfigured;
    bool modificationConfigured;
    bool refiningConfigured;
    // 도축대는 세션이 조작을 허용하고 패널에 View가 나와 있을 때만 드래그·표시 대상이 된다.
    bool ButcheryViewReady => session != null && session.IsButcheryOpen && ActiveButcheryView != null &&
        ActiveButcheryView.gameObject.activeInHierarchy;
    internal static bool HasActiveDrag { get; private set; }
    bool ModificationViewReady => session != null && session.IsModificationOpen && ActiveModificationView != null &&
        ActiveModificationView.gameObject.activeInHierarchy;
    bool RefiningViewReady => session != null && session.IsRefiningOpen && ActiveRefiningView != null &&
        ActiveRefiningView.gameObject.activeInHierarchy;
    decimal? pendingSettlementValue;
    decimal? pendingFreshnessLossValue;
    bool pendingFundsRefresh;
    bool isTravelOpen;
    // 시장 보드가 마지막으로 그린 시장. 도시를 옮기거나 개장으로 새로 채워지면 객체가 바뀐다.
    PuzzleInventory shownMarket;
    PuzzleInventoryView lastDragQuoteView;
    int lastDragQuoteInstanceId = -1;
    string cachedDragQuoteText;
    System.Func<PuzzlePlacement, string> cachedMarketPriceProvider;
    bool TrainOperationsBlocked => isTravelOpen;
    bool MarketOperationsBlocked => isMapOpen || isTravelOpen;

    internal bool AreShortcutsBlocked()
    {
        if (MarketOperationsBlocked || session == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return true;
        }
        if (pauseMenu && pauseMenu.IsOpen)
        {
            return true;
        }
        var runEnd = ResolveRunEndPresenter();
        return runEnd && runEnd.IsOpen;
    }

    void Start()
    {
        if (!initialized || !objectiveView || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        objectiveView.ShowObjective(session.TargetFunds, session.Time.DeadlineHours);
    }

    void OnEnable()
    {
        if (!input || !trainView || !marketView || trainView == marketView || !cityView || !pauseMenu ||
            (butcheryView && (butcheryView == trainView || butcheryView == marketView)) ||
            (modificationView && (modificationView == trainView || modificationView == marketView ||
                modificationView == butcheryView)) ||
            (refiningView && (refiningView == trainView || refiningView == marketView ||
                refiningView == butcheryView || refiningView == modificationView)) ||
            !trainView.transform.IsChildOf(transform) || !marketView.transform.IsChildOf(transform) ||
            !cityView.transform.IsChildOf(transform))
        {
            Debug.LogError("화물 인벤토리 Presenter에는 Input, 기차·시장·도시 View와 일시 정지 메뉴가 필요합니다. 도축대·개조대·정제대 View는 서로 다른 View여야 합니다.", this);
            enabled = false;
            return;
        }

        if (!tooltipView)
        {
            tooltipView = GetComponentInChildren<PuzzleItemTooltipView>(true);
            if (!tooltipView)
            {
                tooltipView = transform.root.GetComponentInChildren<PuzzleItemTooltipView>(true);
            }
        }

        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        long trainCapacity = (long)session.Train.Width * session.Train.Height;
        long dragCapacity = System.Math.Max(trainCapacity, session.MarketCapacity);
        trainView.Initialize(trainCapacity, dragCapacity, Presentation.PuzzleTrainTitle(session.Train), session.ItemDefinitions, _showSynergies: true);
        bool relayoutViewInitialized = false;
        if (trainWindowManager && trainWindowManager.Windows != null)
        {
            var wins = trainWindowManager.Windows;
            for (int i = 0; i < wins.Count; i++)
            {
                var iv = wins[i] ? wins[i].InventoryView : null;
                if (iv)
                {
                    iv.Initialize(trainCapacity, dragCapacity, Presentation.PuzzleTrainTitle(session.Train), session.ItemDefinitions, _showSynergies: true);
                    if (iv == relayoutTrainView)
                    {
                        relayoutViewInitialized = true;
                    }
                }
            }
        }
        if (relayoutTrainView && !relayoutViewInitialized)
        {
            relayoutTrainView.Initialize(trainCapacity, dragCapacity, Presentation.PuzzleTrainTitle(session.Train), session.ItemDefinitions, _showSynergies: true);
        }
        marketView.Initialize(session.MarketCapacity, dragCapacity, Presentation.PuzzleMarketTitle, session.ItemDefinitions, _showSynergies: false);
        if (relayoutMarketView)
        {
            relayoutMarketView.Initialize(session.MarketCapacity, dragCapacity, Presentation.PuzzleMarketTitle, session.ItemDefinitions, _showSynergies: false);
        }
        ConfigureButcheryView();
        ConfigureModificationView();
        ConfigureRefiningView();
        ConfigureQuestBoardView();
        RebuildQuestOfferBoard();
        if (tavernWindowView)
        {
            tavernWindowView.ResetCity(session.CityData?.displayName ?? string.Empty);
        }
        cityView.Initialize(session.TargetFunds);
        cityView.InitializeTown(GetTavernRumor);
        ShowCurrentCity();
        cityView.SetTown(session.CityData.displayName, session.RunStatus == PuzzleRunStatus.InProgress);
        initialized = true;

        input.Pressed += OnPressed;
        input.QuickTransferRequested += OnQuickTransferRequested;
        input.Dragged += OnDragged;
        input.Released += OnReleased;
        input.Rotated += OnRotated;
        input.Canceled += ClearDrag;
        input.Hovered += OnHovered;
        input.HoverEnded += OnHoverEnded;
        trainView.Disabled += OnViewDisabled;
        if (relayoutTrainView)
        {
            relayoutTrainView.Disabled += OnViewDisabled;
        }
        marketView.Disabled += OnViewDisabled;
        if (relayoutMarketView)
        {
            relayoutMarketView.Disabled += OnViewDisabled;
        }
        SubscribeLens();
        if (butcheryView)
        {
            butcheryView.Disabled += OnViewDisabled;
        }
        if (modificationView)
        {
            modificationView.Disabled += OnViewDisabled;
        }
        if (refiningView)
        {
            refiningView.Disabled += OnViewDisabled;
        }
        if (tavernWindowView)
        {
            tavernWindowView.RumorRequested += OnTavernRumorRequested;
            tavernWindowView.Opened += RebuildQuestOfferBoard;
            if (tavernWindowView.QuestBoardView)
            {
                tavernWindowView.QuestBoardView.Disabled += OnViewDisabled;
            }
        }
        if (workshopWindowView)
        {
            if (workshopWindowView.ButcheryBoardView) workshopWindowView.ButcheryBoardView.Disabled += OnViewDisabled;
            if (workshopWindowView.ModificationBoardView) workshopWindowView.ModificationBoardView.Disabled += OnViewDisabled;
            if (workshopWindowView.RefiningBoardView) workshopWindowView.RefiningBoardView.Disabled += OnViewDisabled;
        }
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PuzzleRunSettled += OnRunSettled;
        GameEvents.PuzzleRunContinued += OnRunContinued;
        GameEvents.PuzzleActionCommitted += OnPuzzleActionCommitted;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.TravelPreviewChanged += OnTravelPreviewChanged;
        GameEvents.ButcheryChanged += OnButcheryChanged;
        GameEvents.ModificationChanged += OnModificationChanged;
        GameEvents.RefiningChanged += OnRefiningChanged;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.ProficiencyChanged += OnProficiencyChanged;
        if (wagonRefrigerationButton)
        {
            wagonRefrigerationButton.onClick.AddListener(OnWagonRefrigerationClicked);
        }
        cityView.WaitRequested += WaitForDaylight;
        if (trainBarView)
        {
            trainBarView.WagonSelected += OnWagonSelected;
            trainBarView.WagonProductDropped += OnWagonProductDropped;
            trainBarView.WagonMoved += OnWagonMoved;
            trainBarView.WagonHovered += OnWagonHovered;
            trainBarView.WagonHoverEnded += HideWagonTooltip;
            trainBarView.OwnedWagonDragged += PreviewWagonSale;
            trainBarView.OwnedWagonReleased += FinishWagonSale;
            trainBarView.OwnedWagonDragEnded += ClearWagonSalePreview;
        }
        if (railStripView)
        {
            railStripView.WagonSelected += OnRelayoutWagonSelected;
            railStripView.WagonReordered += OnWagonMoved;
            railStripView.WagonDraggedOutside += OnRelayoutWagonDraggedOutside;
            railStripView.WagonDragEndedOutside += OnRelayoutWagonDragEndedOutside;
        }
        if (trainWindowManager)
        {
            trainWindowManager.WagonFocused += OnRelayoutWagonFocused;
            trainWindowManager.OpenWindowsChanged += OnRelayoutOpenWindowsChanged;
            trainWindowManager.LensToggleRequested += OnRelayoutLensToggleRequested;
        }
        stationActions = GetComponentInChildren<PuzzleStationActionsView>(true);
        if (RelayoutSettings.Enabled && tavernWindowView)
        {
            tavernWindowView.ResetCity(session.CityData.displayName);
            RebuildQuestOfferBoard();
        }
        if (RelayoutSettings.Enabled && marketWindowView)
        {
            marketWindowView.SetCity(session.CityData.displayName, Presentation.GetCityTradePreferences(session.CityData));
            if (session.IsMarketOpen && session.TryEnterMarket())
            {
                relayoutMarketView?.SetMarketOpen(session.CanTradeInMarket);
                marketWindowView.SetOpen(true, false);
            }
        }
    }

    void OnDisable()
    {
        if (!initialized)
        {
            return;
        }

        if (trainWindowManager)
        {
            trainWindowManager.ClearAllDragVisuals();
        }
        if (trainView)
        {
            trainView.Disabled -= OnViewDisabled;
        }
        if (relayoutTrainView)
        {
            relayoutTrainView.Disabled -= OnViewDisabled;
        }
        if (marketView)
        {
            marketView.Disabled -= OnViewDisabled;
            UnsubscribeLens();
        }
        if (relayoutMarketView)
        {
            relayoutMarketView.Disabled -= OnViewDisabled;
        }
        if (butcheryView)
        {
            butcheryView.Disabled -= OnViewDisabled;
        }
        if (modificationView)
        {
            modificationView.Disabled -= OnViewDisabled;
        }
        if (refiningView)
        {
            refiningView.Disabled -= OnViewDisabled;
        }
        if (tavernWindowView)
        {
            tavernWindowView.RumorRequested -= OnTavernRumorRequested;
            tavernWindowView.Opened -= RebuildQuestOfferBoard;
            if (tavernWindowView.QuestBoardView)
            {
                tavernWindowView.QuestBoardView.Disabled -= OnViewDisabled;
            }
        }
        if (workshopWindowView)
        {
            if (workshopWindowView.ButcheryBoardView) workshopWindowView.ButcheryBoardView.Disabled -= OnViewDisabled;
            if (workshopWindowView.ModificationBoardView) workshopWindowView.ModificationBoardView.Disabled -= OnViewDisabled;
            if (workshopWindowView.RefiningBoardView) workshopWindowView.RefiningBoardView.Disabled -= OnViewDisabled;
        }
        if (input)
        {
            input.Pressed -= OnPressed;
            input.QuickTransferRequested -= OnQuickTransferRequested;
            input.Dragged -= OnDragged;
            input.Released -= OnReleased;
            input.Rotated -= OnRotated;
            input.Canceled -= ClearDrag;
            input.Hovered -= OnHovered;
            input.HoverEnded -= OnHoverEnded;
        }
        if (tooltipView)
        {
            tooltipView.Close();
        }
        ClearKeywordHover();
        currentHoveredInstanceId = null;
        isMapOpen = false;
        HasActiveDrag = false;
        pendingSettlementValue = null;
        pendingFreshnessLossValue = null;
        pendingFundsRefresh = false;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PuzzleRunSettled -= OnRunSettled;
        GameEvents.PuzzleRunContinued -= OnRunContinued;
        GameEvents.PuzzleActionCommitted -= OnPuzzleActionCommitted;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.TravelPreviewChanged -= OnTravelPreviewChanged;
        GameEvents.ButcheryChanged -= OnButcheryChanged;
        GameEvents.ModificationChanged -= OnModificationChanged;
        GameEvents.RefiningChanged -= OnRefiningChanged;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.ProficiencyChanged -= OnProficiencyChanged;
        if (wagonRefrigerationButton)
        {
            wagonRefrigerationButton.onClick.RemoveListener(OnWagonRefrigerationClicked);
        }
        cityView.WaitRequested -= WaitForDaylight;
        ClearWagonSalePreview();
        if (trainBarView)
        {
            trainBarView.WagonSelected -= OnWagonSelected;
            trainBarView.WagonProductDropped -= OnWagonProductDropped;
            trainBarView.WagonMoved -= OnWagonMoved;
            trainBarView.WagonHovered -= OnWagonHovered;
            trainBarView.WagonHoverEnded -= HideWagonTooltip;
            trainBarView.OwnedWagonDragged -= PreviewWagonSale;
            trainBarView.OwnedWagonReleased -= FinishWagonSale;
            trainBarView.OwnedWagonDragEnded -= ClearWagonSalePreview;
            trainBarView.CancelWagonDrag();
        }
        if (railStripView)
        {
            railStripView.WagonSelected -= OnRelayoutWagonSelected;
            railStripView.WagonReordered -= OnWagonMoved;
            railStripView.WagonDraggedOutside -= OnRelayoutWagonDraggedOutside;
            railStripView.WagonDragEndedOutside -= OnRelayoutWagonDragEndedOutside;
        }
        if (trainWindowManager)
        {
            trainWindowManager.WagonFocused -= OnRelayoutWagonFocused;
            trainWindowManager.OpenWindowsChanged -= OnRelayoutOpenWindowsChanged;
            trainWindowManager.LensToggleRequested -= OnRelayoutLensToggleRequested;
        }
        HideWagonTooltip();
        OnViewDisabled();
        initialized = false;
    }

    void Update()
    {
        if (!initialized || session == null)
        {
            ClearKeywordHover();
            if (hoveredWagonIndex.HasValue)
            {
                hoveredWagonIndex = null;
                RefreshTrainBar();
            }
            return;
        }
        if (drag == null)
        {
            if (hoveredWagonIndex.HasValue)
            {
                hoveredWagonIndex = null;
                RefreshTrainBar();
            }
            if (MarketOperationsBlocked || (pauseMenu && pauseMenu.IsOpen) || !Application.isFocused)
            {
                ClearKeywordHover();
                return;
            }

            Vector2? currentPointer = Pointer.current != null ? Pointer.current.position.ReadValue() : hoverPosition;
            UpdateKeywordHover(currentPointer);
            return;
        }

        ClearKeywordHover();
        bool hasWagonBar = RelayoutSettings.Enabled ? (bool)railStripView : (bool)trainBarView;
        if (TrainOperationsBlocked || !hasWagonBar || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            if (hoveredWagonIndex.HasValue)
            {
                hoveredWagonIndex = null;
                RefreshTrainBar();
            }
            return;
        }

        bool overWagon = RelayoutSettings.Enabled && railStripView
            ? railStripView.TryGetWagonAtScreenPosition(pointerPosition, out int wagonIndex)
            : trainBarView.TryGetWagonAtScreenPosition(pointerPosition, out wagonIndex);
        if (overWagon && wagonIndex < session.WagonCount && wagonIndex != session.CurrentWagonIndex)
        {
            if (hoveredWagonIndex != wagonIndex)
            {
                hoveredWagonIndex = wagonIndex;
                hoveredWagonStartTime = Time.unscaledTime;
                RefreshTrainBar();
            }
            else if (Time.unscaledTime - hoveredWagonStartTime >= wagonDragHoverDelay)
            {
                SwitchWagonDuringDrag(wagonIndex);
            }
        }
        else if (hoveredWagonIndex.HasValue)
        {
            hoveredWagonIndex = null;
            RefreshTrainBar();
        }
    }

    bool CanQuickDropToWagon(int _wagonIndex)
    {
        if (drag == null || session == null || _wagonIndex < 0 || _wagonIndex >= session.WagonCount)
        {
            return false;
        }

        return drag.Source != session.TrainWagons[_wagonIndex];
    }

    void SwitchWagonDuringDrag(int _index)
    {
        if (UseMultiTrainWindows)
        {
            // 드래그 출발 창과 현재 대상 창은 LRU 교체에서 제외한다.
            var switchedWin = trainWindowManager.OpenOrFocusWagon(_index, session, true, drag?.Source, dragView);
            if (!switchedWin)
            {
                return;
            }
        }
        if (!session.SelectWagon(_index))
        {
            return;
        }

        if (!UseMultiTrainWindows)
        {
            SetDragView(null);
            ActiveTrainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train));
            ActiveTrainView.SetBoard(session.Train);
        }
        RenderBoards();
        RenderValues();
        RefreshTrainBar();
        UpdateLensRanking();
        RefreshDrag();
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.SetTown(session.CityData.displayName, inProgress);
        if (RelayoutSettings.Enabled && tavernWindowView)
        {
            tavernWindowView.ResetCity(session.CityData.displayName);
            RebuildQuestOfferBoard();
        }
        if (RelayoutSettings.Enabled && marketWindowView)
        {
            marketWindowView.SetCity(session.CityData.displayName, Presentation.GetCityTradePreferences(session.CityData));
            bool open = session.IsMarketOpen && session.TryEnterMarket();
            relayoutMarketView?.SetMarketOpen(session.CanTradeInMarket);
            if (relayoutMarketView && session.Market != null)
            {
                relayoutMarketView.SetBoardSize(session.Market.Width, session.Market.Height, session.Market.BlockedCells);
            }
            marketWindowView.SetOpen(open, false);
            if (open)
            {
                RenderBoards();
            }
        }
    }

    #region 술집 대화 생성

    void OnTavernRumorRequested()
    {
        if (session == null || session.RunStatus != PuzzleRunStatus.InProgress || !tavernWindowView)
        {
            return;
        }
        tavernWindowView.SetRumor(GetTavernRumor());
    }

    bool questBoardConfigured;

    void ConfigureQuestBoardView()
    {
        var boardView = ActiveQuestBoardView;
        if (!boardView || session == null)
        {
            return;
        }
        if (!questBoardConfigured)
        {
            long dragCapacity = System.Math.Max(System.Math.Max((long)session.Train.Width * session.Train.Height,
                session.MarketCapacity), 45L);
            boardView.Initialize(45L, dragCapacity, Presentation.RumorLogQuestTab, session.ItemDefinitions,
                _showSynergies: false);
            questBoardConfigured = true;
        }
        boardView.SetBoardSize(9, 5, System.Array.Empty<Vector2Int>());
    }

    void RebuildQuestOfferBoard()
    {
        if (session == null)
        {
            return;
        }
        questOfferBoard = session.BuildQuestOfferBoard(9, 5, out var mapped, out int skippedCount);
        questOfferByInstanceId.Clear();
        foreach (var kv in mapped)
        {
            questOfferByInstanceId[kv.Key] = kv.Value;
        }
        tavernWindowView?.SetOverflowCount(skippedCount);
        ConfigureQuestBoardView();
        if (ActiveQuestBoardView && ActiveQuestBoardView.isActiveAndEnabled)
        {
            ActiveQuestBoardView.RenderPlacements(questOfferBoard, drag?.InstanceId, currentHoveredInstanceId);
        }
    }

    string GetTavernRumor()
    {
        var candidates = session.CollectRumorCandidates().ToArray();
        var preferenceCandidates = candidates.Where(_candidate => _candidate.Kind == PuzzleRumorKind.Preference).ToArray();
        if (preferenceCandidates.Length > 0)
        {
            return PickRumor(preferenceCandidates);
        }

        var rareCandidates = candidates.Where(_candidate => _candidate.Kind == PuzzleRumorKind.RareDemand).ToArray();
        if (rareCandidates.Length > 0)
        {
            return PickRumor(rareCandidates);
        }

        var candidatesByKind = candidates
            .Where(_candidate => _candidate.Kind != PuzzleRumorKind.Preference &&
                _candidate.Kind != PuzzleRumorKind.RareDemand)
            .GroupBy(_candidate => _candidate.Kind)
            .Select(_group => _group.ToArray())
            .ToArray();
        if (candidatesByKind.Length == 0)
        {
            return Presentation.TavernNoRumor;
        }

        return PickRumor(candidatesByKind[Random.Range(0, candidatesByKind.Length)]);
    }

    string PickRumor(PuzzleRumorCandidate[] _candidates)
    {
        var selected = _candidates[Random.Range(0, _candidates.Length)];
        string message = Presentation.TavernRumor(selected);
        session.RecordTavernRumor(selected, message);
        return message;
    }

    #endregion

    // 시계가 흐른 뒤의 가벼운 갱신. 도시·보드 크기는 그대로이므로 다시 잡지 않고, 화물 표시·가치·상태 글자만 맞춘다.
    // 떠 있는 툴팁과 상세 정보는 닫지 않고 RefreshHover가 새 진행도로 다시 그린다.
    void RefreshAfterTimeAdvance()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.ShowTime(session.Time);
        RefreshFunds();
        cityView.ShowTradeStatus(inProgress
            ? session.IsMarketOpen ? string.Empty : Presentation.PuzzleNightMarket
            : Presentation.PuzzleRunStatusText(session.RunStatus));
        marketView.SetMarketOpen(session.CanTradeInMarket);
        if (relayoutMarketView)
        {
            relayoutMarketView.SetMarketOpen(session.CanTradeInMarket);
        }
        if (RelayoutSettings.Enabled && marketWindowView && !session.CanTradeInMarket && marketWindowView.IsOpen)
        {
            marketWindowView.SetOpen(false, false);
        }
        RefreshWaitAction();
        SyncMarketBoard();
        RenderValues();
        RefreshTrainBar();
    }

    // 이동(통과 포함)·개장 보충으로 시장이 바뀌면 보드 크기·화물·도시 제목을 새 시장으로 다시 잡는다.
    // 이동 연출 중에는 RefreshHover가 보드를 그리지 않으므로 여기서 직접 그려야 이전 시장이 남지 않는다.
    void SyncMarketBoard()
    {
        if (shownMarket == session.Market)
        {
            return;
        }

        shownMarket = session.Market;
        cityView.ShowCity(Presentation.PuzzleCityTitle(session.CityData, session.RegionData));
        marketView.SetBoardSize(session.Market.Width, session.Market.Height, session.Market.BlockedCells);
        if (relayoutMarketView)
        {
            relayoutMarketView.SetBoardSize(session.Market.Width, session.Market.Height, session.Market.BlockedCells);
        }
        marketWindowView?.SetCity(session.CityData.displayName, Presentation.GetCityTradePreferences(session.CityData));
        if (currentHoveredInstanceId.HasValue && !session.Train.Placements.Any(_item => _item.InstanceId == currentHoveredInstanceId.Value))
        {
            currentHoveredInstanceId = null;
            if (tooltipView)
            {
                tooltipView.Close();
            }
        }
        marketView.ShowItemDetails(null, default, default, default);
        relayoutMarketView?.ShowItemDetails(null, default, default, default);
        RenderBoards(currentHoveredInstanceId);
    }

    // 역 시설 진입은 시간을 쓰지 않아 TimeAdvanced가 오지 않는다. 진입이 확정되면 시장 보드의 거래 가능 여부만 바로 다시 그린다.
    void OnPuzzleActionCommitted(GameEvents.PuzzleActionInfo _info)
    {
        if (_info.session != session || !initialized ||
            (_info.action != PuzzleActionKind.EnterMarket && _info.action != PuzzleActionKind.EnterTavern &&
            _info.action != PuzzleActionKind.EnterWagonFacility))
        {
            return;
        }
        marketView.SetMarketOpen(session.CanTradeInMarket);
    }

    // 영업 기간이 끝나 자동 결산되면 결과 패널을 연다(승리·패배 모두).
    void OnRunSettled(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session != session || !initialized)
        {
            return;
        }
        if (drag != null)
        {
            input.CancelCapture();
            ClearDrag();
        }
        if (RelayoutSettings.Enabled)
        {
            marketWindowView?.SetOpen(false, false);
            lensWindowView?.SetOpen(false, false);
            notebookWindowView?.FloatingWindow?.SetOpen(false, false);
        }
        RenderBoards();
        cityView.SetTown(session.CityData.displayName, false);
        RefreshFunds();
        cityView.ShowTradeStatus(Presentation.PuzzleRunStatusText(session.RunStatus));
        RefreshTrainBar();
        var runEnd = ResolveRunEndPresenter();
        if (runEnd)
        {
            runEnd.Show(session);
        }
        else
        {
            Debug.LogError("RunEndPresenter를 찾을 수 없어 결산 모달을 열 수 없습니다.", this);
        }
    }

    // 무한 모드로 이어 가면 진행 중 표시로 되돌린다. 마감이 사라져 시계의 남은 기간 표시는 숨는다.
    void OnRunContinued(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session != session || !initialized)
        {
            return;
        }
        cityView.SetTown(session.CityData.displayName, true);
        cityView.ShowEndless();
        RefreshAfterTimeAdvance();
        RenderBoards();
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        // 시간은 이동·휴식·작업 확정으로만 흐르므로 보통 드래그가 없다. 남아 있으면 안전하게 정리한다.
        if (drag != null)
        {
            input.CancelCapture();
            ClearDrag();
        }
        RefreshAfterTimeAdvance();
        pendingSettlementValue = _info.cargoValueBeforeSettlement;
        pendingFreshnessLossValue = _info.cargoValueBeforeFreshnessLoss;
        PlayPendingSettlement();
        if (session.RunStatus != PuzzleRunStatus.InProgress)
        {
            RenderBoards();
            cityView.SetTown(session.CityData.displayName, false);
            cityView.ShowTradeStatus(Presentation.PuzzleRunStatusText(session.RunStatus));
            if (session.RunStatus == PuzzleRunStatus.Lost)
            {
                var runEnd = ResolveRunEndPresenter();
                if (runEnd)
                {
                    runEnd.Show(session);
                }
                else
                {
                    Debug.LogError("RunEndPresenter를 찾을 수 없어 패배 결산 모달을 열 수 없습니다.", this);
                }
            }
        }
        else
        {
            // 떠 있던 툴팁이 새로 흐른 시간의 진행도와 대기 상태를 반영한다.
            RefreshHover();
        }
    }

    void OnTravelPreviewChanged(GameEvents.TravelPreviewInfo _info)
    {
        if (_info.session == session)
        {
            RefreshWaitAction();
            RefreshHover();
        }
    }

    void RefreshWaitAction()
    {
        cityView.ShowWaitAction(session.CanWaitForDaylight);
    }

    void WaitForDaylight()
    {
        if (!MarketOperationsBlocked && session.CanWaitForDaylight)
        {
            // 아침(낮이 시작하는 턴)까지 쉰다.
            if (!session.Rest((int)session.Time.TurnsUntilDaylight))
            {
                cityView.ShowTradeStatus(Presentation.PuzzleTimeOverflow);
            }
        }
    }

    public void ClearGame()
    {
        var runEnd = ResolveRunEndPresenter();
        if (!runEnd)
        {
            Debug.LogError("RunEndPresenter를 찾을 수 없어 결산 모달을 열 수 없습니다.", this);
            return;
        }
        if (!initialized || !isActiveAndEnabled || MarketOperationsBlocked || !runEnd.CanShow ||
            !session.TryFinishRun())
        {
            return;
        }

        runEnd.Show(session);
        cityView.SetTown(session.CityData.displayName, false);
        RefreshFunds();
        cityView.ShowTradeStatus(Presentation.PuzzleRunStatusText(session.RunStatus));
        RefreshTrainBar();
    }

    RunEndPresenter ResolveRunEndPresenter()
    {
        if (!runEndPresenter)
        {
            runEndPresenter = SceneServices.Find<RunEndPresenter>(gameObject.scene);
        }
        return runEndPresenter;
    }

    void RefreshFunds()
    {
        if (TrainOperationsBlocked)
        {
            pendingFundsRefresh = true;
            return;
        }
        pendingFundsRefresh = false;
        cityView.ShowFunds(session.Funds, session.CanFinishRun);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == Presentation.PUZZLE_WAGON_SHOP_PANEL_NAME && !_info.open)
        {
            ClearWagonSalePreview();
        }
        if (_info.panel == Presentation.PUZZLE_TRAVEL_PANEL_NAME)
        {
            isTravelOpen = _info.open;
            if (_info.open)
            {
                if (trainBarView)
                {
                    trainBarView.CancelWagonDrag();
                }
                hoverPosition = null;
                currentHoveredInstanceId = null;
                if (input)
                {
                    input.CancelCapture();
                }
                ClearDrag();
                if (tooltipView)
                {
                    tooltipView.Close();
                }
                if (RelayoutSettings.Enabled)
                {
                    marketWindowView?.SetOpen(false, false);
                    lensWindowView?.SetOpen(false, false);
                }
                RenderBoards();
                ShowItemDetails(null, null);
            }
            else if (RelayoutSettings.Enabled && marketWindowView && session != null && session.CanTradeInMarket)
            {
                marketWindowView.SetOpen(true, false);
            }
            return;
        }

        if (_info.panel != Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            return;
        }
        isMapOpen = _info.open;
        RefreshWagonRefrigeration();
        if (_info.open)
        {
            // 지도가 열려 있어도 기차 보드 드래그·툴팁은 허용하므로,
            // 시장·작업대 보드에서 시작된 드래그만 취소한다.
            if (drag != null && drag.Source != session.Train)
            {
                if (input)
                {
                    input.CancelCapture();
                }
                ClearDrag();
            }
            // 툴팁이 기차 보드가 아닌 시장/작업대 아이템을 표시하고 있었다면 닫는다.
            if (currentHoveredInstanceId.HasValue && !session.Train.Placements.Any(p => p.InstanceId == currentHoveredInstanceId.Value))
            {
                hoverPosition = null;
                currentHoveredInstanceId = null;
                if (tooltipView)
                {
                    tooltipView.Close();
                }
                ShowItemDetails(null, null);
            }
            RenderBoards();
        }
        else
        {
            PlayPendingSettlement();
            if (pendingFundsRefresh)
            {
                RefreshFunds();
            }
            RefreshTrainBar();
        }
    }

    void PlayPendingSettlement()
    {
        if (TrainOperationsBlocked || (!pendingSettlementValue.HasValue && !pendingFreshnessLossValue.HasValue))
        {
            return;
        }

        ActiveTrainView.ShowCargoValueFeedback(pendingSettlementValue, pendingFreshnessLossValue,
            session.Train.Placements.Sum(item => item.FreshnessAdjustedValue), session.Train.Value.BaseValue);
        pendingSettlementValue = null;
        pendingFreshnessLossValue = null;
    }

    void ShowCurrentCity()
    {
        HideLens();
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        shownMarket = session.Market;
        cityView.ShowCity(Presentation.PuzzleCityTitle(session.CityData, session.RegionData));
        cityView.ShowTime(session.Time);
        RefreshFunds();
        cityView.ShowTradeStatus(inProgress
            ? session.IsMarketOpen ? string.Empty : Presentation.PuzzleNightMarket
            : Presentation.PuzzleRunStatusText(session.RunStatus));
        marketView.SetMarketOpen(session.CanTradeInMarket);
        if (relayoutMarketView)
        {
            relayoutMarketView.SetMarketOpen(session.CanTradeInMarket);
        }
        if (RelayoutSettings.Enabled && marketWindowView && !session.CanTradeInMarket && marketWindowView.IsOpen)
        {
            marketWindowView.SetOpen(false, false);
        }
        RefreshWaitAction();
        marketView.SetBoardSize(session.Market.Width, session.Market.Height, session.Market.BlockedCells);
        if (relayoutMarketView)
        {
            relayoutMarketView.SetBoardSize(session.Market.Width, session.Market.Height, session.Market.BlockedCells);
        }
        ActiveTrainView.SetBoard(session.Train);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

    void OnPressed(PointerEventData _eventData)
    {
        if (TrainOperationsBlocked || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        var position = _eventData.position;
        hoverPosition = position;
        if (!TryGetBoard(position, out var sourceView, out var source, out var boardCell) ||
            !source.TryGetCell(boardCell, out var placement, out var cell))
        {
            return;
        }

        drag = new PuzzleInventoryDrag(placement, cell);
        HasActiveDrag = true;
        sourceView.ShowOriginalOutline(drag.OriginalPose);
        cityView.ShowTradeStatus(string.Empty);
        pointerPosition = position;
        input.CapturePointer(_eventData.pointerId);
        currentHoveredInstanceId = null;
        if (tooltipView)
        {
            tooltipView.Close();
        }
        RenderBoards();
        ShowItemDetails(sourceView, placement);
        BeginLens(placement);
        RefreshDrag();
    }

    void OnQuickTransferRequested(PointerEventData _eventData)
    {
        if (MarketOperationsBlocked || drag != null || session.RunStatus != PuzzleRunStatus.InProgress ||
            !(UseMultiTrainWindows ? trainWindowManager.HasAnyOpenWindow : (ActiveTrainView && ActiveTrainView.isActiveAndEnabled)) ||
            !TryGetBoard(_eventData.position, out _, out var source, out var boardCell) ||
            !source.TryGetCell(boardCell, out var placement, out _))
        {
            return;
        }

        var otherBoard = QuickTransferPartner();
        if (otherBoard == null || (source != session.Train && source != otherBoard))
        {
            return;
        }

        var destination = source == session.Train ? otherBoard : session.Train;
        hoverPosition = _eventData.position;
        var result = session.QuickTransferTo(placement, destination, placement.Pose.ClockwiseTurns);
        if (result.CanDrop)
        {
            RefreshActionBoardsAfter(source, destination);
            RenderValues();
            RefreshFunds();
            RefreshTrainBar();
        }
        RefreshHover();
        cityView.ShowTradeStatus(WithProficiencyNotice(Presentation.PuzzleTradeStatus(result, placement.Definition, _committed: true)));
    }

    PuzzleInventory QuickTransferPartner()
    {
        if (ButcheryViewReady)
        {
            return session.ButcheryBoard();
        }
        if (ModificationViewReady)
        {
            return session.ModificationBoard();
        }
        if (RefiningViewReady)
        {
            return session.RefiningBoard();
        }
        return ActiveMarketView && ActiveMarketView.isActiveAndEnabled ? session.Market : null;
    }

    void OnDragged(PointerEventData _eventData)
    {
        if (TrainOperationsBlocked)
        {
            return;
        }

        pointerPosition = _eventData.position;
        hoverPosition = _eventData.position;
        RefreshDrag();
    }

    void OnRotated()
    {
        if (TrainOperationsBlocked || drag == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        drag.RotateClockwise();
        UpdateLensRanking();
        RefreshDrag();
    }

    void OnReleased(PointerEventData _eventData)
    {
        if (TrainOperationsBlocked)
        {
            ClearDrag();
            return;
        }

        hoverPosition = _eventData.position;
        var activeDrag = drag;
        PuzzleDropResult result = default;
        int droppedWagonIndex = -1;
        try
        {
            if (TryGetBoard(_eventData.position, out _, out var destination, out var targetCell))
            {
                if (questOfferBoard != null && ReferenceEquals(activeDrag.Source, questOfferBoard))
                {
                    if (questOfferByInstanceId.TryGetValue(activeDrag.InstanceId, out var offer))
                    {
                        if (session.IsTrainWagon(destination))
                        {
                            var failure = session.TryAcceptQuestAt(offer, destination, activeDrag.GrabbedCell, targetCell,
                                activeDrag.ClockwiseTurns);
                            if (failure == TrainventoryQuestAcceptFailure.None)
                            {
                                RebuildQuestOfferBoard();
                                RenderValues();
                                RefreshFunds();
                                RefreshTrainBar();
                                statusBannerView?.ShowStatus(Presentation.QuestOffer(offer));
                            }
                            else
                            {
                                statusBannerView?.ShowStatus(Presentation.QuestAcceptFailure(failure, offer, session.MaxActiveQuests));
                            }
                        }
                        else if (!ReferenceEquals(destination, questOfferBoard))
                        {
                            statusBannerView?.ShowStatus(Presentation.QuestAcceptFailure(
                                TrainventoryQuestAcceptFailure.NoSpace, offer, session.MaxActiveQuests));
                        }
                    }
                    return;
                }
                if (questOfferBoard != null && ReferenceEquals(destination, questOfferBoard))
                {
                    if (session.IsTrainWagon(activeDrag.Source))
                    {
                        if (session.TryCompleteQuestWithCargo(activeDrag.OriginalPlacement, out string failReason))
                        {
                            RebuildQuestOfferBoard();
                            RenderValues();
                            RefreshFunds();
                            RefreshTrainBar();
                            statusBannerView?.ShowStatus(Presentation.QuestCompleteLabel);
                        }
                        else if (!string.IsNullOrEmpty(failReason))
                        {
                            statusBannerView?.ShowStatus(failReason);
                        }
                    }
                    else
                    {
                        statusBannerView?.ShowStatus(Presentation.QuestDropNotQuestCargo);
                    }
                    return;
                }
                result = activeDrag.DropOnBoard(session, destination, targetCell);
                if (result.CanDrop)
                {
                    RefreshActionBoardsAfter(activeDrag.Source, destination);
                    RenderValues();
                    RefreshFunds();
                    RefreshTrainBar();
                }
            }
            else if (questOfferBoard != null && ReferenceEquals(activeDrag.Source, questOfferBoard))
            {
                if (questOfferByInstanceId.TryGetValue(activeDrag.InstanceId, out var offer))
                {
                    statusBannerView?.ShowStatus(Presentation.QuestAcceptFailure(
                        TrainventoryQuestAcceptFailure.NoSpace, offer, session.MaxActiveQuests));
                }
            }
            else if (trainBarView && trainBarView.TryGetWagonAtScreenPosition(_eventData.position, out int candidateWagon) &&
                     CanQuickDropToWagon(candidateWagon))
            {
                droppedWagonIndex = candidateWagon;
                result = activeDrag.QuickDropToWagon(session, droppedWagonIndex);
                if (result.CanDrop)
                {
                    RefreshActionBoardsAfter(activeDrag.Source, session.TrainWagons[droppedWagonIndex]);
                    RenderValues();
                    RefreshFunds();
                    RefreshTrainBar();
                }
            }
        }
        finally
        {
            ClearDrag();
        }
        FinishLens(result);
        if (result.Direction != PuzzleTradeDirection.None)
        {
            cityView.ShowTradeStatus(WithProficiencyNotice(Presentation.PuzzleTradeStatus(result, activeDrag.Definition, _committed: true)));
        }
        else if (droppedWagonIndex >= 0 && result.Placement.Failure == PuzzlePlacementFailure.NoSpace)
        {
            cityView.ShowTradeStatus($"{activeDrag.Definition.DisplayName} · {session.TrainWagons[droppedWagonIndex].WagonNumber}번 칸에 화물을 놓을 공간이 없습니다");
        }
    }

    static readonly Vector2Int[] ADJACENT_OFFSETS =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    PuzzleInventory lastSynergyPreviewDestination;
    Vector2Int lastSynergyPreviewCell = new Vector2Int(int.MinValue, int.MinValue);
    int lastSynergyPreviewRotation = -1;
    readonly List<(PuzzlePlacement neighbor, decimal deltaPercent)> cachedSynergyDeltas = new();

    void CalculateAdjacentSynergyDeltas(
        PuzzleInventory _destination,
        PuzzlePose _candidatePose,
        PuzzleInventoryDrag _drag,
        List<(PuzzlePlacement neighbor, decimal deltaPercent)> _results)
    {
        _results.Clear();
        if (_destination == null || _candidatePose == null || _drag == null)
        {
            return;
        }

        string wasteTag = TableManager.Instance.PuzzleSynergy?.animals?.wasteTag;
        var scope = session.IsTrainWagon(_destination) ? PuzzleSynergyScope.Train : PuzzleSynergyScope.Market;
        var rules = TableManager.Instance.SynergiesFor(scope);
        var adjacentRules = rules.Where(r => r.spatialRange == nameof(PuzzleSynergyRange.Adjacent)).ToArray();
        if (adjacentRules.Length == 0)
        {
            return;
        }

        bool sameBoard = ReferenceEquals(_drag.Source, _destination);
        int draggedInstanceId = _drag.InstanceId;

        var neighborPlacements = new HashSet<PuzzlePlacement>();
        var candidateCellSet = new HashSet<Vector2Int>();
        foreach (var cell in _candidatePose.Cells)
        {
            candidateCellSet.Add(cell.Position);
        }

        foreach (var cellPos in candidateCellSet)
        {
            foreach (var offset in ADJACENT_OFFSETS)
            {
                var neighborPos = cellPos + offset;
                if (candidateCellSet.Contains(neighborPos))
                {
                    continue;
                }
                if (_destination.TryGetCell(neighborPos, out var neighbor, out _) &&
                    neighbor.InstanceId != draggedInstanceId)
                {
                    neighborPlacements.Add(neighbor);
                }
            }
        }

        if (sameBoard && _drag.OriginalPose != null)
        {
            var origCellSet = new HashSet<Vector2Int>();
            foreach (var cell in _drag.OriginalPose.Cells)
            {
                origCellSet.Add(cell.Position);
            }
            foreach (var cellPos in origCellSet)
            {
                foreach (var offset in ADJACENT_OFFSETS)
                {
                    var neighborPos = cellPos + offset;
                    if (origCellSet.Contains(neighborPos))
                    {
                        continue;
                    }
                    if (_destination.TryGetCell(neighborPos, out var neighbor, out _) &&
                        neighbor.InstanceId != draggedInstanceId)
                    {
                        neighborPlacements.Add(neighbor);
                    }
                }
            }
        }

        foreach (var neighbor in neighborPlacements)
        {
            decimal newPercent = 0;
            foreach (var nCell in neighbor.Pose.Cells)
            {
                foreach (var offset in ADJACENT_OFFSETS)
                {
                    if (candidateCellSet.Contains(nCell.Position + offset))
                    {
                        foreach (var rule in adjacentRules)
                        {
                            if (PuzzlePieceEffects.IsTarget(neighbor.Definition, rule, wasteTag) &&
                                PuzzlePieceEffects.IsSource(_drag.Definition, rule))
                            {
                                newPercent += rule.multiplierPercent - PuzzleInventoryValuation.BASE_PERCENT +
                                    session.SynergyComboBonusPercent(rule);
                            }
                        }
                    }
                }
            }

            decimal oldPercent = 0;
            if (sameBoard && _drag.OriginalPose != null)
            {
                var origCellSet = new HashSet<Vector2Int>(_drag.OriginalPose.Cells.Select(c => c.Position));
                foreach (var nCell in neighbor.Pose.Cells)
                {
                    foreach (var offset in ADJACENT_OFFSETS)
                    {
                        if (origCellSet.Contains(nCell.Position + offset))
                        {
                            foreach (var rule in adjacentRules)
                            {
                                if (PuzzlePieceEffects.IsTarget(neighbor.Definition, rule, wasteTag) &&
                                    PuzzlePieceEffects.IsSource(_drag.Definition, rule))
                                {
                                    oldPercent += rule.multiplierPercent - PuzzleInventoryValuation.BASE_PERCENT +
                                        session.SynergyComboBonusPercent(rule);
                                }
                            }
                        }
                    }
                }
            }

            decimal delta = newPercent - oldPercent;
            if (delta != 0)
            {
                _results.Add((neighbor, delta));
            }
        }
    }

    void RefreshDrag()
    {
        if (drag == null)
        {
            return;
        }

        var sourceView = ViewFor(drag.Source);
        if (!IsOnScreen(pointerPosition))
        {
            SetDragView(null);
            if (sourceView)
            {
                sourceView.ClearDrag();
            }
            cityView.ShowTradeStatus(string.Empty);
            return;
        }

        if (sourceView)
        {
            sourceView.ShowOriginalOutline(drag.OriginalPose);
        }

        if (TryGetBoard(pointerPosition, out var targetView, out var destination, out var targetCell))
        {
            if (questOfferBoard != null && ReferenceEquals(drag.Source, questOfferBoard))
            {
                bool canAccept = session.IsTrainWagon(destination) &&
                    questOfferByInstanceId.TryGetValue(drag.InstanceId, out var offer) &&
                    session.EvaluateQuestAcceptAt(offer, destination, drag.GrabbedCell, targetCell, drag.ClockwiseTurns) ==
                    TrainventoryQuestAcceptFailure.None;
                var questPose = destination.CreatePoseAtSlot(drag.Definition.Cells, drag.GrabbedCell, targetCell,
                    drag.ClockwiseTurns, out var pose) == PuzzlePlacementFailure.None
                    ? pose
                    : null;
                SetDragView(targetView);
                if (questPose != null)
                {
                    targetView.ShowGhostOnBoard(questPose, drag.Definition, drag.FreshnessPercent);
                }
                else
                {
                    targetView.ShowGhostAtPointer(drag.GetGhostPose(), drag.Definition, Vector2Int.zero, pointerPosition, drag.FreshnessPercent);
                }
                targetView.ShowPreview(questPose, canAccept);
                if (targetView != sourceView && sourceView)
                {
                    sourceView.HideGhost();
                    sourceView.ShowPreview(null, false);
                }
                return;
            }
            if (questOfferBoard != null && ReferenceEquals(destination, questOfferBoard))
            {
                bool canDeliver = session.IsTrainWagon(drag.Source) &&
                    session.CanCompleteQuestCargoHere(drag.OriginalPlacement, out _);
                SetDragView(targetView);
                targetView.ShowGhostAtPointer(drag.GetGhostPose(), drag.Definition, Vector2Int.zero, pointerPosition, drag.FreshnessPercent);
                targetView.ShowPreview(null, canDeliver);
                if (targetView != sourceView && sourceView)
                {
                    sourceView.HideGhost();
                    sourceView.ShowPreview(null, false);
                }
                return;
            }
            var preview = drag.Evaluate(session, destination, targetCell);
            SetDragView(targetView);
            var candidate = preview.Placement.Candidate;
            if (candidate != null)
            {
                targetView.ShowGhostOnBoard(candidate, drag.Definition, drag.FreshnessPercent);
            }
            else
            {
                targetView.ShowGhostAtPointer(drag.GetGhostPose(), drag.Definition, Vector2Int.zero, pointerPosition, drag.FreshnessPercent);
            }
            if (targetView != lastDragQuoteView || drag.InstanceId != lastDragQuoteInstanceId)
            {
                lastDragQuoteView = targetView;
                lastDragQuoteInstanceId = drag.InstanceId;
                cachedDragQuoteText = targetView == ActiveMarketView && session.IsTrainWagon(drag.Source) &&
                    session.TryQuoteTrade(drag.OriginalPlacement, PuzzleTradeDirection.Sell, out long sellPrice)
                    ? Presentation.MarketItemPrice(sellPrice)
                    : null;
            }
            targetView.ShowGhostPrice(cachedDragQuoteText);
            targetView.ShowPreview(candidate, preview.CanDrop);
            // 고스트는 놓을 후보 보드에만 둔다. 출발 보드에는 원래 자리 외곽선과 광선만 남긴다.
            if (targetView != sourceView && sourceView)
            {
                sourceView.HideGhost();
                sourceView.ShowPreview(null, false);
            }

            Vector2 sourceCenterScreen = sourceView ? sourceView.GetShapeCenterScreen(drag.OriginalPose.Cells) : pointerPosition;
            Vector2 ghostCenterScreen = candidate != null ? targetView.GetShapeCenterScreen(candidate.Cells) : pointerPosition;
            if (sourceView)
            {
                sourceView.ShowDragBeam(sourceCenterScreen, ghostCenterScreen);
            }
            if (targetView != sourceView)
            {
                targetView.HideDragBeam();
            }

            if (preview.CanDrop && candidate != null)
            {
                if (destination != lastSynergyPreviewDestination || targetCell != lastSynergyPreviewCell || drag.ClockwiseTurns != lastSynergyPreviewRotation)
                {
                    lastSynergyPreviewDestination = destination;
                    lastSynergyPreviewCell = targetCell;
                    lastSynergyPreviewRotation = drag.ClockwiseTurns;
                    CalculateAdjacentSynergyDeltas(destination, candidate, drag, cachedSynergyDeltas);
                }
                targetView.ShowAdjacentSynergyPreview(cachedSynergyDeltas);
            }
            else
            {
                targetView.HideAdjacentSynergyPreview();
            }
            if (targetView != sourceView && sourceView)
            {
                sourceView.HideAdjacentSynergyPreview();
            }

            UpdateLensSpot(destination, targetCell, preview.CanDrop && candidate != null);
            cityView.ShowTradeStatus(Presentation.PuzzleTradeStatus(preview, drag.Definition, _committed: false));
            return;
        }

        SetDragView(sourceView);
        if (sourceView)
        {
            sourceView.ShowGhostAtPointer(drag.GetGhostPose(), drag.Definition, Vector2Int.zero, pointerPosition, drag.FreshnessPercent);
            sourceView.ShowPreview(null, false);
            sourceView.HideAdjacentSynergyPreview();
            Vector2 sourceCenterScreen = sourceView.GetShapeCenterScreen(drag.OriginalPose.Cells);
            sourceView.ShowDragBeam(sourceCenterScreen, pointerPosition);
        }
        UpdateLensSpot(null, default, false);
        cityView.ShowTradeStatus(string.Empty);
    }

    void SetDragView(PuzzleInventoryView _next)
    {
        if (dragView == _next)
        {
            return;
        }

        var prev = dragView;
        var sourceView = drag != null ? ViewFor(drag.Source) : null;
        if (prev)
        {
            if (prev != sourceView)
            {
                prev.ClearDrag();
            }
            prev.HidePlacementCandidates();
        }
        dragView = _next;
        if (drag != null && _next)
        {
            UpdateLensRanking();
        }
    }

    bool TryGetBoard(Vector2 _position, out PuzzleInventoryView _view, out PuzzleInventory _inventory, out Vector2Int _cell)
    {
        if (IsOnScreen(_position))
        {
            if (UseMultiTrainWindows)
            {
                if (trainWindowManager.TryGetWindowAtScreenPosition(_position, null, out var tw, out _inventory, out _cell))
                {
                    _view = tw ? tw.InventoryView : null;
                    return _view != null;
                }
            }
            else if (ActiveTrainView && ActiveTrainView.isActiveAndEnabled && ActiveTrainView.TryGetBoardCell(_position, out _cell))
            {
                _view = ActiveTrainView;
                _inventory = session.Train;
                return true;
            }
            if (!isMapOpen)
            {
                if (session.CanTradeInMarket && ActiveMarketView && ActiveMarketView.isActiveAndEnabled && ActiveMarketView.TryGetBoardCell(_position, out _cell))
                {
                    _view = ActiveMarketView;
                    _inventory = session.Market;
                    return true;
                }
                if (ActiveQuestBoardView && ActiveQuestBoardView.isActiveAndEnabled && questOfferBoard != null &&
                    ActiveQuestBoardView.TryGetBoardCell(_position, out _cell))
                {
                    _view = ActiveQuestBoardView;
                    _inventory = questOfferBoard;
                    return true;
                }
                if (ButcheryViewReady && ActiveButcheryView.TryGetBoardCell(_position, out _cell))
                {
                    _view = ActiveButcheryView;
                    _inventory = session.ButcheryBoard();
                    return true;
                }
                if (ModificationViewReady && ActiveModificationView.TryGetBoardCell(_position, out _cell))
                {
                    _view = ActiveModificationView;
                    _inventory = session.ModificationBoard();
                    return true;
                }
                if (RefiningViewReady && ActiveRefiningView.TryGetBoardCell(_position, out _cell))
                {
                    _view = ActiveRefiningView;
                    _inventory = session.RefiningBoard();
                    return true;
                }
            }
        }

        _view = null;
        _inventory = null;
        _cell = default;
        return false;
    }

    void OnHovered(Vector2 _position)
    {
        if (TrainOperationsBlocked)
        {
            return;
        }

        hoverPosition = _position;
        if (drag != null)
        {
            return;
        }

        if (TryGetBoard(_position, out var view, out var board, out var boardCell) &&
            board.TryGetCell(boardCell, out var placement, out _))
        {
            ClearKeywordHover(_closeSharedTooltip: false);
            bool tooltipClosed = tooltipView && (!tooltipView.IsOpen || !tooltipView.gameObject.activeInHierarchy);
            if (currentHoveredInstanceId != placement.InstanceId || tooltipClosed)
            {
                currentHoveredInstanceId = placement.InstanceId;
                RenderBoards(placement.InstanceId);
                ShowItemDetails(view, placement, _position);
            }
            else if (tooltipView)
            {
                tooltipView.SetPointerPosition(_position);
            }
            return;
        }

        if (currentHoveredInstanceId.HasValue || (tooltipView && tooltipView.IsOpen && currentHoveredKeywordId == null))
        {
            currentHoveredInstanceId = null;
            RenderBoards();
            if (currentHoveredKeywordId == null)
            {
                ShowItemDetails(null, null);
            }
            else
            {
                ClearBoardItemDetails();
            }
        }
    }

    void OnHoverEnded()
    {
        if (TrainOperationsBlocked)
        {
            return;
        }

        hoverPosition = null;
        currentHoveredInstanceId = null;
        RenderBoards();
        if (currentHoveredKeywordId == null)
        {
            ShowItemDetails(null, null);
        }
        else
        {
            ClearBoardItemDetails();
        }
    }

    void RefreshHover()
    {
        if (TrainOperationsBlocked || drag != null)
        {
            currentHoveredInstanceId = null;
            ClearKeywordHover();
            if (tooltipView)
            {
                tooltipView.Close();
            }
            return;
        }

        if (hoverPosition is Vector2 position && TryGetBoard(position, out var view, out var board, out var boardCell) &&
            board.TryGetCell(boardCell, out var placement, out _))
        {
            ClearKeywordHover(_closeSharedTooltip: false);
            currentHoveredInstanceId = placement.InstanceId;
            RenderBoards(placement.InstanceId);
            ShowItemDetails(view, placement, position);
            return;
        }

        currentHoveredInstanceId = null;
        RenderBoards();
        if (currentHoveredKeywordId == null)
        {
            ShowItemDetails(null, null);
        }
        else
        {
            ClearBoardItemDetails();
        }
    }

    void UpdateKeywordHover(Vector2? _screenPosition)
    {
        if (!_screenPosition.HasValue || currentHoveredInstanceId.HasValue || !marketView ||
            !marketView.TryGetHoveredBoardInfoLink(_screenPosition.Value, out string linkId) ||
            !Presentation.TryGetTradeKeywordTooltip(linkId, out string title, out string description))
        {
            ClearKeywordHover();
            return;
        }

        var targetTooltip = KeywordTooltipView;
        if (!targetTooltip)
        {
            ClearKeywordHover();
            return;
        }

        Vector2 pos = _screenPosition.Value;
        if (currentHoveredKeywordId == linkId)
        {
            if (!targetTooltip.IsOpen || !targetTooltip.gameObject.activeInHierarchy)
            {
                targetTooltip.ShowKeyword(title, description, pos);
            }
            else
            {
                targetTooltip.SetPointerPosition(pos);
            }
            return;
        }

        currentHoveredKeywordId = linkId;
        targetTooltip.ShowKeyword(title, description, pos);
    }

    void ClearKeywordHover(bool _closeSharedTooltip = true)
    {
        if (currentHoveredKeywordId == null)
        {
            return;
        }

        currentHoveredKeywordId = null;
        var targetTooltip = KeywordTooltipView;
        if (targetTooltip && (_closeSharedTooltip || targetTooltip != tooltipView))
        {
            targetTooltip.Close();
        }
    }

    void ClearBoardItemDetails()
    {
        if (ActiveTrainView)
        {
            ActiveTrainView.ShowItemDetails(null, default, default, default);
        }
        if (marketView)
        {
            marketView.ShowItemDetails(null, default, default, default);
        }
        if (butcheryView)
        {
            butcheryView.ShowItemDetails(null, default, default, default);
        }
        if (modificationView)
        {
            modificationView.ShowItemDetails(null, default, default, default);
        }
        if (refiningView)
        {
            refiningView.ShowItemDetails(null, default, default, default);
        }
    }

    void ShowItemDetails(PuzzleInventoryView _view, PuzzlePlacement _placement, Vector2? _position = null)
    {
        if (_placement == null)
        {
            currentHoveredInstanceId = null;
            if (tooltipView)
            {
                tooltipView.Close();
            }
            if (ActiveTrainView)
            {
                ActiveTrainView.ShowItemDetails(null, default, default, default);
            }
            if (marketView)
            {
                marketView.ShowItemDetails(null, default, default, default);
            }
            if (butcheryView)
            {
                butcheryView.ShowItemDetails(null, default, default, default);
            }
            if (modificationView)
            {
                modificationView.ShowItemDetails(null, default, default, default);
            }
            if (refiningView)
            {
                refiningView.ShowItemDetails(null, default, default, default);
            }
            return;
        }

        var item = _placement.Definition;
        var value = _placement.Owner.ValueOf(_placement.InstanceId);
        bool isActionBoard = session.IsButchery(_placement.Owner) || session.IsModification(_placement.Owner) ||
            session.IsRefining(_placement.Owner);
        bool isTrain = _view == ActiveTrainView || session.IsTrainWagon(_placement.Owner);
        decimal actualValue = _placement.FreshnessAdjustedValue;
        var rate = item == null ? default : session.RateFor(item);
        (IReadOnlyList<string> General, IReadOnlyList<PuzzleTooltipEffectEntry> Synergies) effects =
            BuildEffectDescriptions(_placement, isTrain, isActionBoard);
        long? previewHours = session.PreviewHours;
        long? previewActivations = null;
        decimal? futureValue = null;
        if (isTrain && previewHours.HasValue)
        {
            try
            {
                previewActivations = session.SynergyActivationsFor(previewHours.Value);
                futureValue = session.PreviewValueAfter(_placement, previewHours.Value);
            }
            catch (System.OverflowException)
            {
                futureValue = null;
            }
        }

        if (_view == ActiveQuestBoardView && _placement != null && tavernWindowView &&
            questOfferByInstanceId.TryGetValue(_placement.InstanceId, out var hoveredOffer))
        {
            tavernWindowView.SetHint(Presentation.QuestOffer(hoveredOffer));
        }

        if (tooltipView && drag == null && (_position.HasValue || hoverPosition.HasValue))
        {
            Vector2 pos = _position ?? hoverPosition.Value;
            string previewName = session.PreviewCityId == null ? null : TableManager.Instance.CityPrice(session.PreviewCityId).displayName;
            var direction = isTrain || isActionBoard ? PuzzleTradeDirection.Sell : PuzzleTradeDirection.Buy;
            long? price = session.TryQuoteTrade(_placement, direction, out long quote) ? quote : (long?)null;
            tooltipView.Show(item, actualValue, rate, effects.General, effects.Synergies, futureValue, pos, isTrain, _placement.FreshnessPercent,
                session.HoursUntilNextFreshnessBand(_placement), previewHours,
                previewName, previewActivations, price, isTrain ? PuzzleSynergyBonus.GroupByNeighbor(value.SynergyBonuses) : null,
                isTrain ? session.AppliedPreservationPercent(_placement) : 0, isActionBoard);
            // 툴팁을 열면 미확인 알림만 지운다. 실제로 대기 중인 진행도와 빨간 표시는 상태가 풀릴 때까지 남는다.
            if (_placement.HasUnreadNotice && tooltipView.IsOpen)
            {
                _placement.HasUnreadSynergySettlement = false;
                _placement.HasUnreadSpaceNotice = false;
                _placement.HasUnreadSpawnNotice = false;
                RenderBoards(_placement.InstanceId);
                RefreshTrainBar();
            }
        }

        if (UseMultiTrainWindows && trainWindowManager.Windows != null)
        {
            var wins = trainWindowManager.Windows;
            for (int i = 0; i < wins.Count; i++)
            {
                var iv = wins[i] ? wins[i].InventoryView : null;
                if (iv && wins[i].FloatingWindow && wins[i].FloatingWindow.IsOpen)
                {
                    iv.ShowItemDetails(_view == iv ? item : null, value, actualValue, rate);
                }
            }
        }
        else if (ActiveTrainView)
        {
            ActiveTrainView.ShowItemDetails(_view == ActiveTrainView ? item : null, value, actualValue, rate);
        }
        if (ActiveMarketView)
        {
            ActiveMarketView.ShowItemDetails(_view == ActiveMarketView ? item : null, value, actualValue, rate);
        }
        if (butcheryView)
        {
            butcheryView.ShowItemDetails(_view == butcheryView ? item : null, value, actualValue, rate);
        }
        if (modificationView)
        {
            modificationView.ShowItemDetails(_view == modificationView ? item : null, value, actualValue, rate);
        }
        if (refiningView)
        {
            refiningView.ShowItemDetails(_view == refiningView ? item : null, value, actualValue, rate);
        }
    }

    (IReadOnlyList<string> General, IReadOnlyList<PuzzleTooltipEffectEntry> Synergies) BuildEffectDescriptions(
        PuzzlePlacement _placement, bool _isTrain, bool _isActionBoard)
    {
        return Presentation.PuzzleTooltipPossibleEffects(
            _placement?.Definition,
            _isTrain || _isActionBoard ? PuzzleSynergyScope.Train : PuzzleSynergyScope.Market,
            _isTrain ? _placement : null,
            session);
    }

    void RenderValues()
    {
        if (UseMultiTrainWindows && trainWindowManager.Windows != null)
        {
            var wins = trainWindowManager.Windows;
            for (int i = 0; i < wins.Count; i++)
            {
                var w = wins[i];
                if (!w || !w.FloatingWindow || !w.FloatingWindow.IsOpen || !w.InventoryView) continue;
                var wagon = w.BoundWagon;
                if (wagon != null && session.IsTrainWagon(wagon))
                {
                    w.InventoryView.ShowBoardValue(wagon.Placements.Sum(_item => _item.FreshnessAdjustedValue), wagon.Value.BaseValue);
                    w.SetHeader(wagon, RemainingSettlementHours(), session.TrainSynergyDelta);
                }
            }
        }
        else if (ActiveTrainView)
        {
            ActiveTrainView.ShowBoardValue(session.Train.Placements.Sum(_item => _item.FreshnessAdjustedValue), session.Train.Value.BaseValue);
        }
        if (ActiveMarketView)
        {
            if (ActiveMarketView.HasTradePreferences)
            {
                ActiveMarketView.ShowBoardTradePreferences(Presentation.GetCityTradePreferences(session.CityData));
            }
            else
            {
                ActiveMarketView.ShowBoardInfo(Presentation.PuzzleCityTagPreferences(session.CityData));
            }
        }
    }

    bool suppressRestoreTrainWindows;

    void RenderBoards(int? _highlightedInstanceId = null)
    {
        if (UseMultiTrainWindows)
        {
            if (!suppressRestoreTrainWindows)
            {
                trainWindowManager.RestoreOrEnsureDefault(session);
            }
            trainWindowManager.RefreshAllOpenWindows(session, RemainingSettlementHours(), session.TrainSynergyDelta,
                drag?.InstanceId, _highlightedInstanceId, RefrigerationContacts);
        }
        else if (ActiveTrainView)
        {
            ActiveTrainView.RenderPlacements(session.Train, drag?.InstanceId, _highlightedInstanceId,
                RefrigerationContacts(session.Train));
        }
        if (ActiveMarketView)
        {
            cachedMarketPriceProvider ??= MarketPriceLabel;
            System.Func<PuzzlePlacement, string> priceProvider = RelayoutSettings.Enabled ? cachedMarketPriceProvider : null;
            ActiveMarketView.RenderPlacements(session.Market, drag?.InstanceId, _highlightedInstanceId,
                _slotLabelProvider: priceProvider);
        }
        if (ActiveQuestBoardView && ActiveQuestBoardView.isActiveAndEnabled && questOfferBoard != null)
        {
            ActiveQuestBoardView.RenderPlacements(questOfferBoard, drag?.InstanceId, _highlightedInstanceId);
        }
        if (ButcheryViewReady)
        {
            ActiveButcheryView.RenderPlacements(session.ButcheryBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
        if (ModificationViewReady)
        {
            ActiveModificationView.RenderPlacements(session.ModificationBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
        if (RefiningViewReady)
        {
            ActiveRefiningView.RenderPlacements(session.RefiningBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
    }

    // 냉장은 기차 객차에서만 적용되므로 객차의 접촉 관계만 표시한다. 판정은 시간 경과 계산과 같은 함수를 쓴다.
    IReadOnlyList<PuzzleRefrigerationContact> RefrigerationContacts(PuzzleInventory _wagon)
    {
        return session.IsTrainWagon(_wagon)
            ? PuzzleRefrigeration.Contacts(_wagon.Placements, TableManager.Instance.PuzzleSynergy.freshness)
            : null;
    }

    string MarketPriceLabel(PuzzlePlacement _placement)
    {
        if (session == null || _placement == null) return null;
        return session.TryQuoteTrade(_placement, PuzzleTradeDirection.Buy, out long price)
            ? Presentation.MarketItemPrice(price)
            : null;
    }

    PuzzleInventoryView ViewFor(PuzzleInventory _inventory)
    {
        if (questOfferBoard != null && ReferenceEquals(_inventory, questOfferBoard) && ActiveQuestBoardView)
        {
            return ActiveQuestBoardView;
        }
        if (session.IsButchery(_inventory) && ActiveButcheryView)
        {
            return ActiveButcheryView;
        }
        if (session.IsModification(_inventory) && ActiveModificationView)
        {
            return ActiveModificationView;
        }
        if (session.IsRefining(_inventory) && ActiveRefiningView)
        {
            return ActiveRefiningView;
        }
        if (UseMultiTrainWindows && session.IsTrainWagon(_inventory))
        {
            return trainWindowManager.GetViewForInventory(_inventory, session)?.InventoryView;
        }
        return session.IsTrainWagon(_inventory) ? ActiveTrainView : ActiveMarketView;
    }

    // 도축대 View는 한 번 구성하면 된다. 열 때마다 크기와 상태 문구만 갱신한다.
    void ConfigureButcheryView()
    {
        var view = ActiveButcheryView;
        if (!view)
        {
            return;
        }
        var board = session.ButcheryBoard();
        if (board == null)
        {
            return;
        }
        if (!butcheryConfigured)
        {
            long capacity = (long)board.Width * board.Height;
            long dragCapacity = System.Math.Max(System.Math.Max((long)session.Train.Width * session.Train.Height,
                session.MarketCapacity), capacity);
            view.Initialize(capacity, dragCapacity, Presentation.PuzzleButcheryTitle, session.ItemDefinitions,
                _showSynergies: false);
            butcheryConfigured = true;
        }
        view.SetBoardSize(board.Width, board.Height, board.BlockedCells);
    }

    void OnButcheryChanged(GameEvents.ButcheryInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        if (_info.open)
        {
            ConfigureButcheryView();
        }
        RenderBoards();
    }

    // 개조대 View도 한 번 구성하고, 열릴 때 크기와 상태 문구만 갱신한다.
    void ConfigureModificationView()
    {
        var view = ActiveModificationView;
        if (!view)
        {
            return;
        }
        var board = session.ModificationBoard();
        if (board == null)
        {
            return;
        }
        if (!modificationConfigured)
        {
            long capacity = (long)board.Width * board.Height;
            long dragCapacity = System.Math.Max(System.Math.Max((long)session.Train.Width * session.Train.Height,
                session.MarketCapacity), capacity);
            view.Initialize(capacity, dragCapacity, Presentation.PuzzleModificationTitle,
                session.ItemDefinitions, _showSynergies: false);
            modificationConfigured = true;
        }
        view.SetBoardSize(board.Width, board.Height, board.BlockedCells);
    }

    void OnModificationChanged(GameEvents.ModificationInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        if (_info.open)
        {
            ConfigureModificationView();
        }
        RenderBoards();
    }

    // 정제대 View도 한 번 구성하고, 열릴 때 크기와 상태 문구만 갱신한다.
    void ConfigureRefiningView()
    {
        var view = ActiveRefiningView;
        if (!view)
        {
            return;
        }
        var board = session.RefiningBoard();
        if (board == null)
        {
            return;
        }
        if (!refiningConfigured)
        {
            long capacity = (long)board.Width * board.Height;
            long dragCapacity = System.Math.Max(System.Math.Max((long)session.Train.Width * session.Train.Height,
                session.MarketCapacity), capacity);
            view.Initialize(capacity, dragCapacity, Presentation.PuzzleRefiningTitle,
                session.ItemDefinitions, _showSynergies: false);
            refiningConfigured = true;
        }
        view.SetBoardSize(board.Width, board.Height, board.BlockedCells);
    }

    void OnRefiningChanged(GameEvents.RefiningInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        if (_info.open)
        {
            ConfigureRefiningView();
        }
        RenderBoards();
    }

    void OnFundsChanged(GameEvents.FundsInfo _info)
    {
        if (_info.session == session)
        {
            RefreshFunds();
            RefreshTrainBar();
        }
    }

    // 확정된 이동이 도축대·개조대·정제대에 있었는지 확인해 상태를 갱신한다. ClearDrag의 다시 그리기가 이 결과를 그대로 표시한다.
    void RefreshActionBoardsAfter(PuzzleInventory _source, PuzzleInventory _destination)
    {
        session.RefreshButcheryAfterMove(_source);
        session.RefreshButcheryAfterMove(_destination);
        session.RefreshModificationAfterMove(_source);
        session.RefreshModificationAfterMove(_destination);
        session.RefreshRefiningAfterMove(_source);
        session.RefreshRefiningAfterMove(_destination);
    }

    void OnViewDisabled()
    {
        hoverPosition = null;
        HideLens();
        bool hadDrag = drag != null;
        if (input)
        {
            input.CancelCapture();
        }
        suppressRestoreTrainWindows = true;
        try
        {
            ClearDrag();
            if (!hadDrag)
            {
                RenderBoards();
                ShowItemDetails(null, null);
            }
        }
        finally
        {
            suppressRestoreTrainWindows = false;
        }
    }

    void ClearDrag()
    {
        bool hadHoveredWagon = hoveredWagonIndex.HasValue;
        hoveredWagonIndex = null;
        lastSynergyPreviewDestination = null;
        lastSynergyPreviewCell = new Vector2Int(int.MinValue, int.MinValue);
        lastSynergyPreviewRotation = -1;
        lastDragQuoteView = null;
        lastDragQuoteInstanceId = -1;
        cachedDragQuoteText = null;
        cachedSynergyDeltas.Clear();

        if (trainWindowManager)
        {
            trainWindowManager.ClearAllDragVisuals();
        }
        if (trainView)
        {
            trainView.ClearDrag();
            trainView.HidePlacementCandidates();
        }
        if (relayoutTrainView)
        {
            relayoutTrainView.ClearDrag();
            relayoutTrainView.HidePlacementCandidates();
        }
        if (marketView) marketView.ClearDrag();
        if (relayoutMarketView) relayoutMarketView.ClearDrag();
        if (butcheryView) butcheryView.ClearDrag();
        if (modificationView) modificationView.ClearDrag();
        if (refiningView) refiningView.ClearDrag();

        if (drag == null)
        {
            if (hadHoveredWagon)
            {
                RefreshTrainBar();
            }
            return;
        }

        drag = null;
        HasActiveDrag = false;
        SetDragView(null);
        if (hadHoveredWagon)
        {
            RefreshTrainBar();
        }
        if (cityView && session != null && session.RunStatus == PuzzleRunStatus.InProgress)
        {
            cityView.ShowTradeStatus(string.Empty);
        }
        RefreshHover();
    }

    static bool IsOnScreen(Vector2 _position)
    {
        return new Rect(0f, 0f, Screen.width, Screen.height).Contains(_position);
    }

    void RefreshTrainBar()
    {
        RefreshWagonRefrigeration();
        if (!TrainOperationsBlocked && session?.Train != null)
        {
            session.Train.HasUnreadEventNotice = false;
        }
        if (trainBarView && session != null)
        {
            trainBarView.Refresh(session.TrainWagons, session.CurrentWagonIndex, session.MaxWagonCount, hoveredWagonIndex);
        }
        if (RelayoutSettings.Enabled && session != null)
        {
            if (railStripView)
            {
                railStripView.Refresh(session.TrainWagons, session.CurrentWagonIndex, hoveredWagonIndex);
            }
            if (!UseMultiTrainWindows && trainWindowView)
            {
                long remainingSettlementHours = 0;
                double interval = TableManager.Instance != null && TableManager.Instance.IsValid
                    ? TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours
                    : 0d;
                if (interval > 0d && session.Time != null)
                {
                    double now = session.Time.ElapsedHours + PuzzleTimeManager.HOURS_PER_TURN / 2d;
                    double next = (System.Math.Floor(now / interval) + 1d) * interval;
                    remainingSettlementHours = (long)(next - session.Time.ElapsedHours);
                }
                trainWindowView.SetHeader(session.Train, remainingSettlementHours, session.TrainSynergyDelta);
            }
        }
    }

    internal void RefreshForRelayoutSwitch()
    {
        if (!initialized || session == null)
        {
            return;
        }
        butcheryConfigured = false;
        modificationConfigured = false;
        refiningConfigured = false;
        questBoardConfigured = false;
        ConfigureButcheryView();
        ConfigureModificationView();
        ConfigureRefiningView();
        ConfigureQuestBoardView();
        RebuildQuestOfferBoard();
        if (drag != null)
        {
            input.CancelCapture();
            ClearDrag();
        }
        else
        {
            if (trainWindowManager)
        {
            trainWindowManager.ClearAllDragVisuals();
        }
        if (trainView)
            {
                trainView.ClearDrag();
                trainView.HidePlacementCandidates();
            }
            if (relayoutTrainView)
            {
                relayoutTrainView.ClearDrag();
                relayoutTrainView.HidePlacementCandidates();
            }
        }
        if (!UseMultiTrainWindows && ActiveTrainView)
        {
            ActiveTrainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train));
            ActiveTrainView.SetBoard(session.Train);
        }
        if (RelayoutSettings.Enabled && tavernWindowView)
        {
            tavernWindowView.ResetCity(session.CityData.displayName);
            RebuildQuestOfferBoard();
        }
        if (RelayoutSettings.Enabled && marketWindowView)
        {
            marketWindowView.SetCity(session.CityData.displayName, Presentation.GetCityTradePreferences(session.CityData));
            bool open = session.IsMarketOpen && session.TryEnterMarket();
            relayoutMarketView?.SetMarketOpen(session.CanTradeInMarket);
            if (relayoutMarketView && session.Market != null)
            {
                relayoutMarketView.SetBoardSize(session.Market.Width, session.Market.Height, session.Market.BlockedCells);
            }
            marketWindowView.SetOpen(open, false);
            if (open)
            {
                RenderBoards();
            }
        }
        RenderBoards();
        RenderValues();
        RefreshTrainBar();
    }

    void OnRelayoutLensToggleRequested()
    {
        if (!RelayoutSettings.Enabled || !lensWindowView)
        {
            return;
        }
        lensWindowView.Toggle();
        if (lensWindowView.IsOpen && RouteLens)
        {
            if (lensDefinition != null)
            {
                RouteLens.Show(lensTitle, lensTrade, JoinLensLines(lensSpotText, lensRankingText), LensDestinationText(),
                    Presentation.RouteLensFootnote(lensTab), lensTab, _tabsInteractable: drag == null);
            }
            else
            {
                RouteLens.Show(Presentation.LensWindowTitle, Presentation.LensWindowEmpty, string.Empty,
                    string.Empty, string.Empty, lensTab, _tabsInteractable: false);
            }
        }
    }

    void RefreshWagonRefrigeration()
    {
        if (wagonEffectsLabel && session?.Train is { } wagon)
        {
            var effects = wagon.WagonModifications.Select(_effect => _effect.ActiveEffectLabel).ToList();
            var process = session.WagonProcessOf(wagon);
            if (process != null)
            {
                effects.Add(Presentation.WagonProcessStatus(process, session.CurrentWagonProcess(wagon)));
            }
            wagonEffectsLabel.text = string.Join(" · ", effects);
            wagonEffectsLabel.gameObject.SetActive(effects.Count > 0);
        }

        if (!wagonRefrigerationButton || !wagonRefrigerationLabel || session == null)
        {
            return;
        }
        wagonRefrigerationButton.interactable = !TrainOperationsBlocked && session.CanInstallWagonRefrigeration;
        wagonRefrigerationLabel.text = session.HasWagonRefrigeration
            ? Presentation.PuzzleWagonRefrigerationInstalled
            : Presentation.PuzzleWagonRefrigerationLabel(session.WagonRefrigerationCost);
    }

    void OnWagonRefrigerationClicked()
    {
        if (TrainOperationsBlocked || session == null)
        {
            return;
        }

        bool installed = session.TryInstallWagonRefrigeration();
        RefreshWagonRefrigeration();
        if (installed)
        {
            cityView.ShowTradeStatus(Presentation.PuzzleWagonRefrigerationInstalled);
            RefreshHover();
        }
    }

    // 의뢰 화물을 싣거나 내리면 기차가 바뀐다. 수락 시 화물을 실은 화물칸으로 전환되므로 제목·크기도 다시 맞춘다.
    void OnQuestChanged(GameEvents.QuestInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        input.CancelCapture();
        ClearDrag();
        RebuildQuestOfferBoard();
        if (!UseMultiTrainWindows && ActiveTrainView)
        {
            ActiveTrainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train));
            ActiveTrainView.SetBoard(session.Train);
        }
        RenderBoards();
        RenderValues();
        RefreshTrainBar();
        RefreshHover();
        cityView.ShowTradeStatus(WithProficiencyNotice(Presentation.QuestChanged(_info)));
    }

    // 판매·의뢰 완료 중에 오른 숙련 레벨. 그 결과를 상태 줄에 띄울 때 끝에 붙이고 비운다.
    void OnProficiencyChanged(GameEvents.ProficiencyInfo _info)
    {
        if (_info.session != session || !_info.LeveledUp)
        {
            return;
        }
        string notice = Presentation.ProficiencyLevelUp(session, _info);
        pendingProficiencyNotice = string.IsNullOrEmpty(pendingProficiencyNotice)
            ? notice
            : pendingProficiencyNotice + Presentation.ProficiencyNoticeSeparator + notice;
    }

    string WithProficiencyNotice(string _status)
    {
        if (string.IsNullOrEmpty(pendingProficiencyNotice))
        {
            return _status;
        }
        string notice = pendingProficiencyNotice;
        pendingProficiencyNotice = null;
        return string.IsNullOrEmpty(_status) ? notice : _status + Presentation.ProficiencyNoticeSeparator + notice;
    }

    void OnWagonHovered(int _index, Vector2 _slotAnchor)
    {
        if (!wagonTooltipView || TrainOperationsBlocked || drag != null)
        {
            HideWagonTooltip();
            return;
        }

        var wagon = session.TrainWagons[_index];
        if (hoveredTooltipWagon == wagon && wagonTooltipView.IsOpen)
        {
            wagonTooltipView.SetPositionAboveAnchor(_slotAnchor);
            return;
        }

        hoveredTooltipWagon = wagon;
        string name = wagon.WagonProduct?.displayName ?? wagon.DisplayName ?? "일반 화차";
        wagonTooltipStats.Clear();
        wagonTooltipStats.Add(("적재", $"{wagon.OccupiedCells} / {wagon.UsableCellCount}칸"));
        wagonTooltipStats.Add(("판매가", Presentation.WagonSaleAmount(wagon.WagonSalePrice)));
        if (session.WagonProcessOf(wagon) is { } process)
        {
            wagonTooltipStats.Add(("가공", Presentation.WagonProcessStatus(process, session.CurrentWagonProcess(wagon))));
        }

        wagonTooltipEffects.Clear();
        if (wagon.WagonProduct != null && !string.IsNullOrEmpty(wagon.WagonProduct.description))
        {
            wagonTooltipEffects.Add(Presentation.PuzzleTooltipEffectDescription(wagon.WagonProduct.description));
        }
        string productModId = wagon.WagonProduct?.modificationId;
        foreach (var modification in wagon.WagonModifications)
        {
            if (!string.IsNullOrEmpty(productModId) &&
                string.Equals(modification.Id, productModId, System.StringComparison.Ordinal))
            {
                continue;
            }
            wagonTooltipEffects.Add(Presentation.PuzzleTooltipEffectDescription(modification.TooltipEffect));
        }

        string failure = session.WagonSaleFailure(_index);
        string notice = failure == null ? null : Presentation.WagonSaleNotice(failure);
        wagonTooltipView.ShowEntityInfo($"{name} · {wagon.WagonNumber}번", null,
            notice == null ? null : new[] { notice }, _slotAnchor, _statRows: wagonTooltipStats, _aboveAnchor: true,
            _effectHeader: "효과", _effectLines: wagonTooltipEffects);
    }

    void HideWagonTooltip()
    {
        hoveredTooltipWagon = null;
        if (wagonTooltipView && wagonTooltipView.IsOpen)
        {
            wagonTooltipView.Close();
        }
    }

    void OnWagonSelected(int _index)
    {
        if (TrainOperationsBlocked || session == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        RebuildQuestOfferBoard();
        if (!session.SelectWagon(_index))
        {
            return;
        }
        if (!UseMultiTrainWindows && ActiveTrainView)
        {
            ActiveTrainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train));
            ActiveTrainView.SetBoard(session.Train);
        }
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

    void OnRelayoutWagonSelected(int _index)
    {
        if (session == null || _index < 0 || _index >= session.WagonCount)
        {
            return;
        }
        if (UseMultiTrainWindows)
        {
            trainWindowManager.OpenOrFocusWagon(_index, session);
        }
        OnWagonSelected(_index);
    }

    void OnRelayoutWagonFocused(int _index)
    {
        if (session != null && _index >= 0 && _index < session.WagonCount && session.CurrentWagonIndex != _index)
        {
            session.SelectWagon(_index);
            RefreshTrainBar();
        }
    }

    void OnRelayoutOpenWindowsChanged()
    {
        if (session != null)
        {
            RenderBoards();
            RenderValues();
            RefreshTrainBar();
        }
    }

    void OnRelayoutWagonDraggedOutside(int _index, Vector2 _screenPos)
    {
        if (_index < 0 || MarketOperationsBlocked || !IsWagonSaleDropPosition(_screenPos))
        {
            ClearWagonSalePreview();
            return;
        }
        if (wagonSalePreviewIndex == _index)
        {
            return;
        }
        wagonSalePreviewIndex = _index;
        string failure = session.WagonSaleFailure(_index);
        string msg = failure == null
            ? BasicData.Format("rules.wagon.salePreview", Presentation.WagonSaleAmount(session.TrainWagons[_index].WagonSalePrice))
            : Presentation.WagonSaleNotice(failure);
        statusBannerView?.ShowStatus(msg);
        cityView.ShowTradeStatus(msg);
    }

    void OnRelayoutWagonDragEndedOutside(int _index, Vector2 _screenPos)
    {
        ClearWagonSalePreview();
        if (_index < 0 || MarketOperationsBlocked || !IsWagonSaleDropPosition(_screenPos))
        {
            return;
        }

        var wagon = _index < session.WagonCount ? session.TrainWagons[_index] : null;
        if (!session.TrySellWagon(_index, out string failure))
        {
            string failMsg = Presentation.WagonSaleNotice(failure);
            statusBannerView?.ShowStatus(failMsg);
            cityView.ShowTradeStatus(failMsg);
            return;
        }

        HideWagonTooltip();
        RefreshSelectedWagon();
        string okMsg = BasicData.Format("rules.wagon.saleComplete", wagon.WagonNumber,
            Presentation.WagonSaleAmount(wagon.WagonSalePrice, _showPlus: true));
        statusBannerView?.ShowStatus(okMsg);
        cityView.ShowTradeStatus(okMsg);
    }

    bool IsWagonSaleDropPosition(Vector2 _screenPos)
    {
        if (stationHallWindowView && stationHallWindowView.ContainsScreenPoint(_screenPos))
        {
            return true;
        }
        if (townSceneView && townSceneView.IsScreenPointOverBuilding(TownBuildingButton.BuildingKind.StationHall, _screenPos))
        {
            return true;
        }
        if (stationActions && stationActions.IsWagonShopOpen && UnityEngine.EventSystems.EventSystem.current)
        {
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = _screenPos };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                if (stationActions.IsWagonShopTarget(hits[i].gameObject))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // S6 역사 창에서 구매한 화차를 선로 삽입 지점에 놓을 때 호출한다(R24).
    public bool TryDropPurchasedWagonAtScreenPosition(PuzzleWagonProductData _product, Vector2 _screenPos)
    {
        if (MarketOperationsBlocked || session == null || _product == null || !railStripView ||
            !railStripView.TryGetInsertionIndexAtScreenPosition(_screenPos, out int insertionIndex))
        {
            return false;
        }
        if (!session.TryPurchaseWagon(_product.id, out string failureReason))
        {
            statusBannerView?.ShowStatus(failureReason);
            cityView.ShowTradeStatus(failureReason);
            return false;
        }
        int newIndex = session.WagonCount - 1;
        var purchasedWagon = session.TrainWagons[newIndex];
        int clampedTarget = Mathf.Clamp(insertionIndex, 0, newIndex);
        if (clampedTarget != newIndex && !session.TryMoveWagon(newIndex, clampedTarget))
        {
            const string moveFail = "화차 위치를 옮기지 못했습니다.";
            statusBannerView?.ShowStatus(moveFail);
            cityView.ShowTradeStatus(moveFail);
        }
        if (UseMultiTrainWindows)
        {
            trainWindowManager.OpenOrFocusWagon(purchasedWagon, session);
        }
        RefreshSelectedWagon();
        return true;
    }

    long RemainingSettlementHours()
    {
        double interval = TableManager.Instance != null && TableManager.Instance.IsValid
            ? TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours
            : 0d;
        if (interval > 0d && session?.Time != null)
        {
            double now = session.Time.ElapsedHours + PuzzleTimeManager.HOURS_PER_TURN / 2d;
            double next = (System.Math.Floor(now / interval) + 1d) * interval;
            return (long)(next - session.Time.ElapsedHours);
        }
        return 0;
    }

    void OnWagonMoved(int _sourceIndex, int _destinationIndex)
    {
        if (!TrainOperationsBlocked && drag == null && session.TryMoveWagon(_sourceIndex, _destinationIndex))
        {
            RenderBoards();
            RefreshTrainBar();
            if (UseMultiTrainWindows)
            {
                trainWindowManager.SavePersistence(session);
            }
        }
    }

    void PreviewWagonSale(int _index, GameObject _target)
    {
        if (MarketOperationsBlocked || !stationActions || !stationActions.IsWagonShopTarget(_target))
        {
            ClearWagonSalePreview();
            return;
        }

        if (wagonSalePreviewIndex == _index)
        {
            return;
        }

        wagonSalePreviewIndex = _index;
        string failure = session.WagonSaleFailure(_index);
        cityView.ShowTradeStatus(failure == null
            ? BasicData.Format("rules.wagon.salePreview", Presentation.WagonSaleAmount(session.TrainWagons[_index].WagonSalePrice))
            : Presentation.WagonSaleNotice(failure));
    }

    void ClearWagonSalePreview()
    {
        if (wagonSalePreviewIndex.HasValue)
        {
            wagonSalePreviewIndex = null;
            cityView.ShowTradeStatus(string.Empty);
        }
    }

    void FinishWagonSale(int _index, GameObject _target)
    {
        if (MarketOperationsBlocked || !stationActions || !stationActions.IsWagonShopOpen)
        {
            return;
        }
        if (!stationActions.IsWagonShopTarget(_target))
        {
            cityView.ShowTradeStatus(Presentation.WagonSaleNotice("saleCanceled"));
            return;
        }

        var wagon = _index >= 0 && _index < session.WagonCount ? session.TrainWagons[_index] : null;
        if (!session.TrySellWagon(_index, out string failure))
        {
            cityView.ShowTradeStatus(Presentation.WagonSaleNotice(failure));
            return;
        }

        HideWagonTooltip();
        RefreshSelectedWagon();
        cityView.ShowTradeStatus(BasicData.Format("rules.wagon.saleComplete", wagon.WagonNumber,
            Presentation.WagonSaleAmount(wagon.WagonSalePrice, _showPlus: true)));
    }

    void OnWagonProductDropped(PuzzleWagonProductData _product)
    {
        if (MarketOperationsBlocked || session == null)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        RebuildQuestOfferBoard();
        if (!session.TryPurchaseWagon(_product.id, out string failureReason))
        {
            cityView.ShowTradeStatus(failureReason);
            return;
        }

        RefreshFunds();
        RefreshSelectedWagon();
        cityView.ShowTradeStatus(Presentation.PuzzleWagonPurchaseStatus(session.Train.WagonNumber, _product.price));
    }

    void RefreshSelectedWagon()
    {
        if (!UseMultiTrainWindows && ActiveTrainView)
        {
            ActiveTrainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train));
            ActiveTrainView.SetBoard(session.Train);
        }
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

}
