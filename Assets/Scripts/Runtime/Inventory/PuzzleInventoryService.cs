using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryService : MonoBehaviour
{
    const int PERCENT_SCALE = 100;

    TableManager tables;
    IReadOnlyList<PuzzleCityData> cities;
    Dictionary<string, PuzzleInventory> markets;
    Dictionary<string, PuzzleItemDefinition> definitions;
    IReadOnlyList<PuzzleSynergyRuleData> marketRules;
    int lastInstanceId;
    int currentCityIndex;
    readonly HashSet<string> visitedCities = new HashSet<string>(StringComparer.Ordinal);

    internal bool HasVisitedCity(string _cityId)
    {
        return visitedCities.Contains(_cityId);
    }

    public PuzzleInventory Train { get; private set; }
    public PuzzleInventory Market => markets[CurrentCityId];
    public string CurrentCityId => cities[currentCityIndex].id;
    internal PuzzleCityData CityData => tables.CityPrice(CurrentCityId);
    public long Funds { get; private set; }
    public int CityMoveCount { get; private set; }
    public int PaidDebtCount { get; private set; }
    public PuzzleRunStatus RunStatus { get; private set; }
    internal string CurrentCityName => cities[currentCityIndex].displayName;
    internal PuzzleDebtPaymentData NextDebt => PaidDebtCount < tables.DebtSchedule.Count
        ? tables.DebtSchedule[PaidDebtCount]
        : null;
    internal bool CanTravel => cities.Count > 1;
    internal long MarketCapacity { get; private set; }
    internal IReadOnlyCollection<PuzzleItemDefinition> ItemDefinitions { get; private set; }

    internal void Initialize()
    {
        if (Train != null)
        {
            return;
        }

        var validatedTables = TableManager.Instance.RequireValid();
        definitions = new Dictionary<string, PuzzleItemDefinition>(StringComparer.Ordinal);
        foreach (var item in validatedTables.Pieces)
        {
            definitions.Add(item.id, new PuzzleItemDefinition(item, validatedTables.ItemPrice(item.id).baseValue));
        }

        tables = validatedTables;
        cities = tables.Cities;
        lastInstanceId = 0;
        var trainRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Train);
        marketRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Market);
        var train = new PuzzleInventory(validatedTables.Puzzle.train, validatedTables.TrainStartingPlacements, definitions, trainRules,
            ref lastInstanceId);
        var cityMarkets = new Dictionary<string, PuzzleInventory>(StringComparer.Ordinal);
        long marketCapacity = 0;
        foreach (var city in validatedTables.Cities)
        {
            var market = CreateMarket(city.id);
            cityMarkets.Add(city.id, market);
            marketCapacity = Math.Max(marketCapacity, (long)market.Width * market.Height);
        }

        markets = cityMarkets;
        ItemDefinitions = definitions.Values;
        MarketCapacity = marketCapacity;
        currentCityIndex = 0;
        visitedCities.Clear();
        visitedCities.Add(CurrentCityId);
        Funds = tables.PuzzleTrade.startingFunds;
        CityMoveCount = 0;
        PaidDebtCount = 0;
        RunStatus = PuzzleRunStatus.InProgress;
        Train = train;
    }

    // 도시 시장의 유일한 생성 경로. 처음 만들 때와 떠난 도시를 되돌릴 때 같은 경로를 쓴다.
    // 시작 배치로 새 인스턴스를 발급하므로 인스턴스 ID는 세션 동안 계속 증가하고 과거 ID는 재사용하지 않는다.
    PuzzleInventory CreateMarket(string _cityId)
    {
        var city = tables.CityPrice(_cityId);
        return new PuzzleInventory(city.market, tables.MarketStartingPlacements(_cityId), definitions, marketRules,
            ref lastInstanceId);
    }

    // 순환 이동에서 목적지의 인덱스. cities는 시작 도시 우선, ID 오름차순의 결정적 순서다.
    int NextCityIndex(bool _forward)
    {
        int offset = _forward ? 1 : cities.Count - 1;
        return (currentCityIndex + offset) % cities.Count;
    }

    // 노선 기본 비용에 출발 직전 기차가 점유한 칸 수를 더한 금액을 확인한다. 미리보기와 확정이 같은 경로를 쓴다.
    internal PuzzleTravelResult EvaluateTravel(bool _forward)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.Blocked);
        }

        if (!CanTravel)
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.NotAvailable);
        }

        string fromCityId = CurrentCityId;
        string toCityId = cities[NextCityIndex(_forward)].id;
        if (!tables.TryGetRoute(fromCityId, toCityId, out var route))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.MissingRoute, _fromCityId: fromCityId, _toCityId: toCityId);
        }

        int cells = Train.OccupiedCells;
        if (!TryTravelCost(route.cost, cells, tables.PuzzleRoute.costPerCell, out long cost))
        {
            return new PuzzleTravelResult(PuzzleTravelFailure.CostOverflow, _fromCityId: fromCityId, _toCityId: toCityId,
                _baseCost: route.cost, _cells: cells);
        }

        return new PuzzleTravelResult(Funds < cost ? PuzzleTravelFailure.InsufficientFunds : PuzzleTravelFailure.None,
            cost, fromCityId, toCityId, _baseCost: route.cost, _cells: cells);
    }

    // 이동 버튼에 표시할 목적지 노선의 비용. 기차 보유 칸이 바뀌면 값도 바뀐다.
    internal long? NextRouteCost(bool _forward)
    {
        var evaluation = EvaluateTravel(_forward);
        return evaluation.Failure == PuzzleTravelFailure.None || evaluation.Failure == PuzzleTravelFailure.InsufficientFunds
            ? (long?)evaluation.Cost
            : null;
    }

    // 노선 비용 + 기차 점유 칸 수 × 칸당 단가. 한도를 넘으면 조용히 넘기지 않고 실패로 알린다.
    static bool TryTravelCost(long _baseCost, int _cells, int _costPerCell, out long _cost)
    {
        _cost = 0;
        try
        {
            _cost = checked(_baseCost + (long)_cells * _costPerCell);
        }
        catch (OverflowException)
        {
            return false;
        }

        return true;
    }

    internal PuzzleTravelResult Travel(bool _forward)
    {
        var evaluation = EvaluateTravel(_forward);
        if (!evaluation.IsSuccess)
        {
            return evaluation;
        }

        // 이동 비용은 출발 전에 낸다. 기차는 출발 직전까지의 배치로 계산한다. 이후의 도시 도착·턴 증가·가치 변화는 한 번에 확정한다.
        Funds -= evaluation.Cost;
        // 출발 직전 기차 인접 관계로 한 턴의 가치 변화를 계산한다.
        Train.ApplyTurnValueChange();
        // 떠난 도시의 시장은 테이블의 시작 배치로 되돌린다. 기차는 그대로 두고, 목적지 시장은 손대지 않는다.
        markets[evaluation.FromCityId] = CreateMarket(evaluation.FromCityId);
        currentCityIndex = NextCityIndex(_forward);
        CityMoveCount++;
        visitedCities.Add(CurrentCityId);
        Market.RecalculateValue();
        GameEvents.RaiseCityArrived(CurrentCityId, CityMoveCount);

        // 테스트를 위해 빚 차감 및 빚으로 인한 승패 판정을 임시 비활성화한다.
        /*
        // 빚은 도착 후에 차감한다.
        var debt = NextDebt;
        if (debt != null && debt.moveCount == CityMoveCount)
        {
            if (Funds < debt.amount)
            {
                RunStatus = PuzzleRunStatus.Lost;
            }
            else
            {
                Funds -= debt.amount;
                PaidDebtCount++;
                if (PaidDebtCount == tables.DebtSchedule.Count)
                {
                    RunStatus = PuzzleRunStatus.Won;
                }
            }
        }

        */

        return new PuzzleTravelResult(PuzzleTravelFailure.None, evaluation.Cost, evaluation.FromCityId,
            evaluation.ToCityId, Funds, evaluation.BaseCost, evaluation.Cells);
    }

    internal PuzzleDropResult EvaluateDrop(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        if (RunStatus != PuzzleRunStatus.InProgress)
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination));
        }

        var source = _original?.Owner;
        var market = Market;
        if ((source != Train && source != market) || (_destination != Train && _destination != market))
        {
            return new PuzzleDropResult(new PuzzlePlacementResult(PuzzlePlacementFailure.InvalidDestination));
        }

        var placement = source.EvaluateMove(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (source == _destination)
        {
            return new PuzzleDropResult(placement);
        }

        bool buying = source == market;
        var direction = buying ? PuzzleTradeDirection.Buy : PuzzleTradeDirection.Sell;
        if (placement.Failure == PuzzlePlacementFailure.StaleSource)
        {
            return new PuzzleDropResult(placement, direction);
        }

        var city = tables.CityPrice(CurrentCityId);
        bool enabled = buying ? city.buyEnabled : city.sellEnabled;
        var rate = RateFor(city, _original.Definition);
        int percent = rate.PercentFor(direction);
        string tag = rate.Tag;
        if (!TryGetTradePrice(buying ? _original.Definition.BaseValue : _original.ActualValue,
            percent, out long price))
        {
            return new PuzzleDropResult(placement, direction, _tradeFailure: PuzzleTradeFailure.PriceOverflow,
                _percent: percent, _tag: tag);
        }

        var failure = PuzzleTradeFailure.None;
        if (!enabled)
        {
            failure = PuzzleTradeFailure.Disabled;
        }
        else if (buying && price > Funds)
        {
            failure = PuzzleTradeFailure.InsufficientFunds;
        }
        else if (!buying && Funds > long.MaxValue - price)
        {
            failure = PuzzleTradeFailure.BalanceOverflow;
        }
        return new PuzzleDropResult(placement, direction, price, failure, percent, tag);
    }

    // 현재 도시에서 이 아이템에 적용되는 태그 규칙과 구매·판매 비율.
    internal PuzzleTradeRate RateFor(PuzzleItemDefinition _item)
    {
        return RateFor(tables.CityPrice(CurrentCityId), _item);
    }

    PuzzleTradeRate RateFor(PuzzleCityData _city, PuzzleItemDefinition _item)
    {
        var best = BestTagRule(_item);
        return best == null
            ? new PuzzleTradeRate(null, _city.buyPercentOfBaseValue, _city.sellPercentOfFinalValue)
            : new PuzzleTradeRate(best.tag, best.buyPercent, best.sellPercent);
    }

    // 아이템 태그와 맞는 규칙 중 priority가 가장 높은 하나를 고른다. 맞는 규칙이 없으면 null.
    PuzzleCityTagPriceData BestTagRule(PuzzleItemDefinition _item)
    {
        PuzzleCityTagPriceData best = null;
        foreach (string tag in _item.Tags)
        {
            if (tables.TryGetCityTagPrice(CurrentCityId, tag, out var rule) &&
                (best == null || rule.priority > best.priority))
            {
                best = rule;
            }
        }
        return best;
    }

    static bool TryGetTradePrice(decimal _value, int _percent, out long _price)
    {
        _price = 0;
        if (_percent <= 0 || _value < 0)
        {
            return false;
        }

        decimal rate = (decimal)_percent / PERCENT_SCALE;
        if (_value > ((decimal)long.MaxValue + 1) / rate)
        {
            return false;
        }

        decimal rounded = Math.Round(_value * rate, MidpointRounding.AwayFromZero);
        if (rounded > long.MaxValue)
        {
            return false;
        }

        _price = (long)rounded;
        return true;
    }

    internal PuzzleDropResult Drop(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
        var result = EvaluateDrop(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (!result.CanDrop)
        {
            return result;
        }

        var move = _original.Owner.Move(_original, _destination, _grabbedCell, _targetCell, _clockwiseTurns);
        if (!move.CanPlace)
        {
            return new PuzzleDropResult(move, result.Direction, result.Price, _percent: result.AppliedPercent,
                _tag: result.AppliedTag);
        }

        if (result.Direction != PuzzleTradeDirection.None)
        {
            Funds += result.Direction == PuzzleTradeDirection.Buy ? -result.Price : result.Price;
            GameEvents.RaiseTradeCompleted(new GameEvents.TradeInfo(CurrentCityId, _original.Definition.Id,
                _original.InstanceId, result.Direction, result.Price, Funds, move.Candidate.Origin,
                move.Candidate.ClockwiseTurns));
        }
        return result;
    }
}

public enum PuzzleRunStatus
{
    InProgress,
    Won,
    Lost
}

internal enum PuzzleTravelFailure
{
    None,
    Blocked,
    NotAvailable,
    MissingRoute,
    InsufficientFunds,
    CostOverflow
}

// 이동 미리보기와 확정의 공통 결과. 실패하면 도시·턴·시장·자금 어느 것도 바뀌지 않는다.
internal readonly struct PuzzleTravelResult
{
    internal PuzzleTravelFailure Failure { get; }
    internal long Cost { get; }
    // 노선 기본 비용과 그때의 기차 점유 칸 수. Cost = BaseCost + 점유 칸 × 칸당 단가다.
    internal long BaseCost { get; }
    internal int Cells { get; }
    internal long FundsAfter { get; }
    internal string FromCityId { get; }
    internal string ToCityId { get; }
    internal bool IsSuccess => Failure == PuzzleTravelFailure.None;

    internal PuzzleTravelResult(PuzzleTravelFailure _failure, long _cost = 0, string _fromCityId = null,
        string _toCityId = null, long _fundsAfter = 0, long _baseCost = 0, int _cells = 0)
    {
        Failure = _failure;
        Cost = _cost;
        FromCityId = _fromCityId;
        ToCityId = _toCityId;
        FundsAfter = _fundsAfter;
        BaseCost = _baseCost;
        Cells = _cells;
    }
}

public enum PuzzleTradeDirection
{
    None,
    Buy,
    Sell
}

internal enum PuzzleTradeFailure
{
    None,
    Disabled,
    InsufficientFunds,
    PriceOverflow,
    BalanceOverflow
}

internal readonly struct PuzzleTradeRate
{
    internal string Tag { get; }
    internal int BuyPercent { get; }
    internal int SellPercent { get; }

    internal PuzzleTradeRate(string _tag, int _buyPercent, int _sellPercent)
    {
        Tag = _tag;
        BuyPercent = _buyPercent;
        SellPercent = _sellPercent;
    }

    internal int PercentFor(PuzzleTradeDirection _direction)
    {
        return _direction == PuzzleTradeDirection.Buy ? BuyPercent : SellPercent;
    }
}

internal readonly struct PuzzleDropResult
{
    internal PuzzlePlacementResult Placement { get; }
    internal PuzzleTradeDirection Direction { get; }
    internal long Price { get; }
    internal PuzzleTradeFailure TradeFailure { get; }
    // 적용된 도시·태그 비율. 태그가 없으면 기본 비율이다.
    internal int AppliedPercent { get; }
    internal string AppliedTag { get; }
    internal bool CanDrop => Placement.CanPlace && TradeFailure == PuzzleTradeFailure.None;

    internal PuzzleDropResult(PuzzlePlacementResult _placement, PuzzleTradeDirection _direction = PuzzleTradeDirection.None,
        long _price = 0, PuzzleTradeFailure _tradeFailure = PuzzleTradeFailure.None, int _percent = 0, string _tag = null)
    {
        Placement = _placement;
        Direction = _direction;
        Price = _price;
        TradeFailure = _tradeFailure;
        AppliedPercent = _percent;
        AppliedTag = _tag;
    }
}
