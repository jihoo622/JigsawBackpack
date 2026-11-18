using System;

// 세션의 유일한 게임 시계. 시간은 턴 단위로만 흐른다. 1턴은 게임 6시간이고 하루는 4턴(새벽·오전·오후·저녁)이다.
// 시뮬레이션은 경과 시간을 정수 게임 시간으로 보관하며 항상 턴 길이의 배수다.
// 경과 0시간은 시간 테이블의 startHour(06:00, 오전)이며 하루의 낮은 그 시각부터 daylightHours 동안이다.
// 낮이 시작되는 시각마다 날짜가 바뀌고 시장을 재입고한다.
public sealed class PuzzleTimeManager
{
    public const int HOURS_PER_DAY = 24;
    // 1턴의 게임 시간. 테이블의 시간 값(유통기한·번식·작업 시간 등)은 이 값의 배수여야 한다.
    public const int HOURS_PER_TURN = 6;
    public const int TURNS_PER_DAY = HOURS_PER_DAY / HOURS_PER_TURN;
    readonly PuzzleTimeTableData rules;
    public long ElapsedHours { get; private set; }
    public long DeadlineHours { get; }
    public bool HasDeadline => DeadlineHours > 0;
    public bool IsOverdue => HasDeadline && ElapsedHours > DeadlineHours;
    public long RemainingHours => HasDeadline ? Math.Max(0, DeadlineHours - ElapsedHours) : long.MaxValue;
    // 지난 턴 수. 화면의 턴 번호는 1을 더한 값이다.
    public long ElapsedTurns => ElapsedHours / HOURS_PER_TURN;
    public long TurnNumber => ElapsedTurns + 1;
    public long RemainingTurns => HasDeadline ? RemainingHours / HOURS_PER_TURN : long.MaxValue;
    // 0부터 센 날짜. 화면에는 1을 더해 표시한다.
    public long Day => DayAt(ElapsedHours);
    // 하루 안의 경과 시간(0부터, 낮 시작 기준).
    public int HourOfDay => HourOfDayAt(ElapsedHours);
    // 현재 시각(0~23시). 턴이 시작하는 정시다.
    public int Hour => HourAt(ElapsedHours);
    // 지금 턴의 시간대(0 새벽, 1 오전, 2 오후, 3 저녁). 시각(00시 기준)으로 정한다.
    public int TurnSlot => TurnSlotAt(ElapsedHours);
    public bool IsNight => IsNightAt(ElapsedHours);
    public long HoursUntilDaylight => IsNight ? HOURS_PER_DAY - HourOfDay : 0;
    public long TurnsUntilDaylight => HoursUntilDaylight / HOURS_PER_TURN;
    // 하루(HourOfDay 0~23) 안에서 밤이 시작하는 시간과 길이. 낮·밤 막대 표시에 쓴다.
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

    // 턴 수를 게임 시간으로 바꾼다.
    public static long HoursOf(long _turns)
    {
        return checked(_turns * HOURS_PER_TURN);
    }

    // 게임 시간을 턴 수로 바꾼다. 턴 경계에 맞지 않는 값은 올림한다.
    public static long TurnsOf(long _hours)
    {
        return _hours <= 0 ? 0 : (_hours + HOURS_PER_TURN - 1) / HOURS_PER_TURN;
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

    public int TurnSlotAt(long _elapsedHours)
    {
        return HourAt(_elapsedHours) / HOURS_PER_TURN;
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

    // 지도 선의 전체 길이. Map Editor의 노선 거리 자동 계산과 노선 검증에 쓴다.
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
}
