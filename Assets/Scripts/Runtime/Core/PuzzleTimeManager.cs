using System;

// 세션의 유일한 게임 시계. 시뮬레이션은 경과 시간을 정수 게임 시간(1시간 단위)으로 보관하고,
// 실시간 진행 중 다음 정시까지 흐른 비율(HourFraction)은 표시에만 쓴다.
// 경과 0시간은 시간 테이블의 startHour(06:00)이며 하루의 낮은 그 시각부터 daylightHours 동안이다.
// 낮이 시작되는 시각마다 날짜가 바뀌고 시장을 재입고한다.
public sealed class PuzzleTimeManager
{
    public const int HOURS_PER_DAY = 24;
    public const int MINUTES_PER_HOUR = 60;
    readonly PuzzleTimeTableData rules;
    public long ElapsedHours { get; private set; }
    // 다음 정시까지 흐른 비율(0 이상 1 미만). 실시간 시계가 갱신한다.
    public float HourFraction { get; private set; }
    public long DeadlineHours { get; }
    public bool HasDeadline => DeadlineHours > 0;
    public bool IsOverdue => HasDeadline && ElapsedHours > DeadlineHours;
    public long RemainingHours => HasDeadline ? Math.Max(0, DeadlineHours - ElapsedHours) : long.MaxValue;
    // 0부터 센 날짜. 화면에는 1을 더해 표시한다.
    public long Day => DayAt(ElapsedHours);
    // 하루 안의 경과 시간(0부터, 낮 시작 기준).
    public int HourOfDay => HourOfDayAt(ElapsedHours);
    // 현재 시각(0~23시).
    public int Hour => HourAt(ElapsedHours);
    public int Minute => Math.Min(MINUTES_PER_HOUR - 1, (int)(HourFraction * MINUTES_PER_HOUR));
    public bool IsNight => IsNightAt(ElapsedHours);
    public long HoursUntilDaylight => IsNight ? HOURS_PER_DAY - HourOfDay : 0;
    // 하루(HourOfDay 0~23) 안에서 밤이 시작하는 시간과 길이. 낮·밤 게이지 표시에 쓴다.
    public int NightStartHourOfDay => rules.daylightHours;
    public int NightLengthHours => HOURS_PER_DAY - rules.daylightHours;

    public PuzzleTimeManager(long _deadlineHours, PuzzleTimeTableData _rules)
    {
        if (_deadlineHours < 0 || _rules == null)
        {
            throw new ArgumentOutOfRangeException(nameof(_deadlineHours));
        }
        rules = _rules;
        DeadlineHours = _deadlineHours;
    }

    // 현재 이후 _elapsedHours까지 낮이 시작된 횟수(시장 재입고 횟수).
    internal long DaylightOpeningsThrough(long _elapsedHours)
    {
        return Math.Max(0, _elapsedHours / HOURS_PER_DAY - ElapsedHours / HOURS_PER_DAY);
    }

    public bool IsNightAt(long _elapsedHours)
    {
        return HourOfDayAt(_elapsedHours) >= rules.daylightHours;
    }

    public long DayAt(long _elapsedHours)
    {
        return _elapsedHours / HOURS_PER_DAY;
    }

    public int HourOfDayAt(long _elapsedHours)
    {
        return (int)(_elapsedHours % HOURS_PER_DAY);
    }

    public int HourAt(long _elapsedHours)
    {
        return (rules.startHour + HourOfDayAt(_elapsedHours)) % HOURS_PER_DAY;
    }

    // _elapsedHours 이후 처음 맞는 주기 경계(경과 시간이 주기의 배수인 시점)의 절대 시간. 배설 주기의 기준이다.
    internal long NextIntervalAt(long _elapsedHours, long _intervalHours)
    {
        if (_intervalHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_intervalHours));
        }
        return checked(_elapsedHours + (_intervalHours - _elapsedHours % _intervalHours));
    }

    // 노선의 이동 시간 = 노선 거리 × hoursPerDistance. 지도 선 모양과 무관하다.
    public static long TravelHours(PuzzleRouteData _route, PuzzleTimeTableData _rules)
    {
        if (_route.distance <= 0 || _rules.hoursPerDistance <= 0)
        {
            throw new OverflowException("노선의 이동 거리가 없습니다.");
        }
        return checked((long)_route.distance * _rules.hoursPerDistance);
    }

    // 지도 선의 전체 길이. Map Editor가 노선 거리를 자동 계산할 때만 쓴다.
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

    public long ArrivalAt(long _durationHours)
    {
        if (_durationHours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_durationHours));
        }
        return checked(ElapsedHours + _durationHours);
    }

    public bool CanArriveBeforeDeadline(long _durationHours)
    {
        long arrival = ArrivalAt(_durationHours);
        return !HasDeadline || arrival <= DeadlineHours;
    }

    public long IntervalCountAfter(long _durationHours, long _intervalHours)
    {
        if (_intervalHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_intervalHours));
        }
        return ArrivalAt(_durationHours) / _intervalHours - ElapsedHours / _intervalHours;
    }

    internal void Advance(long _hours)
    {
        ElapsedHours = ArrivalAt(_hours);
    }

    internal void SetHourFraction(float _fraction)
    {
        HourFraction = Math.Max(0f, Math.Min(0.999f, _fraction));
    }
}
