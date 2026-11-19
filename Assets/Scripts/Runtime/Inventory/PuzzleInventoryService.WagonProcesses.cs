using System;
using System.Collections.Generic;
using System.Linq;

// 화차 가공(텃밭·제련·분쇄). 재료가 차면 자동으로 묶음을 시작하고, 시간이 지나면 재료를 없애고 산출물을 같은 객차에 놓는다.
// 산출물 자리가 없으면 완료 상태로 기다리고, 재료가 빠지거나 바뀌면 묶음을 버리고 처음부터 다시 센다.
// 묶음 상태는 객차(PuzzleInventory.ProcessState)가 들고 시간 진행 사본과 함께 커밋된다.
public sealed partial class PuzzleInventoryService
{
    internal TrainventoryWagonProcessData WagonProcessOf(PuzzleInventory _wagon)
    {
        return _wagon?.WagonProduct != null && tables.TryGetWagonProcess(_wagon.WagonProduct.processId, out var process)
            ? process
            : null;
    }

    // 표시용 묶음 상태. 재료가 빠진 묶음은 다음 시간 진행에서 버려지므로 지금도 없는 것으로 본다.
    internal PuzzleWagonProcessState CurrentWagonProcess(PuzzleInventory _wagon)
    {
        var state = _wagon?.ProcessState;
        return state != null && InputsPresent(_wagon, state) ? state : null;
    }

    // 운임에 넣는 적재 칸 수. 화차가 면제하는 태그의 화물은 빼고 센다.
    internal static int FreightCellsOf(PuzzleInventory _wagon)
    {
        var exempt = _wagon.WagonProduct?.freightExemptTags;
        if (exempt == null || exempt.Length == 0)
        {
            return _wagon.OccupiedCells;
        }
        return _wagon.Placements
            .Where(_item => !_item.Definition.Tags.Any(exempt.Contains))
            .Sum(_item => _item.Pose.Cells.Count);
    }

    static bool InputsPresent(PuzzleInventory _wagon, PuzzleWagonProcessState _state)
    {
        foreach (var input in _state.Inputs)
        {
            if (!_wagon.TryGetPlacement(input.InstanceId, out var placement) ||
                !string.Equals(placement.Definition.Id, input.PieceId, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    // 묶음이 없는 객차는 재료를 골라 시작한다. 재료가 빠진 묶음은 버린다.
    void EnsureWagonProcesses(PuzzleInventory[] _wagons)
    {
        foreach (var wagon in _wagons)
        {
            var process = WagonProcessOf(wagon);
            if (process == null)
            {
                wagon.ProcessState = null;
                continue;
            }
            if (wagon.ProcessState != null && !InputsPresent(wagon, wagon.ProcessState))
            {
                wagon.ProcessState = null;
            }
            wagon.ProcessState ??= SelectWagonProcess(wagon, process);
        }
    }

    // 재료 고르기. 주재료는 인스턴스 ID가 작은 것부터 보고, 처음으로 부재료까지 다 갖춰지는 묶음을 고른다.
    PuzzleWagonProcessState SelectWagonProcess(PuzzleInventory _wagon, TrainventoryWagonProcessData _process)
    {
        if (!TableManager.TryGetWagonProcessType(_process, out var type))
        {
            return null;
        }
        var items = _wagon.Placements.OrderBy(_item => _item.InstanceId).ToArray();
        foreach (var primary in items.Where(_item => _item.Definition.Tags.Contains(_process.primaryTag)))
        {
            int cells = primary.Pose.Cells.Count;
            if (type == TrainventoryWagonProcessType.Crush)
            {
                if (cells > 1)
                {
                    return new PuzzleWagonProcessState(new[] { (primary.InstanceId, primary.Definition.Id) }, cells,
                        checked(_process.hoursPerCell * cells), 0, false);
                }
                continue;
            }

            var chosen = new List<PuzzlePlacement> { primary };
            bool complete = true;
            foreach (var input in _process.inputs ?? Array.Empty<TrainventoryWagonProcessInputData>())
            {
                int need = checked(input.countPerUnit * cells);
                var picks = items
                    .Where(_item => !chosen.Contains(_item) && _item.Pose.Cells.Count == 1 &&
                        _item.Definition.Tags.Contains(input.tag))
                    .Take(need)
                    .ToArray();
                if (picks.Length < need)
                {
                    complete = false;
                    break;
                }
                chosen.AddRange(picks);
            }
            if (complete)
            {
                return new PuzzleWagonProcessState(chosen.Select(_item => (_item.InstanceId, _item.Definition.Id)).ToArray(),
                    cells, _process.hours, 0, false);
            }
        }
        return null;
    }

    static long HoursUntilWagonProcess(PuzzleInventory[] _wagons)
    {
        long next = long.MaxValue;
        foreach (var wagon in _wagons)
        {
            var state = wagon.ProcessState;
            if (state != null && !state.WaitingForSpace)
            {
                next = Math.Min(next, state.RequiredHours - state.ElapsedHours);
            }
        }
        return Math.Max(1, next);
    }

    // 경과 시간을 더하고 다 된 묶음을 완료한다. 자리를 기다리던 묶음은 경계마다 다시 놓아 본다.
    void AdvanceWagonProcesses(TimeAdvancePlan _plan, PuzzleInventory[] _wagons, long _step)
    {
        foreach (var wagon in _wagons)
        {
            var state = wagon.ProcessState;
            var process = WagonProcessOf(wagon);
            if (state == null || process == null)
            {
                continue;
            }
            if (!InputsPresent(wagon, state))
            {
                wagon.ProcessState = null;
                continue;
            }
            long elapsed = state.WaitingForSpace ? state.RequiredHours : Math.Min(state.RequiredHours, state.ElapsedHours + _step);
            if (elapsed < state.RequiredHours)
            {
                wagon.ProcessState = state.With(elapsed, false);
                continue;
            }
            wagon.ProcessState = TryCompleteWagonProcess(_plan, wagon, process, state) ? null : state.With(elapsed, true);
        }
    }

    // 시간 진행 시작 시각에 자리를 기다리던 묶음을 다시 놓아 본다(앞선 행동으로 자리가 났을 수 있다).
    void ResumeWagonProcesses(TimeAdvancePlan _plan, PuzzleInventory[] _wagons)
    {
        foreach (var wagon in _wagons)
        {
            var state = wagon.ProcessState;
            var process = WagonProcessOf(wagon);
            if (state is { WaitingForSpace: true } && process != null && InputsPresent(wagon, state) &&
                TryCompleteWagonProcess(_plan, wagon, process, state))
            {
                wagon.ProcessState = null;
            }
        }
    }

    bool TryCompleteWagonProcess(TimeAdvancePlan _plan, PuzzleInventory _wagon, TrainventoryWagonProcessData _process,
        PuzzleWagonProcessState _state)
    {
        if (!TableManager.TryGetWagonProcessType(_process, out var type) ||
            !definitions.TryGetValue(_process.outputPieceId, out var output))
        {
            return false;
        }
        var inputs = new List<PuzzlePlacement>(_state.Inputs.Count);
        foreach (var input in _state.Inputs)
        {
            if (!_wagon.TryGetPlacement(input.InstanceId, out var placement))
            {
                return false;
            }
            inputs.Add(placement);
        }

        if (type == TrainventoryWagonProcessType.Crush)
        {
            // 분쇄 조각은 원래 화물이 있던 칸에 하나씩 그대로 놓인다.
            var cells = inputs[0].Pose.Cells.Select(_cell => _cell.Position).ToArray();
            return _wagon.TryReplaceWithOutputs(inputs, output, cells.Length, cells, ref _plan.lastInstanceId);
        }
        return _wagon.TryReplaceWithOutputs(inputs, output, checked(_process.outputPerUnit * _state.Units), null,
            ref _plan.lastInstanceId);
    }
}
