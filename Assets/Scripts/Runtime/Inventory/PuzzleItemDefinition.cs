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
        Tags = Array.AsReadOnly(_item.tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray());
        HasDurability = Tags.Contains("Food");
        Cells = Array.AsReadOnly(_item.cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x)
            .Select(cell => new Vector2Int(cell.x, cell.y)).ToArray());
    }
}
