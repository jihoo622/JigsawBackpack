using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

public sealed partial class WorldMapEditorWindow : EditorWindow
{
    [SerializeField] WorldMapDraft draft;
    [SerializeField] string originalLayout;
    [SerializeField] string originalPuzzle;
    [SerializeField] string originalRoutes;
    [SerializeField] string originalGeography;
    [SerializeField] string originalCityActions;
    [SerializeField] string originalTrade;
    [SerializeField] int tab;
    [SerializeField] string selectedId;
    [SerializeField] float snapStep = 10f;
    Vector2 listScroll, detailScroll;
    string search = "", newId = "", newName = "", newFrom, newTo;
    string message, rawText, rawKey;
    JObject layout, puzzle, routes;

    static string PuzzlePath => TableManager.EditorAssetPath<PuzzleTableData>();
    static string RoutesPath => TableManager.EditorAssetPath<PuzzleRouteTableData>();
    static string CityActionsPath => TableManager.EditorAssetPath<PuzzleCityActionTableData>();
    static string TradePath => TableManager.EditorAssetPath<PuzzleTradeTableData>();
    IEnumerable<JObject> Nodes => layout["nodes"].OfType<JObject>();
    IEnumerable<JObject> Rails => layout["rails"].OfType<JObject>();
    IEnumerable<JObject> Cities => puzzle["cities"].OfType<JObject>();
    IEnumerable<JObject> Regions => puzzle["regions"].OfType<JObject>();
    IEnumerable<JObject> Routes => routes["routes"].OfType<JObject>();
    bool Dirty => draft && (draft.layout != originalLayout || draft.puzzle != originalPuzzle || draft.routes != originalRoutes || draft.geography != originalGeography || draft.cityActions != originalCityActions || draft.trade != originalTrade);

    [MenuItem("Tools/MyJG/Map Editor")]
    public static void Open()
    {
        var window = GetWindow<WorldMapEditorWindow>("Map Editor");
        window.minSize = new Vector2(1080f, 620f);
    }

    void OnEnable()
    {
        minSize = new Vector2(1080f, 620f);
        wantsMouseMove = true;
        saveChangesMessage = "맵 편집 초안을 저장할까요? 테이블·레이아웃·지형·지도 프리팹을 함께 반영합니다.";
        Undo.undoRedoPerformed += RestoreDraft;
        if (draft)
        {
            RestoreDraft();
        }
        else
        {
            Reload();
        }
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= RestoreDraft;
        ReleaseCanvasDrag();
    }

    void OnDestroy()
    {
        if (terrainPreview)
        {
            DestroyImmediate(terrainPreview);
        }
        if (draft)
        {
            Undo.ClearUndo(draft);
            DestroyImmediate(draft);
        }
    }

    static string Json(JObject _value) => _value.ToString(Formatting.Indented) + "\n";
    static Vector2 Position(JObject _node) => new Vector2((float)_node["x"], (float)_node["y"]);
    JObject Node(string _id) => Nodes.Single(_node => (string)_node["cityId"] == _id);
    JObject City(string _id) => Cities.Single(_city => (string)_city["id"] == _id);
    string CityName(string _id) => (string)City(_id)["displayName"];
    static bool Finite(float _value) => !float.IsNaN(_value) && !float.IsInfinity(_value);

    void Reload()
    {
        try
        {
            originalLayout = File.ReadAllText(RailMapLayoutTool.LAYOUT_PATH);
            originalPuzzle = File.ReadAllText(PuzzlePath);
            originalRoutes = File.ReadAllText(RoutesPath);
            originalGeography = File.ReadAllText(WorldMapGeography.PATH);
            originalCityActions = File.ReadAllText(CityActionsPath);
            originalTrade = File.ReadAllText(TradePath);
            if (!draft)
            {
                draft = CreateInstance<WorldMapDraft>();
                draft.hideFlags = HideFlags.HideAndDontSave;
            }
            Undo.ClearUndo(draft);
            draft.layout = originalLayout;
            draft.puzzle = originalPuzzle;
            draft.routes = originalRoutes;
            draft.geography = originalGeography;
            draft.cityActions = originalCityActions;
            draft.trade = originalTrade;
            majorThreshold = null;
            RestoreDraft();
            fitCanvas = true;
            message = null;
        }
        catch (Exception exception)
        {
            layout = null;
            message = exception.Message;
        }
    }

    void RestoreDraft()
    {
        try
        {
            layout = JObject.Parse(draft.layout);
            puzzle = JObject.Parse(draft.puzzle);
            routes = JObject.Parse(draft.routes);
            if (draft.geography == null)
            {
                // 지형 편집 추가 전에 만든 초안은 디스크 지형을 기준으로 이어서 편집한다.
                originalGeography = File.ReadAllText(WorldMapGeography.PATH);
                draft.geography = originalGeography;
            }
            if (draft.cityActions == null || draft.trade == null)
            {
                // 역 ID 바꾸기 추가 전에 만든 초안은 디스크의 마을 행동·거래 테이블을 기준으로 이어서 편집한다.
                originalCityActions = File.ReadAllText(CityActionsPath);
                originalTrade = File.ReadAllText(TradePath);
                draft.cityActions = originalCityActions;
                draft.trade = originalTrade;
            }
            geography = WorldMapGeography.Parse(draft.geography);
            terrainPreviewDirty = true;
            validationDirty = true;
            RebasePathMigration();
            rawKey = null;
            hasUnsavedChanges = Dirty;
            Repaint();
            SceneView.RepaintAll();
        }
        catch (Exception exception)
        {
            message = exception.Message;
        }
    }

    void RebasePathMigration()
    {
        string currentLayout = File.ReadAllText(RailMapLayoutTool.LAYOUT_PATH);
        string currentRoutes = File.ReadAllText(RoutesPath);
        if (currentLayout == originalLayout && currentRoutes == originalRoutes)
        {
            return;
        }
        var baselineLayout = JObject.Parse(originalLayout);
        var baselineRoutes = JObject.Parse(originalRoutes);
        RailMapLayoutTool.SynchronizeRoutePoints(baselineLayout, baselineRoutes);
        foreach (var rail in baselineLayout["rails"].OfType<JObject>())
        {
            if (rail["labelManual"] == null)
            {
                rail["labelManual"] = true;
            }
        }
        var diskLayout = JObject.Parse(currentLayout);
        var diskRoutes = JObject.Parse(currentRoutes);
        baselineLayout.Remove("note");
        baselineRoutes.Remove("note");
        var comparableLayout = (JObject)diskLayout.DeepClone();
        var comparableRoutes = (JObject)diskRoutes.DeepClone();
        comparableLayout.Remove("note");
        comparableRoutes.Remove("note");
        if (!JToken.DeepEquals(baselineLayout, comparableLayout) || !JToken.DeepEquals(baselineRoutes, comparableRoutes))
        {
            // 사용자/외부의 다른 수정은 자동 병합하지 않고 기존 저장 충돌 검사로 보호한다.
            return;
        }
        RailMapLayoutTool.SynchronizeRoutePoints(layout, routes);
        foreach (var rail in Rails)
        {
            if (rail["labelManual"] == null)
            {
                rail["labelManual"] = true;
            }
        }
        layout["note"] = diskLayout["note"]?.DeepClone();
        routes["note"] = diskRoutes["note"]?.DeepClone();
        originalLayout = currentLayout;
        originalRoutes = currentRoutes;
        draft.layout = Json(layout);
        draft.routes = Json(routes);
    }

    void Change(string _name, Action _edit)
    {
        // 실패한 조작은 메모리의 원본도 복구한다.
        Undo.RecordObject(draft, _name);
        try
        {
            string geometry = autoTimeLabels ? LabelGeometryKey() : null;
            _edit();
            RailMapLayoutTool.SynchronizeRoutePoints(layout, routes);
            ValidateStructure();
            if (autoTimeLabels && geometry != LabelGeometryKey())
            {
                if (canvasDragControl != 0)
                {
                    pendingTimeLabels = true;
                }
                else
                {
                    PlaceTimeLabels(null);
                }
            }
            draft.layout = Json(layout);
            draft.puzzle = Json(puzzle);
            draft.routes = Json(routes);
            hasUnsavedChanges = Dirty;
            validationDirty = true;
            message = null;
            rawKey = null;
            Repaint();
            SceneView.RepaintAll();
        }
        catch (Exception exception)
        {
            RestoreDraft();
            message = exception.Message;
        }
    }

    void OnGUI()
    {
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton) && (!Dirty || EditorUtility.DisplayDialog("Map Editor", "저장하지 않은 초안을 버릴까요?", "버리기", "취소")))
                {
                    Reload();
                }
                if (GUILayout.Button("전체 보기", EditorStyles.toolbarButton))
                {
                    fitCanvas = true;
                }
                if (GUILayout.Button("초안 내보내기", EditorStyles.toolbarButton))
                {
                    ExportDraft();
                }
                using (new EditorGUI.DisabledScope(!Dirty))
                {
                    if (GUILayout.Button("전체 저장 · 프리팹 반영", EditorStyles.toolbarButton))
                    {
                        Save();
                    }
                }
                GUILayout.Label(Dirty ? "미저장 초안" : "저장됨", EditorStyles.miniLabel);
                if (layout != null && puzzle != null && routes != null)
                {
                    GUILayout.FlexibleSpace();
                    DrawValidationBadge();
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            if (layout == null || puzzle == null || routes == null)
            {
                return;
            }
            DrawDraftErrors();
            int nextTab = GUILayout.Toolbar(tab, new[] { "역", "노선", "지역", "지형", "공통 설정" });
            if (tab != nextTab)
            {
                tab = nextTab;
                selectedId = null;
                rawKey = null;
                GUIUtility.ExitGUI();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (tab < 3)
                {
                    DrawList();
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawCanvasToolbar();
                    Rect canvas = GUILayoutUtility.GetRect(250f, 250f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawCanvas(canvas);
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(340f)))
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (tab == 0)
                    {
                        DrawStation();
                    }
                    else if (tab == 1)
                    {
                        DrawRail();
                    }
                    else if (tab == 2)
                    {
                        DrawRegion();
                    }
                    else if (tab == TERRAIN_TAB)
                    {
                        DrawTerrain();
                    }
                    else
                    {
                        DrawSettings();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }
    }

    public override void SaveChanges()
    {
        Save();
        if (Dirty)
        {
            throw new InvalidOperationException("맵을 저장하지 못했습니다. 편집 창의 메시지를 확인하세요.");
        }
        base.SaveChanges();
    }

    public override void DiscardChanges()
    {
        Reload();
        base.DiscardChanges();
    }

    void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(190f)))
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            var rows = tab == 0 ? Cities : tab == 1 ? Rails : Regions;
            foreach (var row in rows.OrderBy(_row => (string)(_row[tab == 1 ? "sourceId" : "id"]), StringComparer.Ordinal).ToArray())
            {
                string id = (string)row[tab == 1 ? "sourceId" : "id"];
                string name = tab == 1 ? CityName((string)row["fromCityId"]) + " ↔ " + CityName((string)row["toCityId"]) : (string)row["displayName"];
                if (!string.IsNullOrEmpty(search) && (id + name).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (GUILayout.Toggle(selectedId == id, name + "\n" + id, "Button"))
                {
                    selectedId = id;
                }
            }
            EditorGUILayout.EndScrollView();
            if (tab == 0)
            {
                EditorGUILayout.LabelField("선택 역의 시장·교역 설정을 복제", EditorStyles.miniLabel);
                newName = EditorGUILayout.TextField("새 역 이름", newName);
                newId = EditorGUILayout.TextField("새 역 ID", newId);
                EditorGUILayout.LabelField("ID를 비우면 " + SuggestCityId(newName), EditorStyles.miniLabel);
                if (GUILayout.Button("선택 역을 템플릿으로 추가"))
                {
                    Change("역 추가", AddStation);
                    GUIUtility.ExitGUI();
                }
            }
            else if (tab == 1)
            {
                EditorGUILayout.LabelField("선택 노선의 분류·고속선 설정을 복제", EditorStyles.miniLabel);
                newFrom = CityPopup("출발", newFrom);
                newTo = CityPopup("도착", newTo);
                if (GUILayout.Button("두 역 사이 왕복 노선 추가"))
                {
                    Change("노선 추가", AddRail);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    string CityPopup(string _label, string _id)
    {
        var cities = Cities.OrderBy(_city => (string)_city["id"], StringComparer.Ordinal).ToArray();
        int index = Array.FindIndex(cities, _city => (string)_city["id"] == _id);
        string[] options = new[] { "선택하세요" }.Concat(cities.Select(_city => (string)_city["displayName"] + " (" + (string)_city["id"] + ")")).ToArray();
        int next = EditorGUILayout.Popup(_label, index + 1, options);
        return next == 0 ? null : (string)cities[next - 1]["id"];
    }

    void TextField(JObject _row, string _key, string _label)
    {
        string original = (string)_row[_key] ?? "";
        string value = EditorGUILayout.DelayedTextField(_label, original);
        if (value != original)
        {
            Change(_label, () => _row[_key] = value);
        }
    }

    void IntField(JObject _row, string _key, string _label)
    {
        int original = (int)_row[_key];
        int value = EditorGUILayout.DelayedIntField(_label, original);
        if (value != original)
        {
            Change(_label, () => _row[_key] = value);
        }
    }

    void BoolField(JObject _row, string _key, string _label)
    {
        bool original = (bool)_row[_key];
        bool value = EditorGUILayout.Toggle(_label, original);
        if (value != original)
        {
            Change(_label, () => _row[_key] = value);
        }
    }

    void DrawStation()
    {
        var city = Cities.FirstOrDefault(_city => (string)_city["id"] == selectedId);
        if (city == null)
        {
            EditorGUILayout.HelpBox("목록이나 가운데 지도에서 역 표식을 클릭하세요. 지도에서 드래그하면 위치를 바꿀 수 있습니다.", MessageType.Info);
            return;
        }
        var node = Node(selectedId);
        EditorGUILayout.LabelField((string)city["displayName"], EditorStyles.boldLabel);
        DrawStationId(city);
        string name = EditorGUILayout.DelayedTextField("역 이름", (string)city["displayName"]);
        if (name != (string)city["displayName"])
        {
            Change("역 이름", () =>
            {
                city["displayName"] = name;
                node["displayName"] = name;
            });
        }
        var regionRows = Regions.OrderBy(_region => (string)_region["id"], StringComparer.Ordinal).ToArray();
        int regionIndex = Array.FindIndex(regionRows, _region => (string)_region["id"] == (string)city["regionId"]);
        int nextRegion = EditorGUILayout.Popup("지역", regionIndex, regionRows.Select(_region => (string)_region["displayName"]).ToArray());
        if (nextRegion >= 0 && nextRegion != regionIndex)
        {
            Change("역 지역", () =>
            {
                city["regionId"] = regionRows[nextRegion]["id"].DeepClone();
                node["regionId"] = city["regionId"].DeepClone();
                ApplyRegionTrade(city);
                UpdateTopology();
            });
        }
        if (GUILayout.Button("교역 설정을 지역 기준으로 맞추기"))
        {
            Change("교역 설정 맞추기", () => ApplyRegionTrade(city));
        }
        IntField(city, "mapLabelImportance", "지명 중요도");
        DrawMajorStation(city);
        bool interchange = EditorGUILayout.Toggle("환승역 표식", (bool)node["interchange"]);
        if (interchange != (bool)node["interchange"])
        {
            Change("환승역 표식", () => node["interchange"] = interchange);
        }
        snapStep = EditorGUILayout.FloatField("격자 간격 (0: 해제)", snapStep);
        EditorGUI.BeginChangeCheck();
        Vector2 point = EditorGUILayout.Vector2Field("역 위치", Position(node));
        if (EditorGUI.EndChangeCheck())
        {
            Change("역 이동", () => MoveStation(selectedId, point));
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("시장·교역", EditorStyles.boldLabel);
        BoolField(city, "buyEnabled", "구매 허용");
        BoolField(city, "sellEnabled", "판매 허용");
        IntField(city, "buyPercentOfBaseValue", "기본 구매 배율 (%)");
        IntField(city, "sellPercentOfFinalValue", "기본 판매 배율 (%)");
        DrawMarket(city);
        DrawRaw(city, "역 상세 데이터", _updated =>
        {
            if ((string)_updated["id"] != selectedId)
            {
                throw new InvalidDataException("기존 역 ID는 바꿀 수 없습니다.");
            }
            city.Replace(_updated);
            node["displayName"] = _updated["displayName"].DeepClone();
            node["regionId"] = _updated["regionId"].DeepClone();
            UpdateTopology();
        });
        if (GUILayout.Button("이 역과 연결 노선 삭제") && EditorUtility.DisplayDialog("역 삭제", CityName(selectedId) + " 및 연결 노선을 초안에서 삭제할까요?", "삭제", "취소"))
        {
            Change("역 삭제", DeleteStation);
            GUIUtility.ExitGUI();
        }
    }

    void MoveStation(string _id, Vector2 _point)
    {
        if (!Finite(_point.x) || !Finite(_point.y))
        {
            throw new InvalidDataException("유한한 역 좌표가 필요합니다.");
        }
        Vector2 point = Snap(_point);
        foreach (var rail in Rails.Where(_rail => (string)_rail["fromCityId"] == _id || (string)_rail["toCityId"] == _id))
        {
            if (rail["displayPoints"] == null)
            {
                rail["displayPoints"] = rail["points"].DeepClone();
            }
            bool from = (string)rail["fromCityId"] == _id;
            SetEndpoint((JArray)rail["displayPoints"], from, point);
        }
        Node(_id)["x"] = point.x;
        Node(_id)["y"] = point.y;
    }

    Vector2 Snap(Vector2 _point)
    {
        return Finite(snapStep) && snapStep > 0f ? new Vector2(Mathf.Round(_point.x / snapStep) * snapStep, Mathf.Round(_point.y / snapStep) * snapStep) : _point;
    }

    static void SetEndpoint(JArray _points, bool _from, Vector2 _point)
    {
        int index = _from ? 0 : _points.Count - 2;
        _points[index] = _point.x;
        _points[index + 1] = _point.y;
    }

    JObject Forward(JObject _rail) => Routes.Single(_route => (string)_route["fromCityId"] == (string)_rail["fromCityId"] && (string)_route["toCityId"] == (string)_rail["toCityId"]);
    JObject Reverse(JObject _rail) => Routes.Single(_route => (string)_route["fromCityId"] == (string)_rail["toCityId"] && (string)_route["toCityId"] == (string)_rail["fromCityId"]);

    void SyncRouteGeometry(JObject _rail)
    {
        var points = (JArray)_rail["points"];
        Forward(_rail)["points"] = points.DeepClone();
        var reverse = new JArray();
        for (int i = points.Count - 2; i >= 0; i -= 2)
        {
            reverse.Add(points[i].DeepClone());
            reverse.Add(points[i + 1].DeepClone());
        }
        Reverse(_rail)["points"] = reverse;
    }

    void DrawRail()
    {
        var rail = Rails.FirstOrDefault(_rail => (string)_rail["sourceId"] == selectedId);
        if (rail == null)
        {
            EditorGUILayout.HelpBox("노선을 선택하거나 두 역 사이에 새 노선을 추가하세요.", MessageType.Info);
            return;
        }
        var forward = Forward(rail);
        var reverse = Reverse(rail);
        EditorGUILayout.LabelField(selectedId + "  " + CityName((string)rail["fromCityId"]) + " ↔ " + CityName((string)rail["toCityId"]), EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("연결 역을 바꾸면 표시·시간 계산 경로의 끝점을 새 역에 맞춥니다.", MessageType.None);
        string from = CityPopup("출발 역", (string)rail["fromCityId"]);
        string to = CityPopup("도착 역", (string)rail["toCityId"]);
        if (from != null && to != null && (from != (string)rail["fromCityId"] || to != (string)rail["toCityId"]))
        {
            Change("노선 연결 역 변경", () => Reconnect(rail, from, to));
            GUIUtility.ExitGUI();
        }
        int distance = EditorGUILayout.DelayedIntField("요금용 거리 (왕복)", (int)rail["distance"]);
        if (distance != (int)rail["distance"])
        {
            Change("노선 거리", () =>
            {
                rail["distance"] = distance;
                forward["distance"] = distance;
                reverse["distance"] = distance;
            });
        }
        DrawFarePreview(rail);
        long cost = EditorGUILayout.LongField("정방향 기본요금", (long)forward["cost"]);
        if (cost != (long)forward["cost"])
        {
            Change("정방향 요금", () =>
            {
                forward["cost"] = cost;
                rail["cost"] = cost;
            });
        }
        long reverseCost = EditorGUILayout.LongField("역방향 기본요금", (long)reverse["cost"]);
        if (reverseCost != (long)reverse["cost"])
        {
            Change("역방향 요금", () => reverse["cost"] = reverseCost);
        }
        bool highSpeed = EditorGUILayout.Toggle("고속선 (왕복)", (bool)forward["highSpeed"]);
        if (highSpeed != (bool)forward["highSpeed"])
        {
            Change("고속선", () =>
            {
                forward["highSpeed"] = highSpeed;
                reverse["highSpeed"] = highSpeed;
            });
        }
        var groups = Regions.OrderBy(_region => (string)_region["id"], StringComparer.Ordinal).Select(_region => (string)_region["id"]).Concat(new[] { "line.regional" }).ToArray();
        int group = Array.IndexOf(groups, (string)rail["regionId"]);
        int nextGroup = EditorGUILayout.Popup("노선 색 그룹", group, groups);
        if (nextGroup >= 0 && group != nextGroup)
        {
            Change("노선 색 그룹", () => rail["regionId"] = groups[nextGroup]);
        }
        TextField(rail, "corridorType", "노선 분류");
        string key = "displayPoints";
        var points = (JArray)(rail[key] ?? rail["points"]);
        for (int i = 0; i < points.Count / 2; i++)
        {
            int index = i * 2;
            Vector2 before = new Vector2((float)points[index], (float)points[index + 1]);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(i == 0 || i == points.Count / 2 - 1))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector2 point = EditorGUILayout.Vector2Field("점 " + i, before);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Change("노선 점 이동", () => SetRailPoint(rail, key, index, point));
                    }
                }
                if (i < points.Count / 2 - 1 && GUILayout.Button("+", GUILayout.Width(26f)))
                {
                    Change("경유점 추가", () =>
                    {
                        var copy = (JArray)points.DeepClone();
                        copy.Insert(index + 2, ((float)points[index] + (float)points[index + 2]) * 0.5f);
                        copy.Insert(index + 3, ((float)points[index + 1] + (float)points[index + 3]) * 0.5f);
                        rail[key] = copy;
                    });
                    GUIUtility.ExitGUI();
                }
                if (i > 0 && i < points.Count / 2 - 1 && GUILayout.Button("−", GUILayout.Width(26f)))
                {
                    Change("경유점 삭제", () =>
                    {
                        var copy = (JArray)points.DeepClone();
                        copy.RemoveAt(index + 1);
                        copy.RemoveAt(index);
                        rail[key] = copy;
                    });
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUI.BeginChangeCheck();
        Vector2 label = EditorGUILayout.Vector2Field("시간 라벨 위치", new Vector2((float)rail["labelX"], (float)rail["labelY"]));
        if (EditorGUI.EndChangeCheck())
        {
            Change("시간 라벨", () =>
            {
                rail["labelX"] = label.x;
                rail["labelY"] = label.y;
                rail["labelManual"] = true;
            });
        }
        bool pinned = (bool?)rail["labelManual"] ?? false;
        bool nextPinned = EditorGUILayout.Toggle("시간 라벨 위치 고정", pinned);
        if (pinned != nextPinned)
        {
            Change("시간 라벨 위치 고정", () => rail["labelManual"] = nextPinned);
        }
        autoTimeLabels = EditorGUILayout.Toggle("수정 후 시간 라벨 자동 배치", autoTimeLabels);
        if (GUILayout.Button("이 노선 시간 라벨 자동 배치"))
        {
            Change("선택 노선 시간 라벨 자동 배치", () => PlaceTimeLabels((string)rail["sourceId"]));
            showTimes = true;
        }
        if (!string.IsNullOrEmpty(timeLabelNotice))
        {
            EditorGUILayout.HelpBox(timeLabelNotice, MessageType.Info);
        }
        if (GUILayout.Button("왕복 노선 삭제") && EditorUtility.DisplayDialog("노선 삭제", "왕복 연결을 초안에서 삭제할까요?", "삭제", "취소"))
        {
            Change("노선 삭제", () =>
            {
                DeleteRail(rail);
                selectedId = null;
                UpdateTopology();
            });
            GUIUtility.ExitGUI();
        }
    }

    void SetRailPoint(JObject _rail, string _key, int _index, Vector2 _point)
    {
        if (!Finite(_point.x) || !Finite(_point.y))
        {
            throw new InvalidDataException("유한한 좌표가 필요합니다.");
        }
        var points = (JArray)(_rail[_key] ?? _rail["points"]).DeepClone();
        Vector2 point = Snap(_point);
        points[_index] = point.x;
        points[_index + 1] = point.y;
        _rail[_key] = points;
        if (_key == "points")
        {
            SyncRouteGeometry(_rail);
        }
    }

    void Reconnect(JObject _rail, string _from, string _to)
    {
        var forward = Forward(_rail);
        var reverse = Reverse(_rail);
        if (_from == _to || Routes.Any(_route => _route != forward && _route != reverse &&
            (((string)_route["fromCityId"] == _from && (string)_route["toCityId"] == _to) || ((string)_route["fromCityId"] == _to && (string)_route["toCityId"] == _from))))
        {
            throw new InvalidOperationException("동일 역 또는 이미 존재하는 연결로 바꿀 수 없습니다.");
        }
        if (_rail["displayPoints"] == null)
        {
            _rail["displayPoints"] = _rail["points"].DeepClone();
        }
        _rail["fromCityId"] = _from;
        _rail["toCityId"] = _to;
        forward["fromCityId"] = _from;
        forward["toCityId"] = _to;
        reverse["fromCityId"] = _to;
        reverse["toCityId"] = _from;
        foreach (string key in new[] { "points", "displayPoints" })
        {
            SetEndpoint((JArray)_rail[key], true, Position(Node(_from)));
            SetEndpoint((JArray)_rail[key], false, Position(Node(_to)));
        }
        SyncRouteGeometry(_rail);
        UpdateTopology();
    }

    void DrawRegion()
    {
        var region = Regions.FirstOrDefault(_region => (string)_region["id"] == selectedId);
        if (region == null)
        {
            return;
        }
        TextField(region, "displayName", "지역 이름");
        IntField(region, "mapLabelPriority", "지명 표시 우선순위");
        IntField(region, "pricePercent", "지역 물가 (%)");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("소속 역 지명 중요도", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(MajorThresholdText, EditorStyles.wordWrappedMiniLabel);
        foreach (var city in Cities.Where(_city => (string)_city["regionId"] == selectedId).OrderByDescending(_city => (int)_city["mapLabelImportance"]).ThenBy(_city => (string)_city["id"], StringComparer.Ordinal).ToArray())
        {
            IntField(city, "mapLabelImportance", (IsMajor(city) ? "★ " : "") + (string)city["displayName"]);
        }
        EditorGUILayout.Space();
        DrawRegionStock(region);
        DrawRaw(region, "지역 상세 데이터", _updated =>
        {
            if ((string)_updated["id"] != selectedId)
            {
                throw new InvalidDataException("지역 ID는 유지해야 합니다.");
            }
            region.Replace(_updated);
        });
    }

    void DrawSettings()
    {
        string start = CityPopup("시작 역", (string)puzzle["startCity"]);
        if (start != null && start != (string)puzzle["startCity"])
        {
            Change("시작 역", () =>
            {
                puzzle["startCity"] = start;
                layout["startCity"] = start;
            });
        }
        string target = CityPopup("지도 목표 표식", (string)puzzle["planningTargetCityId"]);
        if (target != null && target != (string)puzzle["planningTargetCityId"])
        {
            Change("지도 목표", () =>
            {
                puzzle["planningTargetCityId"] = target;
                layout["planningTargetCityId"] = target;
            });
        }
        EditorGUILayout.HelpBox("지도 목표 표식과 상환 장소는 별도 설정입니다. 상환 장소는 PuzzleTradeTableData.debtCityId를 사용합니다.", MessageType.Info);
        IntField(routes, "costPerCell", "적재 칸당 단가");
        IntField(routes, "costPerDistance", "거리당 단가");
        DrawFareTools();
        DrawTimeLabelTools();
    }

    void DrawRaw(JObject _row, string _label, Action<JObject> _apply)
    {
        EditorGUILayout.LabelField(_label + " (JSON)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("시장·거래·태그 등 모든 필드를 편집할 수 있습니다. 아래 적용 버튼을 눌러 초안에 반영하세요.", MessageType.None);
        string key = tab + ":" + selectedId;
        if (rawKey != key)
        {
            rawKey = key;
            rawText = _row.ToString(Formatting.Indented);
        }
        rawText = EditorGUILayout.TextArea(rawText, GUILayout.MinHeight(180f));
        if (GUILayout.Button("상세 데이터를 초안에 적용"))
        {
            Change(_label, () => _apply(JObject.Parse(rawText)));
            GUIUtility.ExitGUI();
        }
    }

    void ExportDraft()
    {
        string path = EditorUtility.SaveFilePanel("맵 초안 보관", "", "map-draft.json", "json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        try
        {
            var data = new JObject
            {
                ["layout"] = layout.DeepClone(),
                ["puzzle"] = puzzle.DeepClone(),
                ["routes"] = routes.DeepClone()
            };
            File.WriteAllText(path, Json(data), new UTF8Encoding(false));
            message = "초안을 별도 파일로 내보냈습니다.";
        }
        catch (Exception exception)
        {
            message = exception.Message;
        }
    }

    void ValidateStructure()
    {
        foreach (var city in Cities)
        {
            var data = city.ToObject<PuzzleCityData>();
            if (string.IsNullOrWhiteSpace(data.id) || string.IsNullOrWhiteSpace(data.displayName) || city["mapLabelImportance"] == null || city["buyEnabled"] == null || city["sellEnabled"] == null || city["buyPercentOfBaseValue"] == null || city["sellPercentOfFinalValue"] == null || !Regions.Any(_region => (string)_region["id"] == data.regionId))
            {
                throw new InvalidDataException("역 ID·이름·중요도·등록된 지역이 필요합니다.");
            }
        }
        foreach (var region in Regions)
        {
            var data = region.ToObject<PuzzleRegionData>();
            if (string.IsNullOrWhiteSpace(data.id) || string.IsNullOrWhiteSpace(data.displayName) || region["mapLabelPriority"] == null || region["pricePercent"] == null)
            {
                throw new InvalidDataException("지역 ID·이름·우선순위·물가가 필요합니다.");
            }
        }
    }

    void AddStation()
    {
        var template = Cities.FirstOrDefault(_city => (string)_city["id"] == selectedId);
        if (template == null || string.IsNullOrWhiteSpace(newName))
        {
            throw new InvalidOperationException("템플릿 역을 선택하고 새 역 이름을 입력하세요.");
        }
        var city = (JObject)template.DeepClone();
        city["id"] = string.IsNullOrWhiteSpace(newId) ? SuggestCityId(newName) : newId.Trim();
        city["displayName"] = newName.Trim();
        if (!CITY_ID.IsMatch((string)city["id"]))
        {
            throw new InvalidOperationException("역 ID는 city.<영문 소문자·숫자> 형식이어야 합니다(예: city.riobruk).");
        }
        if (Cities.Any(_city => (string)_city["id"] == (string)city["id"]))
        {
            throw new InvalidOperationException("역 ID가 중복됩니다.");
        }
        RenamePlacements(city);
        var node = (JObject)Node(selectedId).DeepClone();
        node["cityId"] = city["id"].DeepClone();
        node["displayName"] = city["displayName"].DeepClone();
        node["sourceId"] = EDITOR_SOURCE + (string)city["id"];
        node["degree"] = 0;
        node["interchange"] = false;
        ((JArray)puzzle["cities"]).Add(city);
        ((JArray)layout["nodes"]).Add(node);
        selectedId = (string)city["id"];
        newId = "";
        newName = "";
    }

    // 교역 설정(특산 태그·태그 가격·고정 재고·지역 재고)은 지역 규칙을 따르므로 같은 지역의 다른 역에서 복사한다.
    // 특산 태그가 지역 생산 목록에 있는 역만 쓰며(교역 중심지는 모두), 현재 특산 태그를 쓰는 역을 우선하고
    // 없으면 지역 대표 특산품 역, 그다음 ID 순 첫 역을 쓴다.
    void ApplyRegionTrade(JObject _city)
    {
        string regionId = (string)_city["regionId"];
        var region = Regions.First(_region => (string)_region["id"] == regionId);
        bool tradeHub = (bool?)region["tradeHub"] == true;
        var production = (region["productionTags"] ?? new JArray()).Select(_tag => (string)_tag).ToArray();
        var specialties = (region["specialtyTags"] ?? new JArray()).Select(_tag => (string)_tag).ToArray();
        var candidates = Cities.Where(_other => _other != _city && (string)_other["regionId"] == regionId)
            .Where(_other => tradeHub || production.Contains((string)_other["specialtyTag"]))
            .OrderBy(_other => (string)_other["id"], StringComparer.Ordinal).ToArray();
        var template = candidates.FirstOrDefault(_other => (string)_other["specialtyTag"] == (string)_city["specialtyTag"])
            ?? candidates.FirstOrDefault(_other => specialties.Contains((string)_other["specialtyTag"]))
            ?? candidates.FirstOrDefault();
        if (template == null)
        {
            throw new InvalidOperationException((string)region["displayName"] + "에 교역 설정을 가져올 다른 역이 없습니다. 역 상세 데이터에서 직접 입력하세요.");
        }
        foreach (string key in new[] { "specialtyTag", "tagPrices", "market", "regionalStockId" })
        {
            _city[key] = template[key].DeepClone();
        }
        RenamePlacements(_city);
    }

    // 고정 재고 배치 ID는 전체 역에서 유일해야 한다.
    static void RenamePlacements(JObject _city)
    {
        var placements = _city["market"]?["startingPlacements"] as JArray;
        if (placements == null)
        {
            return;
        }
        for (int i = 0; i < placements.Count; i++)
        {
            placements[i]["id"] = (string)_city["id"] + ".stock." + i;
        }
    }

    void AddRail()
    {
        if (!HasFareSettings)
        {
            throw new InvalidOperationException("요금 설정 원본이 갱신되었습니다. 필요한 초안을 내보낸 뒤 다시 읽기를 누르세요.");
        }
        if (newFrom == null || newTo == null || newFrom == newTo || Routes.Any(_route => ((string)_route["fromCityId"] == newFrom && (string)_route["toCityId"] == newTo) || ((string)_route["fromCityId"] == newTo && (string)_route["toCityId"] == newFrom)))
        {
            throw new InvalidOperationException("서로 다른 두 역을 선택하세요. 이미 연결된 역에는 중복 노선을 추가할 수 없습니다.");
        }
        var template = Rails.FirstOrDefault(_rail => (string)_rail["sourceId"] == selectedId);
        if (template == null)
        {
            throw new InvalidOperationException("분류·고속선 템플릿으로 사용할 기존 노선을 선택하세요.");
        }
        var rail = (JObject)template.DeepClone();
        int index = Rails.Max(_rail => (int)_rail["railIndex"]) + 1;
        string id = "R" + (index + 1).ToString("D2");
        while (Rails.Any(_rail => (string)_rail["sourceId"] == id))
        {
            index++;
            id = "R" + (index + 1).ToString("D2");
        }
        rail["labelManual"] = false;
        rail["railIndex"] = index;
        rail["sourceId"] = id;
        rail["fromCityId"] = newFrom;
        rail["toCityId"] = newTo;
        Vector2 from = Position(Node(newFrom)), to = Position(Node(newTo));
        rail["points"] = new JArray(from.x, from.y, to.x, to.y);
        rail["displayPoints"] = rail["points"].DeepClone();
        rail["labelX"] = (from.x + to.x) * 0.5f;
        rail["labelY"] = (from.y + to.y) * 0.5f;
        var forward = (JObject)Forward(template).DeepClone();
        var reverse = (JObject)Reverse(template).DeepClone();
        forward["fromCityId"] = newFrom;
        forward["toCityId"] = newTo;
        reverse["fromCityId"] = newTo;
        reverse["toCityId"] = newFrom;
        ((JArray)routes["routes"]).Add(forward);
        ((JArray)routes["routes"]).Add(reverse);
        ((JArray)layout["rails"]).Add(rail);
        SyncRouteGeometry(rail);
        SetBaseFare(rail);
        SetFareDistance(rail);
        UpdateTopology();
        selectedId = id;
    }

    void DeleteRail(JObject _rail)
    {
        Forward(_rail).Remove();
        Reverse(_rail).Remove();
        _rail.Remove();
    }

    void DeleteStation()
    {
        string id = selectedId;
        string debtCityId = (string)JObject.Parse(draft.trade)["debtCityId"];
        if (Cities.Count() <= 1 || id == (string)puzzle["startCity"] || id == (string)puzzle["planningTargetCityId"] || id == debtCityId)
        {
            throw new InvalidOperationException("시작 역·목표 역·상환 역 또는 마지막 역은 삭제할 수 없습니다. 참조를 먼저 변경하세요.");
        }
        foreach (var rail in Rails.Where(_rail => (string)_rail["fromCityId"] == id || (string)_rail["toCityId"] == id).ToArray())
        {
            DeleteRail(rail);
        }
        City(id).Remove();
        Node(id).Remove();
        selectedId = null;
        UpdateTopology();
    }

    void UpdateTopology()
    {
        foreach (var node in Nodes)
        {
            string id = (string)node["cityId"];
            int degree = Rails.Count(_rail => (string)_rail["fromCityId"] == id || (string)_rail["toCityId"] == id);
            node["degree"] = degree;
        }
        foreach (var rail in Rails)
        {
            string from = (string)City((string)rail["fromCityId"])["regionId"];
            string to = (string)City((string)rail["toCityId"])["regionId"];
            rail["regionId"] = from == to ? from : "line.regional";
        }
    }

    void ValidateLayout()
    {
        ValidateStructure();
        if (!Nodes.Any() || !Rails.Any())
        {
            throw new InvalidDataException("역과 노선이 하나 이상 필요합니다.");
        }
        if (Nodes.Select(_node => (string)_node["cityId"]).Distinct().Count() != Nodes.Count() || Rails.Select(_rail => (string)_rail["sourceId"]).Distinct().Count() != Rails.Count() || Rails.Select(_rail => (int)_rail["railIndex"]).Distinct().Count() != Rails.Count())
        {
            throw new InvalidDataException("역·노선 ID 또는 railIndex가 중복됩니다.");
        }
        if (!Cities.Select(_city => (string)_city["id"]).OrderBy(_id => _id).SequenceEqual(Nodes.Select(_node => (string)_node["cityId"]).OrderBy(_id => _id)))
        {
            throw new InvalidDataException("역 테이블과 지도 역 목록이 일치해야 합니다.");
        }
        foreach (var node in Nodes)
        {
            if (!Finite((float)node["x"]) || !Finite((float)node["y"]) || string.IsNullOrWhiteSpace(CityName((string)node["cityId"])))
            {
                throw new InvalidDataException("역 이름과 유한한 좌표가 필요합니다.");
            }
        }
        foreach (var rail in Rails)
        {
            if (!Finite((float)rail["labelX"]) || !Finite((float)rail["labelY"]))
            {
                throw new InvalidDataException("유한한 시간 라벨 좌표가 필요합니다.");
            }
            foreach (string key in new[] { "points", "displayPoints" })
            {
                var points = (JArray)(rail[key] ?? rail["points"]);
                if (points.Count < 4 || points.Count % 2 != 0 || points.Any(_value => !Finite((float)_value)))
                {
                    throw new InvalidDataException("노선 좌표가 올바르지 않습니다.");
                }
            }
            var display = (JArray)(rail["displayPoints"] ?? rail["points"]);
            if (new Vector2((float)display[0], (float)display[1]) != Position(Node((string)rail["fromCityId"])) || new Vector2((float)display[display.Count - 2], (float)display[display.Count - 1]) != Position(Node((string)rail["toCityId"])))
            {
                throw new InvalidDataException("표시 노선 끝점과 역 위치가 일치해야 합니다: " + (string)rail["sourceId"]);
            }
            if (!rail["points"].Select(_value => (float)_value).SequenceEqual(Forward(rail)["points"].Select(_value => (float)_value)))
            {
                throw new InvalidDataException("레이아웃과 노선 테이블의 시간 좌표가 다릅니다.");
            }
            Reverse(rail);
        }
        if (Routes.Count() != Rails.Count() * 2)
        {
            throw new InvalidDataException("지도 노선과 왕복 테이블이 일치해야 합니다.");
        }
    }

    void Save()
    {
        var paths = new[] { RailMapLayoutTool.LAYOUT_PATH, PuzzlePath, RoutesPath, WorldMapGeography.PATH, CityActionsPath, TradePath };
        var originals = new[] { originalLayout, originalPuzzle, originalRoutes, originalGeography, originalCityActions, originalTrade };
        var replacements = new[] { draft.layout, draft.puzzle, draft.routes, draft.geography, draft.cityActions, draft.trade };
        byte[] prefab = null, terrain = null;
        bool writing = false;
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (File.ReadAllText(paths[i]) != originals[i])
                {
                    throw new IOException("외부에서 원본이 바뀌었습니다. 초안을 보관하고 다시 읽으세요: " + paths[i]);
                }
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == RailMapLayoutTool.PREFAB_PATH && stage.scene.isDirty)
            {
                throw new IOException("지도 프리팹의 다른 미저장 편집을 먼저 정리하세요.");
            }
            RailMapLayoutTool.SynchronizeRoutePoints(layout, routes);
            draft.layout = Json(layout);
            draft.routes = Json(routes);
            replacements = new[] { draft.layout, draft.puzzle, draft.routes, draft.geography, draft.cityActions, draft.trade };
            ValidateLayout();
            var errors = DraftErrors;
            if (errors.Count > 0)
            {
                throw new InvalidDataException("테이블 검증 오류 " + errors.Count + "건이 있어 저장하지 않았습니다. 첫 오류: " + errors[0]);
            }
            var renamedCities = RenamedCities();
            prefab = File.ReadAllBytes(RailMapLayoutTool.PREFAB_PATH);
            terrain = File.Exists(MapPresentationTool.TERRAIN_PATH) ? File.ReadAllBytes(MapPresentationTool.TERRAIN_PATH) : null;
            writing = true;
            for (int i = 0; i < paths.Length; i++)
            {
                File.WriteAllText(paths[i], replacements[i], new UTF8Encoding(false));
            }
            TableManager.Reload();
            TableManager.Instance.RequireValid();
            if (draft.geography != originalGeography || terrain == null)
            {
                MapPresentationTool.WriteTerrain(geography);
            }
            if (!RailMapLayoutTool.TryApply(renamedCities))
            {
                throw new IOException("지도 프리팹 반영에 실패했습니다.");
            }
            originalLayout = draft.layout;
            originalPuzzle = draft.puzzle;
            originalRoutes = draft.routes;
            originalGeography = draft.geography;
            originalCityActions = draft.cityActions;
            originalTrade = draft.trade;
            hasUnsavedChanges = false;
            Undo.ClearUndo(draft);
            message = "테이블·레이아웃·지형·지도 프리팹을 저장했습니다.";
        }
        catch (Exception exception)
        {
            message = exception.Message;
            if (writing)
            {
                try
                {
                    for (int i = 0; i < paths.Length; i++)
                    {
                        File.WriteAllText(paths[i], originals[i], new UTF8Encoding(false));
                    }
                    File.WriteAllBytes(RailMapLayoutTool.PREFAB_PATH, prefab);
                    if (terrain != null)
                    {
                        File.WriteAllBytes(MapPresentationTool.TERRAIN_PATH, terrain);
                    }
                    TableManager.Reload();
                    message += "\n원본 파일과 프리팹을 복구했습니다.";
                }
                catch (Exception rollback)
                {
                    message += "\n복구 실패: " + rollback.Message;
                }
            }
        }
        SceneView.RepaintAll();
    }
}
