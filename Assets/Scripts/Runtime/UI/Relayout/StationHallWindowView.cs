using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// S6: 역사 부동 창. 화차 상품(드래그로 선로 삽입 구매) + 판매 드롭 구역(선로 화차 드래그로 판매) + 출발(지도 탭).
[DisallowMultipleComponent]
public sealed class StationHallWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] PuzzleWagonProductRow[] productRows;
    [SerializeField] ScrollRect productScrollRect;
    [SerializeField] RectTransform productListContent;
    [SerializeField] Button nextPageButton;
    [SerializeField] TextMeshProUGUI pageLabel;
    [SerializeField] RectTransform sellDropZone;
    [SerializeField] TextMeshProUGUI sellZoneLabel;
    [SerializeField] Button departButton;
    [SerializeField] TextMeshProUGUI departButtonLabel;
    [SerializeField] TextMeshProUGUI statusText;

    PuzzleInventoryService session;
    PuzzleInventoryPresenter inventoryPresenter;
    RailStripView railStripView;
    Canvas rootCanvas;
    IReadOnlyList<PuzzleWagonProductData> currentProducts = Array.Empty<PuzzleWagonProductData>();
    readonly List<PuzzleWagonProductRow> spawnedRows = new();
    PuzzleWagonProductRow rowTemplate;
    bool layoutInitialized;
    bool rowEventsBound;

    public FloatingWindow FloatingWindow => floatingWindow;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    public event Action DepartureRequested;

    Camera EventCamera => rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

    void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c ? c.rootCanvas : null;
        EnsureProductListLayout();
    }

    void OnEnable()
    {
        EnsureProductListLayout();
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        inventoryPresenter = SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        railStripView = SceneServices.Find<RailStripView>(gameObject.scene);

        if (departButton) departButton.onClick.AddListener(OnDepart);
        SetRowEventsBound(true);

        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.PuzzleActionCommitted += OnPuzzleActionCommitted;
        Refresh();
    }

    void OnDisable()
    {
        if (departButton) departButton.onClick.RemoveListener(OnDepart);
        SetRowEventsBound(false);

        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.PuzzleActionCommitted -= OnPuzzleActionCommitted;
        if (railStripView)
        {
            railStripView.HideInsertionLine();
        }
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (_open)
        {
            session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        }
        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
        if (_open)
        {
            Refresh();
            if (productScrollRect)
            {
                productScrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }

    public bool ContainsScreenPoint(Vector2 _screenPos)
    {
        if (!IsOpen)
        {
            return false;
        }
        var target = sellDropZone ? sellDropZone : transform as RectTransform;
        return target && RectTransformUtility.RectangleContainsScreenPoint(target, _screenPos, EventCamera);
    }

    public void Refresh()
    {
        session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session == null)
        {
            return;
        }
        if (titleText)
        {
            titleText.text = Presentation.StationHallWindowTitle(session.CityData?.displayName ?? string.Empty);
        }
        if (sellZoneLabel)
        {
            sellZoneLabel.text = Presentation.StationHallSaleDrop;
        }
        if (departButtonLabel)
        {
            departButtonLabel.text = Presentation.StationHallDepartButton;
        }
        if (statusText)
        {
            statusText.text = Presentation.StationHallSaleDrop;
        }

        EnsureProductListLayout();

        currentProducts = session.WagonProductsHere ?? Array.Empty<PuzzleWagonProductData>();
        EnsureRowCount(currentProducts.Count);

        for (int i = 0; i < spawnedRows.Count; i++)
        {
            var row = spawnedRows[i];
            if (!row) continue;
            var prod = i < currentProducts.Count ? currentProducts[i] : null;
            row.Bind(prod);
            row.SetSelected(false);
        }

        if (productListContent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(productListContent);
        }
        if (pageLabel)
        {
            pageLabel.gameObject.SetActive(false);
        }
        if (nextPageButton)
        {
            nextPageButton.gameObject.SetActive(false);
        }
    }

    void EnsureProductListLayout()
    {
        if (layoutInitialized)
        {
            return;
        }
        layoutInitialized = true;

        if (pageLabel)
        {
            pageLabel.gameObject.SetActive(false);
        }
        if (nextPageButton)
        {
            nextPageButton.gameObject.SetActive(false);
        }

        if (productRows != null)
        {
            for (int i = 0; i < productRows.Length; i++)
            {
                if (!productRows[i]) continue;
                if (!rowTemplate)
                {
                    rowTemplate = productRows[i];
                }
                if (!spawnedRows.Contains(productRows[i]))
                {
                    spawnedRows.Add(productRows[i]);
                }
            }
        }

        if (productListContent != null && productScrollRect != null)
        {
            return;
        }

        var rootRect = transform as RectTransform;
        if (!rootRect)
        {
            return;
        }

        var windowSize = new Vector2(572f, 576f);
        if (floatingWindow)
        {
            floatingWindow.SetDefaultSize(windowSize);
        }
        else
        {
            rootRect.sizeDelta = windowSize;
        }

        if (sellDropZone)
        {
            sellDropZone.anchorMin = Vector2.zero;
            sellDropZone.anchorMax = Vector2.zero;
            sellDropZone.pivot = Vector2.zero;
            sellDropZone.anchoredPosition = new Vector2(16f, 16f);
            sellDropZone.sizeDelta = new Vector2(372f, 72f);
        }
        if (departButton && departButton.transform is RectTransform departRt)
        {
            departRt.anchorMin = new Vector2(1f, 0f);
            departRt.anchorMax = new Vector2(1f, 0f);
            departRt.pivot = new Vector2(1f, 0f);
            departRt.anchoredPosition = new Vector2(-16f, 16f);
            departRt.sizeDelta = new Vector2(152f, 72f);
        }

        var scrollGo = new GameObject("ProductListScroll", typeof(RectTransform));
        scrollGo.layer = gameObject.layer;
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.SetParent(rootRect, false);
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(16f, 96f);
        scrollRt.offsetMax = new Vector2(-16f, -38f);

        productScrollRect = scrollGo.AddComponent<ScrollRect>();
        productScrollRect.horizontal = false;
        productScrollRect.vertical = true;
        productScrollRect.movementType = ScrollRect.MovementType.Clamped;
        productScrollRect.scrollSensitivity = 36f;

        var barGo = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.layer = gameObject.layer;
        var barRt = (RectTransform)barGo.transform;
        barRt.SetParent(scrollRt, false);
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.anchoredPosition = Vector2.zero;
        barRt.sizeDelta = new Vector2(8f, 0f);
        barGo.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.28f, 1f);

        var slidingGo = new GameObject("SlidingArea", typeof(RectTransform));
        slidingGo.layer = gameObject.layer;
        var slidingRt = (RectTransform)slidingGo.transform;
        slidingRt.SetParent(barRt, false);
        slidingRt.anchorMin = Vector2.zero;
        slidingRt.anchorMax = Vector2.one;
        slidingRt.offsetMin = Vector2.zero;
        slidingRt.offsetMax = Vector2.zero;

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.layer = gameObject.layer;
        var handleRt = (RectTransform)handleGo.transform;
        handleRt.SetParent(slidingRt, false);
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.offsetMin = Vector2.zero;
        handleRt.offsetMax = Vector2.zero;
        var handleImg = handleGo.GetComponent<Image>();
        handleImg.color = new Color(0.84f, 0.71f, 0.42f, 1f);

        var scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImg;
        productScrollRect.verticalScrollbar = scrollbar;
        productScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        vpGo.layer = gameObject.layer;
        var vpRt = (RectTransform)vpGo.transform;
        vpRt.SetParent(scrollRt, false);
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = new Vector2(-12f, 0f);
        vpGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.layer = gameObject.layer;
        productListContent = (RectTransform)contentGo.transform;
        productListContent.SetParent(vpRt, false);
        productListContent.anchorMin = new Vector2(0f, 1f);
        productListContent.anchorMax = new Vector2(1f, 1f);
        productListContent.pivot = new Vector2(0.5f, 1f);
        productListContent.anchoredPosition = Vector2.zero;
        productListContent.sizeDelta = Vector2.zero;

        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        productScrollRect.viewport = vpRt;
        productScrollRect.content = productListContent;

        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] && spawnedRows[i].transform is RectTransform rowRt)
            {
                rowRt.SetParent(productListContent, false);
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.sizeDelta = new Vector2(528f, 120f);
            }
        }

        var dragPreview = rootRect.Find("DragPreview");
        if (dragPreview)
        {
            dragPreview.SetAsLastSibling();
        }
    }

    void EnsureRowCount(int _count)
    {
        if (!rowTemplate || !productListContent)
        {
            return;
        }
        while (spawnedRows.Count < _count)
        {
            var cloned = Instantiate(rowTemplate, productListContent, false);
            cloned.name = $"WagonProductRow_{spawnedRows.Count + 1}";
            if (cloned.transform is RectTransform rt)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(528f, 120f);
            }
            spawnedRows.Add(cloned);
            if (rowEventsBound)
            {
                BindRowEvents(cloned, true);
            }
        }
    }

    void SetRowEventsBound(bool _bind)
    {
        if (rowEventsBound == _bind)
        {
            return;
        }
        rowEventsBound = _bind;
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i])
            {
                BindRowEvents(spawnedRows[i], _bind);
            }
        }
    }

    void BindRowEvents(PuzzleWagonProductRow _row, bool _bind)
    {
        if (_bind)
        {
            _row.Clicked += OnProductClicked;
            _row.DragStarted += OnProductDragStarted;
            _row.Dragged += OnProductDragged;
            _row.DragEnded += OnProductDragEnded;
        }
        else
        {
            _row.Clicked -= OnProductClicked;
            _row.DragStarted -= OnProductDragStarted;
            _row.Dragged -= OnProductDragged;
            _row.DragEnded -= OnProductDragEnded;
        }
    }

    void OnProductClicked(PuzzleWagonProductRow _row)
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i])
            {
                spawnedRows[i].SetSelected(spawnedRows[i] == _row);
            }
        }
    }

    void OnProductDragStarted(PuzzleWagonProductRow _row)
    {
        OnProductClicked(_row);
    }

    void OnProductDragged(PuzzleWagonProductRow _row, PointerEventData _eventData)
    {
        if (!railStripView || _eventData == null)
        {
            return;
        }
        if (railStripView.TryGetInsertionIndexAtScreenPosition(_eventData.position, out int ins))
        {
            railStripView.ShowInsertionLine(ins);
        }
        else
        {
            railStripView.HideInsertionLine();
        }
    }

    void OnProductDragEnded(PuzzleWagonProductRow _row, PointerEventData _eventData)
    {
        if (railStripView)
        {
            railStripView.HideInsertionLine();
        }
        if (_row == null || _row.Product == null || _eventData == null)
        {
            return;
        }
        inventoryPresenter ??= SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        if (inventoryPresenter && inventoryPresenter.TryDropPurchasedWagonAtScreenPosition(_row.Product, _eventData.position))
        {
            Refresh();
        }
    }

    void OnDepart()
    {
        DepartureRequested?.Invoke();
    }

    void OnFundsChanged(GameEvents.FundsInfo _info) => Refresh();
    void OnCityArrived(GameEvents.CityArrivalInfo _info) => Refresh();
    void OnPuzzleActionCommitted(GameEvents.PuzzleActionInfo _info) => Refresh();
}
