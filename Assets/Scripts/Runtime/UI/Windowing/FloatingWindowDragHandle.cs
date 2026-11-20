using UnityEngine;
using UnityEngine.EventSystems;

// 부동 창 제목줄 드래그 핸들.
public sealed class FloatingWindowDragHandle : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] FloatingWindow window;

    Vector2 startLocalPoint;
    bool dragging;

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
        if (!window || _eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        if (TryGetParentLocalPoint(_eventData, out startLocalPoint))
        {
            dragging = true;
            window.BeginDrag();
        }
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (!dragging || !window)
        {
            return;
        }
        if (TryGetParentLocalPoint(_eventData, out var current))
        {
            Vector2 deltaUnity = current - startLocalPoint;
            // Unity Y+는 위, 좌상단 원점 Y+는 아래이므로 Y 부호를 반전한다.
            window.UpdateDrag(new Vector2(deltaUnity.x, -deltaUnity.y));
        }
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        if (!dragging || !window)
        {
            return;
        }
        dragging = false;
        window.EndDrag();
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
