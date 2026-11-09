using System;
using System.Collections.Generic;
using UnityEngine;

public enum PuzzlePlacementFailure
{
    None,
    StaleSource,
    InvalidDestination,
    InvalidGrabbedCell,
    InvalidRotation,
    CellOutsideBoard,
    CellOccupied
}

public readonly struct PuzzleCell
{
    // 회전 전 조각 로컬 좌표. 오른쪽과 아래로 증가한다.
    public Vector2Int OriginalCell { get; }
    // 배치 포즈 안의 보드 좌표. 오른쪽과 위로 증가한다.
    public Vector2Int Position { get; }
    internal PuzzleCell(Vector2Int _originalCell, Vector2Int _position)
    {
        OriginalCell = _originalCell;
        Position = _position;
    }
}

internal readonly struct PuzzleRotation
{
    internal const int TURN_COUNT = 4;

    readonly Vector2Int minimum;

    public int ClockwiseTurns { get; }

    public PuzzleRotation(IReadOnlyList<Vector2Int> _definition, int _clockwiseTurns)
    {
        ClockwiseTurns = _clockwiseTurns;
        var rotatedMinimum = RotateLocal(_definition[0], _clockwiseTurns);
        for (int i = 1; i < _definition.Count; i++)
        {
            var rotated = RotateLocal(_definition[i], _clockwiseTurns);
            rotatedMinimum = Vector2Int.Min(rotatedMinimum, rotated);
        }
        minimum = rotatedMinimum;
    }

    public Vector2Int GetBoardOffset(Vector2Int _originalCell)
    {
        var normalized = RotateLocal(_originalCell, ClockwiseTurns) - minimum;
        return new Vector2Int(normalized.x, -normalized.y);
    }

    static Vector2Int RotateLocal(Vector2Int _cell, int _clockwiseTurns)
    {
        return _clockwiseTurns switch
        {
            0 => _cell,
            1 => new Vector2Int(-_cell.y, _cell.x),
            2 => new Vector2Int(-_cell.x, -_cell.y),
            3 => new Vector2Int(_cell.y, -_cell.x),
            _ => throw new ArgumentOutOfRangeException(nameof(_clockwiseTurns))
        };
    }
}

public sealed class PuzzlePose
{
    // 회전 후 외접 사각형의 왼쪽 위 기준 셀에 대응하는 보드 좌표.
    public Vector2Int Origin { get; }
    public int ClockwiseTurns { get; }
    public IReadOnlyList<PuzzleCell> Cells { get; }

    internal PuzzlePose(Vector2Int _origin, int _clockwiseTurns, PuzzleCell[] _cells)
    {
        Origin = _origin;
        ClockwiseTurns = _clockwiseTurns;
        Cells = Array.AsReadOnly(_cells);
    }
}

public sealed class PuzzlePlacement
{
    public int InstanceId { get; }
    public PuzzleItemDefinition Definition { get; }
    public PuzzleInventory Owner { get; }
    public PuzzlePose Pose { get; }
    // 도시 이동 턴마다 기존 시너지 변화량을 누적한 값. 인접 배치 변경만으로는 바뀌지 않는다.
    public decimal ActualValue { get; private set; }

    internal PuzzlePlacement(int _instanceId, PuzzleItemDefinition _definition, PuzzleInventory _owner, PuzzlePose _pose,
        decimal _actualValue)
    {
        InstanceId = _instanceId;
        Definition = _definition;
        Owner = _owner;
        Pose = _pose;
        ActualValue = _actualValue;
    }

    internal void AddActualValue(decimal _amount)
    {
        ActualValue += _amount;
    }
}

public readonly struct PuzzlePlacementResult
{
    public PuzzlePlacementFailure Failure { get; }
    // 후보 좌표를 산출할 수 있으면 거부된 위치도 미리보기에 사용한다.
    public PuzzlePose Candidate { get; }
    public bool CanPlace => Failure == PuzzlePlacementFailure.None && Candidate != null;

    internal PuzzlePlacementResult(PuzzlePlacementFailure _failure, PuzzlePose _candidate = null)
    {
        Failure = _failure;
        Candidate = _candidate;
    }
}

