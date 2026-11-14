using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 역 시장·교역 편집과 저장 전 테이블 검증. 검증은 초안을 테이블 규칙(GameTableRules)에 그대로 넣어 오류를 모은다.
public sealed partial class WorldMapEditorWindow
{
    const float MARKET_CELL = 18f;
    const int ERROR_LIST_LIMIT = 30;
    static readonly string[] TURN_LABELS = { "0°", "90°", "180°", "270°" };
    List<string> draftErrors = new List<string>();
    bool validationDirty = true, showAllErrors;

    IEnumerable<JObject> Pieces => puzzle["pieces"].OfType<JObject>().OrderBy(_piece => (string)_piece["id"], StringComparer.Ordinal);
    IEnumerable<JObject> StockProfiles => puzzle["stockProfiles"].OfType<JObject>().OrderBy(_profile => (string)_profile["id"], StringComparer.Ordinal);
    string[] KnownTags => Pieces.SelectMany(_piece => _piece["tags"].Select(_tag => (string)_tag)).Distinct().OrderBy(_tag => _tag, StringComparer.Ordinal).ToArray();
    JObject Region(string _id) => Regions.FirstOrDefault(_region => (string)_region["id"] == _id);
    string PieceName(string _id) => (string)Pieces.FirstOrDefault(_piece => (string)_piece["id"] == _id)?["displayName"] ?? _id;

    List<string> DraftErrors
    {
        get
        {
            if (validationDirty && puzzle != null)
            {
                draftErrors = TableManager.ValidateSources(new Dictionary<Type, string>
                {
                    [typeof(PuzzleTableData)] = Json(puzzle),
                    [typeof(PuzzleRouteTableData)] = Json(routes),
                    [typeof(PuzzleCityActionTableData)] = draft.cityActions,
                    [typeof(PuzzleTradeTableData)] = draft.trade
                });
                validationDirty = false;
            }
            return draftErrors;
        }
    }

    void DrawValidationBadge()
    {
        int count = DraftErrors.Count;
        showAllErrors = GUILayout.Toggle(showAllErrors && count > 0, count == 0 ? "검증 통과" : "검증 오류 " + count, EditorStyles.toolbarButton) && count > 0;
    }

    void DrawDraftErrors()
    {
        if (!showAllErrors || DraftErrors.Count == 0)
        {
            return;
        }
        var shown = DraftErrors.Take(ERROR_LIST_LIMIT).ToList();
        if (DraftErrors.Count > shown.Count)
        {
            shown.Add("외 " + (DraftErrors.Count - shown.Count) + "건");
        }
        EditorGUILayout.HelpBox("저장하면 막히는 테이블 오류입니다.\n" + string.Join("\n", shown), MessageType.Error);
    }

    void DrawMarket(JObject _city)
    {
        var region = Region((string)_city["regionId"]);
        if (region == null)
        {
            return;
        }
        DrawSpecialty(_city, region);
        DrawStockProfile(_city, "regionalStockId", "지역 재고");
        DrawStockProfile(_city, "generalStockId", "일반 재고");
        EditorGUILayout.Space();
        DrawTagPrices(_city);
        EditorGUILayout.Space();
        DrawStartingStock(_city);
        EditorGUILayout.Space();
        DrawCityErrors(_city);
    }

    void DrawSpecialty(JObject _city, JObject _region)
    {
        bool tradeHub = (bool?)_region["tradeHub"] == true;
        var production = (_region["productionTags"] ?? new JArray()).Select(_tag => (string)_tag).ToArray();
        string current = (string)_city["specialtyTag"] ?? "";
        var values = (tradeHub ? new[] { "" }.Concat(KnownTags) : production).ToList();
        if (!values.Contains(current))
        {
            values.Insert(0, current);
        }
        string[] labels = values.Select(_tag => _tag == "" ? "(없음)" : tradeHub || production.Contains(_tag) ? _tag : _tag + " (지역 생산 목록 밖)").ToArray();
        int index = values.IndexOf(current);
        int next = EditorGUILayout.Popup("특산 태그", index, labels);
        if (next != index)
        {
            Change("특산 태그", () => _city["specialtyTag"] = values[next]);
        }
        EditorGUILayout.LabelField(tradeHub
            ? "교역 중심지 지역이라 특산 태그가 없어도 됩니다."
            : "특산 태그는 지역 생산 태그 중 하나이며, 태그 가격에서 구매·판매가 모두 100% 미만이어야 하고 고정 재고는 모두 이 태그를 가져야 합니다.", EditorStyles.wordWrappedMiniLabel);
    }

    void DrawStockProfile(JObject _city, string _key, string _label)
    {
        string current = (string)_city[_key] ?? "";
        var profiles = StockProfiles.ToArray();
        var ids = profiles.Select(_profile => (string)_profile["id"]).ToList();
        if (!ids.Contains(current))
        {
            ids.Insert(0, current);
        }
        int index = ids.IndexOf(current);
        int next = EditorGUILayout.Popup(_label, index, ids.Select(_id => _id == "" ? "(없음)" : _id).ToArray());
        if (next != index)
        {
            Change(_label, () => _city[_key] = ids[next]);
        }
        var profile = profiles.FirstOrDefault(_profile => (string)_profile["id"] == current);
        if (profile != null)
        {
            EditorGUILayout.LabelField("후보: " + string.Join(", ", profile["pieceIds"].Select(_id => PieceName((string)_id))), EditorStyles.wordWrappedMiniLabel);
        }
    }

    void DrawTagPrices(JObject _city)
    {
        EditorGUILayout.LabelField("태그 가격", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("기본 배율에 더하는 태그별 구매·판매 %입니다. 100 미만은 풍부(싸게 사고 싸게 팖), 초과는 귀함입니다. 우선순위가 높은 항목부터 적용하며 역 안에서 중복될 수 없습니다.", EditorStyles.wordWrappedMiniLabel);
        var prices = _city["tagPrices"] as JArray ?? new JArray();
        string[] tags = KnownTags;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("태그", EditorStyles.miniLabel, GUILayout.Width(110f));
            GUILayout.Label("구매%", EditorStyles.miniLabel, GUILayout.Width(48f));
            GUILayout.Label("판매%", EditorStyles.miniLabel, GUILayout.Width(48f));
            GUILayout.Label("우선순위", EditorStyles.miniLabel, GUILayout.Width(56f));
        }
        foreach (var entry in prices.OfType<JObject>().ToArray())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string tag = (string)entry["tag"];
                var options = tags.Contains(tag) ? tags : new[] { tag }.Concat(tags).ToArray();
                int index = Array.IndexOf(options, tag);
                int next = EditorGUILayout.Popup(index, options, GUILayout.Width(110f));
                if (next != index)
                {
                    Change("태그 가격 태그", () => entry["tag"] = options[next]);
                }
                PriceField(entry, "buyPercent", "태그 구매 배율", 48f);
                PriceField(entry, "sellPercent", "태그 판매 배율", 48f);
                PriceField(entry, "priority", "태그 가격 우선순위", 56f);
                if (GUILayout.Button("−", GUILayout.Width(22f)))
                {
                    Change("태그 가격 삭제", () => entry.Remove());
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (GUILayout.Button("+ 태그 가격 추가"))
        {
            Change("태그 가격 추가", () =>
            {
                var used = prices.Select(_entry => (string)_entry["tag"]).ToArray();
                string tag = tags.FirstOrDefault(_tag => !used.Contains(_tag));
                if (tag == null)
                {
                    throw new InvalidOperationException("추가할 수 있는 태그가 없습니다.");
                }
                if (_city["tagPrices"] == null)
                {
                    _city["tagPrices"] = prices;
                }
                int priority = prices.Count == 0 ? 100 : prices.Max(_entry => (int)_entry["priority"]) + 1;
                prices.Add(new JObject { ["tag"] = tag, ["priority"] = priority, ["buyPercent"] = 100, ["sellPercent"] = 100 });
            });
            GUIUtility.ExitGUI();
        }
    }

    void PriceField(JObject _entry, string _key, string _label, float _width)
    {
        int original = (int)_entry[_key];
        int value = EditorGUILayout.DelayedIntField(original, GUILayout.Width(_width));
        if (value != original)
        {
            Change(_label, () => _entry[_key] = value);
        }
    }

    void DrawStartingStock(JObject _city)
    {
        var market = _city["market"] as JObject;
        if (market == null)
        {
            return;
        }
        EditorGUILayout.LabelField("시장 격자·고정 재고", EditorStyles.boldLabel);
        IntField(market, "width", "격자 너비");
        IntField(market, "height", "격자 높이");
        var placements = market["startingPlacements"] as JArray ?? new JArray();
        DrawMarketGrid(market, placements);
        EditorGUILayout.LabelField("고정 재고는 역에 들를 때마다 놓이는 화물입니다. x·y는 화물의 왼쪽 위 칸이며 y는 위로 갈수록 커집니다. 배치 ID는 자동으로 붙습니다.", EditorStyles.wordWrappedMiniLabel);
        var pieces = Pieces.ToArray();
        string[] pieceLabels = pieces.Select(_piece => (string)_piece["displayName"] + " [" + string.Join(",", _piece["tags"].Select(_tag => (string)_tag)) + "]").ToArray();
        var rows = placements.OfType<JObject>().ToArray();
        for (int i = 0; i < rows.Length; i++)
        {
            var placement = rows[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect swatch = GUILayoutUtility.GetRect(12f, 18f, GUILayout.Width(12f));
                EditorGUI.DrawRect(swatch, PlacementColor(i));
                int index = Array.FindIndex(pieces, _piece => (string)_piece["id"] == (string)placement["pieceId"]);
                int next = EditorGUILayout.Popup(index, pieceLabels, GUILayout.Width(130f));
                if (next >= 0 && next != index)
                {
                    Change("고정 재고 화물", () => placement["pieceId"] = pieces[next]["id"].DeepClone());
                }
                PriceField(placement, "x", "고정 재고 x", 30f);
                PriceField(placement, "y", "고정 재고 y", 30f);
                int turns = (int)placement["clockwiseTurns"];
                int nextTurns = EditorGUILayout.Popup(turns, TURN_LABELS, GUILayout.Width(50f));
                if (nextTurns != turns)
                {
                    Change("고정 재고 회전", () => placement["clockwiseTurns"] = nextTurns);
                }
                if (GUILayout.Button("−", GUILayout.Width(22f)))
                {
                    Change("고정 재고 삭제", () =>
                    {
                        placement.Remove();
                        RenamePlacements(_city);
                    });
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (GUILayout.Button("+ 고정 재고 추가"))
        {
            Change("고정 재고 추가", () => AddPlacement(_city, market, placements));
            GUIUtility.ExitGUI();
        }
    }

    // 특산 태그를 가진 첫 화물을 빈 자리 중 가장 위·왼쪽에 놓는다.
    void AddPlacement(JObject _city, JObject _market, JArray _placements)
    {
        string specialty = (string)_city["specialtyTag"];
        var piece = Pieces.FirstOrDefault(_piece => _piece["tags"].Any(_tag => (string)_tag == specialty)) ?? Pieces.First();
        int width = (int)_market["width"], height = (int)_market["height"];
        var occupied = OccupiedCells(_placements).Select(_cell => _cell.position).ToHashSet();
        var shape = Shape(piece);
        var rotation = new PuzzleRotation(shape, 0);
        Vector2Int origin = new Vector2Int(0, height - 1);
        bool found = false;
        for (int y = height - 1; y >= 0 && !found; y--)
        {
            for (int x = 0; x < width && !found; x++)
            {
                var cells = shape.Select(_cell => new Vector2Int(x, y) + rotation.GetBoardOffset(_cell)).ToArray();
                if (cells.All(_cell => _cell.x >= 0 && _cell.x < width && _cell.y >= 0 && _cell.y < height && !occupied.Contains(_cell)))
                {
                    origin = new Vector2Int(x, y);
                    found = true;
                }
            }
        }
        if (_market["startingPlacements"] == null)
        {
            _market["startingPlacements"] = _placements;
        }
        _placements.Add(new JObject { ["id"] = "", ["pieceId"] = piece["id"].DeepClone(), ["x"] = origin.x, ["y"] = origin.y, ["clockwiseTurns"] = 0 });
        RenamePlacements(_city);
    }

    static Vector2Int[] Shape(JObject _piece)
    {
        return _piece["cells"].Select(_cell => new Vector2Int((int)_cell["x"], (int)_cell["y"])).ToArray();
    }

    IEnumerable<(int index, Vector2Int position)> OccupiedCells(JArray _placements)
    {
        var rows = _placements.OfType<JObject>().ToArray();
        for (int i = 0; i < rows.Length; i++)
        {
            var piece = Pieces.FirstOrDefault(_piece => (string)_piece["id"] == (string)rows[i]["pieceId"]);
            int turns = (int)rows[i]["clockwiseTurns"];
            if (piece == null || turns < 0 || turns >= PuzzleRotation.TURN_COUNT)
            {
                continue;
            }
            var shape = Shape(piece);
            var rotation = new PuzzleRotation(shape, turns);
            var origin = new Vector2Int((int)rows[i]["x"], (int)rows[i]["y"]);
            foreach (var cell in shape)
            {
                yield return (i, origin + rotation.GetBoardOffset(cell));
            }
        }
    }

    void DrawMarketGrid(JObject _market, JArray _placements)
    {
        int width = Mathf.Max(0, (int)_market["width"]), height = Mathf.Max(0, (int)_market["height"]);
        if (width == 0 || height == 0)
        {
            return;
        }
        Rect area = GUILayoutUtility.GetRect(width * MARKET_CELL, height * MARKET_CELL, GUILayout.Width(width * MARKET_CELL), GUILayout.Height(height * MARKET_CELL));
        var owners = new Dictionary<Vector2Int, int>();
        var overlaps = new HashSet<Vector2Int>();
        int outside = 0;
        foreach (var (index, position) in OccupiedCells(_placements))
        {
            if (position.x < 0 || position.x >= width || position.y < 0 || position.y >= height)
            {
                outside++;
            }
            else if (!owners.TryAdd(position, index))
            {
                overlaps.Add(position);
            }
        }
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var position = new Vector2Int(x, y);
                Rect cell = new Rect(area.x + x * MARKET_CELL, area.y + (height - 1 - y) * MARKET_CELL, MARKET_CELL - 1f, MARKET_CELL - 1f);
                Color color = overlaps.Contains(position) ? Color.red : owners.TryGetValue(position, out int owner) ? PlacementColor(owner) : new Color(0.4f, 0.4f, 0.4f);
                EditorGUI.DrawRect(cell, color);
            }
        }
        if (overlaps.Count > 0 || outside > 0)
        {
            EditorGUILayout.HelpBox("겹친 칸 " + overlaps.Count + "개(빨강) · 격자 밖 칸 " + outside + "개", MessageType.Warning);
        }
    }

    static Color PlacementColor(int _index)
    {
        return Color.HSVToRGB(_index * 0.13f % 1f, 0.45f, 0.95f);
    }

    void DrawCityErrors(JObject _city)
    {
        string prefix = "[PuzzleTableData.cities[" + ((JArray)puzzle["cities"]).IndexOf(_city) + "]";
        var errors = DraftErrors.Where(_error => _error.StartsWith(prefix, StringComparison.Ordinal))
            .Select(_error => _error.Substring(prefix.Length).TrimStart('.')).ToArray();
        if (errors.Length == 0)
        {
            EditorGUILayout.LabelField("이 역의 시장·교역 설정은 테이블 검증을 통과합니다.", EditorStyles.wordWrappedMiniLabel);
            return;
        }
        EditorGUILayout.HelpBox("이 역의 테이블 오류\n" + string.Join("\n", errors.Distinct()), MessageType.Error);
    }

    // 지역 재고 후보는 같은 재고 설정을 쓰는 모든 역에 적용된다.
    void DrawRegionStock(JObject _region)
    {
        string regionId = (string)_region["id"];
        var used = Cities.Where(_city => (string)_city["regionId"] == regionId).Select(_city => (string)_city["regionalStockId"]).Distinct().ToArray();
        var specialties = (_region["specialtyTags"] ?? new JArray()).Select(_tag => (string)_tag).OrderBy(_tag => _tag, StringComparer.Ordinal);
        var pieces = Pieces.ToArray();
        foreach (var profile in StockProfiles.Where(_profile => used.Contains((string)_profile["id"])).ToArray())
        {
            string id = (string)profile["id"];
            EditorGUILayout.LabelField("지역 재고 후보 · " + id, EditorStyles.boldLabel);
            var shared = Cities.Where(_city => (string)_city["regionalStockId"] == id && (string)_city["regionId"] != regionId)
                .Select(_city => (string)Region((string)_city["regionId"])?["displayName"]).Distinct().ToArray();
            if (shared.Length > 0)
            {
                EditorGUILayout.LabelField("다른 지역과 함께 씁니다: " + string.Join(", ", shared), EditorStyles.wordWrappedMiniLabel);
            }
            if ((bool?)_region["tradeHub"] != true)
            {
                EditorGUILayout.LabelField("지역 재고 후보는 대표 특산 태그(" + string.Join(", ", specialties) + ") 중 하나 이상을 가져야 합니다.", EditorStyles.wordWrappedMiniLabel);
            }
            var ids = profile["pieceIds"].Select(_id => (string)_id).ToArray();
            foreach (var piece in pieces)
            {
                string pieceId = (string)piece["id"];
                bool on = ids.Contains(pieceId);
                bool next = EditorGUILayout.ToggleLeft((string)piece["displayName"] + "  [" + string.Join(",", piece["tags"].Select(_tag => (string)_tag)) + "]", on);
                if (next != on)
                {
                    Change("지역 재고 후보", () => profile["pieceIds"] = new JArray(next
                        ? ids.Append(pieceId).OrderBy(_id => _id, StringComparer.Ordinal)
                        : ids.Where(_id => _id != pieceId)));
                }
            }
        }
    }
}
