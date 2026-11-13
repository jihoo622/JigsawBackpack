using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleWagonProductRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image artwork;
    [SerializeField] Image dragPreview;
    PuzzleWagonProductData product;
    int dragPointerId;
    PuzzleWagonProductData draggedProduct;

    internal void Bind(PuzzleWagonProductData _product)
    {
        CancelDrag();
        product = _product;
        gameObject.SetActive(product != null);
        if (product != null)
        {
            label.text = Presentation.WagonProductLabel(product.displayName, product.price);
        }
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (_eventData.button != PointerEventData.InputButton.Left || product == null ||
            dragPreview.gameObject.activeSelf)
        {
            return;
        }

        draggedProduct = product;
        dragPointerId = _eventData.pointerId;
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
        if (draggedProduct != null)
        {
            draggedProduct = null;
            dragPreview.gameObject.SetActive(false);
        }
    }
}
