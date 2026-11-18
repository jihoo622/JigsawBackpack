using System;
using System.Collections.Generic;
using System.Linq;

// 지도의 전장의 안개. 들른 역(정차·통과)과 그 역에 연결된 역만 지도에 보이고, 노선은 한쪽 끝을 들렀을 때만 보인다.
// 핵심 도시(cities[].landmark)와 소문·의뢰로 알게 된 역은 위치(표식·이름)만 보이고 가는 길은 보이지 않는다.
// 의뢰를 수락하면 목적지까지의 최단 경로 노선만 드러난다. 경유역은 드러나지 않는다. 경로 계획도 아는 노선만 쓴다.
public sealed partial class PuzzleInventoryService
{
    // 기차로 들른 역. 정차하지 않고 통과한 역도 포함한다(교역 정보 공개 기준인 visitedCities와 별개).
    readonly HashSet<string> exploredCities = new HashSet<string>(StringComparer.Ordinal);
    // 의뢰로 위치를 알게 된 역. 소문으로 알게 된 역은 소문 기록에서 바로 판정한다.
    readonly HashSet<string> locatedCities = new HashSet<string>(StringComparer.Ordinal);
    // 의뢰로 드러난 노선(양방향, 역 ID 오름차순 쌍). 끝 역을 들르지 않아도 선로와 경로 계획에 쓴다.
    readonly HashSet<(string First, string Second)> revealedRoutes = new HashSet<(string First, string Second)>();
    // 기차로 실제로 달린 노선(양방향). 지도에서 이 노선만 제 색으로 그리고 주변 안개를 걷는다.
    readonly HashSet<(string First, string Second)> traveledRoutes = new HashSet<(string First, string Second)>();

    void ResetFog()
    {
        exploredCities.Clear();
        locatedCities.Clear();
        revealedRoutes.Clear();
        traveledRoutes.Clear();
        exploredCities.Add(CurrentCityId);
    }

    // 노선을 달려 _cityId에 왔다.
    void ExploreCity(string _fromCityId, string _cityId)
    {
        exploredCities.Add(_cityId);
        traveledRoutes.Add(RoutePair(_fromCityId, _cityId));
    }

    internal bool IsRouteTraveled(string _fromCityId, string _toCityId)
    {
        return traveledRoutes.Contains(RoutePair(_fromCityId, _toCityId));
    }

    void LocateCity(string _cityId)
    {
        if (!string.IsNullOrEmpty(_cityId))
        {
            locatedCities.Add(_cityId);
        }
    }

    static (string First, string Second) RoutePair(string _a, string _b)
    {
        return string.CompareOrdinal(_a, _b) <= 0 ? (_a, _b) : (_b, _a);
    }

    // 지금 역에서 목적지까지 최단 경로의 노선을 드러낸다. 목적지는 위치만 드러나고 경유역은 숨긴 채 둔다.
    void RevealRouteTo(string _targetCityId)
    {
        LocateCity(_targetCityId);
        var path = PuzzleRoutePlanner.FindFastestPath(tables, CurrentCityId, _targetCityId);
        if (path == null)
        {
            return;
        }
        for (int i = 0; i < path.Count - 1; i++)
        {
            revealedRoutes.Add(RoutePair(path[i], path[i + 1]));
        }
    }

    internal bool IsCityExplored(string _cityId)
    {
        return exploredCities.Contains(_cityId);
    }

    // 한쪽 끝을 들른 노선과 의뢰로 드러난 노선만 안다.
    internal bool IsRouteKnown(string _fromCityId, string _toCityId)
    {
        return tables.HasRoute(_fromCityId, _toCityId) &&
            (exploredCities.Contains(_fromCityId) || exploredCities.Contains(_toCityId) ||
             revealedRoutes.Contains(RoutePair(_fromCityId, _toCityId)));
    }

    internal PuzzleMapVisibility CityVisibility(string _cityId)
    {
        if (string.IsNullOrEmpty(_cityId) || !tables.HasCity(_cityId))
        {
            return PuzzleMapVisibility.Hidden;
        }
        if (exploredCities.Contains(_cityId) ||
            tables.RoutesFrom(_cityId).Any(_route => exploredCities.Contains(_route.toCityId)))
        {
            return PuzzleMapVisibility.Known;
        }
        return locatedCities.Contains(_cityId) || HasRumorForCity(_cityId) || tables.CityPrice(_cityId).landmark
            ? PuzzleMapVisibility.Located
            : PuzzleMapVisibility.Hidden;
    }
}

internal enum PuzzleMapVisibility
{
    // 지도에 보이지 않는다.
    Hidden,
    // 위치(표식·이름)만 안다. 연결 노선은 보이지 않는다.
    Located,
    // 들렀거나 들른 역과 이어져 있어 위치와 연결을 안다.
    Known
}
