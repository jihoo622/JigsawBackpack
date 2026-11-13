using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine;
using Newtonsoft.Json.Linq;

// 생성 월드 데이터(seed 20265146)의 좌표·폴리라인을 PuzzleWorldMap 프리팹에 적용하는 에디터 도구다.
// 표(PuzzleTableData·PuzzleRouteTableData)는 이미 갱신되어 있고, 이 도구는 표시층만 고친다.
//   노드 위치·환승 표시 / 노선 폴리라인(Track_i_j) / 시간 라벨(Time_i·TimePlate_i) / 노선 색상 그룹
// 레이아웃 파일의 railIndex는 sourceId 숫자 순서이며 프리팹의 Track_·Time_ 접미수와 같은 의미다.
public static class RailMapLayoutTool
{
    public const string PREFAB_PATH = "Assets/Art/PuzzleWorldMap.prefab";
    public const string LAYOUT_PATH = "Docs/Management/rail_map_layout_seed_20265146.json";
    const string MENU = "Tools/MyJG/Apply Rail Map Layout (seed 20265146)";
    const string TRACK_PARENT_NAME = "SubwayMapTracks";
    const string TERRAIN_NAME = "TerrainBackground";
    static float BackboneThickness => BasicData.Float("editor.RailMapLayoutTool.backbonethickness");
    static float SideThickness => BasicData.Float("editor.RailMapLayoutTool.sidethickness");
    static float LabelNodeClearance => BasicData.Float("editor.RailMapLayoutTool.labelnodeclearance");
    static float LabelNudge => BasicData.Float("editor.RailMapLayoutTool.labelnudge");
    static float EndpointTolerance => BasicData.Float("editor.RailMapLayoutTool.endpointtolerance");
    static readonly string[] BACKBONE_CORRIDORS = { "trade_backbone", "express_backbone" };

    // JsonUtility가 리플렉션으로 값을 채우므로 컴파일러는 필드가 항상 기본값이라고 경고한다.
#pragma warning disable CS0649
    [Serializable]
    class LayoutNode
    {
        public string cityId;
        public string displayName;
        public string regionId;
        public string settlementType;
        public float x;
        public float y;
        public bool interchange;
        public int degree;
        public int tileX;
        public int tileY;
        public string sourceId;
    }

    [Serializable]
    class LayoutRail
    {
        public int railIndex;
        public string sourceId;
        public string fromCityId;
        public string toCityId;
        public string regionId;
        public string corridorType;
        public int distance;
        public long cost;
        public float labelX;
        public float labelY;
        public bool labelManual;
        public float[] points;
        public float[] displayPoints;
    }

    [Serializable]
    class LayoutFile
    {
        public string note;
        public int seed;
        public string source;
        public string startCity;
        public string planningTargetCityId;
        public string debtCityId;
        public LayoutNode[] nodes;
        public LayoutRail[] rails;
    }
#pragma warning restore CS0649

    [MenuItem(MENU)]
    static void ApplyMenuItem()
    {
        if (Application.isPlaying)
        {
            return;
        }
        Apply();
    }

    public static void Apply()
    {
        TryApply();
    }

    // _renamedCities: 이전 역 ID → 새 역 ID. 지정한 역 노드는 다시 만들지 않고 ID와 이름만 바꾼다.
    public static bool TryApply(IReadOnlyDictionary<string, string> _renamedCities = null)
    {
        if (Application.isPlaying)
        {
            return false;
        }
        if (!File.Exists(LAYOUT_PATH))
        {
            Debug.LogError($"[RailMap] 레이아웃 파일을 찾지 못했습니다: {LAYOUT_PATH}");
            return false;
        }

        LayoutFile layout;
        try
        {
            layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(LAYOUT_PATH));
        }
        catch (Exception error)
        {
            Debug.LogError($"[RailMap] 레이아웃 파싱 실패: {error.Message}");
            return false;
        }

        if (layout == null || layout.nodes == null || layout.rails == null
            || layout.nodes.Length == 0 || layout.rails.Length == 0)
        {
            Debug.LogError("[RailMap] 레이아웃 파일에 nodes·rails가 비어 있습니다.");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
        {
            Debug.LogError($"[RailMap] 프리팹을 찾지 못했습니다: {PREFAB_PATH}");
            return false;
        }

        var root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            ApplyTo(root, layout, _renamedCities);
            ApplyTerrain(root, WorldMapGeography.Load());
            if (!PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH))
            {
                throw new InvalidOperationException("지도 프리팹을 저장하지 못했습니다.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return true;
    }

    // 지형 이미지 영역은 지형 격자 크기·중심과 같다.
    static void ApplyTerrain(GameObject _root, WorldMapGeography _geography)
    {
        var terrain = FindChild(_root.transform, TERRAIN_NAME) as RectTransform;
        if (terrain == null)
        {
            throw new InvalidOperationException($"{TERRAIN_NAME} 자식을 찾지 못했습니다.");
        }
        terrain.sizeDelta = _geography.Size;
        terrain.anchoredPosition = _geography.Center;
    }

    static void ApplyTo(GameObject _root, LayoutFile _layout, IReadOnlyDictionary<string, string> _renamedCities)
    {
        var view = _root.GetComponentInChildren<PuzzleMapView>(true);
        if (view == null)
        {
            throw new InvalidOperationException("프리팹에서 PuzzleMapView를 찾지 못했습니다.");
        }

        var nodeById = new Dictionary<string, LayoutNode>();
        foreach (var node in _layout.nodes)
        {
            nodeById[node.cityId] = node;
        }

        var report = new StringBuilder();
        SynchronizeNodes(_root, view, nodeById, _renamedCities);
        var nodesMoved = ApplyNodes(_root, nodeById, _layout.planningTargetCityId, report);
        if (nodesMoved != _layout.nodes.Length)
        {
            Debug.LogWarning($"[RailMap] 프리팹 노드 {nodesMoved}개 / 레이아웃 노드 {_layout.nodes.Length}개");
        }

        var trackParent = FindChild(_root.transform, TRACK_PARENT_NAME);
        if (trackParent == null)
        {
            throw new InvalidOperationException($"{TRACK_PARENT_NAME} 자식 컨테이너를 찾지 못했습니다.");
        }

        var segments = new Dictionary<string, RectTransform>();
        RectTransform legTemplate = null;
        foreach (var rect in _root.GetComponentsInChildren<RectTransform>(true))
        {
            if (!rect.name.StartsWith("Track_", StringComparison.Ordinal))
            {
                continue;
            }
            segments[rect.name] = rect;
            legTemplate = legTemplate ? legTemplate : rect;
        }

        var timeByIndex = new Dictionary<int, RectTransform>();
        var plateByIndex = new Dictionary<int, RectTransform>();
        foreach (var rect in _root.GetComponentsInChildren<RectTransform>(true))
        {
            if (TryIndex(rect.name, "TimePlate_", out var plateIndex))
            {
                plateByIndex[plateIndex] = rect;
            }
            else if (TryIndex(rect.name, "Time_", out var timeIndex))
            {
                timeByIndex[timeIndex] = rect;
            }
        }

        if (!legTemplate)
        {
            throw new InvalidOperationException("Track_로 시작하는 노선 조각이 프리팹에 없습니다.");
        }

        var legsUsed = 0;
        var legsCreated = 0;
        var legsDisabled = 0;
        var labelsMoved = 0;
        var labelsNudged = 0;
        var imagesByRegion = new Dictionary<string, List<Image>>();
        var activeLegs = new List<Image>();

        var orderedRails = (LayoutRail[])_layout.rails.Clone();
        Array.Sort(orderedRails, CompareDrawingOrder);
        foreach (var rail in orderedRails)
        {
            if (!timeByIndex.ContainsKey(rail.railIndex))
            {
                var template = timeByIndex[Array.Find(_layout.rails, _rail => timeByIndex.ContainsKey(_rail.railIndex)).railIndex];
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.parent);
                clone.name = $"Time_{rail.railIndex}";
                timeByIndex.Add(rail.railIndex, (RectTransform)clone.transform);
            }
            if (!plateByIndex.ContainsKey(rail.railIndex))
            {
                var template = plateByIndex[Array.Find(_layout.rails, _rail => plateByIndex.ContainsKey(_rail.railIndex)).railIndex];
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.parent);
                clone.name = $"TimePlate_{rail.railIndex}";
                plateByIndex.Add(rail.railIndex, (RectTransform)clone.transform);
            }
            var points = DrawingPoints(rail);
            if (points == null || points.Length < 4 || points.Length % 2 != 0)
            {
                Debug.LogError($"[RailMap] {rail.sourceId}의 points가 폴리라인으로 맞지 않습니다(원소 {points?.Length ?? 0}).");
                continue;
            }

            if (!nodeById.TryGetValue(rail.fromCityId, out var from) || !nodeById.TryGetValue(rail.toCityId, out var to))
            {
                Debug.LogError($"[RailMap] {rail.sourceId}의 도시를 노드에서 찾지 못했습니다: {rail.fromCityId} / {rail.toCityId}");
                continue;
            }

            var parts = BuildDrawingSegments(rail, orderedRails);
            var legCount = parts.Count;
            var backbone = Array.IndexOf(BACKBONE_CORRIDORS, rail.corridorType) >= 0;
            var thickness = backbone ? BackboneThickness : SideThickness;

            for (var leg = 1; leg <= legCount; leg++)
            {
                var start = parts[leg - 1].start;
                var end = parts[leg - 1].end;
                var name = $"Track_{rail.railIndex}_{leg}";
                if (!segments.TryGetValue(name, out var segment))
                {
                    var clone = UnityEngine.Object.Instantiate(legTemplate.gameObject, legTemplate.transform.parent);
                    clone.name = name;
                    segment = clone.transform as RectTransform;
                    segments[name] = segment;
                    legsCreated++;
                }

                segment.gameObject.SetActive(true);
                segment.SetAsLastSibling();
                var length = Vector2.Distance(start, end);
                segment.anchoredPosition = (start + end) * 0.5f;
                segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
                segment.sizeDelta = new Vector2(length, thickness);

                var image = segment.GetComponent<Image>();
                if (!image)
                {
                    Debug.LogError($"[RailMap] {name}에 Image 컴포넌트가 없습니다.");
                    continue;
                }

                if (!imagesByRegion.TryGetValue(rail.regionId, out var list))
                {
                    list = new List<Image>();
                    imagesByRegion[rail.regionId] = list;
                }
                list.Add(image);
                image.color = Presentation.PuzzleMapLineColor(rail.regionId);
                activeLegs.Add(image);
                legsUsed++;
            }

            for (var leg = legCount + 1; ; leg++)
            {
                if (!segments.TryGetValue($"Track_{rail.railIndex}_{leg}", out var spare))
                {
                    break;
                }
                spare.gameObject.SetActive(false);
                legsDisabled++;
            }

            var label = new Vector2(rail.labelX, rail.labelY);
            if (!rail.labelManual && NudgeFromNodes(label, nodeById, out var nudged))
            {
                label = nudged;
                labelsNudged++;
            }
            if (timeByIndex.TryGetValue(rail.railIndex, out var time))
            {
                time.anchoredPosition = label;
                labelsMoved++;
            }
            else
            {
                Debug.LogWarning($"[RailMap] Time_{rail.railIndex} 오브젝트가 없어 시간 라벨을 옮기지 않았습니다.");
            }
            if (plateByIndex.TryGetValue(rail.railIndex, out var plate))
            {
                plate.anchoredPosition = label;
            }

            CheckEndpoints(rail, from, to);
        }

        foreach (var pair in segments)
        {
            if (!pair.Value.gameObject.activeSelf)
            {
                continue;
            }
            var owned = false;
            foreach (var list in imagesByRegion.Values)
            {
                if (list.Contains(pair.Value.GetComponent<Image>()))
                {
                    owned = true;
                    break;
                }
            }
            if (!owned)
            {
                pair.Value.gameObject.SetActive(false);
                legsDisabled++;
            }
        }

        var usedIndices = new HashSet<int>(_layout.rails.Select(_rail => _rail.railIndex));
        foreach (var pair in timeByIndex)
        {
            pair.Value.gameObject.SetActive(usedIndices.Contains(pair.Key));
        }
        foreach (var pair in plateByIndex)
        {
            pair.Value.gameObject.SetActive(usedIndices.Contains(pair.Key));
        }
        BindRouteTimes(view, _layout, timeByIndex, plateByIndex);
        BindLineGroups(view, imagesByRegion, activeLegs);
        OrderTimeVisuals(_root);
        var regionBindings = new SerializedObject(view).FindProperty("regions");
        for (int i = 0; i < regionBindings.arraySize; i++)
        {
            var binding = regionBindings.GetArrayElementAtIndex(i);
            var label = binding.FindPropertyRelative("label").objectReferenceValue as Text;
            if (label)
            {
                string regionId = binding.FindPropertyRelative("regionId").stringValue;
                var region = Array.Find(TableManager.Instance.Puzzle.regions, _region => _region.id == regionId);
                if (region == null)
                {
                    throw new InvalidOperationException("지역 표시의 원본이 없습니다: " + regionId);
                }
                label.text = Presentation.PuzzleMapRegionLabel(region);
            }
        }

        Debug.Log($"[RailMap] 노드 {nodesMoved} · 레일 {_layout.rails.Length} · 레그 {legsUsed}(신규 {legsCreated}, 비활성 {legsDisabled}) · 시간 라벨 {labelsMoved}(겹침 보정 {labelsNudged})\n{report}");
    }

    public static void SynchronizeRoutePoints(JObject _layout, JObject _routes)
    {
        var rows = _routes["routes"].OfType<JObject>().ToArray();
        foreach (var rail in _layout["rails"].OfType<JObject>())
        {
            var display = rail["displayPoints"] as JArray;
            var points = display != null && display.Count >= 4 ? display : (JArray)rail["points"];
            rail["points"] = points.DeepClone();
            var forward = rows.Single(_row => (string)_row["fromCityId"] == (string)rail["fromCityId"] && (string)_row["toCityId"] == (string)rail["toCityId"]);
            var backward = rows.Single(_row => (string)_row["fromCityId"] == (string)rail["toCityId"] && (string)_row["toCityId"] == (string)rail["fromCityId"]);
            forward["points"] = points.DeepClone();
            var reverse = new JArray();
            for (int i = points.Count - 2; i >= 0; i -= 2)
            {
                reverse.Add(points[i].DeepClone());
                reverse.Add(points[i + 1].DeepClone());
            }
            backward["points"] = reverse;
        }
    }

    public static void OrderTimeVisuals(GameObject _root)
    {
        var view = new SerializedObject(_root.GetComponentInChildren<PuzzleMapView>(true));
        var routes = view.FindProperty("routeTimes");
        // 모든 배경 뒤에 모든 글자를 그린다.
        foreach (string field in new[] { "plate", "label" })
        {
            for (int i = 0; i < routes.arraySize; i++)
            {
                var graphic = routes.GetArrayElementAtIndex(i).FindPropertyRelative(field).objectReferenceValue as Graphic;
                if (graphic)
                {
                    graphic.transform.SetAsLastSibling();
                }
            }
        }
    }

    static void SynchronizeNodes(GameObject _root, PuzzleMapView _view, Dictionary<string, LayoutNode> _nodeById, IReadOnlyDictionary<string, string> _renamedCities)
    {
        var nodes = _root.GetComponentsInChildren<PuzzleMapNodeView>(true).ToList();
        var template = nodes.FirstOrDefault();
        if (!template)
        {
            throw new InvalidOperationException("역을 만들 프리팹 템플릿이 없습니다.");
        }
        foreach (var node in nodes)
        {
            var serialized = new SerializedObject(node);
            var cityId = serialized.FindProperty("cityId");
            if (_renamedCities != null && _renamedCities.TryGetValue(cityId.stringValue, out var renamed))
            {
                node.gameObject.name = node.gameObject.name.Replace(cityId.stringValue, renamed);
                cityId.stringValue = renamed;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        // 삭제 전에 복제해야 템플릿으로 선택된 역을 지운 경우에도 참조가 유효하다.
        foreach (var pair in _nodeById.OrderBy(_pair => _pair.Key, StringComparer.Ordinal))
        {
            if (nodes.Any(_node => new SerializedObject(_node).FindProperty("cityId").stringValue == pair.Key))
            {
                continue;
            }
            var clone = UnityEngine.Object.Instantiate(template, template.transform.parent);
            clone.name = "Station_" + pair.Key;
            var serialized = new SerializedObject(clone);
            serialized.FindProperty("cityId").stringValue = pair.Key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            nodes.Add(clone);
        }
        foreach (var node in nodes.ToArray())
        {
            if (!_nodeById.ContainsKey(new SerializedObject(node).FindProperty("cityId").stringValue))
            {
                nodes.Remove(node);
                UnityEngine.Object.DestroyImmediate(node.gameObject);
            }
        }
        var view = new SerializedObject(_view);
        var references = view.FindProperty("nodes");
        references.arraySize = nodes.Count;
        var ordered = nodes.OrderBy(_node => new SerializedObject(_node).FindProperty("cityId").stringValue, StringComparer.Ordinal).ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            references.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
        }
        view.ApplyModifiedPropertiesWithoutUndo();
    }

    static int ApplyNodes(GameObject _root, Dictionary<string, LayoutNode> _nodeById, string _planningTargetCityId, StringBuilder _report)
    {
        var moved = 0;
        foreach (var node in _root.GetComponentsInChildren<PuzzleMapNodeView>(true))
        {
            var serialized = new SerializedObject(node);
            var cityIdProperty = serialized.FindProperty("cityId");
            if (cityIdProperty == null)
            {
                Debug.LogError("[RailMap] PuzzleMapNodeView.cityId 필드를 찾지 못했습니다.");
                return moved;
            }

            var cityId = cityIdProperty.stringValue;
            if (!_nodeById.TryGetValue(cityId, out var layout))
            {
                Debug.LogWarning($"[RailMap] 레이아웃에 없는 노드입니다. 위치를 그대로 둡니다: {cityId}");
                continue;
            }

            if (!(node.transform is RectTransform rect))
            {
                Debug.LogError($"[RailMap] 노드가 RectTransform을 갖지 않습니다: {cityId}");
                continue;
            }

            if (rect.anchorMin != new Vector2(0.5f, 0.5f) || rect.anchorMax != new Vector2(0.5f, 0.5f))
            {
                _report.AppendLine($"  앵커가 중앙이 아니어서 좌표가 어긋납니다: {cityId} anchorMin={rect.anchorMin}");
            }

            rect.anchoredPosition = new Vector2(layout.x, layout.y);
            var interchange = serialized.FindProperty("interchange");
            if (interchange != null)
            {
                interchange.boolValue = layout.interchange;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var label = serialized.FindProperty("label").objectReferenceValue as Text;
            if (label != null)
            {
                label.text = Presentation.PuzzleMapStationName(layout.displayName, cityId == _planningTargetCityId);
                label.color = Presentation.PuzzleHeading;
            }
            var ring = serialized.FindProperty("stationRing").objectReferenceValue as Image;
            if (ring != null)
            {
                ring.color = cityId == _planningTargetCityId ? Presentation.PuzzleMapTarget :
                    Presentation.PuzzleMapLineColor(layout.regionId);
            }
            moved++;
        }
        return moved;
    }

    static void BindRouteTimes(PuzzleMapView _view, LayoutFile _layout, Dictionary<int, RectTransform> _timeByIndex,
        Dictionary<int, RectTransform> _plateByIndex)
    {
        var serialized = new SerializedObject(_view);
        var routes = serialized.FindProperty("routeTimes");
        if (routes == null)
        {
            Debug.LogError("[RailMap] PuzzleMapView.routeTimes 필드를 찾지 못했습니다.");
            return;
        }

        if (routes.arraySize != _layout.rails.Length)
        {
            Debug.LogWarning($"[RailMap] routeTimes {routes.arraySize}개 → 레이아웃 레일 {_layout.rails.Length}개로 맞춥니다.");
            routes.arraySize = _layout.rails.Length;
        }

        for (var i = 0; i < _layout.rails.Length; i++)
        {
            var rail = _layout.rails[i];
            var element = routes.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("fromCityId").stringValue = rail.fromCityId;
            element.FindPropertyRelative("toCityId").stringValue = rail.toCityId;
            element.FindPropertyRelative("manualPosition").boolValue = rail.labelManual;
            element.FindPropertyRelative("plate").objectReferenceValue = _plateByIndex[rail.railIndex].GetComponent<Image>();

            var label = element.FindPropertyRelative("label");
            if (_timeByIndex.TryGetValue(rail.railIndex, out var time))
            {
                label.objectReferenceValue = time.GetComponent<Text>();
                var text = label.objectReferenceValue as Text;
                if (text != null)
                {
                    var route = Array.Find(TableManager.Instance.RequireValid().PuzzleRoute.routes,
                        _row => _row.fromCityId == rail.fromCityId && _row.toCityId == rail.toCityId);
                    if (route == null)
                    {
                        throw new InvalidOperationException("노선 시간의 원본이 없습니다: " + rail.sourceId);
                    }
                    text.text = Presentation.PuzzleMapTime(PuzzleTimeManager.TravelMinutes(route, TableManager.Instance.PuzzleTime));
                    text.color = Presentation.PuzzleHeading;
                }
                if (!label.objectReferenceValue)
                {
                    Debug.LogError($"[RailMap] Time_{rail.railIndex}에 Text 컴포넌트가 없습니다.");
                }
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindLineGroups(PuzzleMapView _view, Dictionary<string, List<Image>> _imagesByRegion, List<Image> _activeLegs)
    {
        var serialized = new SerializedObject(_view);
        var lines = serialized.FindProperty("lines");
        if (lines == null)
        {
            Debug.LogError("[RailMap] PuzzleMapView.lines 필드를 찾지 못했습니다.");
            return;
        }

        for (var i = 0; i < lines.arraySize; i++)
        {
            var element = lines.GetArrayElementAtIndex(i);
            var regionId = element.FindPropertyRelative("regionId").stringValue;
            var segments = element.FindPropertyRelative("segments");
            _imagesByRegion.TryGetValue(regionId, out var images);
            segments.arraySize = images?.Count ?? 0;
            for (var j = 0; j < segments.arraySize; j++)
            {
                segments.GetArrayElementAtIndex(j).objectReferenceValue = images[j];
            }
        }

        var routeLines = serialized.FindProperty("routeLines");
        if (routeLines != null)
        {
            routeLines.arraySize = _activeLegs.Count;
            for (var i = 0; i < _activeLegs.Count; i++)
            {
                routeLines.GetArrayElementAtIndex(i).objectReferenceValue = _activeLegs[i];
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static float[] DrawingPoints(LayoutRail _rail)
    {
        return _rail.displayPoints != null && _rail.displayPoints.Length > 0 ? _rail.displayPoints : _rail.points;
    }

    static int DrawingOrder(LayoutRail _rail)
    {
        string key = _rail.regionId == "line.regional" ? "interregionalOrder"
            : _rail.regionId == "region.common" ? "neutralOrder" : "regionalOrder";
        return BasicData.Int("editor.RailMapLayoutTool." + key);
    }

    static int CompareDrawingOrder(LayoutRail _a, LayoutRail _b)
    {
        int order = DrawingOrder(_a).CompareTo(DrawingOrder(_b));
        return order != 0 ? order : string.CompareOrdinal(_a.sourceId, _b.sourceId);
    }

    // 역이 아닌 교차점에서 아래 노선을 끊는다. 이 조각들은 프리팹에 저장하며 런타임에 생성하지 않는다.
    static List<(Vector2 start, Vector2 end)> BuildDrawingSegments(LayoutRail _rail, LayoutRail[] _rails)
    {
        var result = new List<(Vector2 start, Vector2 end)>();
        var points = DrawingPoints(_rail);
        float gap = BasicData.Float("editor.RailMapLayoutTool.crossingGap");
        for (int i = 2; i < points.Length; i += 2)
        {
            var start = new Vector2(points[i - 2], points[i - 1]);
            var end = new Vector2(points[i], points[i + 1]);
            var direction = end - start;
            float length = direction.magnitude;
            if (length <= Mathf.Epsilon)
            {
                continue;
            }
            var gaps = new List<(float start, float end)>();
            foreach (var other in _rails)
            {
                if (CompareDrawingOrder(other, _rail) <= 0)
                {
                    continue;
                }
                var otherPoints = DrawingPoints(other);
                for (int j = 2; j < otherPoints.Length; j += 2)
                {
                    var otherStart = new Vector2(otherPoints[j - 2], otherPoints[j - 1]);
                    var otherDirection = new Vector2(otherPoints[j], otherPoints[j + 1]) - otherStart;
                    float cross = Cross(direction, otherDirection);
                    float sine = Mathf.Abs(cross) / (length * otherDirection.magnitude);
                    if (float.IsNaN(sine) || sine < 0.01f)
                    {
                        continue;
                    }
                    var delta = otherStart - start;
                    float t = Cross(delta, otherDirection) / cross;
                    float u = Cross(delta, direction) / cross;
                    if (t < 0f || t > 1f || u < 0f || u > 1f)
                    {
                        continue;
                    }
                    var crossing = start + direction * t;
                    if (NearStationEndpoint(crossing, _rail) || NearStationEndpoint(crossing, other))
                    {
                        continue;
                    }
                    float halfGap = gap * 0.5f / (length * sine);
                    gaps.Add((Mathf.Clamp01(t - halfGap), Mathf.Clamp01(t + halfGap)));
                }
            }
            gaps.Sort((_a, _b) => _a.start.CompareTo(_b.start));
            float cursor = 0f;
            foreach (var interval in gaps)
            {
                if (interval.start > cursor)
                {
                    result.Add((start + direction * cursor, start + direction * interval.start));
                }
                cursor = Mathf.Max(cursor, interval.end);
            }
            if (cursor < 1f)
            {
                result.Add((start + direction * cursor, end));
            }
        }
        return result;
    }

    static float Cross(Vector2 _a, Vector2 _b)
    {
        return _a.x * _b.y - _a.y * _b.x;
    }

    static bool NearStationEndpoint(Vector2 _point, LayoutRail _rail)
    {
        var points = DrawingPoints(_rail);
        float clearance = BasicData.Float("editor.RailMapLayoutTool.crossingNodeClearance");
        return Vector2.Distance(_point, new Vector2(points[0], points[1])) < clearance ||
            Vector2.Distance(_point, new Vector2(points[points.Length - 2], points[points.Length - 1])) < clearance;
    }

    static void CheckEndpoints(LayoutRail _rail, LayoutNode _from, LayoutNode _to)
    {
        var points = DrawingPoints(_rail);
        var head = new Vector2(points[0], points[1]);
        var tail = new Vector2(points[points.Length - 2], points[points.Length - 1]);
        var fromPos = new Vector2(_from.x, _from.y);
        var toPos = new Vector2(_to.x, _to.y);
        if (Vector2.Distance(head, fromPos) > EndpointTolerance || Vector2.Distance(tail, toPos) > EndpointTolerance)
        {
            Debug.LogWarning($"[RailMap] {_rail.sourceId}의 폴리라인 끝이 노드 좌표와 어긋납니다. head={head} from={fromPos} tail={tail} to={toPos}");
        }
    }

    static bool NudgeFromNodes(Vector2 _label, Dictionary<string, LayoutNode> _nodeById, out Vector2 _nudged)
    {
        _nudged = _label;
        Vector2? nearest = null;
        var best = float.MaxValue;
        foreach (var node in _nodeById.Values)
        {
            var delta = Vector2.Distance(_label, new Vector2(node.x, node.y));
            if (delta < best)
            {
                best = delta;
                nearest = new Vector2(node.x, node.y);
            }
        }

        if (nearest == null || best >= LabelNodeClearance)
        {
            return false;
        }

        var away = _label - nearest.Value;
        if (away.sqrMagnitude < BasicData.Float("editor.RailMapLayoutTool.nudgeFromNodesBelowPercent"))
        {
            away = Vector2.up;
        }
        _nudged = nearest.Value + away.normalized * LabelNudge;
        return true;
    }

    static bool TryIndex(string _name, string _prefix, out int _index)
    {
        _index = -1;
        if (!_name.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return false;
        }
        return int.TryParse(_name.Substring(_prefix.Length), out _index);
    }

    static Transform FindChild(Transform _root, string _name)
    {
        if (_root.name == _name)
        {
            return _root;
        }
        for (var i = 0; i < _root.childCount; i++)
        {
            var found = FindChild(_root.GetChild(i), _name);
            if (found)
            {
                return found;
            }
        }
        return null;
    }
}
