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
    Color ownedSlotBackground = Color.white;
    bool slotBackgroundCaptured;
    bool wagonShopOpen;
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

    void OnEnable()
    {
        // 화차 상점 하위 화면이 열리면 다음 잠긴 칸을 증차 드롭 대상으로 강조한다.
        GameEvents.PanelVisibility += OnPanelVisibility;
    }

    void OnDisable()
    {
        GameEvents.PanelVisibility -= OnPanelVisibility;
        CancelWagonDrag();
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == Presentation.PUZZLE_WAGON_SHOP_PANEL_NAME)
        {
            wagonShopOpen = _info.open;
            if (ownedWagonCount >= 0)
            {
                RefreshHighlightOnly();
            }
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

        // 잠긴 칸은 슬롯이 보여도 히트 대상이 아니다(전환·드롭·드래그 시작 불가).
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

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot == null || slot.root == null)
            {
                continue;
            }

            // 8칸 슬롯은 항상 보인다. 보유 수 이후는 잠김 라벨·어두운 색으로 표시하고 클릭을 막는다.
            slot.root.SetActive(true);
            bool owned = i < ownedWagonCount;
            if (slot.button != null)
            {
                slot.button.interactable = owned;
            }

            if (slot.rectTransform != null)
            {
                slot.rectTransform.localScale = owned && _dragHoveredIndex == i ? Vector3.one * wagonDragHoverScale : Vector3.one;
            }

            if (slot.wagonImage != null)
            {
                slot.wagonImage.enabled = owned;
            }

            if (slot.numberText != null)
            {
                slot.numberText.text = owned ? _wagons[i].WagonNumber.ToString() : Presentation.TrainLockedSlot(i + 1);
                slot.numberText.color = owned ? Presentation.PuzzleHeading : Presentation.PuzzleAddWagonTextDisabled;
            }

            RefreshSlotBackground(slot, owned);
            RefreshSlotHighlight(slot, i, owned, _selectedIndex, _dragHoveredIndex);
        }

        LayoutButtons();
    }

    // 화차 상점 열림 전환은 슬롯 표시를 다시 만들지 않고 강조만 다시 계산한다.
    void RefreshHighlightOnly()
    {
        if (wagonSlots == null)
        {
            return;
        }
        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot == null || slot.root == null)
            {
                continue;
            }
            RefreshSlotHighlight(slot, i, i < ownedWagonCount, -1, null);
        }
    }

    void RefreshSlotBackground(WagonSlotUI _slot, bool _owned)
    {
        var background = _slot.root ? _slot.root.GetComponent<Image>() : null;
        if (!background)
        {
            return;
        }
        if (!slotBackgroundCaptured)
        {
            // 보유 칸 배경은 프리팹 직렬화 색을 유지한다. 잠깐 칸은 기초데이터 배경색으로 어둡게 한다.
            ownedSlotBackground = background.color;
            slotBackgroundCaptured = true;
        }
        background.color = _owned ? ownedSlotBackground : Presentation.PuzzleBackground;
    }

    void RefreshSlotHighlight(WagonSlotUI _slot, int _index, bool _owned, int _selectedIndex, int? _dragHoveredIndex)
    {
        if (_slot.highlightBorder == null)
        {
            return;
        }
        if (!_owned)
        {
            // 화차 상점이 열려 있으면 다음 잠긴 칸 하나를 증차 대상으로 강조한다.
            bool isNextPurchaseTarget = wagonShopOpen && _index == ownedWagonCount;
            _slot.highlightBorder.enabled = isNextPurchaseTarget;
            _slot.highlightBorder.color = Presentation.PuzzleAddWagonBox;
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
