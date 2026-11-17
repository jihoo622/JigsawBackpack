using System;

// 세션의 유일한 게임 시계. 경과 턴을 정수로 보관한다. 1턴은 3시간이고 하루는 8턴이며 턴 안의 일부 시간은 없다.
// 하루의 첫 턴(TurnOfDay 0)은 시간 테이블의 startHour(06:00) 시각이고, 그 턴이 시작될 때 날짜가 바뀌고 시장을 재입고한다.
public sealed class PuzzleTimeManager
{
    public const int HOURS_PER_TURN = 3;
    public const int HOURS_PER_DAY = 24;
    public const int TURNS_PER_DAY = HOURS_PER_DAY / HOURS_PER_TURN;
    readonly PuzzleTimeTableData rules;
    public long ElapsedTurns { get; private set; }
    public long DeadlineTurns { get; }
    public bool HasDeadline => DeadlineTurns > 0;
    public bool IsOverdue => HasDeadline && ElapsedTurns > DeadlineTurns;
    public long RemainingTurns => HasDeadline ? Math.Max(0, DeadlineTurns - ElapsedTurns) : long.MaxValue;
    // 0부터 센 날짜. 화면에는 1을 더해 표시한다.
    public long Day => DayAt(ElapsedTurns);
    // 하루 안의 턴(0부터). 화면에는 1을 더해 "N턴"으로 표시한다.
    public int TurnOfDay => TurnOfDayAt(ElapsedTurns);
    // 현재 턴이 시작하는 시각(0~23시).
    public int Hour => HourAt(ElapsedTurns);
    public bool IsNight => IsNightAt(ElapsedTurns);
    public long TurnsUntilDaylight => IsNight ? TURNS_PER_DAY - TurnOfDay : 0;
    // 하루(TurnOfDay 0~7) 안에서 밤이 시작하는 턴과 길이. 낮·밤 게이지 표시에 쓴다.
    public int NightStartTurnOfDay => rules.daylightTurns;
    public int NightLengthTurns => TURNS_PER_DAY - rules.daylightTurns;

    public PuzzleTimeManager(long _deadlineTurns, PuzzleTimeTableData _rules)
    {
        if (_deadlineTurns < 0 || _rules == null)
        {
            throw new ArgumentOutOfRangeException(nameof(_deadlineTurns));
        }
        rules = _rules;
        DeadlineTurns = _deadlineTurns;
    }

    // 현재 이후 _elapsedTurns까지 하루의 첫 턴이 시작된 횟수(시장 재입고 횟수).
    internal long DaylightOpeningsThrough(long _elapsedTurns)
    {
        return Math.Max(0, _elapsedTurns / TURNS_PER_DAY - ElapsedTurns / TURNS_PER_DAY);
    }

    public bool IsNightAt(long _elapsedTurns)
    {
        return TurnOfDayAt(_elapsedTurns) >= rules.daylightTurns;
    }

    public long DayAt(long _elapsedTurns)
    {
        return _elapsedTurns / TURNS_PER_DAY;
    }

    public int TurnOfDayAt(long _elapsedTurns)
    {
        return (int)(_elapsedTurns % TURNS_PER_DAY);
    }

    public int HourAt(long _elapsedTurns)
    {
        return (rules.startHour + TurnOfDayAt(_elapsedTurns) * HOURS_PER_TURN) % HOURS_PER_DAY;
    }

    // _elapsedTurns 이후 처음 맞는 주기 경계(경과 턴이 주기의 배수인 시점)의 절대 턴. 배설 주기의 기준이다.
    internal long NextIntervalAt(long _elapsedTurns, long _intervalTurns)
    {
        if (_intervalTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_intervalTurns));
        }
        return checked(_elapsedTurns + (_intervalTurns - _elapsedTurns % _intervalTurns));
    }

    // 노선의 이동 턴. 노선 표의 turns가 원본이며 지도 선 모양과 무관하다.
    public static long TravelTurns(PuzzleRouteData _route, PuzzleTimeTableData _rules)
    {
        if (_route.turns <= 0)
        {
            throw new OverflowException("노선의 이동 턴이 없습니다.");
        }
        return _route.turns;
    }

    // 지도 선의 전체 길이. Map Editor가 이동 턴을 자동 계산할 때만 쓴다.
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

    public long ArrivalAt(long _durationTurns)
    {
        if (_durationTurns < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_durationTurns));
        }
        return checked(ElapsedTurns + _durationTurns);
    }

    public bool CanArriveBeforeDeadline(long _durationTurns)
    {
        long arrival = ArrivalAt(_durationTurns);
        return !HasDeadline || arrival <= DeadlineTurns;
    }

    public long IntervalCountAfter(long _durationTurns, long _intervalTurns)
    {
        if (_intervalTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_intervalTurns));
        }
        return ArrivalAt(_durationTurns) / _intervalTurns - ElapsedTurns / _intervalTurns;
    }

    internal void Advance(long _turns)
    {
        ElapsedTurns = ArrivalAt(_turns);
    }
}
