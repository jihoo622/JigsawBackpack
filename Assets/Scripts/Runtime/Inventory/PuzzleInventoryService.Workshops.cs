using System;
using System.Collections.Generic;

// 작업장 공통 계산. 역은 PuzzleCityData.workshopIds로 작업장을 가지고, 작업장은 WorksData(TrainventoryWorkTableData)의
// 작업으로 화물을 처리한다. 수수료와 작업 시간은 맡긴 화물마다 작업장 계수를 거쳐 정한다.
public sealed partial class PuzzleInventoryService
{
    // 맡긴 화물 전체의 수수료 합. 작업이 없는 화물은 확정 단계에서 막히므로 합에서 뺀다.
    long WorkshopFeeOf(TrainventoryWorkshopData _workshop, IEnumerable<PuzzlePlacement> _items)
    {
        long fee = 0;
        foreach (var item in _items)
        {
            if (tables.TryGetWorkFor(_workshop, item.Definition.Id, out var work))
            {
                fee = checked(fee + TableManager.WorkFee(_workshop, work));
            }
        }
        return fee;
    }

    // 함께 맡긴 화물은 동시에 시작해 가장 긴 작업 시간이 지나면 모두 나온다.
    long WorkshopHoursOf(TrainventoryWorkshopData _workshop, IEnumerable<PuzzlePlacement> _items)
    {
        long hours = 0;
        foreach (var item in _items)
        {
            if (tables.TryGetWorkFor(_workshop, item.Definition.Id, out var work))
            {
                hours = Math.Max(hours, work.workHours);
            }
        }
        return hours;
    }

    // 작업 시간만큼 흘려도 기한 안인지. 기한을 넘기는 작업은 확정하지 않는다.
    bool WorkshopFitsDeadline(long _hours, out string _reason)
    {
        bool fits = Time != null && Time.CanArriveBeforeDeadline(_hours);
        _reason = fits ? null : BasicData.Text("rules.workshop.deadline");
        return fits;
    }

    // 맡기기 전 견적. 비어 있지 않은 대기(Idle) 보드에서 처리할 수 있는 화물의 수수료 합과 걸리는 시간이다.
    // 맡긴 뒤(작업 중·수령 대기)나 처리할 화물이 없으면 false다.
    bool TryQuoteIdleBoard(TrainventoryWorkshopData _workshop, PuzzleInventory _board, bool _idle,
        out long _fee, out long _hours)
    {
        _fee = 0;
        _hours = 0;
        if (!_idle || _workshop == null || _board == null || _board.Placements.Count == 0)
        {
            return false;
        }
        _fee = WorkshopFeeOf(_workshop, _board.Placements);
        _hours = WorkshopHoursOf(_workshop, _board.Placements);
        return _hours > 0;
    }

    internal bool TryQuoteButchery(out long _fee, out long _hours)
    {
        var state = EnsureButchery(CurrentCityId);
        return TryQuoteIdleBoard(state?.Workshop, state?.Board, state != null && state.Phase == PuzzleButcheryPhase.Idle,
            out _fee, out _hours);
    }

    internal bool TryQuoteRefining(out long _fee, out long _hours)
    {
        var state = EnsureRefining(CurrentCityId);
        return TryQuoteIdleBoard(state?.Workshop, state?.Board, state != null && state.Phase == PuzzleRefiningPhase.Idle,
            out _fee, out _hours);
    }

    // 개조는 고른 작업 하나를 보드의 화물 전체에 적용한다.
    internal bool TryQuoteModification(out long _fee, out long _hours)
    {
        _fee = 0;
        _hours = 0;
        var state = EnsureModification(CurrentCityId);
        if (state == null || state.Phase != PuzzleModificationPhase.Idle || state.Board.Placements.Count == 0 ||
            !TryGetSelectedModificationMode(out var work))
        {
            return false;
        }
        _fee = checked(TableManager.WorkFee(state.Workshop, work) * state.Board.Placements.Count);
        _hours = work.workHours;
        return true;
    }
}
