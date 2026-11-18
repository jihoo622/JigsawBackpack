using UnityEngine;

// 경로 계획 및 자동 이동 표기 규약 (Docs/Management/ROUTE_AND_SEARCH.md).
// 문구와 색은 기초데이터 키(presentation.route.*)가 원본이다.
public static partial class Presentation
{
    public static Color RoutePlanWarningColor => BasicData.Color("presentation.route.warningColor");

    public static string RoutePlanTitle(string _fromCityName, string _toCityName)
    {
        return BasicData.Format("presentation.route.title", _fromCityName, _toCityName);
    }

    internal static string RoutePlanSummary(PuzzleRouteQuote _quote)
    {
        string nightText = _quote.ArrivalIsNight ? RoutePlanArrivalNight : RoutePlanArrivalDay;
        return BasicData.Format(
            "presentation.route.summary",
            _quote.Legs,
            PuzzleTravelTime(_quote.TotalMinutes),
            _quote.ArrivalHourOfDay,
            _quote.ArrivalMinute,
            nightText,
            _quote.EstimatedCost);
    }

    public static string RoutePlanAffordableWarning => BasicData.Text("presentation.route.warningFunds");
    public static string RoutePlanArrivalNight => BasicData.Text("presentation.route.arrivalNight");
    public static string RoutePlanArrivalDay => BasicData.Text("presentation.route.arrivalDay");
    public static string RouteStopText => BasicData.Text("presentation.route.stop");
    public static string RoutePassText => BasicData.Text("presentation.route.pass");

    public static string RouteMoreStations(int _count)
    {
        return BasicData.Format("presentation.route.moreStations", _count);
    }

    public static string RouteAutoTravelButton => BasicData.Text("presentation.route.autoTravelBtn");
    public static string RouteStopButton => BasicData.Text("presentation.route.stopBtn");
    public static string RouteCancelButton => BasicData.Text("presentation.route.cancelBtn");
    public static string RouteAutoTravelStoppingNotice => BasicData.Text("presentation.route.stoppingNotice");

    internal static string RoutePlanFailureStatus(PuzzleRoutePlanFailure _failure)
    {
        return _failure switch
        {
            PuzzleRoutePlanFailure.Blocked => BasicData.Text("presentation.route.failureBlocked"),
            PuzzleRoutePlanFailure.SameCity => BasicData.Text("presentation.route.failureSameCity"),
            PuzzleRoutePlanFailure.InvalidCity => BasicData.Text("presentation.route.failureInvalidCity"),
            PuzzleRoutePlanFailure.Unreachable => BasicData.Text("presentation.route.failureUnreachable"),
            _ => string.Empty
        };
    }

    public static string RouteAutoTravelStoppedMessage(string _reason)
    {
        return _reason switch
        {
            "stop_reached" => BasicData.Text("presentation.route.stoppedStopReached"),
            "insufficient_funds" => BasicData.Text("presentation.route.stoppedInsufficientFunds"),
            "run_ended" => BasicData.Text("presentation.route.stoppedRunEnded"),
            "user_stopped" => BasicData.Text("presentation.route.stoppedUserStopped"),
            "failed" => BasicData.Text("presentation.route.stoppedFailed"),
            _ => string.Empty
        };
    }
}
