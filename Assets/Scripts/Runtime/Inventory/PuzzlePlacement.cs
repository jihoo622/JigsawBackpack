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
    CellOccupied,
    NoSpace,
    MarketClosed
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
    // 공통 시계의 시너지 정산마다 변화량을 누적한다. 배치 변경만으로는 바뀌지 않는다.
    public decimal ActualValue { get; private set; }
    /// <summary>획득 후 보유 시간. 시작 지급·구매 시 0부터 시작하며 -1은 미소유·판매된 물건이다.</summary>
    public long OwnedMinutes { get; private set; }
    public decimal OwnedHours => OwnedMinutes < 0 ? -1 : OwnedMinutes / (decimal)PuzzleTimeManager.MINUTES_PER_HOUR;
    // 현재 내구도. 생성 시 정의의 시작값으로 초기화하고, 이동 시 보존한다. 감소는 외부 Manager가 담당한다.
    public int Durability { get; private set; }

    internal PuzzlePlacement(int _instanceId, PuzzleItemDefinition _definition, PuzzleInventory _owner, PuzzlePose _pose,
        decimal _actualValue, long _ownedMinutes = -1, int _durability = -1)
    {
        InstanceId = _instanceId;
        Definition = _definition;
        Owner = _owner;
        Pose = _pose;
        ActualValue = _actualValue;
        OwnedMinutes = _ownedMinutes;
        Durability = _durability >= 0 ? _durability : _definition.Durability;
    }

    /// <summary>현재 내구도를 깎는 연결 지점. 0 이하로 내려가지 않으며, 아직 호출자는 없다.</summary>
    internal void ReduceDurability(int _amount)
    {
        if (_amount <= 0)
        {
            return;
        }
        Durability = Math.Max(0, Durability - _amount);
    }

    /// <summary>시작 지급·구매 시 보유 시간을 0으로 시작하고 판매 시 추적을 종료한다.</summary>
    internal void SetOwned(bool _owned)
    {
        OwnedMinutes = _owned ? 0 : -1;
    }

    /// <summary>획득 이후 경과 분을 누적한다. 부패·내구도 상태는 변경하지 않는다.</summary>
    /// <param name="_minutes">이번 행동으로 경과한 분. 0은 무변화이며 미소유 물건은 무시한다.</param>
    /// <exception cref="ArgumentOutOfRangeException">시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">보유 시간이 long 범위를 초과할 때 발생한다.</exception>
    internal void ApplyElapsedMinutes(long _minutes)
    {
        if (_minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_minutes));
        }
        if (OwnedMinutes >= 0 && _minutes > 0)
        {
            OwnedMinutes = checked(OwnedMinutes + _minutes);
        }
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

