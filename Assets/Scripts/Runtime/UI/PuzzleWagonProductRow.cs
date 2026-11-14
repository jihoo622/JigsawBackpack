using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 화차 상점 카드. 누르면 선택을 토글하고, 끌기 시작하면 선택하며 커서 옆에 화차 미리보기를 띄운다.
public sealed class PuzzleWagonProductRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image artwork;
    [SerializeField] Image dragPreview;
    [SerializeField] Image selectionHighlight;
    [SerializeField] GameObject chipRoot;
    [SerializeField] TextMeshProUGUI chipLabel;
    // 끄는 동안 미리보기 크기(화면 방향 가로×세로 상자)와 커서 기준 중심 위치. 화물칸 바 슬롯과 비슷한 크기로 프리팹에서 정한다.
    [SerializeField] Vector2 dragPreviewBox;
    [SerializeField] Vector2 dragPreviewOffset;

    internal event Action<PuzzleWagonProductRow> Clicked;
    internal event Action<PuzzleWagonProductRow> DragStarted;
    internal PuzzleWagonProductData Product => product;
    internal Sprite ArtworkSprite => artwork != null ? artwork.sprite : null;

    PuzzleWagonProductData product;
    int dragPointerId;
    PuzzleWagonProductData draggedProduct;

    static PuzzleWagonProductRow activeDraggingRow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        activeDraggingRow = null;
    }

    // 화물칸 바가 드래그 중에만 다음 빈 칸 자리에 드롭 위치 힌트를 띄우는 데 쓴다.
    internal static bool IsDraggingProduct => activeDraggingRow != null && activeDraggingRow.draggedProduct != null;

    public static bool TryConsumeCancel()
    {
        if (activeDraggingRow != null && activeDraggingRow.draggedProduct != null)
        {
            activeDraggingRow.CancelDrag();
            activeDraggingRow = null;
            return true;
        }
        return false;
    }

    internal void Bind(PuzzleWagonProductData _product)
    {
        CancelDrag();
        product = _product;
        gameObject.SetActive(product != null);
        if (product != null)
        {
            label.text = Presentation.WagonProductLabel(product.displayName, product.price);
            if (chipLabel != null)
            {
                chipLabel.text = "+1칸";
            }
            if (chipRoot != null)
            {
                chipRoot.SetActive(true);
            }
        }
    }

    // 끌다가 같은 카드 위에서 놓은 경우는 클릭으로 보지 않는다(끌기 시작이 이미 선택했다).
    public void OnPointerClick(PointerEventData _eventData)
    {
        if (product != null && !_eventData.dragging)
        {
            Clicked?.Invoke(this);
        }
    }

    // 선택 표시는 속이 빈 테두리이며 의뢰 게시판·화물칸 바 선택 테두리와 같은 색을 쓴다(카드를 칠하지 않는다).
    internal void SetSelected(bool _selected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.color = Presentation.PuzzleWagonSelectedBorder;
            selectionHighlight.gameObject.SetActive(_selected);
        }
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (_eventData.button != PointerEventData.InputButton.Left || product == null ||
            dragPreview.gameObject.activeSelf)
        {
            return;
        }

        DragStarted?.Invoke(this);

        draggedProduct = product;
        dragPointerId = _eventData.pointerId;
        activeDraggingRow = this;
        dragPreview.sprite = artwork.sprite;
        dragPreview.color = artwork.color;
        // 카드 그림 칸(가로로 긴 상자)을 그대로 복사하지 않고, 카드 그림과 같은 회전으로 원본 종횡비를 지켜 미리보기 상자에 맞춘다.
        dragPreview.rectTransform.localRotation = artwork.rectTransform.localRotation;
        dragPreview.rectTransform.sizeDelta = FitPreviewSize(artwork.sprite, artwork.rectTransform.localEulerAngles.z);
        dragPreview.gameObject.SetActive(true);
        OnDrag(_eventData);
    }

    // 화물칸 바(fitRotatedWagonArtwork)와 같은 규칙: 90도 돌린 그림은 화면 가로 상자를 세로로 바꿔 넣고 종횡비를 유지한다.
    Vector2 FitPreviewSize(Sprite _sprite, float _angle)
    {
        bool sideways = Mathf.Abs(Mathf.DeltaAngle(_angle, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(_angle, -90f)) < 1f;
        var box = sideways ? new Vector2(dragPreviewBox.y, dragPreviewBox.x) : dragPreviewBox;
        if (_sprite == null || _sprite.rect.height <= 0f)
        {
            return box;
        }
        float aspect = _sprite.rect.width / _sprite.rect.height;
        float width = Mathf.Min(box.x, box.y * aspect);
        return new Vector2(width, width / aspect);
    }

    // 미리보기는 커서를 가리지 않게 커서 오른쪽 아래(dragPreviewOffset)에 둔다. 드롭 판정은 커서 위치 기준이다.
    public void OnDrag(PointerEventData _eventData)
    {
        var parent = dragPreview.rectTransform.parent as RectTransform;
        if (draggedProduct != null && dragPointerId == _eventData.pointerId && parent != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parent, _eventData.position, _eventData.pressEventCamera, out var position))
        {
            dragPreview.rectTransform.position = position + parent.TransformVector(dragPreviewOffset);
        }
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        if (dragPointerId == _eventData.pointerId)
        {
            CancelDrag();
        }
    }

    internal PuzzleWagonProductData TakeDroppedProduct(int _pointerId)
    {
        if (dragPointerId != _pointerId)
        {
            return null;
        }

        var droppedProduct = draggedProduct;
        CancelDrag();
        return droppedProduct;
    }

    void OnDisable() => CancelDrag();

    void OnApplicationFocus(bool _focused)
    {
        if (!_focused)
        {
            CancelDrag();
        }
    }

    void CancelDrag()
    {
        if (activeDraggingRow == this)
        {
            activeDraggingRow = null;
        }
        if (draggedProduct != null)
        {
            draggedProduct = null;
            dragPreview.gameObject.SetActive(false);
        }
    }
}
