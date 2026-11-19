using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UnityEngine;

public sealed class GameTableIssue
{
    public string Path { get; }
    public string Message { get; }
    public string PieceId { get; }
    public string Field { get; }

    public GameTableIssue(string _path, string _message, string _pieceId = null, string _field = null)
    {
        Path = _path;
        Message = _message;
        PieceId = _pieceId;
        Field = _field;
    }

    public override string ToString()
    {
        return "[" + Path + "] " + Message;
    }
}

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
    readonly List<GameTableIssue> issues;

    public GameTableRules()
    {
    }

    public GameTableRules(List<string> _errors) : this(_errors, null)
    {
    }

    public GameTableRules(List<string> _errors, List<GameTableIssue> _issues)
    {
        errors = _errors;
        issues = _issues;
    }

    // 화차 보드 최대 크기(10×10 = 100칸). PuzzleInventoryView.EnsureCapacity가 필요한 만큼 슬롯을 자동 복제한다.
    const int MAX_WAGON_BOARD_CELLS = 100;

    // 턴제: 테이블의 시간 값은 턴 길이(6시간)의 배수여야 턴 경계에서만 사건이 일어난다.
    void CheckTurnHours(long _hours, string _path, string _pieceId = null, string _field = null)
    {
        Check(_hours % PuzzleTimeManager.HOURS_PER_TURN == 0, _path,
            "시간 값은 1턴(" + PuzzleTimeManager.HOURS_PER_TURN + "시간)의 배수여야 합니다.", _pieceId, _field);
    }

    void Check(bool _condition, string _path, string _rule, string _pieceId = null, string _field = null)
    {
        if (_condition)
        {
            return;
        }
        IsValid = false;
        issues?.Add(new GameTableIssue(_path, _rule, _pieceId, _field));
        if (errors != null || issues != null)
        {
            errors?.Add($"[{_path}] {_rule}");
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
        ValidateSellGrades(_tables);
        ValidatePuzzle(_tables);
        ValidatePuzzleSynergy(_tables);
        ValidateCombinations(_tables);
        ValidateWorkshops(_tables);
        ValidateWagonProcesses(_tables);
        ValidatePuzzleTrade(_tables);
        ValidateRumor(_tables);
        ValidateTime(_tables.PuzzleTime);
        ValidateRoutes(_tables);
        ValidateQuests(_tables);
        ValidateProficiency(_tables);
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
        Check(_tables.PuzzleTrade.targetFunds > 0, PATH + ".targetFunds", "목표 잔액은 0보다 커야 합니다.");
        Check(_tables.PuzzleTrade.deadlineHours > 0, PATH + ".deadlineHours", "영업 기간은 양수여야 합니다. 결산은 기간이 끝나야 할 수 있습니다.");
        CheckTurnHours(_tables.PuzzleTrade.deadlineHours, PATH + ".deadlineHours");
        Check(_tables.PuzzleTrade.wagonRefrigerationCost > 0, PATH + ".wagonRefrigerationCost",
            "객차 냉장 개조 비용은 양수여야 합니다.");
        Check(_tables.PuzzleTrade.startingWagonValue >= 0, PATH + ".startingWagonValue",
            "시작 화차 가치는 0 이상이어야 합니다.");
        Check(_tables.PuzzleTrade.wagonSalePercent >= 0 && _tables.PuzzleTrade.wagonSalePercent <= 100,
            PATH + ".wagonSalePercent", "화차 판매가 비율은 0~100%여야 합니다.");
        Check(_tables.PuzzleTrade.maxWagonCount >= 1, PATH + ".maxWagonCount",
            "최대 객차 수는 1 이상이어야 합니다.");
        Check(_tables.PuzzleTrade.maxWagonCountCap >= _tables.PuzzleTrade.maxWagonCount, PATH + ".maxWagonCountCap",
            "증차 상한 최댓값은 기본 상한 이상이어야 합니다.");
        Check(IsFinitePositive(_tables.PuzzleTrade.travelEffectSeconds), PATH + ".travelEffectSeconds",
            "이동 연출 시간은 유한한 양수여야 합니다.");
        Check(IsFinitePositive(_tables.PuzzleTrade.travelArrivalHoldSeconds), PATH + ".travelArrivalHoldSeconds",
            "도착 표시 시간은 유한한 양수여야 합니다.");
        ValidateWagonProducts(_tables, _tables.PuzzleTrade.wagonProducts, PATH + ".wagonProducts");
    }

    // 품목 중 하나라도 가진 태그만 등록된 태그로 본다.
    static bool IsKnownTag(TableManager _tables, string _tag)
    {
        return !string.IsNullOrWhiteSpace(_tag) &&
            _tables.Pieces.Any(_piece => (_piece.tags ?? Array.Empty<string>()).Contains(_tag));
    }

    // 전용 구역: id 고유, 보드 안 직사각형, 구역끼리 겹치지 않음, 받는 태그는 등록된 태그.
    void ValidateBoardZones(TableManager _tables, PuzzleBoardData _board, string _path)
    {
        var zones = _board.zones ?? Array.Empty<PuzzleBoardZoneData>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < zones.Length; i++)
        {
            var zone = zones[i];
            string path = $"{_path}.zones[{i}]";
            if (zone == null)
            {
                Check(false, path, "전용 구역 데이터가 필요합니다.");
                continue;
            }
            Check(!string.IsNullOrWhiteSpace(zone.id) && ids.Add(zone.id), path + ".id", "구역 ID는 비어 있지 않고 고유해야 합니다.");
            Check(!string.IsNullOrWhiteSpace(zone.displayName), path + ".displayName", "구역 이름이 필요합니다.");
            bool inBounds = zone.width > 0 && zone.height > 0 && zone.x >= 0 && zone.y >= 0 &&
                zone.x + zone.width <= _board.width && zone.y + zone.height <= _board.height;
            Check(inBounds, path, "구역은 보드 안의 양수 크기 직사각형이어야 합니다.");
            var tags = zone.allowedTags ?? Array.Empty<string>();
            Check(tags.Length > 0, path + ".allowedTags", "구역이 받는 태그가 하나 이상 필요합니다.");
            foreach (string tag in tags)
            {
                Check(IsKnownTag(_tables, tag), path + ".allowedTags", "'" + tag + "'은 등록된 화물 태그가 아닙니다.");
            }
            Check(zone.maxPieceCells >= 0, path + ".maxPieceCells", "구역 화물 칸 수 상한은 0 이상이어야 합니다.");
            if (!inBounds)
            {
                continue;
            }
            for (int x = zone.x; x < zone.x + zone.width; x++)
            {
                for (int y = zone.y; y < zone.y + zone.height; y++)
                {
                    Check(cells.Add(new Vector2Int(x, y)), path, "구역끼리 겹칠 수 없습니다.");
                }
            }
        }
    }

    void ValidateWagonProcesses(TableManager _tables)
    {
        const string PATH = nameof(TrainventoryWagonProcessTableData);
        var processes = _tables.WagonProcess.processes ?? Array.Empty<TrainventoryWagonProcessData>();
        for (int i = 0; i < processes.Length; i++)
        {
            var process = processes[i];
            string path = $"{PATH}.processes[{i}]";
            if (process == null)
            {
                Check(false, path, "가공 데이터가 필요합니다.");
                continue;
            }
            Check(!string.IsNullOrWhiteSpace(process.displayName), path + ".displayName", "가공 이름이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(process.description), path + ".description", "가공 설명이 필요합니다.");
            Check(IsKnownTag(_tables, process.primaryTag), path + ".primaryTag", "주재료 태그는 등록된 화물 태그여야 합니다.");
            bool hasOutput = _tables.TryGetPiece(process.outputPieceId, out var output);
            Reference(process.outputPieceId, path + ".outputPieceId", hasOutput, "가공 산출 품목");
            if (!TableManager.TryGetWagonProcessType(process, out var type))
            {
                Check(false, path + ".type", "가공 종류는 Convert 또는 Crush여야 합니다.");
                continue;
            }
            if (type == TrainventoryWagonProcessType.Convert)
            {
                Check(process.hours > 0, path + ".hours", "가공 시간은 양수여야 합니다.");
                CheckTurnHours(process.hours, path + ".hours");
                Check(process.outputPerUnit > 0, path + ".outputPerUnit", "단위당 산출 개수는 양수여야 합니다.");
                var inputs = process.inputs ?? Array.Empty<TrainventoryWagonProcessInputData>();
                for (int j = 0; j < inputs.Length; j++)
                {
                    var input = inputs[j];
                    string inputPath = $"{path}.inputs[{j}]";
                    Check(input != null && IsKnownTag(_tables, input.tag), inputPath + ".tag", "부재료 태그는 등록된 화물 태그여야 합니다.");
                    Check(input != null && input.countPerUnit > 0, inputPath + ".countPerUnit", "단위당 부재료 수는 양수여야 합니다.");
                }
            }
            else
            {
                Check(process.hoursPerCell > 0, path + ".hoursPerCell", "칸당 가공 시간은 양수여야 합니다.");
                CheckTurnHours(process.hoursPerCell, path + ".hoursPerCell");
                if (hasOutput)
                {
                    Check((output.cells ?? Array.Empty<PuzzleCellData>()).Length == 1, path + ".outputPieceId",
                        "분쇄 산출물은 1칸 품목이어야 합니다.");
                    Check((output.tags ?? Array.Empty<string>()).Contains(process.primaryTag), path + ".outputPieceId",
                        "분쇄 산출물은 주재료 태그를 가져야 합니다(같은 구역에 다시 놓인다).");
                }
            }
        }
    }

    void ValidateWagonProducts(TableManager _tables, PuzzleWagonProductData[] _products, string _path)
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
            Check(!string.IsNullOrWhiteSpace(product.description), path + ".description", "화차 효과 설명이 필요합니다.");
            foreach (string tag in product.freightExemptTags ?? Array.Empty<string>())
            {
                Check(IsKnownTag(_tables, tag), path + ".freightExemptTags", "'" + tag + "'은 등록된 화물 태그가 아닙니다.");
            }
            if (!string.IsNullOrEmpty(product.processId))
            {
                Reference(product.processId, path + ".processId", _tables.TryGetWagonProcess(product.processId, out _), "화차 가공");
            }
            if (product.board != null && (product.board.width > 0 || product.board.height > 0))
            {
                Check(product.board.width > 0 && product.board.height > 0, path + ".board", "화차 보드의 너비·높이는 양수여야 합니다.");
                // 화물칸 화면은 미리 배치한 칸 슬롯(최대 7×6 = 42칸)만 켠다.
                Check((long)product.board.width * product.board.height <= MAX_WAGON_BOARD_CELLS, path + ".board",
                    "화차 보드는 " + MAX_WAGON_BOARD_CELLS + "칸 이하여야 합니다.");
                var blocked = ValidateBlockedCells(product.board, path + ".board");
                ValidateBoardZones(_tables, product.board, path + ".board");
                Check((long)product.board.width * product.board.height - blocked.Count > 0, path + ".board",
                    "화차 보드에 쓸 수 있는 칸이 하나 이상 필요합니다.");
                Check((product.board.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>()).Length == 0,
                    path + ".board.startingPlacements", "구매 화차는 빈 상태로 온다. 시작 배치를 둘 수 없습니다.");
            }
            if (!string.IsNullOrEmpty(product.modificationId))
            {
                Reference(product.modificationId, path + ".modificationId",
                    PuzzleWagonModificationRegistry.Contains(product.modificationId), "화차 강화");
            }
        }
    }

    void ValidateRumor(TableManager _tables)
    {
        const string PATH = nameof(PuzzleRumorTableData);
        var rumor = _tables.PuzzleRumor;
        Check(rumor.refreshHours > 0, PATH + ".refreshHours", "소문 재생 주기는 양수여야 합니다.");
        CheckTurnHours(rumor.refreshHours, PATH + ".refreshHours");
        var firstPreference = rumor.firstPreference;
        if (firstPreference != null)
        {
            string path = PATH + ".firstPreference";
            bool hasCity = _tables.HasCity(firstPreference.cityId);
            Reference(firstPreference.cityId, path + ".cityId", hasCity, "첫 선호 소문 마을");
            Check(!string.IsNullOrWhiteSpace(firstPreference.tag) && _tables.Pieces.Any(_piece =>
                    !_piece.excludeFromMarket && (_piece.tags ?? Array.Empty<string>()).Contains(firstPreference.tag)),
                path + ".tag", "시장에 유통되는 품목의 태그가 필요합니다.");
            if (hasCity)
            {
                var city = _tables.CityPrice(firstPreference.cityId);
                Check(city.sellEnabled && (city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>()).Any(_rule =>
                        _rule != null && _rule.tag == firstPreference.tag &&
                        TableManager.SellGradeKindOf(_rule) == PuzzleSellGradeKind.Expensive),
                    path + ".tag", "첫 선호 소문의 태그는 해당 마을에서 Expensive 판매 등급이어야 합니다.");
            }
        }
        var excludedIds = new HashSet<string>(StringComparer.Ordinal);
        var excludedItemIds = rumor.excludedItemIds ?? Array.Empty<string>();
        for (int i = 0; i < excludedItemIds.Length; i++)
        {
            string id = excludedItemIds[i];
            string path = PATH + ".excludedItemIds[" + i + "]";
            Check(!string.IsNullOrWhiteSpace(id) && excludedIds.Add(id), path, "품목 ID는 비어 있지 않고 중복되지 않아야 합니다.");
            Reference(id, path, _tables.TryGetPiece(id, out _), "품목");
        }
    }

    void ValidateProficiency(TableManager _tables)
    {
        const string PATH = nameof(TrainventoryProficiencyTableData);
        var proficiency = _tables.Proficiency;
        Inspect(proficiency, PATH);
        Check(_tables.ProficiencyTags.Count > 0, "PuzzleTableData.regions.productionTags",
            "숙련 대상이 될 지역 생산 태그가 하나 이상 필요합니다.");
        Check(proficiency.xpPercentOfSalePrice > 0, PATH + ".xpPercentOfSalePrice", "판매 경험치 비율은 양수여야 합니다.");
        var thresholds = proficiency.levelThresholds ?? Array.Empty<long>();
        Check(thresholds.Length > 0, PATH + ".levelThresholds", "레벨 기준이 하나 이상 필요합니다.");
        for (int i = 0; i < thresholds.Length; i++)
        {
            Check(thresholds[i] > (i == 0 ? 0 : thresholds[i - 1]), $"{PATH}.levelThresholds[{i}]",
                "레벨 기준은 양수이고 앞 기준보다 커야 합니다.");
        }
        Check(proficiency.extraLevelXp > 0, PATH + ".extraLevelXp", "추가 레벨 경험치는 양수여야 합니다.");
        Check(proficiency.bonusPercentPerLevel > 0, PATH + ".bonusPercentPerLevel", "레벨당 거래 이득은 양수여야 합니다.");
    }

    void ValidateQuests(TableManager _tables)
    {
        const string PATH = nameof(TrainventoryQuestTableData);
        var quest = _tables.Quest;
        Inspect(quest, PATH);
        Check(!string.IsNullOrWhiteSpace(quest.cargoTag), PATH + ".cargoTag", "의뢰 전용 태그가 필요합니다.");
        Check(quest.offersPerTavern > 0, PATH + ".offersPerTavern", "술집 의뢰 수는 양수여야 합니다.");
        Check(quest.proficiencyXpPercentOfReward >= 0, PATH + ".proficiencyXpPercentOfReward",
            "의뢰 숙련 경험치 비율은 0 이상이어야 합니다.");

        var levels = quest.reputationLevels ?? Array.Empty<TrainventoryQuestReputationData>();
        Check(levels.Any(_level => _level != null && _level.reputation == 0), PATH + ".reputationLevels",
            "평판 0 단계가 필요합니다.");
        var seenReputation = new HashSet<int>();
        for (int i = 0; i < levels.Length; i++)
        {
            string path = $"{PATH}.reputationLevels[{i}]";
            var level = levels[i];
            if (level == null)
            {
                Check(false, path, "평판 단계가 필요합니다.");
                continue;
            }
            Check(level.reputation >= 0, path + ".reputation", "평판 기준은 0 이상이어야 합니다.");
            Check(seenReputation.Add(level.reputation), path + ".reputation", "평판 기준이 중복되었습니다.");
            Check(level.maxActiveQuests > 0, path + ".maxActiveQuests", "동시 진행 수는 양수여야 합니다.");
        }
        var ordered = levels.Where(_level => _level != null).OrderBy(_level => _level.reputation).ToArray();
        for (int i = 1; i < ordered.Length; i++)
        {
            Check(ordered[i].maxActiveQuests >= ordered[i - 1].maxActiveQuests, PATH + ".reputationLevels",
                "평판이 높은 단계의 동시 진행 수가 낮은 단계보다 작을 수 없습니다.");
        }

        foreach (var penalty in quest.penalties ?? Array.Empty<TrainventoryQuestPenaltyData>())
        {
            if (penalty == null || string.IsNullOrEmpty(penalty.id))
            {
                continue;
            }
            string path = $"{PATH}.penalties.{penalty.id}";
            bool known = Enum.TryParse(penalty.type, out TrainventoryQuestPenaltyType type) &&
                Enum.IsDefined(typeof(TrainventoryQuestPenaltyType), type);
            Check(known, path + ".type",
                $"패널티 종류는 {string.Join(", ", Enum.GetNames(typeof(TrainventoryQuestPenaltyType)))} 중 하나여야 합니다.");
            Check(!string.IsNullOrWhiteSpace(penalty.displayName), path + ".displayName", "패널티 이름이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(penalty.description), path + ".description", "패널티 설명이 필요합니다.");
            if (known && type == TrainventoryQuestPenaltyType.Deposit)
            {
                IntRange(penalty.depositPercentOfReward, 1, 1000, path + ".depositPercentOfReward");
            }
            if (known && type == TrainventoryQuestPenaltyType.RegionTrade)
            {
                Check(penalty.buyPercentDelta >= 0 && penalty.sellPercentDelta <= 0 &&
                    (penalty.buyPercentDelta != 0 || penalty.sellPercentDelta != 0), path,
                    "지역 거래 불이익은 구매 배율을 올리거나 판매 배율을 내려야 합니다.");
                Check(penalty.durationHours > 0, path + ".durationHours", "지역 거래 불이익 시간은 양수여야 합니다.");
                CheckTurnHours(penalty.durationHours, path + ".durationHours");
            }
        }

        Check(_tables.CourierQuests.Count > 0, PATH + ".courierQuests", "운송 의뢰가 하나 이상 필요합니다.");
        foreach (var courier in _tables.CourierQuests)
        {
            string path = $"{PATH}.courierQuests.{courier.id}";
            Reference(courier.penaltyId, path + ".penaltyId", _tables.TryGetQuestPenalty(courier.penaltyId, out _), "의뢰 패널티");
            Check(courier.weight > 0, path + ".weight", "출현 가중치는 양수여야 합니다.");
            Check(courier.minTravelDistance > 0 && courier.maxTravelDistance >= courier.minTravelDistance, path,
                "최단 이동 시간 범위는 양수이고 최소가 최대 이하여야 합니다.");
            Check(courier.baseReward >= 0 && courier.rewardPerCellDistance >= 0 &&
                courier.baseReward + courier.rewardPerCellDistance > 0, path, "보상 계수는 0 이상이고 둘 다 0일 수 없습니다.");
            Check(courier.deadlinePercent >= 100, path + ".deadlinePercent", "기한 배율은 100% 이상이어야 합니다.");
            Check(courier.deadlineExtraHours >= 0, path + ".deadlineExtraHours", "기한 여유 시간은 0 이상이어야 합니다.");
            CheckTurnHours(courier.deadlineExtraHours, path + ".deadlineExtraHours");
            foreach (string tag in courier.proficiencyTags ?? Array.Empty<string>())
            {
                Check(_tables.ProficiencyTags.Contains(tag), path + ".proficiencyTags",
                    $"'{tag}'은 숙련 대상(지역 생산) 태그가 아닙니다.");
            }
            var pieceIds = courier.pieceIds ?? Array.Empty<string>();
            Check(pieceIds.Length > 0, path + ".pieceIds", "의뢰 화물이 하나 이상 필요합니다.");
            foreach (string pieceId in pieceIds)
            {
                bool found = _tables.TryGetPiece(pieceId, out var piece);
                Reference(pieceId, path + ".pieceIds", found, "퍼즐 조각");
                if (found)
                {
                    Check(piece.excludeFromMarket, path + ".pieceIds",
                        $"의뢰 화물 '{pieceId}'은 시장 재고에서 제외(excludeFromMarket)되어야 합니다.");
                    Check((piece.tags ?? Array.Empty<string>()).Contains(quest.cargoTag),
                        path + ".pieceIds", $"의뢰 화물 '{pieceId}'에 의뢰 전용 태그가 필요합니다.");
                }
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
        Check(_time.daylightHours > 0 && _time.daylightHours < PuzzleTimeManager.HOURS_PER_DAY, PATH + ".daylightHours",
            "낮 시간은 1 이상, 하루(" + PuzzleTimeManager.HOURS_PER_DAY + "시간) 미만이어야 합니다.");
        Check(_time.startHour >= 0 && _time.startHour < PuzzleTimeManager.HOURS_PER_DAY, PATH + ".startHour", "시작 시각이 범위를 벗어났습니다.");
        CheckTurnHours(_time.daylightHours, PATH + ".daylightHours");
        CheckTurnHours(_time.startHour, PATH + ".startHour");
        Check(_time.travelTurns > 0, PATH + ".travelTurns", "이동 턴은 1 이상이어야 합니다.");
        Check(_time.restTurns > 0 && _time.restTurns <= PuzzleTimeManager.TURNS_PER_DAY, PATH + ".restTurns",
            "기본 휴식 턴은 1 이상, 하루(" + PuzzleTimeManager.TURNS_PER_DAY + "턴) 이하여야 합니다.");
        Check(_time.restockTurns > 0, PATH + ".restockTurns", "재입고 주기는 1턴 이상이어야 합니다.");
    }

    void ValidateRoutes(TableManager _tables)
    {
        const string PATH = nameof(PuzzleRouteTableData);
        const string ROUTES_PATH = PATH + ".routes";
        Check(_tables.PuzzleRoute.defaultBaseCost >= 0, PATH + ".defaultBaseCost", "기준 기본요금은 0 이상이어야 합니다.");
        Check(!double.IsNaN(_tables.PuzzleRoute.pixelsPerDistance) && !double.IsInfinity(_tables.PuzzleRoute.pixelsPerDistance) && _tables.PuzzleRoute.pixelsPerDistance > 0,
            PATH + ".pixelsPerDistance", "노선 거리 1당 지도 길이는 유한한 양수여야 합니다.");
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

    void ValidateCityTagPrices(TableManager _tables, PuzzleCityData _city, HashSet<string> _knownTags, string _path)
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
            ValidateSellRate(_tables, entry, path);
        }
    }

    // 판매 배율은 등급 참조와 사용자 지정 수치가 서로 배타적이어야 한다. 등급 참조는 단일 원본 테이블로 해결된다.
    void ValidateSellRate(TableManager _tables, PuzzleCityTagPriceData _entry, string _path)
    {
        bool hasGrade = !string.IsNullOrWhiteSpace(_entry.sellGradeId);
        bool hasValue = _entry.sellPercent >= 0;
        if (hasGrade && hasValue)
        {
            Check(false, _path + ".sellGradeId", "판매 등급을 지정하면 판매 배율을 함께 저장하지 않는다.");
            return;
        }
        if (hasGrade)
        {
            Check(_entry.sellGradeId == _entry.sellGradeId.Trim(), _path + ".sellGradeId",
                "판매 등급 ID에 앞뒤 공백이 있어서는 안 됩니다.");
            Reference(_entry.sellGradeId, _path + ".sellGradeId", _tables.TryGetSellGrade(_entry.sellGradeId, out _), "판매 등급");
            return;
        }
        Check(hasValue, _path, "판매 등급 또는 0보다 큰 사용자 지정 판매 배율 중 하나가 필요합니다.");
        Check(_entry.sellPercent > 0, _path + ".sellPercent", "사용자 지정 판매 배율은 0보다 커야 합니다.");
    }

    void ValidatePuzzleSynergy(TableManager _tables)
    {
        Check(_tables.PuzzleSynergy.activationIntervalHours > 0, "PuzzleSynergyTableData.activationIntervalHours",
            "시너지 적용 주기는 양의 정수 시간이어야 합니다.");
        CheckTurnHours(_tables.PuzzleSynergy.activationIntervalHours, "PuzzleSynergyTableData.activationIntervalHours");
        Check(_tables.PuzzleSynergy.proficiencyComboPercentPerLevel >= 0, "PuzzleSynergyTableData.proficiencyComboPercentPerLevel",
            "숙련 콤보 보강 계수는 0 이상이어야 합니다.");
        var tags = new HashSet<string>(_tables.Pieces.SelectMany(_item => _item.tags ?? Array.Empty<string>()),
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
                  rule.excludedTargetTag.Split(',').All(_tag => tags.Contains(_tag.Trim())),
                path + ".excludedTargetTag", "제외 대상 태그는 쉼표로 구분된 등록 태그여야 합니다.");
            Reference(rule.neighborTag, path + ".neighborTag", tags.Contains(rule.neighborTag), "이웃 태그");
            Check(string.IsNullOrEmpty(rule.excludedNeighborTag) ||
                  rule.excludedNeighborTag.Split(',').All(_tag => tags.Contains(_tag.Trim())),
                path + ".excludedNeighborTag", "제외 이웃 태그는 쉼표로 구분된 등록 태그여야 합니다.");
            Reference(rule.descriptionTag, path + ".descriptionTag", tags.Contains(rule.descriptionTag),
                "설명 표시 태그", _allowEmpty: true);
            Check(rule.scope is nameof(PuzzleSynergyScope.Train) or nameof(PuzzleSynergyScope.Market)
                or nameof(PuzzleSynergyScope.Both), path + ".scope", "적용 범위는 Train, Market, Both 중 하나여야 합니다.");
            Check(rule.spatialRange is nameof(PuzzleSynergyRange.Adjacent) or nameof(PuzzleSynergyRange.Board),
                path + ".spatialRange", "공간 범위는 Adjacent 또는 Board여야 합니다.");
            Check(rule.multiplierPercent > 0, path + ".multiplierPercent", "배율은 양의 정수 백분율이어야 합니다.");
            Check(!string.IsNullOrWhiteSpace(rule.description), path + ".description", "시너지 설명이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(rule.targetDescription), path + ".targetDescription", "효과를 받는 대상의 설명이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(rule.sourceDescription), path + ".sourceDescription", "효과를 주는 이웃의 설명이 필요합니다.");
        }
        ValidateAnimalSynergy(_tables, tags);
        const string FRESHNESS_PATH = "PuzzleSynergyTableData.freshness";
        var freshness = _tables.PuzzleSynergy.freshness;
        Check(freshness != null, FRESHNESS_PATH, "신선도 설정이 필요합니다.");
        if (freshness != null)
        {
            Reference(freshness.refrigerationTag, FRESHNESS_PATH + ".refrigerationTag",
                tags.Contains(freshness.refrigerationTag), "냉장 태그");
            Check(freshness.refrigerationReductionPercent >= 0 &&
                freshness.refrigerationReductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
                FRESHNESS_PATH + ".refrigerationReductionPercent", "기존 냉장 효과의 이행 원본은 0~100%여야 합니다.");
            Check(freshness.wagonRefrigerationReductionPercent > 0 &&
                freshness.wagonRefrigerationReductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
                FRESHNESS_PATH + ".wagonRefrigerationReductionPercent", "객차 냉장 감소율은 1~100%여야 합니다.");
            Check(freshness.saltPreservationReductionPercent >= 0 &&
                freshness.saltPreservationReductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
                FRESHNESS_PATH + ".saltPreservationReductionPercent", "소금 보존 감소율은 0~100%여야 합니다.");
        }
        foreach (var piece in _tables.Pieces)
        {
            ValidatePieceEffects(piece, _tables.SynergyRules, freshness, _tables.PuzzleSynergy.animals?.wasteTag,
                "PuzzleTableData.pieces." + piece.id);
        }
    }

    public void ValidatePieceEffects(PuzzlePieceData _piece, IReadOnlyList<PuzzleSynergyRuleData> _rules,
        PuzzleFreshnessData _freshness, string _wasteTag, string _path)
    {
        var tags = _piece.tags ?? Array.Empty<string>();
        Check(_piece.synergyEffects != null, _path + ".synergyEffects", "효과 목록이 누락되었습니다. 기존 데이터는 명시적으로 이행하고 효과 없음은 빈 배열로 저장하세요.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in _piece.synergyEffects ?? Array.Empty<PuzzlePieceSynergyData>())
        {
            if (effect == null)
            {
                Check(false, _path + ".synergyEffects", "효과 연결이 누락되었습니다.");
                continue;
            }
            string path = _path + ".synergyEffects." + effect.ruleId;
            bool hasId = !string.IsNullOrWhiteSpace(effect.ruleId);
            Check(hasId && ids.Add(effect.ruleId), path, "규칙 ID는 비어 있지 않고 품목 안에서 중복되지 않아야 합니다.");
            var rule = hasId ? _rules.FirstOrDefault(_rule => _rule != null && _rule.id == effect.ruleId) : null;
            Check(rule != null, path, "등록된 시너지 규칙 ID가 필요합니다.");
            Check(effect.target || effect.source, path, "받는 대상 또는 주는 이웃 역할이 하나 이상 필요합니다.");
            if (rule != null)
            {
                Check(!effect.target || PuzzlePieceEffects.EligibleTarget(tags, rule, _wasteTag), path + ".target",
                    "품목의 태그가 이 시너지의 대상 조건과 맞지 않습니다. 대상 역할을 해제하고 남은 역할이 없으면 효과 항목을 제거하세요.");
                Check(!effect.source || PuzzlePieceEffects.EligibleSource(tags, rule), path + ".source",
                    "품목의 태그가 이 시너지의 발생원 조건과 맞지 않습니다. 발생원 역할을 해제하고 남은 역할이 없으면 효과 항목을 제거하세요.");
            }
        }
        var preservation = _piece.preservationEffect;
        Check(preservation != null, _path + ".preservationEffect", "신선 효과를 명시해야 합니다. 미부여 품목도 enabled=false, reductionPercent=0이 필요합니다.");
        if (preservation == null)
        {
            return;
        }
        string preservationPath = _path + ".preservationEffect";
        Check(preservation.reductionPercent >= 0 && preservation.reductionPercent <= PuzzleInventoryValuation.BASE_PERCENT,
            preservationPath + ".reductionPercent", "품목별 신선 감소율은 0~100%여야 합니다.");
        Check(preservation.enabled || preservation.reductionPercent == 0, preservationPath,
            "신선 효과가 없는 품목의 감소율은 0이어야 합니다.");
        if (_freshness != null)
        {
            Check(preservation.enabled == tags.Contains(_freshness.refrigerationTag), preservationPath + ".enabled",
                "신선 효과 활성 여부와 " + _freshness.refrigerationTag + " 태그가 일치해야 합니다.");
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
        Check(animals.wasteIntervalHours > 0, PATH + ".wasteIntervalHours", "배설 주기는 양의 정수 일수여야 합니다.");
        Check(animals.wasteCountPerAnimal > 0, PATH + ".wasteCountPerAnimal", "배설물 수량은 양수여야 합니다.");
        ValidateEffectDescription(animals.wasteDescription, PATH + ".wasteDescription",
            animals.wasteIntervalHours, string.Empty, animals.wasteCountPerAnimal);
        Check(animals.breedingHours > 0, PATH + ".breedingHours", "번식 시간은 양의 정수 시간이어야 합니다.");
        Check(animals.completionHoldHours > 0, PATH + ".completionHoldHours",
            "완료 전 대기 시간은 양의 정수 시간이어야 합니다.");
        Check(animals.completionHoldHours > 0 && animals.breedingHours > 0 &&
                animals.completionHoldHours < animals.breedingHours,
            PATH + ".completionHoldHours", "완료 전 대기 시간은 번식 시간보다 짧아야 합니다.");
        Check(animals.completionHoldHours > 0 && animals.wasteIntervalHours > 0 &&
                (long)animals.completionHoldHours < (long)animals.wasteIntervalHours,
            PATH + ".completionHoldHours", "완료 전 대기 시간은 배설 주기보다 짧아야 합니다.");

        Reference(animals.grainTag, PATH + ".grainTag", _tags.Contains(animals.grainTag), "곡물 태그");
        Check(animals.grainTag != animals.animalTag && animals.grainTag != animals.wasteTag, PATH + ".grainTag",
            "곡물 태그는 동물·배설물 태그와 서로 달라야 합니다.");
        Check(animals.feedIntervalHours > 0, PATH + ".feedIntervalHours", "곡물 섭취 주기는 양의 정수 시간이어야 합니다.");
        CheckTurnHours(animals.wasteIntervalHours, PATH + ".wasteIntervalHours");
        CheckTurnHours(animals.breedingHours, PATH + ".breedingHours");
        CheckTurnHours(animals.completionHoldHours, PATH + ".completionHoldHours");
        CheckTurnHours(animals.feedIntervalHours, PATH + ".feedIntervalHours");
        CheckTurnHours(animals.breedingFeedReductionHours, PATH + ".breedingFeedReductionHours");
        CheckTurnHours(animals.growthFeedReductionHours, PATH + ".growthFeedReductionHours");
        Check(animals.breedingFeedReductionHours > 0, PATH + ".breedingFeedReductionHours",
            "섭취 1회 번식 시간 단축은 양의 정수 시간이어야 합니다.");
        Check(animals.growthFeedReductionHours > 0, PATH + ".growthFeedReductionHours",
            "섭취 1회 성장 시간 단축은 양의 정수 시간이어야 합니다.");
        ValidateEffectDescription(animals.breedingFeedDescription, PATH + ".breedingFeedDescription",
            string.Empty, string.Empty);
        ValidateEffectDescription(animals.growthFeedDescription, PATH + ".growthFeedDescription",
            string.Empty, string.Empty);

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
            ValidateEffectDescription(rule.description, path + ".description", string.Empty, string.Empty, rule.offspringCount, string.Empty);
        }

        foreach (var piece in _tables.Pieces)
        {
            var tags = piece.tags ?? Array.Empty<string>();
            if (tags.Contains(animals.animalTag) && !tags.Contains(PuzzleItemDefinition.BABY_TAG))
            {
                Check(_tables.TryGetAnimalBreeding(piece.id, out _), PATH + ".breeding",
                    $"성체 동물 품목 '{piece.id}'의 번식 규칙이 필요합니다.", piece.id, "breeding");
            }
        }
    }

    // 테이블의 효과 설명은 화면에서 서식 인자로 값을 채워 표시한다. 포맷이 깨지거나 값 자리가
    // 빠지면 툴팁에서 조용히 틀리므로 항목 초기화에 막는다.
    void ValidateEffectDescription(string _description, string _path, params object[] _arguments)
    {
        bool hasDescription = !string.IsNullOrWhiteSpace(_description);
        Check(hasDescription, _path, "효과 설명이 필요합니다.");
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
                $"설명에 {string.Join(", ", Enumerable.Range(0, _arguments.Length).Select(_index => $"{{{_index}}}"))} 서식 인자가 모두 필요합니다.");
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

    // 같은 객차에서 인접한 두 품목을 정해진 시간 후 하나로 바꾸는 공용 조합 규칙.
    // 품목 참조, 서로 다른 두 입력, 양의 시간, 자리를 이어받는 입력과 같은 결과 모양,
    // 중복 입력 쌍, 결과를 입력으로 되돌리는 재조합을 항목별로 검사한다.
    void ValidateCombinations(TableManager _tables)
    {
        const string PATH = nameof(PuzzleCombinationTableData);
        var rules = _tables.PuzzleCombination.rules ?? Array.Empty<PuzzleCombinationRuleData>();
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        var inputIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule == null)
            {
                continue;
            }

            string path = $"{PATH}.rules[{i}]";
            bool hasSource = _tables.TryGetPiece(rule.sourcePieceId, out var source);
            bool hasPartner = _tables.TryGetPiece(rule.partnerPieceId, out _);
            bool hasResult = _tables.TryGetPiece(rule.resultPieceId, out var result);
            Reference(rule.sourcePieceId, path + ".sourcePieceId", hasSource, "입력 화물");
            Reference(rule.partnerPieceId, path + ".partnerPieceId", hasPartner, "입력 화물");
            Reference(rule.resultPieceId, path + ".resultPieceId", hasResult, "조합 결과 화물");
            Check(!string.IsNullOrWhiteSpace(rule.sourcePieceId) && !string.IsNullOrWhiteSpace(rule.partnerPieceId) &&
                !string.Equals(rule.sourcePieceId, rule.partnerPieceId, StringComparison.Ordinal), path + ".partnerPieceId",
                "두 입력은 비어 있지 않고 서로 다른 품목이어야 합니다.");
            Check(rule.requiredHours > 0, path + ".requiredHours", "조합 시간은 양의 정수 시간이어야 합니다.");
            CheckTurnHours(rule.requiredHours, path + ".requiredHours");
            ValidateEffectDescription(rule.description, path + ".description", string.Empty, string.Empty);
            Check(pairs.Add(PairKey(rule.sourcePieceId, rule.partnerPieceId)), path,
                "같은 입력 쌍의 조합 규칙은 하나만 둘 수 있습니다.");
            if (hasSource)
            {
                inputIds.Add(rule.sourcePieceId);
            }
            if (hasPartner)
            {
                inputIds.Add(rule.partnerPieceId);
            }
            if (!hasResult)
            {
                continue;
            }

            Check(!string.Equals(result.id, rule.sourcePieceId, StringComparison.Ordinal) &&
                !string.Equals(result.id, rule.partnerPieceId, StringComparison.Ordinal), path + ".resultPieceId",
                "조합 결과는 두 입력과 서로 다른 품목이어야 합니다.", rule.resultPieceId, "resultPieceId");
            if (!hasSource)
            {
                continue;
            }

            Check(SamePieceGeometry(source, result), path + ".resultPieceId",
                "조합 결과는 자리를 이어받는 입력과 같은 셀 좌표와 slotCount를 가져야 합니다.", rule.resultPieceId, "cells");
            Check(result.excludeFromMarket == source.excludeFromMarket, path + ".resultPieceId",
                "조합 결과의 시장 재고 제외 설정은 자리를 이어받는 입력과 같아야 합니다.", rule.resultPieceId, "excludeFromMarket");
        }

        foreach (var rule in rules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.resultPieceId))
            {
                continue;
            }
            Check(!inputIds.Contains(rule.resultPieceId), PATH + ".rules." + rule.id + ".resultPieceId",
                $"조합 결과 품목 '{rule.resultPieceId}'는 다른 조합의 입력으로 등록할 수 없습니다.",
                rule.resultPieceId, "resultPieceId");
        }
    }

    // 입력 쌍은 보드의 위치와 무관하므로 두 ID를 사전순으로 정렬한 문자열로 본다.
    static string PairKey(string _first, string _second)
    {
        string left = _first ?? string.Empty;
        string right = _second ?? string.Empty;
        return string.CompareOrdinal(left, right) <= 0 ? left + "|" + right : right + "|" + left;
    }

    // 마을별 추가 행동. 도시·조각·태그 참조와 같은 도시 같은 종류의 중복 등록을 검사하고,
    // 도축·개조·정제는 완성 방식과 변환 규칙이 다르므로 유형별로 분리해 검증한다.
    void ValidateWorkshops(TableManager _tables)
    {
        ValidateWorks(_tables);
        const string PATH = nameof(TrainventoryWorkshopTableData);
        var workshops = _tables.Workshop.workshops ?? Array.Empty<TrainventoryWorkshopData>();
        var modificationOutputs = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < workshops.Length; i++)
        {
            var workshop = workshops[i];
            if (workshop == null)
            {
                Check(false, $"{PATH}.workshops[{i}]", "작업장 항목이 필요합니다.");
                continue;
            }

            string path = $"{PATH}.workshops[{i}]";
            bool knownType = TableManager.TryGetWorkshopType(workshop, out var type);
            Check(knownType, path + ".type",
                $"작업장 종류는 {string.Join(", ", Enum.GetNames(typeof(TrainventoryWorkshopType)))} 중 하나여야 합니다.");
            Check(!string.IsNullOrWhiteSpace(workshop.title), path + ".title", "작업장 이름이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(workshop.description), path + ".description", "작업장 설명이 필요합니다.");
            Check(workshop.costPercent >= 0, path + ".costPercent", "작업 비용 계수는 0 이상이어야 합니다.");
            Check(workshop.feePerHour >= 0, path + ".feePerHour", "시간당 수수료는 0 이상이어야 합니다.");
            Check(workshop.extraCost >= 0, path + ".extraCost", "추가 비용은 0 이상이어야 합니다.");

            var board = workshop.board;
            Check(board != null, path + ".board", "작업 보드 설정이 필요합니다.");
            if (board != null)
            {
                Check(board.width > 0, path + ".board.width", "작업 보드 너비는 0보다 커야 합니다.");
                Check(board.height > 0, path + ".board.height", "작업 보드 높이는 0보다 커야 합니다.");
                ValidateBlockedCells(board, path + ".board");
                Check((board.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>()).Length == 0,
                    path + ".board.startingPlacements", "작업 보드는 빈 판으로 시작해야 합니다.");
            }

            var workIds = workshop.workIds ?? Array.Empty<string>();
            Check(workIds.Length > 0, path + ".workIds", "작업장은 작업이 하나 이상 필요합니다.");
            var inputs = new HashSet<string>(StringComparer.Ordinal);
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int w = 0; w < workIds.Length; w++)
            {
                string workPath = $"{path}.workIds[{w}]";
                bool hasWork = _tables.TryGetWork(workIds[w], out var work);
                Reference(workIds[w], workPath, hasWork, "작업");
                Check(seen.Add(workIds[w] ?? string.Empty), workPath, "같은 작업을 두 번 둘 수 없습니다.");
                if (!hasWork)
                {
                    continue;
                }
                Check(inputs.Add(work.inputPieceId ?? string.Empty), workPath,
                    "한 작업장 안에서 같은 입력 화물의 작업은 하나만 둘 수 있습니다.");
                if (!string.IsNullOrEmpty(work.outputPieceId))
                {
                    outputs.Add(work.outputPieceId);
                }
                bool hasInput = _tables.TryGetPiece(work.inputPieceId, out var inputPiece);
                bool hasOutput = _tables.TryGetPiece(work.outputPieceId, out var outputPiece);
                if (!knownType || !hasInput || !hasOutput)
                {
                    continue;
                }
                if (type == TrainventoryWorkshopType.Butchery)
                {
                    Check(!(inputPiece.tags ?? Array.Empty<string>()).Contains(PuzzleItemDefinition.BABY_TAG),
                        workPath, "Baby 태그가 있는 화물은 도축할 수 없습니다.");
                }
                if (type == TrainventoryWorkshopType.Modification)
                {
                    Check(work.outputCount == 1, workPath, "개조 작업의 산출 개수는 입력 하나당 하나여야 합니다.");
                    Check(HoldsAllTags(outputPiece, inputPiece), workPath, "개조 산출 화물은 입력 화물의 모든 태그를 유지해야 합니다.");
                    Check(SamePieceGeometry(inputPiece, outputPiece), workPath,
                        "개조 산출 화물은 입력 화물과 같은 셀 좌표와 slotCount를 가져야 합니다.");
                    Check(inputPiece.baseValue == outputPiece.baseValue, workPath,
                        "개조 산출 화물의 기본 가치는 입력 화물과 같아야 합니다.");
                    modificationOutputs.Add(outputPiece.id);
                }
                else if (board != null && board.width > 0 && board.height > 0 && work.outputCount > 0)
                {
                    long outputSlots = (long)work.outputCount * Math.Max(outputPiece.slotCount, 1);
                    Check(outputSlots <= (long)board.width * board.height, workPath,
                        "산출물을 빈 작업 보드에 모두 놓을 수 있어야 합니다.");
                }
            }
            // 산출 품목을 입력으로도 받으면 같은 작업장에서 다시 처리된다.
            foreach (string input in inputs)
            {
                Check(!outputs.Contains(input), path + ".workIds", $"산출 품목 '{input}'을 같은 작업장의 입력으로 둘 수 없습니다.");
            }
        }

        // 역 배치. 같은 역에 같은 종류의 작업장은 하나만 둔다.
        foreach (var city in _tables.Cities)
        {
            var ids = city.workshopIds ?? Array.Empty<string>();
            var types = new HashSet<TrainventoryWorkshopType>();
            for (int i = 0; i < ids.Length; i++)
            {
                string path = $"PuzzleTableData.cities.{city.id}.workshopIds[{i}]";
                bool known = !string.IsNullOrEmpty(ids[i]) &&
                    (_tables.Workshop.workshops ?? Array.Empty<TrainventoryWorkshopData>()).Any(_row => _row != null && _row.id == ids[i]);
                Reference(ids[i], path, known, "작업장");
                if (known && TableManager.TryGetWorkshopType(_tables.WorkshopById(ids[i]), out var type))
                {
                    Check(types.Add(type), path, "같은 역에 같은 종류의 작업장은 하나만 둘 수 있습니다.");
                }
            }
        }

        // 역의 화차 상품 참조. 같은 역에 같은 상품을 두 번 두지 않는다.
        foreach (var city in _tables.Cities)
        {
            var ids = city.wagonProductIds ?? Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Length; i++)
            {
                string path = $"PuzzleTableData.cities.{city.id}.wagonProductIds[{i}]";
                bool known = !string.IsNullOrEmpty(ids[i]) &&
                    (_tables.PuzzleTrade.wagonProducts ?? Array.Empty<PuzzleWagonProductData>()).Any(_row => _row != null && _row.id == ids[i]);
                Reference(ids[i], path, known, "화차 상품");
                Check(seen.Add(ids[i] ?? string.Empty), path, "같은 역에 같은 화차 상품을 두 번 둘 수 없습니다.");
            }
        }

        // 개조 결과 품목은 시장 무작위 재고·고정 재고 후보에 편입되지 않는다.
        foreach (var profile in _tables.Puzzle.stockProfiles ?? Array.Empty<PuzzleMarketStockData>())
        {
            foreach (string id in profile?.pieceIds ?? Array.Empty<string>())
            {
                Check(!modificationOutputs.Contains(id), "PuzzleTableData.stockProfiles." + profile.id + ".pieceIds",
                    "개조 결과 품목 '" + id + "'는 시장 재고 후보에 둘 수 없습니다.");
            }
        }
        foreach (var city in _tables.Cities)
        {
            foreach (var placement in city.market?.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
            {
                Check(placement == null || !modificationOutputs.Contains(placement.pieceId ?? string.Empty),
                    "PuzzleTableData.cities." + city.id + ".market.startingPlacements",
                    "개조 결과 품목 '" + placement.pieceId + "'는 시장 고정 재고에 둘 수 없습니다.");
            }
        }
    }

    void ValidateWorks(TableManager _tables)
    {
        const string PATH = nameof(TrainventoryWorkTableData);
        var works = _tables.Work.works ?? Array.Empty<TrainventoryWorkData>();
        for (int i = 0; i < works.Length; i++)
        {
            var work = works[i];
            if (work == null)
            {
                Check(false, $"{PATH}.works[{i}]", "작업 항목이 필요합니다.");
                continue;
            }
            string path = $"{PATH}.works[{i}]";
            Check(!string.IsNullOrWhiteSpace(work.displayName), path + ".displayName", "작업 이름이 필요합니다.");
            Check(!string.IsNullOrWhiteSpace(work.description), path + ".description", "작업 설명이 필요합니다.");
            Reference(work.inputPieceId, path + ".inputPieceId", _tables.TryGetPiece(work.inputPieceId, out _), "입력 화물");
            Reference(work.outputPieceId, path + ".outputPieceId", _tables.TryGetPiece(work.outputPieceId, out _), "산출 화물");
            Check(!string.Equals(work.inputPieceId, work.outputPieceId, StringComparison.Ordinal), path + ".outputPieceId",
                "입력과 산출은 서로 다른 품목이어야 합니다.");
            Check(work.workHours > 0, path + ".workHours", "작업 시간은 0보다 큰 게임 시간이어야 합니다.");
            CheckTurnHours(work.workHours, path + ".workHours");
            Check(work.outputCount > 0, path + ".outputCount", "산출 개수는 0보다 커야 합니다.");
            Check(work.cost >= 0, path + ".cost", "작업 비용은 0 이상이어야 합니다.");
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

    // 판매 등급 테이블은 등급별 배율의 단일 원본이다. id 고유성·null은 Index가 보고하고 여기서는 값과 형식을 검사한다.
    void ValidateSellGrades(TableManager _tables)
    {
        const string PATH = "PuzzleTableData.sellGrades";
        var grades = _tables.Puzzle.sellGrades ?? Array.Empty<PuzzleSellGradeData>();
        for (int i = 0; i < grades.Length; i++)
        {
            var grade = grades[i];
            string path = $"{PATH}[{i}]";
            if (grade == null)
            {
                continue;
            }
            Check(!string.IsNullOrWhiteSpace(grade.id) && grade.id == grade.id.Trim(), path + ".id",
                "앞뒤 공백이 없는 비어 있지 않은 판매 등급 ID가 필요합니다.");
            Check(grade.sellPercent > 0, path + ".sellPercent", "판매 등급 배율은 0보다 커야 합니다.");
        }
        Check(grades.Length > 0, PATH, "판매 등급이 하나 이상 필요합니다.");
        Reference(TableManager.SELL_GRADE_RARE, PATH + ".Rare", _tables.TryGetSellGrade(TableManager.SELL_GRADE_RARE, out _), "판매 등급");
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
        ValidateTagPresentations(puzzle);
        var cities = puzzle.cities ?? Array.Empty<PuzzleCityData>();
        int maxSide = cities.Select(_city => MaxBoardSide(_city?.market)).Append(MaxBoardSide(puzzle.train))
            .Append(MaxBoardSide(puzzle.startingTrain)).Max();

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
        if (puzzle.startingTrain != null)
        {
            ValidateBoard(_tables, puzzle.startingTrain, shapes, placementIds, PATH + ".startingTrain");
        }
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
            var specialtyTags = region.specialtyTags ?? Array.Empty<string>();
            var productionTags = region.productionTags ?? Array.Empty<string>();
            string specialtyPath = regionPath + ".specialtyTags";
            Check(region.tradeHub || specialtyTags.Length > 0, specialtyPath, "생산 지역의 대표 특산품 태그가 필요합니다.");
            Check(specialtyTags.Distinct(StringComparer.Ordinal).Count() == specialtyTags.Length, specialtyPath,
                "지역 특산품 태그는 중복될 수 없습니다.");
            foreach (string tag in specialtyTags)
            {
                Check(!string.IsNullOrWhiteSpace(tag) && knownTags.Contains(tag), specialtyPath,
                    $"등록된 지역 특산품 태그가 필요합니다. ('{tag}')");
                Check(productionTags.Contains(tag), specialtyPath, $"지역 특산품 '{tag}'는 지역 생산 목록에 있어야 합니다.");
            }
            foreach (string tag in productionTags)
            {
                Check(knownTags.Contains(tag), regionPath + ".productionTags", "등록된 생산 태그가 필요합니다.");
            }
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
            ValidateCityTagPrices(_tables, city, knownTags, cityPath + ".tagPrices");
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
            _path + ".growthBlockedHours", "성장 전 공간을 확인하는 시간은 1턴 이상이며 성장 시간 이하여야 합니다.");
        CheckTurnHours(_piece.growthHours, _path + ".growthHours", _piece.id, "growthHours");
        CheckTurnHours(_piece.growthBlockedHours, _path + ".growthBlockedHours", _piece.id, "growthBlockedHours");
        ValidateEffectDescription(_piece.growthDescription, _path + ".growthDescription",
            string.Empty, string.Empty, string.Empty);
        bool hasAdult = _tables.TryGetPiece(_piece.adultPieceId, out var adult);
        Reference(_piece.adultPieceId, _path + ".adultPieceId", hasAdult, "성체 화물");
        if (hasAdult)
        {
            Check(!(adult.tags ?? Array.Empty<string>()).Contains(PuzzleItemDefinition.BABY_TAG),
                _path + ".adultPieceId", "성체 화물에는 Baby 태그를 둘 수 없습니다.");
        }
    }

    void ValidateTagPresentations(PuzzleTableData _puzzle)
    {
        const string PATH = "PuzzleTableData.tagPresentations";
        Check(_puzzle.tagPresentations != null, PATH, "태그 표시 목록은 null일 수 없습니다.");
        var entries = _puzzle.tagPresentations ?? Array.Empty<PuzzleTagPresentationData>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            ValidateTagPresentation(entry, PATH + "." + entry?.id);
            if (entry != null)
            {
                Check(seen.Add(entry.id ?? string.Empty), PATH + "." + entry.id, "태그 ID가 대소문자 구분 없이 중복되었습니다.");
            }
        }
    }

    public void ValidateTagPresentation(PuzzleTagPresentationData _tag, string _path)
    {
        Check(_tag != null, _path, "태그 표시 정의가 필요합니다.");
        if (_tag == null)
        {
            return;
        }
        bool validId = !string.IsNullOrWhiteSpace(_tag.id) && !_tag.id.Any(char.IsWhiteSpace) && !_tag.id.Contains(",");
        Check(validId, _path + ".id", "공백·쉼표가 없는 태그 ID가 필요합니다.");
        Check(!Presentation.HasBuiltInPuzzleTag(_tag.id), _path + ".id", "기존 기본 태그의 표시 정의를 덮어쓸 수 없습니다.");
        Check(!string.IsNullOrWhiteSpace(_tag.displayName) && _tag.displayName.Any(_character => _character >= '가' && _character <= '힣'),
            _path + ".displayName", "한글이 포함된 표시명이 필요합니다.");
        Check(new[] { _tag.r, _tag.g, _tag.b, _tag.a }.All(_value => !float.IsNaN(_value) && _value >= 0 && _value <= 1),
            _path, "태그 색 RGBA는 0~1 범위여야 합니다.");
    }

    static IEnumerable<PuzzleCityTagPriceData> MatchingTagPrices(TableManager _tables, PuzzleCityData _city, IReadOnlyCollection<string> _tags)
    {
        return (_city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>())
            .Where(_price => _price != null && _tags.Contains(_price.tag))
            .Select(_price => SellPercentOf(_tables, _price) == _price.sellPercent
                ? _price
                : new PuzzleCityTagPriceData
                {
                    tag = _price.tag,
                    priority = _price.priority,
                    buyPercent = _price.buyPercent,
                    sellPercent = SellPercentOf(_tables, _price)
                });
    }

    // 태그 행의 실효 판매 배율. 판매 등급을 참조하면 등급 테이블의 단일 원본으로 해결한다.
    static int SellPercentOf(TableManager _tables, PuzzleCityTagPriceData _price)
    {
        if (_price == null)
        {
            return 0;
        }
        return !string.IsNullOrEmpty(_price.sellGradeId) && _tables.TryGetSellGrade(_price.sellGradeId, out var grade)
            ? grade.sellPercent
            : _price.sellPercent;
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
            var fromRate = PuzzleTradeRate.Combine(_from, MatchingTagPrices(_tables, _from, tags));
            bool nativeSupply = (fromRate.BuyPercent < 100 && fromRate.SellPercent < 100) ||
                MatchesRegionSpecialty(region, tags);
            if (!nativeSupply)
            {
                continue;
            }
            var toRate = PuzzleTradeRate.Combine(_to, MatchingTagPrices(_tables, _to, tags));
            if (toRate.Tag != null && toRate.BuyPercent > 100 && toRate.SellPercent > 100)
            {
                return true;
            }
        }
        return false;
    }

    static bool MatchesRegionSpecialty(PuzzleRegionData _region, string[] _tags)
    {
        var tags = _tags ?? Array.Empty<string>();
        return (_region?.specialtyTags ?? Array.Empty<string>()).Any(_tag => tags.Contains(_tag));
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
        Check(prices.Any(_price => _price != null && _price.tag == tag && _price.buyPercent < 100 &&
            SellPercentOf(_tables, _price) < 100),
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
                $"{_path}[{group.id}].pieceIds", "각 추첨 그룹에는 중복 없는 후보가 필요합니다.");
        }
        if (_tables.TryGetRegion(_city.regionId, out var region) && !region.tradeHub)
        {
            foreach (string id in regional.pieceIds ?? Array.Empty<string>())
            {
                if (_tables.TryGetPiece(id, out var definition))
                {
                    Check(MatchesRegionSpecialty(region, definition.tags), _path,
                        $"지역 재고 '{id}'는 지역 특산품 태그 중 하나 이상을 가져야 합니다.");
                }
            }
        }
        if (_city.market == null)
        {
            return;
        }
        var occupied = new HashSet<Vector2Int>();
        foreach (var placement in _city.market.startingPlacements ?? Array.Empty<PuzzleStartingPlacementData>())
        {
            if (placement == null || !_tables.TryGetPiece(placement.pieceId, out var piece))
            {
                continue;
            }
            Check(!piece.excludeFromMarket, _path, $"'{placement.pieceId}'는 시장 고정 재고에 둘 수 없습니다.", placement.pieceId, "excludeFromMarket");
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
        ValidateStockProfile(_tables, _city, general, _shapes, _placementIds, occupied, _path);
        ValidateStockProfile(_tables, _city, regional, _shapes, _placementIds, occupied, _path);
    }

    // 프로필 하나의 후보와 슬롯만 견준다. 다른 프로필의 후보가 이 프로필의 슬롯에 들어가야 할 이유는 없다.
    void ValidateStockProfile(TableManager _tables, PuzzleCityData _city, PuzzleMarketStockData _profile,
        Dictionary<PuzzlePieceData, Vector2Int[]> _shapes, HashSet<string> _placementIds,
        HashSet<Vector2Int> _occupied, string _path)
    {
        if (_profile == null)
        {
            return;
        }
        string profilePath = $"{_path}[{_profile.id}]";
        var candidates = _profile.pieceIds ?? Array.Empty<string>();
        var declaredSlots = _profile.slots ?? Array.Empty<PuzzleMarketStockSlotData>();
        Check(candidates.Length > 0, profilePath + ".pieceIds", "재고 추첨 후보가 필요합니다.");
        Check(declaredSlots.Length > 0, profilePath + ".slots", "재고 배치 슬롯이 필요합니다.");
        var shapes = new Dictionary<string, Vector2Int[]>(StringComparer.Ordinal);
        foreach (string id in candidates)
        {
            bool exists = _tables.TryGetPiece(id, out var piece);
            Reference(id, profilePath + ".pieceIds", exists, "재고 품목");
            if (exists)
            {
                Check(!piece.excludeFromMarket, profilePath + ".pieceIds", $"'{id}'는 시장 재고에 둘 수 없습니다.", id, "excludeFromMarket");
            }
            if (exists && _shapes.TryGetValue(piece, out var shape))
            {
                shapes[id] = shape;
            }
        }
        var slots = new List<PuzzleMarketStockSlotData>();
        for (int i = 0; i < declaredSlots.Length; i++)
        {
            var slot = declaredSlots[i];
            string path = $"{profilePath}.slots[{i}]";
            Check(slot != null, path, "재고 슬롯이 필요합니다.");
            if (slot == null)
            {
                continue;
            }
            Check(!string.IsNullOrWhiteSpace(slot.id) && _placementIds.Add(_city.id + "." + slot.id), path + ".id",
                "모든 시작 배치와 슬롯에서 고유한 ID가 필요합니다.");
            bool valid = slot.width > 0 && slot.height > 0 && slot.x >= 0 && slot.y >= 0 &&
                (long)slot.x + slot.width <= _city.market.width && slot.y < _city.market.height &&
                (long)slot.y - slot.height + 1 >= 0;
            Check(valid, path, "재고 슬롯 전체가 시장 격자 안에 있어야 합니다.");
            if (!valid)
            {
                continue;
            }
            slots.Add(slot);
            for (int x = 0; x < slot.width; x++)
            {
                for (int y = 0; y < slot.height; y++)
                {
                    Check(_occupied.Add(new Vector2Int(slot.x + x, slot.y - y)), path,
                        "재고 슬롯이 특산품 또는 다른 슬롯과 겹칩니다.");
                }
            }
        }
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool filled = shapes.Keys.OrderBy(_id => _id, StringComparer.Ordinal)
                .Any(_id => PuzzleRotation.TryFitIntoSlot(shapes[_id], slot.width, slot.height, out _));
            Check(filled, $"{profilePath}.slots[{slot.id}]",
                $"'{_profile.id}' 슬롯 '{slot.id}'({slot.width}×{slot.height})에 들어가는 후보가 없습니다.");
        }
        foreach (string id in shapes.Keys.OrderBy(_id => _id, StringComparer.Ordinal))
        {
            bool placed = slots.Any(_slot => PuzzleRotation.TryFitIntoSlot(shapes[id], _slot.width, _slot.height, out _));
            Check(placed, profilePath + ".pieceIds", $"'{_profile.id}' 후보 '{id}'가 이 프로필의 어떤 슬롯에도 들어가지 않습니다.",
                id, "cells");
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
        Check(_piece.shelfLifeHours >= 0, _path + ".shelfLifeHours", "유통기한은 0(시간 소모 없음) 이상의 시간이어야 합니다.");
        CheckTurnHours(_piece.shelfLifeHours, _path + ".shelfLifeHours", _piece.id, "shelfLifeHours");
        const string DURABILITY_MAX = "rules.durability.maximum";
        if (_tables.HasBasicNumber(DURABILITY_MAX))
        {
            Check(_piece.durability >= 1 && _piece.durability <= _tables.BasicNumber(DURABILITY_MAX).value,
                _path + ".durability", "내구도 시작값은 1 이상이며 기초데이터의 상한 이하여야 합니다.");
        }

        return ValidatePieceCells(_piece.cells, _piece.slotCount, _maxSide, _path);
    }

    public Vector2Int[] ValidatePieceCells(PuzzleCellData[] _cells, int _slotCount, int _maxSide, string _path)
    {
        string path = _path + ".cells";
        var cells = _cells ?? Array.Empty<PuzzleCellData>();
        Check(cells.Length > 0, path, "슬롯이 하나 이상 필요합니다.");
        Check(_slotCount > 0 && _slotCount == cells.Length, _path + ".slotCount",
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

    public void ValidateItemVisuals(IReadOnlyList<PuzzlePieceData> _items, Func<string, bool> _hasVisual, string _viewPath)
    {
        foreach (var item in _items)
        {
            if (item != null)
            {
                Check(_hasVisual(item.visualKey), "PuzzleTableData.pieces." + item.id + ".visualKey",
                    "'" + item.visualKey + "'에 해당하는 스프라이트가 '" + _viewPath + "'에 없습니다.");
            }
        }
    }

    HashSet<Vector2Int> ValidateBlockedCells(PuzzleBoardData _board, string _path)
    {
        var cells = _board.blockedCells ?? Array.Empty<PuzzleBoardCellData>();
        var blocked = new HashSet<Vector2Int>();
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            string path = $"{_path}.blockedCells[{i}]";
            if (cell == null)
            {
                Check(false, path, "사용 불가 칸 좌표가 누락되었습니다.");
                continue;
            }
            bool inBounds = cell.x >= 0 && cell.x < _board.width && cell.y >= 0 && cell.y < _board.height;
            Check(inBounds, path, "사용 불가 칸 좌표는 보드 안에 있어야 합니다.");
            if (inBounds)
            {
                Check(blocked.Add(new Vector2Int(cell.x, cell.y)), path, "사용 불가 칸 좌표가 중복되었습니다.");
            }
        }
        Check(blocked.Count < (long)_board.width * _board.height, _path + ".blockedCells",
            "사용 가능한 칸이 하나 이상 필요합니다.");
        return blocked;
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
        var blocked = ValidateBlockedCells(_board, _path);
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
                Check(inBounds, path, $"회전한 슬롯 ({x}, {y})이 격자 밖에 있습니다.", piece.id, "cells");
                if (!inBounds)
                {
                    continue;
                }

                var position = new Vector2Int((int)x, (int)y);
                Check(!blocked.Contains(position), path, $"슬롯 {position}은 사용할 수 없는 칸입니다.", piece.id, "cells");
                if (!occupied.TryAdd(position, path))
                {
                    Check(false, path, $"슬롯 {position}이 {occupied[position]}의 시작 배치와 겹칩니다.", piece.id, "cells");
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
