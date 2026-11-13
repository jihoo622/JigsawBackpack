using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 전역 테이블의 유일한 보관소. 씬 오브젝트나 실행 중 상태(GameObject·풀·씬 진행 상태)는 여기에 두지 않는다.
// 사용 규칙:
//   초기화    TableManager.Initialize()                       // 기동 중 한 번, 없어도 지연 로드된다
//   조회      TableManager.Instance.Upgrade(id)               // 모르는 키면 예외
//   안전하게  TableManager.Instance.TryGetScene(name, out ..)  // 없으면 false
//   차단      TableManager.Instance.RequireValid()            // 잘못된 테이블로 동작하지 않게 초기화 거부
public sealed class TableManager
{
    static TableManager instance;
    public static TableManager Instance
    {
        get
        {
            return instance ??= new TableManager();
        }
    }
    const string RESOURCE_FOLDER = "Tables/";

    public GameplayTableData Gameplay { get; }
    public BasicDataTableData Basic { get; }
    public FlowTableData Flow { get; }
    public PuzzleTableData Puzzle { get; }
    public PuzzleSynergyTableData PuzzleSynergy { get; }
    public PuzzleCityActionTableData PuzzleCityAction { get; }
    public PuzzleTradeTableData PuzzleTrade { get; }
    public PuzzleRouteTableData PuzzleRoute { get; }
    public PuzzleTimeTableData PuzzleTime { get; }
    public IReadOnlyList<PuzzleSynergyRuleData> SynergyRules { get; }
    public IReadOnlyList<PuzzleCityActionData> CityActions { get; }
    public IReadOnlyList<PuzzlePieceData> Pieces { get; }
    public IReadOnlyList<PuzzleCityData> Cities { get; }
    public IReadOnlyList<PuzzleStartingPlacementData> TrainStartingPlacements { get; }
    public bool IsValid { get; }

    readonly Dictionary<string, UpgradeData> upgrades;
    readonly Dictionary<string, BasicNumberData> basicNumbers;
    readonly Dictionary<string, BasicTextData> basicTexts;
    readonly Dictionary<string, BasicColorData> basicColors;
    internal IReadOnlyDictionary<string, BasicNumberData> BasicNumbers => basicNumbers;
    internal IReadOnlyDictionary<string, BasicTextData> BasicTexts => basicTexts;
    internal IReadOnlyDictionary<string, BasicColorData> BasicColors => basicColors;
    readonly Dictionary<string, SceneLinkData> scenes;
    readonly Dictionary<string, PuzzlePieceData> pieces;
    readonly Dictionary<string, PuzzleAnimalBreedingData> animalBreeding;
    readonly Dictionary<string, PuzzleCityData> cities;
    readonly Dictionary<string, PuzzleRegionData> regions;
    readonly Dictionary<string, PuzzleMarketStockData> stockProfiles;
    readonly Dictionary<string, Dictionary<string, PuzzleCityTagPriceData>> cityTagPrices;
    readonly Dictionary<string, PuzzleCityActionData> cityActions;
    readonly Dictionary<string, List<PuzzleCityActionData>> actionsByCity;
    readonly Dictionary<string, PuzzleActionModeData> actionModes;
    readonly Dictionary<string, Dictionary<string, PuzzleRouteData>> routes;
    readonly Dictionary<string, IReadOnlyList<PuzzleRouteData>> routesFrom;

    TableManager() : this(null, null)
    {
    }

    // _sources: 테이블 클래스별 대체 JSON(편집 초안 검증용). _errors: 검증 오류를 콘솔 대신 모을 목록.
    TableManager(IReadOnlyDictionary<Type, string> _sources, List<string> _errors)
    {
        bool loaded = true;
        Basic = Load<BasicDataTableData>(ref loaded, _sources);
        Gameplay = Load<GameplayTableData>(ref loaded, _sources);
        Flow = Load<FlowTableData>(ref loaded, _sources);
        Puzzle = Load<PuzzleTableData>(ref loaded, _sources);
        PuzzleSynergy = Load<PuzzleSynergyTableData>(ref loaded, _sources);
        PuzzleCityAction = Load<PuzzleCityActionTableData>(ref loaded, _sources);
        PuzzleTrade = Load<PuzzleTradeTableData>(ref loaded, _sources);
        PuzzleRoute = Load<PuzzleRouteTableData>(ref loaded, _sources);
        PuzzleTime = Load<PuzzleTimeTableData>(ref loaded, _sources);

        var rules = _errors == null ? new GameTableRules() : new GameTableRules(_errors);
        var basicGroups = rules.Index(Basic.groups, _group => _group.id, "BasicDataTableData.groups");
        basicNumbers = IndexBasic(basicGroups.Values, _group => _group.numbers, _row => _row.id, "numbers", rules);
        basicTexts = IndexBasic(basicGroups.Values, _group => _group.texts, _row => _row.id, "texts", rules);
        basicColors = IndexBasic(basicGroups.Values, _group => _group.colors, _row => _row.id, "colors", rules);
        upgrades = rules.Index(Gameplay.upgrades, e => e.id, "GameplayTableData.upgrades");
        scenes = rules.Index(Flow.scenes, e => e.scene, "FlowTableData.scenes");
        pieces = rules.Index(Puzzle.pieces, e => e.id, "PuzzleTableData.pieces");
        cities = rules.Index(Puzzle.cities, e => e.id, "PuzzleTableData.cities");
        regions = rules.Index(Puzzle.regions, entry => entry.id, "PuzzleTableData.regions");
        stockProfiles = rules.Index(Puzzle.stockProfiles, _entry => _entry.id, "PuzzleTableData.stockProfiles");
        cityTagPrices = IndexCityTagPrices(Puzzle.cities);
        routes = IndexRoutes(PuzzleRoute.routes);
        routesFrom = IndexRoutesFrom(routes);
        var synergyRules = rules.Index(PuzzleSynergy.rules, e => e.id, "PuzzleSynergyTableData.rules");
        animalBreeding = rules.Index(PuzzleSynergy.animals?.breeding, _row => _row.parentPieceId,
            "PuzzleSynergyTableData.animals.breeding");
        SynergyRules = Array.AsReadOnly(synergyRules.Values.OrderBy(entry => entry.id, StringComparer.Ordinal).ToArray());
        cityActions = rules.Index(PuzzleCityAction.actions, e => e.id, "PuzzleCityActionTableData.actions");
        actionsByCity = IndexActionsByCity(cityActions);
        actionModes = IndexActionModes(cityActions, rules);
        CityActions = Array.AsReadOnly(cityActions.Values.OrderBy(entry => entry.id, StringComparer.Ordinal).ToArray());
        Pieces = Array.AsReadOnly(pieces.Values.OrderBy(entry => entry.id, StringComparer.Ordinal).ToArray());
        TrainStartingPlacements = SortStartingPlacements(Puzzle.train);
        Cities = Array.AsReadOnly(cities.Values.OrderBy(entry => entry.id == Puzzle.startCity ? 0 : 1)
            .ThenBy(entry => entry.id, StringComparer.Ordinal).ToArray());
        // 노출용 목록은 배열 순서에 의존하지 않는다. 사슬의 첫 항목 우선, 이후 id 오름차순의 결정적 순서.
        string first = Gameplay.sequence == null ? null : Gameplay.sequence.firstUpgrade;
        Upgrades = (Gameplay.upgrades ?? Array.Empty<UpgradeData>())
            .Where(entry => entry != null && !string.IsNullOrEmpty(entry.id))
            .OrderBy(entry => entry.id == first ? 0 : 1)
            .ThenBy(entry => entry.id, StringComparer.Ordinal)
            .ToArray();

        rules.Validate(this);
        IsValid = loaded && rules.IsValid;
    }

    internal bool TryGetRegion(string _id, out PuzzleRegionData _region)
    {
        if (string.IsNullOrEmpty(_id))
        {
            _region = null;
            return false;
        }
        return regions.TryGetValue(_id, out _region);
    }

    sealed class ScopedBasicData<T> where T : class
    {
        public string id;
        public T value;
    }

    static Dictionary<string, T> IndexBasic<T>(IEnumerable<BasicDataGroupData> _groups,
        Func<BasicDataGroupData, T[]> _entries, Func<T, string> _id, string _kind, GameTableRules _rules) where T : class
    {
        var scoped = new List<ScopedBasicData<T>>();
        foreach (var group in _groups)
        {
            var entries = _rules.Index(_entries(group), _id, "BasicDataTableData.groups." + group.id + "." + _kind);
            foreach (var pair in entries)
            {
                scoped.Add(new ScopedBasicData<T>
                {
                    id = group.id + "." + pair.Key,
                    value = pair.Value
                });
            }
        }
        return _rules.Index(scoped.ToArray(), _row => _row.id, "BasicDataTableData." + _kind)
            .ToDictionary(_pair => _pair.Key, _pair => _pair.Value.value, StringComparer.Ordinal);
    }

    internal PuzzleRegionData Region(string _id)
    {
        return regions[_id];
    }

    static IReadOnlyList<PuzzleStartingPlacementData> SortStartingPlacements(PuzzleBoardData _board)
    {
        if (_board?.startingPlacements == null)
        {
            return Array.Empty<PuzzleStartingPlacementData>();
        }

        return Array.AsReadOnly(_board.startingPlacements.Where(entry => entry != null)
            .OrderBy(entry => entry.id, StringComparer.Ordinal).ToArray());
    }

    // 도시 안에서 태그로 바로 찾을 수 있게 색인한다. 중복 태그·우선순위는 GameTableRules가 오류로 보고한다.
    static Dictionary<string, Dictionary<string, PuzzleCityTagPriceData>> IndexCityTagPrices(
        PuzzleCityData[] _cities)
    {
        var result = new Dictionary<string, Dictionary<string, PuzzleCityTagPriceData>>(StringComparer.Ordinal);
        foreach (var city in _cities ?? Array.Empty<PuzzleCityData>())
        {
            if (city == null || string.IsNullOrEmpty(city.id))
            {
                continue;
            }
            if (!result.TryGetValue(city.id, out var tags))
            {
                tags = new Dictionary<string, PuzzleCityTagPriceData>(StringComparer.Ordinal);
                result.Add(city.id, tags);
            }
            foreach (var tagPrice in city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>())
            {
                if (tagPrice == null || string.IsNullOrEmpty(tagPrice.tag))
                {
                    continue;
                }
                tags.TryAdd(tagPrice.tag, tagPrice);
            }
        }
        return result;
    }

    internal bool TryGetCityTagPrice(string _cityId, string _tag, out PuzzleCityTagPriceData _price)
    {
        if (!string.IsNullOrEmpty(_cityId) && !string.IsNullOrEmpty(_tag) &&
            cityTagPrices.TryGetValue(_cityId, out var tags) && tags.TryGetValue(_tag, out _price))
        {
            return true;
        }

        _price = null;
        return false;
    }

    // cityIds 역방향 색인. 한 도시 같은 타입의 중복 행은 GameTableRules가 오류로 보고하고 색인은 첫 항목만 남긴다.
    static Dictionary<string, List<PuzzleCityActionData>> IndexActionsByCity(
        Dictionary<string, PuzzleCityActionData> _actions)
    {
        var result = new Dictionary<string, List<PuzzleCityActionData>>(StringComparer.Ordinal);
        foreach (var action in _actions.Values.OrderBy(entry => entry.id, StringComparer.Ordinal))
        {
            foreach (string cityId in action.cityIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(cityId))
                {
                    continue;
                }
                if (!result.TryGetValue(cityId, out var list))
                {
                    list = new List<PuzzleCityActionData>();
                    result.Add(cityId, list);
                }
                list.Add(action);
            }
        }
        return result;
    }

    // 행동 모드의 id 색인. 행동 안 배열이 아니라 id로 조회하므로 배열 순서에 의존하지 않는다.
    static Dictionary<string, PuzzleActionModeData> IndexActionModes(
        Dictionary<string, PuzzleCityActionData> _actions, GameTableRules _rules)
    {
        var modes = _actions.Values.SelectMany(_action => _action.modes ?? Array.Empty<PuzzleActionModeData>())
            .Where(_mode => _mode != null).ToArray();
        return _rules.Index(modes, _mode => _mode.id, "PuzzleCityActionTableData.modes");
    }

    // 특정 도시의 역에서 사용할 수 있는 행동. id 오름차순이라 배열 순서에 의존하지 않는다.
    internal IReadOnlyList<PuzzleCityActionData> CityActionsOf(string _cityId)
    {
        if (!string.IsNullOrEmpty(_cityId) && actionsByCity.TryGetValue(_cityId, out var list))
        {
            return list;
        }

        return Array.Empty<PuzzleCityActionData>();
    }

    internal bool TryGetCityAction(string _cityId, PuzzleCityActionType _type, out PuzzleCityActionData _action)
    {
        foreach (var action in CityActionsOf(_cityId))
        {
            if (TryGetActionType(action, out var type) && type == _type)
            {
                _action = action;
                return true;
            }
        }

        _action = null;
        return false;
    }

    internal bool HasCityAction(string _cityId, PuzzleCityActionType _type)
    {
        return TryGetCityAction(_cityId, _type, out _);
    }

    internal PuzzleCityActionData CityAction(string _id)
    {
        return Find(cityActions, _id, "마을 행동");
    }

    // id로 찾는 행동 모드. 행동 안 배열 순서에 의존하지 않는다.
    internal bool TryGetActionMode(string _id, out PuzzleActionModeData _mode)
    {
        if (!string.IsNullOrEmpty(_id) && actionModes.TryGetValue(_id, out _mode))
        {
            return true;
        }

        _mode = null;
        return false;
    }

    // 행동 안의 모드 목록. id 오름차순이라 배열 순서에 의존하지 않는다.
    internal IReadOnlyList<PuzzleActionModeData> ActionModesOf(PuzzleCityActionData _action)
    {
        if (_action == null)
        {
            return Array.Empty<PuzzleActionModeData>();
        }

        return Array.AsReadOnly((_action.modes ?? Array.Empty<PuzzleActionModeData>())
            .Where(_mode => _mode != null)
            .OrderBy(_mode => _mode.id, StringComparer.Ordinal).ToArray());
    }

    // type 문자열과 enum 이름을 대조한다. 숫자 문자열·알 수 없는 값은 false이고 검증에서 오류로 보고된다.
    internal static bool TryGetActionType(PuzzleCityActionData _action, out PuzzleCityActionType _type)
    {
        _type = default;
        if (_action == null || string.IsNullOrEmpty(_action.type))
        {
            return false;
        }

        return Enum.TryParse(_action.type, out _type) && Enum.IsDefined(typeof(PuzzleCityActionType), _type);
    }

    internal IReadOnlyList<PuzzleSynergyRuleData> SynergiesFor(PuzzleSynergyScope _scope)
    {
        string scope = _scope.ToString();
        return Array.AsReadOnly(SynergyRules.Where(rule => rule.scope == scope ||
            rule.scope == nameof(PuzzleSynergyScope.Both)).ToArray());
    }

    // 사슬의 첫 해금 id. 없으면 빈 문자열.
    public string FirstUpgradeId
    {
        get
        {
            return Gameplay.sequence == null ? string.Empty : Gameplay.sequence.firstUpgrade ?? string.Empty;
        }
    }
    // UI 등에 노출하는 해금 목록.
    public IReadOnlyList<UpgradeData> Upgrades { get; }
    public IReadOnlyList<SceneLinkData> Scenes
    {
        get
        {
            return Flow.scenes ?? Array.Empty<SceneLinkData>();
        }
    }

    public static TableManager Initialize()
    {
        return Instance;
    }

    // 오류를 모두 보고한 뒤, 필수 데이터를 사용하는 기능만 명시적으로 초기화를 거부한다.
    public TableManager RequireValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("게임 테이블이 올바르지 않습니다. 먼저 출력된 테이블 오류를 수정하세요.");
        }
        return this;
    }

    public BasicNumberData BasicNumber(string _id) => Find(basicNumbers, _id, "BasicDataTableData.numbers");
    public BasicTextData BasicText(string _id) => Find(basicTexts, _id, "BasicDataTableData.texts");
    public BasicColorData BasicColor(string _id) => Find(basicColors, _id, "BasicDataTableData.colors");
    public bool HasBasicNumber(string _id) => basicNumbers.ContainsKey(_id);
    public bool HasBasicText(string _id) => basicTexts.ContainsKey(_id);
    public bool HasBasicColor(string _id) => basicColors.ContainsKey(_id);

    public UpgradeData Upgrade(string _id)
    {
        return Find(upgrades, _id, "해금");
    }
    public SceneLinkData Scene(string _scene)
    {
        return Find(scenes, _scene, "씬 진행");
    }
    public PuzzlePieceData Piece(string _id)
    {
        return Find(pieces, _id, "퍼즐 조각");
    }
    public bool TryGetPiece(string _id, out PuzzlePieceData _piece)
    {
        if (!string.IsNullOrEmpty(_id) && pieces.TryGetValue(_id, out _piece))
        {
            return true;
        }
        _piece = null;
        return false;
    }

    internal bool TryGetAnimalBreeding(string _pieceId, out PuzzleAnimalBreedingData _rule)
    {
        return animalBreeding.TryGetValue(_pieceId, out _rule);
    }
    internal bool TryGetStockProfile(string _id, out PuzzleMarketStockData _profile)
    {
        _profile = null;
        return !string.IsNullOrWhiteSpace(_id) && stockProfiles.TryGetValue(_id, out _profile);
    }

    internal PuzzleMarketStockData StockProfile(string _id)
    {
        return Find(stockProfiles, _id, "시장 재고 프로필");
    }

    public bool HasCity(string _id)
    {
        return !string.IsNullOrWhiteSpace(_id) && cities.ContainsKey(_id);
    }
    internal PuzzleCityData CityPrice(string _cityId)
    {
        return Find(cities, _cityId, "도시");
    }
    public IReadOnlyList<PuzzleStartingPlacementData> MarketStartingPlacements(string _cityId)
    {
        return SortStartingPlacements(Find(cities, _cityId, "도시").market);
    }
    // 출발 도시→도착 도시의 2중 색인. 방향이 다른 노선은 따로 보관하고, 중복 행은 GameTableRules가 오류로 보고한다.
    static Dictionary<string, Dictionary<string, PuzzleRouteData>> IndexRoutes(PuzzleRouteData[] _routes)
    {
        var result = new Dictionary<string, Dictionary<string, PuzzleRouteData>>(StringComparer.Ordinal);
        foreach (var route in _routes ?? Array.Empty<PuzzleRouteData>())
        {
            if (route == null || string.IsNullOrEmpty(route.fromCityId) || string.IsNullOrEmpty(route.toCityId))
            {
                continue;
            }
            if (!result.TryGetValue(route.fromCityId, out var targets))
            {
                targets = new Dictionary<string, PuzzleRouteData>(StringComparer.Ordinal);
                result.Add(route.fromCityId, targets);
            }
            targets.TryAdd(route.toCityId, route);
        }
        return result;
    }

    static Dictionary<string, IReadOnlyList<PuzzleRouteData>> IndexRoutesFrom(
        Dictionary<string, Dictionary<string, PuzzleRouteData>> _routes)
    {
        var result = new Dictionary<string, IReadOnlyList<PuzzleRouteData>>(StringComparer.Ordinal);
        foreach (var pair in _routes)
        {
            var sorted = pair.Value.Values
                .OrderBy(route => route.toCityId, StringComparer.Ordinal)
                .ToArray();
            result.Add(pair.Key, Array.AsReadOnly(sorted));
        }
        return result;
    }

    internal bool TryGetRoute(string _fromCityId, string _toCityId, out PuzzleRouteData _route)
    {
        if (!string.IsNullOrEmpty(_fromCityId) && !string.IsNullOrEmpty(_toCityId) &&
            routes.TryGetValue(_fromCityId, out var targets) && targets.TryGetValue(_toCityId, out _route))
        {
            return true;
        }

        _route = null;
        return false;
    }

    internal bool HasRoute(string _fromCityId, string _toCityId)
    {
        return TryGetRoute(_fromCityId, _toCityId, out _);
    }

    internal IReadOnlyList<PuzzleRouteData> RoutesFrom(string _cityId)
    {
        if (!string.IsNullOrEmpty(_cityId) && routesFrom.TryGetValue(_cityId, out var list))
        {
            return list;
        }

        return Array.Empty<PuzzleRouteData>();
    }

    public bool TryGetScene(string _scene, out SceneLinkData _link)
    {
        if (!string.IsNullOrEmpty(_scene) && scenes.TryGetValue(_scene, out _link))
        {
            return true;
        }
        _link = null;
        return false;
    }
    public bool TryGetUpgrade(string _id, out UpgradeData _upgrade)
    {
        if (!string.IsNullOrEmpty(_id) && upgrades.TryGetValue(_id, out _upgrade))
        {
            return true;
        }
        _upgrade = null;
        return false;
    }

    // nextScene 미참조 등 진행 경로를 따라갈 때 쓰는 빈 값 안전 조회.
    public string NextSceneOf(string _scene)
    {
        return TryGetScene(_scene, out var link) ? link.nextScene ?? string.Empty : string.Empty;
    }
    public string RestartSceneOf(string _scene)
    {
        return TryGetScene(_scene, out var link) ? string.IsNullOrEmpty(link.restartScene) ? link.scene : link.restartScene : _scene;
    }

    // 모르는 키면 기본값으로 조용히 동작하지 않고 예외로 막는다.
    static T Find<T>(Dictionary<string, T> _index, string _id, string _table) where T : class
    {
        if (!string.IsNullOrEmpty(_id) && _index.TryGetValue(_id, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"{_table} 테이블에 '{_id}' 키가 없습니다.");
    }

    static T Load<T>(ref bool _valid, IReadOnlyDictionary<Type, string> _sources = null) where T : class, new()
    {
        string path = RESOURCE_FOLDER + typeof(T).Name;
        var asset = Resources.Load<TextAsset>(path);
        if (!asset)
        {
            Debug.LogError($"테이블 누락: Resources/{path}.json");
            _valid = false;
            return new T();
        }
        try
        {
            string json = asset.text;
#if UNITY_EDITOR
            // 편집 도구의 연속 실행도 에셋 자동 새로고침 전 최신 원본을 읽는다.
            string diskPath = EditorAssetPath<T>();
            if (System.IO.File.Exists(diskPath))
            {
                json = System.IO.File.ReadAllText(diskPath);
            }
#endif
            if (_sources != null && _sources.TryGetValue(typeof(T), out var source))
            {
                json = source;
            }
            var table = JsonUtility.FromJson<T>(json);
            if (table != null)
            {
                return table;
            }
            Debug.LogError($"테이블 내용이 null입니다: {path}");
        }
        catch (Exception error) when (error is ArgumentException || error is System.IO.IOException || error is UnauthorizedAccessException)
        {
            Debug.LogError($"테이블 읽기 실패: {path}: {error.Message}");
        }
        _valid = false;
        return new T();
    }

    // 기존 데이터를 수정하는 테스트는 전후에 호출해 다음 테스트로 변경을 전파하지 않는다.
    public static void Reload()
    {
        instance = null;
    }

#if UNITY_EDITOR
    public static T ReadSourceForAuthoring<T>() where T : class, new()
    {
        bool loaded = true;
        var data = Load<T>(ref loaded);
        if (!loaded)
        {
            throw new InvalidOperationException("테이블 원본을 읽을 수 없습니다: " + typeof(T).Name);
        }
        return data;
    }

    // 저장 전 초안 검증. 지정한 테이블은 주어진 JSON으로 읽고, 오류는 콘솔 대신 목록으로 돌려준다.
    public static List<string> ValidateSources(IReadOnlyDictionary<Type, string> _sources)
    {
        var errors = new List<string>();
        try
        {
            var tables = new TableManager(_sources, errors);
            if (!tables.IsValid && errors.Count == 0)
            {
                errors.Add("테이블 원본을 읽지 못했습니다. 콘솔을 확인하세요.");
            }
        }
        catch (Exception exception)
        {
            errors.Add("검증 중 예외: " + exception.Message);
        }
        return errors;
    }

    public static string EditorAssetPath<T>()
    {
        return "Assets/Resources/" + RESOURCE_FOLDER + typeof(T).Name + ".json";
    }
    public static void ReloadAfterTableEdit()
    {
        Reload();
        Initialize();
    }
#endif

    // Enter Play Mode Options로 도메인 리로드를 꺼도 정적 상태는 매 실행 초기화된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        instance = null;
    }
}
