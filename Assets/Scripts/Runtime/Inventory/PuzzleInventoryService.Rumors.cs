using System;
using System.Collections.Generic;
using System.Linq;

// 술집 소문의 후보 정의. 종류를 고르고 문장을 만드는 일은 Presenter가 담당하고 여기서는 후보만 만든다.
public sealed partial class PuzzleInventoryService
{
    // 미방문 마을에서 특산·희귀 후보를 모두 모은다. 순서는 테이블 배열 순서와 무관하다.
    internal IReadOnlyList<PuzzleRumorCandidate> CollectRumorCandidates()
    {
        var candidates = new List<PuzzleRumorCandidate>();
        CollectSpecialtyCandidates(candidates);
        CollectRareCandidates(candidates);
        return PuzzleRumorCandidate.SortDistinct(candidates);
    }

    // 특산 후보는 마을 고정 재고의 품목 중 그 마을에서만 실리는 품목이다.
    // 구매 배율이 도시 기본 배율보다 낮을 때만 '저렴하게 산다'고 말할 수 있다.
    void CollectSpecialtyCandidates(List<PuzzleRumorCandidate> _candidates)
    {
        var stockCityCount = CountStockCities();
        foreach (var city in tables.Cities)
        {
            if (!IsRumorTarget(city) || !city.buyEnabled || string.IsNullOrEmpty(city.specialtyTag))
            {
                continue;
            }

            foreach (string itemId in FixedStockItemIds(city.id))
            {
                if (!definitions.TryGetValue(itemId, out var item) || item.ExcludeFromMarket ||
                    !item.Tags.Contains(city.specialtyTag) || stockCityCount[itemId] > 1 ||
                    CityTradeRate(city, item).BuyPercent >= city.buyPercentOfBaseValue)
                {
                    continue;
                }

                _candidates.Add(new PuzzleRumorCandidate(PuzzleRumorKind.Specialty, city.id, itemId, city.specialtyTag));
            }
        }
    }

    // 희귀 후보는 그 마을에서 판매 등급이 희귀인 태그다. 등급 이름이 희귀처럼 보이는 사용자 지정은 포함하지 않는다.
    void CollectRareCandidates(List<PuzzleRumorCandidate> _candidates)
    {
        foreach (var city in tables.Cities)
        {
            if (!IsRumorTarget(city) || !city.sellEnabled)
            {
                continue;
            }

            foreach (var rule in city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>())
            {
                if (rule == null || string.IsNullOrEmpty(rule.tag) ||
                    TableManager.SellGradeKindOf(rule) != PuzzleSellGradeKind.Rare || !HasMarketItem(rule.tag))
                {
                    continue;
                }

                _candidates.Add(new PuzzleRumorCandidate(PuzzleRumorKind.RareDemand, city.id, null, rule.tag));
            }
        }
    }

    // 품목이 몇 마을에서 실릴 수 있는지 센다. 고정 재고와 그 마을이 쓰는 일반·지역 재고 후보를 함께 본다.
    Dictionary<string, int> CountStockCities()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var city in tables.Cities)
        {
            foreach (string itemId in StockItemIds(city))
            {
                counts.TryGetValue(itemId, out int current);
                counts[itemId] = current + 1;
            }
        }

        return counts;
    }

    IEnumerable<string> StockItemIds(PuzzleCityData _city)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in tables.MarketStartingPlacements(_city.id))
        {
            ids.Add(placement.pieceId);
        }

        foreach (string stockId in new[] { _city.generalStockId, _city.regionalStockId })
        {
            if (tables.TryGetStockProfile(stockId, out var profile))
            {
                foreach (string itemId in profile.pieceIds ?? Array.Empty<string>())
                {
                    ids.Add(itemId);
                }
            }
        }

        return ids;
    }

    // 같은 품목이 여러 고정 슬롯에 있어도 후보는 하나다.
    IEnumerable<string> FixedStockItemIds(string _cityId)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in tables.MarketStartingPlacements(_cityId))
        {
            if (seen.Add(placement.pieceId))
            {
                ids.Add(placement.pieceId);
            }
        }

        return ids;
    }

    // 시장에 실릴 수 있는 품목이 하나도 없는 태그는 소문으로 말할 수 없다.
    bool HasMarketItem(string _tag)
    {
        return definitions.Values.Any(_item => !_item.ExcludeFromMarket && _item.Tags.Contains(_tag));
    }

    bool IsRumorTarget(PuzzleCityData _city)
    {
        return _city != null && !string.IsNullOrEmpty(_city.id) &&
            !string.Equals(_city.id, CurrentCityId, StringComparison.Ordinal) && !HasVisitedCity(_city.id);
    }
}
