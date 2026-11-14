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

    int ownedWagonCount = -1;
    bool dropHintVisible;
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

    void OnDisable()
    {
        CancelWagonDrag();
    }

    void Update()
    {
        // 화차 카드를 드래그하는 동안에만 다음 빈 칸 자리에 드롭 위치 힌트를 보인다.
        bool dragging = PuzzleWagonProductRow.IsDraggingProduct;
        if (dragging == dropHintVisible)
        {
            return;
        }
        dropHintVisible = dragging;
        if (ownedWagonCount >= 0 && wagonSlots != null && ownedWagonCount < wagonSlots.Length &&
            wagonSlots[ownedWagonCount]?.root != null)
        {
            RefreshUnownedSlot(wagonSlots[ownedWagonCount], ownedWagonCount);
        }
    }

    void OnWagonSelected(int _index)
    {
        if (_index >= ownedWagonCount)
        {
            return;
        }
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
        if (_eventData.button != PointerEventData.InputButton.Left || ownedWagonCount < 2 ||
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

    public bool TryConsumeCancel()
    {
        if (draggedWagonIndex.HasValue)
        {
            CancelWagonDrag();
            return true;
        }
        return false;
    }

    internal void CancelWagonDrag()
    {
        if (draggedWagonIndex.HasValue)
        {
            draggedWagonIndex = null;
            wagonDragPreview.gameObject.SetActive(false);
        }
    }

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

        // 미보유 칸은 숨기며, 드롭 위치 힌트로 보여도 히트 대상이 아니다(전환·순서 변경·드래그 시작 불가).
        // 화차 카드 증차 드롭은 슬롯과 무관하게 화물칸 바 전체 영역에서 받는다(OnDrop).
        int owned = Mathf.Max(0, ownedWagonCount);
        for (int i = 0; i < wagonSlots.Length && i < owned; i++)
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

        ownedWagonCount = Mathf.Min(_wagons.Count, wagonSlots.Length);
        dropHintVisible = PuzzleWagonProductRow.IsDraggingProduct;

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot == null || slot.root == null)
            {
                continue;
            }

            if (i >= ownedWagonCount)
            {
                RefreshUnownedSlot(slot, i);
                continue;
            }

            slot.root.SetActive(true);
            if (slot.button != null)
            {
                slot.button.interactable = true;
            }

            if (slot.rectTransform != null)
            {
                slot.rectTransform.localScale = _dragHoveredIndex == i ? Vector3.one * wagonDragHoverScale : Vector3.one;
            }

            if (slot.wagonImage != null)
            {
                slot.wagonImage.enabled = true;
            }

            if (slot.numberText != null)
            {
                slot.numberText.enabled = true;
                slot.numberText.text = _wagons[i].WagonNumber.ToString();
                slot.numberText.color = Presentation.PuzzleHeading;
            }

            RefreshSlotHighlight(slot, i, _selectedIndex, _dragHoveredIndex);
        }

        LayoutButtons();
    }

    // 미보유 칸은 아무것도 보이지 않는다. 화차 카드를 드래그하는 동안 다음 빈 칸 하나만
    // 그래픽·라벨 없이 외곽선(highlightBorder)만 켜서 드롭 위치 힌트로 쓴다.
    void RefreshUnownedSlot(WagonSlotUI _slot, int _index)
    {
        bool showDropHint = IsDropHintSlot(_index);
        _slot.root.SetActive(showDropHint);
        if (!showDropHint)
        {
            return;
        }

        if (_slot.button != null)
        {
            _slot.button.interactable = false;
        }
        if (_slot.rectTransform != null)
        {
            _slot.rectTransform.localScale = Vector3.one;
        }
        if (_slot.wagonImage != null)
        {
            _slot.wagonImage.enabled = false;
        }
        if (_slot.numberText != null)
        {
            _slot.numberText.enabled = false;
        }
        if (_slot.highlightBorder != null)
        {
            _slot.highlightBorder.enabled = true;
            _slot.highlightBorder.color = Presentation.PuzzleAddWagonBox;
        }
    }

    bool IsDropHintSlot(int _index)
    {
        if (!dropHintVisible || _index != ownedWagonCount)
        {
            return false;
        }
        int maxWagonCount = TableManager.Instance?.PuzzleTrade?.maxWagonCount ?? 0;
        return maxWagonCount <= 0 || ownedWagonCount < maxWagonCount;
    }

    void RefreshSlotHighlight(WagonSlotUI _slot, int _index, int _selectedIndex, int? _dragHoveredIndex)
    {
        if (_slot.highlightBorder == null)
        {
            return;
        }

        bool isSelected = _index == _selectedIndex;
        bool isDragHovered = _dragHoveredIndex == _index;
        _slot.highlightBorder.enabled = isSelected || isDragHovered;
        _slot.highlightBorder.color = isDragHovered
            ? Presentation.PuzzleWagonDragHoverBorder
            : Presentation.PuzzleWagonSelectedBorder;
    }

    void OnRectTransformDimensionsChange()
    {
        // 상위 레이아웃 그룹이 첫 갱신 뒤에 너비를 지정할 수 있다.
        LayoutButtons();
    }

    // E3 규격: 슬롯 8개(각 124×56, 간격 50)를 좌측 고정 간격으로 배치한다.
    // 좌표는 직렬화 값(originX·originY·wagonWidth·wagonSpacing)에서 나오며 프리팹이 원본이다.
    void LayoutButtons()
    {
        if (wagonSlots == null)
        {
            return;
        }

        var rect = (RectTransform)transform;
        if (rect.rect.width <= 0f)
        {
            return;
        }

        float step = wagonWidth + wagonSpacing;
        var size = new Vector2(wagonWidth, wagonHeight);

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slotRect = wagonSlots[i]?.rectTransform;
            if (!slotRect)
            {
                continue;
            }
            slotRect.anchoredPosition = new Vector2(originX + i * step, originY);
            slotRect.sizeDelta = size;
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
