using System;
using System.Collections.Generic;
using System.Linq;

internal static class PuzzleWagonDamage
{
    const int ATTEMPTS_PER_COUNT = 64;
    const int LARGE_CARGO_CELLS = 3;

    internal static PuzzleBoardData Roll(PuzzleBoardData _board, IEnumerable<PuzzleItemDefinition> _definitions,
        IEnumerable<PuzzleItemDefinition> _owned, System.Random _random)
    {
        // 각 배치를 비트로 기록해 같은 셀을 차지하는 대칭 회전은 한 번만 센다.
        if ((long)_board.width * _board.height > 64)
        {
            return _board;
        }

        int width = _board.width;
        int height = _board.height;
        int cellCount = width * height;
        var baseDamage = _board.blockedCells ?? Array.Empty<PuzzleBoardCellData>();
        ulong original = 0;
        foreach (var cell in baseDamage)
        {
            original |= CellBit(cell.x, cell.y, width);
        }

        var freeCells = Enumerable.Range(0, cellCount).Select(index => 1UL << index)
            .Where(bit => (original & bit) == 0).ToArray();
        var shapes = _definitions.Select(definition => (Definition: definition,
            Placements: Placements(definition, width, height, original)))
            .Where(shape => shape.Placements.Length > 0).ToArray();
        var owned = _owned.ToHashSet();
        var targets = shapes.Where(shape => shape.Definition.Cells.Count >= LARGE_CARGO_CELLS &&
            shape.Placements.Length > 1 && owned.Contains(shape.Definition))
            .Select(shape => shape.Placements).ToArray();
        var preferredCells = freeCells.Where(bit => targets.Length > 0 &&
            Loss(targets, bit) is >= 0.10 and <= 0.25).ToArray();

        int roll = _random.Next(100);
        int desired = roll < 55 ? 3 : roll < 85 ? 4 : 5;
        for (int count = Math.Min(desired, freeCells.Length - 1); count > 0; count--)
        {
            ulong fallback = 0;
            for (int attempt = 0; attempt < ATTEMPTS_PER_COUNT; attempt++)
            {
                ulong damage = preferredCells.Length > 0 && attempt < ATTEMPTS_PER_COUNT / 2
                    ? preferredCells[_random.Next(preferredCells.Length)] : 0;
                for (int added = damage == 0 ? 0 : 1; added < count; added++)
                {
                    ulong bit;
                    do
                    {
                        bit = freeCells[_random.Next(freeCells.Length)];
                    }
                    while ((damage & bit) != 0);
                    damage |= bit;
                }

                if (!shapes.All(shape => shape.Placements.Any(mask => (mask & damage) == 0)) ||
                    !IsConnected(width, height, original | damage))
                {
                    continue;
                }
                if (targets.Length == 0 || Loss(targets, damage) is >= 0.20 and <= 0.40)
                {
                    return WithDamage(_board, original | damage);
                }
                if (fallback == 0)
                {
                    fallback = damage;
                }
            }
            if (fallback != 0)
            {
                return WithDamage(_board, original | fallback);
            }
        }
        return _board;
    }

    static ulong[] Placements(PuzzleItemDefinition _definition, int _width, int _height, ulong _original)
    {
        var result = new HashSet<ulong>();
        for (int turn = 0; turn < PuzzleRotation.TURN_COUNT; turn++)
        {
            var rotation = new PuzzleRotation(_definition.Cells, turn);
            var offsets = _definition.Cells.Select(rotation.GetBoardOffset).ToArray();
            int right = offsets.Max(offset => offset.x);
            int depth = -offsets.Min(offset => offset.y);
            for (int y = depth; y < _height; y++)
            {
                for (int x = 0; x < _width - right; x++)
                {
                    ulong mask = 0;
                    foreach (var offset in offsets)
                    {
                        mask |= CellBit(x + offset.x, y + offset.y, _width);
                    }
                    if ((mask & _original) == 0)
                    {
                        result.Add(mask);
                    }
                }
            }
        }
        return result.ToArray();
    }

    static double Loss(IEnumerable<ulong[]> _shapes, ulong _damage) =>
        _shapes.Average(placements => (double)placements.Count(mask => (mask & _damage) != 0) / placements.Length);

    static bool IsConnected(int _width, int _height, ulong _blocked)
    {
        int cellCount = _width * _height;
        ulong remaining = (ulong.MaxValue >> (64 - cellCount)) & ~_blocked;
        ulong frontier = remaining & unchecked(0UL - remaining);
        while (frontier != 0)
        {
            remaining &= ~frontier;
            ulong neighbors = 0;
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if ((frontier & CellBit(x, y, _width)) == 0)
                    {
                        continue;
                    }
                    if (x > 0) neighbors |= CellBit(x - 1, y, _width);
                    if (x + 1 < _width) neighbors |= CellBit(x + 1, y, _width);
                    if (y > 0) neighbors |= CellBit(x, y - 1, _width);
                    if (y + 1 < _height) neighbors |= CellBit(x, y + 1, _width);
                }
            }
            frontier = neighbors & remaining;
        }
        return remaining == 0;
    }

    static PuzzleBoardData WithDamage(PuzzleBoardData _board, ulong _blocked) => new()
    {
        width = _board.width,
        height = _board.height,
        displayName = _board.displayName,
        description = _board.description,
        startingPlacements = _board.startingPlacements,
        blockedCells = Enumerable.Range(0, _board.width * _board.height)
            .Where(index => (_blocked & (1UL << index)) != 0)
            .Select(index => new PuzzleBoardCellData { x = index % _board.width, y = index / _board.width }).ToArray()
    };

    static ulong CellBit(int _x, int _y, int _width) => 1UL << (_y * _width + _x);
}
