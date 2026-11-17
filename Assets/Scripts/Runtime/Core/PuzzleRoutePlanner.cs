using System;
using System.Collections.Generic;

public static class PuzzleRoutePlanner
{
    public static IReadOnlyList<string> FindFastestPath(TableManager _tables, string _fromCityId, string _toCityId)
    {
        if (_tables == null || string.IsNullOrEmpty(_fromCityId) || string.IsNullOrEmpty(_toCityId))
        {
            return null;
        }

        if (!_tables.HasCity(_fromCityId) || !_tables.HasCity(_toCityId))
        {
            return null;
        }

        if (string.Equals(_fromCityId, _toCityId, StringComparison.Ordinal))
        {
            return new List<string> { _fromCityId };
        }

        var hours = new Dictionary<string, long>(StringComparer.Ordinal) { [_fromCityId] = 0 };
        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            string current = null;
            long best = long.MaxValue;
            foreach (var pair in hours)
            {
                if (!done.Contains(pair.Key) && (pair.Value < best ||
                    (pair.Value == best && (current == null || StringComparer.Ordinal.Compare(pair.Key, current) < 0))))
                {
                    current = pair.Key;
                    best = pair.Value;
                }
            }

            if (current == null)
            {
                break;
            }

            if (string.Equals(current, _toCityId, StringComparison.Ordinal))
            {
                break;
            }

            done.Add(current);

            var routes = _tables.RoutesFrom(current);
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                if (route == null || string.IsNullOrEmpty(route.toCityId) || done.Contains(route.toCityId))
                {
                    continue;
                }

                long travelTime = PuzzleTimeManager.TravelHours(route, _tables.PuzzleTime);
                long arrival = checked(best + travelTime);
                if (!hours.TryGetValue(route.toCityId, out long known) || arrival < known)
                {
                    hours[route.toCityId] = arrival;
                    previous[route.toCityId] = current;
                }
                else if (arrival == known && previous.TryGetValue(route.toCityId, out var existingPrev))
                {
                    if (StringComparer.Ordinal.Compare(current, existingPrev) < 0)
                    {
                        previous[route.toCityId] = current;
                    }
                }
            }
        }

        if (!hours.ContainsKey(_toCityId) || !previous.ContainsKey(_toCityId))
        {
            return null;
        }

        var path = new List<string>();
        string step = _toCityId;
        path.Add(step);
        while (previous.TryGetValue(step, out var prev))
        {
            step = prev;
            path.Add(step);
        }

        if (!string.Equals(step, _fromCityId, StringComparison.Ordinal))
        {
            return null;
        }

        path.Reverse();
        return path;
    }
}
