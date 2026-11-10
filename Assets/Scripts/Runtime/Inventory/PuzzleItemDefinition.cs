using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PuzzleItemDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int BaseValue { get; }
    public string VisualKey { get; }
    public int Durability { get; }
    // 아이콘 표시 크기(칸 단위). 미지정이면 Vector2.zero로, 렌더 시 외접 사각형 크기를 쓴다.
    public Vector2 IconSize { get; }
    // 외접 사각형 중심에서 아이콘 중심을 옮기는 오프셋(칸 단위).
    public Vector2 IconOffset { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<Vector2Int> Cells { get; }

    // Food 태그 아이템만 신선도를 표시·관리 대상으로 본다.
    public bool HasDurability { get; }

    internal PuzzleItemDefinition(PuzzlePieceData _item, int _baseValue)
    {
        Id = _item.id;
        DisplayName = _item.displayName;
        Description = _item.description;
        BaseValue = _baseValue;
        VisualKey = _item.visualKey;
        Durability = _item.durability;
        IconSize = _item.iconWidth > 0f && _item.iconHeight > 0f
            ? new Vector2(_item.iconWidth, _item.iconHeight)
            : Vector2.zero;
        IconOffset = new Vector2(_item.iconOffsetX, _item.iconOffsetY);
        Tags = Array.AsReadOnly(_item.tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray());
        HasDurability = Tags.Contains("Food");
        Cells = Array.AsReadOnly(_item.cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x)
            .Select(cell => new Vector2Int(cell.x, cell.y)).ToArray());
    }
}
