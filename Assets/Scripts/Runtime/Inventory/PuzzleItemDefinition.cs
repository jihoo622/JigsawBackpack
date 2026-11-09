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
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<Vector2Int> Cells { get; }

    internal PuzzleItemDefinition(PuzzlePieceData _item, int _baseValue)
    {
        Id = _item.id;
        DisplayName = _item.displayName;
        Description = _item.description;
        BaseValue = _baseValue;
        VisualKey = _item.visualKey;
        Tags = Array.AsReadOnly(_item.tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray());
        Cells = Array.AsReadOnly(_item.cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x)
            .Select(cell => new Vector2Int(cell.x, cell.y)).ToArray());
    }
}
