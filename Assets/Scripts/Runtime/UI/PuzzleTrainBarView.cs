using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleTrainBarView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Serializable]
    public sealed class WagonSlotUI
    {
        public GameObject root;
        public RectTransform rectTransform;
        public Button button;
        public Image wagonImage;
        public Image highlightBorder;
        public TextMeshProUGUI numberText;
    }

    [Header("Slots Pool")]
    [SerializeField] WagonSlotUI[] wagonSlots;
    [SerializeField] bool fitRotatedWagonArtwork;
    [SerializeField] Image wagonDragPreview;

    [Header("Layout Settings")]
    [SerializeField] float wagonWidth = 150f;
    [SerializeField] float wagonHeight = 56f;
    [SerializeField] float wagonSpacing = 50f;
    [SerializeField] float wagonDragHoverScale = 1.08f;
    [SerializeField] float originX = 0f;
    [SerializeField] float originY = 0f;

    int visibleWagonCount = -1;
    int? draggedWagonIndex;
    int dragPointerId;

    public event Action<int> WagonSelected;
    internal event Action<PuzzleWagonProductData> WagonProductDropped;
    internal event Action<int, int> WagonMoved;

    void Awake()
    {
        int maxWagonCount = TableManager.Instance?.PuzzleTrade?.maxWagonCount ?? 0;
        if (maxWagonCount > 0 && (wagonSlots == null || wagonSlots.Length < maxWagonCount))
        {
            throw new InvalidOperationException($"객차 바 슬롯 수({wagonSlots?.Length ?? 0})가 최대 객차 수({maxWagonCount})보다 적습니다.");
        }

        if (wagonSlots != null)
        {
            for (int i = 0; i < wagonSlots.Length; i++)
            {
                int index = i;
                if (wagonSlots[i]?.button != null)
                {
                    wagonSlots[i].button.onClick.AddListener(() => OnWagonSelected(index));
                }
            }
        }
    }

    void OnDestroy()
    {
        if (wagonSlots != null)
        {
            for (int i = 0; i < wagonSlots.Length; i++)
            {
                if (wagonSlots[i]?.button != null)
                {
                    wagonSlots[i].button.onClick.RemoveAllListeners();
                }
            }
        }
    }

    void OnWagonSelected(int _index)
    {
        WagonSelected?.Invoke(_index);
    }

    public void OnDrop(PointerEventData _eventData)
    {
        if (_eventData.pointerDrag == gameObject && draggedWagonIndex is int source &&
            _eventData.pointerId == dragPointerId)
        {
            CancelWagonDrag();
            if (TryGetWagonAtScreenPosition(_eventData.position, out int target))
            {
                WagonMoved?.Invoke(source, target);
            }
            return;
        }

        if (_eventData.button == PointerEventData.InputButton.Left && _eventData.pointerDrag &&
            _eventData.pointerDrag.TryGetComponent<PuzzleWagonProductRow>(out var row) &&
            row.TakeDroppedProduct(_eventData.pointerId) is { } product)
        {
            WagonProductDropped?.Invoke(product);
        }
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (_eventData.button != PointerEventData.InputButton.Left || visibleWagonCount < 2 ||
            !wagonDragPreview || wagonDragPreview.gameObject.activeSelf ||
            !TryGetWagonAtScreenPosition(_eventData.pressPosition, out int index))
        {
            return;
        }

        var artwork = wagonSlots[index].wagonImage;
        draggedWagonIndex = index;
        dragPointerId = _eventData.pointerId;
        wagonDragPreview.sprite = artwork.sprite;
        wagonDragPreview.color = artwork.color;
        wagonDragPreview.rectTransform.sizeDelta = artwork.rectTransform.rect.size;
        wagonDragPreview.rectTransform.rotation = artwork.rectTransform.rotation;
        wagonDragPreview.gameObject.SetActive(true);
        OnDrag(_eventData);
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (draggedWagonIndex.HasValue && _eventData.pointerId == dragPointerId &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(wagonDragPreview.rectTransform,
                _eventData.position, _eventData.pressEventCamera, out var position))
        {
            wagonDragPreview.rectTransform.position = position;
        }
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        if (_eventData.pointerId == dragPointerId)
        {
            CancelWagonDrag();
        }
    }

    internal void CancelWagonDrag()
    {
        if (draggedWagonIndex.HasValue)
        {
            draggedWagonIndex = null;
            wagonDragPreview.gameObject.SetActive(false);
        }
    }

    void OnDisable() => CancelWagonDrag();

    void OnApplicationFocus(bool _focused)
    {
        if (!_focused)
        {
            CancelWagonDrag();
        }
    }

    public bool TryGetWagonAtScreenPosition(Vector2 _screenPosition, out int _wagonIndex)
    {
        _wagonIndex = -1;
        if (wagonSlots == null)
        {
            return false;
        }

        var canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot?.root != null && slot.root.activeInHierarchy && slot.rectTransform != null &&
                RectTransformUtility.RectangleContainsScreenPoint(slot.rectTransform, _screenPosition, eventCamera))
            {
                _wagonIndex = i;
                return true;
            }
        }

        return false;
    }

    internal void Refresh(IReadOnlyList<PuzzleInventory> _wagons, int _selectedIndex, int? _dragHoveredIndex = null)
    {
        if (wagonSlots == null)
        {
            return;
        }

        int maxWagonCount = TableManager.Instance?.PuzzleTrade?.maxWagonCount ?? 0;
        if (maxWagonCount > 0 && wagonSlots.Length < maxWagonCount)
        {
            throw new InvalidOperationException($"객차 바 슬롯 수({wagonSlots.Length})가 최대 객차 수({maxWagonCount})보다 적습니다.");
        }

        visibleWagonCount = Mathf.Min(_wagons.Count, wagonSlots.Length);

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot == null || slot.root == null)
            {
                continue;
            }

            bool active = i < _wagons.Count;
            slot.root.SetActive(active);

            if (active)
            {
                bool isSelected = i == _selectedIndex;
                bool isDragHovered = _dragHoveredIndex == i;
                if (slot.rectTransform != null)
                {
                    slot.rectTransform.localScale = isDragHovered ? Vector3.one * wagonDragHoverScale : Vector3.one;
                }

                if (slot.numberText != null)
                {
                    slot.numberText.text = _wagons[i].WagonNumber.ToString();
                }

                if (slot.highlightBorder != null)
                {
                    slot.highlightBorder.enabled = isSelected || isDragHovered;
                    slot.highlightBorder.color = isDragHovered
                        ? Presentation.PuzzleWagonDragHoverBorder
                        : Presentation.PuzzleWagonSelectedBorder;
                }
            }
        }

        LayoutButtons();
    }

    void OnRectTransformDimensionsChange()
    {
        // 상위 레이아웃 그룹이 첫 갱신 뒤에 너비를 지정할 수 있다.
        LayoutButtons();
    }

    void LayoutButtons()
    {
        if (visibleWagonCount <= 0)
        {
            return;
        }

        float available = ((RectTransform)transform).rect.width;
        if (available <= 0f)
        {
            return;
        }

        int count = visibleWagonCount;
        float gap = Mathf.Min(wagonSpacing, available / (count * 2f));
        float width = Mathf.Min(wagonWidth, (available - gap * (count - 1)) / count);
        float step = width + gap;
        float start = originX + (count - 1) * step * 0.5f;
        float y = originY;
        var size = new Vector2(width, wagonHeight);

        for (int i = 0; i < visibleWagonCount; i++)
        {
            var rect = wagonSlots[i]?.rectTransform;
            if (rect)
            {
                rect.anchoredPosition = new Vector2(start - i * step, y);
                rect.sizeDelta = size;
                if (fitRotatedWagonArtwork && wagonSlots[i].wagonImage && wagonSlots[i].wagonImage.sprite)
                {
                    var image = wagonSlots[i].wagonImage;
                    float aspect = image.sprite.rect.width / image.sprite.rect.height;
                    float height = Mathf.Min(size.x, size.y / aspect);
                    image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
                }
            }
        }
    }
}
