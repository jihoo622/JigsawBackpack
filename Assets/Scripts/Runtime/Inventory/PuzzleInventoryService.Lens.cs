using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

// 행선지 렌즈의 읽기 전용 계산. 거래 권한이나 확정 근거가 아니며 실제 보드·세션 상태를 바꾸지 않는다.
// 설계: Docs/Research/ROUTE_LENS_IMPLEMENTATION_PLAN.md §3·§4
public sealed partial class PuzzleInventoryService
{
    // 기차 화차가 목적일 때만 계산한다. 다른 화차에서 옮기면 출발 화차의 정산 변화도 더한다.
    internal PuzzlePlacementDelta PreviewPlacementDelta(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (_original?.Owner == null || _destination == null || !IsTrainWagon(_destination))
        {
            return null;
        }

        var result = _original.Owner.EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (!result.CanPlace)
        {
            return null;
        }

        var delta = _destination.PreviewPlacementDelta(_original, result.Candidate, _targetCell);
        return delta == null ? null : WithSourceRemoval(delta, SourceRemovalDelta(_original, _destination));
    }

    // 보드의 모든 칸을 목표로 놓아 보고 유효한 자리만 Δ 내림차순으로 정렬해 상위 _max개를 돌려준다.
    // 동률은 화면상 위→아래, 같은 줄이면 왼→오른이다. 보드 좌표 y=0이 가장 아래 줄이므로 y가 큰 칸이 위에 있다.
    // 누름·회전 때만 호출하며 포인터 이동마다 부르지 않는다.
    internal PuzzlePlacementRanking RankPlacements(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, int _clockwiseTurns, int _max = 3)
    {
        var watch = Stopwatch.StartNew();
        var deltas = new List<PuzzlePlacementDelta>();
        int validCount = 0;
        if (_original?.Owner != null && _destination != null && IsTrainWagon(_destination))
        {
            // 출발 화차 쪽 변화는 목표 칸과 무관하므로 한 번만 구한다.
            decimal removal = SourceRemovalDelta(_original, _destination);
            for (int y = 0; y < _destination.Height; y++)
            {
                for (int x = 0; x < _destination.Width; x++)
                {
                    var target = new Vector2Int(x, y);
                    var result = _original.Owner.EvaluateMove(_original, _destination, _grabbedCell, target, _clockwiseTurns);
                    if (!result.CanPlace)
                    {
                        continue;
                    }

                    validCount++;
                    var delta = _destination.PreviewPlacementDelta(_original, result.Candidate, target);
                    if (delta != null)
                    {
                        deltas.Add(WithSourceRemoval(delta, removal));
                    }
                }
            }
        }

        deltas.Sort((_left, _right) =>
        {
            int order = _right.SettlementDelta.CompareTo(_left.SettlementDelta);
            if (order != 0)
            {
                return order;
            }
            order = _right.TargetCell.y.CompareTo(_left.TargetCell.y);
            return order != 0 ? order : _left.TargetCell.x.CompareTo(_right.TargetCell.x);
        });

        int topCount = Math.Max(0, Math.Min(_max, deltas.Count));
        var top = deltas.GetRange(0, topCount);
        watch.Stop();
        return new PuzzlePlacementRanking(_destination, _clockwiseTurns, top, validCount, watch.Elapsed.TotalMilliseconds);
    }

    // 소문 탭은 위치·방문 여부와 무관하게 실제 들은 기록에서 이 화물과 관련된 내용을 읽는다.
    internal IReadOnlyList<TavernRumor> LensRumors(PuzzleItemDefinition _definition)
    {
        if (_definition == null)
        {
            return Array.Empty<TavernRumor>();
        }
        return tavernRumors.Where(_rumor => MatchesLensRumor(_rumor, _definition))
            .OrderByDescending(_rumor => _rumor.RecordedHours)
            .ThenBy(_rumor => _rumor.TargetCityId, StringComparer.Ordinal)
            .ThenBy(_rumor => _rumor.SourceCityId, StringComparer.Ordinal)
            .ToArray();
    }

    static bool MatchesLensRumor(TavernRumor _rumor, PuzzleItemDefinition _definition)
    {
        return string.Equals(_rumor.ItemId, _definition.Id, StringComparison.Ordinal) ||
            (_rumor.Tag.Length > 0 && _definition.Tags.Contains(_rumor.Tag));
    }

    internal IReadOnlyList<PuzzleLensDestination> LensVisitedDestinations(PuzzleItemDefinition _definition, decimal _value,
        bool _itemAlreadyOnTrain)
    {
        if (_definition == null || tables == null || Time == null || string.IsNullOrEmpty(currentCityId))
        {
            return Array.Empty<PuzzleLensDestination>();
        }

        int freightCells = TotalTrainOccupiedCells + (_itemAlreadyOnTrain ? 0 : ItemFreightCellsOf(_definition));
        var destinations = new List<PuzzleLensDestination>();
        foreach (var city in tables.Cities)
        {
            if (!HasVisitedCity(city.id))
            {
                continue;
            }

            bool planned = string.Equals(city.id, RoutePlan?.DestinationCityId, StringComparison.Ordinal);
            var path = planned ? RoutePlan.Cities : PuzzleRoutePlanner.FindFastestPath(tables, currentCityId, city.id,
                _route => IsRouteKnown(_route.fromCityId, _route.toCityId));
            int legs = path != null ? path.Count - 1 : 0;
            long? travelTurns = path != null ? legs * (long)tables.PuzzleTime.travelTurns : (long?)null;
            destinations.Add(BuildLensDestination(city, _definition, _value, legs, travelTurns,
                LensRouteFare(path, freightCells), planned));
        }

        return destinations.OrderByDescending(_destination => _destination.Planned)
            .ThenByDescending(_destination => _destination.Quote.HasValue)
            .ThenByDescending(_destination => _destination.Quote ?? long.MinValue)
            .ThenBy(_destination => _destination.CityId, StringComparer.Ordinal)
            .ToArray();
    }

    long? LensRouteFare(IReadOnlyList<string> _path, int _freightCells)
    {
        if (_path == null)
        {
            return null;
        }
        long total = 0;
        for (int i = 1; i < _path.Count; i++)
        {
            if (!tables.TryGetRoute(_path[i - 1], _path[i], out var route) ||
                !TryTravelCost(route.cost, route.distance, tables.PuzzleRoute.costPerDistance, _freightCells,
                    tables.PuzzleRoute.costPerCell, out long cost, out _) || total > long.MaxValue - cost)
            {
                return null;
            }
            total += cost;
        }
        return total;
    }

    // 현재 역에서 노선이 보이는 직결 역과 지도에서 계획한 목적지를 후보로 만든다(계획 §4 공개 규칙).
    // 방문한 역만 견적·등급을 계산하고, 나머지는 소문 기록이나 운임·턴만 보여 준다.
    internal IReadOnlyList<PuzzleLensDestination> LensDestinations(PuzzleItemDefinition _definition, decimal _value,
        bool _itemAlreadyOnTrain)
    {
        if (_definition == null || tables == null || Time == null || string.IsNullOrEmpty(currentCityId))
        {
            return Array.Empty<PuzzleLensDestination>();
        }

        int freightCells = TotalTrainOccupiedCells + (_itemAlreadyOnTrain ? 0 : ItemFreightCellsOf(_definition));
        string plannedCityId = RoutePlan?.DestinationCityId;
        var all = new List<PuzzleLensDestination>();
        foreach (var city in tables.Cities)
        {
            if (string.Equals(city.id, currentCityId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(city.id, plannedCityId, StringComparison.Ordinal))
            {
                // 계획 목적지의 운임·턴은 지금 적재량 기준 경로 견적이라 이 화물의 칸은 들어 있지 않다.
                var quote = QuoteRoutePlan();
                all.Add(BuildLensDestination(city, _definition, _value, quote.Legs, quote.TotalTurns, quote.EstimatedCost, true));
                continue;
            }

            if (!tables.TryGetRoute(currentCityId, city.id, out var route) || !IsRouteKnown(currentCityId, city.id))
            {
                continue;
            }

            long? fare = TryTravelCost(route.cost, route.distance, tables.PuzzleRoute.costPerDistance, freightCells,
                tables.PuzzleRoute.costPerCell, out long fareValue, out _)
                ? fareValue
                : null;
            all.Add(BuildLensDestination(city, _definition, _value, 1, tables.PuzzleTime.travelTurns, fare, false));
        }

        // 계획 목적지를 맨 앞에 두고, 방문 역은 견적 높은 순, 소문 역은 최근 순, 미확인 역은 구간·운임 적은 순이다.
        var ordered = new List<PuzzleLensDestination>(all.Count);
        ordered.AddRange(all.Where(_destination => _destination.Planned));
        ordered.AddRange(all.Where(_destination => !_destination.Planned && _destination.Level == PuzzleLensLevel.Visited)
            .OrderByDescending(_destination => _destination.Quote.HasValue)
            .ThenByDescending(_destination => _destination.Quote ?? long.MinValue)
            .ThenBy(_destination => _destination.CityId, StringComparer.Ordinal));
        ordered.AddRange(all.Where(_destination => !_destination.Planned && _destination.Level == PuzzleLensLevel.Rumor)
            .OrderBy(_destination => _destination.RumorHoursAgo ?? long.MaxValue)
            .ThenBy(_destination => _destination.CityId, StringComparer.Ordinal));
        ordered.AddRange(all.Where(_destination => !_destination.Planned && _destination.Level == PuzzleLensLevel.Unknown)
            .OrderBy(_destination => _destination.Legs)
            .ThenBy(_destination => _destination.Fare ?? long.MaxValue)
            .ThenBy(_destination => _destination.CityId, StringComparer.Ordinal));
        return ordered;
    }

    // 공개 수준은 방문 여부로 정한다. 방문 역이 아니면 견적·등급을 계산하지 않는다.
    PuzzleLensDestination BuildLensDestination(PuzzleCityData _city, PuzzleItemDefinition _definition, decimal _value,
        int _legs, long? _travelTurns, long? _fare, bool _planned)
    {
        if (HasVisitedCity(_city.id))
        {
            // 현재 가치 기준 판매 견적에 숙련 비율을 적용한다. 현재 역의 구매 취소 환불은 현재 탭에서 처리한다.
            long? quote = null;
            if (TryQuoteTradeAt(_city.id, _definition, _value, PuzzleTradeDirection.Sell, out long price) &&
                TryApplyProficiencyBonus(_definition, PuzzleTradeDirection.Sell, ref price))
            {
                quote = price;
            }

            return new PuzzleLensDestination(_city.id, _city.displayName, PuzzleLensLevel.Visited,
                RateFor(_city, _definition).SellPercent, tables.Region(_city.regionId).pricePercent, quote,
                null, null, false, _legs, _travelTurns, _fare, _planned, LensSellGrade(_city, _definition));
        }

        if (TryFindRumor(_city.id, _definition, out var rumor))
        {
            long hoursAgo = Time.ElapsedHours - rumor.RecordedHours;
            return new PuzzleLensDestination(_city.id, _city.displayName, PuzzleLensLevel.Rumor, null, null, null,
                rumor.Message, hoursAgo, hoursAgo >= tables.PuzzleRumor.refreshHours, _legs, _travelTurns, _fare, _planned);
        }

        return new PuzzleLensDestination(_city.id, _city.displayName, PuzzleLensLevel.Unknown, null, null, null,
            null, null, false, _legs, _travelTurns, _fare, _planned);
    }

    // 합산 배율은 유지하고, 일치하는 태그 규칙 중 우선순위가 가장 높은 판매 등급을 대표로 표시한다.
    PuzzleSellGradeKind LensSellGrade(PuzzleCityData _city, PuzzleItemDefinition _definition)
    {
        PuzzleCityTagPriceData selected = null;
        foreach (string tag in _definition.Tags)
        {
            if (tables.TryGetCityTagPrice(_city.id, tag, out var rule) &&
                (selected == null || rule.priority > selected.priority))
            {
                selected = rule;
            }
        }
        return selected == null ? PuzzleSellGradeKind.Normal : TableManager.SellGradeKindOf(selected);
    }

    // 같은 역의 소문 중 이 화물과 맞는 가장 최근 기록. 소문 태그가 화물 태그에 들거나 품목 ID가 같아야 한다.
    bool TryFindRumor(string _cityId, PuzzleItemDefinition _definition, out TavernRumor _rumor)
    {
        bool found = false;
        _rumor = default;
        foreach (var rumor in tavernRumors)
        {
            if (!string.Equals(rumor.TargetCityId, _cityId, StringComparison.Ordinal))
            {
                continue;
            }

            if (MatchesLensRumor(rumor, _definition) && (!found || rumor.RecordedHours >= _rumor.RecordedHours))
            {
                _rumor = rumor;
                found = true;
            }
        }
        return found;
    }

    // 화물 하나가 운임 칸에 드는 수. FreightCellsOf와 같은 면제 규칙을 쓰며, 시장에서 사면 현재 화차에 실리므로 그 화차의 면제 태그를 본다.
    int ItemFreightCellsOf(PuzzleItemDefinition _definition)
    {
        var exempt = CurrentTrainWagon?.WagonProduct?.freightExemptTags;
        if (exempt == null || exempt.Length == 0)
        {
            return _definition.Cells.Count;
        }
        return _definition.Tags.Any(exempt.Contains) ? 0 : _definition.Cells.Count;
    }

    // 다른 기차 화차에서 옮길 때만 출발 화차의 정산 변화가 생긴다. 시장이나 같은 화차면 0이다.
    decimal SourceRemovalDelta(PuzzlePlacement _original, PuzzleInventory _destination)
    {
        var source = _original.Owner;
        if (ReferenceEquals(source, _destination) || !IsTrainWagon(source))
        {
            return 0m;
        }
        return source.PreviewRemovalDelta(_original);
    }

    static PuzzlePlacementDelta WithSourceRemoval(PuzzlePlacementDelta _delta, decimal _removal)
    {
        if (_removal == 0m)
        {
            return _delta;
        }
        return new PuzzlePlacementDelta(_delta.Pose, _delta.TargetCell, _delta.SettlementDelta + _removal, _delta.Affected);
    }
}
