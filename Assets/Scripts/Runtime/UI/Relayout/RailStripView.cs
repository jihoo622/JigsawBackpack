using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// S3: 중앙 세로 선로(549, 82, 91, 998). 가상화 풀(R16)로 무제한 화차 수를 지원한다.
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
    [SerializeField] RailWagonCarView[] carPool;
    [SerializeField] float carHeight = 96f;
    [SerializeField] float carSpacing = 8f;
    [SerializeField] float topPadding = 8f;
    [SerializeField] float bottomPadding = 8f;

    IReadOnlyList<PuzzleInventory> wagons = Array.Empty<PuzzleInventory>();
    int selectedIndex;
    int? hoveredIndex;
    int lastSelectedForScroll = -1;
    Canvas rootCanvas;

    public event Action<int> WagonSelected;

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
                }
            }
        }
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
                }
            }
        }
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
        UpdateVirtualPool();
        UpdateScrollIndicators();
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

    void UpdateVirtualPool()
    {
        if (carPool == null || carPool.Length == 0 || !content)
        {
            return;
        }

        float scrollY = Mathf.Max(0f, content.anchoredPosition.y);
        int firstVisible = Mathf.Max(0, Mathf.FloorToInt((scrollY - topPadding) / CarStride));
        int maxStart = Mathf.Max(0, wagons.Count - carPool.Length);
        int start = Mathf.Min(firstVisible, maxStart);

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
        UpdateVirtualPool();
        UpdateScrollIndicators();
    }

    void ScrollUp()
    {
        if (!content || !viewport) return;
        float next = Mathf.Max(0f, content.anchoredPosition.y - CarStride);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, next);
        UpdateVirtualPool();
        UpdateScrollIndicators();
    }

    void ScrollDown()
    {
        if (!content || !viewport) return;
        float maxScroll = Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);
        float next = Mathf.Min(maxScroll, content.anchoredPosition.y + CarStride);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, next);
        UpdateVirtualPool();
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
