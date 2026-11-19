using UnityEngine;
using UnityEngine.EventSystems;

// 부동 창의 4변(8px) 및 우하단 모서리(18×18px) 크기 조절 핸들.
public sealed class FloatingWindowResizeHandle : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] FloatingWindow window;
    [SerializeField] FloatingWindowMath.ResizeEdge edge = FloatingWindowMath.ResizeEdge.BottomRight;

    Vector2 startLocalPoint;
    bool resizing;

    void Awake()
    {
        if (!window)
        {
            window = GetComponentInParent<FloatingWindow>();
        }
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (window)
        {
            window.BringToFront();
        }
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (!window || edge == FloatingWindowMath.ResizeEdge.None ||
            _eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        if (TryGetParentLocalPoint(_eventData, out startLocalPoint))
        {
            resizing = true;
            window.BeginResize();
        }
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (!resizing || !window)
        {
            return;
        }
        if (TryGetParentLocalPoint(_eventData, out var current))
        {
            Vector2 deltaUnity = current - startLocalPoint;
            window.UpdateResize(edge, new Vector2(deltaUnity.x, -deltaUnity.y));
        }
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        if (!resizing || !window)
        {
            return;
        }
        resizing = false;
        window.EndResize();
    }

    bool TryGetParentLocalPoint(PointerEventData _eventData, out Vector2 _localPoint)
    {
        var parentRect = window.transform.parent as RectTransform;
        if (!parentRect)
        {
            _localPoint = default;
            return false;
        }
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, _eventData.position, _eventData.pressEventCamera, out _localPoint);
    }
}
