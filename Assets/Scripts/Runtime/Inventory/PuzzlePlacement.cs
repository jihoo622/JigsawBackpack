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

public readonly struct PuzzleRotation
{
    public const int TURN_COUNT = 4;

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
    // 소모한 유통기한 시간과 아직 1시간에 못 미친 소모율 누적(%). 보존 효과로 소모율이 100% 미만이면 누적이 100이 될 때마다 1시간 준다.
    internal (long Elapsed, int CarryPercent) ShelfLifeAge { get; private set; }

    public int InstanceId { get; }
    public PuzzleItemDefinition Definition { get; }
    public PuzzleInventory Owner { get; }
    public PuzzlePose Pose { get; }
    // 공통 시계의 시너지 정산마다 변화량을 누적한다. 배치 변경만으로는 바뀌지 않는다.
    public decimal ActualValue { get; private set; }
    internal bool HasUnreadSynergySettlement { get; set; }
    // 공간 부족 대기에 새로 진입한 뒤 아직 툴팁으로 확인하지 않았는지. 가치 알림과 독립적으로 둔다.
    internal bool HasUnreadSpaceNotice { get; set; }
    // 번식·배설로 새로 생성된 뒤 아직 툴팁으로 확인하지 않았는지.
    internal bool HasUnreadSpawnNotice { get; set; }
    internal bool HasUnreadNotice => HasUnreadSynergySettlement || HasUnreadSpaceNotice || HasUnreadSpawnNotice;
    /// <summary>획득 후 보유 시간. 시작 지급·구매 시 0부터 시작하며 -1은 미소유·판매된 물건이다.</summary>
    public long OwnedHours { get; private set; }
    /// <summary>성장에 쌓은 게임 시간의 시간. 보유 시간과 따로 가며 대기 중에는 멈춘다. 미소유면 -1이다.</summary>
    internal long GrowthElapsedHours { get; set; }
    // 성체가 들어갈 칸이 없어 성장을 대기 중인지. 사본·커밋에서 성장 진행도와 함께 옮긴다.
    internal bool GrowthSpaceBlocked { get; set; }
    // 현재 내구도. 생성 시 정의의 시작값으로 초기화하고, 이동 시 보존한다. 감소는 외부 Manager가 담당한다.
    public int Durability { get; private set; }
    long ShelfLifeHours => Definition.ShelfLifeHours;
    // 남은 유통기한 시간. 유통기한이 없는 품목은 null이다.
    internal long? RemainingShelfLifeHours => Definition.HasDurability && ShelfLifeHours > 0
        ? ShelfLifeHours - ShelfLifeAge.Elapsed : null;

    internal int? FreshnessPercent
    {
        get
        {
            if (!Definition.HasDurability)
            {
                return null;
            }
            if (RemainingShelfLifeHours is not long remaining)
            {
                return Durability;
            }
            return remaining <= 0 ? 0 : (int)Math.Max(1, remaining * 100 / ShelfLifeHours);
        }
    }
    internal decimal FreshnessAdjustedValue => ApplyFreshnessToValue(ActualValue, FreshnessPercent);

    // 보존율을 반영한 시간당 유통기한 소모율(%). 0이면 소모하지 않는다.
    internal static int DecayPercent(decimal _decayRate)
    {
        return (int)Math.Max(0, Math.Round(_decayRate * 100m, MidpointRounding.AwayFromZero));
    }

    // 유통기한을 모두 소모할 때까지 남은 시간. 소모하지 않거나 이미 만료됐으면 경계가 없다.
    internal long HoursUntilShelfLifeExpires(decimal _decayRate)
    {
        int percent = DecayPercent(_decayRate);
        if (percent <= 0 || RemainingShelfLifeHours is not long remaining || remaining <= 0)
        {
            return long.MaxValue;
        }
        long needed = remaining * 100 - ShelfLifeAge.CarryPercent;
        return (needed + percent - 1) / percent;
    }

    // 신선도 등급(가치 배율)이 바뀌거나 만료될 때까지 남은 시간. 유통기한이 없으면 null이다.
    internal long? HoursUntilNextFreshnessBand(decimal _decayRate)
    {
        if (RemainingShelfLifeHours is not long remaining)
        {
            return null;
        }
        if (remaining <= 0)
        {
            return 0;
        }
        int percent = DecayPercent(_decayRate);
        long untilExpiry = HoursUntilShelfLifeExpires(_decayRate);
        if (percent <= 0)
        {
            return untilExpiry;
        }
        var band = FreshnessBand(FreshnessPercent.Value);
        // 유통기한은 최대 수백 시간이라 시간마다 등급을 확인해도 충분하다.
        for (long hours = 1; hours < untilExpiry; hours++)
        {
            long elapsed = ShelfLifeAge.Elapsed + (ShelfLifeAge.CarryPercent + hours * percent) / 100;
            long left = ShelfLifeHours - elapsed;
            int freshness = left <= 0 ? 0 : (int)Math.Max(1, left * 100 / ShelfLifeHours);
            if (FreshnessBand(freshness) != band)
            {
                return hours;
            }
        }
        return untilExpiry;
    }

    // 성장 완료까지 남은 시간. 성장하지 않는 품목과 미소유 물건은 경계가 없다.
    // 대기 중에는 다음 완료 시각이 아니라 자리를 다시 확인하는 재시도 시각을 경계로 제시한다.
    internal long HoursUntilGrowthCompletion
    {
        get
        {
            if (Definition.GrowthHours <= 0 || OwnedHours < 0)
            {
                return long.MaxValue;
            }
            return GrowthSpaceBlocked
                ? Definition.GrowthBlockedHours
                : Math.Max(0, Definition.GrowthHours - GrowthElapsedHours);
        }
    }

    // 성체가 들어갈 칸이 없어 성장을 대기한다. 진행은 대기 시작 지점에서 멈추고 시간이 흐르지 않는다.
    internal void WaitForGrowthSpace()
    {
        if (!GrowthSpaceBlocked)
        {
            HasUnreadSpaceNotice = true;
            Owner.HasUnreadEventNotice = true;
        }
        GrowthSpaceBlocked = true;
        if (GrowthElapsedHours > Definition.GrowthFreezeHours)
        {
            GrowthElapsedHours = Definition.GrowthFreezeHours;
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
        if (_percent < BasicData.Int("rules.freshness.lowThreshold"))
        {
            return PuzzleFreshnessBand.Low;
        }
        return _percent < BasicData.Int("rules.freshness.mediumThreshold")
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
        decimal _actualValue, long _ownedHours = -1, int _durability = -1,
        (long Elapsed, int CarryPercent) _shelfLifeAge = default)
    {
        InstanceId = _instanceId;
        Definition = _definition;
        Owner = _owner;
        Pose = _pose;
        ActualValue = _actualValue;
        OwnedHours = _ownedHours;
        // 새로 세상에 나온 화물은 성장 시계를 0부터 시작하고, 미소유 물건은 성장 추적 자체가 없다.
        GrowthElapsedHours = _ownedHours < 0 ? -1 : 0;
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
        OwnedHours = _owned ? 0 : -1;
        GrowthElapsedHours = _owned ? 0 : -1;
        GrowthSpaceBlocked = false;
        HasUnreadSynergySettlement = false;
        HasUnreadSpaceNotice = false;
        HasUnreadSpawnNotice = false;
    }

    /// <summary>보유 시간·성장 시계·유통기한에 반영할 경과 시간을 누적한다.</summary>
    /// <remarks>
    /// 유통기한은 정수 시간으로 소모한다. 시간마다 소모율(%)을 누적하고 100이 찰 때마다 1시간 줄인다.
    /// 소모율 100%면 시간당 1시간, 냉장 50%면 2시간마다 1시간이며 0%면 줄지 않는다.
    /// </remarks>
    /// <param name="_hours">이번 행동으로 경과한 시간. 0은 무변화이며 미소유 물건은 무시한다.</param>
    /// <param name="_decayRate">유통기한 소모율. 0이면 소모하지 않는다.</param>
    /// <exception cref="ArgumentOutOfRangeException">시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">보유 시간이 long 범위를 초과할 때 발생한다.</exception>
    internal void ApplyElapsedHours(long _hours, decimal _decayRate)
    {
        if (_hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_hours));
        }
        if (OwnedHours >= 0 && _hours > 0)
        {
            OwnedHours = checked(OwnedHours + _hours);
            // 성장 시계는 보유 시간과 따로 간다. 대기 상태를 기록하면 성장만 멈추고 보유 시간은 흐른다.
            if (Definition.GrowthHours > 0 && !GrowthSpaceBlocked)
            {
                GrowthElapsedHours = checked(GrowthElapsedHours + _hours);
            }
            if (RemainingShelfLifeHours.HasValue)
            {
                decimal valueBefore = FreshnessAdjustedValue;
                var age = ShelfLifeAge;
                long total = checked(age.CarryPercent + _hours * DecayPercent(_decayRate));
                long elapsed = Math.Min(ShelfLifeHours, age.Elapsed + total / 100);
                ShelfLifeAge = (elapsed, elapsed >= ShelfLifeHours ? 0 : (int)(total % 100));
                HasUnreadSynergySettlement |= FreshnessAdjustedValue != valueBefore;
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
        OwnedHours = _staged.OwnedHours;
        GrowthElapsedHours = _staged.GrowthElapsedHours;
        GrowthSpaceBlocked = _staged.GrowthSpaceBlocked;
        ShelfLifeAge = _staged.ShelfLifeAge;
        HasUnreadSynergySettlement = _staged.HasUnreadSynergySettlement;
        HasUnreadSpaceNotice = _staged.HasUnreadSpaceNotice;
        HasUnreadSpawnNotice = _staged.HasUnreadSpawnNotice;
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

