using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleWagonProductRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image artwork;
    [SerializeField] Image dragPreview;
    [SerializeField] Image selectionHighlight;
    [SerializeField] GameObject chipRoot;
    [SerializeField] TextMeshProUGUI chipLabel;

    internal event Action<PuzzleWagonProductRow, PuzzleWagonProductData> Selected;
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

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (product != null)
        {
            Selected?.Invoke(this, product);
        }
    }

    internal void SetSelected(bool _selected)
    {
        if (selectionHighlight != null)
        {
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

        Selected?.Invoke(this, product);

        draggedProduct = product;
        dragPointerId = _eventData.pointerId;
        activeDraggingRow = this;
        dragPreview.sprite = artwork.sprite;
        dragPreview.color = artwork.color;
        dragPreview.rectTransform.sizeDelta = artwork.rectTransform.rect.size;
        dragPreview.rectTransform.localRotation = artwork.rectTransform.localRotation;
        dragPreview.gameObject.SetActive(true);
        OnDrag(_eventData);
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (draggedProduct != null && dragPointerId == _eventData.pointerId &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                dragPreview.rectTransform, _eventData.position, _eventData.pressEventCamera, out var position))
        {
            dragPreview.rectTransform.position = position;
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
