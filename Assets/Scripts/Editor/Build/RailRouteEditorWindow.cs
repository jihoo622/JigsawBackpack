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

// 저장 전 조작은 Undo 가능한 초안에만 적용한다. 역·연결·게임 시간 좌표는 편집하지 않는다.
public sealed class RailRouteEditorWindow : EditorWindow
{
    [SerializeField] RailRouteDraft draft;
    [SerializeField] string loadedJson;
    [SerializeField] float snapStep = 10f;
    [SerializeField] int selectedPoint;
    [SerializeField] bool editTimeLabel;
    [SerializeField] PuzzleMapView map;
    JObject document;
    Vector2 scroll;
    string error;

    [MenuItem("Tools/MyJG/Edit Rail Routes")]
    static void Open()
    {
        GetWindow<RailRouteEditorWindow>("노선 경유점 편집");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        LoadDocument(false);
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnDestroy()
    {
        if (draft)
        {
            Undo.ClearUndo(draft);
            DestroyImmediate(draft);
        }
    }

    void OnUndoRedo()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    IEnumerable<JObject> Rails => document?["rails"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>();
    JObject Rail => Rails.FirstOrDefault(_rail => (string)_rail["sourceId"] == draft?.railId);

    static List<Vector2> ReadPoints(JObject _rail)
    {
        var source = _rail["displayPoints"] as JArray;
        if (source == null || source.Count == 0)
        {
            source = _rail["points"] as JArray;
        }
        if (source == null || source.Count < 4 || source.Count % 2 != 0)
        {
            throw new InvalidDataException("노선 좌표는 두 점 이상의 x/y 쌍이어야 합니다.");
        }
        var points = new List<Vector2>();
        for (int i = 0; i < source.Count; i += 2)
        {
            points.Add(new Vector2((float)source[i], (float)source[i + 1]));
        }
        return points;
    }

    static Vector2 LabelPosition(JObject _rail)
    {
        return new Vector2((float)_rail["labelX"], (float)_rail["labelY"]);
    }

    bool HasChanges => draft && Rail != null &&
        (!draft.points.SequenceEqual(ReadPoints(Rail)) || !draft.timeLabel.Equals(LabelPosition(Rail)));

    void LoadDocument(bool _reload)
    {
        try
        {
            if (_reload || string.IsNullOrEmpty(loadedJson))
            {
                loadedJson = File.ReadAllText(RailMapLayoutTool.LAYOUT_PATH);
            }
            document = JObject.Parse(loadedJson);
            if (!Rails.Any() || Rails.Select(_rail => (string)_rail["sourceId"]).Distinct().Count() != Rails.Count())
            {
                throw new InvalidDataException("고유한 sourceId를 가진 노선 목록이 필요합니다.");
            }
            var nodes = document["nodes"] as JArray;
            if (nodes == null || nodes.Count == 0)
            {
                throw new InvalidDataException("역 좌표 목록이 필요합니다.");
            }
            var cities = new HashSet<string>(nodes.OfType<JObject>().Select(_node => (string)_node["cityId"]), StringComparer.Ordinal);
            foreach (var rail in Rails)
            {
                if (string.IsNullOrWhiteSpace((string)rail["sourceId"]) || !cities.Contains((string)rail["fromCityId"])
                    || !cities.Contains((string)rail["toCityId"]) || ReadPoints(rail).Any(_point => !Finite(_point)) || !Finite(LabelPosition(rail)))
                {
                    throw new InvalidDataException("노선 ID·연결 역·유한 좌표를 확인하세요.");
                }
            }
            if (!draft)
            {
                draft = CreateInstance<RailRouteDraft>();
                draft.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_reload || Rail == null)
            {
                SelectRail(Rails.OrderBy(_rail => (string)_rail["sourceId"], StringComparer.Ordinal).First());
            }
            error = null;
        }
        catch (Exception exception)
        {
            document = null;
            error = exception.Message;
        }
    }

    void SelectRail(JObject _rail)
    {
        Undo.ClearUndo(draft);
        draft.railId = (string)_rail["sourceId"];
        draft.points = ReadPoints(_rail);
        draft.timeLabel = LabelPosition(_rail);
        selectedPoint = 0;
        SceneView.RepaintAll();
    }

    bool CanDiscard()
    {
        return !HasChanges || EditorUtility.DisplayDialog("경유점 편집", "저장하지 않은 경유점 변경을 버릴까요?", "버리기", "계속 편집");
    }

    string CityName(string _id)
    {
        return (string)document["nodes"].OfType<JObject>().First(_node => (string)_node["cityId"] == _id)["displayName"];
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("지도 표시 경로만 편집합니다. 역 연결·운임·이동 시간은 유지됩니다. Scene 화면의 경유점을 드래그하거나 좌표를 수정하세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("파일 다시 읽기") && CanDiscard())
            {
                LoadDocument(true);
                GUIUtility.ExitGUI();
            }
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            if (document == null || !draft || Rail == null)
            {
                return;
            }
            var rails = Rails.OrderBy(_rail => (string)_rail["sourceId"], StringComparer.Ordinal).ToArray();
            int index = Array.FindIndex(rails, _rail => (string)_rail["sourceId"] == draft.railId);
            string[] options = rails.Select(_rail => (string)_rail["sourceId"] + "  " + CityName((string)_rail["fromCityId"])
                + " ↔ " + CityName((string)_rail["toCityId"])).ToArray();
            int next = EditorGUILayout.Popup("노선", index, options);
            if (next != index && CanDiscard())
            {
                SelectRail(rails[next]);
                GUIUtility.ExitGUI();
            }
            map = (PuzzleMapView)EditorGUILayout.ObjectField("편집 지도", map, typeof(PuzzleMapView), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("지도 프리팹 열기"))
                {
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(RailMapLayoutTool.PREFAB_PATH));
                    FindMap();
                    FrameRoute();
                }
                if (GUILayout.Button("선택 노선 보기"))
                {
                    FindMap();
                    FrameRoute();
                }
            }
            snapStep = EditorGUILayout.FloatField("격자 간격 (0: 해제)", snapStep);
            editTimeLabel = EditorGUILayout.Toggle("시간 라벨 위치 편집", editTimeLabel);
            EditorGUILayout.LabelField(HasChanges ? "저장하지 않은 변경 있음" : "저장된 경로", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < draft.points.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(selectedPoint == i, i.ToString(), "Button", GUILayout.Width(EditorGUIUtility.singleLineHeight * 2f)))
                    {
                        selectedPoint = i;
                    }
                    using (new EditorGUI.DisabledScope(i == 0 || i == draft.points.Count - 1))
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector2 point = EditorGUILayout.Vector2Field(i == 0 ? "출발역" : i == draft.points.Count - 1 ? "도착역" : "경유점", draft.points[i]);
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetPoint(i, point);
                        }
                    }
                }
            }
            if (editTimeLabel)
            {
                EditorGUI.BeginChangeCheck();
                Vector2 label = EditorGUILayout.Vector2Field("시간 라벨", draft.timeLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    SetLabel(label);
                }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택 점 다음에 추가"))
                {
                    int after = Mathf.Clamp(selectedPoint, 0, draft.points.Count - 2);
                    Undo.RecordObject(draft, "노선 경유점 추가");
                    draft.points.Insert(after + 1, (draft.points[after] + draft.points[after + 1]) * 0.5f);
                    selectedPoint = after + 1;
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(selectedPoint <= 0 || selectedPoint >= draft.points.Count - 1))
                {
                    if (GUILayout.Button("선택 경유점 삭제"))
                    {
                        Undo.RecordObject(draft, "노선 경유점 삭제");
                        draft.points.RemoveAt(selectedPoint);
                        selectedPoint = Mathf.Min(selectedPoint, draft.points.Count - 1);
                        SceneView.RepaintAll();
                    }
                }
            }
            using (new EditorGUI.DisabledScope(!HasChanges))
            {
                if (GUILayout.Button("저장하고 지도 프리팹 반영"))
                {
                    Save();
                }
            }
        }
    }

    void FindMap()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == RailMapLayoutTool.PREFAB_PATH)
        {
            map = stage.prefabContentsRoot.GetComponentInChildren<PuzzleMapView>(true);
        }
        else if (!map)
        {
            var maps = UnityEngine.Object.FindObjectsByType<PuzzleMapView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (maps.Length == 1)
            {
                map = maps[0];
            }
        }
    }

    RectTransform MapSpace()
    {
        if (!map)
        {
            FindMap();
        }
        if (!map)
        {
            return null;
        }
        var serialized = new SerializedObject(map);
        var nodes = serialized.FindProperty("nodes");
        if (nodes == null || nodes.arraySize == 0)
        {
            return null;
        }
        var node = nodes.GetArrayElementAtIndex(0).objectReferenceValue as PuzzleMapNodeView;
        return node ? node.transform.parent as RectTransform : null;
    }

    void FrameRoute()
    {
        var space = MapSpace();
        if (!space)
        {
            return;
        }
        var bounds = new Bounds(space.TransformPoint(draft.points[0]), Vector3.zero);
        foreach (var point in draft.points)
        {
            bounds.Encapsulate(space.TransformPoint(point));
        }
        var sceneView = SceneView.lastActiveSceneView;
        if (!sceneView)
        {
            sceneView = GetWindow<SceneView>();
        }
        sceneView.Show();
        sceneView.Focus();
        sceneView.in2DMode = true;
        sceneView.Frame(bounds, false);
    }

    static bool Finite(Vector2 _point)
    {
        return !float.IsNaN(_point.x) && !float.IsInfinity(_point.x) && !float.IsNaN(_point.y) && !float.IsInfinity(_point.y);
    }

    Vector2 Snap(Vector2 _point)
    {
        return snapStep > 0f && !float.IsInfinity(snapStep)
            ? new Vector2(Mathf.Round(_point.x / snapStep) * snapStep, Mathf.Round(_point.y / snapStep) * snapStep) : _point;
    }

    void SetPoint(int _index, Vector2 _point)
    {
        Vector2 snapped = Snap(_point);
        if (_index <= 0 || _index >= draft.points.Count - 1 || !Finite(snapped))
        {
            return;
        }
        Undo.RecordObject(draft, "노선 경유점 이동");
        draft.points[_index] = snapped;
        selectedPoint = _index;
        SceneView.RepaintAll();
        Repaint();
    }

    void SetLabel(Vector2 _point)
    {
        Vector2 snapped = Snap(_point);
        if (!Finite(snapped))
        {
            return;
        }
        Undo.RecordObject(draft, "시간 라벨 이동");
        draft.timeLabel = snapped;
        SceneView.RepaintAll();
        Repaint();
    }

    void OnSceneGUI(SceneView _sceneView)
    {
        if (Application.isPlaying || document == null || !draft || draft.points.Count < 2)
        {
            return;
        }
        var space = MapSpace();
        if (!space)
        {
            return;
        }
        Color previousColor = Handles.color;
        try
        {
            DrawMapPreview(space);
        }
        finally
        {
            Handles.color = previousColor;
        }
    }

    void DrawMapPreview(RectTransform _space)
    {
        foreach (var rail in Rails)
        {
            if ((string)rail["sourceId"] == draft.railId)
            {
                continue;
            }
            Handles.color = Presentation.PuzzleMapLineColor((string)rail["regionId"]);
            Handles.DrawAAPolyLine(ReadPoints(rail).Select(_point => _space.TransformPoint(_point)).ToArray());
        }
        foreach (var node in document["nodes"].OfType<JObject>())
        {
            Vector3 world = _space.TransformPoint(new Vector2((float)node["x"], (float)node["y"]));
            Handles.color = Presentation.PuzzleMapLineColor((string)node["regionId"]);
            Handles.DrawWireDisc(world, _space.forward, HandleUtility.GetHandleSize(world) * 0.025f);
            Handles.Label(world, (string)node["displayName"]);
        }
        Handles.color = Presentation.PuzzleMapTarget;
        Handles.DrawAAPolyLine(draft.points.Select(_point => _space.TransformPoint(_point)).ToArray());
        for (int i = 0; i < draft.points.Count; i++)
        {
            Vector3 world = _space.TransformPoint(draft.points[i]);
            if (i == 0 || i == draft.points.Count - 1)
            {
                continue;
            }
            Handles.Label(world, i.ToString());
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, HandleUtility.GetHandleSize(world) * 0.05f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                SetPoint(i, _space.InverseTransformPoint(moved));
            }
        }
        if (editTimeLabel)
        {
            Vector3 world = _space.TransformPoint(draft.timeLabel);
            Handles.Label(world, "시간 표시");
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, HandleUtility.GetHandleSize(world) * 0.05f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                SetLabel(_space.InverseTransformPoint(moved));
            }
        }
    }

    void Save()
    {
        string replacement = null;
        try
        {
            if (File.ReadAllText(RailMapLayoutTool.LAYOUT_PATH) != loadedJson)
            {
                throw new InvalidOperationException("외부에서 레이아웃 파일이 바뀌었습니다. 초안을 별도 보관한 뒤 파일을 다시 읽으세요.");
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == RailMapLayoutTool.PREFAB_PATH && stage.scene.isDirty)
            {
                throw new InvalidOperationException("지도 프리팹의 다른 편집을 먼저 저장하거나 되돌린 뒤 적용하세요.");
            }
            var original = ReadPoints(Rail);
            if (draft.points.Count < 2 || !draft.points[0].Equals(original[0]) || !draft.points[draft.points.Count - 1].Equals(original[original.Count - 1]))
            {
                throw new InvalidDataException("출발·도착 역 좌표는 바꿀 수 없습니다.");
            }
            if (!Finite(draft.timeLabel) || draft.points.Any(_point => !Finite(_point))
                || draft.points.Zip(draft.points.Skip(1), (_a, _b) => _a.Equals(_b)).Any(_same => _same))
            {
                throw new InvalidDataException("유한 좌표와 길이가 있는 노선 구간이 필요합니다.");
            }
            var updated = (JObject)document.DeepClone();
            var rail = updated["rails"].OfType<JObject>().Single(_rail => (string)_rail["sourceId"] == draft.railId);
            rail["displayPoints"] = new JArray(draft.points.SelectMany(_point => new[]
            {
                _point.x,
                _point.y
            }));
            rail["labelX"] = draft.timeLabel.x;
            rail["labelY"] = draft.timeLabel.y;
            replacement = updated.ToString(Formatting.Indented) + "\n";
            File.WriteAllText(RailMapLayoutTool.LAYOUT_PATH, replacement, new UTF8Encoding(false));
            if (!RailMapLayoutTool.TryApply())
            {
                throw new InvalidOperationException("지도 프리팹 적용에 실패했습니다. Console 오류를 확인하세요.");
            }
            loadedJson = replacement;
            document = updated;
            SelectRail(rail);
            error = null;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            try
            {
                if (replacement != null && File.ReadAllText(RailMapLayoutTool.LAYOUT_PATH) == replacement)
                {
                    File.WriteAllText(RailMapLayoutTool.LAYOUT_PATH, loadedJson, new UTF8Encoding(false));
                }
            }
            catch (Exception rollback)
            {
                error += "\n레이아웃 원본 복구 실패: " + rollback.Message;
            }
        }
        SceneView.RepaintAll();
    }
}
