using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PuzzleItemDefinition
{
    internal const string BABY_TAG = "Baby";

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int BaseValue { get; }
    public string VisualKey { get; }
    public int Durability { get; }
    public int ShelfLifeHours { get; }
    // 아이콘 표시 크기(칸 단위). 미지정이면 Vector2.zero로, 렌더 시 외접 사각형 크기를 쓴다.
    public Vector2 IconSize { get; }
    // 외접 사각형 중심에서 아이콘 중심을 옮기는 오프셋(칸 단위).
    public Vector2 IconOffset { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<Vector2Int> Cells { get; }
    internal bool IsBaby { get; }
    internal bool ExcludeFromMarket { get; }
    internal string AdultPieceId { get; }
    internal string GrowthDescription { get; }
    internal long GrowthMinutes { get; }
    internal long GrowthBlockedMinutes { get; }
    // 성체 자리를 확인하기 시작하는 진행. 이 시각 이후 자리가 없으면 성장은 여기에서 대기한다.
    internal long GrowthFreezeMinutes => GrowthMinutes - GrowthBlockedMinutes;

    // Food 태그 아이템만 신선도를 표시·관리 대상으로 본다.
    public bool HasDurability { get; }

    internal PuzzleItemDefinition(PuzzlePieceData _item)
    {
        Id = _item.id;
        DisplayName = _item.displayName;
        Description = _item.description;
        BaseValue = _item.baseValue;
        VisualKey = _item.visualKey;
        Durability = _item.durability;
        ShelfLifeHours = _item.shelfLifeHours;
        IconSize = _item.iconWidth > 0f && _item.iconHeight > 0f
            ? new Vector2(_item.iconWidth, _item.iconHeight)
            : Vector2.zero;
        IconOffset = new Vector2(_item.iconOffsetX, _item.iconOffsetY);
        Tags = Array.AsReadOnly(_item.tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray());
        IsBaby = Tags.Contains(BABY_TAG);
        ExcludeFromMarket = _item.excludeFromMarket;
        AdultPieceId = _item.adultPieceId;
        GrowthDescription = _item.growthDescription;
        GrowthMinutes = (long)_item.growthHours * PuzzleTimeManager.MINUTES_PER_HOUR;
        GrowthBlockedMinutes = (long)_item.growthBlockedHours * PuzzleTimeManager.MINUTES_PER_HOUR;
        HasDurability = Tags.Contains(BasicData.Text("rules.items.durabilityTag"));
        Cells = Array.AsReadOnly(_item.cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x)
            .Select(cell => new Vector2Int(cell.x, cell.y)).ToArray());
    }
}
