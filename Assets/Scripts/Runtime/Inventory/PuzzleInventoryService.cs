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
            definitions.Add(item.id, new PuzzleItemDefinition(item));
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

    internal void Travel(bool _forward)
    {
        int offset = _forward ? 1 : cities.Count - 1;
        currentCityIndex = (currentCityIndex + offset) % cities.Count;
        Market.RecalculateValue();
    }

    internal PuzzleDropResult EvaluateDrop(PuzzlePlacement _original, PuzzleInventory _destination,
        Vector2Int _grabbedCell, Vector2Int _targetCell, int _clockwiseTurns)
    {
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

        var rates = tables.CityPrice(CurrentCityId);
        bool enabled = buying ? rates.buyEnabled : rates.sellEnabled;
        long price;
        if (buying)
        {
            price = ((long)_original.Definition.BaseValue * rates.buyPercentOfBaseValue + PERCENT_SCALE / 2) / PERCENT_SCALE;
        }
        else if (!TryGetSalePrice(source.ValueOf(_original.InstanceId).FinalValue, GameTableRules.SellPercentOfFinalValue(rates),
            out price))
        {
            return new PuzzleDropResult(placement, direction, _tradeFailure: PuzzleTradeFailure.PriceOverflow);
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
        return new PuzzleDropResult(placement, direction, price, failure);
    }

    static bool TryGetSalePrice(decimal _value, int _percent, out long _price)
    {
        _price = 0;
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
            return new PuzzleDropResult(move, result.Direction, result.Price);
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

internal readonly struct PuzzleDropResult
{
    internal PuzzlePlacementResult Placement { get; }
    internal PuzzleTradeDirection Direction { get; }
    internal long Price { get; }
    internal PuzzleTradeFailure TradeFailure { get; }
    internal bool CanDrop => Placement.CanPlace && TradeFailure == PuzzleTradeFailure.None;

    internal PuzzleDropResult(PuzzlePlacementResult _placement, PuzzleTradeDirection _direction = PuzzleTradeDirection.None,
        long _price = 0, PuzzleTradeFailure _tradeFailure = PuzzleTradeFailure.None)
    {
        Placement = _placement;
        Direction = _direction;
        Price = _price;
        TradeFailure = _tradeFailure;
    }
}
