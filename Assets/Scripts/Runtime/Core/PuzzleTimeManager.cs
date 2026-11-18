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
    // 화면 시계의 분. 숫자가 너무 빨리 바뀌지 않게 10분 단위로 내린다.
    public const int DISPLAY_MINUTE_STEP = 10;
    public int DisplayMinute => Minute / DISPLAY_MINUTE_STEP * DISPLAY_MINUTE_STEP;
    // 화면 시계가 바뀌는 단위(10분)의 누적 번호. 같으면 시계 글자를 다시 그리지 않는다.
    public long DisplayStep => ElapsedHours * (MINUTES_PER_HOUR / DISPLAY_MINUTE_STEP) + Minute / DISPLAY_MINUTE_STEP;
    // 정시 사이 흐름까지 포함한 연속 경과 시간. 막대·눈금 위치처럼 매끄럽게 움직이는 표시에만 쓴다.
    public double ContinuousHours => ElapsedHours + HourFraction;
    // 하루 안의 연속 시간(0~24, 낮 시작 기준).
    public double ContinuousHourOfDay => HourOfDay + HourFraction;
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

    // 노선의 이동 시간(분). 지도 선(points) 길이 ÷ pixelsPerHour를 travelRoundingMinutes 단위로 올림한다.
    // 운임은 노선 거리(distance)를 따로 쓰므로 선 길이와 무관하다.
    public static long TravelMinutes(PuzzleRouteData _route, PuzzleTimeTableData _rules)
    {
        double pixels = PolylineLength(_route.points ?? Array.Empty<float>());
        double step = _rules.travelRoundingMinutes;
        double minutes = Math.Ceiling(pixels / _rules.pixelsPerHour * MINUTES_PER_HOUR / step) * step;
        if (double.IsNaN(minutes) || double.IsInfinity(minutes) || minutes < step || minutes >= long.MaxValue)
        {
            throw new OverflowException("노선의 이동 시간을 계산할 수 없습니다.");
        }
        return checked((long)minutes);
    }

    // 이동 시간을 시간 단위로 올림한 값. 시간 단위로 계획을 세우는 경제 분석만 쓴다.
    public static long TravelHoursRoundedUp(PuzzleRouteData _route, PuzzleTimeTableData _rules)
    {
        return (TravelMinutes(_route, _rules) + MINUTES_PER_HOUR - 1) / MINUTES_PER_HOUR;
    }

    // 지도 선의 전체 길이. 이동 시간과 Map Editor의 노선 거리 자동 계산에 쓴다.
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
