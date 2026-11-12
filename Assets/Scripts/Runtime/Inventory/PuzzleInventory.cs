using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PuzzleInventory
{
    readonly IReadOnlyDictionary<string, PuzzleItemDefinition> definitions;
    readonly IReadOnlyList<PuzzleSynergyRuleData> synergyRules;
    readonly PuzzleInventoryValuation valuation = new();
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

    internal void RecalculateValue()
    {
        valuation.Recalculate(this, synergyRules);
    }

    // 현재 인접 배치의 1회 변화량을 정산 횟수만큼 적용한다.
    internal bool CanApplySynergyActivations(long _count)
    {
        RecalculateValue();
        try
        {
            foreach (var placement in placements.Values)
            {
                var value = valuation.ValueOf(placement.InstanceId);
                _ = checked(placement.ActualValue + (value.FinalValue - value.BaseValue) * _count);
            }
        }
        catch (OverflowException)
        {
            return false;
        }
        return true;
    }

    internal void ApplySynergyActivations(long _count)
    {
        RecalculateValue();
        foreach (var placement in placements.Values)
        {
            var value = valuation.ValueOf(placement.InstanceId);
            placement.ApplySynergySettlement((value.FinalValue - value.BaseValue) * _count);
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
                _original.ActualValue, _original.OwnedMinutes, _original.Durability)
            {
                HasUnreadSynergySettlement = _original.HasUnreadSynergySettlement
            };
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
        _placement = null;
        var rotation = new PuzzleRotation(_definition.Cells, 0);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!TryCreatePose(_definition.Cells, new Vector2Int(x, y), rotation, out var pose))
                {
                    continue;
                }

                bool free = true;
                foreach (var cell in pose.Cells)
                {
                    if (!InBounds(cell.Position) || occupancy.ContainsKey(cell.Position))
                    {
                        free = false;
                        break;
                    }
                }

                if (!free)
                {
                    continue;
                }

                var placement = new PuzzlePlacement(_instanceId, _definition, this, pose, _definition.BaseValue, 0);
                StorePlacement(placement);
                RecalculateValue();
                _placement = placement;
                return true;
            }
        }

        return false;
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
