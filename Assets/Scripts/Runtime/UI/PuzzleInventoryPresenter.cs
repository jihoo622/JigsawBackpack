using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryPresenter : MonoBehaviour
{
    const float KEYWORD_DWELL_MOVE_THRESHOLD = 4f;

    [Header("Drag Settings")]
    [SerializeField] float wagonDragHoverDelay = 0.5f;

    [Header("Tooltip Settings")]
    [SerializeField] float keywordTooltipDelay = 0.35f;

    [SerializeField] PuzzleInventoryView trainView;
    [SerializeField] TextMeshProUGUI wagonEffectsLabel;
    [SerializeField] Button wagonRefrigerationButton;
    [SerializeField] TextMeshProUGUI wagonRefrigerationLabel;
    [SerializeField] PuzzleInventoryView marketView;
    [SerializeField] PuzzleInventoryView butcheryView;
    [SerializeField] PuzzleInventoryView modificationView;
    [SerializeField] PuzzleInventoryView refiningView;
    [SerializeField] PuzzleCityView cityView;
    [SerializeField] PuzzleItemTooltipView tooltipView;
    [SerializeField] PuzzleItemTooltipView keywordTooltipView;
    [SerializeField] PuzzleTrainBarView trainBarView;
    [SerializeField] PauseMenu pauseMenu;

    PuzzleInventoryInput input;
    PuzzleInventoryService session;
    // 아직 상태 줄에 띄우지 않은 숙련 레벨 상승 알림.
    string pendingProficiencyNotice;
    PuzzleInventoryDrag drag;
    PuzzleInventoryView dragView;
    Vector2 pointerPosition;
    Vector2? hoverPosition;
    int? currentHoveredInstanceId;
    string currentHoveredKeywordId;
    string pendingKeywordId;
    float pendingKeywordStartTime;
    Vector2 pendingKeywordPosition;
    int? hoveredWagonIndex;
    PuzzleItemTooltipView KeywordTooltipView => keywordTooltipView ? keywordTooltipView : tooltipView;
    float hoveredWagonStartTime;
    bool initialized;
    bool isMapOpen;
    bool butcheryConfigured;
    bool modificationConfigured;
    bool refiningConfigured;
    // 도축대는 세션이 조작을 허용하고 패널에 View가 나와 있을 때만 드래그·표시 대상이 된다.
    bool ButcheryViewReady => session != null && session.IsButcheryOpen && butcheryView != null &&
        butcheryView.gameObject.activeInHierarchy;
    bool ModificationViewReady => session != null && session.IsModificationOpen && modificationView != null &&
        modificationView.gameObject.activeInHierarchy;
    bool RefiningViewReady => session != null && session.IsRefiningOpen && refiningView != null &&
        refiningView.gameObject.activeInHierarchy;
    decimal? pendingSettlementValue;
    decimal? pendingFreshnessLossValue;
    bool pendingFundsRefresh;
    bool isTravelOpen;
    bool TrainOperationsBlocked => isTravelOpen;
    bool MarketOperationsBlocked => isMapOpen || isTravelOpen;

    void OnEnable()
    {
        if (!TryGetComponent(out input) || !trainView || !marketView || trainView == marketView || !cityView || !pauseMenu ||
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
        trainView.Initialize(trainCapacity, dragCapacity, Presentation.PuzzleTrainTitle(session.Train.WagonNumber), session.ItemDefinitions, _showSynergies: true);
        marketView.Initialize(session.MarketCapacity, dragCapacity, Presentation.PuzzleMarketTitle, session.ItemDefinitions, _showSynergies: false);
        ConfigureButcheryView();
        ConfigureModificationView();
        ConfigureRefiningView();
        cityView.Initialize(session.GameClearCost);
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
        marketView.Disabled += OnViewDisabled;
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
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
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
        }
    }

    void OnDisable()
    {
        if (!initialized)
        {
            return;
        }

        if (trainView)
        {
            trainView.Disabled -= OnViewDisabled;
        }
        if (marketView)
        {
            marketView.Disabled -= OnViewDisabled;
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
        pendingSettlementValue = null;
        pendingFreshnessLossValue = null;
        pendingFundsRefresh = false;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
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
        if (trainBarView)
        {
            trainBarView.WagonSelected -= OnWagonSelected;
            trainBarView.WagonProductDropped -= OnWagonProductDropped;
            trainBarView.WagonMoved -= OnWagonMoved;
            trainBarView.CancelWagonDrag();
        }
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
        if (TrainOperationsBlocked || !trainBarView || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            if (hoveredWagonIndex.HasValue)
            {
                hoveredWagonIndex = null;
                RefreshTrainBar();
            }
            return;
        }

        if (trainBarView.TryGetWagonAtScreenPosition(pointerPosition, out int wagonIndex) &&
            wagonIndex < session.WagonCount && wagonIndex != session.CurrentWagonIndex)
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
        if (!session.SelectWagon(_index))
        {
            return;
        }

        SetDragView(null);
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train.WagonNumber));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        RenderBoards();
        RenderValues();
        RefreshTrainBar();
        RefreshDrag();
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.SetTown(session.CityData.displayName, inProgress);
    }

    #region 술집 대화 생성

    // 미방문 마을의 특산·희귀 후보에서 테이블 비율로 종류를 고르고, 그 종류 안에서 후보 하나를 균등 추첨한다.
    string GetTavernRumor()
    {
        var specialty = new List<PuzzleRumorCandidate>();
        var rare = new List<PuzzleRumorCandidate>();
        foreach (var candidate in session.CollectRumorCandidates())
        {
            if (candidate.Kind == PuzzleRumorKind.Specialty)
            {
                specialty.Add(candidate);
            }
            else
            {
                rare.Add(candidate);
            }
        }

        // 두 종류 모두에 후보가 없으면 들은 소식이 없다고 답한다.
        if (specialty.Count == 0 && rare.Count == 0)
        {
            return Presentation.TavernNoRumor;
        }

        // 한 종류에만 후보가 있으면 그쪽에서 고른다.
        var rumorTable = TableManager.Instance.PuzzleRumor;
        bool pickSpecialty = specialty.Count > 0 && (rare.Count == 0 ||
            Random.Range(0, rumorTable.specialtyPercent + rumorTable.rarePercent) < rumorTable.specialtyPercent);
        var selected = pickSpecialty
            ? specialty[Random.Range(0, specialty.Count)]
            : rare[Random.Range(0, rare.Count)];

        string message = Presentation.TavernRumor(selected);
        session.RecordTavernRumor(selected, message);
        return message;
    }

    #endregion

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        input.CancelCapture();
        ClearDrag();
        ShowCurrentCity();
        pendingSettlementValue = _info.cargoValueBeforeSettlement;
        pendingFreshnessLossValue = _info.cargoValueBeforeFreshnessLoss;
        PlayPendingSettlement();
        if (session.RunStatus != PuzzleRunStatus.InProgress)
        {
            cityView.SetTown(session.CityData.displayName, false);
            cityView.ShowTradeStatus(Presentation.PuzzleRunStatusText(session.RunStatus));
            if (session.RunStatus == PuzzleRunStatus.Lost)
            {
                pauseMenu.ShowCompletion(PuzzleRunStatus.Lost, session.Funds);
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
            input.CancelCapture();
            ClearDrag();
            if (!session.WaitForDaylight())
            {
                cityView.ShowTradeStatus(Presentation.PuzzleTimeOverflow);
            }
        }
    }

    public void ClearGame()
    {
        if (!initialized || !isActiveAndEnabled || MarketOperationsBlocked || !pauseMenu || !pauseMenu.CanShowCompletion ||
            !session.TryClearGame())
        {
            return;
        }

        pauseMenu.ShowCompletion(PuzzleRunStatus.Won, session.Funds);
        cityView.SetTown(session.CityData.displayName, false);
        RefreshFunds();
        cityView.ShowTradeStatus(Presentation.PuzzleClearTitle);
        RefreshTrainBar();
    }

    void RefreshFunds()
    {
        if (TrainOperationsBlocked)
        {
            pendingFundsRefresh = true;
            return;
        }
        pendingFundsRefresh = false;
        cityView.ShowFunds(session.Funds, session.CanClearGame);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
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
                RenderBoards();
                ShowItemDetails(null, null);
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
        }
    }

    void PlayPendingSettlement()
    {
        if (TrainOperationsBlocked || (!pendingSettlementValue.HasValue && !pendingFreshnessLossValue.HasValue))
        {
            return;
        }

        trainView.ShowCargoValueFeedback(pendingSettlementValue, pendingFreshnessLossValue,
            session.Train.Placements.Sum(item => item.FreshnessAdjustedValue), session.Train.Value.BaseValue);
        pendingSettlementValue = null;
        pendingFreshnessLossValue = null;
    }

    void ShowCurrentCity()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.ShowCity(Presentation.PuzzleCityTitle(session.CityData, session.RegionData));
        cityView.ShowTime(session.Time);
        RefreshFunds();
        cityView.ShowTradeStatus(inProgress
            ? session.IsMarketOpen ? string.Empty : Presentation.PuzzleNightMarket
            : Presentation.PuzzleRunStatusText(session.RunStatus));
        marketView.SetMarketOpen(session.IsMarketOpen);
        RefreshWaitAction();
        marketView.SetBoardSize(session.Market.Width, session.Market.Height);
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
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
        RefreshDrag();
    }

    void OnQuickTransferRequested(PointerEventData _eventData)
    {
        if (MarketOperationsBlocked || drag != null || session.RunStatus != PuzzleRunStatus.InProgress ||
            !trainView.isActiveAndEnabled ||
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
        return marketView.isActiveAndEnabled ? session.Market : null;
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
                result = activeDrag.DropOnBoard(session, destination, targetCell);
                if (result.CanDrop)
                {
                    RefreshActionBoardsAfter(activeDrag.Source, destination);
                    RenderValues();
                    RefreshFunds();
                    RefreshTrainBar();
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
                                newPercent += (rule.multiplierPercent - PuzzleInventoryValuation.BASE_PERCENT);
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
                                    oldPercent += (rule.multiplierPercent - PuzzleInventoryValuation.BASE_PERCENT);
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
        cityView.ShowTradeStatus(string.Empty);
    }

    void SetDragView(PuzzleInventoryView _next)
    {
        if (dragView == _next)
        {
            return;
        }

        var sourceView = drag != null ? ViewFor(drag.Source) : null;
        if (dragView && dragView != sourceView)
        {
            dragView.ClearDrag();
        }
        dragView = _next;
    }

    bool TryGetBoard(Vector2 _position, out PuzzleInventoryView _view, out PuzzleInventory _inventory, out Vector2Int _cell)
    {
        if (IsOnScreen(_position))
        {
            if (trainView && trainView.isActiveAndEnabled && trainView.TryGetBoardCell(_position, out _cell))
            {
                _view = trainView;
                _inventory = session.Train;
                return true;
            }
            if (!isMapOpen)
            {
                if (session.IsMarketOpen && marketView && marketView.isActiveAndEnabled && marketView.TryGetBoardCell(_position, out _cell))
                {
                    _view = marketView;
                    _inventory = session.Market;
                    return true;
                }
                if (ButcheryViewReady && butcheryView.TryGetBoardCell(_position, out _cell))
                {
                    _view = butcheryView;
                    _inventory = session.ButcheryBoard();
                    return true;
                }
                if (ModificationViewReady && modificationView.TryGetBoardCell(_position, out _cell))
                {
                    _view = modificationView;
                    _inventory = session.ModificationBoard();
                    return true;
                }
                if (RefiningViewReady && refiningView.TryGetBoardCell(_position, out _cell))
                {
                    _view = refiningView;
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

        if (currentHoveredKeywordId != null)
        {
            currentHoveredKeywordId = null;
            targetTooltip.Close();
        }

        if (pendingKeywordId != linkId)
        {
            pendingKeywordId = linkId;
            pendingKeywordStartTime = Time.unscaledTime;
            pendingKeywordPosition = pos;
            return;
        }

        if (Vector2.Distance(pos, pendingKeywordPosition) > KEYWORD_DWELL_MOVE_THRESHOLD)
        {
            pendingKeywordStartTime = Time.unscaledTime;
            pendingKeywordPosition = pos;
            return;
        }

        if (Time.unscaledTime - pendingKeywordStartTime >= keywordTooltipDelay)
        {
            currentHoveredKeywordId = linkId;
            pendingKeywordId = null;
            targetTooltip.ShowKeyword(title, description, pos);
        }
    }

    void ClearKeywordHover(bool _closeSharedTooltip = true)
    {
        pendingKeywordId = null;
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
        if (trainView)
        {
            trainView.ShowItemDetails(null, default, default, default);
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
            if (trainView)
            {
                trainView.ShowItemDetails(null, default, default, default);
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
        bool isButchery = session.IsButchery(_placement.Owner);
        bool isActionBoard = isButchery || session.IsModification(_placement.Owner) || session.IsRefining(_placement.Owner);
        bool isTrain = _view == trainView || session.IsTrainWagon(_placement.Owner);
        decimal actualValue = _placement.FreshnessAdjustedValue;
        var rate = item == null || isActionBoard ? default : session.RateFor(item);
        (IReadOnlyList<string> General, IReadOnlyList<string> Synergies) effects =
            isActionBoard ? default : BuildEffectDescriptions(_placement, isTrain);
        long? previewMinutes = session.PreviewMinutes;
        long? previewActivations = null;
        decimal? futureValue = null;
        if (isTrain && previewMinutes.HasValue)
        {
            try
            {
                previewActivations = session.SynergyActivationsFor(previewMinutes.Value);
                futureValue = session.PreviewValueAfter(_placement, previewMinutes.Value);
            }
            catch (System.OverflowException)
            {
                futureValue = null;
            }
        }

        if (tooltipView && drag == null && (_position.HasValue || hoverPosition.HasValue))
        {
            Vector2 pos = _position ?? hoverPosition.Value;
            string previewName = session.PreviewCityId == null ? null : TableManager.Instance.CityPrice(session.PreviewCityId).displayName;
            var direction = isTrain ? PuzzleTradeDirection.Sell : PuzzleTradeDirection.Buy;
            long? price = !isActionBoard && session.TryQuoteTrade(_placement, direction, out long quote) ? quote : (long?)null;
            tooltipView.Show(item, actualValue, rate, effects.General, effects.Synergies, futureValue, pos, isTrain, _placement.FreshnessPercent,
                session.MinutesUntilNextFreshnessBand(_placement), previewMinutes,
                previewName, previewActivations, price, isTrain ? PuzzleSynergyBonus.GroupByNeighbor(value.SynergyBonuses) : null,
                isTrain ? session.AppliedPreservationPercent(_placement) : 0,
                _placement.Pose?.ClockwiseTurns ?? 0);
            // 툴팁을 열면 미확인 알림만 지운다. 실제로 대기 중인 진행도와 빨간 표시는 상태가 풀릴 때까지 남는다.
            if ((_placement.HasUnreadSynergySettlement || _placement.HasUnreadSpaceNotice) && tooltipView.IsOpen)
            {
                _placement.HasUnreadSynergySettlement = false;
                _placement.HasUnreadSpaceNotice = false;
                RenderBoards(_placement.InstanceId);
            }
        }

        if (trainView)
        {
            trainView.ShowItemDetails(_view == trainView ? item : null, value, actualValue, rate);
        }
        if (marketView)
        {
            marketView.ShowItemDetails(_view == marketView ? item : null, value, actualValue, rate);
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

    (IReadOnlyList<string> General, IReadOnlyList<string> Synergies) BuildEffectDescriptions(PuzzlePlacement _placement, bool _isTrain)
    {
        PuzzleItemDefinition item = _placement?.Definition;
        if (item == null)
        {
            return default;
        }

        PuzzleSynergyScope scope = _isTrain ? PuzzleSynergyScope.Train : PuzzleSynergyScope.Market;
        var general = new List<PuzzleEffectDisplay>();
        var synergies = new List<(bool isTarget, bool isPenalty, string text)>();
        var tables = TableManager.Instance;
        int preservation = PuzzleRefrigeration.SourceReductionPercent(item, tables.PuzzleSynergy.freshness);
        if (preservation > 0)
        {
            synergies.Add((false, false, Presentation.PuzzleRefrigerationEffect(preservation)));
        }

        var animals = tables.PuzzleSynergy.animals;
        if (item.Tags.Contains(animals.animalTag))
        {
            if (!item.IsBaby && tables.TryGetAnimalBreeding(item.Id, out var breeding))
            {
                var offspring = tables.Piece(breeding.offspringPieceId);
                long breedingMinutes = (long)animals.breedingHours * PuzzleTimeManager.MINUTES_PER_HOUR;
                // 실제 선택된 번식 쌍에 속한 동물에만 진행도를 표시한다.
                PuzzleProgressDisplay? breedingProgress = null;
                if (_isTrain && session.TryGetBreedingProgress(_placement, out long pairedMinutes, out bool pairedBlocked))
                {
                    breedingProgress = new PuzzleProgressDisplay(pairedMinutes, breedingMinutes, pairedBlocked);
                }
                general.Add(new PuzzleEffectDisplay(Presentation.PuzzleAnimalBreedingEffect(breeding.description,
                    breedingMinutes, offspring.displayName, breeding.offspringCount), breedingProgress));
            }
            if (item.GrowthMinutes > 0)
            {
                var adult = tables.Piece(item.AdultPieceId);
                // 성장 시계는 객차의 소유 물건에만 있다. 시장·도축·개조 보드에는 진행도를 보여주지 않는다.
                PuzzleProgressDisplay? growthProgress = _isTrain && _placement.GrowthElapsedMinutes >= 0
                    ? new PuzzleProgressDisplay(_placement.GrowthElapsedMinutes, item.GrowthMinutes,
                        _placement.GrowthSpaceBlocked)
                    : null;
                general.Add(new PuzzleEffectDisplay(Presentation.PuzzleAnimalGrowthEffect(item.GrowthDescription,
                    item.GrowthMinutes, adult.displayName, item.GrowthBlockedMinutes), growthProgress));
            }

            var waste = tables.Piece(animals.wastePieceId);
            // 배설은 달력 경계 기반으로 흐르므로 진행도를 붙이지 않는다.
            general.Add(new PuzzleEffectDisplay(Presentation.PuzzleAnimalWasteEffect(animals.wasteDescription,
                animals.wasteIntervalDays, waste.displayName, animals.wasteCountPerAnimal)));
        }

        foreach (var rule in tables.SynergiesFor(scope))
        {
            bool isPenalty = rule.multiplierPercent < 100;
            if (PuzzlePieceEffects.IsSource(item, rule))
            {
                synergies.Add((false, isPenalty, Presentation.PuzzleTooltipEffectDescription(rule.sourceDescription)));
            }
            if (PuzzlePieceEffects.IsTarget(item, rule, animals.wasteTag))
            {
                synergies.Add((true, isPenalty, Presentation.PuzzleTooltipEffectDescription(rule.targetDescription)));
            }
        }

        var orderedSynergies = synergies
            .OrderBy(_entry => _entry.isTarget)
            .ThenBy(_entry => _entry.isPenalty)
            .Select(_entry => _entry.text)
            .ToList();
        return (general.Select(_effect => Presentation.PuzzleTooltipGeneralEffect(_effect)).ToList(), orderedSynergies);
    }

    void RenderValues()
    {
        if (trainView)
        {
            trainView.ShowBoardValue(session.Train.Placements.Sum(_item => _item.FreshnessAdjustedValue), session.Train.Value.BaseValue);
        }
        if (marketView)
        {
            marketView.ShowBoardInfo(Presentation.PuzzleCityTagPreferences(session.CityData));
        }
    }

    void RenderBoards(int? _highlightedInstanceId = null)
    {
        if (trainView)
        {
            trainView.RenderPlacements(session.Train, drag?.InstanceId, _highlightedInstanceId,
                RefrigerationContacts(session.Train));
        }
        if (marketView)
        {
            marketView.RenderPlacements(session.Market, drag?.InstanceId, _highlightedInstanceId);
        }
        if (ButcheryViewReady)
        {
            butcheryView.RenderPlacements(session.ButcheryBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
        if (ModificationViewReady)
        {
            modificationView.RenderPlacements(session.ModificationBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
        if (RefiningViewReady)
        {
            refiningView.RenderPlacements(session.RefiningBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
    }

    // 냉장은 기차 객차에서만 적용되므로 객차의 접촉 관계만 표시한다. 판정은 시간 경과 계산과 같은 함수를 쓴다.
    IReadOnlyList<PuzzleRefrigerationContact> RefrigerationContacts(PuzzleInventory _wagon)
    {
        return session.IsTrainWagon(_wagon)
            ? PuzzleRefrigeration.Contacts(_wagon.Placements, TableManager.Instance.PuzzleSynergy.freshness)
            : null;
    }

    PuzzleInventoryView ViewFor(PuzzleInventory _inventory)
    {
        if (session.IsButchery(_inventory) && butcheryView)
        {
            return butcheryView;
        }
        if (session.IsModification(_inventory) && modificationView)
        {
            return modificationView;
        }
        if (session.IsRefining(_inventory) && refiningView)
        {
            return refiningView;
        }
        return session.IsTrainWagon(_inventory) ? trainView : marketView;
    }

    // 도축대 View는 한 번 구성하면 된다. 열 때마다 크기와 상태 문구만 갱신한다.
    void ConfigureButcheryView()
    {
        if (!butcheryView)
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
            butcheryView.Initialize(capacity, dragCapacity, Presentation.PuzzleButcheryTitle, session.ItemDefinitions,
                _showSynergies: false);
            butcheryConfigured = true;
        }
        butcheryView.SetBoardSize(board.Width, board.Height);
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
        if (!modificationView)
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
            modificationView.Initialize(capacity, dragCapacity, Presentation.PuzzleModificationTitle,
                session.ItemDefinitions, _showSynergies: false);
            modificationConfigured = true;
        }
        modificationView.SetBoardSize(board.Width, board.Height);
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
        if (!refiningView)
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
            refiningView.Initialize(capacity, dragCapacity, Presentation.PuzzleRefiningTitle,
                session.ItemDefinitions, _showSynergies: false);
            refiningConfigured = true;
        }
        refiningView.SetBoardSize(board.Width, board.Height);
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
        bool hadDrag = drag != null;
        if (input)
        {
            input.CancelCapture();
        }
        ClearDrag();
        if (!hadDrag)
        {
            RenderBoards();
            ShowItemDetails(null, null);
        }
    }

    void ClearDrag()
    {
        bool hadHoveredWagon = hoveredWagonIndex.HasValue;
        hoveredWagonIndex = null;
        lastSynergyPreviewDestination = null;
        lastSynergyPreviewCell = new Vector2Int(int.MinValue, int.MinValue);
        lastSynergyPreviewRotation = -1;
        cachedSynergyDeltas.Clear();

        if (trainView) trainView.ClearDrag();
        if (marketView) marketView.ClearDrag();
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
        if (trainBarView && session != null)
        {
            trainBarView.Refresh(session.TrainWagons, session.CurrentWagonIndex, hoveredWagonIndex);
        }
    }

    void RefreshWagonRefrigeration()
    {
        if (wagonEffectsLabel && session?.Train is { } wagon)
        {
            wagonEffectsLabel.text = string.Join(" · ", wagon.WagonModifications.Select(_effect => _effect.ActiveEffectLabel));
            wagonEffectsLabel.gameObject.SetActive(wagon.WagonModifications.Count > 0);
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
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
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

    void OnWagonSelected(int _index)
    {
        if (TrainOperationsBlocked || session == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        if (!session.SelectWagon(_index))
        {
            return;
        }
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train.WagonNumber));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

    void OnWagonMoved(int _sourceIndex, int _destinationIndex)
    {
        if (!TrainOperationsBlocked && drag == null && session.TryMoveWagon(_sourceIndex, _destinationIndex))
        {
            RefreshTrainBar();
        }
    }

    void OnWagonProductDropped(PuzzleWagonProductData _product)
    {
        if (MarketOperationsBlocked || session == null)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        if (!session.TryPurchaseWagon(_product.id, out string failureReason))
        {
            cityView.ShowTradeStatus(failureReason);
            return;
        }

        RefreshFunds();
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.Train.WagonNumber));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
        cityView.ShowTradeStatus(Presentation.PuzzleWagonPurchaseStatus(session.Train.WagonNumber, _product.price));
    }

}
