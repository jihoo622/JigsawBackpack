using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal enum PuzzleSynergyScope
{
    Train,
    Market,
    Both
}

internal readonly struct PuzzleValue
{
    readonly IReadOnlyList<PuzzleSynergyBonus> synergyBonuses;

    public long BaseValue { get; }
    public decimal FinalValue { get; }
    internal IReadOnlyList<PuzzleSynergyBonus> SynergyBonuses => synergyBonuses ?? Array.Empty<PuzzleSynergyBonus>();

    internal PuzzleValue(long _baseValue, decimal _finalValue, IReadOnlyList<PuzzleSynergyBonus> _synergyBonuses = null)
    {
        BaseValue = _baseValue;
        FinalValue = _finalValue;
        synergyBonuses = _synergyBonuses;
    }
}

internal readonly struct PuzzleSynergyBonus
{
    internal string NeighborName { get; }
    internal int NeighborId { get; }
    internal (Vector2Int Target, Vector2Int Neighbor)? Contact { get; }
    internal decimal Percent { get; }
    internal decimal Amount { get; }
    // 같은 이웃과 닿은 면 수. 면 하나의 효과는 1이고 이웃별로 묶으면 합친 면 수다.
    internal int Faces { get; }

    internal PuzzleSynergyBonus(PuzzlePlacement _neighbor, (Vector2Int Target, Vector2Int Neighbor)? _contact,
        decimal _percent, decimal _amount)
    {
        NeighborName = _neighbor.Definition.DisplayName;
        NeighborId = _neighbor.InstanceId;
        Contact = _contact;
        Percent = _percent;
        Amount = _amount;
        Faces = 1;
    }

    PuzzleSynergyBonus(string _neighborName, int _neighborId, decimal _percent, decimal _amount, int _faces)
    {
        NeighborName = _neighborName;
        NeighborId = _neighborId;
        Contact = null;
        Percent = _percent;
        Amount = _amount;
        Faces = _faces;
    }

    // 면마다 나뉜 효과를 이웃 화물별로 합친다. 화살표는 면 단위, 목록 표기는 이웃 단위로 쓴다.
    internal static IReadOnlyList<PuzzleSynergyBonus> GroupByNeighbor(IEnumerable<PuzzleSynergyBonus> _bonuses)
    {
        return _bonuses.GroupBy(_bonus => _bonus.NeighborId)
            .Select(_group => new PuzzleSynergyBonus(_group.First().NeighborName, _group.Key,
                _group.Sum(_bonus => _bonus.Percent), _group.Sum(_bonus => _bonus.Amount), _group.Sum(_bonus => _bonus.Faces)))
            .ToList();
    }
}

internal sealed class PuzzleInventoryValuation
{
    internal const int BASE_PERCENT = 100;
    // 시너지 대상에서 항상 제외하는 면역 태그.
    static string WasteTag => TableManager.Instance.PuzzleSynergy.animals.wasteTag;
    // 반대편 슬롯에서 같은 경계를 다시 조회하지 않도록 오른쪽과 위만 확인한다. 경계(면)마다 한 번씩 평가한다.
    static readonly Vector2Int[] FORWARD_OFFSETS = { Vector2Int.right, Vector2Int.up };

    readonly struct NeighborEffect
    {
        internal readonly PuzzlePlacement neighbor;
        internal readonly (Vector2Int Target, Vector2Int Neighbor)? contact;
        // 이 이웃이 주는 모든 규칙의 (배율 − 100) 합. 음수면 역시너지다.
        internal readonly decimal percent;

        internal NeighborEffect(PuzzlePlacement _neighbor, (Vector2Int Target, Vector2Int Neighbor)? _contact, decimal _percent)
        {
            neighbor = _neighbor;
            contact = _contact;
            percent = _percent;
        }
    }

    readonly Dictionary<int, PuzzleValue> items = new();

    internal PuzzleValue Total { get; private set; }

    internal PuzzleValue ValueOf(int _instanceId)
    {
        return items[_instanceId];
    }

    internal void Recalculate(PuzzleInventory _board, IReadOnlyList<PuzzleSynergyRuleData> _rules)
    {
        var effects = new Dictionary<int, List<NeighborEffect>>();
        var adjacentRules = _rules.Where(_rule => _rule.spatialRange == nameof(PuzzleSynergyRange.Adjacent)).ToArray();
        if (adjacentRules.Length > 0)
        {
            CollectAdjacentEffects(_board, adjacentRules, effects);
        }

        var boardRules = _rules.Where(_rule => _rule.spatialRange == nameof(PuzzleSynergyRange.Board)).ToArray();
        if (boardRules.Length > 0)
        {
            var placements = _board.Placements.ToArray();
            for (int i = 0; i < placements.Length; i++)
            {
                for (int j = i + 1; j < placements.Length; j++)
                {
                    CollectEffects(placements[i], placements[j], null, boardRules, effects);
                    CollectEffects(placements[j], placements[i], null, boardRules, effects);
                }
            }
        }

        items.Clear();
        long baseTotal = 0;
        decimal finalTotal = 0;
        foreach (var placement in _board.Placements)
        {
            int baseValue = placement.Definition.BaseValue;
            var value = effects.TryGetValue(placement.InstanceId, out var itemEffects)
                ? CalculateValue(baseValue, itemEffects)
                : new PuzzleValue(baseValue, baseValue);
            items.Add(placement.InstanceId, value);
            baseTotal += baseValue;
            finalTotal += value.FinalValue;
        }
        Total = new PuzzleValue(baseTotal, finalTotal);
    }

    static void CollectAdjacentEffects(PuzzleInventory _board, IReadOnlyList<PuzzleSynergyRuleData> _rules,
        Dictionary<int, List<NeighborEffect>> _effects)
    {
        foreach (var placement in _board.Placements)
        {
            foreach (var cell in placement.Pose.Cells)
            {
                foreach (var offset in FORWARD_OFFSETS)
                {
                    if (!_board.TryGetCell(cell.Position + offset, out var neighbor, out _) ||
                        placement.InstanceId == neighbor.InstanceId)
                    {
                        continue;
                    }

                    // 같은 이웃과 여러 면이 닿으면 면 수만큼 효과를 받는다.
                    var neighborPosition = cell.Position + offset;
                    CollectEffects(placement, neighbor, (cell.Position, neighborPosition), _rules, _effects);
                    CollectEffects(neighbor, placement, (neighborPosition, cell.Position), _rules, _effects);
                }
            }
        }
    }

    static PuzzleValue CalculateValue(int _baseValue, List<NeighborEffect> _effects)
    {
        _effects.Sort((_left, _right) =>
        {
            int order = _left.neighbor.InstanceId.CompareTo(_right.neighbor.InstanceId);
            return order != 0 ? order : _right.contact.HasValue.CompareTo(_left.contact.HasValue);
        });
        // 합연산: 100% + 모든 효과의 합. 시너지와 역시너지를 같은 식으로 더하며 음수 배율도 허용한다.
        var details = new List<PuzzleSynergyBonus>();
        decimal totalPercent = 0;
        foreach (var effect in _effects)
        {
            totalPercent += effect.percent;
            details.Add(new PuzzleSynergyBonus(effect.neighbor, effect.contact, effect.percent,
                _baseValue * (effect.percent / BASE_PERCENT)));
        }
        decimal finalValue = _baseValue * ((BASE_PERCENT + totalPercent) / BASE_PERCENT);
        return new PuzzleValue(_baseValue, finalValue, details.AsReadOnly());
    }

    static void CollectEffects(PuzzlePlacement _target, PuzzlePlacement _neighbor,
        (Vector2Int Target, Vector2Int Neighbor)? _contact, IReadOnlyList<PuzzleSynergyRuleData> _rules,
        Dictionary<int, List<NeighborEffect>> _effects)
    {
        // Poo 태그 아이템은 어떤 태그 시너지의 대상도 되지 않는다(음수 가치 고정).
        if (_target.Definition.Tags.Contains(WasteTag))
        {
            return;
        }

        decimal percent = 0;
        foreach (var rule in _rules)
        {
            if (!PuzzlePieceEffects.IsTarget(_target.Definition, rule, WasteTag) ||
                !PuzzlePieceEffects.IsSource(_neighbor.Definition, rule))
            {
                continue;
            }

            percent += rule.multiplierPercent - BASE_PERCENT;
        }

        if (percent != 0)
        {
            if (!_effects.TryGetValue(_target.InstanceId, out var itemEffects))
            {
                itemEffects = new List<NeighborEffect>();
                _effects.Add(_target.InstanceId, itemEffects);
            }

            itemEffects.Add(new NeighborEffect(_neighbor, _contact, percent));
        }
    }
}
