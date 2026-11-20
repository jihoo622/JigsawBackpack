using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// S3/S3b: 중앙 선로의 가상화 화차 슬롯(R8 7×5 미니 격자 포함, H20 순서 드래그 + H17 판매 드래그).
// 목업 concept-3b: 미니 격자 아래에 '번호 · 적재 · 가치' 한 줄 라벨, 선택 차량은 금색 테두리(상하좌우 4개 평면 사각형).
[DisallowMultipleComponent]
public sealed class RailWagonCarView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    public const int MINI_COLS = 7;
    // 화차 보드는 7×5(PuzzleTableData train)라서 목업과 같이 5행만 그린다.
    public const int MINI_ROWS = 5;
    public const int MINI_CELL_COUNT = MINI_COLS * MINI_ROWS;

    [SerializeField] RectTransform rectTransform;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Image highlightBorder;
    // 테두리 4변(위·오른쪽·아래·왼쪽) 순서. 선택·호버 때만 켠다. 외곽선(Outline)은 칸 틴트 알파에 곱해져 흐려지므로 쓰지 않는다.
    [SerializeField] Image[] borderEdges;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image[] miniGridCells;
    [SerializeField] UIPalette palette;

    readonly Dictionary<int, int> placementToneById = new();
    readonly List<int> neighborTones = new();
    bool isDragging;
    int dragCapturedWagonIndex = -1;
    bool isHovered;
    Vector2 lastHoverScreenPos;

    public event Action<int> Selected;
    public event Action<int, PointerEventData> DragStarted;
    public event Action<int, PointerEventData> Dragged;
    public event Action<int, PointerEventData> DragEnded;
    public event Action<int, Vector2> Hovered;
    public event Action HoverEnded;

    public int BoundWagonIndex { get; private set; } = -1;
    public RectTransform RectTransform => rectTransform ? rectTransform : (RectTransform)transform;

    void OnEnable()
    {
        if (button)
        {
            button.onClick.AddListener(OnClicked);
        }
    }

    void OnDisable()
    {
        ClearHover();
        isDragging = false;
        dragCapturedWagonIndex = -1;
        if (button)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }

    public void Bind(PuzzleInventory _wagon, int _index, bool _selected, bool _hovered)
    {
        bool indexChanged = BoundWagonIndex != _index;
        BoundWagonIndex = _index;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        var pal = palette ? palette : (palette = Resources.Load<UIPalette>("UI/UIPalette"));

        if (label && _wagon != null)
        {
            decimal rawVal = _wagon.Placements.Sum(_p => _p.FreshnessAdjustedValue);
            long value = Decimal.ToInt64(Decimal.Round(rawVal, 0, MidpointRounding.AwayFromZero));
            // 기존 문자열은 '번호 · 적재' 줄과 '가치' 줄로 나뉘므로, 목업처럼 한 줄(· 구분)로 이어 붙인다.
            label.text = Presentation.RailWagonLabel(_wagon.WagonNumber, _wagon.OccupiedCells, _wagon.UsableCellCount, value)
                .Replace("\n", " · ");
        }

        if (highlightBorder)
        {
            highlightBorder.enabled = _selected || _hovered;
            if (pal)
            {
                // 칸 전체 틴트(채움)는 반투명, 테두리는 아래 borderEdges가 불투명 색으로 그린다.
                var c = _selected ? pal.GoldAccent : pal.BorderBright;
                c.a = 0.16f;
                highlightBorder.color = c;
            }
        }
        SetBorderEdges(_selected || _hovered, _selected, pal);
        if (background && pal)
        {
            background.color = _selected ? pal.Panel2 : pal.Panel;
        }

        RenderMiniGrid(_wagon, pal);
        if (isHovered && indexChanged && BoundWagonIndex >= 0 && !isDragging && !PuzzleWagonProductRow.IsDraggingProduct &&
            !PuzzleInventoryPresenter.HasActiveDrag)
        {
            Hovered?.Invoke(BoundWagonIndex, lastHoverScreenPos);
        }
    }

    // 선택은 금색, 호버는 밝은 테두리 색으로 4변을 켠다.
    void SetBorderEdges(bool _visible, bool _selected, UIPalette _pal)
    {
        if (borderEdges == null || !_pal)
        {
            return;
        }
        var edgeColor = _selected ? _pal.GoldAccent : _pal.BorderBright;
        edgeColor.a = 1f;
        for (int i = 0; i < borderEdges.Length; i++)
        {
            var edge = borderEdges[i];
            if (!edge)
            {
                continue;
            }
            edge.color = edgeColor;
            edge.enabled = _visible;
        }
    }

    public void Unbind()
    {
        ClearHover();
        BoundWagonIndex = -1;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData _eventData) => UpdateHover(_eventData);

    public void OnPointerMove(PointerEventData _eventData) => UpdateHover(_eventData);

    public void OnPointerExit(PointerEventData _eventData) => ClearHover();

    void UpdateHover(PointerEventData _eventData)
    {
        if (BoundWagonIndex < 0 || isDragging || _eventData == null || _eventData.dragging ||
            PuzzleWagonProductRow.IsDraggingProduct || PuzzleInventoryPresenter.HasActiveDrag)
        {
            ClearHover();
            return;
        }
        isHovered = true;
        lastHoverScreenPos = _eventData.position;
        Hovered?.Invoke(BoundWagonIndex, lastHoverScreenPos);
    }

    void ClearHover()
    {
        if (!isHovered)
        {
            return;
        }
        isHovered = false;
        HoverEnded?.Invoke();
    }

    void RenderMiniGrid(PuzzleInventory _wagon, UIPalette _pal)
    {
        if (miniGridCells == null || miniGridCells.Length < MINI_CELL_COUNT || _wagon == null || !_pal)
        {
            return;
        }

        AssignCargoTones(_wagon);

        int w = Mathf.Min(_wagon.Width, MINI_COLS);
        int h = Mathf.Min(_wagon.Height, MINI_ROWS);

        for (int row = 0; row < MINI_ROWS; row++)
        {
            for (int col = 0; col < MINI_COLS; col++)
            {
                int idx = row * MINI_COLS + col;
                var img = miniGridCells[idx];
                if (!img)
                {
                    continue;
                }

                if (col >= w || row >= h)
                {
                    img.enabled = false;
                    continue;
                }

                img.enabled = true;
                var cell = new Vector2Int(col, row);
                if (_wagon.BlockedCells.Contains(cell))
                {
                    img.color = _pal.MiniGridBlocked;
                }
                else
                {
                    if (!_wagon.TryGetCell(cell, out var placement, out _))
                    {
                        img.color = _pal.MiniGridEmpty;
                    }
                    else
                    {
                        int tone = placementToneById.TryGetValue(placement.InstanceId, out int t) ? t : 0;
                        img.color = tone == 0 ? _pal.MiniGridCargoA : _pal.MiniGridCargoB;
                    }
                }
            }
        }
    }

    // R8: 인접한 서로 다른 화물이 같은 노란색으로 뭉치지 않도록 2단계 색상을 교대 배정한다.
    void AssignCargoTones(PuzzleInventory _wagon)
    {
        placementToneById.Clear();
        foreach (var p in _wagon.Placements)
        {
            neighborTones.Clear();
            var cells = p.Pose.Cells;
            for (int c = 0; c < cells.Count; c++)
            {
                var pos = cells[c].Position;
                CheckNeighborTone(_wagon, pos + Vector2Int.up, p.InstanceId);
                CheckNeighborTone(_wagon, pos + Vector2Int.down, p.InstanceId);
                CheckNeighborTone(_wagon, pos + Vector2Int.left, p.InstanceId);
                CheckNeighborTone(_wagon, pos + Vector2Int.right, p.InstanceId);
            }

            int chosen = 0;
            if (neighborTones.Contains(0) && !neighborTones.Contains(1))
            {
                chosen = 1;
            }
            placementToneById[p.InstanceId] = chosen;
        }
    }

    void CheckNeighborTone(PuzzleInventory _wagon, Vector2Int _pos, int _selfId)
    {
        if (_wagon.TryGetCell(_pos, out var other, out _) && other.InstanceId != _selfId &&
            placementToneById.TryGetValue(other.InstanceId, out int tone) &&
            !neighborTones.Contains(tone))
        {
            neighborTones.Add(tone);
        }
    }

    void OnClicked()
    {
        if (isDragging)
        {
            return;
        }
        if (BoundWagonIndex >= 0)
        {
            Selected?.Invoke(BoundWagonIndex);
        }
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (BoundWagonIndex < 0)
        {
            return;
        }
        ClearHover();
        isDragging = true;
        dragCapturedWagonIndex = BoundWagonIndex;
        DragStarted?.Invoke(dragCapturedWagonIndex, _eventData);
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (!isDragging || dragCapturedWagonIndex < 0)
        {
            return;
        }
        Dragged?.Invoke(dragCapturedWagonIndex, _eventData);
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        if (!isDragging)
        {
            return;
        }
        isDragging = false;
        int captured = dragCapturedWagonIndex;
        dragCapturedWagonIndex = -1;
        if (captured >= 0)
        {
            DragEnded?.Invoke(captured, _eventData);
        }
    }
}
