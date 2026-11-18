// 휴식. 고른 턴 수(1턴~하루)만큼 시간을 흘린다. 그동안의 시너지 정산·유통기한·동물 사건은 이동과 같은 경로로 처리한다.
public sealed partial class PuzzleInventoryService
{
    internal static int MaxRestTurns => PuzzleTimeManager.TURNS_PER_DAY;

    // 기한을 넘기는 휴식은 막는다.
    internal bool CanRest(int _turns)
    {
        return Time != null && RunStatus == PuzzleRunStatus.InProgress && _turns > 0 && _turns <= MaxRestTurns &&
            Time.CanArriveBeforeDeadline(PuzzleTimeManager.HoursOf(_turns));
    }

    // _turns 뒤의 시간대(0 새벽, 1 오전, 2 오후, 3 저녁).
    internal int TurnSlotAfter(int _turns)
    {
        return Time.TurnSlotAt(Time.ElapsedHours + PuzzleTimeManager.HoursOf(_turns));
    }

    internal bool Rest(int _turns)
    {
        if (!CanRest(_turns))
        {
            RecordAction(PuzzleActionKind.Rest, "unavailable", _requestedHours: PuzzleTimeManager.HoursOf(_turns));
            return false;
        }
        return AdvanceHours(PuzzleTimeManager.HoursOf(_turns), PuzzleActionKind.Rest);
    }
}
