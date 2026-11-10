using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryView : MonoBehaviour
{
    const float CELL_GAP = 2f;
    const string ITEM_ICON_PATH = "PuzzleInventoryUI.itemIcons";

    [Serializable]
    sealed class ItemIcon
    {
        public string key;
        public Sprite sprite;
    }

    // PuzzleSlotVisual의 이미지와 같은 시계 방향 순서다.
    static readonly Vector2Int[] NEIGHBOR_OFFSETS =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    [SerializeField] Image board;
    [SerializeField] TextMeshProUGUI boardTitle;
    [SerializeField] TextMeshProUGUI boardValue;
    [SerializeField] TextMeshProUGUI moveGuide;
    [SerializeField] TextMeshProUGUI itemDetails;
    [SerializeField] ItemIcon[] itemIcons;
    [SerializeField] Image flatTriangle;
    [SerializeField] Sprite eastFlatTriangle;
    [SerializeField] Sprite southFlatTriangle;
    [SerializeField] Sprite westFlatTriangle;
    [SerializeField] Sprite flatOutline;
    [SerializeField] Image[] boardCells;
    [SerializeField] PuzzleSlotVisual[] placementSlots;
    [SerializeField] PuzzleSlotVisual[] ghostSlots;
    [SerializeField] PuzzleSlotVisual[] outlineSlots;
    [SerializeField] Image[] synergyArrows;
    [SerializeField] Sprite increaseArrow;
    [SerializeField] Sprite decreaseArrow;

    readonly HashSet<Vector2Int> piecePositions = new();
    Dictionary<string, ItemIcon> iconIndex;
    Canvas canvas;
    Vector2Int gridSize;
    Vector2 gridMin;
    float cellSize;
    bool showSynergies;

    Camera EventCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    internal event Action Disabled;

    internal void Initialize(long _boardCapacity, long _dragCapacity, string _title,
        IReadOnlyCollection<PuzzleItemDefinition> _items, bool _showSynergies)
    {
        showSynergies = _showSynergies;
        var parentCanvas = GetComponentInParent<Canvas>();
        if (!parentCanvas || !board || !boardTitle || !boardTitle.font || !boardValue || !boardValue.font ||
            !moveGuide || !moveGuide.font ||

            !flatTriangle || boardCells == null || boardCells.Distinct().Count() != boardCells.Length ||
            boardCells.Any(image => !image || image.rectTransform.parent != board.rectTransform) ||
            !increaseArrow || !decreaseArrow || synergyArrows == null ||
            synergyArrows.Distinct().Count() != synergyArrows.Length ||
            synergyArrows.Any(image => !image || image.rectTransform.parent != board.rectTransform))
        {
            throw new InvalidOperationException("화물 인벤토리 View의 Canvas 또는 프리팹 참조가 누락되었습니다.");
        }

        var rules = new GameTableRules();
        var icons = rules.Index(itemIcons, entry => entry.key, ITEM_ICON_PATH);
        rules.ValidateItemVisuals(_items, key => icons.TryGetValue(key, out var icon) && icon.sprite, ITEM_ICON_PATH);
        if (!rules.IsValid || icons.Values.Any(icon => !icon.sprite))
        {
            throw new InvalidOperationException("화물 인벤토리 프리팹의 아이콘 키 또는 스프라이트 참조가 올바르지 않습니다.");
        }
        iconIndex = icons;

        var reference = flatTriangle.sprite;
        var fillSprites = new[] { reference, eastFlatTriangle, southFlatTriangle, westFlatTriangle };
        if (!reference || !fillSprites.All(sprite => MatchesReference(sprite, reference)) ||
            !MatchesReference(flatOutline, reference) ||
            !HasSlots(placementSlots, board.rectTransform, reference) || !HasSlots(ghostSlots, transform, reference) ||
            !HasSlots(outlineSlots, board.rectTransform, reference))
        {
            throw new InvalidOperationException("화물 슬롯의 스프라이트 참조·크기 또는 슬롯 구성이 올바르지 않습니다.");
        }

        if (_boardCapacity > boardCells.Length || _boardCapacity > placementSlots.Length ||
            _dragCapacity > ghostSlots.Length || _dragCapacity > outlineSlots.Length ||
            _boardCapacity * 8 > synergyArrows.Length)
        {
            throw new InvalidOperationException("화물 테이블과 인벤토리 프리팹의 표시 용량이 맞지 않습니다.");
        }

        canvas = parentCanvas.rootCanvas;
        if (canvas.renderMode == RenderMode.WorldSpace && !canvas.worldCamera)
        {
            throw new InvalidOperationException("World Space Canvas에는 화물 좌표 변환에 사용할 카메라가 필요합니다.");
        }

        board.color = Presentation.PuzzleBackground;
        if (itemDetails)
        {
            itemDetails.gameObject.SetActive(false);
        }
        boardTitle.text = _title;
        ShowItemDetails(null, default, default, default);

        ConfigureSlots(placementSlots, flatOutline, fillSprites);
        ConfigureSlots(ghostSlots, flatOutline, fillSprites);
        ConfigureSlots(outlineSlots, flatOutline);
    }

    internal void SetTitle(string _title)
    {
        if (boardTitle)
        {
            boardTitle.text = _title;
        }
    }

    internal void SetBoardSize(int _width, int _height)
    {
        gridSize = new Vector2Int(_width, _height);
        Rect boardRect = board.rectTransform.rect;
        cellSize = Mathf.Min(boardRect.width / _width, boardRect.height / _height);
        gridMin = boardRect.center - new Vector2(_width, _height) * (cellSize * 0.5f);

        long cellCount = (long)_width * _height;
        for (int i = 0; i < boardCells.Length; i++)
        {
            var cell = boardCells[i];
            cell.enabled = i < cellCount;
            if (cell.enabled)
            {
                cell.color = Presentation.PuzzleCell;
                cell.rectTransform.localPosition = GetCellCenter(new Vector2Int(i % _width, i / _width));
                cell.rectTransform.sizeDelta = Vector2.one * (cellSize - CELL_GAP);
            }
        }

        HideSlots(placementSlots);
        HideArrows();
        ClearDrag();
    }

    public bool TryGetBoardCell(Vector2 _screenPosition, out Vector2Int _cell)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            board.rectTransform, _screenPosition, EventCamera, out var local))
        {
            var position = (local - gridMin) / cellSize;
            _cell = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
            return _cell.x >= 0 && _cell.x < gridSize.x && _cell.y >= 0 && _cell.y < gridSize.y;
        }

        _cell = default;
        return false;
    }

    public void RenderPlacements(PuzzleInventory _inventory,
        int? _hiddenInstanceId = null, int? _highlightedInstanceId = null)
    {
        int index = 0;
        foreach (var placement in _inventory.Placements)
        {
            if (placement.InstanceId == _hiddenInstanceId)
            {
                continue;
            }

            var color = placement.InstanceId == _highlightedInstanceId
                ? Presentation.PuzzleMoveTarget
                : Presentation.PuzzlePiece;
            index = RenderPose(placementSlots, index, placement.Pose, Vector2.zero, color,
                iconIndex[placement.Definition.VisualKey].sprite, placement.Definition.IconSize, placement.Definition.IconOffset);
        }

        HideSlots(placementSlots, index);
        if (showSynergies)
        {
            RenderSynergies(_inventory, _hiddenInstanceId);
        }
        else
        {
            HideArrows();
        }
    }

    void RenderSynergies(PuzzleInventory _inventory, int? _hiddenInstanceId)
    {
        int index = 0;
        foreach (var target in _inventory.Placements)
        {
            if (target.InstanceId == _hiddenInstanceId)
            {
                continue;
            }

            foreach (var effect in _inventory.ValueOf(target.InstanceId).SynergyBonuses)
            {
                if (effect.NeighborId == _hiddenInstanceId)
                {
                    continue;
                }

                Vector2 direction = effect.TargetCell - effect.NeighborCell;
                bool forward = direction.x > 0 || direction.y > 0;
                bool increase = effect.Percent > 0;
                Vector2 axis = direction.x != 0 ? Vector2.up : Vector2.right;
                float lane = (forward ? 1f : -1f) * (increase ? 0.08f : 0.2f);
                var arrow = synergyArrows[index++];
                arrow.sprite = increase ? increaseArrow : decreaseArrow;
                arrow.color = Presentation.PuzzleSynergyArrow;
                arrow.rectTransform.localPosition = (GetCellCenter(effect.TargetCell) +
                    GetCellCenter(effect.NeighborCell)) * 0.5f + axis * (lane * cellSize);
                // 원본 스프라이트의 투명 여백을 포함한 크기다.
                arrow.rectTransform.sizeDelta = new Vector2(cellSize * 1.8f, cellSize * 0.55f);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                arrow.enabled = true;
            }
        }
        HideArrows(index);
    }

    void HideArrows(int _start = 0)
    {
        for (int i = _start; i < synergyArrows.Length; i++)
        {
            synergyArrows[i].enabled = false;
        }
    }

    public void ShowGhost(PuzzlePose _pose, PuzzleItemDefinition _item, Vector2Int _grabbedBoardCell, Vector2 _screenPosition)
    {
        if (_pose == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, _screenPosition, EventCamera, out var local))
        {
            HideSlots(ghostSlots);
            return;
        }

        var offset = local - GetCellCenter(_grabbedBoardCell);
        int used = RenderPose(ghostSlots, 0, _pose, offset, Presentation.PuzzleGhost, iconIndex[_item.VisualKey].sprite, _item.IconSize, _item.IconOffset);
        HideSlots(ghostSlots, used);
    }

    public void ShowPreview(PuzzlePose _pose, bool _canPlace)
    {
        int used = _pose == null ? 0 : RenderPose(outlineSlots, 0, _pose, Vector2.zero, Presentation.PuzzleOutline(_canPlace));
        HideSlots(outlineSlots, used);
    }

    int RenderPose(PuzzleSlotVisual[] _slots, int _start, PuzzlePose _pose, Vector2 _offset, Color _color, Sprite _icon = null,
        Vector2 _iconSize = default, Vector2 _iconOffset = default)
    {
        piecePositions.Clear();
        foreach (var cell in _pose.Cells)
        {
            piecePositions.Add(cell.Position);
        }

        int index = _start;
        var min = _pose.Cells[0].Position;
        var max = min;
        var originalMin = _pose.Cells[0].OriginalCell;
        var originalMax = originalMin;
        foreach (var cell in _pose.Cells)
        {
            min = Vector2Int.Min(min, cell.Position);
            max = Vector2Int.Max(max, cell.Position);
            originalMin = Vector2Int.Min(originalMin, cell.OriginalCell);
            originalMax = Vector2Int.Max(originalMax, cell.OriginalCell);
        }
        Vector2 iconOffset = ((Vector2)min + max) * 0.5f - (Vector2)_pose.Cells[0].Position + _iconOffset;
        Vector2 iconSize = (_iconSize.x > 0f && _iconSize.y > 0f)
            ? _iconSize
            : (Vector2)(originalMax - originalMin + Vector2Int.one);
        foreach (var cell in _pose.Cells)
        {
            int hiddenEdges = GetHiddenEdges(cell.Position);
            _slots[index].Show(GetCellCenter(cell.Position) + _offset, cellSize, _color, hiddenEdges,
                index == _start ? _icon : null, iconOffset, iconSize, _pose.ClockwiseTurns);
            index++;
        }
        if (_icon && index > _start)
        {
            var first = _slots[_start].transform;
            if (_slots == placementSlots && synergyArrows.Length > 0)
            {
                int arrowIndex = synergyArrows[0].transform.GetSiblingIndex();
                first.SetSiblingIndex(first.GetSiblingIndex() < arrowIndex ? arrowIndex - 1 : arrowIndex);
            }
            else if (_slots == ghostSlots)
            {
                first.SetAsLastSibling();
            }
        }
        return index;
    }

    public void ClearDrag()
    {
        HideSlots(ghostSlots);
        HideSlots(outlineSlots);
    }

    internal void ShowBoardValue(PuzzleValue _value)
    {
        boardValue.text = Presentation.PuzzleValueText(_value);
    }

    internal void ShowBoardInfo(string _text)
    {
        boardValue.text = _text;
    }

    internal void ShowItemDetails(PuzzleItemDefinition _item, PuzzleValue _value, decimal _actualValue,
        PuzzleTradeRate _rate)
    {
        if (itemDetails)
        {
            itemDetails.text = Presentation.PuzzleItemDetails(_item, _value, _actualValue, _rate);
        }
    }

    void OnDisable()
    {
        if (canvas)
        {
            ClearDrag();
        }
        Disabled?.Invoke();
    }

    Vector2 GetCellCenter(Vector2Int _cell)
    {
        return gridMin + ((Vector2)_cell + Vector2.one * 0.5f) * cellSize;
    }

    int GetHiddenEdges(Vector2Int _position)
    {
        int hiddenEdges = 0;
        for (int direction = 0; direction < NEIGHBOR_OFFSETS.Length; direction++)
        {
            if (piecePositions.Contains(_position + NEIGHBOR_OFFSETS[direction]))
            {
                hiddenEdges |= 1 << direction;
            }
        }
        return hiddenEdges;
    }

    static bool MatchesReference(Sprite _sprite, Sprite _reference)
    {
        return _sprite && _sprite.rect.size == _reference.rect.size && _sprite.pixelsPerUnit == _reference.pixelsPerUnit &&
            _sprite.pivot == _sprite.rect.size * 0.5f;
    }

    static bool HasSlots(PuzzleSlotVisual[] _slots, Transform _parent, Sprite _reference)
    {
        return _slots != null && _slots.Distinct().Count() == _slots.Length &&
            _slots.All(slot => slot && slot.transform.parent == _parent && slot.IsConfigured(_reference));
    }

    static void ConfigureSlots(PuzzleSlotVisual[] _slots, Sprite _outlineSprite, Sprite[] _fillSprites = null)
    {
        foreach (var slot in _slots)
        {
            slot.ConfigureSprites(_outlineSprite, _fillSprites);
        }
    }

    static void HideSlots(PuzzleSlotVisual[] _slots, int _start = 0)
    {
        for (int i = _start; i < _slots.Length; i++)
        {
            _slots[i].Hide();
        }
    }
}
