using System;
using System.Collections.Generic;

internal interface IPuzzlePreservationEffect
{
    int ReductionPercentFor(PuzzleInventory _wagon, PuzzlePlacement _item);
}

internal sealed class PuzzleAdjacentPreservation : IPuzzlePreservationEffect
{
    readonly PuzzleFreshnessData freshness;

    internal PuzzleAdjacentPreservation(PuzzleFreshnessData _freshness)
    {
        freshness = _freshness;
    }

    public int ReductionPercentFor(PuzzleInventory _wagon, PuzzlePlacement _item)
    {
        return PuzzleRefrigeration.AdjacentReductionPercent(_wagon.Placements, _item, freshness);
    }
}

internal sealed class PuzzleShelfLifePreservation
{
    readonly IReadOnlyList<IPuzzlePreservationEffect> cargoEffects;

    internal PuzzleShelfLifePreservation(IReadOnlyList<IPuzzlePreservationEffect> _cargoEffects)
    {
        cargoEffects = _cargoEffects;
    }

    internal decimal DecayRate(PuzzleInventory _wagon, PuzzlePlacement _item)
    {
        if (_item.RemainingShelfLifeMinutes is not > 0)
        {
            return 1m;
        }

        // 화물 규칙과 객차 개조 중 가장 큰 보존 효과를 적용한다.
        int reduction = 0;
        foreach (var effect in cargoEffects)
        {
            reduction = Math.Max(reduction, effect.ReductionPercentFor(_wagon, _item));
        }
        foreach (var modification in _wagon.WagonModifications)
        {
            if (modification is IPuzzlePreservationEffect effect)
            {
                reduction = Math.Max(reduction, effect.ReductionPercentFor(_wagon, _item));
            }
        }
        const int fullPercent = PuzzleInventoryValuation.BASE_PERCENT;
        return 1m - Math.Min(reduction, fullPercent) / (decimal)fullPercent;
    }
}
