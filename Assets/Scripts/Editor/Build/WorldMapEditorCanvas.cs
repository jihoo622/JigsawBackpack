using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 맵 데이터 초안을 직접 그린다. Scene/Prefab Stage와 렌더용 GameObject를 만들지 않는다.
public sealed partial class WorldMapEditorWindow
{
    const float MIN_CANVAS_ZOOM = 0.05f;
    const float MAX_CANVAS_ZOOM = 8f;
    const float NODE_RADIUS = 7f;
    [SerializeField] Vector2 canvasCenter;
    [SerializeField] float canvasZoom = 0.5f;
    [SerializeField] bool showTerrain = true;
    [SerializeField] bool showGrid;
    [SerializeField] bool showNames = true;
    [SerializeField] bool showTimes;
    [SerializeField] bool fitCanvas = true;
    bool frameSelection;
    Texture2D terrainTexture;
    int canvasDragControl, canvasUndoGroup = -1, dragPoint = -1;
    string dragCity, dragRail;
    bool panCanvas;
    Vector2 dragOffset;
    Rect canvasRect;

    void OnLostFocus()
    {
        ReleaseCanvasDrag();
    }

    void DrawCanvasToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            showTerrain = GUILayout.Toggle(showTerrain, "지형", EditorStyles.toolbarButton);
            showGrid = GUILayout.Toggle(showGrid, "격자", EditorStyles.toolbarButton);
            showNames = GUILayout.Toggle(showNames, "역명", EditorStyles.toolbarButton);
            showTimes = GUILayout.Toggle(showTimes, "시간", EditorStyles.toolbarButton);
            if (GUILayout.Button("선택 보기", EditorStyles.toolbarButton))
            {
                frameSelection = true;
            }
            GUILayout.Label(Mathf.RoundToInt(canvasZoom * 100f) + "%", EditorStyles.miniLabel, GUILayout.Width(42f));
        }
        EditorGUILayout.LabelField("휠: 확대/축소 · 빈 곳/중간/오른쪽 드래그: 이동 · 역/경유점 드래그: 편집", EditorStyles.miniLabel);
    }

    Vector2 CanvasPoint(Vector2 _world)
    {
        Vector2 delta = (_world - canvasCenter) * canvasZoom;
        return canvasRect.size * 0.5f + new Vector2(delta.x, -delta.y);
    }

    Vector2 WorldPoint(Vector2 _point)
    {
        Vector2 delta = (_point - canvasRect.size * 0.5f) / canvasZoom;
        return canvasCenter + new Vector2(delta.x, -delta.y);
    }

    static JArray DisplayPoints(JObject _rail)
    {
        var points = _rail["displayPoints"] as JArray;
        return points != null && points.Count >= 4 ? points : (JArray)_rail["points"];
    }

    JArray EditingPoints(JObject _rail)
    {
        return editTimeGeometry ? (JArray)_rail["points"] : DisplayPoints(_rail);
    }

    static Vector2 ArrayPoint(JArray _points, int _index)
    {
        return new Vector2((float)_points[_index], (float)_points[_index + 1]);
    }

    void FitCanvas(bool _selection)
    {
        var points = Nodes.Select(Position).ToArray();
        if (_selection && tab == 0 && Nodes.Any(_node => (string)_node["cityId"] == selectedId))
        {
            canvasCenter = Position(Node(selectedId));
            canvasZoom = Mathf.Max(canvasZoom, 1f);
            return;
        }
        var rail = Rails.FirstOrDefault(_rail => (string)_rail["sourceId"] == selectedId);
        if (_selection && tab == 1 && rail != null)
        {
            var source = EditingPoints(rail);
            points = Enumerable.Range(0, source.Count / 2).Select(_index => ArrayPoint(source, _index * 2)).ToArray();
        }
        if (points.Length == 0)
        {
            return;
        }
        Vector2 min = points.Aggregate(Vector2.Min), max = points.Aggregate(Vector2.Max);
        canvasCenter = (min + max) * 0.5f;
        canvasZoom = Mathf.Clamp(Mathf.Min((canvasRect.width - 100f) / Mathf.Max(100f, max.x - min.x), (canvasRect.height - 100f) / Mathf.Max(100f, max.y - min.y)), MIN_CANVAS_ZOOM, MAX_CANVAS_ZOOM);
    }

    void DrawCanvas(Rect _rect)
    {
        if (_rect.width < 10f || _rect.height < 10f)
        {
            return;
        }
        canvasRect = new Rect(Vector2.zero, _rect.size);
        if (Event.current.type == EventType.Repaint && (fitCanvas || frameSelection))
        {
            FitCanvas(frameSelection && !fitCanvas);
            fitCanvas = false;
            frameSelection = false;
        }
        int control = GUIUtility.GetControlID(FocusType.Passive);
        GUI.BeginGroup(_rect);
        try
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(canvasRect, Presentation.PuzzleMapOcean);
                DrawCanvasTerrain();
                Handles.BeginGUI();
                Color previous = Handles.color;
                try
                {
                    if (showGrid)
                    {
                        DrawCanvasGrid();
                    }
                    DrawCanvasRails();
                    DrawCanvasStations();
                }
                finally
                {
                    Handles.color = previous;
                    Handles.EndGUI();
                }
                string mode = tab == 1 && editTimeGeometry ? "시간 계산 경로 편집" : "표시 지도 편집";
                GUI.Label(new Rect(8f, canvasRect.height - 24f, canvasRect.width - 16f, 22f), mode, EditorStyles.helpBox);
            }
            HandleCanvasInput(control);
        }
        finally
        {
            GUI.EndGroup();
        }
    }

    void DrawCanvasTerrain()
    {
        if (!showTerrain)
        {
            return;
        }
        if (!terrainTexture)
        {
            terrainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PuzzleMapTerrain.png");
        }
        if (!terrainTexture)
        {
            return;
        }
        float width = BasicData.Float("editor.MapPresentationTool.terrainBackgroundWidth");
        float height = BasicData.Float("editor.MapPresentationTool.terrainBackgroundHeight");
        Vector2 corner = CanvasPoint(new Vector2(-width * 0.5f, height * 0.5f));
        GUI.DrawTexture(new Rect(corner, new Vector2(width, height) * canvasZoom), terrainTexture, ScaleMode.StretchToFill);
    }

    void DrawCanvasGrid()
    {
        float step = Mathf.Pow(10f, Mathf.Ceil(Mathf.Log10(30f / canvasZoom)));
        Vector2 min = WorldPoint(new Vector2(0f, canvasRect.height)), max = WorldPoint(new Vector2(canvasRect.width, 0f));
        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        float firstX = Mathf.Ceil(min.x / step) * step;
        for (int i = 0; i <= Mathf.CeilToInt(canvasRect.width / (step * canvasZoom)); i++)
        {
            float x = firstX + i * step;
            Handles.DrawLine(CanvasPoint(new Vector2(x, min.y)), CanvasPoint(new Vector2(x, max.y)));
        }
        float firstY = Mathf.Ceil(min.y / step) * step;
        for (int i = 0; i <= Mathf.CeilToInt(canvasRect.height / (step * canvasZoom)); i++)
        {
            float y = firstY + i * step;
            Handles.DrawLine(CanvasPoint(new Vector2(min.x, y)), CanvasPoint(new Vector2(max.x, y)));
        }
    }

    GUIStyle CanvasLabelStyle(bool _selected)
    {
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = _selected ? Presentation.PuzzleMapTarget : Presentation.PuzzleHeading;
        style.fontStyle = _selected ? FontStyle.Bold : FontStyle.Normal;
        return style;
    }

    void DrawCanvasRails()
    {
        foreach (var rail in Rails.OrderBy(_rail => (string)_rail["sourceId"] == selectedId ? 1 : 0))
        {
            bool selected = tab == 1 && (string)rail["sourceId"] == selectedId;
            var points = selected ? EditingPoints(rail) : DisplayPoints(rail);
            var screen = Enumerable.Range(0, points.Count / 2).Select(_index => (Vector3)CanvasPoint(ArrayPoint(points, _index * 2))).ToArray();
            Handles.color = selected ? Presentation.PuzzleMapTarget : Presentation.PuzzleMapLineColor((string)rail["regionId"]);
            Handles.DrawAAPolyLine(selected ? 5f : 3f, screen);
            if (selected)
            {
                for (int i = 0; i < screen.Length; i++)
                {
                    if (!editTimeGeometry && (i == 0 || i == screen.Length - 1))
                    {
                        continue;
                    }
                    EditorGUI.DrawRect(new Rect(screen[i].x - 5f, screen[i].y - 5f, 10f, 10f), Presentation.PuzzleMapTarget);
                    GUI.Label(new Rect(screen[i].x + 7f, screen[i].y - 16f, 40f, 18f), i.ToString(), CanvasLabelStyle(true));
                }
            }
            if (showTimes)
            {
                string text = (string)rail["sourceId"];
                try
                {
                    var row = Forward(rail).ToObject<PuzzleRouteData>();
                    text += " · " + Presentation.PuzzleMapTime(PuzzleTimeManager.TravelMinutes(row, TableManager.Instance.PuzzleTime));
                }
                catch (OverflowException)
                {
                    text += " · 시간 범위 초과";
                }
                Vector2 point = CanvasPoint(new Vector2((float)rail["labelX"], (float)rail["labelY"]));
                var style = CanvasLabelStyle(selected);
                Vector2 size = style.CalcSize(new GUIContent(text));
                var box = new Rect(point - size * 0.5f, size);
                EditorGUI.DrawRect(box, Presentation.PuzzleMapOcean);
                GUI.Label(box, text, style);
            }
        }
    }

    void DrawCanvasStations()
    {
        var reserved = Nodes.Select(_node => new Rect(CanvasPoint(Position(_node)) - Vector2.one * (NODE_RADIUS + 2f), Vector2.one * (NODE_RADIUS + 2f) * 2f)).ToList();
        foreach (var node in Nodes)
        {
            string id = (string)node["cityId"];
            bool selected = tab == 0 && id == selectedId;
            Vector2 point = CanvasPoint(Position(node));
            Color tint = id == (string)puzzle["startCity"] || id == (string)puzzle["planningTargetCityId"] ? Presentation.PuzzleMapTarget : Presentation.PuzzleMapLineColor((string)node["regionId"]);
            Handles.color = selected ? Presentation.PuzzleMapTarget : tint;
            Handles.DrawSolidDisc(point, Vector3.forward, selected ? NODE_RADIUS + 3f : NODE_RADIUS);
            Handles.color = Presentation.PuzzleMapOcean;
            Handles.DrawSolidDisc(point, Vector3.forward, NODE_RADIUS - 3f);
            if ((bool)node["interchange"])
            {
                Handles.color = tint;
                Handles.DrawWireDisc(point, Vector3.forward, NODE_RADIUS + 3f);
            }
        }
        if (!showNames)
        {
            return;
        }
        foreach (var node in Nodes.OrderByDescending(_node => (string)_node["cityId"] == selectedId).ThenByDescending(_node => (int)City((string)_node["cityId"])["mapLabelImportance"]).ThenBy(_node => (string)_node["cityId"], StringComparer.Ordinal))
        {
            string id = (string)node["cityId"];
            Vector2 point = CanvasPoint(Position(node));
            if (!canvasRect.Contains(point))
            {
                continue;
            }
            var style = CanvasLabelStyle(id == selectedId);
            string name = CityName(id);
            Vector2 size = style.CalcSize(new GUIContent(name)) + new Vector2(6f, 2f);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float gap = 13f + (attempt / 4) * 17f;
                Vector2 offset;
                switch (attempt % 4)
                {
                    case 0:
                        offset = new Vector2(-size.x * 0.5f, -gap - size.y);
                        break;
                    case 1:
                        offset = new Vector2(gap, -size.y * 0.5f);
                        break;
                    case 2:
                        offset = new Vector2(-size.x * 0.5f, gap);
                        break;
                    default:
                        offset = new Vector2(-gap - size.x, -size.y * 0.5f);
                        break;
                }
                var rect = new Rect(point + offset, size);
                if (!canvasRect.Contains(rect.min) || !canvasRect.Contains(rect.max) || reserved.Any(_other => _other.Overlaps(rect)))
                {
                    continue;
                }
                reserved.Add(rect);
                Handles.color = Presentation.PuzzleHeading;
                Handles.DrawLine(point, new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax)));
                EditorGUI.DrawRect(rect, Presentation.PuzzleMapOcean);
                GUI.Label(rect, name, style);
                break;
            }
        }
    }

    JObject HitStation(Vector2 _point)
    {
        return Nodes.Select(_node => new { Node = _node, Distance = (CanvasPoint(Position(_node)) - _point).sqrMagnitude })
            .Where(_entry => _entry.Distance <= (NODE_RADIUS + 5f) * (NODE_RADIUS + 5f)).OrderBy(_entry => _entry.Distance)
            .ThenBy(_entry => (string)_entry.Node["cityId"], StringComparer.Ordinal).Select(_entry => _entry.Node).FirstOrDefault();
    }

    JObject HitRail(Vector2 _point)
    {
        JObject nearest = null;
        float distance = 8f;
        foreach (var rail in Rails.OrderBy(_rail => (string)_rail["sourceId"], StringComparer.Ordinal))
        {
            var points = DisplayPoints(rail);
            for (int i = 0; i < points.Count - 2; i += 2)
            {
                Vector2 start = CanvasPoint(ArrayPoint(points, i));
                Vector2 end = CanvasPoint(ArrayPoint(points, i + 2));
                Vector2 direction = end - start;
                float fraction = direction.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(_point - start, direction) / direction.sqrMagnitude) : 0f;
                float current = Vector2.Distance(_point, start + direction * fraction);
                if (current < distance)
                {
                    distance = current;
                    nearest = rail;
                }
            }
        }
        return nearest;
    }

    void BeginCanvasDrag(int _control)
    {
        GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = _control;
        canvasDragControl = _control;
        Undo.IncrementCurrentGroup();
        canvasUndoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("맵 요소 이동");
    }

    void ReleaseCanvasDrag()
    {
        if (pendingTimeLabels && draft && layout != null)
        {
            pendingTimeLabels = false;
            Change("시간 라벨 자동 배치", () => PlaceTimeLabels(null));
        }
        if (canvasDragControl != 0 && GUIUtility.hotControl == canvasDragControl)
        {
            GUIUtility.hotControl = 0;
        }
        if (canvasUndoGroup >= 0)
        {
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(canvasUndoGroup);
        }
        canvasDragControl = 0;
        canvasUndoGroup = -1;
        dragCity = null;
        dragRail = null;
        dragPoint = -1;
        panCanvas = false;
    }

    void HandleCanvasInput(int _control)
    {
        var input = Event.current;
        int previousTab = tab;
        string previousSelection = selectedId;
        Vector2 mouse = input.mousePosition;
        if (input.type == EventType.MouseUp && canvasDragControl != 0)
        {
            ReleaseCanvasDrag();
            input.Use();
            Repaint();
            return;
        }
        if (input.type == EventType.MouseDrag && canvasDragControl != 0 && GUIUtility.hotControl == canvasDragControl)
        {
            if (panCanvas)
            {
                canvasCenter += new Vector2(-input.delta.x, input.delta.y) / canvasZoom;
            }
            else if (dragCity != null)
            {
                Change("역 위치 이동", () => MoveStation(dragCity, WorldPoint(mouse) + dragOffset));
            }
            else if (dragRail != null)
            {
                var rail = Rails.Single(_rail => (string)_rail["sourceId"] == dragRail);
                if (dragPoint == -2)
                {
                    Change("시간 라벨 이동", () =>
                    {
                        Vector2 point = Snap(WorldPoint(mouse) + dragOffset);
                        rail["labelX"] = point.x;
                        rail["labelY"] = point.y;
                    });
                }
                else
                {
                    Change("노선 경유점 이동", () => SetRailPoint(rail, editTimeGeometry ? "points" : "displayPoints", dragPoint, WorldPoint(mouse) + dragOffset));
                }
            }
            input.Use();
            Repaint();
            return;
        }
        if (!canvasRect.Contains(mouse))
        {
            return;
        }
        if (input.type == EventType.ScrollWheel)
        {
            Vector2 anchor = WorldPoint(mouse);
            canvasZoom = Mathf.Clamp(canvasZoom * Mathf.Exp(-input.delta.y * 0.08f), MIN_CANVAS_ZOOM, MAX_CANVAS_ZOOM);
            canvasCenter += anchor - WorldPoint(mouse);
            input.Use();
            Repaint();
        }
        if (input.type != EventType.MouseDown || Application.isPlaying)
        {
            return;
        }
        if (input.button == 1 || input.button == 2 || input.alt)
        {
            BeginCanvasDrag(_control);
            panCanvas = true;
        }
        else if (input.button == 0)
        {
            var rail = tab == 1 ? Rails.FirstOrDefault(_rail => (string)_rail["sourceId"] == selectedId) : null;
            if (rail != null)
            {
                Vector2 label = new Vector2((float)rail["labelX"], (float)rail["labelY"]);
                if (showTimes && Vector2.Distance(CanvasPoint(label), mouse) < 12f)
                {
                    BeginCanvasDrag(_control);
                    dragRail = selectedId;
                    dragPoint = -2;
                    dragOffset = label - WorldPoint(mouse);
                }
                var points = EditingPoints(rail);
                for (int i = 0; i < points.Count && dragRail == null; i += 2)
                {
                    if (!editTimeGeometry && (i == 0 || i == points.Count - 2))
                    {
                        continue;
                    }
                    if (Vector2.Distance(CanvasPoint(ArrayPoint(points, i)), mouse) < 10f)
                    {
                        BeginCanvasDrag(_control);
                        dragRail = selectedId;
                        dragPoint = i;
                        dragOffset = ArrayPoint(points, i) - WorldPoint(mouse);
                        break;
                    }
                }
            }
            if (dragRail == null)
            {
                var station = HitStation(mouse);
                if (station != null)
                {
                    tab = 0;
                    selectedId = (string)station["cityId"];
                    BeginCanvasDrag(_control);
                    dragCity = selectedId;
                    dragOffset = Position(station) - WorldPoint(mouse);
                }
                else
                {
                    rail = HitRail(mouse);
                    if (rail != null)
                    {
                        tab = 1;
                        selectedId = (string)rail["sourceId"];
                    }
                    else
                    {
                        BeginCanvasDrag(_control);
                        panCanvas = true;
                    }
                }
            }
        }
        input.Use();
        Repaint();
        if (tab != previousTab || selectedId != previousSelection)
        {
            GUIUtility.ExitGUI();
        }
    }
}
