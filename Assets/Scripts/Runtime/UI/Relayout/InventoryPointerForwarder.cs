using UnityEngine;
using UnityEngine.EventSystems;

// 개편 레이아웃의 창 보드는 입력 담당(Inventory Input Area)의 자식이 아니라서 포인터 이벤트가 그쪽으로 올라가지 않는다.
// 보드가 받은 클릭·끌기·마우스 올림 이벤트를 입력 담당에 그대로 넘겨 옛 보드와 같은 경로로 처리되게 한다.
[DisallowMultipleComponent]
public sealed class InventoryPointerForwarder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    static PuzzleInventoryInput cachedInput;

    static PuzzleInventoryInput Target
    {
        get
        {
            if (!cachedInput)
            {
                cachedInput = FindFirstObjectByType<PuzzleInventoryInput>();
            }
            return cachedInput;
        }
    }

    public void OnPointerDown(PointerEventData _eventData) { var target = Target; if (target) target.OnPointerDown(_eventData); }
    public void OnPointerUp(PointerEventData _eventData) { var target = Target; if (target) target.OnPointerUp(_eventData); }
    public void OnInitializePotentialDrag(PointerEventData _eventData) { var target = Target; if (target) target.OnInitializePotentialDrag(_eventData); }
    public void OnDrag(PointerEventData _eventData) { var target = Target; if (target) target.OnDrag(_eventData); }
    public void OnPointerEnter(PointerEventData _eventData) { var target = Target; if (target) target.OnPointerEnter(_eventData); }
    public void OnPointerMove(PointerEventData _eventData) { var target = Target; if (target) target.OnPointerMove(_eventData); }
    public void OnPointerExit(PointerEventData _eventData) { var target = Target; if (target) target.OnPointerExit(_eventData); }
}
