using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class PuzzleInventoryService
{
    internal IEnumerable<PuzzleRumorCandidate> CollectRumorCandidates()
    {
        return CollectRumorDefinitions().Where(_candidate => IsRumorTarget(_candidate.CityId));
    }

    IReadOnlyList<PuzzleRumorCandidate> CollectRumorDefinitions()
    {
        var candidates = new List<PuzzleRumorCandidate>();
        CollectSpecialtyCandidates(candidates);
        CollectRareCandidates(candidates);
        CollectActionCandidates(candidates);
        CollectPreferenceCandidates(candidates);
        CollectWagonCandidates(candidates);
        return PuzzleRumorCandidate.SortDistinct(candidates);
    }

    // 원본 마을 테이블 대신 현재 회차에서 실제 추첨한 판매 목록을 읽는다.
    void CollectWagonCandidates(List<PuzzleRumorCandidate> _candidates)
    {
        foreach (var shop in wagonShops)
        {
            foreach (var product in shop.Value.Where(_product => !string.IsNullOrEmpty(_product.saleRegionId)))
            {
                _candidates.Add(new PuzzleRumorCandidate(PuzzleRumorKind.Wagon, shop.Key,
                    _wagonProductId: product.id));
            }
        }
    }

    // 특산 후보는 마을 고정 재고의 품목 중 그 마을의 지역 로테이션에는 없는 품목이다.
    // 다른 지역 마을이 같은 품목을 쓰거나 공통 일반 재고에 있어도 상관없다.
    // 구매 배율이 도시 기본 배율보다 낮을 때만 '저렴하게 산다'고 말할 수 있다.
    void CollectSpecialtyCandidates(List<PuzzleRumorCandidate> _candidates)
    {
        var excluded = new HashSet<string>(
            tables.PuzzleRumor.excludedItemIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var city in tables.Cities)
        {
            if (!city.buyEnabled || string.IsNullOrEmpty(city.specialtyTag))
            {
                continue;
            }

            foreach (string itemId in FixedStockItemIds(city.id))
            {
                if (!definitions.TryGetValue(itemId, out var item) || item.ExcludeFromMarket || excluded.Contains(itemId) ||
                    !item.Tags.Contains(city.specialtyTag) || InRegionalStock(city, itemId) ||
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
            if (!city.sellEnabled)
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

                _candidates.Add(new PuzzleRumorCandidate(PuzzleRumorKind.RareDemand, city.id, _tag: rule.tag));
            }
        }
    }

    void CollectActionCandidates(List<PuzzleRumorCandidate> _candidates)
    {
        foreach (var city in tables.Cities)
        {
            foreach (var action in tables.WorkshopsOf(city.id))
            {
                _candidates.Add(new PuzzleRumorCandidate(PuzzleRumorKind.Action, city.id, _actionId: action.id));
            }
        }
    }

    // 선호 소문은 시작 마을의 첫 대화에서 제공하는 테이블 지정 한 건이다.
    void CollectPreferenceCandidates(List<PuzzleRumorCandidate> _candidates)
    {
        var firstPreference = tables.PuzzleRumor.firstPreference;
        if (firstPreference == null || CityMoveCount != 0 || tavernRumors.Count > 0)
        {
            return;
        }

        var city = tables.CityPrice(firstPreference.cityId);
        if (!city.sellEnabled || !HasMarketItem(firstPreference.tag) ||
            !tables.TryGetCityTagPrice(firstPreference.cityId, firstPreference.tag, out var rule) ||
            TableManager.SellGradeKindOf(rule) != PuzzleSellGradeKind.Expensive)
        {
            return;
        }

        _candidates.Add(new PuzzleRumorCandidate(PuzzleRumorKind.Preference, firstPreference.cityId,
            _tag: firstPreference.tag));
    }

    // 품목이 그 마을의 지역 재고 로테이션에 들어 있으면 지역 공통 물건이라 특산이라 말하지 않는다.
    bool InRegionalStock(PuzzleCityData _city, string _itemId)
    {
        return tables.TryGetStockProfile(_city.regionalStockId, out var profile) &&
            (profile.pieceIds ?? Array.Empty<string>()).Contains(_itemId);
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

    bool IsRumorTarget(string _cityId)
    {
        return !string.Equals(_cityId, CurrentCityId, StringComparison.Ordinal) &&
            !HasVisitedCity(_cityId) && !HasFreshRumorForCity(_cityId);
    }

    // 마을 소문은 refreshHours가 지나면 낡아 다시 들을 수 있다. 그 마을의 가장 최근 소문부터 센다.
    // 기록 자체(수첩·지도 배지)는 남고 소문 후보에서만 빠진다.
    bool HasFreshRumorForCity(string _cityId)
    {
        long latest = -1;
        foreach (var rumor in tavernRumors)
        {
            if (string.Equals(rumor.TargetCityId, _cityId, StringComparison.Ordinal) && rumor.RecordedHours > latest)
            {
                latest = rumor.RecordedHours;
            }
        }
        return latest >= 0 && Time.ElapsedHours - latest < tables.PuzzleRumor.refreshHours;
    }
}
