using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryPresenter : MonoBehaviour
{
    const float WAGON_DRAG_HOVER_DELAY = 0.5f;

    [SerializeField] PuzzleInventoryView trainView;
    [SerializeField] PuzzleInventoryView marketView;
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

    void OnEnable()
    {
        if (!TryGetComponent(out input) || !trainView || !marketView || trainView == marketView || !cityView || !pauseMenu ||
            !trainView.transform.IsChildOf(transform) || !marketView.transform.IsChildOf(transform) ||
            !cityView.transform.IsChildOf(transform))
        {
            Debug.LogError("화물 인벤토리 Presenter에는 Input, 기차·시장·도시 View와 일시 정지 메뉴가 필요합니다.", this);
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
        marketView.Initialize(session.MarketCapacity, dragCapacity, Presentation.PUZZLE_MARKET_TITLE, session.ItemDefinitions, _showSynergies: false);
        cityView.Initialize(session.CanTravel, session.GameClearCost);
        ShowCurrentCity();
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
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PanelVisibility += OnPanelVisibility;
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
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PanelVisibility -= OnPanelVisibility;
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
        if (!initialized || isMapOpen || drag == null || !trainBarView || session == null ||
            session.RunStatus != PuzzleRunStatus.InProgress)
        {
            hoveredWagonIndex = null;
            return;
        }

        if (trainBarView.TryGetWagonAtScreenPosition(pointerPosition, out int wagonIndex) &&
            wagonIndex != session.CurrentWagonIndex)
        {
            if (hoveredWagonIndex != wagonIndex)
            {
                hoveredWagonIndex = wagonIndex;
                hoveredWagonStartTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - hoveredWagonStartTime >= WAGON_DRAG_HOVER_DELAY)
            {
                hoveredWagonIndex = null;
                SwitchWagonDuringDrag(wagonIndex);
            }
        }
        else
        {
            hoveredWagonIndex = null;
        }
    }

    void SwitchWagonDuringDrag(int _index)
    {
        if (!session.SelectWagon(_index))
        {
            return;
        }

        SetDragView(null);
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height, marketView.CellSize);
        RenderBoards();
        RenderValues();
        RefreshTrainBar();
        RefreshDrag();
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        ShowCurrentCity();
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        cityView.ShowTime(session.Time);
        RefreshFunds();
        if (session.RunStatus != PuzzleRunStatus.InProgress)
        {
            cityView.ShowTradeStatus(Presentation.PuzzleRunStatusText(session.RunStatus));
        }
    }

    public void ClearGame()
    {
        if (!initialized || !isActiveAndEnabled || isMapOpen || !pauseMenu || !pauseMenu.CanShowCompletion ||
            !session.TryClearGame())
        {
            return;
        }

        pauseMenu.ShowCompletion(session.Funds);
        RefreshFunds();
        cityView.ShowTradeStatus(Presentation.PUZZLE_CLEAR_TITLE);
        RefreshTrainBar();
    }

    void RefreshFunds()
    {
        cityView.ShowFunds(session.Funds, session.CanClearGame);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel != Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            return;
        }

        isMapOpen = _info.open;
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
        cityView.ShowCity(session.CurrentCityName);
        cityView.ShowTime(session.Time);
        RefreshFunds();
        cityView.ShowTradeStatus(inProgress
            ? Presentation.PUZZLE_TRADE_GUIDE
            : Presentation.PuzzleRunStatusText(session.RunStatus));
        marketView.SetBoardSize(session.Market.Width, session.Market.Height);
        trainView.SetBoardSize(session.Train.Width, session.Train.Height, marketView.CellSize);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

    void OnPressed(PointerEventData _eventData)
    {
        if (isMapOpen || session.RunStatus != PuzzleRunStatus.InProgress)
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
        cityView.ShowTradeStatus(Presentation.PUZZLE_TRADE_GUIDE);
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
        if (isMapOpen || drag != null || session.RunStatus != PuzzleRunStatus.InProgress ||
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
            RenderValues();
            RefreshFunds();
            RefreshTrainBar();
        }
        RefreshHover();
        cityView.ShowTradeStatus(Presentation.PuzzleTradeStatus(result, placement.Definition, _committed: true));
    }

    void OnDragged(PointerEventData _eventData)
    {
        if (isMapOpen)
        {
            return;
        }

        pointerPosition = _eventData.position;
        hoverPosition = _eventData.position;
        RefreshDrag();
    }

    void OnRotated()
    {
        if (isMapOpen || drag == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        drag.RotateClockwise();
        RefreshDrag();
    }

    void OnReleased(PointerEventData _eventData)
    {
        if (isMapOpen)
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
                    RenderValues();
                    RefreshFunds();
                    RefreshTrainBar();
                }
            }
            else if (trainBarView && trainBarView.TryGetWagonAtScreenPosition(_eventData.position, out droppedWagonIndex))
            {
                result = activeDrag.QuickDropToWagon(session, droppedWagonIndex);
                if (result.CanDrop)
                {
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
            cityView.ShowTradeStatus(Presentation.PUZZLE_TRADE_GUIDE);
            return;
        }

        if (TryGetBoard(pointerPosition, out var targetView, out var destination, out var targetCell))
        {
            var preview = drag.Evaluate(session, destination, targetCell);
            SetDragView(targetView);
            var candidate = preview.Placement.Candidate;
            var grabbedBoardCell = candidate != null ? targetCell : Vector2Int.zero;
            targetView.ShowGhost(candidate ?? drag.GetGhostPose(), drag.Definition, grabbedBoardCell, pointerPosition);
            targetView.ShowPreview(candidate, preview.CanDrop);
            cityView.ShowTradeStatus(Presentation.PuzzleTradeStatus(preview, drag.Definition, _committed: false));
            return;
        }

        var sourceView = session.IsTrainWagon(drag.Source) ? trainView : marketView;
        SetDragView(sourceView);
        sourceView.ShowGhost(drag.GetGhostPose(), drag.Definition, Vector2Int.zero, pointerPosition);
        sourceView.ShowPreview(null, false);
        cityView.ShowTradeStatus(Presentation.PUZZLE_TRADE_GUIDE);
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
            if (marketView && marketView.isActiveAndEnabled && marketView.TryGetBoardCell(_position, out _cell))
            {
                _view = marketView;
                _inventory = session.Market;
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
        if (isMapOpen)
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
        if (isMapOpen)
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
        if (isMapOpen || drag != null)
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
            return;
        }

        var item = _placement.Definition;
        var value = _placement.Owner.ValueOf(_placement.InstanceId);
        bool isTrain = _view == trainView || session.IsTrainWagon(_placement.Owner);
        decimal actualValue = isTrain ? _placement.ActualValue : _placement.Definition.BaseValue;
        var rate = item == null ? default : session.RateFor(item);
        var bonuses = isTrain ? value.SynergyBonuses : null;
        decimal? futureValue = isTrain ? actualValue + (value.FinalValue - value.BaseValue) : null;

        if (tooltipView && drag == null && (_position.HasValue || hoverPosition.HasValue))
        {
            Vector2 pos = _position ?? hoverPosition.Value;
            tooltipView.Show(item, actualValue, rate, bonuses, futureValue, pos, isTrain);
        }

        if (trainView)
        {
            trainView.ShowItemDetails(_view == trainView ? item : null, value, actualValue, rate);
        }
        if (marketView)
        {
            marketView.ShowItemDetails(_view == marketView ? item : null, value, actualValue, rate);
        }
    }

    void RenderValues()
    {
        if (trainView)
        {
            trainView.ShowBoardValue(session.Train.Value);
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
            trainView.RenderPlacements(session.Train, drag?.InstanceId, _highlightedInstanceId);
        }
        if (marketView)
        {
            marketView.RenderPlacements(session.Market, drag?.InstanceId, _highlightedInstanceId);
        }
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
        hoveredWagonIndex = null;
        if (drag == null)
        {
            return;
        }

        drag = null;
        SetDragView(null);
        if (cityView && session != null && session.RunStatus == PuzzleRunStatus.InProgress)
        {
            cityView.ShowTradeStatus(Presentation.PUZZLE_TRADE_GUIDE);
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
            trainBarView.Refresh(session.WagonCount, session.CurrentWagonIndex, session.NextWagonCost, session.CanAddWagon);
        }
    }

    void OnWagonSelected(int _index)
    {
        if (isMapOpen || session == null || session.RunStatus != PuzzleRunStatus.InProgress)
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
        trainView.SetBoardSize(session.Train.Width, session.Train.Height, marketView.CellSize);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }

    void OnAddWagonClicked()
    {
        if (isMapOpen || session == null)
        {
            return;
        }

        input.CancelCapture();
        ClearDrag();
        if (!session.TryAddWagon(out string failureReason))
        {
            if (!string.IsNullOrEmpty(failureReason))
            {
                cityView.ShowTradeStatus(failureReason);
            }
            return;
        }

        RefreshFunds();
        trainView.SetTitle(Presentation.PuzzleTrainTitle(session.CurrentWagonIndex + 1));
        trainView.SetBoardSize(session.Train.Width, session.Train.Height, marketView.CellSize);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
        RefreshTrainBar();
    }
}
