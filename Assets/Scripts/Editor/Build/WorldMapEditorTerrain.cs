using System.Linq;
using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 지형 탭과 지명 중요도 표시. 획 하나가 Undo 하나이며, 저장하면 지형 이미지와 지도 프리팹의 지형 영역을 다시 만든다.
public sealed partial class WorldMapEditorWindow
{
    const int TERRAIN_TAB = 3;
    const float REGION_PREVIEW_BLEND = 0.45f;
    [SerializeField] bool paintRegion;
    [SerializeField] int paintKindIndex = 1;
    [SerializeField] string paintRegionId;
    [SerializeField] float brushRadius = 3f;
    [SerializeField] bool showRegions = true;
    WorldMapGeography geography;
    Texture2D terrainPreview;
    bool terrainPreviewDirty = true, terrainPreviewTinted, paintingTerrain, terrainStrokeChanged;
    Vector2 lastPaintCell;
    int? majorThreshold;

    int MajorThreshold
    {
        get
        {
            if (majorThreshold == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailMapLayoutTool.PREFAB_PATH);
                var view = prefab ? prefab.GetComponentInChildren<PuzzleMapView>(true) : null;
                majorThreshold = view ? view.MajorStationThreshold : int.MaxValue;
            }
            return majorThreshold.Value;
        }
    }

    string MajorThresholdText => MajorThreshold == int.MaxValue
        ? "지도 프리팹에서 주요 역 기준을 읽지 못했습니다."
        : "중요도 " + MajorThreshold + " 이상이 주요 역입니다. 주요 역은 지도를 축소해도 이름이 보입니다(기준은 지도 프리팹 PuzzleMapView).";

    bool IsMajor(JObject _city)
    {
        return (int)_city["mapLabelImportance"] >= MajorThreshold;
    }

    void DrawMajorStation(JObject _city)
    {
        if (MajorThreshold == int.MaxValue)
        {
            EditorGUILayout.HelpBox(MajorThresholdText, MessageType.Warning);
            return;
        }
        bool major = IsMajor(_city);
        bool next = EditorGUILayout.Toggle("주요 역", major);
        if (next != major)
        {
            // 켜면 기준값까지 올리고, 끄면 기준 바로 아래로 내린다. 세부 순서는 중요도 숫자로 조정한다.
            Change("주요 역", () => _city["mapLabelImportance"] = next ? MajorThreshold : MajorThreshold - 1);
        }
        EditorGUILayout.LabelField(MajorThresholdText, EditorStyles.wordWrappedMiniLabel);
    }

    void DrawTerrain()
    {
        EditorGUILayout.LabelField("지형 칠하기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("가운데 지도에서 왼쪽 드래그로 칠합니다. 오른쪽·중간 드래그는 이동, 휠은 확대/축소입니다. 저장하면 지형 이미지(" + MapPresentationTool.TERRAIN_PATH + ")와 지도 프리팹의 지형 영역이 갱신됩니다.", MessageType.Info);
        paintRegion = GUILayout.Toolbar(paintRegion ? 1 : 0, new[] { "지형", "지역" }) == 1;
        if (paintRegion)
        {
            var regionRows = Regions.OrderBy(_region => (string)_region["id"], StringComparer.Ordinal).ToArray();
            string[] options = new[] { "지역 없음" }.Concat(regionRows.Select(_region => (string)_region["displayName"])).ToArray();
            int index = Array.FindIndex(regionRows, _region => (string)_region["id"] == paintRegionId) + 1;
            int next = EditorGUILayout.Popup("칠할 지역", index, options);
            paintRegionId = next == 0 ? null : (string)regionRows[next - 1]["id"];
            EditorGUILayout.LabelField("지역이 다른 육지 칸 사이에는 지형 이미지에 경계선이 그려집니다.", EditorStyles.wordWrappedMiniLabel);
        }
        else
        {
            for (int i = 0; i < WorldMapGeography.KINDS.Length; i++)
            {
                char kind = WorldMapGeography.KINDS[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));
                    EditorGUI.DrawRect(swatch, WorldMapGeography.KindColor(kind));
                    if (GUILayout.Toggle(paintKindIndex == i, WorldMapGeography.KindName(kind) + " (" + kind + ")", "Button"))
                    {
                        paintKindIndex = i;
                    }
                }
            }
        }
        brushRadius = EditorGUILayout.Slider("붓 크기 (칸)", brushRadius, 0.5f, 20f);
        bool regions = EditorGUILayout.Toggle("지역 색 겹쳐 보기", showRegions);
        if (regions != showRegions)
        {
            showRegions = regions;
            terrainPreviewDirty = true;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("격자 " + geography.Width + "×" + geography.Height + "칸 · 칸 크기 " + geography.CellWidth.ToString("0.##") + "×" + geography.CellHeight.ToString("0.##"), EditorStyles.miniLabel);
        EditorGUILayout.LabelField("색은 기초데이터 presentation.map.terrain* 색을 따릅니다.", EditorStyles.miniLabel);
    }

    void DrawCanvasTerrain()
    {
        if (!showTerrain || geography == null)
        {
            return;
        }
        bool tinted = tab == TERRAIN_TAB && showRegions;
        if (!terrainPreview || terrainPreview.width != geography.Width || terrainPreview.height != geography.Height)
        {
            if (terrainPreview)
            {
                DestroyImmediate(terrainPreview);
            }
            terrainPreview = new Texture2D(geography.Width, geography.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            terrainPreviewDirty = true;
        }
        if (terrainPreviewDirty || terrainPreviewTinted != tinted)
        {
            terrainPreview.SetPixels32(geography.Render(1, 0f, tinted ? RegionTint : (Func<int, int, Color, Color>)null));
            terrainPreview.Apply();
            terrainPreviewDirty = false;
            terrainPreviewTinted = tinted;
        }
        GUI.DrawTexture(new Rect(CanvasPoint(geography.TopLeft), geography.Size * canvasZoom), terrainPreview, ScaleMode.StretchToFill);
    }

    Color RegionTint(int _x, int _y, Color _color)
    {
        string region = geography.RegionId(_x, _y);
        if (region == null)
        {
            return _color;
        }
        Color tint = Presentation.PuzzleMapLineColor(region);
        tint.a = 1f;
        return Color.Lerp(_color, tint, REGION_PREVIEW_BLEND);
    }

    void DrawTerrainBrush()
    {
        if (tab != TERRAIN_TAB || geography == null || !canvasRect.Contains(Event.current.mousePosition))
        {
            return;
        }
        Handles.color = Presentation.PuzzleHeading;
        Handles.DrawWireDisc(Event.current.mousePosition, Vector3.forward, brushRadius * geography.CellHeight * canvasZoom);
    }

    void BeginTerrainPaint(int _control, Vector2 _mouse)
    {
        BeginCanvasDrag(_control);
        paintingTerrain = true;
        terrainStrokeChanged = false;
        lastPaintCell = geography.CellPoint(WorldPoint(_mouse));
        PaintTerrain(lastPaintCell);
    }

    void ContinueTerrainPaint(Vector2 _mouse)
    {
        // 빠르게 끌어도 끊기지 않도록 이전 위치부터 붓 반경 절반 간격으로 칠한다.
        Vector2 cell = geography.CellPoint(WorldPoint(_mouse));
        float step = Mathf.Max(0.5f, brushRadius * 0.5f);
        int count = Mathf.CeilToInt(Vector2.Distance(lastPaintCell, cell) / step);
        for (int i = 1; i <= count; i++)
        {
            PaintTerrain(Vector2.Lerp(lastPaintCell, cell, i / (float)count));
        }
        lastPaintCell = cell;
    }

    void PaintTerrain(Vector2 _cell)
    {
        char code = paintRegion ? geography.RegionCodeFor(paintRegionId) : WorldMapGeography.KINDS[paintKindIndex];
        if (geography.Paint(_cell, brushRadius, code, paintRegion))
        {
            terrainPreviewDirty = true;
            terrainStrokeChanged = true;
        }
    }

    void CommitTerrainStroke()
    {
        paintingTerrain = false;
        if (!terrainStrokeChanged)
        {
            return;
        }
        terrainStrokeChanged = false;
        Undo.RecordObject(draft, "지형 칠하기");
        draft.geography = geography.ToJson();
        hasUnsavedChanges = Dirty;
        message = null;
    }
}
