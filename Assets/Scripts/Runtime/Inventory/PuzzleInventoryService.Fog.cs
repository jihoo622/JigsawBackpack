using System;
using System.Collections.Generic;
using System.Linq;

// 지도의 전장의 안개. 들른 역(정차·통과)과 그 역에 연결된 역만 지도에 보이고, 노선은 한쪽 끝을 들렀을 때만 보인다.
// 소문·의뢰로 알게 된 역은 위치(표식·이름)만 보이고 가는 길은 보이지 않는다. 경로 계획도 아는 노선만 쓴다.
public sealed partial class PuzzleInventoryService
{
    // 기차로 들른 역. 정차하지 않고 통과한 역도 포함한다(교역 정보 공개 기준인 visitedCities와 별개).
    readonly HashSet<string> exploredCities = new HashSet<string>(StringComparer.Ordinal);
    // 의뢰로 위치를 알게 된 역. 소문으로 알게 된 역은 소문 기록에서 바로 판정한다.
    readonly HashSet<string> locatedCities = new HashSet<string>(StringComparer.Ordinal);

    void ResetFog()
    {
        exploredCities.Clear();
        locatedCities.Clear();
        exploredCities.Add(CurrentCityId);
    }

    void ExploreCity(string _cityId)
    {
        exploredCities.Add(_cityId);
    }

    void LocateCity(string _cityId)
    {
        if (!string.IsNullOrEmpty(_cityId))
        {
            locatedCities.Add(_cityId);
        }
    }

    internal bool IsCityExplored(string _cityId)
    {
        return exploredCities.Contains(_cityId);
    }

    // 한쪽 끝을 들른 노선만 안다.
    internal bool IsRouteKnown(string _fromCityId, string _toCityId)
    {
        return tables.HasRoute(_fromCityId, _toCityId) &&
            (exploredCities.Contains(_fromCityId) || exploredCities.Contains(_toCityId));
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
        return locatedCities.Contains(_cityId) || HasRumorForCity(_cityId)
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
