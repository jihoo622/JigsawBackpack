using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// S3/S3b: 중앙 세로 선로(549, 82, 91, 998). 가상화 풀(R16), 드래그 순서 변경(H20), 판매·구매 드롭 타깃(H17/R24).
[DisallowMultipleComponent]
public sealed class RailStripView : MonoBehaviour
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform content;
    [SerializeField] Button scrollUpButton;
    [SerializeField] Button scrollDownButton;
    [SerializeField] Image topFade;
    [SerializeField] Image bottomFade;
    [SerializeField] Image insertionLine;
    [SerializeField] RailWagonCarView[] carPool;
    [SerializeField] float carHeight = 96f;
    [SerializeField] float carSpacing = 8f;
    [SerializeField] float topPadding = 8f;
    [SerializeField] float bottomPadding = 8f;
    [SerializeField] float dragAutoScrollZone = 40f;
    [SerializeField] float dragAutoScrollSpeed = 320f;

    IReadOnlyList<PuzzleInventory> wagons = Array.Empty<PuzzleInventory>();
    int selectedIndex;
    int? hoveredIndex;
    int lastSelectedForScroll = -1;
    Canvas rootCanvas;
    int? draggingWagonIndex;
    PuzzleInventory draggingWagon;
    Vector2 draggingScreenPos;
    int lastPoolStart = -1;

    public event Action<int> WagonSelected;
    public event Action<int, int> WagonReordered;
    public event Action<int, Vector2> WagonDraggedOutside;
    public event Action<int, Vector2> WagonDragEndedOutside;

    float CarStride => carHeight + carSpacing;
    Camera EventCamera => rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

    void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c ? c.rootCanvas : null;
    }

    void OnEnable()
    {
        if (scrollRect)
        {
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
        }
        if (scrollUpButton)
        {
            scrollUpButton.onClick.AddListener(ScrollUp);
        }
        if (scrollDownButton)
        {
            scrollDownButton.onClick.AddListener(ScrollDown);
        }
        if (carPool != null)
        {
            for (int i = 0; i < carPool.Length; i++)
            {
                if (carPool[i])
                {
                    carPool[i].Selected += OnCarSelected;
                    carPool[i].DragStarted += OnCarDragStarted;
                    carPool[i].Dragged += OnCarDragged;
                    carPool[i].DragEnded += OnCarDragEnded;
                }
            }
        }
        HideInsertionLine();
        GameEvents.PuzzleActionCommitted += OnPuzzleActionCommitted;
        GameEvents.TradeCompleted += OnTradeCompleted;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
    }

    void OnDisable()
    {
        if (scrollRect)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
        }
        if (scrollUpButton)
        {
            scrollUpButton.onClick.RemoveListener(ScrollUp);
        }
        if (scrollDownButton)
        {
            scrollDownButton.onClick.RemoveListener(ScrollDown);
        }
        if (carPool != null)
        {
            for (int i = 0; i < carPool.Length; i++)
            {
                if (carPool[i])
                {
                    carPool[i].Selected -= OnCarSelected;
                    carPool[i].DragStarted -= OnCarDragStarted;
                    carPool[i].Dragged -= OnCarDragged;
                    carPool[i].DragEnded -= OnCarDragEnded;
                }
            }
        }
        draggingWagonIndex = null;
        draggingWagon = null;
        lastPoolStart = -1;
        HideInsertionLine();
        GameEvents.PuzzleActionCommitted -= OnPuzzleActionCommitted;
        GameEvents.TradeCompleted -= OnTradeCompleted;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
    }

    public void Refresh(IReadOnlyList<PuzzleInventory> _wagons, int _selectedIndex, int? _hoveredIndex = null)
    {
        wagons = _wagons ?? Array.Empty<PuzzleInventory>();
        selectedIndex = _selectedIndex;
        hoveredIndex = _hoveredIndex;

        UpdateContentHeight();
        ClampScrollPosition();
        if (selectedIndex != lastSelectedForScroll)
        {
            if (EnsureIndexVisible(selectedIndex))
            {
                lastSelectedForScroll = selectedIndex;
            }
        }
        UpdateVirtualPool(true);
        UpdateScrollIndicators();
    }

    void Update()
    {
        if (!draggingWagonIndex.HasValue || !viewport || !content)
        {
            return;
        }
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, draggingScreenPos, EventCamera))
        {
            return;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, draggingScreenPos, EventCamera, out var local))
        {
            return;
        }

        Rect vpRect = viewport.rect;
        float distTop = vpRect.yMax - local.y;
        float distBottom = local.y - vpRect.yMin;
        float delta = 0f;
        if (distTop >= 0f && distTop < dragAutoScrollZone)
        {
            delta = -dragAutoScrollSpeed * Time.unscaledDeltaTime;
        }
        else if (distBottom >= 0f && distBottom < dragAutoScrollZone)
        {
            delta = dragAutoScrollSpeed * Time.unscaledDeltaTime;
        }

        if (!Mathf.Approximately(delta, 0f))
        {
            float maxScroll = Mathf.Max(0f, content.sizeDelta.y - vpRect.height);
            float nextY = Mathf.Clamp(content.anchoredPosition.y + delta, 0f, maxScroll);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, nextY);
            UpdateVirtualPool();
            UpdateScrollIndicators();
            if (TryGetInsertionIndexAtScreenPosition(draggingScreenPos, out int ins))
            {
                ShowInsertionLine(ins);
            }
        }
    }

    public bool TryGetInsertionIndexAtScreenPosition(Vector2 _screenPosition, out int _insertionIndex)
    {
        _insertionIndex = 0;
        if (!viewport || !content ||
            !RectTransformUtility.RectangleContainsScreenPoint(viewport, _screenPosition, EventCamera))
        {
            return false;
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, _screenPosition, EventCamera, out var local))
        {
            return false;
        }

        // content는 pivot(0, 1)이므로 아래로 내려갈수록 local.y가 음수다.
        float distFromTop = Mathf.Max(0f, -local.y);
        int count = wagons != null ? wagons.Count : 0;
        int idx = 0;
        for (int i = 0; i < count; i++)
        {
            float mid = topPadding + i * CarStride + carHeight * 0.5f;
            if (distFromTop > mid)
            {
                idx = i + 1;
            }
        }
        _insertionIndex = Mathf.Clamp(idx, 0, count);
        return true;
    }

    public void ShowInsertionLine(int _insertionIndex)
    {
        if (!insertionLine || !content)
        {
            return;
        }
        int count = wagons != null ? wagons.Count : 0;
        int clamped = Mathf.Clamp(_insertionIndex, 0, count);
        float y = -(topPadding + clamped * CarStride - carSpacing * 0.5f);
        if (clamped == 0)
        {
            y = -Mathf.Max(2f, topPadding * 0.5f);
        }
        var rt = insertionLine.rectTransform;
        rt.anchoredPosition = new Vector2(0f, y);
        insertionLine.transform.SetAsLastSibling();
        if (!insertionLine.gameObject.activeSelf)
        {
            insertionLine.gameObject.SetActive(true);
        }
    }

    public void HideInsertionLine()
    {
        if (insertionLine && insertionLine.gameObject.activeSelf)
        {
            insertionLine.gameObject.SetActive(false);
        }
    }

    void OnCarDragStarted(int _wagonIndex, PointerEventData _eventData)
    {
        draggingWagonIndex = _wagonIndex;
        draggingWagon = (wagons != null && _wagonIndex >= 0 && _wagonIndex < wagons.Count)
            ? wagons[_wagonIndex]
            : null;
        draggingScreenPos = _eventData.position;
        if (TryGetInsertionIndexAtScreenPosition(draggingScreenPos, out int ins))
        {
            ShowInsertionLine(ins);
        }
    }

    int ResolveDraggingWagonIndex(int _fallbackIndex)
    {
        if (draggingWagon != null && wagons != null)
        {
            for (int i = 0; i < wagons.Count; i++)
            {
                if (ReferenceEquals(wagons[i], draggingWagon))
                {
                    return i;
                }
            }
            return -1;
        }
        return draggingWagonIndex ?? _fallbackIndex;
    }

    void OnCarDragged(int _wagonIndex, PointerEventData _eventData)
    {
        int sourceIndex = ResolveDraggingWagonIndex(_wagonIndex);
        if (sourceIndex < 0)
        {
            return;
        }
        draggingWagonIndex = sourceIndex;
        draggingScreenPos = _eventData.position;
        if (TryGetInsertionIndexAtScreenPosition(draggingScreenPos, out int ins))
        {
            ShowInsertionLine(ins);
        }
        else
        {
            HideInsertionLine();
        }
        WagonDraggedOutside?.Invoke(sourceIndex, draggingScreenPos);
    }

    void OnCarDragEnded(int _wagonIndex, PointerEventData _eventData)
    {
        int sourceIndex = ResolveDraggingWagonIndex(_wagonIndex);
        draggingWagonIndex = null;
        draggingWagon = null;
        HideInsertionLine();
        if (sourceIndex < 0 || wagons == null || sourceIndex >= wagons.Count)
        {
            WagonDraggedOutside?.Invoke(-1, _eventData.position);
            return;
        }
        if (TryGetInsertionIndexAtScreenPosition(_eventData.position, out int ins))
        {
            int count = wagons.Count;
            int target = ins > sourceIndex ? ins - 1 : ins;
            target = Mathf.Clamp(target, 0, Mathf.Max(0, count - 1));
            if (target != sourceIndex)
            {
                WagonReordered?.Invoke(sourceIndex, target);
            }
            WagonDraggedOutside?.Invoke(-1, _eventData.position);
            return;
        }
        WagonDragEndedOutside?.Invoke(sourceIndex, _eventData.position);
    }

    public bool TryGetWagonAtScreenPosition(Vector2 _screenPosition, out int _wagonIndex)
    {
        if (viewport && !RectTransformUtility.RectangleContainsScreenPoint(viewport, _screenPosition, EventCamera))
        {
            _wagonIndex = -1;
            return false;
        }
        if (carPool != null)
        {
            for (int i = 0; i < carPool.Length; i++)
            {
                var car = carPool[i];
                if (car && car.gameObject.activeInHierarchy && car.BoundWagonIndex >= 0 &&
                    RectTransformUtility.RectangleContainsScreenPoint(car.RectTransform, _screenPosition, EventCamera))
                {
                    _wagonIndex = car.BoundWagonIndex;
                    return true;
                }
            }
        }
        _wagonIndex = -1;
        return false;
    }

    public bool EnsureIndexVisible(int _index)
    {
        if (!viewport || !content || _index < 0 || _index >= wagons.Count)
        {
            return false;
        }
        float vh = viewport.rect.height;
        if (vh <= 0.5f)
        {
            return false;
        }
        float ch = content.sizeDelta.y;
        if (ch <= vh + 0.5f)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
            return true;
        }

        float itemTop = topPadding + _index * CarStride;
        float itemBottom = itemTop + carHeight;
        float scrollY = content.anchoredPosition.y;
        float maxScroll = Mathf.Max(0f, ch - vh);

        if (itemTop < scrollY)
        {
            scrollY = itemTop - topPadding;
        }
        else if (itemBottom > scrollY + vh)
        {
            scrollY = itemBottom - vh + bottomPadding;
        }
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(scrollY, 0f, maxScroll));
        return true;
    }

    void ClampScrollPosition()
    {
        if (!viewport || !content)
        {
            return;
        }
        float maxScroll = Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);
        float clamped = Mathf.Clamp(content.anchoredPosition.y, 0f, maxScroll);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, clamped);
    }

    void UpdateContentHeight()
    {
        if (!content || !viewport)
        {
            return;
        }
        float total = wagons.Count > 0
            ? topPadding + wagons.Count * CarStride - carSpacing + bottomPadding
            : viewport.rect.height;
        float height = Mathf.Max(viewport.rect.height, total);
        content.sizeDelta = new Vector2(content.sizeDelta.x, height);
    }

    void UpdateVirtualPool(bool _force = false)
    {
        if (carPool == null || carPool.Length == 0 || !content)
        {
            return;
        }

        float scrollY = Mathf.Max(0f, content.anchoredPosition.y);
        int firstVisible = Mathf.Max(0, Mathf.FloorToInt((scrollY - topPadding) / CarStride));
        int maxStart = Mathf.Max(0, wagons.Count - carPool.Length);
        int start = Mathf.Min(firstVisible, maxStart);
        // 자동 스크롤/휠 스크롤 중에는 첫 표시 인덱스가 바뀔 때만 슬롯을 재바인딩한다.
        if (!_force && start == lastPoolStart)
        {
            return;
        }
        lastPoolStart = start;

        for (int i = 0; i < carPool.Length; i++)
        {
            var car = carPool[i];
            if (!car)
            {
                continue;
            }
            int wagonIdx = start + i;
            if (wagonIdx < wagons.Count)
            {
                var rt = car.RectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(topPadding + wagonIdx * CarStride));
                car.Bind(wagons[wagonIdx], wagonIdx, wagonIdx == selectedIndex, hoveredIndex == wagonIdx);
            }
            else
            {
                car.Unbind();
            }
        }
    }

    void UpdateScrollIndicators()
    {
        if (!viewport || !content)
        {
            return;
        }
        float vh = viewport.rect.height;
        float ch = content.sizeDelta.y;
        float scrollY = content.anchoredPosition.y;
        bool canScrollUp = scrollY > 2f;
        bool canScrollDown = ch > vh + 2f && scrollY < ch - vh - 2f;

        if (topFade) topFade.enabled = canScrollUp;
        if (bottomFade) bottomFade.enabled = canScrollDown;
        if (scrollUpButton) scrollUpButton.interactable = canScrollUp;
        if (scrollDownButton) scrollDownButton.interactable = canScrollDown;
    }

    void OnScrollChanged(Vector2 _pos)
    {
        UpdateVirtualPool(true);
        UpdateScrollIndicators();
    }

    void ScrollUp()
    {
        if (!content || !viewport) return;
        float next = Mathf.Max(0f, content.anchoredPosition.y - CarStride);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, next);
        UpdateVirtualPool(true);
        UpdateScrollIndicators();
    }

    void ScrollDown()
    {
        if (!content || !viewport) return;
        float maxScroll = Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);
        float next = Mathf.Min(maxScroll, content.anchoredPosition.y + CarStride);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, next);
        UpdateVirtualPool(true);
        UpdateScrollIndicators();
    }

    void OnCarSelected(int _index)
    {
        WagonSelected?.Invoke(_index);
    }

    void RefreshFromService()
    {
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session != null)
        {
            Refresh(session.TrainWagons, session.CurrentWagonIndex, hoveredIndex);
        }
    }

    void OnPuzzleActionCommitted(GameEvents.PuzzleActionInfo _info) => RefreshFromService();
    void OnTradeCompleted(GameEvents.TradeInfo _info) => RefreshFromService();
    void OnCityArrived(GameEvents.CityArrivalInfo _info) => RefreshFromService();
    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info) => RefreshFromService();
}
