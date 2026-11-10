using System;

/// <summary>
/// PuzzleInventoryService가 세션별로 소유하는 정수 시간 단위 게임 시계다.
/// 현실 시간·분석 로그와 독립적이며 확정된 행동으로만 진행한다.
/// GameObject 컴포넌트가 아니며 이벤트·승패 판정은 소유 세션이 처리한다.
/// </summary>
public sealed class PuzzleTimeManager
{
    /// <summary>하루를 구성하는 게임 시간.</summary>
    public const int HOURS_PER_DAY = 24;
    /// <summary>노선 시간 가중치 1에 해당하는 시간. 가까운 노선 3회가 하루다.</summary>
    public const int HOURS_PER_TRAVEL_WEIGHT = 8;
    /// <summary>기본 상환 기한인 14일을 시간으로 환산한 값.</summary>
    public const int DEFAULT_DEADLINE_HOURS = 14 * HOURS_PER_DAY;

    /// <summary>시작 이후 누적 경과 시간. 외부에서는 읽기만 가능하다.</summary>
    public long ElapsedHours { get; private set; }
    /// <summary>시작 기준 상환 기한(시간). 0은 무기한이며 생성 후 변경하지 않는다.</summary>
    public long DeadlineHours { get; }
    /// <summary>시간 제한이 설정됐는지 여부.</summary>
    public bool HasDeadline => DeadlineHours > 0;
    /// <summary>남은 정수 시간. 초과 시 0, 무기한은 long.MaxValue이므로 HasDeadline을 먼저 확인한다.</summary>
    public long RemainingHours => HasDeadline ? Math.Max(0, DeadlineHours - ElapsedHours) : long.MaxValue;
    /// <summary>기한 초과 여부. 정확히 기한과 같은 시각 및 무기한에서는 false다.</summary>
    public bool IsOverdue => HasDeadline && ElapsedHours > DeadlineHours;
    /// <summary>완전히 경과한 일수. 시작은 Day 0, 24시간부터 Day 1이다.</summary>
    public long Day => ElapsedHours / HOURS_PER_DAY;
    /// <summary>현재 날짜 안의 시각(0~23시).</summary>
    public int Hour => (int)(ElapsedHours % HOURS_PER_DAY);

    /// <summary>경과 0시간의 시계를 생성한다. 기본 기한은 14일이다.</summary>
    /// <param name="_deadlineHours">시작 기준 기한(시간). 0이면 무기한.</param>
    /// <exception cref="ArgumentOutOfRangeException">기한이 음수일 때 발생한다.</exception>
    public PuzzleTimeManager(long _deadlineHours = DEFAULT_DEADLINE_HOURS)
    {
        if (_deadlineHours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_deadlineHours));
        }
        DeadlineHours = _deadlineHours;
    }

    /// <summary>노선 시간 가중치를 시간으로 변환한다. 거리·비용·테마는 참조하지 않는다.</summary>
    /// <param name="_weight">1 이상의 가중치. 1은 8시간, 6은 48시간.</param>
    /// <returns>소요 정수 시간.</returns>
    /// <exception cref="ArgumentOutOfRangeException">가중치가 1 미만일 때 발생한다.</exception>
    /// <exception cref="OverflowException">변환 결과가 long 범위를 초과할 때 발생한다.</exception>
    public static long TravelHours(long _weight)
    {
        if (_weight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(_weight));
        }
        return checked(_weight * HOURS_PER_TRAVEL_WEIGHT);
    }

    /// <summary>시계를 변경하지 않고 소요 시간 후의 예상 도착 누적 시간을 계산한다.</summary>
    /// <param name="_durationHours">0 이상의 소요 시간.</param>
    /// <returns>시작부터 도착까지 누적 시간. 현실 시각이나 날짜가 아니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException">소요 시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">누적 시간이 long 범위를 초과할 때 발생한다.</exception>
    public long ArrivalAt(long _durationHours)
    {
        if (_durationHours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_durationHours));
        }
        return checked(ElapsedHours + _durationHours);
    }

    /// <summary>예상 도착이 기한 이내인지 조회한다. 기한과 같거나 무기한이면 true다. 이동 자체를 차단하지 않는다.</summary>
    /// <param name="_durationHours">0 이상의 소요 시간.</param>
    /// <returns>도착 시각이 기한을 초과하지 않는지 여부.</returns>
    /// <exception cref="ArgumentOutOfRangeException">소요 시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">예상 누적 시간이 long 범위를 초과할 때 발생한다.</exception>
    public bool CanArriveBeforeDeadline(long _durationHours)
    {
        long arrival = ArrivalAt(_durationHours);
        return !HasDeadline || arrival <= DeadlineHours;
    }

    /// <summary>
    /// 누적 시간을 증가시킨다. 세션의 AdvanceHours를 통해 호출해 이벤트·승패 판정을 함께 처리한다.
    /// 자체 이벤트 발행이나 기한 초과 차단은 하지 않으며 예외 시 기존 시간은 유지된다.
    /// </summary>
    /// <param name="_hours">0 이상의 추가 시간. 0은 무변화.</param>
    /// <exception cref="ArgumentOutOfRangeException">추가 시간이 음수일 때 발생한다.</exception>
    /// <exception cref="OverflowException">누적 시간이 long 범위를 초과할 때 발생한다.</exception>
    internal void Advance(long _hours)
    {
        ElapsedHours = ArrivalAt(_hours);
    }
}
