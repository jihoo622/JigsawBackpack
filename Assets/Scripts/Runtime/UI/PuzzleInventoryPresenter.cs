using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryPresenter : MonoBehaviour
{
    static float WagonDragHoverDelay => BasicData.Float("ui.PuzzleInventoryPresenter.wagonDragHoverDelay");

    [SerializeField] PuzzleInventoryView trainView;
    [SerializeField] PuzzleInventoryView marketView;
    [SerializeField] PuzzleInventoryView butcheryView;
    [SerializeField] PuzzleCityView cityView;
    [SerializeField] PuzzleItemTooltipView tooltipView;
    [SerializeField] PuzzleTrainBarView trainBarView;
    [SerializeField] PauseMenu pauseMenu;

    PuzzleInventoryInput input;
    PuzzleInventoryService session;
    PuzzleInventoryDrag drag;
    PuzzleInventoryView dragView;
    Vector2 pointerPosition;
    Vector2? hoverPosition;
    int? currentHoveredInstanceId;
    int? hoveredWagonIndex;
    float hoveredWagonStartTime;
    bool initialized;
    bool isMapOpen;
    bool butcheryConfigured;
    // 도축대는 세션이 조작을 허용하고 패널에 View가 나와 있을 때만 드래그·표시 대상이 된다.
    bool ButcheryViewReady => session != null && session.IsButcheryOpen && butcheryView != null &&
        butcheryView.gameObject.activeInHierarchy;
    bool isNotebookOpen;
    bool InventoryBlocked => isMapOpen || isNotebookOpen;

    void OnEnable()
    {
        if (!TryGetComponent(out input) || !trainView || !marketView || trainView == marketView || !cityView || !pauseMenu ||
            (butcheryView && (butcheryView == trainView || butcheryView == marketView)) ||
            !trainView.transform.IsChildOf(transform) || !marketView.transform.IsChildOf(transform) ||
            !cityView.transform.IsChildOf(transform))
        {
            Debug.LogError("화물 인벤토리 Presenter에는 Input, 기차·시장·도시 View와 일시 정지 메뉴가 필요합니다. 도축대 View는 기차·시장과 다른 View여야 합니다.", this);
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
        trainView.Initialize(trainCapacity, dragCapacity, Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1), session.ItemDefinitions, _showSynergies: true);
        marketView.Initialize(session.MarketCapacity, dragCapacity, Presentation.PuzzleMarketTitle, session.ItemDefinitions, _showSynergies: false);
        ConfigureButcheryView();
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
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.TravelPreviewChanged += OnTravelPreviewChanged;
        GameEvents.ButcheryChanged += OnButcheryChanged;
        cityView.WaitRequested += WaitForDaylight;
        if (trainBarView)
        {
            trainBarView.WagonSelected += OnWagonSelected;
            trainBarView.AddWagonClicked += OnAddWagonClicked;
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
        currentHoveredInstanceId = null;
        isMapOpen = false;
        isNotebookOpen = false;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.TravelPreviewChanged -= OnTravelPreviewChanged;
        GameEvents.ButcheryChanged -= OnButcheryChanged;
        cityView.WaitRequested -= WaitForDaylight;
        if (trainBarView)
        {
            trainBarView.WagonSelected -= OnWagonSelected;
            trainBarView.AddWagonClicked -= OnAddWagonClicked;
        }
        OnViewDisabled();
        initialized = false;
    }

    void Update()
    {
        if (!initialized || InventoryBlocked || drag == null || !trainBarView || session == null ||
            session.RunStatus != PuzzleRunStatus.InProgress)
        {
            if (hoveredWagonIndex.HasValue)
            {
                hoveredWagonIndex = null;
                RefreshTrainBar();
            }
            return;
        }

        if (trainBarView.TryGetWagonAtScreenPosition(pointerPosition, out int wagonIndex) &&
            IsValidDragTargetWagon(wagonIndex))
        {
            if (hoveredWagonIndex != wagonIndex)
            {
                hoveredWagonIndex = wagonIndex;
                hoveredWagonStartTime = Time.unscaledTime;
                RefreshTrainBar();
            }
            else if (wagonIndex != session.CurrentWagonIndex &&
                     Time.unscaledTime - hoveredWagonStartTime >= WagonDragHoverDelay)
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

    bool IsValidDragTargetWagon(int _wagonIndex)
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
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        RenderBoards();
        RenderValues();
        RefreshTrainBar();
        RefreshDrag();
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        ShowCurrentCity();
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.SetTown(session.CityData.displayName, inProgress);
    }

    #region 술집 대화 생성

    // 지도 전체의 미방문 도시에서 고가 수요를 모아, 마을·품목 조합 하나를 동일한 확률로 고른다.
    string GetTavernRumor()
    {
        // 1. 지도에 있는 모든 도시를 소문 후보로 살펴본다.
        var tables = TableManager.Instance;
        var candidates = new List<(PuzzleCityData city, string tag)>();
        foreach (var city in tables.Cities)
        {
            // 2. 이미 방문했거나 플레이어가 물건을 팔 수 없는 도시는 건너뛴다.
            if (session.HasVisitedCity(city.id) || !city.sellEnabled)
            {
                continue;
            }

            // 3. 구매·판매 배율이 모두 100%를 넘는 품목을 후보에 넣는다.
            foreach (var rule in city.tagPrices)
            {
                if (rule.buyPercent > PuzzleInventoryValuation.BASE_PERCENT && rule.sellPercent > PuzzleInventoryValuation.BASE_PERCENT)
                {
                    candidates.Add((city, rule.tag));
                }
            }
        }
        // 4. 조건에 맞는 품목이 없으면 들은 소식이 없다고 답한다.
        if (candidates.Count == 0)
        {
            return Presentation.TavernNoRumor;
        }

        // 5. 후보 하나를 무작위로 골라 대사를 만들고 수첩에 기록한다.
        var selected = candidates[Random.Range(0, candidates.Count)];
        string message = Presentation.TavernRumor(selected.city.displayName, selected.tag);
        session.RecordTavernRumor(selected.city.id, message);
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
        if (session.RunStatus != PuzzleRunStatus.InProgress)
        {
            cityView.SetTown(session.CityData.displayName, false);
            cityView.ShowTradeStatus(Presentation.PuzzleRunStatusText(session.RunStatus));
            if (session.RunStatus == PuzzleRunStatus.Lost)
            {
                pauseMenu.ShowCompletion(PuzzleRunStatus.Lost, session.Funds);
            }
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
        if (!InventoryBlocked && session.CanWaitForDaylight)
        {
            input.CancelCapture();
            ClearDrag();
            if (!session.WaitForDaylight())
            {
                cityView.ShowTradeStatus(Presentation.PuzzleGrowthBlocked);
            }
        }
    }

    public void ClearGame()
    {
        if (!initialized || !isActiveAndEnabled || InventoryBlocked || !pauseMenu || !pauseMenu.CanShowCompletion ||
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
        cityView.ShowFunds(session.Funds, session.CanClearGame);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == PuzzleTradeNotebookView.PanelName)
        {
            isNotebookOpen = _info.open;
        }
        else if (_info.panel == Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            isMapOpen = _info.open;
        }
        else
        {
            return;
        }

        if (_info.open)
        {
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
        if (InventoryBlocked || session.RunStatus != PuzzleRunStatus.InProgress)
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
        if (InventoryBlocked || drag != null || session.RunStatus != PuzzleRunStatus.InProgress ||
            !trainView.isActiveAndEnabled || !marketView.isActiveAndEnabled ||
            !TryGetBoard(_eventData.position, out _, out var source, out var boardCell) ||
            !source.TryGetCell(boardCell, out var placement, out _))
        {
            return;
        }

        hoverPosition = _eventData.position;
        var result = session.QuickTransfer(placement);
        if (result.CanDrop)
        {
            session.RefreshButcheryAfterMove(source);
            RenderValues();
            RefreshFunds();
            RefreshTrainBar();
        }
        RefreshHover();
        cityView.ShowTradeStatus(Presentation.PuzzleTradeStatus(result, placement.Definition, _committed: true));
    }

    void OnDragged(PointerEventData _eventData)
    {
        if (InventoryBlocked)
        {
            return;
        }

        pointerPosition = _eventData.position;
        hoverPosition = _eventData.position;
        RefreshDrag();
    }

    void OnRotated()
    {
        if (InventoryBlocked || drag == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        drag.RotateClockwise();
        RefreshDrag();
    }

    void OnReleased(PointerEventData _eventData)
    {
        if (InventoryBlocked)
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
                    RefreshButcheryAfter(activeDrag.Source, destination);
                    RenderValues();
                    RefreshFunds();
                    RefreshTrainBar();
                }
            }
            else if (trainBarView && trainBarView.TryGetWagonAtScreenPosition(_eventData.position, out int candidateWagon) &&
                     IsValidDragTargetWagon(candidateWagon))
            {
                droppedWagonIndex = candidateWagon;
                result = activeDrag.QuickDropToWagon(session, droppedWagonIndex);
                if (result.CanDrop)
                {
                    RefreshButcheryAfter(activeDrag.Source, session.TrainWagons[droppedWagonIndex]);
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
            cityView.ShowTradeStatus(Presentation.PuzzleTradeStatus(result, activeDrag.Definition, _committed: true));
        }
        else if (droppedWagonIndex >= 0 && result.Placement.Failure == PuzzlePlacementFailure.NoSpace)
        {
            cityView.ShowTradeStatus($"{activeDrag.Definition.DisplayName} · {droppedWagonIndex + 1}번 칸에 화물을 놓을 공간이 없습니다");
        }
    }

    void RefreshDrag()
    {
        if (!IsOnScreen(pointerPosition))
        {
            SetDragView(null);
            cityView.ShowTradeStatus(string.Empty);
            return;
        }

        if (TryGetBoard(pointerPosition, out var targetView, out var destination, out var targetCell))
        {
            var preview = drag.Evaluate(session, destination, targetCell);
            SetDragView(targetView);
            var candidate = preview.Placement.Candidate;
            var grabbedBoardCell = candidate != null ? targetCell : Vector2Int.zero;
            targetView.ShowGhost(candidate ?? drag.GetGhostPose(), drag.Definition, grabbedBoardCell, pointerPosition, drag.Durability);
            targetView.ShowPreview(candidate, preview.CanDrop);
            cityView.ShowTradeStatus(Presentation.PuzzleTradeStatus(preview, drag.Definition, _committed: false));
            return;
        }

        var sourceView = ViewFor(drag.Source);
        SetDragView(sourceView);
        sourceView.ShowGhost(drag.GetGhostPose(), drag.Definition, Vector2Int.zero, pointerPosition, drag.Durability);
        sourceView.ShowPreview(null, false);
        cityView.ShowTradeStatus(string.Empty);
    }

    void SetDragView(PuzzleInventoryView _next)
    {
        if (dragView == _next)
        {
            return;
        }

        if (dragView)
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
        }

        _view = null;
        _inventory = null;
        _cell = default;
        return false;
    }

    void OnHovered(Vector2 _position)
    {
        if (InventoryBlocked)
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

        if (currentHoveredInstanceId.HasValue || (tooltipView && tooltipView.IsOpen))
        {
            currentHoveredInstanceId = null;
            RenderBoards();
            ShowItemDetails(null, null);
        }
    }

    void OnHoverEnded()
    {
        if (InventoryBlocked)
        {
            return;
        }

        hoverPosition = null;
        currentHoveredInstanceId = null;
        RenderBoards();
        ShowItemDetails(null, null);
    }

    void RefreshHover()
    {
        if (InventoryBlocked || drag != null)
        {
            currentHoveredInstanceId = null;
            if (tooltipView)
            {
                tooltipView.Close();
            }
            return;
        }

        if (hoverPosition is Vector2 position && TryGetBoard(position, out var view, out var board, out var boardCell) &&
            board.TryGetCell(boardCell, out var placement, out _))
        {
            currentHoveredInstanceId = placement.InstanceId;
            RenderBoards(placement.InstanceId);
            ShowItemDetails(view, placement, position);
            return;
        }

        currentHoveredInstanceId = null;
        RenderBoards();
        ShowItemDetails(null, null);
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
            return;
        }

        var item = _placement.Definition;
        var value = _placement.Owner.ValueOf(_placement.InstanceId);
        bool isButchery = session.IsButchery(_placement.Owner);
        bool isTrain = _view == trainView || session.IsTrainWagon(_placement.Owner);
        decimal actualValue = _placement.ActualValue;
        var rate = item == null || isButchery ? default : session.RateFor(item);
        var effects = isButchery ? null : BuildEffectDescriptions(item, isTrain);
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
            int? freshness = item != null && item.HasDurability ? _placement.Durability : (int?)null;
            string previewName = session.PreviewCityId == null ? null : TableManager.Instance.CityPrice(session.PreviewCityId).displayName;
            var direction = isTrain ? PuzzleTradeDirection.Sell : PuzzleTradeDirection.Buy;
            long? price = !isButchery && session.TryQuoteTrade(_placement, direction, out long quote) ? quote : (long?)null;
            tooltipView.Show(item, actualValue, rate, effects, futureValue, pos, isTrain, freshness, previewMinutes,
                previewName, previewActivations, price, isTrain ? Presentation.PuzzleActiveEffects(value) : Presentation.PuzzleMarketNoSettlement);
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
    }

    IReadOnlyList<string> BuildEffectDescriptions(PuzzleItemDefinition _item, bool _isTrain)
    {
        if (_item == null)
        {
            return null;
        }

        PuzzleSynergyScope scope = _isTrain ? PuzzleSynergyScope.Train : PuzzleSynergyScope.Market;
        var descriptions = new List<string>();
        foreach (var rule in TableManager.Instance.SynergiesFor(scope))
        {
            bool isSubject = !string.IsNullOrEmpty(rule.descriptionTag)
                ? _item.Tags.Contains(rule.descriptionTag)
                : !string.IsNullOrEmpty(rule.targetTag)
                    ? _item.Tags.Contains(rule.targetTag)
                    : !string.IsNullOrEmpty(rule.excludedTargetTag) && !_item.Tags.Contains(rule.excludedTargetTag);
            if (isSubject && !string.IsNullOrWhiteSpace(rule.description))
            {
                descriptions.Add(rule.description);
            }
        }

        return descriptions;
    }

    void RenderValues()
    {
        if (trainView)
        {
            trainView.ShowBoardValue(session.Train.Placements.Sum(_item => _item.ActualValue), session.Train.Value.BaseValue);
        }
        if (marketView)
        {
            marketView.ShowBoardInfo(Presentation.PuzzleCityTagPreferences(session.CityData, session.RegionData));
        }
        ShowButcheryStatus();
    }

    void RenderBoards(int? _highlightedInstanceId = null)
    {
        if (trainView)
        {
            trainView.RenderPlacements(session.Train, drag?.InstanceId, _highlightedInstanceId);
        }
        if (marketView)
        {
            marketView.RenderPlacements(session.Market, drag?.InstanceId, _highlightedInstanceId);
        }
        if (ButcheryViewReady)
        {
            butcheryView.RenderPlacements(session.ButcheryBoard(), drag?.InstanceId, _highlightedInstanceId);
        }
    }

    PuzzleInventoryView ViewFor(PuzzleInventory _inventory)
    {
        if (session.IsButchery(_inventory) && butcheryView)
        {
            return butcheryView;
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

    void ShowButcheryStatus()
    {
        if (butcheryView && butcheryConfigured)
        {
            butcheryView.ShowBoardInfo(Presentation.StationButcheryStatus(session.ButcheryPhase(),
                session.ButcheryRemainingMinutes()));
        }
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
        ShowButcheryStatus();
        RenderBoards();
    }

    // 확정된 이동이 도축대에 있었는지 확인해 상태를 갱신한다. ClearDrag의 다시 그리기가 이 결과를 그대로 표시한다.
    void RefreshButcheryAfter(PuzzleInventory _source, PuzzleInventory _destination)
    {
        session.RefreshButcheryAfterMove(_source);
        session.RefreshButcheryAfterMove(_destination);
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
        if (trainBarView && session != null)
        {
            trainBarView.Refresh(session.WagonCount, session.CurrentWagonIndex, session.NextWagonCost, session.CanAddWagon, hoveredWagonIndex);
        }
    }

    void OnWagonSelected(int _index)
    {
        if (InventoryBlocked || session == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        if (!session.SelectWagon(_index))
        {
            return;
        }
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

    void OnAddWagonClicked()
    {
        if (InventoryBlocked || session == null)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        long purchaseCost = session.NextWagonCost;
        if (!session.TryAddWagon(out string failureReason))
        {
            if (!string.IsNullOrEmpty(failureReason))
            {
                cityView.ShowTradeStatus($"화물칸 구매 불가 · {failureReason}");
            }
            return;
        }

        RefreshFunds();
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
        cityView.ShowTradeStatus(Presentation.PuzzleWagonPurchaseStatus(session.CurrentWagonIndex + 1, purchaseCost));
    }
}
