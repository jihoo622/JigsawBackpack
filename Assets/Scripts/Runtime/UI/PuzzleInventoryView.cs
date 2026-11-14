using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryView : MonoBehaviour
{
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
    [SerializeField] CanvasGroup interaction;
    [SerializeField] TextMeshProUGUI boardTitle;
    [SerializeField] TextMeshProUGUI boardValue;
    [SerializeField] GameObject synergySettlementNotice;
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

    [Header("Drag Assist Visuals")]
    [SerializeField] Image dragBeam;
    [SerializeField] float beamThickness = 3f;
    [SerializeField] RectTransform[] synergyChips;
    [SerializeField] TextMeshProUGUI[] synergyChipLabels;
    [SerializeField] Image[] synergyChipBackgrounds;

    [Header("Layout & Visual Settings")]
    [SerializeField] float cellGap = 2f;
    [SerializeField, Range(0f, 1f)] float freshnessOutlineWidth = 0.35f;
    [SerializeField] int synergyArrowsCapacityMultiplier = 8;
    [SerializeField] float refrigerationLaneOffset = 0.34f;
    [SerializeField] float synergyArrowWidthScale = 1.8f;
    [SerializeField] float synergyArrowHeightScale = 0.55f;
    [SerializeField] float synergyIncreaseLaneOffset = 0.08f;
    [SerializeField] float synergyDecreaseLaneOffset = 0.2f;

    void OnValidate()
    {
        freshnessOutlineWidth = Mathf.Clamp01(freshnessOutlineWidth);
    }

    readonly HashSet<Vector2Int> piecePositions = new();
    Dictionary<string, ItemIcon> iconIndex;
    Material freshnessMaterial;
    Coroutine settlementFeedback;
    Canvas canvas;
    [SerializeField] RectTransform ghostLayer;
    Vector2Int gridSize;
    Vector2 gridMin;
    float cellSize;
    bool showSynergies;

    Camera EventCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    internal event Action Disabled;

    internal void SetMarketOpen(bool _open)
    {
        interaction.alpha = _open ? 1f : Presentation.PuzzleMarketClosedAlpha;
        interaction.interactable = _open;
        interaction.blocksRaycasts = _open;
    }

    internal void Initialize(long _boardCapacity, long _dragCapacity, string _title,
        IReadOnlyCollection<PuzzleItemDefinition> _items, bool _showSynergies)
    {
        showSynergies = _showSynergies;
        var parentCanvas = GetComponentInParent<Canvas>();
        if (!interaction || !parentCanvas || !board || !boardTitle || !boardTitle.font || !boardValue || !boardValue.font ||
            (_showSynergies && !synergySettlementNotice) ||
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
            !HasSlots(placementSlots, board.rectTransform, reference) ||
            !HasSlots(ghostSlots, ghostLayer ? ghostLayer : transform, reference) ||
            !HasSlots(outlineSlots, board.rectTransform, reference))
        {
            throw new InvalidOperationException("화물 슬롯의 스프라이트 참조·크기 또는 슬롯 구성이 올바르지 않습니다.");
        }

        if (_boardCapacity > boardCells.Length || _boardCapacity > placementSlots.Length ||
            _dragCapacity > ghostSlots.Length || _dragCapacity > outlineSlots.Length ||
            _boardCapacity * synergyArrowsCapacityMultiplier > synergyArrows.Length)
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
        if (!freshnessMaterial)
        {
            freshnessMaterial = new Material(boardTitle.font.material);
            freshnessMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Presentation.PuzzleFreshnessOutline);
            freshnessMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, freshnessOutlineWidth);
            freshnessMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        }
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
                cell.rectTransform.sizeDelta = Vector2.one * (cellSize - cellGap);
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

    internal void RenderPlacements(PuzzleInventory _inventory,
        int? _hiddenInstanceId = null, int? _highlightedInstanceId = null,
        IReadOnlyList<PuzzleRefrigerationContact> _refrigerationContacts = null)
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
                iconIndex[placement.Definition.VisualKey].sprite, placement.Definition.IconSize, placement.Definition.IconOffset,
                // 가치 변동과 공간 부족은 별도 상태지만 같은 알림 아이콘을 쓴다. 읽지 않은 쪽이 남아 있으면 아이콘은 유지한다.
                placement.FreshnessPercent, placement.HasUnreadSynergySettlement || placement.HasUnreadSpaceNotice,
                Presentation.PuzzlePieceAccentOutline(placement.Definition, color.a));
        }

        HideSlots(placementSlots, index);
        // 가치 화살표는 풀 앞에서, 냉장 화살표는 풀 뒤에서 채워 같은 슬롯을 차지하지 않는다.
        int valueEnd = showSynergies ? RenderSynergies(_inventory, _hiddenInstanceId, 0) : 0;
        HideArrows(valueEnd, RenderRefrigeration(_refrigerationContacts, _hiddenInstanceId, synergyArrows.Length));
    }

    int RenderSynergies(PuzzleInventory _inventory, int? _hiddenInstanceId, int _start)
    {
        int index = _start;
        foreach (var target in _inventory.Placements)
        {
            if (target.InstanceId == _hiddenInstanceId)
            {
                continue;
            }

            foreach (var effect in _inventory.ValueOf(target.InstanceId).SynergyBonuses)
            {
                if (effect.NeighborId == _hiddenInstanceId || !effect.Contact.HasValue)
                {
                    continue;
                }

                var contact = effect.Contact.Value;
                if (!ShowArrow(index, contact.Neighbor, contact.Target,
                    effect.Percent > 0 ? increaseArrow : decreaseArrow, Presentation.PuzzleSynergyArrow,
                    Lane(effect.Percent > 0, contact.Target - contact.Neighbor)))
                {
                    return index;
                }

                index++;
            }
        }

        return index;
    }

    // 냉장이 실제로 적용된 관계만 냉장원→음식 방향으로 표시한다. 같은 쌍이 여러 칸에서 접해도 접촉 관계는 하나다.
    int RenderRefrigeration(IReadOnlyList<PuzzleRefrigerationContact> _contacts, int? _hiddenInstanceId, int _after)
    {
        if (_contacts == null)
        {
            return _after;
        }

        int index = _after;
        float lane = refrigerationLaneOffset;
        foreach (var contact in _contacts)
        {
            if (contact.Source.InstanceId == _hiddenInstanceId || contact.Target.InstanceId == _hiddenInstanceId)
            {
                continue;
            }

            index--;
            if (!ShowArrow(index, contact.Cells.Source, contact.Cells.Target, increaseArrow,
                Presentation.PuzzleRefrigerationArrow, lane))
            {
                return index + 1;
            }
        }

        return index;
    }

    // 접촉 칸 쌍을 잇는 화살표 하나. 접촉선을 기준으로 증가·감소 레인과 냉장 레인을 나눠 겹치지 않게 놓는다.
    bool ShowArrow(int _index, Vector2Int _from, Vector2Int _to, Sprite _sprite, Color _color, float _lane)
    {
        if (_index < 0 || _index >= synergyArrows.Length)
        {
            return false;
        }

        Vector2 direction = _to - _from;
        Vector2 axis = direction.x != 0 ? Vector2.up : Vector2.right;
        var arrow = synergyArrows[_index];
        arrow.sprite = _sprite;
        arrow.color = _color;
        arrow.rectTransform.localPosition = (GetCellCenter(_from) + GetCellCenter(_to)) * 0.5f +
            axis * (_lane * cellSize);
        // 원본 스프라이트의 투명 여백을 포함한 크기다.
        arrow.rectTransform.sizeDelta = new Vector2(cellSize * synergyArrowWidthScale, cellSize * synergyArrowHeightScale);
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        arrow.enabled = true;
        return true;
    }

    float Lane(bool _increase, Vector2Int _direction)
    {
        float offset = _increase ? synergyIncreaseLaneOffset : synergyDecreaseLaneOffset;
        return (_direction.x > 0 || _direction.y > 0 ? 1f : -1f) * offset;
    }

    void HideArrows(int _start = 0)
    {
        for (int i = _start; i < synergyArrows.Length; i++)
        {
            synergyArrows[i].enabled = false;
        }
    }

    // 가치 화살표는 앞에서, 냉장 화살표는 뒤에서 쓰므로 사이에 남은 슬롯만 끈다.
    void HideArrows(int _start, int _end)
    {
        int last = Mathf.Min(_end, synergyArrows.Length);
        for (int i = _start; i < last; i++)
        {
            synergyArrows[i].enabled = false;
        }
    }

    // 고스트 슬롯은 보드와 피벗이 다른 레이어 아래에 있으므로 보드 칸 좌표를 그 레이어 좌표로 옮겨 그린다.
    RectTransform GhostParent => ghostLayer ? ghostLayer : (RectTransform)transform;

    // 놓을 자리 후보는 외곽선과 같은 보드 칸에 겹쳐 그린다.
    public void ShowGhostOnBoard(PuzzlePose _pose, PuzzleItemDefinition _item, int? _freshness = null)
    {
        if (_pose == null)
        {
            HideGhost();
            return;
        }

        Vector2 boardOrigin = GhostParent.InverseTransformPoint(board.rectTransform.position);
        RenderGhost(_pose, _item, boardOrigin, _freshness);
    }

    // 놓을 자리가 없으면 잡은 칸의 중심이 커서 아래 오도록 커서를 따라간다.
    public void ShowGhostAtPointer(PuzzlePose _pose, PuzzleItemDefinition _item, Vector2Int _grabbedCell, Vector2 _screenPosition,
        int? _freshness = null)
    {
        if (_pose == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                GhostParent, _screenPosition, EventCamera, out var local))
        {
            HideGhost();
            return;
        }

        RenderGhost(_pose, _item, local - GetCellCenter(_grabbedCell), _freshness);
    }

    public void HideGhost()
    {
        HideSlots(ghostSlots);
    }

    void RenderGhost(PuzzlePose _pose, PuzzleItemDefinition _item, Vector2 _offset, int? _freshness)
    {
        var ghost = Presentation.PuzzleGhost;
        int used = RenderPose(ghostSlots, 0, _pose, _offset, ghost,
            iconIndex[_item.VisualKey].sprite, _item.IconSize, _item.IconOffset, _freshness,
            _outlineColor: Presentation.PuzzlePieceAccentOutline(_item, ghost.a));
        HideSlots(ghostSlots, used);
    }

    PuzzlePose originalSlotPose;
    PuzzlePose previewSlotPose;
    bool previewCanPlace;

    public void ShowOriginalOutline(PuzzlePose _pose)
    {
        originalSlotPose = _pose;
        RenderAllOutlines();
    }

    public void ShowPreview(PuzzlePose _pose, bool _canPlace)
    {
        previewSlotPose = _pose;
        previewCanPlace = _canPlace;
        RenderAllOutlines();
    }

    void RenderAllOutlines()
    {
        int index = 0;
        if (originalSlotPose != null)
        {
            index = RenderPose(outlineSlots, index, originalSlotPose, Vector2.zero,
                Presentation.PuzzleOriginalSlotOutline, _useDashedOutline: true);
        }
        if (previewSlotPose != null)
        {
            index = RenderPose(outlineSlots, index, previewSlotPose, Vector2.zero,
                Presentation.PuzzleOutline(previewCanPlace));
        }
        HideSlots(outlineSlots, index);
    }

    public void ShowDragBeam(Vector2 _fromScreen, Vector2 _toScreen)
    {
        if (!dragBeam)
        {
            return;
        }

        var parent = (RectTransform)dragBeam.rectTransform.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _fromScreen, EventCamera, out var localFrom) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _toScreen, EventCamera, out var localTo))
        {
            dragBeam.gameObject.SetActive(false);
            return;
        }

        Vector2 diff = localTo - localFrom;
        float distance = diff.magnitude;
        if (distance < 4f)
        {
            dragBeam.gameObject.SetActive(false);
            return;
        }

        dragBeam.rectTransform.pivot = new Vector2(0f, 0.5f);
        dragBeam.rectTransform.localPosition = localFrom;
        dragBeam.rectTransform.sizeDelta = new Vector2(distance, beamThickness);
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        dragBeam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        dragBeam.color = Presentation.PuzzleDragBeam;
        dragBeam.raycastTarget = false;
        dragBeam.gameObject.SetActive(true);
    }

    public void HideDragBeam()
    {
        if (dragBeam)
        {
            dragBeam.gameObject.SetActive(false);
        }
    }

    internal void ShowAdjacentSynergyPreview(IReadOnlyList<(PuzzlePlacement neighbor, decimal deltaPercent)> _deltas)
    {
        if (synergyChips == null || synergyChips.Length == 0)
        {
            return;
        }

        int count = _deltas != null ? _deltas.Count : 0;
        int activeCount = 0;
        for (int i = 0; i < count && activeCount < synergyChips.Length; i++)
        {
            var (neighbor, delta) = _deltas[i];
            if (delta == 0)
            {
                continue;
            }

            var chip = synergyChips[activeCount];
            var label = synergyChipLabels != null && activeCount < synergyChipLabels.Length ? synergyChipLabels[activeCount] : null;
            var bg = synergyChipBackgrounds != null && activeCount < synergyChipBackgrounds.Length ? synergyChipBackgrounds[activeCount] : null;

            Vector2 center = GetShapeCenter(neighbor.Pose.Cells);
            chip.localPosition = center;
            chip.SetAsLastSibling();

            if (label)
            {
                label.text = delta > 0 ? $"+{delta:#,0.##}%" : $"−{-delta:#,0.##}%";
                label.color = delta > 0 ? Presentation.PuzzleTooltipGreen : Presentation.PuzzleTooltipRed;
            }
            if (bg)
            {
                bg.color = Presentation.PuzzleSynergyChipBackground;
            }

            chip.gameObject.SetActive(true);
            activeCount++;
        }

        for (int i = activeCount; i < synergyChips.Length; i++)
        {
            if (synergyChips[i])
            {
                synergyChips[i].gameObject.SetActive(false);
            }
        }
    }

    internal void HideAdjacentSynergyPreview()
    {
        if (synergyChips == null)
        {
            return;
        }
        for (int i = 0; i < synergyChips.Length; i++)
        {
            if (synergyChips[i])
            {
                synergyChips[i].gameObject.SetActive(false);
            }
        }
    }

    internal Vector2 GetShapeCenter(IReadOnlyList<PuzzleCell> _cells)
    {
        if (_cells == null || _cells.Count == 0)
        {
            return Vector2.zero;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var cell in _cells)
        {
            Vector2 cellCenter = GetCellCenter(cell.Position);
            float half = cellSize * 0.5f;
            minX = Mathf.Min(minX, cellCenter.x - half);
            minY = Mathf.Min(minY, cellCenter.y - half);
            maxX = Mathf.Max(maxX, cellCenter.x + half);
            maxY = Mathf.Max(maxY, cellCenter.y + half);
        }
        return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    internal Vector2 GetShapeCenterScreen(IReadOnlyList<PuzzleCell> _cells)
    {
        Vector2 local = GetShapeCenter(_cells);
        Vector3 world = board.rectTransform.TransformPoint(local);
        return RectTransformUtility.WorldToScreenPoint(EventCamera, world);
    }

    int RenderPose(PuzzleSlotVisual[] _slots, int _start, PuzzlePose _pose, Vector2 _offset, Color _color, Sprite _icon = null,
        Vector2 _iconSize = default, Vector2 _iconOffset = default, int? _freshness = null, bool _showSynergyNotice = false,
        Color? _outlineColor = null, bool _useDashedOutline = false)
    {
        piecePositions.Clear();
        foreach (var cell in _pose.Cells)
        {
            piecePositions.Add(cell.Position);
        }

        int index = _start;
        var min = _pose.Cells[0].Position;
        var max = min;
        var bottomLeft = min;
        var topRight = max;
        var originalMin = _pose.Cells[0].OriginalCell;
        var originalMax = originalMin;
        foreach (var cell in _pose.Cells)
        {
            min = Vector2Int.Min(min, cell.Position);
            max = Vector2Int.Max(max, cell.Position);
            if (cell.Position.y < bottomLeft.y ||
                (cell.Position.y == bottomLeft.y && cell.Position.x < bottomLeft.x))
            {
                bottomLeft = cell.Position;
            }
            if (cell.Position.y > topRight.y ||
                (cell.Position.y == topRight.y && cell.Position.x > topRight.x))
            {
                topRight = cell.Position;
            }
            originalMin = Vector2Int.Min(originalMin, cell.OriginalCell);
            originalMax = Vector2Int.Max(originalMax, cell.OriginalCell);
        }
        var iconLayout = PuzzleIconLayout.Calculate(min, max, originalMin, originalMax,
            _iconSize, _iconOffset, _pose.ClockwiseTurns);
        Vector2 iconOffset = iconLayout.Center - (Vector2)_pose.Cells[0].Position;
        foreach (var cell in _pose.Cells)
        {
            int hiddenEdges = GetHiddenEdges(cell.Position);
            _slots[index].Show(GetCellCenter(cell.Position) + _offset, cellSize, _color, hiddenEdges,
                index == _start ? _icon : null, iconOffset, iconLayout.Size, _pose.ClockwiseTurns, _outlineColor, _useDashedOutline);
            _slots[index].ShowFreshness(index == _start ? _freshness : null,
                bottomLeft - cell.Position, boardTitle.font, freshnessMaterial);
            _slots[index].ShowSynergyNotice(index == _start && _showSynergyNotice,
                topRight - cell.Position);
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
        originalSlotPose = null;
        previewSlotPose = null;
        HideGhost();
        HideSlots(outlineSlots);
        HideDragBeam();
        HideAdjacentSynergyPreview();
    }

    internal void ShowBoardValue(decimal _actualValue, long _baseValue)
    {
        StopSettlementFeedback();
        boardValue.text = Presentation.PuzzleBoardActualValue(_actualValue, _baseValue);
    }

    internal void ShowCargoValueFeedback(decimal? _beforeSettlement, decimal? _beforeFreshnessLoss, decimal _after, long _baseValue)
    {
        ShowBoardValue(_after, _baseValue);
        if (isActiveAndEnabled)
        {
            synergySettlementNotice.SetActive(true);
            settlementFeedback = StartCoroutine(AnimateCargoValueFeedback(_beforeSettlement, _beforeFreshnessLoss, _after, _baseValue));
        }
    }

    IEnumerator AnimateCargoValueFeedback(decimal? _beforeSettlement, decimal? _beforeFreshnessLoss, decimal _after, long _baseValue)
    {
        var notice = synergySettlementNotice.GetComponent<TMP_Text>();
        if (_beforeSettlement is decimal beforeSettlement)
        {
            notice.text = BasicData.Text("presentation.inventory.synergySettlementNotice");
            yield return AnimateCargoValue(beforeSettlement, _beforeFreshnessLoss ?? _after, _baseValue);
        }
        if (_beforeFreshnessLoss is decimal beforeFreshnessLoss)
        {
            notice.text = BasicData.Text("presentation.inventory.freshnessLossNotice");
            yield return AnimateCargoValue(beforeFreshnessLoss, _after, _baseValue);
        }

        boardValue.text = Presentation.PuzzleBoardActualValue(_after, _baseValue);
        synergySettlementNotice.SetActive(false);
        settlementFeedback = null;
    }

    IEnumerator AnimateCargoValue(decimal _before, decimal _after, long _baseValue)
    {
        const float countSeconds = 0.65f;
        const float noticeSeconds = 1.6f;
        Color highlight = _after < _before ? Presentation.PuzzleTooltipRed : Presentation.PuzzleTooltipGreen;
        string color = ColorUtility.ToHtmlStringRGB(highlight);
        for (float elapsed = 0f; elapsed < noticeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            decimal progress = (decimal)Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / countSeconds));
            decimal value = _before == _after ? _after : _before * (1m - progress) + _after * progress;
            string valueText = Presentation.PuzzleBoardActualValue(value, _baseValue);
            if (_before != _after)
            {
                valueText = $"<color=#{color}>{valueText}</color>";
            }
            boardValue.text = valueText;
            yield return null;
        }

        boardValue.text = Presentation.PuzzleBoardActualValue(_after, _baseValue);
    }

    void StopSettlementFeedback()
    {
        if (settlementFeedback != null)
        {
            StopCoroutine(settlementFeedback);
            settlementFeedback = null;
        }
        if (synergySettlementNotice)
        {
            synergySettlementNotice.SetActive(false);
        }
    }

    internal void ShowBoardInfo(string _text)
    {
        boardValue.overflowMode = TextOverflowModes.Overflow;
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
        StopSettlementFeedback();
        if (canvas)
        {
            ClearDrag();
        }
        Disabled?.Invoke();
    }

    void OnDestroy()
    {
        if (freshnessMaterial)
        {
            Destroy(freshnessMaterial);
        }
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
