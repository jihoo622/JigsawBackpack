using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryPresenter : MonoBehaviour
{
    [SerializeField] PuzzleInventoryView trainView;
    [SerializeField] PuzzleInventoryView marketView;
    [SerializeField] PuzzleCityView cityView;

    PuzzleInventoryInput input;
    PuzzleInventoryService session;
    PuzzleInventoryDrag drag;
    PuzzleInventoryView dragView;
    Vector2 pointerPosition;
    Vector2? hoverPosition;
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
        OnViewDisabled();
        initialized = false;
    }

    void OnPreviousCity()
    {
        Travel(false);
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
        if (!session.Travel(_forward))
        {
            return;
        }
        ShowCurrentCity();
    }

    void ShowCurrentCity()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        cityView.ShowCity(session.CurrentCityName);
        cityView.ShowFunds(session.Funds);
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
        if (!TryGetBoard(position, out var sourceView, out var source, out var boardCell) ||
            !source.TryGetCell(boardCell, out var placement, out var cell))
        {
            return;
        }

        drag = new PuzzleInventoryDrag(placement, cell);
        cityView.ShowTradeStatus(Presentation.PUZZLE_TRADE_GUIDE);
        pointerPosition = position;
        input.CapturePointer(_eventData.pointerId);
        RenderBoards();
        ShowItemDetails(sourceView, placement);
        RefreshDrag();
    }

    void OnDragged(PointerEventData _eventData)
    {
        pointerPosition = _eventData.position;
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
        if (hoverPosition != _position)
        {
            hoverPosition = _position;
            RefreshHover();
        }
    }

    void OnHoverEnded()
    {
        if (hoverPosition.HasValue)
        {
            hoverPosition = null;
            RefreshHover();
        }
    }

    void RefreshHover()
    {
        if (drag != null)
        {
            return;
        }

        if (hoverPosition is Vector2 position && TryGetBoard(position, out var view, out var board, out var boardCell) &&
            board.TryGetCell(boardCell, out var placement, out _))
        {
            RenderBoards(placement.InstanceId);
            ShowItemDetails(view, placement);
            return;
        }

        RenderBoards();
        ShowItemDetails(null, null);
    }

    void ShowItemDetails(PuzzleInventoryView _view, PuzzlePlacement _placement)
    {
        var item = _placement?.Definition;
        var value = _placement == null ? default : _placement.Owner.ValueOf(_placement.InstanceId);
        decimal actualValue = _placement?.ActualValue ?? default;
        var rate = item == null ? default : session.RateFor(item);
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
