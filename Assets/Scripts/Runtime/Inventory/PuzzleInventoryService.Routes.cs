using System;
using System.Collections.Generic;

// 경로 계획(예약)과 자동 이동의 한 구간 실행. 규칙 원본: Docs/게임 기획서.md '경로 계획과 자동 이동',
// 기술 계약: Docs/Management/ROUTE_AND_SEARCH.md.
// 각 구간은 수동 이동(Travel)과 같은 규칙을 따른다. 다른 점은 통과역뿐이다:
// 통과역 도착은 방문 기록·의뢰 완료·CityArrived를 하지 않고, 통과역에서 출발할 때 시장을 재생성하지 않는다.
public sealed partial class PuzzleInventoryService
{
    // 현재 예약된 경로 계획. 없으면 null. 바뀔 때마다 GameEvents.RaiseRoutePlanChanged.
    internal PuzzleRoutePlan RoutePlan { get; private set; }

    // 지금 역에 통과역으로 와 있는지(도착 처리를 하지 않은 상태). 자동 이동 도중에만 true가 될 수 있다.
    internal bool IsPassingThrough { get; private set; }

    // UI(워커 B)가 자동 이동 중단을 보고할 때 호출하는 진입점.
    internal void ReportAutoTravelStopped(string _reason)
    {
        GameEvents.RaiseAutoTravelStopped(new GameEvents.AutoTravelStoppedInfo(this, _reason, CurrentCityId));
    }

    // 현재 역 → 목적지 최단 시간 경로로 계획을 만든다(기존 계획은 교체). 목적지는 항상 정차,
    // 진행 중 의뢰의 목적지인 경유역은 기본 정차, 나머지 경유역은 통과.
    internal PuzzleRoutePlanFailure PlanRoute(string _destinationCityId)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return PuzzleRoutePlanFailure.Blocked;
        }

        if (string.IsNullOrEmpty(_destinationCityId) || !tables.HasCity(_destinationCityId))
        {
            return PuzzleRoutePlanFailure.InvalidCity;
        }

        if (string.Equals(_destinationCityId, CurrentCityId, StringComparison.Ordinal))
        {
            return PuzzleRoutePlanFailure.SameCity;
        }

        var path = PuzzleRoutePlanner.FindFastestPath(tables, CurrentCityId, _destinationCityId);
        if (path == null || path.Count < 2)
        {
            return PuzzleRoutePlanFailure.Unreachable;
        }

        var intermediateCities = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i < path.Count - 1; i++)
        {
            intermediateCities.Add(path[i]);
        }

        var stops = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < quests.Count; i++)
        {
            var quest = quests[i];
            if (quest.State == TrainventoryQuestState.Active && intermediateCities.Contains(quest.TargetCityId))
            {
                stops.Add(quest.TargetCityId);
            }
        }

        if (RoutePlan != null)
        {
            EndRoutePlan(PuzzleRouteEndReason.Canceled);
        }

        RoutePlan = new PuzzleRoutePlan(path, stops);

        long totalMinutes = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (tables.TryGetRoute(path[i], path[i + 1], out var legRoute))
            {
                totalMinutes += PuzzleTimeManager.TravelMinutes(legRoute, tables.PuzzleTime);
            }
        }

        int stopsCount = 1 + stops.Count;
        GameEvents.RaiseRoutePlanned(new GameEvents.RoutePlannedInfo(this, CurrentCityId, _destinationCityId,
            path.Count - 1, totalMinutes, stopsCount));
        GameEvents.RaiseRoutePlanChanged(new GameEvents.RoutePlanInfo(this));
        return PuzzleRoutePlanFailure.None;
    }

    // 경유역의 정차 여부를 바꾼다. 목적지·계획에 없는 역은 무시한다.
    internal void SetRouteStop(string _cityId, bool _stop)
    {
        if (RoutePlan == null || string.IsNullOrEmpty(_cityId))
        {
            return;
        }

        if (string.Equals(_cityId, RoutePlan.DestinationCityId, StringComparison.Ordinal))
        {
            return;
        }

        bool isIntermediate = false;
        for (int i = 1; i < RoutePlan.Cities.Count - 1; i++)
        {
            if (string.Equals(RoutePlan.Cities[i], _cityId, StringComparison.Ordinal))
            {
                isIntermediate = true;
                break;
            }
        }

        if (!isIntermediate)
        {
            return;
        }

        if (RoutePlan.IsStop(_cityId) == _stop)
        {
            return;
        }

        RoutePlan.SetStop(_cityId, _stop);
        GameEvents.RaiseRouteStopChanged(new GameEvents.RouteStopChangedInfo(this, _cityId, _stop,
            RoutePlan.DestinationCityId));
        GameEvents.RaiseRoutePlanChanged(new GameEvents.RoutePlanInfo(this));
    }

    // 플레이어가 계획을 취소한다.
    internal void CancelRoutePlan()
    {
        if (RoutePlan == null)
        {
            return;
        }

        EndRoutePlan(PuzzleRouteEndReason.Canceled);
    }

    void EndRoutePlan(PuzzleRouteEndReason _reason)
    {
        if (RoutePlan == null)
        {
            return;
        }

        string destination = RoutePlan.DestinationCityId;
        RoutePlan = null;
        string reasonStr = _reason switch
        {
            PuzzleRouteEndReason.Completed => "completed",
            PuzzleRouteEndReason.Canceled => "canceled",
            PuzzleRouteEndReason.Deviated => "deviated",
            PuzzleRouteEndReason.RunEnded => "run_ended",
            _ => "canceled"
        };
        GameEvents.RaiseRouteEnded(new GameEvents.RouteEndedInfo(this, reasonStr, destination));
        GameEvents.RaiseRoutePlanChanged(new GameEvents.RoutePlanInfo(this));
    }

    // 남은 계획의 요약(구간 수·총 시간·도착 시각·지금 칸 수 기준 예상 운임 합계). 계획이 없으면 default.
    internal PuzzleRouteQuote QuoteRoutePlan()
    {
        if (RoutePlan == null || RoutePlan.Cities.Count < 2 || Time == null)
        {
            return default;
        }

        int legs = RoutePlan.RemainingLegs;
        long totalMinutes = 0;
        long estimatedCost = 0;
        long firstLegCost = 0;
        int cells = TotalTrainOccupiedCells;

        for (int i = 0; i < RoutePlan.Cities.Count - 1; i++)
        {
            string u = RoutePlan.Cities[i];
            string v = RoutePlan.Cities[i + 1];
            if (tables.TryGetRoute(u, v, out var route))
            {
                long legMinutes = PuzzleTimeManager.TravelMinutes(route, tables.PuzzleTime);
                totalMinutes += legMinutes;
                if (TryTravelCost(route.cost, route.distance, tables.PuzzleRoute.costPerDistance,
                    cells, tables.PuzzleRoute.costPerCell, out long cost, out _))
                {
                    estimatedCost += cost;
                    if (i == 0)
                    {
                        firstLegCost = cost;
                    }
                }
            }
        }

        int arrivalClockMinutes = (int)((Time.MinuteOfDay + totalMinutes % PuzzleTimeManager.MINUTES_PER_DAY) % PuzzleTimeManager.MINUTES_PER_DAY);
        bool arrivalIsNight = Time.IsNightAt(Time.ArrivalAt(totalMinutes));
        bool affordableNow = Funds >= firstLegCost;

        return new PuzzleRouteQuote(legs, totalMinutes, estimatedCost, arrivalClockMinutes, arrivalIsNight, affordableNow);
    }

    // 계획의 다음 구간을 지금 이동한다면의 평가(EvaluateTravel과 같은 결과). 계획이 없으면 NotAvailable.
    internal PuzzleTravelResult EvaluateNextRouteLeg()
    {
        if (RoutePlan == null || string.IsNullOrEmpty(RoutePlan.NextCityId))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.NotAvailable);
        }

        return EvaluateTravel(RoutePlan.NextCityId);
    }

    // 계획의 다음 구간을 이동한다. 다음 역이 정차역(목적지 포함)이면 수동 이동과 같은 도착 처리를 하고 _stopped=true,
    // 통과역이면 도착 처리 없이 IsPassingThrough=true로 둔다. 목적지에 정차하면 계획을 끝낸다(Completed).
    // 실패하면 아무것도 바꾸지 않는다(Travel과 같음).
    internal PuzzleTravelResult TravelNextRouteLeg(out bool _stopped)
    {
        _stopped = false;
        if (RoutePlan == null || string.IsNullOrEmpty(RoutePlan.NextCityId))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.NotAvailable);
        }

        string nextCityId = RoutePlan.NextCityId;
        bool isStop = RoutePlan.IsStop(nextCityId);
        bool passThrough = !isStop;

        var result = TravelLeg(nextCityId, passThrough);
        if (!result.IsSuccess)
        {
            return result;
        }

        _stopped = !passThrough;
        // 구간 중 기한이 지나 판이 끝나면 계획은 이미 지워졌다(HandleRunStatusChanged).
        if (RoutePlan == null)
        {
            return result;
        }
        RoutePlan.Advance();

        if (_stopped && string.Equals(nextCityId, RoutePlan.DestinationCityId, StringComparison.Ordinal))
        {
            EndRoutePlan(PuzzleRouteEndReason.Completed);
        }
        else
        {
            GameEvents.RaiseRoutePlanChanged(new GameEvents.RoutePlanInfo(this));
        }

        return result;
    }

    // 통과역에 와 있는 상태를 정차로 바꾼다(자동 이동 중지·운임 부족 등). 방문 기록·의뢰 완료·CityArrived를 처리한다.
    internal void StopAtCurrentCity()
    {
        if (!IsPassingThrough)
        {
            return;
        }

        IsPassingThrough = false;
        visitedCities.Add(CurrentCityId);
        // 이 구간은 이미 통과 이동으로 기록했다. 정차 전환은 auto_travel_stopped로 남는다.
        GameEvents.RaiseCityArrived(CurrentCityId, CityMoveCount);
        CompleteArrivedQuests();
    }
}

internal sealed class PuzzleRoutePlan
{
    readonly List<string> cities;
    readonly HashSet<string> stops;

    internal PuzzleRoutePlan(IEnumerable<string> _cities, IEnumerable<string> _stops)
    {
        cities = new List<string>(_cities);
        stops = new HashSet<string>(_stops, StringComparer.Ordinal);
    }

    // 지금 있는 역(첫 원소)부터 목적지(마지막)까지.
    internal IReadOnlyList<string> Cities => cities;
    internal string DestinationCityId => cities[cities.Count - 1];
    internal string NextCityId => cities.Count > 1 ? cities[1] : null;
    internal int RemainingLegs => cities.Count - 1;

    internal bool IsStop(string _cityId)
    {
        return string.Equals(_cityId, DestinationCityId, StringComparison.Ordinal) || stops.Contains(_cityId);
    }

    internal void SetStop(string _cityId, bool _stop)
    {
        if (_stop)
        {
            stops.Add(_cityId);
        }
        else
        {
            stops.Remove(_cityId);
        }
    }

    // 다음 역으로 한 구간 진행(첫 원소 제거).
    internal void Advance()
    {
        if (cities.Count > 1)
        {
            cities.RemoveAt(0);
        }
    }
}

internal enum PuzzleRoutePlanFailure
{
    None,
    Blocked,
    SameCity,
    InvalidCity,
    Unreachable
}

internal enum PuzzleRouteEndReason
{
    Completed,
    Canceled,
    Deviated,
    RunEnded
}

internal readonly struct PuzzleRouteQuote
{
    internal int Legs { get; }
    internal long TotalMinutes { get; }
    // 지금 기차 점유 칸 수로 계산한 운임 합계(경유역 거래로 달라지는 추정치).
    internal long EstimatedCost { get; }
    internal int ArrivalClockMinutes { get; }
    internal bool ArrivalIsNight { get; }
    internal bool AffordableNow { get; }

    internal PuzzleRouteQuote(int _legs, long _totalMinutes, long _estimatedCost, int _arrivalClockMinutes, bool _arrivalIsNight, bool _affordableNow)
    {
        Legs = _legs;
        TotalMinutes = _totalMinutes;
        EstimatedCost = _estimatedCost;
        ArrivalClockMinutes = _arrivalClockMinutes;
        ArrivalIsNight = _arrivalIsNight;
        AffordableNow = _affordableNow;
    }
}
