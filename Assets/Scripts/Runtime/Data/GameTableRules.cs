using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 검증과 파생 값 계산의 공통 구현. DTO와 로더에는 게임 규칙을 넣지 않는다.
// 규칙을 추가할 때: Check로 항목별 오류를 모은다(첫 오류에서 멈추지 않는다) → IsValid로 보고 → 사용처는 RequireValid로 차단.
public sealed class GameTableRules
{
    static readonly Vector2Int[] CELL_NEIGHBORS =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    public bool IsValid { get; private set; } = true;

    void Check(bool _condition, string _path, string _rule)
    {
        if (_condition)
        {
            return;
        }
        IsValid = false;
        Debug.LogError($"테이블 오류 [{_path}]: {_rule}");
    }

    // 키 인덱스 구성. 빈 키·중복 키·null 항목을 항목별로 보고한다.
    public Dictionary<string, T> Index<T>(T[] _entries, Func<T, string> _key, string _path) where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        Check(_entries != null, _path, "목록이 필요합니다.");
        var entries = _entries ?? Array.Empty<T>();
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            string id = entry == null ? null : _key(entry);
            if (string.IsNullOrWhiteSpace(id))
            {
                Check(false, $"{_path}[{i}]", "항목과 비어 있지 않은 키가 필요합니다.");
                continue;
            }
            Check(result.TryAdd(id, entry), $"{_path}[{i}]", $"키 '{id}'가 중복되었습니다.");
        }
        return result;
    }

    // 모든 숫자의 유한성과 누락된 중첩 설정을 재귀로 검사한다. DTO에 새 클래스를 추가하면 자동으로 덮인다.
    void Inspect(object _value, string _path)
    {
        if (_value is string)
        {
            return;
        }
        if (_value == null)
        {
            Check(false, _path, "값이 누락되었습니다.");
            return;
        }
        if (_value is float number)
        {
            Check(!float.IsNaN(number) && !float.IsInfinity(number), _path, "유한한 숫자여야 합니다.");
            return;
        }
        var type = _value.GetType();
        if (type.IsPrimitive || type.IsEnum)
        {
            return;
        }
        if (_value is IEnumerable array)
        {
            int i = 0;
            foreach (var item in array)
            {
                Inspect(item, _path + "[" + i++ + "]");
            }
            return;
        }
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            // 문자열은 선택 필드일 수 있다. 필수 키와 참조는 Reference에서 별도 검사한다.
            if (field.FieldType == typeof(string))
            {
                continue;
            }
            Inspect(field.GetValue(_value), _path + "." + field.Name);
        }
    }

    void Positive(float _value, string _path)
    {
        Check(_value > 0, _path, "0보다 커야 합니다.");
    }
    void Nonnegative(float _value, string _path)
    {
        Check(_value >= 0, _path, "0 이상이어야 합니다.");
    }
    void Fraction(float _value, string _path)
    {
        Check(_value >= 0 && _value <= 1, _path, "0~1이어야 합니다.");
    }
    void IntRange(int _value, int _min, int _max, string _path)
    {
        Check(_value >= _min && _value <= _max, _path, $"{_min}~{_max} 범위여야 합니다.");
    }

    // 다른 테이블·같은 테이블의 키 참조. 빈 값이 허용되면 allowEmpty로 표현한다.
    void Reference(string _id, string _path, bool _resolved, string _label)
    {
        Reference(_id, _path, _resolved, _label, false);
    }

    void Reference(string _id, string _path, bool _resolved, string _label, bool _allowEmpty)
    {
        if (string.IsNullOrEmpty(_id))
        {
            Check(_allowEmpty, _path, $"{_label} 키가 필요합니다.");
            return;
        }
        Check(_resolved, _path, $"{_label} '{_id}'가 테이블에 없습니다.");
    }

    // 강화 단계처럼 1~maxLevel 항목이 하나씩 있어야 하는 목록 검사.
    public void ValidateLevelSet<T>(T[] _entries, Func<T, int> _getLevel, int _maxLevel, string _path) where T : class
    {
        Check(_entries != null && _entries.Length == _maxLevel, _path, $"단계 1~{_maxLevel} 항목이 모두 필요합니다.");
        if (_entries == null || _maxLevel <= 0)
        {
            return;
        }
        var seen = new HashSet<int>();
        foreach (var entry in _entries)
        {
            if (entry == null)
            {
                continue;
            }
            int level = _getLevel(entry);
            IntRange(level, 1, _maxLevel, $"{_path}.level({level})");
            Check(seen.Add(level), $"{_path}.level({level})", "단계가 중복되었습니다.");
        }
    }

    public void Validate(TableManager _tables)
    {
        Inspect(_tables.Gameplay, nameof(GameplayTableData));
        Inspect(_tables.Flow, nameof(FlowTableData));

        ValidatePlayer(_tables.Gameplay.player);
        ValidateUpgrades(_tables);
        ValidateScenes(_tables);
        ValidatePuzzle(_tables);
        ValidateItemPrices(_tables);
        ValidatePuzzleSynergy(_tables);
        ValidatePuzzleTrade(_tables);
        ValidateRoutes(_tables);
        ValidateDebtSchedule(_tables.PuzzleTurn);
    }

    void ValidateDebtSchedule(PuzzleTurnTableData _turn)
    {
        const string PATH = nameof(PuzzleTurnTableData);
        const string PAYMENTS_PATH = PATH + ".debtPayments";
        int interval = _turn.debtInterval;
        Check(interval > 0, PATH + ".debtInterval", "차감 주기는 0보다 커야 합니다.");
        var payments = _turn.debtPayments ?? Array.Empty<PuzzleDebtPaymentData>();
        Check(payments.Length > 0, PAYMENTS_PATH, "빚 차감 일정이 하나 이상 필요합니다.");

        long lastMove = (long)interval * payments.Length;
        Check(lastMove <= int.MaxValue, PAYMENTS_PATH, "마지막 차감 시점이 이동 횟수의 범위를 초과합니다.");
        var seenMoves = new HashSet<int>();
        for (int i = 0; i < payments.Length; i++)
        {
            var payment = payments[i];
            string path = $"{PAYMENTS_PATH}[{i}]";
            if (payment == null)
            {
                Check(false, path, "빚 차감 항목이 필요합니다.");
                continue;
            }

            int move = payment.moveCount;
            Check(move > 0, path + ".moveCount", "이동 횟수는 0보다 커야 합니다.");
            Check(payment.amount > 0, path + ".amount", "차감액은 0보다 커야 합니다.");
            Check(seenMoves.Add(move), path + ".moveCount", $"이동 {move}회 차감이 중복되었습니다.");
            if (interval > 0 && move > 0)
            {
                Check(move % interval == 0 && move <= lastMove, path + ".moveCount",
                    $"이동 횟수는 {interval}회 주기에 맞고 마지막 차감 시점({lastMove}회) 이내여야 합니다.");
            }
        }

        if (interval > 0)
        {
            for (long move = interval; move <= lastMove && move <= int.MaxValue; move += interval)
            {
                Check(seenMoves.Contains((int)move), PAYMENTS_PATH, $"이동 {move}회의 차감 일정이 누락되었습니다.");
            }
        }
    }

    void ValidatePuzzleTrade(TableManager _tables)
    {
        const string PATH = nameof(PuzzleTradeTableData);
        Check(_tables.PuzzleTrade.startingFunds >= 0, PATH + ".startingFunds", "시작 자금은 0 이상이어야 합니다.");
        Check(_tables.PuzzleTrade.gameClearCost > 0, PATH + ".gameClearCost", "클리어 비용은 0보다 커야 합니다.");
        Check(_tables.PuzzleTrade.purchaseMarkupPercent > 0, PATH + ".purchaseMarkupPercent",
            "공통 구매 배율은 0보다 커야 합니다.");
        Check(_tables.PuzzleTrade.wagonAddCostBase > 0, PATH + ".wagonAddCostBase",
            "화물칸 증차 기본 비용은 0보다 커야 합니다.");
    }

    // 노선은 방향 그래프다. 시작 도시에서 모든 도시로 도달할 수 있어야 한다.
    void ValidateRoutes(TableManager _tables)
    {
        const string PATH = nameof(PuzzleRouteTableData);
        const string ROUTES_PATH = PATH + ".routes";
        Check(_tables.PuzzleRoute.travelTurns == 1, PATH + ".travelTurns", "이동 턴 수는 1이어야 합니다.");
        Check(_tables.PuzzleRoute.costPerCell >= 0, PATH + ".costPerCell", "칸당 이동 단가는 0 이상이어야 합니다.");
        var routes = _tables.PuzzleRoute.routes ?? Array.Empty<PuzzleRouteData>();
        var seen = new HashSet<(string From, string To)>();
        for (int i = 0; i < routes.Length; i++)
        {
            var route = routes[i];
            string path = $"{ROUTES_PATH}[{i}]";
            if (route == null)
            {
                Check(false, path, "노선 항목이 필요합니다.");
                continue;
            }

            Reference(route.fromCityId, path + ".fromCityId", _tables.HasCity(route.fromCityId), "출발 도시");
            Reference(route.toCityId, path + ".toCityId", _tables.HasCity(route.toCityId), "도착 도시");
            Check(route.fromCityId != route.toCityId, path, "출발 도시와 도착 도시는 달라야 합니다.");
            Check(route.cost >= 0, path + ".cost", "이동 비용은 0 이상이어야 합니다.");
            if (!string.IsNullOrEmpty(route.fromCityId) && !string.IsNullOrEmpty(route.toCityId))
            {
                Check(seen.Add((route.fromCityId, route.toCityId)), path, $"노선 '{route.fromCityId}'→'{route.toCityId}'가 중복되었습니다.");
            }
        }

        string startCity = _tables.Puzzle.startCity;
        if (!_tables.HasCity(startCity))
        {
            return;
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(startCity);
        reachable.Add(startCity);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            foreach (var route in _tables.RoutesFrom(current))
            {
                if (_tables.HasCity(route.toCityId) && reachable.Add(route.toCityId))
                {
                    pending.Enqueue(route.toCityId);
                }
            }
        }

        foreach (var city in _tables.Cities)
        {
            if (city != null && !string.IsNullOrEmpty(city.id))
            {
                Check(reachable.Contains(city.id), $"{PATH}.reachability",
                    $"시작 도시 '{startCity}'에서 도시 '{city.id}'(으)로 갈 수 있는 경로가 없습니다.");
            }
        }
    }

    void ValidateCityTagPrices(PuzzleCityData _city, HashSet<string> _knownTags, string _path)
    {
        Check(_city.tagPrices != null, _path, "태그별 가격 목록이 필요합니다.");
        var entries = _city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>();
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var priorities = new HashSet<int>();
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            string path = $"{_path}[{i}]";
            if (entry == null)
            {
                Check(false, path, "태그별 가격 항목이 필요합니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.tag) || entry.tag != entry.tag.Trim())
            {
                Check(false, path + ".tag", "앞뒤 공백이 없는 비어 있지 않은 태그가 필요합니다.");
            }
            else
            {
                Check(_knownTags.Contains(entry.tag), path + ".tag", $"태그 '{entry.tag}'가 아이템 정의에 없습니다.");
                Check(tags.Add(entry.tag), path + ".tag", $"태그 '{entry.tag}'가 도시 안에서 중복되었습니다.");
            }

            Check(entry.priority >= 0, path + ".priority", "우선순위가 누락되었거나 0보다 작습니다.");
            if (entry.priority >= 0)
            {
                Check(priorities.Add(entry.priority), path + ".priority",
                    $"우선순위 {entry.priority}가 도시 안에서 중복되었습니다.");
            }

            Check(entry.buyPercent > 0, path + ".buyPercent", "구매 배율은 0보다 커야 합니다.");
            Check(entry.sellPercent > 0, path + ".sellPercent", "판매 배율은 0보다 커야 합니다.");
        }
    }

    void ValidatePuzzleSynergy(TableManager _tables)
    {
        var tags = new HashSet<string>(_tables.Pieces.SelectMany(item => item.tags ?? Array.Empty<string>()),
            StringComparer.Ordinal);
        var rules = _tables.PuzzleSynergy.rules ?? Array.Empty<PuzzleSynergyRuleData>();
        for (int i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule == null)
            {
                continue;
            }

            string path = $"PuzzleSynergyTableData.rules[{i}]";
            Check(string.IsNullOrEmpty(rule.targetTag) != string.IsNullOrEmpty(rule.excludedTargetTag), path,
                "targetTag와 excludedTargetTag 중 하나를 지정해야 합니다.");
            Reference(rule.targetTag, path + ".targetTag", tags.Contains(rule.targetTag), "대상 태그", _allowEmpty: true);
            Reference(rule.excludedTargetTag, path + ".excludedTargetTag", tags.Contains(rule.excludedTargetTag),
                "제외 대상 태그", _allowEmpty: true);
            Reference(rule.neighborTag, path + ".neighborTag", tags.Contains(rule.neighborTag), "이웃 태그");
            Reference(rule.excludedNeighborTag, path + ".excludedNeighborTag", tags.Contains(rule.excludedNeighborTag),
                "제외 이웃 태그", _allowEmpty: true);
            Check(rule.scope is nameof(PuzzleSynergyScope.Train) or nameof(PuzzleSynergyScope.Market)
                or nameof(PuzzleSynergyScope.Both), path + ".scope", "적용 범위는 Train, Market, Both 중 하나여야 합니다.");
            Check(rule.multiplierPercent > 0, path + ".multiplierPercent", "배율은 양의 정수 백분율이어야 합니다.");
        }
    }

    void ValidatePuzzle(TableManager _tables)
    {
        const string PATH = nameof(PuzzleTableData);
        var puzzle = _tables.Puzzle;
        var cities = puzzle.cities ?? Array.Empty<PuzzleCityData>();
        int maxSide = cities.Select(city => MaxBoardSide(city?.market)).Append(MaxBoardSide(puzzle.train)).Max();

        var pieces = puzzle.pieces ?? Array.Empty<PuzzlePieceData>();
        var shapes = new Dictionary<PuzzlePieceData, Vector2Int[]>();
        Check(pieces.Length > 0, PATH + ".pieces", "조각 목록이 비어 있습니다.");
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            if (piece != null)
            {
                var shape = ValidatePiece(piece, maxSide, $"{PATH}.pieces[{i}]");
                if (shape != null)
                {
                    shapes[piece] = shape;
                }
            }
        }

        var placementIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateBoard(_tables, puzzle.train, shapes, placementIds, PATH + ".train");
        Check(cities.Length > 0, PATH + ".cities", "도시가 하나 이상 필요합니다.");
        Reference(puzzle.startCity, PATH + ".startCity", _tables.HasCity(puzzle.startCity), "시작 도시");
        var knownTags = new HashSet<string>(
            _tables.Pieces.SelectMany(item => item.tags ?? Array.Empty<string>()), StringComparer.Ordinal);
        for (int i = 0; i < cities.Length; i++)
        {
            var city = cities[i];
            if (city == null)
            {
                continue;
            }

            string cityPath = $"{PATH}.cities[{i}]";
            Check(!string.IsNullOrWhiteSpace(city.displayName), cityPath + ".displayName", "도시 이름이 필요합니다.");
            Check(city.buyPercentOfBaseValue > 0, cityPath + ".buyPercentOfBaseValue", "기본 구매 배율은 0보다 커야 합니다.");
            Check(city.sellPercentOfFinalValue > 0, cityPath + ".sellPercentOfFinalValue", "기본 판매 배율은 0보다 커야 합니다.");
            ValidateCityTagPrices(city, knownTags, cityPath + ".tagPrices");
            ValidateBoard(_tables, city.market, shapes, placementIds, cityPath + ".market");
            ValidateGeneralStock(_tables, city, shapes, placementIds, cityPath + ".generalStock");
        }
    }

    void ValidateGeneralStock(TableManager _tables, PuzzleCityData _city,
        Dictionary<PuzzlePieceData, Vector2Int[]> _shapes, HashSet<string> _placementIds, string _path)
    {
        var stock = _city.generalStock;
        Check(stock != null, _path, "일반 물품 추첨 설정이 필요합니다.");
        if (stock == null || _city.market == null)
        {
            return;
        }
        var slots = stock.slots ?? Array.Empty<PuzzleMarketStockSlotData>();
        var candidates = stock.pieceIds ?? Array.Empty<string>();
        Check(slots.Length > 0, _path + ".slots", "일반 물품 배치 슬롯이 필요합니다.");
        Check(candidates.Length > 0, _path + ".pieceIds", "일반 물품 추첨 후보가 필요합니다.");
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        var shapes = new List<Vector2Int[]>();
        foreach (string id in candidates)
        {
            Check(candidateIds.Add(id), _path + ".pieceIds", $"후보 '{id}'가 중복되었습니다.");
            bool exists = _tables.TryGetPiece(id, out var piece);
            Reference(id, _path + ".pieceIds", exists, "일반 물품");
            if (exists && _shapes.TryGetValue(piece, out var shape))
            {
                shapes.Add(shape);
            }
        }
        var occupied = new HashSet<Vector2Int>();
        foreach (var placement in _city.market.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
        {
            if (placement == null || !_tables.TryGetPiece(placement.pieceId, out var piece) ||
                !_shapes.TryGetValue(piece, out var shape) || placement.clockwiseTurns < 0 ||
                placement.clockwiseTurns >= PuzzleRotation.TURN_COUNT)
            {
                continue;
            }
            var rotation = new PuzzleRotation(shape, placement.clockwiseTurns);
            foreach (var cell in shape)
            {
                occupied.Add(new Vector2Int(placement.x, placement.y) + rotation.GetBoardOffset(cell));
            }
        }
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            string path = $"{_path}.slots[{i}]";
            Check(slot != null, path, "일반 물품 슬롯이 필요합니다.");
            if (slot == null)
            {
                continue;
            }
            Check(!string.IsNullOrWhiteSpace(slot.id) && _placementIds.Add(slot.id), path + ".id",
                "모든 시작 배치와 슬롯에서 고유한 ID가 필요합니다.");
            bool valid = slot.width > 0 && slot.height > 0 && slot.x >= 0 && slot.y >= 0 &&
                (long)slot.x + slot.width <= _city.market.width && slot.y < _city.market.height &&
                (long)slot.y - slot.height + 1 >= 0;
            Check(valid, path, "일반 물품 슬롯 전체가 시장 격자 안에 있어야 합니다.");
            if (!valid)
            {
                continue;
            }
            foreach (var shape in shapes)
            {
                var rotation = new PuzzleRotation(shape, 0);
                Check(shape.All(cell => rotation.GetBoardOffset(cell).x < slot.width &&
                    -rotation.GetBoardOffset(cell).y < slot.height), path, "모든 후보가 각 슬롯 크기 안에 들어가야 합니다.");
            }
            for (int x = 0; x < slot.width; x++)
            {
                for (int y = 0; y < slot.height; y++)
                {
                    Check(occupied.Add(new Vector2Int(slot.x + x, slot.y - y)), path,
                        "일반 물품 슬롯이 특산품 또는 다른 슬롯과 겹칩니다.");
                }
            }
        }
    }

    void ValidateItemPrices(TableManager _tables)
    {
        const string PATH = nameof(PuzzleItemPriceTableData) + ".prices";
        var prices = _tables.PuzzleItemPrice.prices ?? Array.Empty<PuzzleItemPriceData>();
        for (int i = 0; i < prices.Length; i++)
        {
            var price = prices[i];
            string path = $"{PATH}[{i}]";
            if (price == null)
            {
                Check(false, path, "아이템 가격 항목이 필요합니다.");
                continue;
            }

            Reference(price.pieceId, path + ".pieceId", _tables.TryGetPiece(price.pieceId, out _), "아이템");
            Check(price.baseValue >= 0, path + ".baseValue", "아이템 기본 가격은 0 이상이어야 합니다.");
        }

        foreach (var piece in _tables.Pieces)
        {
            Check(_tables.HasItemPrice(piece.id), PATH, $"아이템 '{piece.id}'의 가격이 필요합니다.");
        }
    }

    static int MaxBoardSide(PuzzleBoardData _board)
    {
        return _board == null ? 0 : Math.Max(_board.width, _board.height);
    }

    Vector2Int[] ValidatePiece(PuzzlePieceData _piece, int _maxSide, string _path)
    {
        Check(!string.IsNullOrWhiteSpace(_piece.displayName), _path + ".displayName", "아이템 이름이 필요합니다.");
        Check(!string.IsNullOrWhiteSpace(_piece.description), _path + ".description", "조각 설명이 필요합니다.");
        Check(!string.IsNullOrWhiteSpace(_piece.visualKey), _path + ".visualKey", "표시 에셋 키가 필요합니다.");
        ValidateItemTags(_piece.tags, _path + ".tags");
        string path = _path + ".cells";
        var cells = _piece.cells ?? Array.Empty<PuzzleCellData>();
        Check(cells.Length > 0, path, "슬롯이 하나 이상 필요합니다.");
        Check(_piece.slotCount > 0 && _piece.slotCount == cells.Length, _path + ".slotCount",
            $"슬롯 수는 1 이상이며 cells 항목 수({cells.Length})와 일치해야 합니다.");

        var positions = new HashSet<Vector2Int>();
        bool hasZeroX = false;
        bool hasZeroY = false;
        bool positionsInBounds = true;
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            string cellPath = $"{path}[{i}]";
            if (cell == null)
            {
                Check(false, cellPath, "슬롯이 누락되었습니다.");
                positionsInBounds = false;
                continue;
            }

            ValidateFlatEdge(cell.north, cellPath + ".north");
            ValidateFlatEdge(cell.east, cellPath + ".east");
            ValidateFlatEdge(cell.south, cellPath + ".south");
            ValidateFlatEdge(cell.west, cellPath + ".west");

            bool xInBounds = cell.x >= 0 && cell.x < _maxSide;
            bool yInBounds = cell.y >= 0 && cell.y < _maxSide;
            Check(xInBounds, cellPath + ".x", "x 좌표는 0 이상이고 보드의 최대 변 길이보다 작아야 합니다.");
            Check(yInBounds, cellPath + ".y", "y 좌표는 0 이상이고 보드의 최대 변 길이보다 작아야 합니다.");
            if (!xInBounds || !yInBounds)
            {
                positionsInBounds = false;
                continue;
            }

            Check(positions.Add(new Vector2Int(cell.x, cell.y)), cellPath, $"좌표 ({cell.x}, {cell.y})가 중복되었습니다.");
            hasZeroX |= cell.x == 0;
            hasZeroY |= cell.y == 0;
        }

        if (positions.Count > 0 && positionsInBounds)
        {
            Check(hasZeroX && hasZeroY, path, "최소 x와 최소 y는 각각 0이어야 합니다.");
            Check(AreCellsConnected(positions), path, "모든 슬롯이 상하좌우로 연결되어야 합니다.");
        }

        return positionsInBounds && positions.Count > 0 ? positions.ToArray() : null;
    }

    void ValidateItemTags(string[] _tags, string _path)
    {
        Check(_tags != null, _path, "태그 목록이 필요합니다.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tags = _tags ?? Array.Empty<string>();
        for (int i = 0; i < tags.Length; i++)
        {
            string tag = tags[i];
            string path = $"{_path}[{i}]";
            if (string.IsNullOrWhiteSpace(tag) || tag != tag.Trim())
            {
                Check(false, path, "앞뒤 공백이 없는 비어 있지 않은 태그가 필요합니다.");
                continue;
            }

            Check(seen.Add(tag), path, $"태그 '{tag}'가 중복되었습니다.");
        }
    }

    internal void ValidateItemVisuals(IReadOnlyCollection<PuzzleItemDefinition> _items,
        Func<string, bool> _hasVisual, string _viewPath)
    {
        foreach (var item in _items)
        {
            Check(_hasVisual(item.VisualKey), $"PuzzleTableData.pieces.{item.Id}.visualKey",
                $"'{item.VisualKey}'에 해당하는 스프라이트가 '{_viewPath}'에 없습니다.");
        }
    }

    void ValidateBoard(TableManager _tables, PuzzleBoardData _board, Dictionary<PuzzlePieceData, Vector2Int[]> _shapes,
        HashSet<string> _placementIds, string _path)
    {
        Check(_board != null, _path, "보드 설정이 필요합니다.");
        if (_board == null)
        {
            return;
        }

        Check(_board.width > 0, _path + ".width", "격자 너비는 0보다 커야 합니다.");
        Check(_board.height > 0, _path + ".height", "격자 높이는 0보다 커야 합니다.");
        Check(_board.startingPlacements != null, _path + ".startingPlacements", "시작 배치 목록이 필요합니다.");
        var placements = _board.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>();
        var occupied = new Dictionary<Vector2Int, string>();
        for (int i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            string path = $"{_path}.startingPlacements[{i}]";
            if (placement == null)
            {
                Check(false, path, "시작 배치가 누락되었습니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(placement.id))
            {
                Check(false, path + ".id", "시작 배치 ID가 필요합니다.");
            }
            else
            {
                Check(_placementIds.Add(placement.id), path + ".id", $"시작 배치 ID '{placement.id}'가 중복되었습니다.");
            }
            bool hasPiece = _tables.TryGetPiece(placement.pieceId, out var piece);
            Reference(placement.pieceId, path + ".pieceId", hasPiece, "화물 종류");
            bool validX = placement.x >= 0 && placement.x < _board.width;
            bool validY = placement.y >= 0 && placement.y < _board.height;
            bool validRotation = placement.clockwiseTurns >= 0 && placement.clockwiseTurns < PuzzleRotation.TURN_COUNT;
            Check(validX, path + ".x", "x 좌표는 0 이상이고 격자 너비보다 작아야 합니다.");
            Check(validY, path + ".y", "y 좌표는 0 이상이고 격자 높이보다 작아야 합니다.");
            IntRange(placement.clockwiseTurns, 0, PuzzleRotation.TURN_COUNT - 1, path + ".clockwiseTurns");
            if (!hasPiece || !validX || !validY || !validRotation || !_shapes.TryGetValue(piece, out var shape))
            {
                continue;
            }

            var rotation = new PuzzleRotation(shape, placement.clockwiseTurns);
            foreach (var cell in shape)
            {
                var offset = rotation.GetBoardOffset(cell);
                long x = (long)placement.x + offset.x;
                long y = (long)placement.y + offset.y;
                bool inBounds = x >= 0 && x < _board.width && y >= 0 && y < _board.height;
                Check(inBounds, path, $"회전한 슬롯 ({x}, {y})이 격자 밖에 있습니다.");
                if (!inBounds)
                {
                    continue;
                }

                var position = new Vector2Int((int)x, (int)y);
                if (!occupied.TryAdd(position, path))
                {
                    Check(false, path, $"슬롯 {position}이 {occupied[position]}의 시작 배치와 겹칩니다.");
                }
            }
        }
    }

    void ValidateFlatEdge(string _edge, string _path)
    {
        Check(_edge == "Flat", _path, "화물의 변은 Flat이어야 합니다.");
    }

    static bool AreCellsConnected(HashSet<Vector2Int> _positions)
    {
        var unvisited = new HashSet<Vector2Int>(_positions);
        var pending = new Queue<Vector2Int>();
        foreach (var position in _positions)
        {
            pending.Enqueue(position);
            unvisited.Remove(position);
            break;
        }

        while (pending.Count > 0)
        {
            var position = pending.Dequeue();
            foreach (var offset in CELL_NEIGHBORS)
            {
                var adjacent = position + offset;
                if (unvisited.Remove(adjacent))
                {
                    pending.Enqueue(adjacent);
                }
            }
        }

        return unvisited.Count == 0;
    }

    void ValidatePlayer(PlayerData _player)
    {
        if (_player == null)
        {
            Check(false, "Gameplay.player", "플레이어 설정이 필요합니다.");
            return;
        }
        Positive(_player.maxHealth, "Gameplay.player.maxHealth");
        Positive(_player.moveSpeed, "Gameplay.player.moveSpeed");
        IntRange(_player.dashCharges, 0, 10, "Gameplay.player.dashCharges");
        Nonnegative(_player.dashCooldown, "Gameplay.player.dashCooldown");
        Positive(_player.interactRange, "Gameplay.player.interactRange");
    }

    void ValidateUpgrades(TableManager _tables)
    {
        var list = _tables.Gameplay.upgrades ?? Array.Empty<UpgradeData>();
        Check(list.Length > 0, "GameplayTableData.upgrades", "해금 효과 목록이 비어 있습니다.");
        foreach (var upgrade in list)
        {
            if (upgrade == null || string.IsNullOrEmpty(upgrade.id))
            {
                continue;
            }
            string path = "Gameplay.upgrades." + upgrade.id;
            Check(!string.IsNullOrEmpty(upgrade.displayName) || !string.IsNullOrEmpty(upgrade.id), path, "표시 이름이 필요합니다.");
            Check(!string.IsNullOrEmpty(upgrade.description), path + ".description", "설명이 비어 있으면 테이블 오류다(표기가 단일 출처이므로 코드에 폴백을 두지 않는다).");
            Check(TryCategory(upgrade.category, out _), path + ".category", $"EffectCategory 이름이어야 합니다: {upgrade.category}");
            Reference(upgrade.nextUpgrade, path + ".nextUpgrade", _tables.TryGetUpgrade(upgrade.nextUpgrade, out _), "다음 해금", _allowEmpty: true);
            Check(!string.Equals(upgrade.nextUpgrade, upgrade.id, StringComparison.Ordinal), path + ".nextUpgrade", "자기 자신을 다음 해금으로 연결했습니다.");
        }
        Reference(_tables.FirstUpgradeId, "Gameplay.sequence.firstUpgrade", _tables.TryGetUpgrade(_tables.FirstUpgradeId, out _), "시작 해금");

        // 사슬이 순환하면 다음 해금을 영원히 계산하지 못한다.
        var walked = new HashSet<string>(StringComparer.Ordinal);
        for (string id = _tables.FirstUpgradeId; !string.IsNullOrEmpty(id);)
        {
            if (!walked.Add(id))
            {
                Check(false, "Gameplay.upgrades." + id + ".nextUpgrade", "해금 사슬에 순환이 있습니다.");
                break;
            }
            if (!_tables.TryGetUpgrade(id, out var node))
            {
                break;
            }
            id = node.nextUpgrade;
        }
    }

    void ValidateScenes(TableManager _tables)
    {
        var list = _tables.Flow.scenes ?? Array.Empty<SceneLinkData>();
        Check(list.Length > 0, "FlowTableData.scenes", "씬 진행 목록이 비어 있습니다.");
        foreach (var link in list)
        {
            if (link == null || string.IsNullOrEmpty(link.scene))
            {
                continue;
            }
            string path = "Flow.scenes." + link.scene;
            Reference(link.nextScene, path + ".nextScene", _tables.TryGetScene(link.nextScene, out _), "다음 씬", _allowEmpty: true);
            Reference(link.restartScene, path + ".restartScene", _tables.TryGetScene(link.restartScene, out _), "재시작 씬", _allowEmpty: true);
            Check(!string.Equals(link.nextScene, link.scene, StringComparison.Ordinal), path + ".nextScene", "자기 자신을 다음 씬으로 연결했습니다.");
        }
    }

    // 문자열 키를 열거형으로 해석한다. 실패하면 false(호출 측이 Check로 보고한다).
    public static bool TryCategory(string _key, out EffectCategory _category)
    {
        _category = EffectCategory.Utility;
        return !string.IsNullOrEmpty(_key) && Enum.TryParse(_key, out _category);
    }

    public static EffectCategory Category(string _key)
    {
        return TryCategory(_key, out var category) ? category : EffectCategory.Utility;
    }
}
