using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 생성 월드 데이터(seed 20265146)의 좌표·폴리라인을 PuzzleWorldMap 프리팹에 적용하는 에디터 도구다.
// 표(PuzzleTableData·PuzzleRouteTableData)는 이미 갱신되어 있고, 이 도구는 표시층만 고친다.
//   노드 위치·환승 표시 / 노선 폴리라인(Track_i_j) / 시간 라벨(Time_i·TimePlate_i) / 노선 색상 그룹
// 레이아웃 파일의 railIndex는 sourceId 숫자 순서이며 프리팹의 Track_·Time_ 접미수와 같은 의미다.
public static class RailMapLayoutTool
{
    const string PREFAB_PATH = "Assets/Art/PuzzleWorldMap.prefab";
    const string LAYOUT_PATH = "Docs/Management/rail_map_layout_seed_20265146.json";
    const string MENU = "Tools/MyJG/Apply Rail Map Layout (seed 20265146)";
    const string TRACK_PARENT_NAME = "SubwayMapTracks";
    const float BACKBONE_THICKNESS = 7f;
    const float SIDE_THICKNESS = 5f;
    const float LABEL_NODE_CLEARANCE = 60f;
    const float LABEL_NUDGE = 34f;
    const float ENDPOINT_TOLERANCE = 2f;
    static readonly string[] BACKBONE_CORRIDORS = {"trade_backbone", "express_backbone"};

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
        public int travelTimeWeight;
        public int distance;
        public int cost;
        public float labelX;
        public float labelY;
        public float[] points;
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
        if (!File.Exists(LAYOUT_PATH))
        {
            Debug.LogError($"[RailMap] 레이아웃 파일을 찾지 못했습니다: {LAYOUT_PATH}");
            return;
        }

        LayoutFile layout;
        try
        {
            layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(LAYOUT_PATH));
        }
        catch (Exception error)
        {
            Debug.LogError($"[RailMap] 레이아웃 파싱 실패: {error.Message}");
            return;
        }

        if (layout == null || layout.nodes == null || layout.rails == null
            || layout.nodes.Length == 0 || layout.rails.Length == 0)
        {
            Debug.LogError("[RailMap] 레이아웃 파일에 nodes·rails가 비어 있습니다.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
        {
            Debug.LogError($"[RailMap] 프리팹을 찾지 못했습니다: {PREFAB_PATH}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            ApplyTo(root, layout);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    static void ApplyTo(GameObject _root, LayoutFile _layout)
    {
        var view = _root.GetComponentInChildren<PuzzleMapView>(true);
        if (view == null)
        {
            Debug.LogError("[RailMap] 프리팹에서 PuzzleMapView를 찾지 못했습니다.");
            return;
        }

        var nodeById = new Dictionary<string, LayoutNode>();
        foreach (var node in _layout.nodes)
        {
            nodeById[node.cityId] = node;
        }

        var report = new StringBuilder();
        var nodesMoved = ApplyNodes(_root, nodeById, _layout.planningTargetCityId, report);
        if (nodesMoved != _layout.nodes.Length)
        {
            Debug.LogWarning($"[RailMap] 프리팹 노드 {nodesMoved}개 / 레이아웃 노드 {_layout.nodes.Length}개");
        }

        var trackParent = FindChild(_root.transform, TRACK_PARENT_NAME);
        if (trackParent == null)
        {
            Debug.LogError($"[RailMap] {TRACK_PARENT_NAME} 자식 컨테이너를 찾지 못했습니다.");
            return;
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
            Debug.LogError("[RailMap] Track_로 시작하는 노선 조각이 프리팹에 없습니다.");
            return;
        }

        var legsUsed = 0;
        var legsCreated = 0;
        var legsDisabled = 0;
        var labelsMoved = 0;
        var labelsNudged = 0;
        var imagesByRegion = new Dictionary<string, List<Image>>();
        var activeLegs = new List<Image>();

        foreach (var rail in _layout.rails)
        {
            var points = rail.points;
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

            var legCount = points.Length / 2 - 1;
            var backbone = Array.IndexOf(BACKBONE_CORRIDORS, rail.corridorType) >= 0;
            var thickness = backbone ? BACKBONE_THICKNESS : SIDE_THICKNESS;

            for (var leg = 1; leg <= legCount; leg++)
            {
                var start = new Vector2(points[(leg - 1) * 2], points[(leg - 1) * 2 + 1]);
                var end = new Vector2(points[leg * 2], points[leg * 2 + 1]);
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
            if (NudgeFromNodes(label, nodeById, out var nudged))
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

        BindRouteTimes(view, _layout, timeByIndex);
        BindLineGroups(view, imagesByRegion, activeLegs);

        Debug.Log($"[RailMap] 노드 {nodesMoved} · 레일 {_layout.rails.Length} · 레그 {legsUsed}(신규 {legsCreated}, 비활성 {legsDisabled}) · 시간 라벨 {labelsMoved}(겹침 보정 {labelsNudged})\n{report}");
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
                    layout.interchange ? Presentation.PuzzleMapInterchange : Presentation.PuzzleMapLineColor(layout.regionId);
            }
            moved++;
        }
        return moved;
    }

    static void BindRouteTimes(PuzzleMapView _view, LayoutFile _layout, Dictionary<int, RectTransform> _timeByIndex)
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

            var label = element.FindPropertyRelative("label");
            if (_timeByIndex.TryGetValue(rail.railIndex, out var time))
            {
                label.objectReferenceValue = time.GetComponent<Text>();
                var text = label.objectReferenceValue as Text;
                if (text != null)
                {
                    text.text = Presentation.PuzzleMapTime(PuzzleTimeManager.TravelHours(rail.travelTimeWeight));
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

    static void CheckEndpoints(LayoutRail _rail, LayoutNode _from, LayoutNode _to)
    {
        var head = new Vector2(_rail.points[0], _rail.points[1]);
        var tail = new Vector2(_rail.points[_rail.points.Length - 2], _rail.points[_rail.points.Length - 1]);
        var fromPos = new Vector2(_from.x, _from.y);
        var toPos = new Vector2(_to.x, _to.y);
        if (Vector2.Distance(head, fromPos) > ENDPOINT_TOLERANCE || Vector2.Distance(tail, toPos) > ENDPOINT_TOLERANCE)
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

        if (nearest == null || best >= LABEL_NODE_CLEARANCE)
        {
            return false;
        }

        var away = _label - nearest.Value;
        if (away.sqrMagnitude < 0.001f)
        {
            away = Vector2.up;
        }
        _nudged = nearest.Value + away.normalized * LABEL_NUDGE;
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
