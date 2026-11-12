using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PuzzleInventory
{
    readonly IReadOnlyDictionary<string, PuzzleItemDefinition> definitions;
    readonly IReadOnlyList<PuzzleSynergyRuleData> synergyRules;
    PuzzleInventoryValuation valuation = new();
    readonly SortedDictionary<int, PuzzlePlacement> placements = new();
    readonly Dictionary<Vector2Int, (int InstanceId, PuzzleCell Cell)> occupancy = new();

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyCollection<PuzzlePlacement> Placements => placements.Values;
    internal PuzzleValue Value => valuation.Total;

    internal PuzzleInventory(PuzzleBoardData _board, IReadOnlyList<PuzzleStartingPlacementData> _startingPlacements,
        IReadOnlyDictionary<string, PuzzleItemDefinition> _definitions, IReadOnlyList<PuzzleSynergyRuleData> _synergyRules,
        ref int _lastInstanceId)
    {
        Width = _board.width;
        Height = _board.height;
        definitions = _definitions;
        synergyRules = _synergyRules;
        foreach (var placement in _startingPlacements)
        {
            var definition = definitions[placement.pieceId];
            var rotation = new PuzzleRotation(definition.Cells, placement.clockwiseTurns);
            var origin = new Vector2Int(placement.x, placement.y);
            if (!TryCreatePose(definition.Cells, origin, rotation, out var pose))
            {
                throw new InvalidOperationException($"'{placement.id}' 시작 배치의 좌표를 만들 수 없습니다.");
            }

            StorePlacement(new PuzzlePlacement(checked(++_lastInstanceId), definition, this, pose, definition.BaseValue));
        }
        RecalculateValue();
    }

    internal PuzzleValue ValueOf(int _instanceId)
    {
        return valuation.ValueOf(_instanceId);
    }

    internal decimal ActualValueOf(int _instanceId)
    {
        return placements[_instanceId].ActualValue;
    }

    internal PuzzleInventory CopyForTimeAdvance()
    {
        var copy = new PuzzleInventory(this);
        foreach (var placement in placements.Values)
        {
            copy.StorePlacement(copy.CopyPlacement(placement));
        }
        copy.RecalculateValue();
        return copy;
    }

    PuzzleInventory(PuzzleInventory _source)
    {
        Width = _source.Width;
        Height = _source.Height;
        definitions = _source.definitions;
        synergyRules = _source.synergyRules;
    }

    internal void CommitTimeAdvance(PuzzleInventory _staged)
    {
        occupancy.Clear();
        foreach (var staged in _staged.placements.Values)
        {
            if (placements.TryGetValue(staged.InstanceId, out var existing) && existing.Pose == staged.Pose &&
                existing.Definition == staged.Definition)
            {
                existing.CopyTimeState(staged);
                StorePlacement(existing);
            }
            else
            {
                StorePlacement(CopyPlacement(staged));
            }
        }
        valuation = _staged.valuation;
    }

    PuzzlePlacement CopyPlacement(PuzzlePlacement _placement)
    {
        return new PuzzlePlacement(_placement.InstanceId, _placement.Definition, this, _placement.Pose,
            _placement.ActualValue, _placement.OwnedMinutes, _placement.Durability);
    }

    internal void RecalculateValue()
    {
        valuation.Recalculate(this, synergyRules);
    }

    // 현재 배치의 시너지 변화량을 정산 횟수만큼 적용한다.
    internal void ApplySynergyActivations(long _count)
    {
        if (_count == 0)
        {
            return;
        }

        foreach (var placement in placements.Values)
        {
            var value = valuation.ValueOf(placement.InstanceId);
            placement.AddActualValue((value.FinalValue - value.BaseValue) * _count);
        }
    }

    public bool TryGetCell(Vector2Int _position, out PuzzlePlacement _placement, out PuzzleCell _cell)
    {
        if (occupancy.TryGetValue(_position, out var occupant))
        {
            _placement = placements[occupant.InstanceId];
            _cell = occupant.Cell;
            return true;
        }

        _placement = null;
        _cell = default;
        return false;
    }

    // 이 보드가 지금 점유한 칸 수. 노선 이동 비용의 보유량 계산에 쓴다.
    internal int OccupiedCells => occupancy.Count;

    internal bool TryPlanGrowth(long _minutes, List<PuzzleGrowth> _growth)
    {
        var approachingGrowth = placements.Values
            .Where(_item => _item.OwnedMinutes >= 0 && _item.Definition.GrowthMinutes > 0 &&
                _minutes > _item.Definition.GrowthMinutes - _item.Definition.GrowthBlockedMinutes - _item.OwnedMinutes)
            .OrderBy(_item => _item.Definition.GrowthMinutes - _item.OwnedMinutes);
        HashSet<Vector2Int> occupied = null;
        foreach (var item in approachingGrowth)
        {
            occupied ??= new HashSet<Vector2Int>(occupancy.Keys);
            occupied.ExceptWith(item.Pose.Cells.Select(_cell => _cell.Position));
            var adult = definitions[item.Definition.AdultPieceId];
            if (!TryFindGrowthPose(item.Pose, adult, occupied, out var pose))
            {
                return false;
            }

            occupied.UnionWith(pose.Cells.Select(_cell => _cell.Position));
            if (_minutes >= item.Definition.GrowthMinutes - item.OwnedMinutes)
            {
                _growth.Add(new PuzzleGrowth(item, adult, pose));
            }
        }

        return true;
    }

    bool TryFindGrowthPose(PuzzlePose _current, PuzzleItemDefinition _adult, HashSet<Vector2Int> _occupied,
        out PuzzlePose _pose)
    {
        var currentRotation = new PuzzleRotation(_adult.Cells, _current.ClockwiseTurns);
        if (TryCreateGrowthPose(_adult.Cells, _current.Origin, currentRotation, _occupied, out _pose))
        {
            return true;
        }

        for (int turn = 0; turn < PuzzleRotation.TURN_COUNT; turn++)
        {
            var rotation = new PuzzleRotation(_adult.Cells, (_current.ClockwiseTurns + turn) % PuzzleRotation.TURN_COUNT);
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (TryCreateGrowthPose(_adult.Cells, new Vector2Int(x, y), rotation, _occupied, out _pose))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    bool TryCreateGrowthPose(IReadOnlyList<Vector2Int> _cells, Vector2Int _origin, PuzzleRotation _rotation,
        HashSet<Vector2Int> _occupied, out PuzzlePose _pose)
    {
        return TryCreatePose(_cells, _origin, _rotation, out _pose) &&
            _pose.Cells.All(_cell => InBounds(_cell.Position) && !_occupied.Contains(_cell.Position));
    }

    internal void ApplyGrowth(PuzzleGrowth _growth)
    {
        var item = _growth.Item;
        var adult = _growth.Adult;
        var grown = new PuzzlePlacement(item.InstanceId, adult, this, _growth.Pose,
            item.ActualValue + (adult.BaseValue - (decimal)item.Definition.BaseValue), item.OwnedMinutes);
        ClearOccupancy(item);
        StorePlacement(grown);
        RecalculateValue();
    }

    internal PuzzlePlacementResult EvaluateMove(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (_original == null || !placements.TryGetValue(_original.InstanceId, out var current) ||
            !ReferenceEquals(current, _original))
        {
            return new PuzzlePlacementResult(PuzzlePlacementFailure.StaleSource);
        }

        bool sameBoard = ReferenceEquals(this, _destination);
        if (_destination == null || !ReferenceEquals(definitions, _destination.definitions) ||
            (!sameBoard && _destination.placements.ContainsKey(_original.InstanceId)))
        {
            return new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination);
        }

        var failure = _destination.CreatePoseAtSlot(_original.Definition.Cells, _grabbedCell, _targetCell, _clockwiseTurns,
            out var candidate);
        if (failure != PuzzlePlacementFailure.None)
        {
            return new PuzzlePlacementResult(failure);
        }

        int? ignoredInstanceId = sameBoard ? _original.InstanceId : null;
        return new PuzzlePlacementResult(_destination.CheckPlacement(candidate.Cells, ignoredInstanceId), candidate);
    }

    internal PuzzlePlacementResult Move(PuzzlePlacement _original, PuzzleInventory _destination, Vector2Int _grabbedCell,
        Vector2Int _targetCell, int _clockwiseTurns)
    {
        var result = EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (result.CanPlace)
        {
            // 원본과 대상의 현재 점유를 확인한 뒤 같은 호출에서 소속과 점유를 함께 바꾼다.
            var placement = new PuzzlePlacement(_original.InstanceId, _original.Definition, _destination, result.Candidate,
                _original.ActualValue, _original.OwnedMinutes, _original.Durability);
            ClearOccupancy(_original);
            placements.Remove(_original.InstanceId);
            _destination.StorePlacement(placement);
            RecalculateValue();
            if (!ReferenceEquals(this, _destination))
            {
                _destination.RecalculateValue();
            }
        }

        return result;
    }

    // 비어 있는 첫 칸에 조각을 새로 올려 배치한다. 마땅한 빈 칸이 없으면 false.
    internal bool TryAutoPlace(PuzzleItemDefinition _definition, int _instanceId, out PuzzlePlacement _placement)
    {
        if (!TryFindAutoPlacement(_definition, out var pose))
        {
            _placement = null;
            return false;
        }

        _placement = PlaceSpawnedItem(_definition, _instanceId, pose);
        return true;
    }

    internal bool TryAutoPlace(PuzzleItemDefinition _definition, ref int _lastInstanceId, bool _allowRotation = false)
    {
        if (!TryFindAutoPlacement(_definition, out var pose, _allowRotation))
        {
            return false;
        }

        PlaceSpawnedItem(_definition, checked(++_lastInstanceId), pose);
        return true;
    }

    internal bool CanAutoPlace(PuzzleItemDefinition _definition, bool _allowRotation = false)
    {
        return TryFindAutoPlacement(_definition, out _, _allowRotation);
    }

    PuzzlePlacement PlaceSpawnedItem(PuzzleItemDefinition _definition, int _instanceId, PuzzlePose _pose)
    {
        var placement = new PuzzlePlacement(_instanceId, _definition, this, _pose, _definition.BaseValue, 0);
        StorePlacement(placement);
        RecalculateValue();
        return placement;
    }

    bool TryFindAutoPlacement(PuzzleItemDefinition _definition, out PuzzlePose _pose, bool _allowRotation = false)
    {
        int turns = _allowRotation ? PuzzleRotation.TURN_COUNT : 1;
        for (int turn = 0; turn < turns; turn++)
        {
            var rotation = new PuzzleRotation(_definition.Cells, turn);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (TryCreatePose(_definition.Cells, new Vector2Int(x, y), rotation, out _pose) &&
                        CheckPlacement(_pose.Cells, null) == PuzzlePlacementFailure.None)
                    {
                        return true;
                    }
                }
            }
        }

        _pose = null;
        return false;
    }

    // 이 보드에서 배치 하나를 완전히 제거한다. 도축처럼 소멸시키는 행동만 사용하고, 반환값은 소속이 맞는 경우에만 true다.
    internal bool Remove(PuzzlePlacement _placement)
    {
        if (_placement == null || !placements.TryGetValue(_placement.InstanceId, out var current) ||
            !ReferenceEquals(current, _placement))
        {
            return false;
        }

        ClearOccupancy(_placement);
        placements.Remove(_placement.InstanceId);
        RecalculateValue();
        return true;
    }

    internal PuzzlePose GetDragPose(PuzzleItemDefinition _definition, Vector2Int _grabbedCell, int _clockwiseTurns)
    {
        // 보드 밖의 고스트는 잡은 슬롯을 (0, 0)에 둔 포즈에서 상대 위치를 읽는다.
        CreatePoseAtSlot(_definition.Cells, _grabbedCell, Vector2Int.zero, _clockwiseTurns, out var pose);
        return pose;
    }

    PuzzlePlacementFailure CreatePoseAtSlot(IReadOnlyList<Vector2Int> _shape, Vector2Int _grabbedCell, Vector2Int _targetCell,
        int _clockwiseTurns, out PuzzlePose _pose)
    {
        _pose = null;
        if (_clockwiseTurns < 0 || _clockwiseTurns >= PuzzleRotation.TURN_COUNT)
        {
            return PuzzlePlacementFailure.InvalidRotation;
        }
        if (!_shape.Contains(_grabbedCell))
        {
            return PuzzlePlacementFailure.InvalidGrabbedCell;
        }

        var rotation = new PuzzleRotation(_shape, _clockwiseTurns);
        var grabbedOffset = rotation.GetBoardOffset(_grabbedCell);
        if (!InBounds(_targetCell) || !TryTranslate(_targetCell, -grabbedOffset, out var origin) ||
            !TryCreatePose(_shape, origin, rotation, out _pose))
        {
            return PuzzlePlacementFailure.CellOutsideBoard;
        }

        return PuzzlePlacementFailure.None;
    }

    static bool TryCreatePose(IReadOnlyList<Vector2Int> _shape, Vector2Int _origin,
        PuzzleRotation _rotation, out PuzzlePose _pose)
    {
        _pose = null;
        var cells = new PuzzleCell[_shape.Count];
        for (int i = 0; i < _shape.Count; i++)
        {
            var original = _shape[i];
            if (!TryTranslate(_origin, _rotation.GetBoardOffset(original), out var position))
            {
                return false;
            }

            cells[i] = new PuzzleCell(original, position);
        }

        _pose = new PuzzlePose(_origin, _rotation.ClockwiseTurns, cells);
        return true;
    }

    PuzzlePlacementFailure CheckPlacement(IReadOnlyList<PuzzleCell> _cells, int? _ignoredInstanceId)
    {
        foreach (var cell in _cells)
        {
            if (!InBounds(cell.Position))
            {
                return PuzzlePlacementFailure.CellOutsideBoard;
            }
        }

        foreach (var cell in _cells)
        {
            if (occupancy.TryGetValue(cell.Position, out var occupant) && occupant.InstanceId != _ignoredInstanceId)
            {
                return PuzzlePlacementFailure.CellOccupied;
            }
        }

        return PuzzlePlacementFailure.None;
    }

    void StorePlacement(PuzzlePlacement _placement)
    {
        placements[_placement.InstanceId] = _placement;
        foreach (var cell in _placement.Pose.Cells)
        {
            occupancy.Add(cell.Position, (_placement.InstanceId, cell));
        }
    }

    void ClearOccupancy(PuzzlePlacement _placement)
    {
        foreach (var cell in _placement.Pose.Cells)
        {
            occupancy.Remove(cell.Position);
        }
    }

    bool InBounds(Vector2Int _position)
    {
        return _position.x >= 0 && _position.x < Width && _position.y >= 0 && _position.y < Height;
    }

    static bool TryTranslate(Vector2Int _origin, Vector2Int _offset, out Vector2Int _position)
    {
        long x = (long)_origin.x + _offset.x;
        long y = (long)_origin.y + _offset.y;
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
        {
            _position = default;
            return false;
        }

        _position = new Vector2Int((int)x, (int)y);
        return true;
    }
}

internal readonly struct PuzzleGrowth
{
    internal PuzzlePlacement Item { get; }
    internal PuzzleItemDefinition Adult { get; }
    internal PuzzlePose Pose { get; }

    internal PuzzleGrowth(PuzzlePlacement _item, PuzzleItemDefinition _adult, PuzzlePose _pose)
    {
        Item = _item;
        Adult = _adult;
        Pose = _pose;
    }
}
