using System;

// 휴식(빨리 감기). 고른 시각(정시)까지 시계를 rules.time.restTimeScale배로 흘린다. 즉시 건너뛰지 않고
// 그동안의 시너지 정산·유통기한·동물 사건은 평소처럼 1시간씩 처리한다. 다시 누르거나 출발하면 멈춘다.
public sealed partial class PuzzleInventoryService
{
    // 휴식이 끝나는 절대 경과 시간(정시). 쉬고 있지 않으면 null.
    internal long? RestTargetHours { get; private set; }
    internal bool IsResting => RestTargetHours.HasValue;
    static double RestTimeScale => BasicData.Float("rules.time.restTimeScale");
    // 정시 사이 흐름까지 포함한 경과 시간. 시계의 HourFraction(표시용으로 자른 값) 대신 누적값을 쓴다.
    internal double ContinuousElapsedHours => Time.ElapsedHours + clockCarryHours;

    // 지금부터 _minutes 뒤의 연속 경과 시간.
    internal double ContinuousArrivalAfter(long _minutes)
    {
        return ContinuousElapsedHours + _minutes / (double)PuzzleTimeManager.MINUTES_PER_HOUR;
    }

    // 지금부터 _minutes 동안 확정될 정시 수(1시간씩 처리하는 시뮬레이션 횟수).
    internal long HourCommitsDuring(long _minutes)
    {
        return (long)Math.Floor(clockCarryHours + _minutes / (double)PuzzleTimeManager.MINUTES_PER_HOUR);
    }

    // 화면 시각 _clockHour(0~23)가 지금 이후 처음 오는 절대 경과 시간.
    internal long RestTargetFor(int _clockHour)
    {
        int hour = (_clockHour % PuzzleTimeManager.HOURS_PER_DAY + PuzzleTimeManager.HOURS_PER_DAY) %
            PuzzleTimeManager.HOURS_PER_DAY;
        long target = Time.ElapsedHours +
            (hour - Time.Hour + PuzzleTimeManager.HOURS_PER_DAY) % PuzzleTimeManager.HOURS_PER_DAY;
        if (target <= ContinuousElapsedHours)
        {
            target += PuzzleTimeManager.HOURS_PER_DAY;
        }
        return target;
    }

    // 지금부터 그 시각까지 남은 게임 시간.
    internal double RestRemainingHoursFor(int _clockHour)
    {
        return RestTargetFor(_clockHour) - ContinuousElapsedHours;
    }

    // 기한을 넘기는 휴식은 막는다. 이동 중이거나 이미 쉬는 중이면 새로 시작하지 않는다.
    internal bool CanRestUntil(int _clockHour)
    {
        return Time != null && RunStatus == PuzzleRunStatus.InProgress && !IsTraveling && !IsResting &&
            (!Time.HasDeadline || RestTargetFor(_clockHour) <= Time.DeadlineHours);
    }

    internal bool StartRest(int _clockHour)
    {
        if (!CanRestUntil(_clockHour))
        {
            RecordAction(PuzzleActionKind.Rest, "unavailable");
            return false;
        }
        long target = RestTargetFor(_clockHour);
        RestTargetHours = target;
        RecordAction(PuzzleActionKind.Rest, _requestedHours: (long)Math.Ceiling(target - ContinuousElapsedHours));
        GameEvents.RaiseRestChanged(this);
        return true;
    }

    internal void StopRest()
    {
        if (!RestTargetHours.HasValue)
        {
            return;
        }
        RestTargetHours = null;
        GameEvents.RaiseRestChanged(this);
    }

    // AdvanceRealTime이 쉬는 동안 부른다. 목표 시각을 넘는 몫은 휴식이 끝난 뒤 평소 속도로 돌려준다.
    double AdvanceRest(double _hours, bool _holdHourBoundary)
    {
        double scaled = _hours * RestTimeScale;
        double remaining = Math.Max(0d, RestTargetHours.Value - ContinuousElapsedHours);
        bool reaches = scaled >= remaining;
        clockCarryHours += reaches ? remaining : scaled;
        CommitClockHours(_holdHourBoundary);
        if (reaches)
        {
            StopRest();
        }
        return reaches ? (scaled - remaining) / RestTimeScale : 0d;
    }
}
