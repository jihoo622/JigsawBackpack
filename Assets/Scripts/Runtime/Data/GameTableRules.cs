using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
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
    // 비어 있지 않으면 콘솔 대신 이 목록에 오류를 모은다(편집 도구의 저장 전 검증).
    readonly List<string> errors;

    public GameTableRules()
    {
    }

    public GameTableRules(List<string> _errors)
    {
        errors = _errors;
    }

    void Check(bool _condition, string _path, string _rule)
    {
        if (_condition)
        {
            return;
        }
        IsValid = false;
        if (errors != null)
        {
            errors.Add($"[{_path}] {_rule}");
            return;
        }
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
        ValidateBasic(_tables);
        Inspect(_tables.Gameplay, nameof(GameplayTableData));
        Inspect(_tables.Flow, nameof(FlowTableData));

        ValidatePlayer(_tables.Gameplay.player, _tables);
        ValidateUpgrades(_tables);
        ValidateScenes(_tables);
        ValidateStockProfiles(_tables);
        ValidatePuzzle(_tables);
        ValidatePuzzleSynergy(_tables);
        ValidateCityActions(_tables);
        ValidatePuzzleTrade(_tables);
        ValidateTime(_tables.PuzzleTime);
        ValidateRoutes(_tables);
    }

    void ValidateBasic(TableManager _tables)
    {
        foreach (var id in BasicDataContract.Numbers)
        {
            Check(_tables.HasBasicNumber(id), "BasicDataTableData.numbers." + id, "필수 수치가 누락되었습니다.");
        }
        foreach (var id in BasicDataContract.Texts)
        {
            Check(_tables.HasBasicText(id), "BasicDataTableData.texts." + id, "필수 문구가 누락되었습니다.");
        }
        foreach (var id in BasicDataContract.Colors)
        {
            Check(_tables.HasBasicColor(id), "BasicDataTableData.colors." + id, "필수 색이 누락되었습니다.");
        }
        foreach (var pair in _tables.BasicNumbers)
        {
            var row = pair.Value;
            string id = pair.Key;
            if (row == null)
            {
                continue;
            }
            bool finite = !double.IsNaN(row.value) && !double.IsInfinity(row.value) && Math.Abs(row.value) <= float.MaxValue;
            Check(finite, "BasicDataTableData.numbers." + id, "유한한 수치가 필요합니다.");
            Check(!row.whole || (row.value >= int.MinValue && row.value <= int.MaxValue && row.value == Math.Truncate(row.value)),
                "BasicDataTableData.numbers." + id, "정수 범위의 값이 필요합니다.");
            Check(!row.positive || row.value > 0, "BasicDataTableData.numbers." + id, "양수가 필요합니다.");
            if (id.StartsWith("layers.GameLayers.", StringComparison.Ordinal))
            {
                Check(row.value >= 0 && row.value < 32, "BasicDataTableData.numbers." + id, "Unity 레이어 번호 범위를 벗어났습니다.");
            }
        }
        foreach (var pair in _tables.BasicTexts)
        {
            var row = pair.Value;
            string id = pair.Key;
            if (row == null)
            {
                continue;
            }
            bool argumentsValid = BasicDataContract.TextArgumentCounts.TryGetValue(id, out var expected) && row.arguments == expected;
            Check(row.value != null && argumentsValid, "BasicDataTableData.texts." + id, "문구와 서식 인자 수가 필요합니다.");
            if (row.value != null && argumentsValid)
            {
                try
                {
                    string.Format(row.value, new object[row.arguments]);
                }
                catch (FormatException)
                {
                    Check(false, "BasicDataTableData.texts." + id, "문구의 서식 인자가 올바르지 않습니다.");
                }
            }
        }
        foreach (var pair in _tables.BasicColors)
        {
            var row = pair.Value;
            string id = pair.Key;
            if (row != null)
            {
                Check(new[] { row.r, row.g, row.b, row.a }.All(_value => !float.IsNaN(_value) && _value >= 0 && _value <= 1),
                    "BasicDataTableData.colors." + id, "RGBA는 0~1 범위여야 합니다.");
            }
        }
        const string EXPIRED_FOOD = "rules.PuzzleInventoryService.expiredFoodPieceId";
        if (_tables.HasBasicText(EXPIRED_FOOD))
        {
            string pieceId = _tables.BasicText(EXPIRED_FOOD).value;
            Reference(pieceId, "BasicDataTableData.texts." + EXPIRED_FOOD, _tables.TryGetPiece(pieceId, out _), "화물");
        }
        const string LOW_FRESHNESS = "rules.freshness.lowThreshold";
        const string MEDIUM_FRESHNESS = "rules.freshness.mediumThreshold";
        if (_tables.HasBasicNumber(LOW_FRESHNESS) && _tables.HasBasicNumber(MEDIUM_FRESHNESS))
        {
            double low = _tables.BasicNumber(LOW_FRESHNESS).value;
            double medium = _tables.BasicNumber(MEDIUM_FRESHNESS).value;
            Check(low >= 0 && low < medium && medium < 100,
                "BasicDataTableData.rules.freshness", "신선도 구간은 0 이상이며 낮음 < 중간 < 100이어야 합니다.");
        }
        const string LOW_FRESHNESS_VALUE = "rules.freshness.lowValuePercent";
        const string MEDIUM_FRESHNESS_VALUE = "rules.freshness.mediumValuePercent";
        const string HIGH_FRESHNESS_VALUE = "rules.freshness.highValuePercent";
        if (_tables.HasBasicNumber(LOW_FRESHNESS_VALUE) && _tables.HasBasicNumber(MEDIUM_FRESHNESS_VALUE) &&
            _tables.HasBasicNumber(HIGH_FRESHNESS_VALUE))
        {
            double low = _tables.BasicNumber(LOW_FRESHNESS_VALUE).value;
            double medium = _tables.BasicNumber(MEDIUM_FRESHNESS_VALUE).value;
            double high = _tables.BasicNumber(HIGH_FRESHNESS_VALUE).value;
            Check(low > 0 && low <= medium && medium <= high && high <= 100,
                "BasicDataTableData.rules.freshness", "가치 배율은 0보다 크고 낮음 ≤ 보통 ≤ 신선함 ≤ 100이어야 합니다.");
        }
    }

    void ValidatePuzzleTrade(TableManager _tables)
    {
        const string PATH = nameof(PuzzleTradeTableData);
        Check(_tables.PuzzleTrade.startingFunds >= 0, PATH + ".startingFunds", "시작 자금은 0 이상이어야 합니다.");
        Check(_tables.PuzzleTrade.gameClearCost > 0, PATH + ".gameClearCost", "클리어 비용은 0보다 커야 합니다.");
        Check(_tables.PuzzleTrade.deadlineHours >= 0, PATH + ".deadlineHours", "기한은 시간 단위 0 이상이어야 합니다. 0이면 무기한입니다.");
        if (!string.IsNullOrEmpty(_tables.PuzzleTrade.debtCityId))
        {
            Reference(_tables.PuzzleTrade.debtCityId, PATH + ".debtCityId", _tables.HasCity(_tables.PuzzleTrade.debtCityId), "상환 도시");
        }
        Check(_tables.PuzzleTrade.wagonRefrigerationCost > 0, PATH + ".wagonRefrigerationCost",
            "객차 냉장 개조 비용은 양수여야 합니다.");
        Check(_tables.PuzzleTrade.wagonAddCostBase > 0, PATH + ".wagonAddCostBase",
            "화물칸 증차 기본 비용은 0보다 커야 합니다.");
        Check(_tables.PuzzleTrade.maxWagonCount >= 1, PATH + ".maxWagonCount",
            "최대 객차 수는 1 이상이어야 합니다.");
        Check(IsFinitePositive(_tables.PuzzleTrade.travelEffectSeconds), PATH + ".travelEffectSeconds",
            "이동 연출 시간은 유한한 양수여야 합니다.");
        Check(IsFinitePositive(_tables.PuzzleTrade.travelArrivalHoldSeconds), PATH + ".travelArrivalHoldSeconds",
            "도착 표시 시간은 유한한 양수여야 합니다.");
        ValidateWagonProducts(_tables.PuzzleTrade.wagonProducts, PATH + ".wagonProducts");
    }

    void ValidateWagonProducts(PuzzleWagonProductData[] _products, string _path)
    {
        if (_products == null || _products.Length == 0)
        {
            Check(false, _path, "화차 상품이 하나 이상 필요합니다.");
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _products.Length; i++)
        {
            var product = _products[i];
            string path = $"{_path}[{i}]";
            if (product == null)
            {
                Check(false, path, "화차 상품 데이터가 필요합니다.");
                continue;
            }

            Check(!string.IsNullOrWhiteSpace(product.id) && ids.Add(product.id), path + ".id",
                "화차 상품 ID는 비어 있지 않고 고유해야 합니다.");
            Check(!string.IsNullOrWhiteSpace(product.displayName), path + ".displayName",
                "화차 상품 이름이 필요합니다.");
            Check(product.price > 0, path + ".price", "화차 상품 가격은 양수여야 합니다.");
            if (!string.IsNullOrEmpty(product.modificationId))
            {
                Reference(product.modificationId, path + ".modificationId",
                    PuzzleWagonModificationRegistry.Contains(product.modificationId), "화차 강화");
            }
        }
    }

    static bool IsFinitePositive(float _value)
    {
        return !float.IsNaN(_value) && !float.IsInfinity(_value) && _value > 0f;
    }

    // 노선은 방향 그래프다. 시작 도시에서 모든 도시로 도달할 수 있어야 한다.
    void ValidateTime(PuzzleTimeTableData _time)
    {
        const string PATH = nameof(PuzzleTimeTableData);
        Check(_time != null, PATH, "시간 테이블이 필요합니다.");
        if (_time == null)
        {
            return;
        }
        Check(!double.IsNaN(_time.pixelsPerHour) && !double.IsInfinity(_time.pixelsPerHour) && _time.pixelsPerHour > 0,
            PATH + ".pixelsPerHour", "픽셀/시간 환산값은 유한한 양수여야 합니다.");
        Check(_time.daylightHours > 0 && _time.nightHours > 0 &&
            _time.daylightHours + _time.nightHours == PuzzleTimeManager.HOURS_PER_DAY, PATH, "일출·야간 합은 하루여야 합니다.");
        Check(_time.startHour >= 0 && _time.startHour < PuzzleTimeManager.HOURS_PER_DAY, PATH + ".startHour", "시작 시각이 범위를 벗어났습니다.");
        Check(_time.restMinutes > 0, PATH + ".restMinutes", "휴식 시간은 양수여야 합니다.");
        Check(_time.daylightStartHour >= 0 && _time.daylightStartHour < PuzzleTimeManager.HOURS_PER_DAY,
            PATH + ".daylightStartHour", "일출 시각이 범위를 벗어났습니다.");
    }

    void ValidateRoutes(TableManager _tables)
    {
        const string PATH = nameof(PuzzleRouteTableData);
        const string ROUTES_PATH = PATH + ".routes";
        Check(_tables.PuzzleRoute.defaultBaseCost >= 0, PATH + ".defaultBaseCost", "기준 기본요금은 0 이상이어야 합니다.");
        Check(!double.IsNaN(_tables.PuzzleRoute.fareDistancePixelsPerUnit) && !double.IsInfinity(_tables.PuzzleRoute.fareDistancePixelsPerUnit) && _tables.PuzzleRoute.fareDistancePixelsPerUnit > 0,
            PATH + ".fareDistancePixelsPerUnit", "요금 거리 1당 지도 길이는 유한한 양수여야 합니다.");
        Check(_tables.PuzzleRoute.costPerCell >= 0, PATH + ".costPerCell", "칸당 이동 단가는 0 이상이어야 합니다.");
        Check(_tables.PuzzleRoute.costPerDistance >= 0, PATH + ".costPerDistance", "거리당 이동 단가는 0 이상이어야 합니다.");
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
            Check(route.distance > 0, path + ".distance", "노선 거리는 0보다 커야 합니다.");
            bool validPoints = route.points != null && route.points.Length >= 4 && route.points.Length % 2 == 0 &&
                route.points.All(_value => !float.IsNaN(_value) && !float.IsInfinity(_value));
            Check(validPoints, path + ".points", "유한한 좌표의 폴라인 x,y 쌍이 두 점 이상 필요합니다.");
            if (validPoints)
            {
                Check(PuzzleTimeManager.PolylineLength(route.points) > 0, path + ".points", "노선 길이는 0보다 커야 합니다.");
                try
                {
                    if (_tables.PuzzleTime != null)
                    {
                        PuzzleTimeManager.TravelMinutes(route, _tables.PuzzleTime);
                    }
                }
                catch (OverflowException)
                {
                    Check(false, path + ".points", "노선 시간이 계산 범위를 초과합니다.");
                }
            }
            if (_tables.HasCity(route.fromCityId) && _tables.HasCity(route.toCityId))
            {
                var from = _tables.CityPrice(route.fromCityId);
                var to = _tables.CityPrice(route.toCityId);
                if (from.regionId == to.regionId)
                {
                    Check(!HasNearbyTradeMatch(_tables, from, to) && !HasNearbyTradeMatch(_tables, to, from),
                        path, "근거리 직결 마을의 공급과 수요가 일치합니다.");
                }
            }
            if (_tables.TryGetRoute(route.toCityId, route.fromCityId, out var reverse))
            {
                Check(route.distance == reverse.distance, path + ".distance", "같은 철도의 왕복 거리는 같아야 합니다.");
                Check(route.highSpeed == reverse.highSpeed, path + ".highSpeed", "같은 철도의 왕복 고속선 구분은 같아야 합니다.");
                Check(route.points != null && reverse.points != null &&
                    route.points.Length % 2 == 0 && route.points.Length == reverse.points.Length &&
                    Enumerable.Range(0, route.points.Length / 2).All(_index =>
                        route.points[_index * 2] == reverse.points[reverse.points.Length - 2 - _index * 2] &&
                        route.points[_index * 2 + 1] == reverse.points[reverse.points.Length - 1 - _index * 2]),
                    path + ".points", "왕복 폴라인은 역순으로 일치해야 합니다.");
            }
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
        Check(_tables.PuzzleSynergy.activationIntervalHours > 0, "PuzzleSynergyTableData.activationIntervalHours",
            "시너지 적용 주기는 양의 정수 시간이어야 합니다.");
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
            Check(string.IsNullOrEmpty(rule.excludedTargetTag) ||
                  rule.excludedTargetTag.Split(',').All(tag => tags.Contains(tag.Trim())),
                path + ".excludedTargetTag", "제외 대상 태그는 쉼표로 구분된 등록 태그여야 합니다.");
            Reference(rule.neighborTag, path + ".neighborTag", tags.Contains(rule.neighborTag), "이웃 태그");
            Check(string.IsNullOrEmpty(rule.excludedNeighborTag) ||
                  rule.excludedNeighborTag.Split(',').All(tag => tags.Contains(tag.Trim())),
                path + ".excludedNeighborTag", "제외 이웃 태그는 쉼표로 구분된 등록 태그여야 합니다.");
            Reference(rule.descriptionTag, path + ".descriptionTag", tags.Contains(rule.descriptionTag),
                "설명 표시 태그", _allowEmpty: true);
            Check(rule.scope is nameof(PuzzleSynergyScope.Train) or nameof(PuzzleSynergyScope.Market)
                or nameof(PuzzleSynergyScope.Both), path + ".scope", "적용 범위는 Train, Market, Both 중 하나여야 합니다.");
            Check(rule.spatialRange is nameof(PuzzleSynergyRange.Adjacent) or nameof(PuzzleSynergyRange.Board),
                path + ".spatialRange", "공간 범위는 Adjacent 또는 Board여야 합니다.");
            Check(rule.multiplierPercent > 0, path + ".multiplierPercent", "배율은 양의 정수 백분율이어야 합니다.");
            Check(!string.IsNullOrWhiteSpace(rule.description), path + ".description", "시너지 설명이 필요합니다.");
        }
        ValidateAnimalSynergy(_tables, tags);
        const string FRESHNESS_PATH = "PuzzleSynergyTableData.freshness";
        var freshness = _tables.PuzzleSynergy.freshness;
        Check(freshness != null, FRESHNESS_PATH, "신선도 설정이 필요합니다.");
        if (freshness != null)
        {
            Check(freshness.settlementIntervalHours > 0, FRESHNESS_PATH + ".settlementIntervalHours",
                "신선도 반영 주기는 양의 정수 시간이어야 합니다.");
            Reference(freshness.refrigerationTag, FRESHNESS_PATH + ".refrigerationTag",
                tags.Contains(freshness.refrigerationTag), "냉장 태그");
            Check(freshness.refrigerationReductionPercent > 0 &&
                freshness.refrigerationReductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
                FRESHNESS_PATH + ".refrigerationReductionPercent", "인접 냉장 감소율은 1~100%여야 합니다.");
            Check(freshness.wagonRefrigerationReductionPercent > 0 &&
                freshness.wagonRefrigerationReductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
                FRESHNESS_PATH + ".wagonRefrigerationReductionPercent", "객차 냉장 감소율은 1~100%여야 합니다.");
            Check(freshness.saltPreservationReductionPercent >= 0 &&
                freshness.saltPreservationReductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
                FRESHNESS_PATH + ".saltPreservationReductionPercent", "소금 보존 감소율은 0~100%여야 합니다.");
        }
    }

    void ValidateAnimalSynergy(TableManager _tables, HashSet<string> _tags)
    {
        const string PATH = "PuzzleSynergyTableData.animals";
        var animals = _tables.PuzzleSynergy.animals;
        Check(animals != null, PATH, "동물 시너지 설정이 필요합니다.");
        if (animals == null)
        {
            return;
        }

        Reference(animals.animalTag, PATH + ".animalTag", _tags.Contains(animals.animalTag), "동물 태그");
        Reference(animals.wasteTag, PATH + ".wasteTag", _tags.Contains(animals.wasteTag), "배설물 태그");
        Check(animals.animalTag != animals.wasteTag, PATH + ".wasteTag", "동물과 배설물은 서로 다른 태그여야 합니다.");
        bool hasWaste = _tables.TryGetPiece(animals.wastePieceId, out var waste);
        Reference(animals.wastePieceId, PATH + ".wastePieceId", hasWaste, "배설물 화물");
        if (hasWaste)
        {
            var wasteTags = waste.tags ?? Array.Empty<string>();
            Check(wasteTags.Contains(animals.wasteTag) && !wasteTags.Contains(animals.animalTag),
                PATH + ".wastePieceId", "배설물 화물은 배설물 태그를 가져야 하며 동물 태그를 가질 수 없습니다.");
        }
        Check(animals.wasteIntervalDays > 0, PATH + ".wasteIntervalDays", "배설 주기는 양의 정수 일수여야 합니다.");
        Check(animals.wasteCountPerAnimal > 0, PATH + ".wasteCountPerAnimal", "배설물 수량은 양수여야 합니다.");
        ValidateAnimalEffectDescription(animals.wasteDescription, PATH + ".wasteDescription",
            animals.wasteIntervalDays, string.Empty, animals.wasteCountPerAnimal);
        Check(animals.breedingHours > 0, PATH + ".breedingHours", "번식 시간은 양의 정수 시간이어야 합니다.");
        Check(animals.completionHoldHours > 0, PATH + ".completionHoldHours",
            "완료 전 대기 시간은 양의 정수 시간이어야 합니다.");
        Check(animals.completionHoldHours > 0 && animals.breedingHours > 0 &&
                animals.completionHoldHours < animals.breedingHours,
            PATH + ".completionHoldHours", "완료 전 대기 시간은 번식 시간보다 짧아야 합니다.");
        Check(animals.completionHoldHours > 0 && animals.wasteIntervalDays > 0 &&
                (long)animals.completionHoldHours < (long)animals.wasteIntervalDays * PuzzleTimeManager.HOURS_PER_DAY,
            PATH + ".completionHoldHours", "완료 전 대기 시간은 배설 주기보다 짧아야 합니다.");

        var breeding = animals.breeding ?? Array.Empty<PuzzleAnimalBreedingData>();
        for (int i = 0; i < breeding.Length; i++)
        {
            var rule = breeding[i];
            string path = $"{PATH}.breeding[{i}]";
            if (rule == null)
            {
                continue;
            }
            ValidateBreedingPiece(_tables, rule.parentPieceId, animals.animalTag, path + ".parentPieceId", _isParent: true);
            ValidateBreedingPiece(_tables, rule.offspringPieceId, animals.animalTag, path + ".offspringPieceId");
            Check(rule.offspringCount > 0, path + ".offspringCount", "자식 수량은 양수여야 합니다.");
            ValidateAnimalEffectDescription(rule.description, path + ".description", string.Empty, string.Empty, rule.offspringCount);
        }

        foreach (var piece in _tables.Pieces)
        {
            var tags = piece.tags ?? Array.Empty<string>();
            if (tags.Contains(animals.animalTag) && !tags.Contains(PuzzleItemDefinition.BABY_TAG))
            {
                Check(_tables.TryGetAnimalBreeding(piece.id, out _), PATH + ".breeding",
                    $"성체 동물 품목 '{piece.id}'의 번식 규칙이 필요합니다.");
            }
        }
    }

    void ValidateAnimalEffectDescription(string _description, string _path, params object[] _arguments)
    {
        bool hasDescription = !string.IsNullOrWhiteSpace(_description);
        Check(hasDescription, _path, "동물 효과 설명이 필요합니다.");
        if (!hasDescription)
        {
            return;
        }

        try
        {
            _ = string.Format(_description, _arguments);
            // 이스케이프한 중괄호 안의 번호는 실제 인자 참조로 세지 않는다.
            var markers = Enumerable.Range(0, _arguments.Length).Select(_index => $"⟦{_index}⟧").ToArray();
            string formatted = string.Format(_description, (object[])markers);
            Check(markers.All(_marker => formatted.Contains(_marker)), _path,
                "설명에 모든 규칙 값의 서식 인자({0}, {1}, {2})가 필요합니다.");
        }
        catch (FormatException)
        {
            Check(false, _path, "설명의 서식 인자가 올바르지 않습니다.");
        }
    }

    void ValidateBreedingPiece(TableManager _tables, string _id, string _animalTag, string _path, bool _isParent = false)
    {
        bool exists = _tables.TryGetPiece(_id, out var piece);
        Reference(_id, _path, exists, "번식 화물");
        if (exists)
        {
            var tags = piece.tags ?? Array.Empty<string>();
            Check(tags.Contains(_animalTag), _path, "번식 화물은 동물 태그를 가져야 합니다.");
            Check(!_isParent || !tags.Contains(PuzzleItemDefinition.BABY_TAG), _path,
                "Baby 태그가 있는 동물은 번식할 수 없습니다.");
        }
    }

    // 마을별 추가 행동. 도시·조각·태그 참조와 같은 도시 같은 종류의 중복 등록을 검사하고,
    // 도축과 개조는 완성 방식이 다르므로 변환·시간 규칙을 유형별로 분리해 검증한다.
    void ValidateCityActions(TableManager _tables)
    {
        const string PATH = nameof(PuzzleCityActionTableData);
        var actions = _tables.PuzzleCityAction.actions ?? Array.Empty<PuzzleCityActionData>();
        var knownTags = new HashSet<string>(
            _tables.Pieces.SelectMany(item => item.tags ?? Array.Empty<string>()), StringComparer.Ordinal);
        var typesByCity = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var mappedInputs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var outputTags = new HashSet<string>(StringComparer.Ordinal);
        var outputIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            if (action == null)
            {
                continue;
            }

            string path = $"{PATH}.actions[{i}]";
            bool knownType = TableManager.TryGetActionType(action, out var type);
            Check(knownType, path + ".type",
                $"행동 종류는 {string.Join(", ", Enum.GetNames(typeof(PuzzleCityActionType)))} 중 하나여야 합니다.");
            Check(!string.IsNullOrWhiteSpace(action.title), path + ".title", "행동 이름이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(action.description), path + ".description", "행동 설명이 필요합니다.");
            Check(!knownType || type != PuzzleCityActionType.Butchery || action.workMinutes > 0,
                path + ".workMinutes", "도축 작업 시간은 0보다 큰 분 단위여야 합니다.");
            Check(!knownType || type != PuzzleCityActionType.Modification || action.workMinutes == 0,
                path + ".workMinutes", "개조는 즉시 처리하므로 workMinutes가 0이어야 합니다.");
            Check(!knownType || type != PuzzleCityActionType.Butchery || action.cost >= 0,
                path + ".cost", "도축 확인 1회당 비용은 0 이상의 정수여야 합니다.");
            Check(!knownType || type != PuzzleCityActionType.Butchery ||
                (!string.IsNullOrWhiteSpace(action.inputTag) && !string.IsNullOrWhiteSpace(action.outputTag) &&
                (action.conversions ?? Array.Empty<PuzzleActionConversionData>()).Length > 0),
                path, "도축은 행동 차원의 입력·산출 태그와 변환 규칙이 필요합니다.");
            Check(!knownType || type == PuzzleCityActionType.Modification ||
                (action.modes ?? Array.Empty<PuzzleActionModeData>()).Length == 0,
                path, "모드는 개조 행동만 가질 수 있습니다.");

            var cityIds = action.cityIds ?? Array.Empty<string>();
            Check(cityIds.Length > 0, path + ".cityIds", "행동을 여는 도시가 하나 이상 필요합니다.");
            for (int c = 0; c < cityIds.Length; c++)
            {
                string cityPath = $"{path}.cityIds[{c}]";
                Reference(cityIds[c], cityPath, _tables.HasCity(cityIds[c]), "도시");
                if (string.IsNullOrEmpty(cityIds[c]) || !knownType)
                {
                    continue;
                }
                if (!typesByCity.TryGetValue(cityIds[c], out var types))
                {
                    types = new HashSet<string>(StringComparer.Ordinal);
                    typesByCity.Add(cityIds[c], types);
                }
                Check(types.Add(type.ToString()), cityPath, "같은 도시에서 같은 종류의 행동은 하나만 쓸 수 있습니다.");
            }

            var board = action.board;
            Check(board != null, path + ".board", "작업 보드 설정이 필요합니다.");
            if (board != null)
            {
                Check(board.width > 0, path + ".board.width", "작업 보드 너비는 0보다 커야 합니다.");
                Check(board.height > 0, path + ".board.height", "작업 보드 높이는 0보다 커야 합니다.");
                Check((board.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>()).Length == 0,
                    path + ".board.startingPlacements", "작업 보드는 빈 판으로 시작해야 합니다.");
            }

            if (knownType && type == PuzzleCityActionType.Butchery)
            {
                ValidateActionConversions(_tables, type, action.inputTag, action.outputTag,
                    action.conversions, path, false);
            }
            ValidateModificationModes(_tables, type, knownType, action, knownTags, path,
                mappedInputs, outputTags, outputIds);
        }

        ValidateModificationCoverage(_tables, mappedInputs, outputTags, outputIds);
    }

    void ValidateModificationModes(TableManager _tables, PuzzleCityActionType _type, bool _knownType,
        PuzzleCityActionData _action, HashSet<string> _knownTags, string _path,
        Dictionary<string, HashSet<string>> _mappedInputsByTag, HashSet<string> _outputTags, HashSet<string> _outputIds)
    {
        bool isModification = _knownType && _type == PuzzleCityActionType.Modification;
        var modes = _action.modes ?? Array.Empty<PuzzleActionModeData>();
        Check(!isModification || modes.Length > 0, _path + ".modes", "개조는 모드가 하나 이상 필요합니다.");
        Check(!isModification || (string.IsNullOrWhiteSpace(_action.inputTag) &&
            string.IsNullOrWhiteSpace(_action.outputTag) &&
            (_action.conversions ?? Array.Empty<PuzzleActionConversionData>()).Length == 0),
            _path, "개조는 입력·산출 태그와 변환을 모드별로만 가질 수 있습니다.");
        if (modes.Length == 0)
        {
            return;
        }

        var modeIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            if (mode == null)
            {
                Check(false, $"{_path}.modes[{i}]", "모드 항목이 필요합니다.");
                continue;
            }
            string path = _path + ".modes." + (string.IsNullOrWhiteSpace(mode.id) ? i.ToString() : mode.id);
            Check(!string.IsNullOrWhiteSpace(mode.id) && modeIds.Add(mode.id), $"{_path}.modes[{i}].id",
                "행동 안에서 고유한 모드 ID가 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(mode.displayName), path + ".displayName", "모드 이름이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(mode.description), path + ".description", "모드 설명이 필요합니다.");
            Check(mode.cost >= 0, path + ".cost", "개조 모드 확인 1회당 비용은 0 이상의 정수여야 합니다.");
            Reference(mode.inputTag, path + ".inputTag", _knownTags.Contains(mode.inputTag ?? string.Empty), "입력 태그");
            Reference(mode.outputTag, path + ".outputTag", _knownTags.Contains(mode.outputTag ?? string.Empty), "산출 태그");
            bool sameTag = !string.IsNullOrEmpty(mode.inputTag) && mode.inputTag == mode.outputTag;
            Check(!sameTag, path + ".outputTag", "입력 태그와 산출 태그는 달라야 합니다.");
            ValidateActionConversions(_tables, _type, mode.inputTag, mode.outputTag, mode.conversions,
                path, isModification);
            if (!string.IsNullOrEmpty(mode.inputTag) && !string.IsNullOrEmpty(mode.outputTag))
            {
                if (!_mappedInputsByTag.TryGetValue(mode.inputTag, out var mapped))
                {
                    mapped = new HashSet<string>(StringComparer.Ordinal);
                    _mappedInputsByTag.Add(mode.inputTag, mapped);
                }
                foreach (var conversion in mode.conversions ?? Array.Empty<PuzzleActionConversionData>())
                {
                    if (conversion != null && !string.IsNullOrEmpty(conversion.inputPieceId))
                    {
                        mapped.Add(conversion.inputPieceId);
                    }
                    if (conversion != null && !string.IsNullOrEmpty(conversion.outputPieceId))
                    {
                        _outputIds.Add(conversion.outputPieceId);
                    }
                }
                _outputTags.Add(mode.outputTag);
            }
        }
    }

    // 개조 변환 규칙. 입력과 출력은 같은 모양·같은 기본 가치의 1:1 정의 교체이며 결과 품목은 시장 재고 후보가 아니어야 한다.
    void ValidateActionConversions(TableManager _tables, PuzzleCityActionType _type, string _inputTag,
        string _outputTag, PuzzleActionConversionData[] _conversions, string _path, bool _definitionSwap)
    {
        var conversions = _conversions ?? Array.Empty<PuzzleActionConversionData>();
        Check(conversions.Length > 0, _path + ".conversions", "변환 규칙이 하나 이상 필요합니다.");
        var inputPieceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int c = 0; c < conversions.Length; c++)
        {
            var conversion = conversions[c];
            if (conversion == null)
            {
                Check(false, $"{_path}.conversions[{c}]", "변환 규칙이 누락되었습니다.");
                continue;
            }

            string conversionPath = $"{_path}.conversions[{c}]";
            bool hasInput = _tables.TryGetPiece(conversion.inputPieceId, out var inputPiece);
            bool hasOutput = _tables.TryGetPiece(conversion.outputPieceId, out var outputPiece);
            Reference(conversion.inputPieceId, conversionPath + ".inputPieceId", hasInput, "입력 화물");
            Reference(conversion.outputPieceId, conversionPath + ".outputPieceId", hasOutput, "산출 화물");
            Check(inputPieceIds.Add(conversion.inputPieceId ?? string.Empty), conversionPath + ".inputPieceId",
                "같은 입력 화물의 변환 규칙은 하나만 둘 수 있습니다.");
            Check(!_definitionSwap || conversion.outputCount == 1, conversionPath + ".outputCount",
                "개조 변환의 산출 개수는 입력 하나당 하나여야 합니다.");
            Check(_type != PuzzleCityActionType.Butchery || conversion.outputCount > 0,
                conversionPath + ".outputCount", "산출 개수는 0보다 커야 합니다.");
            Check(hasInput && (inputPiece.tags ?? Array.Empty<string>()).Contains(_inputTag),
                conversionPath + ".inputPieceId", "입력 화물은 입력 태그를 가져야 합니다.");
            if (_type == PuzzleCityActionType.Butchery && hasInput)
            {
                Check(!(inputPiece.tags ?? Array.Empty<string>()).Contains(PuzzleItemDefinition.BABY_TAG),
                    conversionPath + ".inputPieceId", "Baby 태그가 있는 화물은 도축할 수 없습니다.");
            }
            if (_definitionSwap && hasInput && hasOutput)
            {
                Check(!(inputPiece.tags ?? Array.Empty<string>()).Contains(_outputTag),
                    conversionPath + ".inputPieceId", "입력 화물은 이미 산출 태그를 가진 품목일 수 없습니다.");
                Check(!string.Equals(outputPiece.id, inputPiece.id, StringComparison.Ordinal),
                    conversionPath + ".outputPieceId", "입력과 산출은 서로 다른 품목이어야 합니다.");
            }
            Check(hasOutput && (outputPiece.tags ?? Array.Empty<string>()).Contains(_outputTag),
                conversionPath + ".outputPieceId", "산출 화물은 산출 태그를 가져야 합니다.");
            if (_definitionSwap && hasInput && hasOutput)
            {
                Check(HoldsAllTags(outputPiece, inputPiece), conversionPath + ".outputPieceId",
                    "산출 화물은 입력 화물의 모든 태그를 유지해야 합니다.");
                Check(SamePieceGeometry(inputPiece, outputPiece), conversionPath + ".outputPieceId",
                    "산출 화물은 입력 화물과 같은 셀 좌표와 slotCount를 가져야 합니다.");
                Check(inputPiece.baseValue == outputPiece.baseValue, conversionPath + ".outputPieceId",
                    "산출 화물의 기본 가치는 입력 화물과 같아야 합니다.");
            }
        }
    }

    // 모든 개조 입력 품목이 모드 매핑을 갖는지 검사한다. 제외하려면 품목에 exemptFromModification을 명시해야 한다.
    // 결과 태그를 이미 가진 품목(개조 결과)은 입력 후보가 아니라서 매핑이 필요 없다.
    void ValidateModificationCoverage(TableManager _tables, Dictionary<string, HashSet<string>> _mappedInputsByTag,
        HashSet<string> _outputTags, HashSet<string> _outputIds)
    {
        foreach (var piece in _tables.Pieces)
        {
            var tags = piece.tags ?? Array.Empty<string>();
            bool isOutput = tags.Any(_tag => _outputTags.Contains(_tag));
            if (isOutput || piece.exemptFromModification)
            {
                continue;
            }
            foreach (var pair in _mappedInputsByTag)
            {
                if (tags.Contains(pair.Key) && !pair.Value.Contains(piece.id))
                {
                    Check(false, "PuzzleCityActionTableData.modes.conversions",
                        $"'{pair.Key}' 태그 품목 '{piece.id}'의 개조 변환 매핑이 없습니다. 제외하려면 exemptFromModification을 설정하세요.");
                }
            }
        }

        // 개조 결과 품목은 시장 무작위 재고·고정 재고 후보에 편입되지 않는다.
        foreach (var profile in _tables.Puzzle.stockProfiles ?? Array.Empty<PuzzleMarketStockData>())
        {
            foreach (string id in profile?.pieceIds ?? Array.Empty<string>())
            {
                Check(!_outputIds.Contains(id), "PuzzleTableData.stockProfiles." + profile.id + ".pieceIds",
                    "개조 결과 품목 '" + id + "'는 시장 재고 후보에 둘 수 없습니다.");
            }
        }
        foreach (var city in _tables.Cities)
        {
            foreach (var placement in city.market?.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
            {
                Check(placement == null || !_outputIds.Contains(placement.pieceId ?? string.Empty),
                    "PuzzleTableData.cities." + city.id + ".market.startingPlacements",
                    "개조 결과 품목 '" + placement.pieceId + "'는 시장 고정 재고에 둘 수 없습니다.");
            }
        }
    }

    static bool HoldsAllTags(PuzzlePieceData _output, PuzzlePieceData _input)
    {
        var outputTags = _output.tags ?? Array.Empty<string>();
        foreach (string tag in _input.tags ?? Array.Empty<string>())
        {
            if (!outputTags.Contains(tag))
            {
                return false;
            }
        }
        return true;
    }

    // 정의 교체 변환은 회전 전 로컬 셀 좌표 집합과 slotCount가 같아야 점유를 그대로 유지할 수 있다.
    static bool SamePieceGeometry(PuzzlePieceData _input, PuzzlePieceData _output)
    {
        if (_input.slotCount != _output.slotCount)
        {
            return false;
        }
        var input = (_input.cells ?? Array.Empty<PuzzleCellData>())
            .OrderBy(_cell => _cell.y).ThenBy(_cell => _cell.x)
            .Select(_cell => new Vector2Int(_cell.x, _cell.y)).ToArray();
        var output = (_output.cells ?? Array.Empty<PuzzleCellData>())
            .OrderBy(_cell => _cell.y).ThenBy(_cell => _cell.x)
            .Select(_cell => new Vector2Int(_cell.x, _cell.y)).ToArray();
        return input.Length == output.Length && !input.Where((_, index) => input[index] != output[index]).Any();
    }

    void ValidateStockProfiles(TableManager _tables)
    {
        foreach (var profile in _tables.Puzzle.stockProfiles ?? Array.Empty<PuzzleMarketStockData>())
        {
            if (profile == null)
            {
                continue;
            }
            string path = "PuzzleTableData.stockProfiles." + profile.id;
            var candidates = profile.pieceIds ?? Array.Empty<string>();
            Check(candidates.Length > 0 && candidates.Distinct(StringComparer.Ordinal).Count() == candidates.Length,
                path + ".pieceIds", "중복 없는 상품 후보가 필요합니다.");
            foreach (string id in candidates)
            {
                Reference(id, path + ".pieceIds", _tables.TryGetPiece(id, out _), "상품");
            }
            var slots = profile.slots ?? Array.Empty<PuzzleMarketStockSlotData>();
            Check(slots.Length > 0, path + ".slots", "재고 배치 슬롯이 필요합니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in slots)
            {
                Check(slot != null, path + ".slots", "슬롯 항목이 필요합니다.");
                if (slot == null)
                {
                    continue;
                }
                Check(!string.IsNullOrWhiteSpace(slot.id) && ids.Add(slot.id), path + ".slots.id", "프로필 안에서 고유한 슬롯 ID가 필요합니다.");
                Check(slot.x >= 0 && slot.y >= 0 && slot.width > 0 && slot.height > 0,
                    path + ".slots." + slot.id, "슬롯 좌표와 크기가 올바르지 않습니다.");
            }
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
                string path = $"{PATH}.pieces[{i}]";
                ValidateGrowth(_tables, piece, path);
                var shape = ValidatePiece(piece, maxSide, path, _tables);
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
        if (!string.IsNullOrEmpty(puzzle.planningTargetCityId))
        {
            Reference(puzzle.planningTargetCityId, PATH + ".planningTargetCityId",
                _tables.HasCity(puzzle.planningTargetCityId), "예정 목적지");
        }
        var knownTags = new HashSet<string>(
            _tables.Pieces.SelectMany(item => item.tags ?? Array.Empty<string>()), StringComparer.Ordinal);
        foreach (var region in _tables.Puzzle.regions ?? Array.Empty<PuzzleRegionData>())
        {
            if (region == null)
            {
                continue;
            }
            string regionPath = PATH + ".regions[" + region.id + "]";
            Check(region.mapLabelPriority >= 0, regionPath + ".mapLabelPriority", "지도 지역 우선순위는 0 이상의 명시적 값이어야 합니다.");
            Check(!string.IsNullOrWhiteSpace(region.displayName), regionPath, "지역 이름이 필요합니다.");
            Check(region.pricePercent > 0, regionPath + ".pricePercent", "지역 물가 배율은 양수여야 합니다.");
            Check(region.tradeHub || knownTags.Contains(region.specialtyTag ?? string.Empty), regionPath,
                "생산 지역의 대표 특산품 태그가 필요합니다.");
            foreach (string tag in region.productionTags ?? Array.Empty<string>())
            {
                Check(knownTags.Contains(tag), regionPath + ".productionTags", "등록된 생산 태그가 필요합니다.");
            }
            Check(region.tradeHub || (region.productionTags ?? Array.Empty<string>()).Contains(region.specialtyTag),
                regionPath, "지역 특산품은 지역 생산 목록에 있어야 합니다.");
        }
        Check((_tables.Puzzle.regions ?? Array.Empty<PuzzleRegionData>()).Length > 0, PATH + ".regions",
            "지역이 하나 이상 필요합니다.");
        for (int i = 0; i < cities.Length; i++)
        {
            var city = cities[i];
            if (city == null)
            {
                continue;
            }

            string cityPath = $"{PATH}.cities[{i}]";
            Check(city.mapLabelImportance >= 0, cityPath + ".mapLabelImportance", "지도 역 중요도는 0 이상의 명시적 값이어야 합니다.");
            Check(!string.IsNullOrWhiteSpace(city.displayName), cityPath + ".displayName", "도시 이름이 필요합니다.");
            Check(city.buyPercentOfBaseValue > 0, cityPath + ".buyPercentOfBaseValue", "기본 구매 배율은 0보다 커야 합니다.");
            Check(city.sellPercentOfFinalValue > 0, cityPath + ".sellPercentOfFinalValue", "기본 판매 배율은 0보다 커야 합니다.");
            Reference(city.regionId, cityPath + ".regionId", _tables.TryGetRegion(city.regionId, out _), "지역");
            ValidateCityTagPrices(city, knownTags, cityPath + ".tagPrices");
            ValidateCitySpecialty(_tables, city, knownTags, cityPath);
            ValidateBoard(_tables, city.market, shapes, placementIds, cityPath + ".market");
            ValidateGeneralStock(_tables, city, shapes, placementIds, cityPath + ".stock");
        }
    }

    void ValidateGrowth(TableManager _tables, PuzzlePieceData _piece, string _path)
    {
        if (string.IsNullOrEmpty(_piece.adultPieceId) && string.IsNullOrEmpty(_piece.growthDescription) &&
            _piece.growthHours == 0 && _piece.growthBlockedHours == 0)
        {
            return;
        }

        Check((_piece.tags ?? Array.Empty<string>()).Contains(PuzzleItemDefinition.BABY_TAG),
            _path + ".tags", "성장하는 화물에는 Baby 태그가 필요합니다.");
        Check(_piece.growthHours > 0, _path + ".growthHours", "성장 시간은 양수여야 합니다.");
        Check(_piece.growthBlockedHours > 0 && _piece.growthBlockedHours <= _piece.growthHours,
            _path + ".growthBlockedHours", "성장 전 공간을 확인하는 시간은 1시간 이상이며 성장 시간 이하여야 합니다.");
        ValidateAnimalEffectDescription(_piece.growthDescription, _path + ".growthDescription",
            string.Empty, string.Empty, string.Empty);
        bool hasAdult = _tables.TryGetPiece(_piece.adultPieceId, out var adult);
        Reference(_piece.adultPieceId, _path + ".adultPieceId", hasAdult, "성체 화물");
        if (hasAdult)
        {
            Check(!(adult.tags ?? Array.Empty<string>()).Contains(PuzzleItemDefinition.BABY_TAG),
                _path + ".adultPieceId", "성체 화물에는 Baby 태그를 둘 수 없습니다.");
        }
    }

    static IEnumerable<PuzzleCityTagPriceData> MatchingTagPrices(PuzzleCityData _city, IReadOnlyCollection<string> _tags)
    {
        return (_city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>()).Where(_price => _price != null && _tags.Contains(_price.tag));
    }

    static bool HasNearbyTradeMatch(TableManager _tables, PuzzleCityData _from, PuzzleCityData _to)
    {
        _tables.TryGetStockProfile(_from.regionalStockId, out var regional);
        var supply = (_from.market?.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
            .Where(_placement => _placement != null).Select(_placement => _placement.pieceId)
            .Concat(regional?.pieceIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal);
        _tables.TryGetRegion(_from.regionId, out var region);
        foreach (string id in supply)
        {
            if (!_tables.TryGetPiece(id, out var piece))
            {
                continue;
            }
            var tags = piece.tags ?? Array.Empty<string>();
            // 실제 거래와 같은 합산 배율로 판정한다.
            var fromRate = PuzzleTradeRate.Combine(_from, MatchingTagPrices(_from, tags));
            bool nativeSupply = (fromRate.BuyPercent < 100 && fromRate.SellPercent < 100) ||
                (region != null && !string.IsNullOrEmpty(region.specialtyTag) && tags.Contains(region.specialtyTag));
            if (!nativeSupply)
            {
                continue;
            }
            var toRate = PuzzleTradeRate.Combine(_to, MatchingTagPrices(_to, tags));
            if (toRate.Tag != null && toRate.BuyPercent > 100 && toRate.SellPercent > 100)
            {
                return true;
            }
        }
        return false;
    }

    void ValidateCitySpecialty(TableManager _tables, PuzzleCityData _city, HashSet<string> _knownTags, string _path)
    {
        if (_tables.TryGetRegion(_city.regionId, out var region) && region.tradeHub)
        {
            return;
        }
        string tag = _city.specialtyTag;
        Check(!string.IsNullOrWhiteSpace(tag) && _knownTags.Contains(tag), _path + ".specialtyTag",
            "아이템 정의에 있는 대표 특산품 태그가 필요합니다.");
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (region != null)
        {
            Check((region.productionTags ?? Array.Empty<string>()).Contains(tag), _path + ".specialtyTag",
                "마을 특산품은 소속 지역의 생산 태그에 있어야 합니다.");
        }
        var prices = _city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>();
        Check(prices.Any(_price => _price != null && _price.tag == tag && _price.buyPercent < 100 && _price.sellPercent < 100),
            _path + ".specialtyTag", "대표 특산품은 구매·판매 배율이 모두 100 미만인 풍부한 품목이어야 합니다.");
        var stock = _city.market?.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>();
        Check(stock.Length > 0, _path + ".market.startingPlacements", "대표 특산품의 고정 재고가 필요합니다.");
        foreach (var placement in stock)
        {
            if (placement != null && _tables.TryGetPiece(placement.pieceId, out var piece))
            {
                Check((piece.tags ?? Array.Empty<string>()).Contains(tag), _path + ".market.startingPlacements",
                    $"고정 재고 '{placement.pieceId}'는 대표 특산품 태그 '{tag}'를 가져야 합니다.");
            }
        }
    }

    void ValidateGeneralStock(TableManager _tables, PuzzleCityData _city,
        Dictionary<PuzzlePieceData, Vector2Int[]> _shapes, HashSet<string> _placementIds, string _path)
    {
        bool hasGeneral = _tables.TryGetStockProfile(_city.generalStockId, out var general);
        bool hasRegional = _tables.TryGetStockProfile(_city.regionalStockId, out var regional);
        Reference(_city.generalStockId, _path + ".generalStockId", hasGeneral, "일반 재고 프로필");
        Reference(_city.regionalStockId, _path + ".regionalStockId", hasRegional, "지역 재고 프로필");
        Check(general != null && regional != null, _path, "지역 재고와 일반 재고 설정이 필요합니다.");
        if (general == null || regional == null)
        {
            return;
        }
        foreach (var group in new[] { regional, general })
        {
            var ids = group.pieceIds ?? Array.Empty<string>();
            Check(ids.Length > 0 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
                _path, "각 추첨 그룹에는 중복 없는 후보가 필요합니다.");
            Check((group.slots ?? Array.Empty<PuzzleMarketStockSlotData>()).Length > 0,
                _path, "각 추첨 그룹에는 배치 슬롯이 필요합니다.");
        }
        if (_tables.TryGetRegion(_city.regionId, out var region) && !region.tradeHub)
        {
            foreach (string id in regional.pieceIds ?? Array.Empty<string>())
            {
                if (_tables.TryGetPiece(id, out var definition))
                {
                    Check((definition.tags ?? Array.Empty<string>()).Contains(region.specialtyTag),
                        _path, "지역 재고는 지역 특산품 태그를 가져야 합니다.");
                }
            }
        }
        var stock = new PuzzleMarketStockData
        {
            pieceIds = (general.pieceIds ?? Array.Empty<string>()).Concat(regional.pieceIds ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal).ToArray(),
            slots = (general.slots ?? Array.Empty<PuzzleMarketStockSlotData>())
                .Concat(regional.slots ?? Array.Empty<PuzzleMarketStockSlotData>()).ToArray()
        };
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
            if (exists)
            {
                Check(!piece.excludeFromMarket, _path + ".pieceIds", $"'{id}'는 시장 재고에 둘 수 없습니다.");
            }
            if (exists && _shapes.TryGetValue(piece, out var shape))
            {
                shapes.Add(shape);
            }
        }
        var occupied = new HashSet<Vector2Int>();
        foreach (var placement in _city.market.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
        {
            if (placement == null || !_tables.TryGetPiece(placement.pieceId, out var piece))
            {
                continue;
            }
            Check(!piece.excludeFromMarket, _path, $"'{placement.pieceId}'는 시장 고정 재고에 둘 수 없습니다.");
            if (!_shapes.TryGetValue(piece, out var shape) || placement.clockwiseTurns < 0 ||
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
            Check(!string.IsNullOrWhiteSpace(slot.id) && _placementIds.Add(_city.id + "." + slot.id), path + ".id",
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

    static int MaxBoardSide(PuzzleBoardData _board)
    {
        return _board == null ? 0 : Math.Max(_board.width, _board.height);
    }

    Vector2Int[] ValidatePiece(PuzzlePieceData _piece, int _maxSide, string _path, TableManager _tables)
    {
        Check(_piece.baseValue != -1, _path + ".baseValue", "상품 기본 가치가 필요합니다.");
        Check(!string.IsNullOrWhiteSpace(_piece.displayName), _path + ".displayName", "아이템 이름이 필요합니다.");
        Check(!string.IsNullOrWhiteSpace(_piece.description), _path + ".description", "조각 설명이 필요합니다.");
        Check(!string.IsNullOrWhiteSpace(_piece.visualKey), _path + ".visualKey", "표시 에셋 키가 필요합니다.");
        ValidateItemTags(_piece.tags, _path + ".tags");
        string path = _path + ".cells";
        var cells = _piece.cells ?? Array.Empty<PuzzleCellData>();
        Check(cells.Length > 0, path, "슬롯이 하나 이상 필요합니다.");
        Check(_piece.slotCount > 0 && _piece.slotCount == cells.Length, _path + ".slotCount",
            $"슬롯 수는 1 이상이며 cells 항목 수({cells.Length})와 일치해야 합니다.");
        const string DURABILITY_MAX = "rules.durability.maximum";
        if (_tables.HasBasicNumber(DURABILITY_MAX))
        {
            Check(_piece.durability >= 1 && _piece.durability <= _tables.BasicNumber(DURABILITY_MAX).value,
                _path + ".durability", "내구도 시작값은 1 이상이며 기초데이터의 상한 이하여야 합니다.");
        }

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

    void ValidatePlayer(PlayerData _player, TableManager _tables)
    {
        if (_player == null)
        {
            Check(false, "Gameplay.player", "플레이어 설정이 필요합니다.");
            return;
        }
        Positive(_player.maxHealth, "Gameplay.player.maxHealth");
        Positive(_player.moveSpeed, "Gameplay.player.moveSpeed");
        const string DASH_MAX = "rules.player.dashChargesMaximum";
        if (_tables.HasBasicNumber(DASH_MAX))
        {
            Check(_player.dashCharges >= 0 && _player.dashCharges <= _tables.BasicNumber(DASH_MAX).value,
                "Gameplay.player.dashCharges", "대시 횟수는 0 이상이며 기초데이터의 상한 이하여야 합니다.");
        }
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
