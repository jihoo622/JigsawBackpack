using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryPresenter : MonoBehaviour
{
    [SerializeField] PuzzleInventoryView trainView;
    [SerializeField] PuzzleInventoryView marketView;
    [SerializeField] PuzzleCityView cityView;
    [SerializeField] PuzzleItemTooltipView tooltipView;

    PuzzleInventoryInput input;
    PuzzleInventoryService session;
    PuzzleInventoryDrag drag;
    PuzzleInventoryView dragView;
    Vector2 pointerPosition;
    Vector2? hoverPosition;
    int? currentHoveredInstanceId;
    bool initialized;

    void OnEnable()
    {
        if (!TryGetComponent(out input) || !trainView || !marketView || trainView == marketView || !cityView ||
            !trainView.transform.IsChildOf(transform) || !marketView.transform.IsChildOf(transform) ||
            !cityView.transform.IsChildOf(transform))
        {
            Debug.LogError("화물 인벤토리 Presenter에는 Input과 하위의 기차·시장·도시 View가 필요합니다.", this);
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
        trainView.Initialize(trainCapacity, dragCapacity, Presentation.PUZZLE_TRAIN_TITLE, session.ItemDefinitions, _showSynergies: true);
        marketView.Initialize(session.MarketCapacity, dragCapacity, Presentation.PUZZLE_MARKET_TITLE, session.ItemDefinitions, _showSynergies: false);
        cityView.Initialize(session.CanTravel);
        trainView.SetBoardSize(session.Train.Width, session.Train.Height);
        ShowCurrentCity();
        initialized = true;

        input.Pressed += OnPressed;
        input.Dragged += OnDragged;
        input.Released += OnReleased;
        input.Rotated += OnRotated;
        input.Canceled += ClearDrag;
        input.Hovered += OnHovered;
        input.HoverEnded += OnHoverEnded;
        trainView.Disabled += OnViewDisabled;
        marketView.Disabled += OnViewDisabled;
        cityView.Subscribe(OnPreviousCity, OnNextCity);
        GameEvents.PanelVisibility += OnPanelVisibility;
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
        if (cityView)
        {
            cityView.Unsubscribe(OnPreviousCity, OnNextCity);
        }
        if (input)
        {
            input.Pressed -= OnPressed;
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
        GameEvents.PanelVisibility -= OnPanelVisibility;
        OnViewDisabled();
        initialized = false;
    }

    void OnPreviousCity()
    {
        Travel(false);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (!_info.open || _info.panel != Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            return;
        }
        hoverPosition = null;
        currentHoveredInstanceId = null;
        input.CancelCapture();
        ClearDrag();
        if (tooltipView)
        {
            tooltipView.Close();
        }
    }

    void OnNextCity()
    {
        Travel(true);
    }

    void Travel(bool _forward)
    {
        if (session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        hoverPosition = null;
        input.CancelCapture();
        ClearDrag();
        var result = session.Travel(_forward);
        if (!result.IsSuccess)
        {
            // 비용 부족은 이동 차단이다. 도시·턴·시장·자금을 바꾸지 않고 이유만 표시한다.
            cityView.ShowTradeStatus(Presentation.PuzzleTravelStatus(result));
            return;
        }
        ShowCurrentCity();
    }

    void ShowCurrentCity()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.ShowCity(session.CurrentCityName);
        cityView.ShowFunds(session.Funds);
        cityView.ShowTravelCost(session.NextRouteCost(false), session.NextRouteCost(true));
        cityView.SetTravelEnabled(inProgress && session.CanTravel);
        cityView.ShowTradeStatus(inProgress
            ? Presentation.PUZZLE_TRADE_GUIDE
            : Presentation.PuzzleRunStatusText(session.RunStatus));
        marketView.SetBoardSize(session.Market.Width, session.Market.Height);
        RenderBoards();
        RenderValues();
        ShowItemDetails(null, null);
    }

    void OnPressed(PointerEventData _eventData)
    {
        if (session.RunStatus != PuzzleRunStatus.InProgress)
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

    void OnDragged(PointerEventData _eventData)
    {
        pointerPosition = _eventData.position;
        hoverPosition = _eventData.position;
        RefreshDrag();
    }

    void OnRotated()
    {
        if (drag == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }

        drag.RotateClockwise();
        RefreshDrag();
    }

    void OnReleased(PointerEventData _eventData)
    {
        hoverPosition = _eventData.position;
        var activeDrag = drag;
        PuzzleDropResult result = default;
        try
        {
            if (TryGetBoard(_eventData.position, out _, out var destination, out var targetCell))
            {
                result = activeDrag.DropOnBoard(session, destination, targetCell);
                if (result.CanDrop)
                {
                    RenderValues();
                    cityView.ShowFunds(session.Funds);
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

        var sourceView = drag.Source == session.Train ? trainView : marketView;
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
        hoverPosition = null;
        currentHoveredInstanceId = null;
        RenderBoards();
        ShowItemDetails(null, null);
    }

    void RefreshHover()
    {
        if (drag != null)
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
        bool isTrain = _view == trainView || _placement.Owner == session.Train;
        decimal actualValue = isTrain ? _placement.ActualValue : _placement.Definition.BaseValue;
        var rate = item == null ? default : session.RateFor(item);
        var bonuses = isTrain ? value.SynergyBonuses : null;
        decimal? futureValue = isTrain ? actualValue + (value.FinalValue - value.BaseValue) : null;

        if (tooltipView && drag == null && (_position.HasValue || hoverPosition.HasValue))
        {
            Vector2 pos = _position ?? hoverPosition.Value;
            tooltipView.Show(item, actualValue, rate, bonuses, futureValue, pos);
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
}
