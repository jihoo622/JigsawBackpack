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
    int page;

    public FloatingWindow FloatingWindow => floatingWindow;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    public event Action DepartureRequested;

    Camera EventCamera => rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

    void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c ? c.rootCanvas : null;
    }

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        inventoryPresenter = SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        railStripView = SceneServices.Find<RailStripView>(gameObject.scene);

        if (nextPageButton) nextPageButton.onClick.AddListener(OnNextPage);
        if (departButton) departButton.onClick.AddListener(OnDepart);

        if (productRows != null)
        {
            for (int i = 0; i < productRows.Length; i++)
            {
                if (productRows[i])
                {
                    productRows[i].Clicked += OnProductClicked;
                    productRows[i].DragStarted += OnProductDragStarted;
                    productRows[i].Dragged += OnProductDragged;
                    productRows[i].DragEnded += OnProductDragEnded;
                }
            }
        }

        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.PuzzleActionCommitted += OnPuzzleActionCommitted;
        Refresh();
    }

    void OnDisable()
    {
        if (nextPageButton) nextPageButton.onClick.RemoveListener(OnNextPage);
        if (departButton) departButton.onClick.RemoveListener(OnDepart);

        if (productRows != null)
        {
            for (int i = 0; i < productRows.Length; i++)
            {
                if (productRows[i])
                {
                    productRows[i].Clicked -= OnProductClicked;
                    productRows[i].DragStarted -= OnProductDragStarted;
                    productRows[i].Dragged -= OnProductDragged;
                    productRows[i].DragEnded -= OnProductDragEnded;
                }
            }
        }

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
            page = 0;
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

        currentProducts = session.WagonProductsHere ?? Array.Empty<PuzzleWagonProductData>();
        int perPage = productRows != null && productRows.Length > 0 ? productRows.Length : 1;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)currentProducts.Count / perPage));
        page = Mathf.Clamp(page, 0, totalPages - 1);

        if (productRows != null)
        {
            int start = page * perPage;
            for (int i = 0; i < productRows.Length; i++)
            {
                if (!productRows[i]) continue;
                int idx = start + i;
                var prod = idx < currentProducts.Count ? currentProducts[idx] : null;
                productRows[i].Bind(prod);
                productRows[i].SetSelected(false);
            }
        }
        // 상품이 한 쪽뿐이면 넘김 줄은 쓸모가 없으므로 숨긴다.
        bool paged = totalPages > 1;
        if (pageLabel)
        {
            pageLabel.text = $"{page + 1} / {totalPages}";
            pageLabel.gameObject.SetActive(paged);
        }
        if (nextPageButton)
        {
            nextPageButton.interactable = paged;
            nextPageButton.gameObject.SetActive(paged);
        }
    }

    void OnNextPage()
    {
        int perPage = productRows != null && productRows.Length > 0 ? productRows.Length : 1;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)currentProducts.Count / perPage));
        page = (page + 1) % totalPages;
        Refresh();
    }

    void OnProductClicked(PuzzleWagonProductRow _row)
    {
        if (productRows == null) return;
        for (int i = 0; i < productRows.Length; i++)
        {
            if (productRows[i])
            {
                productRows[i].SetSelected(productRows[i] == _row);
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
