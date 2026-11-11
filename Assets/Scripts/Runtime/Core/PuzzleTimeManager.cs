using System;

// 세션의 유일한 게임 시계. 분을 정수로 보관하며 소요 시간은 고정 폴라인에서 계산한다.
public sealed class PuzzleTimeManager
{
    public const int MINUTES_PER_HOUR = 60;
    public const int HOURS_PER_DAY = 24;
    public const int MINUTES_PER_DAY = HOURS_PER_DAY * MINUTES_PER_HOUR;
    readonly PuzzleTimeTableData rules;
    public long ElapsedMinutes { get; private set; }
    public decimal ElapsedHours => ElapsedMinutes / (decimal)MINUTES_PER_HOUR;
    public long DeadlineMinutes { get; }
    public bool HasDeadline => DeadlineMinutes > 0;
    public bool IsOverdue => HasDeadline && ElapsedMinutes > DeadlineMinutes;
    public long RemainingMinutes => HasDeadline ? Math.Max(0, DeadlineMinutes - ElapsedMinutes) : long.MaxValue;
    public decimal RemainingHours => RemainingMinutes / (decimal)MINUTES_PER_HOUR;
    public long Day => DayAt(ElapsedMinutes);
    public int MinuteOfDay => (int)((ElapsedMinutes % MINUTES_PER_DAY + rules.startHour * MINUTES_PER_HOUR) % MINUTES_PER_DAY);
    public int Hour => MinuteOfDay / MINUTES_PER_HOUR;
    public int Minute => MinuteOfDay % MINUTES_PER_HOUR;
    public bool IsNight => IsNightAt(ElapsedMinutes);
    public long MinutesUntilDaylight => IsNight ? MINUTES_PER_DAY - DaylightOffset(ElapsedMinutes) : 0;

    public PuzzleTimeManager(long _deadlineHours, PuzzleTimeTableData _rules)
    {
        if (_deadlineHours < 0 || _rules == null)
        {
            throw new ArgumentOutOfRangeException(nameof(_deadlineHours));
        }
        rules = _rules;
        DeadlineMinutes = checked(_deadlineHours * MINUTES_PER_HOUR);
    }

    int DaylightOffset(long _elapsedMinutes)
    {
        long clock = _elapsedMinutes % MINUTES_PER_DAY + (rules.startHour - rules.daylightStartHour) * MINUTES_PER_HOUR;
        return (int)((clock % MINUTES_PER_DAY + MINUTES_PER_DAY) % MINUTES_PER_DAY);
    }

    public bool IsNightAt(long _elapsedMinutes)
    {
        return DaylightOffset(_elapsedMinutes) >= rules.daylightHours * MINUTES_PER_HOUR;
    }

    public long DayAt(long _elapsedMinutes)
    {
        return _elapsedMinutes / MINUTES_PER_DAY +
            (_elapsedMinutes % MINUTES_PER_DAY + rules.startHour * MINUTES_PER_HOUR) / MINUTES_PER_DAY;
    }

    // 이동 시간의 분 미만을 올림하고, 같은 분수를 표시·가치·보유 시간에 사용한다.
    public static long TravelMinutes(PuzzleRouteData _route, PuzzleTimeTableData _rules)
    {
        double pixels = PolylineLength(_route.points);
        double minutes = Math.Ceiling(pixels / _rules.pixelsPerHour * MINUTES_PER_HOUR);
        if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes < 1 || minutes >= long.MaxValue)
        {
            throw new OverflowException("노선의 소요 분을 계산할 수 없습니다.");
        }
        return checked((long)minutes);
    }

    public static double PolylineLength(float[] _points)
    {
        double length = 0;
        for (int i = 2; i < _points.Length; i += 2)
        {
            double x = (double)_points[i] - _points[i - 2];
            double y = (double)_points[i + 1] - _points[i - 1];
            length += Math.Sqrt(x * x + y * y);
        }
        return length;
    }

    public long ArrivalAt(long _durationMinutes)
    {
        if (_durationMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_durationMinutes));
        }
        return checked(ElapsedMinutes + _durationMinutes);
    }

    public bool CanArriveBeforeDeadline(long _durationMinutes)
    {
        long arrival = ArrivalAt(_durationMinutes);
        return !HasDeadline || arrival <= DeadlineMinutes;
    }

    internal void Advance(long _minutes)
    {
        ElapsedMinutes = ArrivalAt(_minutes);
    }
}
