// 진행 중인 역 간 이동. 출발 때 평가 결과(운임·이동 시간)를 고정하고 경과 시간만 센다.
internal sealed class PuzzleJourney
{
    internal PuzzleTravelResult Evaluation { get; }
    internal string FromCityId => Evaluation.FromCityId;
    internal string ToCityId => Evaluation.ToCityId;
    internal long TotalHours => Evaluation.TravelHours;
    internal long ElapsedHours { get; set; }
    // 통과역 도착이면 도착 처리(방문·의뢰 완료·CityArrived)를 하지 않는다.
    internal bool PassThrough { get; set; }
    // 경로 계획의 다음 구간인지. 아니면 계획을 벗어난 이동이다.
    internal bool RouteLeg { get; }
    // 출발할 때 마지막 정시 이후 흐른 시간. 진행률·도착은 이 시점부터 잰다.
    internal double StartCarryHours { get; }

    internal PuzzleJourney(PuzzleTravelResult _evaluation, bool _passThrough, bool _routeLeg, double _startCarryHours)
    {
        StartCarryHours = _startCarryHours;
        Evaluation = _evaluation;
        PassThrough = _passThrough;
        RouteLeg = _routeLeg;
    }
}
