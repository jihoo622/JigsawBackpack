using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 경제 분석 도구(Editor)가 게임 규칙에 닿는 유일한 진입점.
// 공식을 복제하지 않고 PuzzleInventoryService를 편집 모드 임시 객체로 만들어 같은 경로로 계산한다.
// 서비스 API가 바뀌면 이 폴더(EconomyProbe·EconomySimulation)만 맞춘다.
public static class EconomyProbe
{
    // 숨김 임시 호스트에 서비스를 붙인 분석 세션. 씬에 저장되지 않고 Dispose로 제거한다.
    internal sealed class Session : IDisposable
    {
        readonly GameObject host;
        readonly Dictionary<string, PuzzleItemDefinition> definitions;

        internal PuzzleInventoryService Service { get; }

        internal Session(int _stockSeed)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("플레이 중에는 경제 분석을 실행하지 않는다(분석 로그 혼입 방지).");
            }
            host = new GameObject("Economy probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Service = host.AddComponent<PuzzleInventoryService>();
                Service.UseStockSeed(_stockSeed);
                Service.Initialize();
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(host);
                throw;
            }
            definitions = Service.ItemDefinitions.ToDictionary(_item => _item.Id, StringComparer.Ordinal);
        }

        internal PuzzleItemDefinition Definition(string _id)
        {
            return definitions[_id];
        }

        internal IEnumerable<PuzzleItemDefinition> Definitions => definitions.Values;

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    internal static TableManager Tables => TableManager.Instance.RequireValid();

    public static string CityName(string _cityId)
    {
        return Tables.HasCity(_cityId) ? Tables.CityPrice(_cityId).displayName : _cityId;
    }

    public static string ItemName(string _itemId)
    {
        return Tables.TryGetPiece(_itemId, out var piece) ? piece.displayName : _itemId;
    }

    // 도시 시장에 나올 수 있는 품목. 고정 배치는 true, 지역·일반 추첨 후보는 false.
    internal static Dictionary<string, bool> MarketCandidates(string _cityId)
    {
        var tables = Tables;
        var city = tables.CityPrice(_cityId);
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var placement in tables.MarketStartingPlacements(_cityId))
        {
            result[placement.pieceId] = true;
        }
        foreach (string stockId in new[] { city.regionalStockId, city.generalStockId })
        {
            foreach (string pieceId in tables.StockProfile(stockId).pieceIds)
            {
                if (!result.ContainsKey(pieceId))
                {
                    result.Add(pieceId, false);
                }
            }
        }
        return result;
    }

    internal static long RouteCost(PuzzleRouteData _route, int _cells)
    {
        var routeTable = Tables.PuzzleRoute;
        return PuzzleInventoryService.TryTravelCost(_route.cost, _route.distance, routeTable.costPerDistance,
            _cells, routeTable.costPerCell, out long cost, out _) ? cost : long.MaxValue;
    }

    internal static int WagonCells => Tables.Puzzle.train.width * Tables.Puzzle.train.height;

    // 품목별 최저 구매처와 최고 판매처. 판매가는 기본 가치 기준이며 시너지·신선도 변화는 제외한다.
    public static List<EconomyItemRow> BuildItemRows()
    {
        var tables = Tables;
        var rows = new List<EconomyItemRow>();
        using (var session = new Session(0))
        {
            var candidates = tables.Cities.ToDictionary(_city => _city.id, _city => MarketCandidates(_city.id), StringComparer.Ordinal);
            foreach (var item in session.Definitions.OrderBy(_item => _item.Id, StringComparer.Ordinal))
            {
                var row = new EconomyItemRow
                {
                    ItemId = item.Id,
                    DisplayName = item.DisplayName,
                    BaseValue = item.BaseValue,
                    Cells = item.Cells.Count,
                    ExcludeFromMarket = item.ExcludeFromMarket
                };
                foreach (var city in tables.Cities)
                {
                    if (city.buyEnabled && candidates[city.id].TryGetValue(item.Id, out bool isFixed))
                    {
                        row.StockedCityCount++;
                        if (session.Service.TryQuoteTradeAt(city.id, item, item.BaseValue, PuzzleTradeDirection.Buy, out long buy) &&
                            (row.CheapestBuyCityId == null || buy < row.CheapestBuyPrice))
                        {
                            row.CheapestBuyCityId = city.id;
                            row.CheapestBuyPrice = buy;
                        }
                    }
                    if (city.sellEnabled &&
                        session.Service.TryQuoteTradeAt(city.id, item, item.BaseValue, PuzzleTradeDirection.Sell, out long sell) &&
                        (row.BestSellCityId == null || sell > row.BestSellPrice))
                    {
                        row.BestSellCityId = city.id;
                        row.BestSellPrice = sell;
                    }
                }
                rows.Add(row);
            }
        }
        return rows;
    }

    // 품목×도시마다 실제 거래가 고르는 가격 규칙. 일치하는 태그 규칙이 둘 이상이면 Matches에 모두 남긴다.
    public static List<EconomyTagRow> BuildTagRows()
    {
        var tables = Tables;
        var rows = new List<EconomyTagRow>();
        using (var session = new Session(0))
        {
            foreach (var city in tables.Cities)
            {
                var region = tables.Region(city.regionId);
                var candidates = MarketCandidates(city.id);
                foreach (var item in session.Definitions.OrderBy(_item => _item.Id, StringComparer.Ordinal))
                {
                    var matches = city.tagPrices.Where(_rule => item.Tags.Contains(_rule.tag))
                        .OrderByDescending(_rule => _rule.priority).ToList();
                    var rate = session.Service.RateAt(city.id, item);
                    rows.Add(new EconomyTagRow
                    {
                        CityId = city.id,
                        RegionId = city.regionId,
                        ItemId = item.Id,
                        ItemTags = string.Join("·", item.Tags),
                        AppliedTag = rate.Tag,
                        Category = matches.Count == 0 ? "일반" : matches.Count > 1 ? "복합"
                            : matches[0].buyPercent < 100 ? "흔함" : matches[0].buyPercent > 100 ? "귀함" : "기타",
                        BuyPercent = rate.BuyPercent,
                        SellPercent = rate.SellPercent,
                        RegionPercent = region.pricePercent,
                        Matches = string.Join(" > ", matches.Select(_rule => $"{_rule.tag}({_rule.buyPercent}/{_rule.sellPercent})")),
                        MatchCount = matches.Count,
                        Stocked = candidates.ContainsKey(item.Id),
                        ExcludeFromMarket = item.ExcludeFromMarket
                    });
                }
            }
        }
        return rows;
    }

    // 노선별로 출발 시장 후보 중 칸당 차익이 가장 큰 품목과, 객차 1량을 그 품목으로 채웠을 때의 시간당 이익.
    public static List<EconomyRouteRow> BuildRouteRows()
    {
        var tables = Tables;
        int wagonCells = WagonCells;
        var rows = new List<EconomyRouteRow>();
        using (var session = new Session(0))
        {
            foreach (var from in tables.Cities)
            {
                var candidates = MarketCandidates(from.id);
                foreach (var route in tables.RoutesFrom(from.id))
                {
                    var to = tables.CityPrice(route.toCityId);
                    long minutes = PuzzleTimeManager.TravelMinutes(route, tables.PuzzleTime);
                    var row = new EconomyRouteRow
                    {
                        FromCityId = from.id,
                        ToCityId = to.id,
                        TravelMinutes = minutes,
                        EmptyCost = RouteCost(route, 0),
                        FullWagonCost = RouteCost(route, wagonCells),
                        BestProfitPerCell = float.NegativeInfinity
                    };
                    if (from.buyEnabled && to.sellEnabled)
                    {
                        foreach (var pair in candidates)
                        {
                            var item = session.Definition(pair.Key);
                            if (!session.Service.TryQuoteTradeAt(from.id, item, item.BaseValue, PuzzleTradeDirection.Buy, out long buy) ||
                                !session.Service.TryQuoteTradeAt(to.id, item, item.BaseValue, PuzzleTradeDirection.Sell, out long sell))
                            {
                                continue;
                            }
                            float perCell = (sell - buy) / (float)item.Cells.Count;
                            if (perCell > row.BestProfitPerCell)
                            {
                                row.BestItemId = item.Id;
                                row.BestItemFixed = pair.Value;
                                row.BestBuy = buy;
                                row.BestSell = sell;
                                row.BestProfitPerCell = perCell;
                            }
                        }
                    }
                    if (row.BestItemId == null)
                    {
                        row.BestProfitPerCell = 0;
                    }
                    float hours = minutes / (float)PuzzleTimeManager.MINUTES_PER_HOUR;
                    row.WagonProfitPerHour = (row.BestProfitPerCell * wagonCells - row.FullWagonCost) / Mathf.Max(hours, 1f / 60f);
                    rows.Add(row);
                }
            }
        }
        return rows;
    }
}

public sealed class EconomyTagRow
{
    public string CityId, RegionId, ItemId, ItemTags, AppliedTag, Category, Matches;
    public int BuyPercent, SellPercent, RegionPercent, MatchCount;
    public bool Stocked, ExcludeFromMarket;
    // 지역 물가까지 곱한 최종 배율(%).
    public float EffectiveBuyPercent => BuyPercent * RegionPercent / 100f;
    public float EffectiveSellPercent => SellPercent * RegionPercent / 100f;
}

public sealed class EconomyItemRow
{
    public string ItemId, DisplayName;
    public int BaseValue, Cells, StockedCityCount;
    public bool ExcludeFromMarket;
    public string CheapestBuyCityId, BestSellCityId;
    public long CheapestBuyPrice, BestSellPrice;
    public bool HasMargin => CheapestBuyCityId != null && BestSellCityId != null;
    public long Margin => HasMargin ? BestSellPrice - CheapestBuyPrice : 0;
    public float MarginPerCell => Cells > 0 ? Margin / (float)Cells : 0;
}

public sealed class EconomyRouteRow
{
    public string FromCityId, ToCityId, BestItemId;
    public long TravelMinutes, EmptyCost, FullWagonCost, BestBuy, BestSell;
    public bool BestItemFixed;
    public float BestProfitPerCell, WagonProfitPerHour;
}
