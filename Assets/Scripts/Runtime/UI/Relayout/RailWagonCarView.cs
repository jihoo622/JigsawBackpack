using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// S3/S3b: 중앙 선로의 가상화 화차 슬롯(R8 7×5 미니 격자 포함, H20 순서 드래그 + H17 판매 드래그).
[DisallowMultipleComponent]
public sealed class RailWagonCarView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const int MINI_COLS = 7;
    public const int MINI_ROWS = 6;
    public const int MINI_CELL_COUNT = MINI_COLS * MINI_ROWS;

    [SerializeField] RectTransform rectTransform;
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Image highlightBorder;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image[] miniGridCells;
    [SerializeField] UIPalette palette;

    readonly Dictionary<int, int> placementToneById = new();
    readonly List<int> neighborTones = new();
    bool isDragging;
    int dragCapturedWagonIndex = -1;

    public event Action<int> Selected;
    public event Action<int, PointerEventData> DragStarted;
    public event Action<int, PointerEventData> Dragged;
    public event Action<int, PointerEventData> DragEnded;

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
        isDragging = false;
        dragCapturedWagonIndex = -1;
        if (button)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }

    public void Bind(PuzzleInventory _wagon, int _index, bool _selected, bool _hovered)
    {
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
            label.text = Presentation.RailWagonLabel(_wagon.WagonNumber, _wagon.OccupiedCells, _wagon.UsableCellCount, value);
        }

        if (highlightBorder)
        {
            highlightBorder.enabled = _selected || _hovered;
            if (pal)
            {
                var c = _selected ? pal.GoldAccent : pal.BorderBright;
                c.a = 0.16f;
                highlightBorder.color = c;
            }
        }
        if (background && pal)
        {
            background.color = _selected ? pal.Panel2 : pal.Panel;
        }

        RenderMiniGrid(_wagon, pal);
    }

    public void Unbind()
    {
        BoundWagonIndex = -1;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
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
