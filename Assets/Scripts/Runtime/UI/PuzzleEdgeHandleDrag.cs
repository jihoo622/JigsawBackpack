using UnityEngine;
using UnityEngine.EventSystems;

// 화면 가장자리 손잡이를 세로로만 끌어 옮긴다. 움직일 수 있는 범위(위·아래 한계)는 프리팹 직렬화 값이 원본이다.
// 끄는 동안에는 입력 모듈이 클릭을 취소하므로, 같은 오브젝트의 Button은 짧게 눌렀을 때만 반응한다.
// 옮긴 위치는 이 씬이 열려 있는 동안만 유지한다.
[RequireComponent(typeof(RectTransform))]
public sealed class PuzzleEdgeHandleDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] float minTop = 140f;
    [SerializeField] float maxTop = 760f;
    RectTransform rect;
    Canvas canvas;

    void Awake()
    {
        rect = (RectTransform)transform;
        var parentCanvas = GetComponentInParent<Canvas>();
        canvas = parentCanvas ? parentCanvas.rootCanvas : null;
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (!canvas)
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            canvas = parentCanvas ? parentCanvas.rootCanvas : null;
        }
    }

    public void OnDrag(PointerEventData _eventData)
    {
        float scale = canvas && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        var position = rect.anchoredPosition;
        position.y += _eventData.delta.y / scale;
        // 위쪽 앵커 기준: top = -(y) - (1 - pivot.y) * height
        float height = rect.rect.height;
        float top = -position.y - (1f - rect.pivot.y) * height;
        top = Mathf.Clamp(top, minTop, maxTop);
        position.y = -top - (1f - rect.pivot.y) * height;
        rect.anchoredPosition = position;
    }
}
