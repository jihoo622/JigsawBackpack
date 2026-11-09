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
    int currentCityIndex;

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
        var definitions = new Dictionary<string, PuzzleItemDefinition>(StringComparer.Ordinal);
        foreach (var item in validatedTables.Pieces)
        {
            definitions.Add(item.id, new PuzzleItemDefinition(item, validatedTables.ItemPrice(item.id).baseValue));
        }

        int lastInstanceId = 0;
        var trainRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Train);
        var marketRules = validatedTables.SynergiesFor(PuzzleSynergyScope.Market);
        var train = new PuzzleInventory(validatedTables.Puzzle.train, validatedTables.TrainStartingPlacements, definitions, trainRules,
            ref lastInstanceId);
        var cityMarkets = new Dictionary<string, PuzzleInventory>(StringComparer.Ordinal);
        long marketCapacity = 0;
        foreach (var city in validatedTables.Cities)
        {
            var market = new PuzzleInventory(city.market, validatedTables.MarketStartingPlacements(city.id), definitions,
                marketRules, ref lastInstanceId);
            cityMarkets.Add(city.id, market);
            marketCapacity = Math.Max(marketCapacity, (long)market.Width * market.Height);
        }

        tables = validatedTables;
        cities = tables.Cities;
        markets = cityMarkets;
        ItemDefinitions = definitions.Values;
        MarketCapacity = marketCapacity;
        currentCityIndex = 0;
        Funds = tables.PuzzleTrade.startingFunds;
        CityMoveCount = 0;
        PaidDebtCount = 0;
        RunStatus = PuzzleRunStatus.InProgress;
        Train = train;
    }

    internal bool Travel(bool _forward)
    {
        if (RunStatus != PuzzleRunStatus.InProgress || !CanTravel)
        {
            return false;
        }

        int offset = _forward ? 1 : cities.Count - 1;
        int nextCityIndex = (currentCityIndex + offset) % cities.Count;

        // 출발 직전 기차 인접 관계로 한 턴의 가치 변화를 계산하고, 도시·턴·시장을 함께 확정한다.
        Train.ApplyTurnValueChange();
        currentCityIndex = nextCityIndex;
        CityMoveCount++;
        Market.RecalculateValue();

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

        return true;
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
