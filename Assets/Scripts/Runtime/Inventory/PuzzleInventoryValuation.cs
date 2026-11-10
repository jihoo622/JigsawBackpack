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
    internal Vector2Int TargetCell { get; }
    internal Vector2Int NeighborCell { get; }
    internal decimal Percent { get; }
    internal decimal Amount { get; }

    internal PuzzleSynergyBonus(PuzzlePlacement _neighbor, Vector2Int _targetCell, Vector2Int _neighborCell, decimal _percent, decimal _amount)
    {
        NeighborName = _neighbor.Definition.DisplayName;
        NeighborId = _neighbor.InstanceId;
        TargetCell = _targetCell;
        NeighborCell = _neighborCell;
        Percent = _percent;
        Amount = _amount;
    }
}

internal sealed class PuzzleInventoryValuation
{
    internal const int BASE_PERCENT = 100;
    // 시너지 대상에서 항상 제외하는 면역 태그.
    const string POO_TAG = "Poo";
    // 반대편 슬롯에서 같은 경계를 다시 조회하지 않도록 오른쪽과 위만 확인한다.
    static readonly Vector2Int[] FORWARD_OFFSETS = { Vector2Int.right, Vector2Int.up };

    readonly struct NeighborEffect
    {
        internal readonly PuzzlePlacement neighbor;
        internal readonly Vector2Int targetCell, neighborCell;
        internal readonly decimal bonusPercent, reductionMultiplier;

        internal NeighborEffect(PuzzlePlacement _neighbor, Vector2Int _targetCell, Vector2Int _neighborCell,
            decimal _bonusPercent, decimal _reductionMultiplier)
        {
            neighbor = _neighbor;
            targetCell = _targetCell;
            neighborCell = _neighborCell;
            bonusPercent = _bonusPercent;
            reductionMultiplier = _reductionMultiplier;
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
        var pairs = new HashSet<(int First, int Second)>();
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

                    var pair = placement.InstanceId < neighbor.InstanceId
                        ? (placement.InstanceId, neighbor.InstanceId)
                        : (neighbor.InstanceId, placement.InstanceId);
                    if (pairs.Add(pair))
                    {
                        CollectEffects(placement, neighbor, cell.Position, cell.Position + offset, _rules, effects);
                        CollectEffects(neighbor, placement, cell.Position + offset, cell.Position, _rules, effects);
                    }
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

    static PuzzleValue CalculateValue(int _baseValue, List<NeighborEffect> _effects)
    {
        _effects.Sort((left, right) => left.neighbor.InstanceId.CompareTo(right.neighbor.InstanceId));
        var details = new List<PuzzleSynergyBonus>();
        decimal finalValue = _baseValue;
        foreach (var effect in _effects)
        {
            if (effect.bonusPercent > 0)
            {
                decimal amount = _baseValue * (effect.bonusPercent / BASE_PERCENT);
                finalValue += amount;
                details.Add(new PuzzleSynergyBonus(effect.neighbor, effect.targetCell, effect.neighborCell, effect.bonusPercent, amount));
            }
        }
        foreach (var effect in _effects)
        {
            if (effect.reductionMultiplier < 1)
            {
                decimal reducedValue = finalValue * effect.reductionMultiplier;
                decimal percent = (effect.reductionMultiplier - 1) * BASE_PERCENT;
                details.Add(new PuzzleSynergyBonus(effect.neighbor, effect.targetCell, effect.neighborCell, percent, reducedValue - finalValue));
                finalValue = reducedValue;
            }
        }
        return new PuzzleValue(_baseValue, finalValue, details.AsReadOnly());
    }

    static void CollectEffects(PuzzlePlacement _target, PuzzlePlacement _neighbor, Vector2Int _targetCell,
        Vector2Int _neighborCell, IReadOnlyList<PuzzleSynergyRuleData> _rules,
        Dictionary<int, List<NeighborEffect>> _effects)
    {
        // Poo 태그 아이템은 어떤 태그 시너지의 대상도 되지 않는다(음수 가치 고정).
        if (_target.Definition.Tags.Contains(POO_TAG))
        {
            return;
        }

        decimal bonusPercent = 0;
        decimal reductionMultiplier = 1;
        foreach (var rule in _rules)
        {
            bool matchesTarget = string.IsNullOrEmpty(rule.excludedTargetTag)
                ? _target.Definition.Tags.Contains(rule.targetTag)
                : !_target.Definition.Tags.Contains(rule.excludedTargetTag);
            if (!matchesTarget || !_neighbor.Definition.Tags.Contains(rule.neighborTag) ||
                (!string.IsNullOrEmpty(rule.excludedNeighborTag) && _neighbor.Definition.Tags.Contains(rule.excludedNeighborTag)))
            {
                continue;
            }

            if (rule.multiplierPercent < BASE_PERCENT)
            {
                reductionMultiplier *= (decimal)rule.multiplierPercent / BASE_PERCENT;
            }
            else
            {
                bonusPercent += rule.multiplierPercent - BASE_PERCENT;
            }
        }

        if (bonusPercent > 0 || reductionMultiplier < 1)
        {
            if (!_effects.TryGetValue(_target.InstanceId, out var itemEffects))
            {
                itemEffects = new List<NeighborEffect>();
                _effects.Add(_target.InstanceId, itemEffects);
            }

            itemEffects.Add(new NeighborEffect(_neighbor, _targetCell, _neighborCell, bonusPercent, reductionMultiplier));
        }
    }
}
