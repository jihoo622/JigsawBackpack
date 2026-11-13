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
    MarketClosed,
    BabyCannotButcher
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

internal enum PuzzleFreshnessBand { Empty, Low, Medium, High }

public sealed class PuzzlePlacement
{
    internal (decimal Elapsed, decimal Settled, long SinceSettlement) ShelfLifeAge { get; private set; }

    public int InstanceId { get; }
    public PuzzleItemDefinition Definition { get; }
    public PuzzleInventory Owner { get; }
    public PuzzlePose Pose { get; }
    // 공통 시계의 시너지 정산마다 변화량을 누적한다. 배치 변경만으로는 바뀌지 않는다.
    public decimal ActualValue { get; private set; }
    internal bool HasUnreadSynergySettlement { get; set; }
    // 공간 부족 대기에 새로 진입한 뒤 아직 툴팁으로 확인하지 않았는지. 가치 알림과 독립적으로 둔다.
    internal bool HasUnreadSpaceNotice { get; set; }
    /// <summary>획득 후 보유 시간. 시작 지급·구매 시 0부터 시작하며 -1은 미소유·판매된 물건이다.</summary>
    public long OwnedMinutes { get; private set; }
    public decimal OwnedHours => OwnedMinutes < 0 ? -1 : OwnedMinutes / (decimal)PuzzleTimeManager.MINUTES_PER_HOUR;
    /// <summary>성장에 쌓은 게임 시간의 분. 보유 시간과 따로 가며 대기 중에는 멈춘다. 미소유면 -1이다.</summary>
    internal long GrowthElapsedMinutes { get; set; }
    // 성체가 들어갈 칸이 없어 성장을 대기 중인지. 사본·커밋에서 성장 진행도와 함께 옮긴다.
    internal bool GrowthSpaceBlocked { get; set; }
    // 현재 내구도. 생성 시 정의의 시작값으로 초기화하고, 이동 시 보존한다. 감소는 외부 Manager가 담당한다.
    public int Durability { get; private set; }
    long ShelfLifeMinutes => (long)Definition.ShelfLifeHours * PuzzleTimeManager.MINUTES_PER_HOUR;
    decimal? RemainingShelfLife => Definition.HasDurability && ShelfLifeMinutes > 0
        ? ShelfLifeMinutes - ShelfLifeAge.Elapsed : null;
    internal long? RemainingShelfLifeMinutes => RemainingShelfLife is decimal remaining
        ? (long)decimal.Ceiling(remaining) : null;

    internal int? FreshnessPercent
    {
        get
        {
            if (!Definition.HasDurability)
            {
                return null;
            }
            if (RemainingShelfLife is not decimal remaining)
            {
                return Durability;
            }
            return remaining == 0
                ? 0
                : (int)Math.Max(1, (ShelfLifeMinutes - ShelfLifeAge.Settled) * 100 / ShelfLifeMinutes);
        }
    }
    internal decimal FreshnessAdjustedValue => ApplyFreshnessToValue(ActualValue, FreshnessPercent);

    internal long MinutesUntilShelfLifeExpires(decimal _decayRate)
    {
        return _decayRate > 0 && RemainingShelfLife is decimal remaining && remaining > 0
            ? (long)decimal.Ceiling(remaining / _decayRate)
            : long.MaxValue;
    }

    internal long? MinutesUntilNextFreshnessBand(decimal _decayRate, long _settlementInterval)
    {
        if (RemainingShelfLife is not decimal remaining)
        {
            return null;
        }
        if (remaining <= 0)
        {
            return 0;
        }

        long untilExpiry = MinutesUntilShelfLifeExpires(_decayRate);
        int threshold = FreshnessBand(FreshnessPercent.Value) switch
        {
            PuzzleFreshnessBand.High => BasicData.Int("ui.freshness.mediumThreshold"),
            PuzzleFreshnessBand.Medium => BasicData.Int("ui.freshness.lowThreshold"),
            _ => 0
        };
        if (threshold <= 1)
        {
            return untilExpiry;
        }

        // 등급 경계에 정확히 닿았을 때는 기존 등급을 유지한다.
        decimal ageAtThreshold = ShelfLifeMinutes * (100 - threshold) / 100m;
        long untilSettlement = _settlementInterval - ShelfLifeAge.SinceSettlement;
        decimal ageAtSettlement = ShelfLifeAge.Elapsed + untilSettlement * _decayRate;
        if (ageAtSettlement > ageAtThreshold)
        {
            return Math.Min(untilExpiry, untilSettlement);
        }
        if (_decayRate == 0)
        {
            return untilExpiry;
        }

        decimal extraSettlements = decimal.Floor(
            (ageAtThreshold - ageAtSettlement) / (_settlementInterval * _decayRate)) + 1;
        return (long)Math.Min(untilExpiry, untilSettlement + extraSettlements * _settlementInterval);
    }

    // 성장 완료까지 남은 분. 성장하지 않는 품목과 미소유 물건은 경계가 없다.
    // 대기 중에는 다음 완료 시각이 아니라 자리를 다시 확인하는 재시도 시각을 경계로 제시한다.
    internal long MinutesUntilGrowthCompletion
    {
        get
        {
            if (Definition.GrowthMinutes <= 0 || OwnedMinutes < 0)
            {
                return long.MaxValue;
            }
            return GrowthSpaceBlocked
                ? Definition.GrowthBlockedMinutes
                : Math.Max(0, Definition.GrowthMinutes - GrowthElapsedMinutes);
        }
    }

    // 성체가 들어갈 칸이 없어 성장을 대기한다. 진행은 대기 시작 지점에서 멈추고 시간이 흐르지 않는다.
    internal void WaitForGrowthSpace()
    {
        HasUnreadSpaceNotice |= !GrowthSpaceBlocked;
        GrowthSpaceBlocked = true;
        if (GrowthElapsedMinutes > Definition.GrowthFreezeMinutes)
        {
            GrowthElapsedMinutes = Definition.GrowthFreezeMinutes;
        }
    }

    // 성체 자리를 확보했다. 다음 대기 판정 전까지 성장 시계만 다시 흘려보낸다.
    internal void ResumeGrowth()
    {
        GrowthSpaceBlocked = false;
    }

    internal static PuzzleFreshnessBand FreshnessBand(int _percent)
    {
        if (_percent <= 0)
        {
            return PuzzleFreshnessBand.Empty;
        }
        if (_percent < BasicData.Int("ui.freshness.lowThreshold"))
        {
            return PuzzleFreshnessBand.Low;
        }
        return _percent < BasicData.Int("ui.freshness.mediumThreshold")
            ? PuzzleFreshnessBand.Medium : PuzzleFreshnessBand.High;
    }

    internal static decimal ApplyFreshnessToValue(decimal _value, int? _freshness)
    {
        if (!_freshness.HasValue)
        {
            return _value;
        }
        int percent = FreshnessBand(_freshness.Value) switch
        {
            PuzzleFreshnessBand.Low => BasicData.Int("rules.freshness.lowValuePercent"),
            PuzzleFreshnessBand.Medium => BasicData.Int("rules.freshness.mediumValuePercent"),
            PuzzleFreshnessBand.High => BasicData.Int("rules.freshness.highValuePercent"),
            _ => 0
        };
        return _value * (percent / 100m);
    }

    internal PuzzlePlacement(int _instanceId, PuzzleItemDefinition _definition, PuzzleInventory _owner, PuzzlePose _pose,
        decimal _actualValue, long _ownedMinutes = -1, int _durability = -1,
        (decimal Elapsed, decimal Settled, long SinceSettlement) _shelfLifeAge = default)
    {
        InstanceId = _instanceId;
        Definition = _definition;
        Owner = _owner;
        Pose = _pose;
        ActualValue = _actualValue;
        OwnedMinutes = _ownedMinutes;
        // 새로 세상에 나온 화물은 성장 시계를 0부터 시작하고, 미소유 물건은 성장 추적 자체가 없다.
        GrowthElapsedMinutes = _ownedMinutes < 0 ? -1 : 0;
        Durability = _durability >= 0 ? _durability : _definition.Durability;
        ShelfLifeAge = _shelfLifeAge;
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

    /// <summary>시작 지급·구매 시 보유 시간과 성장 시계를 0부터 시작하고 판매 시 추적을 종료한다.</summary>
    internal void SetOwned(bool _owned)
    {
        OwnedMinutes = _owned ? 0 : -1;
        GrowthElapsedMinutes = _owned ? 0 : -1;
        GrowthSpaceBlocked = false;
        HasUnreadSynergySettlement = false;
        HasUnreadSpaceNotice = false;
    }

    /// <summary>보유 시간·성장 시계·유통기한에 반영할 경과 분을 누적한다.</summary>
    /// <remarks>
    /// 유통기한 소모량 = 경과한 게임 시간 × (1 − 최종 보존율).
    /// 최종 보존율은 0~1로 환산한다. 100% 보존이 유지되는 동안 소모량은 0이며, 남은 유통기한이 그대로 유지된다.
    /// </remarks>
    /// <param name="_minutes">이번 행동으로 경과한 분. 0은 무변화이며 미소유 물건은 무시한다.</param>
    /// <param name="_decayRate">유통기한 소모율. 0이면 소모하지 않는다.</param>
    /// <param name="_settlementInterval">신선도 표시를 갱신하는 경과 분 주기.</param>
    /// <exception cref="ArgumentOutOfRangeException">시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">보유 시간이 long 범위를 초과할 때 발생한다.</exception>
    internal void ApplyElapsedMinutes(long _minutes, decimal _decayRate, long _settlementInterval)
    {
        if (_minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_minutes));
        }
        if (OwnedMinutes >= 0 && _minutes > 0)
        {
            OwnedMinutes = checked(OwnedMinutes + _minutes);
            // 성장 시계는 보유 시간과 따로 간다. 대기 상태를 기록하면 성장만 멈추고 보유 시간은 흐른다.
            if (Definition.GrowthMinutes > 0 && !GrowthSpaceBlocked)
            {
                GrowthElapsedMinutes = checked(GrowthElapsedMinutes + _minutes);
            }
            if (RemainingShelfLife.HasValue)
            {
                var age = ShelfLifeAge;
                long sinceSettlement = (age.SinceSettlement + _minutes % _settlementInterval) % _settlementInterval;
                long lastSettlement = _minutes - sinceSettlement;
                // 냉장 중에도 신선도 표시는 실제 경과 시간의 주기를 따른다.
                decimal settled = lastSettlement > 0
                    ? Math.Min(ShelfLifeMinutes, age.Elapsed + lastSettlement * _decayRate)
                    : age.Settled;
                ShelfLifeAge = (Math.Min(ShelfLifeMinutes, age.Elapsed + _minutes * _decayRate), settled, sinceSettlement);
            }
        }
    }

    internal void ApplySynergySettlement(decimal _amount)
    {
        decimal previousValue = ActualValue;
        ActualValue += _amount;
        HasUnreadSynergySettlement |= ActualValue != previousValue;
    }

    internal void CopyTimeState(PuzzlePlacement _staged)
    {
        ActualValue = _staged.ActualValue;
        OwnedMinutes = _staged.OwnedMinutes;
        GrowthElapsedMinutes = _staged.GrowthElapsedMinutes;
        GrowthSpaceBlocked = _staged.GrowthSpaceBlocked;
        ShelfLifeAge = _staged.ShelfLifeAge;
        HasUnreadSynergySettlement = _staged.HasUnreadSynergySettlement;
        HasUnreadSpaceNotice = _staged.HasUnreadSpaceNotice;
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

