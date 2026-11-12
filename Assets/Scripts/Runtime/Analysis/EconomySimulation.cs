using System;
using System.Collections.Generic;
using System.Linq;

public enum EconomyBotPolicy
{
    // 고정 재고 기준 시간당 수익이 가장 큰 인접 왕복을 골라 그곳으로 가서 오간다. 가는 길에는 다음 역 기준으로 거래한다.
    Loop,
    // 2구간 앞까지 보고 (보유 화물 차익 + 살 화물 차익 − 운임) ÷ 시간이 큰 경로를 고른다. 현재 수치에서는 Greedy보다 성적이 낮다.
    Planner,
    // 바로 옆 도시만 본다.
    Greedy,
    Random
}

[Serializable]
public sealed class EconomySimulationSettings
{
    public int runs = 100;
    public int seed = 1;
    public EconomyBotPolicy policy = EconomyBotPolicy.Loop;
    // false면 상환 가능해도 기한 직전까지 거래를 계속한다(경제 상한 측정용).
    public bool repayEarly = true;
    // 실험용. 이번 실행 동안만 모든 품목의 유통기한을 없애 신선도가 100%로 유지된다. 테이블 파일은 바꾸지 않는다.
    public bool freezeFreshness;
    // 실험용. 이번 실행 동안만 모든 시너지 배율을 100%로, 배설 0개, 번식 없음으로 둔다.
    public bool disableSynergy;
}

// 실제 PuzzleInventoryService를 봇이 조작해 여러 판을 돌리고 일자별 자금·지출 항목을 모은다.
// 봇은 도축·개조·번식 배치를 쓰지 않는다. 시장 재고는 판마다 다른 시드로 추첨한다.
public static class EconomySimulation
{
    const int PERCENT_NEUTRAL = 100;
    // 기한(336시간)보다 충분히 길어 한 판 안에 출산이 일어나지 않는 주기.
    const int NO_BREEDING_HOURS = 100000;

    public static EconomySimulationReport Run(EconomySimulationSettings _settings, Func<int, bool> _onRunFinished = null)
    {
        var tables = EconomyProbe.Tables;
        var report = new EconomySimulationReport { Settings = _settings };
        var graph = new EconomyRouteGraph(tables);
        int dayCount = (int)(tables.PuzzleTrade.deadlineHours / PuzzleTimeManager.HOURS_PER_DAY);
        var restore = ApplyExperiments(tables, _settings);
        try
        {
            var loops = _settings.policy == EconomyBotPolicy.Loop ? EconomyLoop.BuildAll(tables) : Array.Empty<EconomyLoop>();
            RunAll(_settings, _onRunFinished, report, graph, loops, dayCount);
        }
        finally
        {
            restore();
        }
        report.Summarize(dayCount);
        return report;
    }

    // 세션은 초기화·시간 진행 때 테이블 행을 읽는다. 실행 동안만 메모리의 행을 바꾸고 되돌리는 동작을 돌려준다.
    static Action ApplyExperiments(TableManager _tables, EconomySimulationSettings _settings)
    {
        var undo = new List<Action>();
        if (_settings.freezeFreshness)
        {
            foreach (var piece in _tables.Pieces)
            {
                int shelfLife = piece.shelfLifeHours;
                undo.Add(() => piece.shelfLifeHours = shelfLife);
                piece.shelfLifeHours = 0;
            }
        }
        if (_settings.disableSynergy)
        {
            foreach (var rule in _tables.SynergyRules)
            {
                int multiplier = rule.multiplierPercent;
                undo.Add(() => rule.multiplierPercent = multiplier);
                rule.multiplierPercent = PERCENT_NEUTRAL;
            }
            var animals = _tables.PuzzleSynergy.animals;
            int wasteCount = animals.wasteCountPerAnimal;
            int breedingHours = animals.breedingHours;
            undo.Add(() =>
            {
                animals.wasteCountPerAnimal = wasteCount;
                animals.breedingHours = breedingHours;
            });
            animals.wasteCountPerAnimal = 0;
            animals.breedingHours = NO_BREEDING_HOURS;
        }
        return () =>
        {
            for (int i = undo.Count - 1; i >= 0; i--)
            {
                undo[i]();
            }
        };
    }

    static void RunAll(EconomySimulationSettings _settings, Func<int, bool> _onRunFinished, EconomySimulationReport _report,
        EconomyRouteGraph _graph, IReadOnlyList<EconomyLoop> _loops, int _dayCount)
    {
        for (int i = 0; i < _settings.runs; i++)
        {
            int stockSeed = unchecked(_settings.seed * 7919 + i);
            using (var session = new EconomyProbe.Session(stockSeed))
            {
                var bot = new EconomyBot(session, _graph, _loops, _settings, new Random(unchecked(stockSeed * 31 + 17)));
                var run = bot.Play();
                run.Index = i;
                run.StockSeed = stockSeed;
                run.BuildDaySeries(_dayCount);
                _report.Runs.Add(run);
            }
            if (_onRunFinished != null && !_onRunFinished(i + 1))
            {
                break;
            }
        }
    }
}

// 모든 도시 쌍의 최단 시간과 그 경로의 첫 역. 상환 도시까지의 값도 여기서 읽는다.
internal sealed class EconomyRouteGraph
{
    const long UNREACHABLE = long.MaxValue / 4;

    readonly Dictionary<string, Dictionary<string, long>> minutes = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
    readonly Dictionary<string, Dictionary<string, string>> firstHops = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

    internal string DebtCityId { get; }

    internal EconomyRouteGraph(TableManager _tables)
    {
        DebtCityId = string.IsNullOrEmpty(_tables.PuzzleTrade.debtCityId) ? _tables.Puzzle.startCity : _tables.PuzzleTrade.debtCityId;
        var outgoing = _tables.Cities.ToDictionary(_city => _city.id,
            _city => _tables.RoutesFrom(_city.id).Select(_route => (To: _route.toCityId, Minutes: PuzzleTimeManager.TravelMinutes(_route, _tables.PuzzleTime))).ToList(),
            StringComparer.Ordinal);

        // 출발 도시마다 다익스트라. 도시 수가 작아 단순 선택 방식을 쓴다.
        foreach (string source in outgoing.Keys)
        {
            var distance = new Dictionary<string, long>(StringComparer.Ordinal) { [source] = 0 };
            var first = new Dictionary<string, string>(StringComparer.Ordinal);
            var open = new HashSet<string>(StringComparer.Ordinal) { source };
            while (open.Count > 0)
            {
                string current = open.OrderBy(_id => distance[_id]).ThenBy(_id => _id, StringComparer.Ordinal).First();
                open.Remove(current);
                foreach (var edge in outgoing[current])
                {
                    long candidate = distance[current] + edge.Minutes;
                    if (!distance.TryGetValue(edge.To, out long known) || candidate < known)
                    {
                        distance[edge.To] = candidate;
                        first[edge.To] = current == source ? edge.To : first[current];
                        open.Add(edge.To);
                    }
                }
            }
            minutes.Add(source, distance);
            firstHops.Add(source, first);
        }
    }

    internal long MinutesBetween(string _from, string _to)
    {
        return minutes.TryGetValue(_from, out var targets) && targets.TryGetValue(_to, out long value) ? value : UNREACHABLE;
    }

    internal string NextHop(string _from, string _to)
    {
        return firstHops.TryGetValue(_from, out var targets) && targets.TryGetValue(_to, out string next) ? next : null;
    }

    internal long MinutesToDebt(string _cityId)
    {
        return MinutesBetween(_cityId, DebtCityId);
    }

    internal string NextHopToDebt(string _cityId)
    {
        return NextHop(_cityId, DebtCityId);
    }
}

// 인접 두 도시의 왕복. 각 도시 고정 재고 중 상대 도시에서 이득인 화물을 모두 싣는다고 보고 한 바퀴 순이익을 잡는다.
internal sealed class EconomyLoop
{
    internal string A { get; private set; }
    internal string B { get; private set; }
    internal long CycleMinutes { get; private set; }
    internal long CycleProfit { get; private set; }
    internal double ProfitPerHour => CycleMinutes <= 0 ? 0 : CycleProfit / (CycleMinutes / (double)PuzzleTimeManager.MINUTES_PER_HOUR);

    internal static IReadOnlyList<EconomyLoop> BuildAll(TableManager _tables)
    {
        var loops = new List<EconomyLoop>();
        using (var session = new EconomyProbe.Session(0))
        {
            foreach (var from in _tables.Cities)
            {
                foreach (var forward in _tables.RoutesFrom(from.id))
                {
                    if (StringComparer.Ordinal.Compare(from.id, forward.toCityId) >= 0 ||
                        !_tables.TryGetRoute(forward.toCityId, from.id, out var backward))
                    {
                        continue;
                    }
                    long profit = LegProfit(session, _tables, forward) + LegProfit(session, _tables, backward);
                    long cycle = PuzzleTimeManager.TravelMinutes(forward, _tables.PuzzleTime) + PuzzleTimeManager.TravelMinutes(backward, _tables.PuzzleTime);
                    if (profit > 0)
                    {
                        loops.Add(new EconomyLoop { A = from.id, B = forward.toCityId, CycleMinutes = cycle, CycleProfit = profit });
                    }
                }
            }
        }
        return loops.OrderByDescending(_loop => _loop.ProfitPerHour).ToList();
    }

    // 출발 도시 고정 재고를 도착 도시에서 팔 때의 차익 합 − 그 화물을 실은 운임.
    static long LegProfit(EconomyProbe.Session _session, TableManager _tables, PuzzleRouteData _route)
    {
        long gain = 0;
        int cells = 0;
        if (_tables.CityPrice(_route.fromCityId).buyEnabled && _tables.CityPrice(_route.toCityId).sellEnabled)
        {
            foreach (var placement in _tables.MarketStartingPlacements(_route.fromCityId))
            {
                var item = _session.Definition(placement.pieceId);
                if (_session.Service.TryQuoteTradeAt(_route.fromCityId, item, item.BaseValue, PuzzleTradeDirection.Buy, out long buy) &&
                    _session.Service.TryQuoteTradeAt(_route.toCityId, item, item.BaseValue, PuzzleTradeDirection.Sell, out long sell) &&
                    sell > buy)
                {
                    gain += sell - buy;
                    cells += item.Cells.Count;
                }
            }
        }
        return gain - EconomyProbe.RouteCost(_route, cells);
    }
}

internal sealed class EconomyBot
{
    const int MAX_STEPS = 5000;
    // 아침 대기(최대 6시간)와 도착 후 거래 여유를 포함한 귀환 여유.
    const long RETURN_BUFFER_MINUTES = 7 * PuzzleTimeManager.MINUTES_PER_HOUR;
    const long RETURN_FUNDS_MARGIN = 30;
    const long WAGON_MIN_REMAINING_MINUTES = 72 * PuzzleTimeManager.MINUTES_PER_HOUR;
    const int WAGON_FUNDS_MULTIPLIER = 3;
    const float WAGON_FREE_RATIO = 0.25f;
    const double RANDOM_TRADE_CHANCE = 0.5;
    const double RANDOM_WAGON_CHANCE = 0.2;
    const int PLANNER_DEPTH = 2;

    readonly PuzzleInventoryService service;
    readonly TableManager tables;
    readonly EconomyRouteGraph graph;
    readonly IReadOnlyList<EconomyLoop> loops;
    readonly EconomySimulationSettings settings;
    readonly Random random;
    readonly EconomyRunResult result = new EconomyRunResult();
    EconomyLoop targetLoop;

    internal EconomyBot(EconomyProbe.Session _session, EconomyRouteGraph _graph, IReadOnlyList<EconomyLoop> _loops,
        EconomySimulationSettings _settings, Random _random)
    {
        service = _session.Service;
        tables = EconomyProbe.Tables;
        graph = _graph;
        loops = _loops;
        settings = _settings;
        random = _random;
    }

    string CurrentCityId => service.CurrentCityId;
    bool AtDebtCity => CurrentCityId == graph.DebtCityId;

    internal EconomyRunResult Play()
    {
        result.Path.Add(CurrentCityId);
        Snapshot();
        int steps = 0;
        while (service.RunStatus == PuzzleRunStatus.InProgress)
        {
            if (++steps > MAX_STEPS)
            {
                result.Outcome = EconomyRunOutcome.StepLimit;
                break;
            }

            var options = TimelyRoutes();
            if (service.CanClearGame && (settings.repayEarly || options.Count == 0))
            {
                Repay();
                break;
            }

            bool returning = options.Count == 0 ||
                (settings.repayEarly && EstimatedWealthAtDebt() - CostToDebt() >= service.GameClearCost + RETURN_FUNDS_MARGIN);
            if (returning && AtDebtCity)
            {
                if (!service.IsMarketOpen && service.CanWaitForDaylight && service.Time.MinutesUntilDaylight <= service.Time.RemainingMinutes)
                {
                    Wait();
                }
                LiquidateCargo();
                Snapshot();
                if (service.CanClearGame)
                {
                    Repay();
                    break;
                }
                if (options.Count == 0)
                {
                    result.Outcome = EconomyRunOutcome.ShortOfFunds;
                    break;
                }
                // 추정 판매액이 실제와 달랐다. 거래를 이어 간다.
                returning = false;
            }

            if (!service.IsMarketOpen)
            {
                if (!returning && service.CanWaitForDaylight)
                {
                    Wait();
                    Snapshot();
                    continue;
                }
            }
            else
            {
                SellCargo(returning);
                if (service.CanClearGame && (settings.repayEarly || options.Count == 0))
                {
                    continue;
                }
                if (!returning)
                {
                    TryAddWagon();
                }
            }

            string destination = returning ? graph.NextHopToDebt(CurrentCityId) : ChooseDestinationAndBuy(options);
            Snapshot();
            if (destination == null || !TravelTo(destination, options))
            {
                result.Outcome = EconomyRunOutcome.Stuck;
                break;
            }
        }

        if (result.Outcome == EconomyRunOutcome.Unknown)
        {
            result.Outcome = service.RunStatus == PuzzleRunStatus.Won ? EconomyRunOutcome.Won
                : service.RunStatus == PuzzleRunStatus.Lost ? EconomyRunOutcome.Lost
                : EconomyRunOutcome.Stuck;
        }
        result.EndMinutes = service.Time.ElapsedMinutes;
        result.FinalFunds = service.Funds;
        result.Wagons = service.WagonCount;
        return result;
    }

    // 다녀와도 기한 안에 상환 도시에 닿을 수 있는 노선.
    List<PuzzleRouteData> TimelyRoutes()
    {
        long remaining = service.Time.RemainingMinutes;
        return tables.RoutesFrom(CurrentCityId)
            .Where(_route => TravelMinutes(_route) + graph.MinutesToDebt(_route.toCityId) + RETURN_BUFFER_MINUTES <= remaining)
            .ToList();
    }

    long TravelMinutes(PuzzleRouteData _route)
    {
        return PuzzleTimeManager.TravelMinutes(_route, tables.PuzzleTime);
    }

    IEnumerable<PuzzlePlacement> Cargo()
    {
        return service.TrainWagons.SelectMany(_wagon => _wagon.Placements).ToList();
    }

    int FreeCells()
    {
        return service.TrainWagons.Sum(_wagon => _wagon.Width * _wagon.Height - _wagon.OccupiedCells);
    }

    long SellPriceAt(string _cityId, PuzzlePlacement _item)
    {
        return service.TryQuoteTradeAt(_cityId, _item.Definition, _item.FreshnessAdjustedValue, PuzzleTradeDirection.Sell, out long price)
            ? price : long.MinValue;
    }

    long EstimatedWealthAtDebt()
    {
        long cargo = 0;
        foreach (var item in Cargo())
        {
            long price = SellPriceAt(graph.DebtCityId, item);
            if (price != long.MinValue)
            {
                cargo += price;
            }
        }
        return service.Funds + cargo;
    }

    // 지금 화물을 실은 채 최단 경로로 상환 도시까지 가는 운임 합계.
    long CostToDebt()
    {
        long total = 0;
        int cells = service.TotalTrainOccupiedCells;
        string city = CurrentCityId;
        while (city != graph.DebtCityId)
        {
            string next = graph.NextHopToDebt(city);
            if (next == null || !tables.TryGetRoute(city, next, out var route))
            {
                return long.MaxValue / 4;
            }
            total += EconomyProbe.RouteCost(route, cells);
            city = next;
        }
        return total;
    }

    void SellCargo(bool _returning)
    {
        var horizon = SaleHorizon();
        foreach (var item in Cargo())
        {
            if (!service.TryQuoteTrade(item, PuzzleTradeDirection.Sell, out long price))
            {
                continue;
            }
            bool sell;
            // 가치가 음수인 화물만 처분한다. 배율 때문에 판매가가 음수인 일반 화물은 아래 가격 비교를 따른다.
            if (item.FreshnessAdjustedValue < 0)
            {
                sell = price >= 0 || service.Funds >= -price;
            }
            else if (_returning)
            {
                sell = AtDebtCity;
            }
            else if (settings.policy == EconomyBotPolicy.Random)
            {
                sell = random.NextDouble() < RANDOM_TRADE_CHANCE;
            }
            else
            {
                sell = horizon.Count == 0 || price >= horizon.Max(_city => SellPriceAt(_city, item));
            }
            if (sell)
            {
                Sell(item);
            }
        }
    }

    void Sell(PuzzlePlacement _item)
    {
        long before = service.Funds;
        string itemId = _item.Definition.Id;
        if (!service.QuickTransfer(_item).CanDrop)
        {
            return;
        }
        long delta = service.Funds - before;
        if (delta < 0)
        {
            result.Disposed += -delta;
            Record(EconomyRunEventKind.Dispose, itemId, delta);
        }
        else
        {
            result.Sold += delta;
            Record(EconomyRunEventKind.Sell, itemId, delta);
        }
        result.Flow(itemId).AddSale(delta);
    }

    void LiquidateCargo()
    {
        if (!service.IsMarketOpen)
        {
            return;
        }
        foreach (var item in Cargo())
        {
            if (service.TryQuoteTrade(item, PuzzleTradeDirection.Sell, out long price) && price > 0)
            {
                Sell(item);
            }
        }
    }

    void TryAddWagon()
    {
        long cost = service.NextWagonCost;
        if (service.Funds < cost * WAGON_FUNDS_MULTIPLIER || service.Time.RemainingMinutes < WAGON_MIN_REMAINING_MINUTES)
        {
            return;
        }
        bool wanted = settings.policy == EconomyBotPolicy.Random
            ? random.NextDouble() < RANDOM_WAGON_CHANCE
            : FreeCells() < EconomyProbe.WagonCells * WAGON_FREE_RATIO;
        if (!wanted)
        {
            return;
        }
        long before = service.Funds;
        if (service.TryAddWagon(out _))
        {
            result.WagonCost += before - service.Funds;
            Record(EconomyRunEventKind.AddWagon, null, service.Funds - before);
        }
    }

    // 목적지를 정하고 그 목적지 기준으로 이득인 화물을 산다. 시장이 닫혀 있으면 사지 않는다.
    string ChooseDestinationAndBuy(List<PuzzleRouteData> _options)
    {
        if (_options.Count == 0)
        {
            return null;
        }
        if (settings.policy == EconomyBotPolicy.Loop)
        {
            string loopDestination = ChooseLoopDestinationAndBuy(_options);
            if (loopDestination != null)
            {
                return loopDestination;
            }
        }
        if (settings.policy == EconomyBotPolicy.Random)
        {
            var route = _options[random.Next(_options.Count)];
            if (service.IsMarketOpen)
            {
                var shuffled = service.Market.Placements.OrderBy(_ => random.Next()).ToList();
                Buy(shuffled.Where(_ => random.NextDouble() < RANDOM_TRADE_CHANCE), route);
            }
            return route.toCityId;
        }

        PuzzleRouteData best = null;
        List<PuzzlePlacement> bestPlan = null;
        double bestScore = double.NegativeInfinity;
        var cargo = Cargo().ToList();
        foreach (var path in CandidatePaths(_options))
        {
            var stops = path.Select(_route => _route.toCityId).ToList();
            long cargoGain = 0;
            foreach (var item in cargo)
            {
                service.TryQuoteTrade(item, PuzzleTradeDirection.Sell, out long here);
                long there = stops.Max(_city => SellPriceAt(_city, item));
                if (there != long.MinValue && there > here)
                {
                    cargoGain += there - here;
                }
            }
            long planProfit = 0;
            int planCells = 0;
            var plan = new List<PuzzlePlacement>();
            if (service.IsMarketOpen)
            {
                plan = PlanPurchases(stops, path[0], out planProfit, out planCells);
            }
            long cost = path.Sum(_route => EconomyProbe.RouteCost(_route, service.TotalTrainOccupiedCells + planCells));
            double hours = Math.Max(path.Sum(TravelMinutes) / (double)PuzzleTimeManager.MINUTES_PER_HOUR, 1.0 / 60);
            double score = (cargoGain + planProfit - cost) / hours;
            if (score > bestScore)
            {
                bestScore = score;
                best = path[0];
                bestPlan = plan;
            }
        }

        Buy(bestPlan, best);
        return best.toCityId;
    }

    int LookaheadDepth => settings.policy == EconomyBotPolicy.Planner ? PLANNER_DEPTH : 1;

    // 남은 시간 안에서 (시간당 수익 × 왕복에 쓸 수 있는 시간)이 가장 큰 왕복. 접근·귀환 시간을 뺀다.
    EconomyLoop PickLoop()
    {
        long remaining = service.Time.RemainingMinutes;
        EconomyLoop best = null;
        double bestValue = 0;
        foreach (var loop in loops)
        {
            long approach = Math.Min(graph.MinutesBetween(CurrentCityId, loop.A), graph.MinutesBetween(CurrentCityId, loop.B));
            long exit = Math.Min(graph.MinutesToDebt(loop.A), graph.MinutesToDebt(loop.B));
            double usable = remaining - approach - exit - RETURN_BUFFER_MINUTES;
            if (usable < loop.CycleMinutes)
            {
                continue;
            }
            double value = loop.ProfitPerHour * usable / PuzzleTimeManager.MINUTES_PER_HOUR;
            if (value > bestValue)
            {
                bestValue = value;
                best = loop;
            }
        }
        return best;
    }

    string LoopDestination()
    {
        if (targetLoop == null)
        {
            return null;
        }
        if (CurrentCityId == targetLoop.A)
        {
            return targetLoop.B;
        }
        if (CurrentCityId == targetLoop.B)
        {
            return targetLoop.A;
        }
        string nearer = graph.MinutesBetween(CurrentCityId, targetLoop.A) <= graph.MinutesBetween(CurrentCityId, targetLoop.B)
            ? targetLoop.A : targetLoop.B;
        return graph.NextHop(CurrentCityId, nearer);
    }

    // 왕복 목표가 있으면 다음 역과 왕복 양 끝, 없으면 지정 구간 수 안의 도시.
    List<string> SaleHorizon()
    {
        if (settings.policy != EconomyBotPolicy.Loop || targetLoop == null)
        {
            return Horizon(CurrentCityId, LookaheadDepth);
        }
        return new[] { LoopDestination(), targetLoop.A, targetLoop.B }
            .Where(_city => _city != null && _city != CurrentCityId).Distinct(StringComparer.Ordinal).ToList();
    }

    // 왕복 목표로 가는 다음 역을 정하고 그 역과 왕복 양 끝에서 이득인 화물을 산다. 목표가 없거나 기한상 갈 수 없으면 null.
    string ChooseLoopDestinationAndBuy(List<PuzzleRouteData> _options)
    {
        targetLoop ??= PickLoop();
        string destination = LoopDestination();
        var route = _options.FirstOrDefault(_route => _route.toCityId == destination);
        if (route == null)
        {
            return null;
        }
        if (service.IsMarketOpen)
        {
            Buy(PlanPurchases(SaleHorizon(), route, out _, out _), route);
        }
        return destination;
    }

    // 첫 구간은 기한 안에 돌아올 수 있는 노선만, 이후 구간도 같은 기준으로 이어 붙인다. 출발 도시로 되돌아가는 구간은 뺀다.
    List<List<PuzzleRouteData>> CandidatePaths(List<PuzzleRouteData> _options)
    {
        var paths = _options.Select(_route => new List<PuzzleRouteData> { _route }).ToList();
        long remaining = service.Time.RemainingMinutes;
        for (int depth = 1; depth < LookaheadDepth; depth++)
        {
            var extended = new List<List<PuzzleRouteData>>();
            foreach (var path in paths.Where(_path => _path.Count == depth))
            {
                long elapsed = path.Sum(TravelMinutes);
                foreach (var next in tables.RoutesFrom(path[path.Count - 1].toCityId))
                {
                    if (next.toCityId == CurrentCityId ||
                        elapsed + TravelMinutes(next) + graph.MinutesToDebt(next.toCityId) + RETURN_BUFFER_MINUTES > remaining)
                    {
                        continue;
                    }
                    extended.Add(new List<PuzzleRouteData>(path) { next });
                }
            }
            paths.AddRange(extended);
        }
        return paths;
    }

    // 지정 구간 수 안에 닿는 도시(출발 도시 제외).
    List<string> Horizon(string _cityId, int _depth)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new List<string> { _cityId };
        for (int depth = 0; depth < _depth; depth++)
        {
            frontier = frontier.SelectMany(_city => tables.RoutesFrom(_city)).Select(_route => _route.toCityId)
                .Where(_city => _city != _cityId && found.Add(_city)).ToList();
        }
        return found.ToList();
    }

    // 칸당 차익이 큰 순으로 예산·빈 칸 안에서 고른다. 실제 배치 가능 여부는 구매 시 확인한다.
    List<PuzzlePlacement> PlanPurchases(List<string> _saleCities, PuzzleRouteData _firstRoute, out long _profit, out int _cells)
    {
        _profit = 0;
        _cells = 0;
        var picks = new List<PuzzlePlacement>();
        var scored = new List<(PuzzlePlacement Item, long Buy, long Margin, double PerCell)>();
        foreach (var item in service.Market.Placements)
        {
            if (!service.TryQuoteTrade(item, PuzzleTradeDirection.Buy, out long buy))
            {
                continue;
            }
            long sell = long.MinValue;
            foreach (string city in _saleCities)
            {
                if (service.TryQuoteTradeAt(city, item.Definition, item.Definition.BaseValue, PuzzleTradeDirection.Sell, out long price))
                {
                    sell = Math.Max(sell, price);
                }
            }
            if (sell == long.MinValue)
            {
                continue;
            }
            long margin = sell - buy;
            if (margin > 0)
            {
                scored.Add((item, buy, margin, margin / (double)item.Definition.Cells.Count));
            }
        }

        long funds = service.Funds;
        int free = FreeCells();
        int occupied = service.TotalTrainOccupiedCells;
        foreach (var entry in scored.OrderByDescending(_entry => _entry.PerCell))
        {
            int cells = entry.Item.Definition.Cells.Count;
            if (cells > free || funds - entry.Buy < EconomyProbe.RouteCost(_firstRoute, occupied + cells))
            {
                continue;
            }
            picks.Add(entry.Item);
            funds -= entry.Buy;
            free -= cells;
            occupied += cells;
            _cells += cells;
            _profit += entry.Margin;
        }
        return picks;
    }

    void Buy(IEnumerable<PuzzlePlacement> _items, PuzzleRouteData _route)
    {
        if (_items == null)
        {
            return;
        }
        foreach (var item in _items.ToList())
        {
            if (!service.TryQuoteTrade(item, PuzzleTradeDirection.Buy, out long price) ||
                service.Funds - price < EconomyProbe.RouteCost(_route, service.TotalTrainOccupiedCells + item.Definition.Cells.Count))
            {
                continue;
            }
            string itemId = item.Definition.Id;
            long before = service.Funds;
            for (int wagon = 0; wagon < service.WagonCount; wagon++)
            {
                if (service.QuickTransferToWagon(item, wagon, item.Pose.ClockwiseTurns).CanDrop)
                {
                    long spent = before - service.Funds;
                    result.Bought += spent;
                    Record(EconomyRunEventKind.Buy, itemId, -spent);
                    result.Flow(itemId).AddPurchase(spent);
                    break;
                }
            }
        }
    }

    bool TravelTo(string _destination, List<PuzzleRouteData> _options)
    {
        if (TryTravel(_destination))
        {
            return true;
        }
        if (!service.IsMarketOpen && service.CanWaitForDaylight)
        {
            Wait();
        }
        LiquidateCargo();
        if (TryTravel(_destination))
        {
            return true;
        }
        foreach (var route in _options.OrderBy(_route => EconomyProbe.RouteCost(_route, service.TotalTrainOccupiedCells)))
        {
            if (route.toCityId != _destination && TryTravel(route.toCityId))
            {
                return true;
            }
        }
        return false;
    }

    bool TryTravel(string _destination)
    {
        long before = service.Funds;
        if (!service.Travel(_destination).IsSuccess)
        {
            return false;
        }
        result.TravelCost += before - service.Funds;
        Record(EconomyRunEventKind.Travel, null, service.Funds - before);
        result.Travels++;
        result.Path.Add(_destination);
        Snapshot();
        return true;
    }

    void Repay()
    {
        long before = service.Funds;
        if (service.TryClearGame())
        {
            result.Repaid += before - service.Funds;
            Record(EconomyRunEventKind.Repay, null, service.Funds - before);
        }
    }

    void Wait()
    {
        if (service.WaitForDaylight())
        {
            Record(EconomyRunEventKind.Wait, null, 0);
        }
    }

    void Record(EconomyRunEventKind _kind, string _itemId, long _amount)
    {
        result.Events.Add(new EconomyRunEvent(service.Time.ElapsedMinutes, service.CurrentCityId, _kind, _itemId, _amount, service.Funds));
    }

    void Snapshot()
    {
        decimal cargo = Cargo().Sum(_item => _item.FreshnessAdjustedValue);
        result.Snapshots.Add((service.Time.ElapsedMinutes, service.Funds, service.Funds + (long)Math.Round(cargo)));
    }
}

public enum EconomyRunOutcome
{
    Unknown,
    Won,
    Lost,
    ShortOfFunds,
    Stuck,
    StepLimit
}

public enum EconomyRunEventKind
{
    Buy,
    Sell,
    Dispose,
    Travel,
    Wait,
    AddWagon,
    Repay
}

// 봇이 확정한 행동 한 건. Amount는 자금 증감(지출은 음수), Travel의 도시는 도착 도시다.
public readonly struct EconomyRunEvent
{
    public readonly long Minutes, Amount, FundsAfter;
    public readonly string CityId, ItemId;
    public readonly EconomyRunEventKind Kind;

    public EconomyRunEvent(long _minutes, string _cityId, EconomyRunEventKind _kind, string _itemId, long _amount, long _fundsAfter)
    {
        Minutes = _minutes;
        CityId = _cityId;
        Kind = _kind;
        ItemId = _itemId;
        Amount = _amount;
        FundsAfter = _fundsAfter;
    }
}

public sealed class EconomyItemFlow
{
    public int Purchases, Sales;
    public long Spent, Earned;

    internal void AddPurchase(long _amount)
    {
        Purchases++;
        Spent += _amount;
    }

    internal void AddSale(long _amount)
    {
        Sales++;
        Earned += _amount;
    }
}

public sealed class EconomyRunResult
{
    public int Index, StockSeed, Wagons, Travels;
    public EconomyRunOutcome Outcome;
    public long EndMinutes, FinalFunds;
    // 지출·수입 항목. Disposed는 음수 가치 화물 처분 비용이다.
    public long Bought, Sold, Disposed, TravelCost, WagonCost, Repaid;
    public readonly List<string> Path = new List<string>();
    public readonly List<EconomyRunEvent> Events = new List<EconomyRunEvent>();
    public readonly Dictionary<string, EconomyItemFlow> ItemFlows = new Dictionary<string, EconomyItemFlow>(StringComparer.Ordinal);
    // 24시간×d 시점의 자금·자산(자금+화물 가치). ValidDays까지만 유효하다(조기 상환 판은 상환 시점 이후 제외).
    public long[] DayFunds = Array.Empty<long>(), DayAssets = Array.Empty<long>();
    public int ValidDays;
    internal readonly List<(long Minutes, long Funds, long Assets)> Snapshots = new List<(long Minutes, long Funds, long Assets)>();

    public float EndHours => EndMinutes / (float)PuzzleTimeManager.MINUTES_PER_HOUR;

    internal EconomyItemFlow Flow(string _itemId)
    {
        if (!ItemFlows.TryGetValue(_itemId, out var flow))
        {
            flow = new EconomyItemFlow();
            ItemFlows.Add(_itemId, flow);
        }
        return flow;
    }

    internal void BuildDaySeries(int _dayCount)
    {
        DayFunds = new long[_dayCount + 1];
        DayAssets = new long[_dayCount + 1];
        ValidDays = 0;
        int cursor = 0;
        for (int day = 0; day <= _dayCount; day++)
        {
            long at = (long)day * PuzzleTimeManager.MINUTES_PER_DAY;
            if (Outcome == EconomyRunOutcome.Won && at > EndMinutes)
            {
                break;
            }
            while (cursor + 1 < Snapshots.Count && Snapshots[cursor + 1].Minutes <= at)
            {
                cursor++;
            }
            DayFunds[day] = Snapshots[cursor].Funds;
            DayAssets[day] = Snapshots[cursor].Assets;
            ValidDays = day + 1;
        }
    }
}

public sealed class EconomyDayStat
{
    public int Day, Active;
    public long FundsP10, FundsP50, FundsP90, AssetsP10, AssetsP50, AssetsP90;
}

public sealed class EconomySimulationReport
{
    public EconomySimulationSettings Settings;
    public readonly List<EconomyRunResult> Runs = new List<EconomyRunResult>();
    public EconomyDayStat[] Days = Array.Empty<EconomyDayStat>();
    public readonly Dictionary<EconomyRunOutcome, int> Outcomes = new Dictionary<EconomyRunOutcome, int>();
    public float ClearHoursP10 = float.NaN, ClearHoursP50 = float.NaN, ClearHoursP90 = float.NaN;
    public readonly Dictionary<string, EconomyItemFlow> ItemFlows = new Dictionary<string, EconomyItemFlow>(StringComparer.Ordinal);
    public readonly Dictionary<string, int> CityVisits = new Dictionary<string, int>(StringComparer.Ordinal);

    public int Count(EconomyRunOutcome _outcome)
    {
        return Outcomes.TryGetValue(_outcome, out int count) ? count : 0;
    }

    public double Average(Func<EconomyRunResult, long> _selector)
    {
        return Runs.Count == 0 ? 0 : Runs.Average(_run => (double)_selector(_run));
    }

    internal void Summarize(int _dayCount)
    {
        foreach (var run in Runs)
        {
            Outcomes[run.Outcome] = Count(run.Outcome) + 1;
            foreach (var pair in run.ItemFlows)
            {
                if (!ItemFlows.TryGetValue(pair.Key, out var total))
                {
                    total = new EconomyItemFlow();
                    ItemFlows.Add(pair.Key, total);
                }
                total.Purchases += pair.Value.Purchases;
                total.Sales += pair.Value.Sales;
                total.Spent += pair.Value.Spent;
                total.Earned += pair.Value.Earned;
            }
            // 출발역은 방문으로 세지 않는다.
            foreach (string city in run.Path.Skip(1))
            {
                CityVisits[city] = (CityVisits.TryGetValue(city, out int visits) ? visits : 0) + 1;
            }
        }

        var clearHours = Runs.Where(_run => _run.Outcome == EconomyRunOutcome.Won).Select(_run => (long)_run.EndMinutes).ToList();
        if (clearHours.Count > 0)
        {
            ClearHoursP10 = Percentile(clearHours, 0.1f) / (float)PuzzleTimeManager.MINUTES_PER_HOUR;
            ClearHoursP50 = Percentile(clearHours, 0.5f) / (float)PuzzleTimeManager.MINUTES_PER_HOUR;
            ClearHoursP90 = Percentile(clearHours, 0.9f) / (float)PuzzleTimeManager.MINUTES_PER_HOUR;
        }

        Days = new EconomyDayStat[_dayCount + 1];
        for (int day = 0; day <= _dayCount; day++)
        {
            var active = Runs.Where(_run => day < _run.ValidDays).ToList();
            var funds = active.Select(_run => _run.DayFunds[day]).ToList();
            var assets = active.Select(_run => _run.DayAssets[day]).ToList();
            Days[day] = new EconomyDayStat
            {
                Day = day,
                Active = active.Count,
                FundsP10 = Percentile(funds, 0.1f),
                FundsP50 = Percentile(funds, 0.5f),
                FundsP90 = Percentile(funds, 0.9f),
                AssetsP10 = Percentile(assets, 0.1f),
                AssetsP50 = Percentile(assets, 0.5f),
                AssetsP90 = Percentile(assets, 0.9f)
            };
        }
    }

    // 최근접 순위 백분위. 값이 없으면 0.
    static long Percentile(List<long> _values, float _ratio)
    {
        if (_values.Count == 0)
        {
            return 0;
        }
        var sorted = _values.OrderBy(_value => _value).ToList();
        int index = Math.Min(sorted.Count - 1, Math.Max(0, (int)Math.Ceiling(_ratio * sorted.Count) - 1));
        return sorted[index];
    }
}
