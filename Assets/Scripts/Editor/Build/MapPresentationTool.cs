using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MapPresentationTool
{
    const string PREFAB_PATH = "Assets/Art/PuzzleWorldMap.prefab";
    const string TERRAIN_PATH = "Assets/Art/PuzzleMapTerrain.png";
    const string GEOGRAPHY_PATH = "Docs/Management/rail_map_geography_seed_20265146.json";

    [MenuItem("Tools/MyJG/Generate Map Terrain Image")]
    public static void GenerateTerrain()
    {
        var data = JObject.Parse(File.ReadAllText(GEOGRAPHY_PATH));
        int width = (int)data["grid"]["width"];
        int height = (int)data["grid"]["height"];
        const int PIXELS_PER_TILE = 12;
        var texture = new Texture2D(width * PIXELS_PER_TILE, height * PIXELS_PER_TILE, TextureFormat.RGB24, false);
        var pixels = new Color32[texture.width * texture.height];
        var layers = data["layers"];
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int tileX = x / PIXELS_PER_TILE;
                int tileY = y / PIXELS_PER_TILE;
                int index = tileY * width + tileX;
                Color color = Presentation.PuzzleMapOcean;
                if ((int)layers["land"][index] == 1)
                {
                    int region = (int)data["regionIndex"][index];
                    Color tint = region < 0 ? Presentation.PuzzleMapLand :
                        Presentation.PuzzleMapLineColor((string)data["regionIndexToId"][region]);
                    color = Color.Lerp(Presentation.PuzzleMapLand, tint, 0.28f);
                    float elevation = (float)layers["elevationNormalized"][index];
                    color *= 0.82f + elevation * 0.42f;
                    if ((int)layers["river"][index] == 1)
                    {
                        color = Presentation.PuzzleMapRiver;
                    }
                    bool edge = tileX > 0 && (int)data["regionIndex"][index - 1] != region && x % PIXELS_PER_TILE == 0;
                    edge |= tileY > 0 && (int)data["regionIndex"][index - width] != region && y % PIXELS_PER_TILE == 0;
                    if (edge || (int)layers["coast"][index] == 1)
                    {
                        color = Color.Lerp(color, Presentation.PuzzleMapTerrainBorder, edge ? 0.35f : 0.15f);
                    }
                }
                color.a = 1f;
                pixels[(texture.height - 1 - y) * texture.width + x] = color;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(TERRAIN_PATH, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    [MenuItem("Tools/MyJG/Apply Map Presentation")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            return;
        }
        var terrain = AssetDatabase.LoadAssetAtPath<Texture2D>(TERRAIN_PATH);
        if (!terrain)
        {
            throw new System.InvalidOperationException("Generate Map Terrain Image, then focus the Editor before applying the map.");
        }
        var root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            ApplyTo(root, terrain);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyTo(GameObject _root, Texture2D _terrain)
    {
        var panel = Find(_root.transform, "MapPanel");
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        var labels = Find(panel, "SubwayMapLabels");
        var template = Find(panel, "CloseMap").GetComponent<Button>();
        var font = template.GetComponentInChildren<Text>(true).font;
        var viewport = Rect(panel, "MapViewport", Vector2.zero, Vector2.zero);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(48f, 228f);
        viewport.offsetMax = new Vector2(-48f, -228f);
        var backdrop = Component<Image>(viewport);
        backdrop.color = Presentation.PuzzleMapOcean;
        backdrop.raycastTarget = true;
        Component<RectMask2D>(viewport);
        var content = Rect(viewport, "MapContent", Vector2.zero, new Vector2(1760f, 960f));
        content.localScale = Vector3.one * 0.65f;

        var terrainRect = Rect(content, "TerrainBackground", new Vector2(0f, 0f), new Vector2(1570f, 820f));
        var image = Component<RawImage>(terrainRect);
        image.texture = _terrain;
        image.color = Color.white;
        image.raycastTarget = false;
        terrainRect.SetAsFirstSibling();

        Find(panel, "SubwayMapTracks").SetParent(content, false);
        foreach (var rect in labels.GetComponentsInChildren<RectTransform>(true).ToArray())
        {
            if (rect.name.StartsWith("Time_") || rect.name.StartsWith("TimePlate_"))
            {
                rect.SetParent(content, false);
            }
        }
        foreach (var node in _root.GetComponentsInChildren<PuzzleMapNodeView>(true))
        {
            node.transform.SetParent(content, false);
            var data = new SerializedObject(node);
            var ring = data.FindProperty("stationRing").objectReferenceValue as Image;
            var haloRect = Rect(node.transform, "CurrentHalo", ring.rectTransform.anchoredPosition, new Vector2(76f, 76f));
            haloRect.SetAsFirstSibling();
            var halo = Component<Image>(haloRect);
            halo.sprite = ring.sprite;
            halo.color = Presentation.PuzzleMapCurrentHalo;
            halo.raycastTarget = false;
            var badge = Label(node.transform, "CurrentBadge", font, Presentation.PUZZLE_MAP_CURRENT,
                ring.rectTransform.anchoredPosition + new Vector2(0f, 58f), new Vector2(190f, 30f), 20);
            badge.color = Presentation.PuzzleMapCurrent;
            badge.fontStyle = FontStyle.Bold;
            halo.gameObject.SetActive(false);
            badge.gameObject.SetActive(false);
            data.FindProperty("currentHalo").objectReferenceValue = halo;
            data.FindProperty("currentBadge").objectReferenceValue = badge;
            data.ApplyModifiedPropertiesWithoutUndo();
            var stationLabel = data.FindProperty("label").objectReferenceValue as Text;
            var outline = Component<Outline>(stationLabel.rectTransform);
            outline.effectColor = Presentation.PuzzleBackground;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        Place(Find(panel, "Title"), new Vector2(0f, 488f), new Vector2(1344f, 48f));
        Place(template.transform, new Vector2(816f, 488f), new Vector2(192f, 48f));
        Place(Find(panel, "Guide"), new Vector2(0f, 366f), new Vector2(1760f, 76f));
        Find(panel, "Guide").GetComponent<Text>().fontSize = 20;
        Place(Find(panel, "CityInformation"), new Vector2(0f, -426f), new Vector2(1744f, 180f));
        int regionIndex = 0;
        foreach (var region in new[] { "region.mine", "region.ranch", "region.industry", "region.capital", "region.coast", "region.farm", "region.common" })
        {
            float x = -796f + regionIndex * 252f;
            Place(Find(labels, "LineSwatch_" + region), new Vector2(x, 432f), new Vector2(20f, 6f));
            var label = Find(labels, "LineName_" + region);
            Place(label, new Vector2(x + 116f, 432f), new Vector2(202f, 30f));
            label.GetComponent<Text>().fontSize = 19;
            regionIndex++;
        }

        labels.anchorMin = Vector2.zero;
        labels.anchorMax = Vector2.one;
        labels.offsetMin = new Vector2(48f, 0f);
        labels.offsetMax = new Vector2(-48f, 0f);
        regionIndex = 0;
        foreach (var label in labels.GetComponentsInChildren<Text>(true))
        {
            if (!label.name.StartsWith("LineName_"))
            {
                continue;
            }
            // Use the same stable region order regardless of hierarchy serialization order.
            var order = new[] { "region.mine", "region.ranch", "region.industry", "region.capital", "region.coast", "region.farm", "region.common" };
            regionIndex = System.Array.IndexOf(order, label.name.Substring("LineName_".Length));
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(regionIndex / 7f, 1f);
            rect.anchorMax = new Vector2((regionIndex + 1f) / 7f, 1f);
            rect.anchoredPosition = new Vector2(12f, -108f);
            rect.sizeDelta = new Vector2(-44f, 30f);
            var swatch = Find(labels, "LineSwatch_" + order[regionIndex]);
            swatch.anchorMin = swatch.anchorMax = new Vector2(regionIndex / 7f, 1f);
            swatch.anchoredPosition = new Vector2(18f, -108f);
        }

        var controls = Rect(panel, "MapControls", new Vector2(0f, -348f), new Vector2(1744f, 52f));
        var minus = Button(controls, template, "ZoomOut", Presentation.PUZZLE_MAP_ZOOM_OUT, -294f, 64f);
        var percent = Label(controls, "ZoomPercent", font, Presentation.PuzzleMapZoom(0.65f), new Vector2(-206f, 0f), new Vector2(100f, 48f), 22);
        var plus = Button(controls, template, "ZoomIn", Presentation.PUZZLE_MAP_ZOOM_IN, -118f, 64f);
        var fit = Button(controls, template, "FitMap", Presentation.PUZZLE_MAP_FIT, 56f, 216f);
        var locate = Button(controls, template, "LocateCurrent", Presentation.PUZZLE_MAP_LOCATE, 304f, 216f);
        Place(Find(panel, "CityInformation"), new Vector2(0f, -458f), new Vector2(1744f, 116f));
        var info = Find(panel, "CityInformation").GetComponent<Text>();
        info.fontSize = 18;
        info.resizeTextForBestFit = true;
        info.resizeTextMinSize = 14;
        info.resizeTextMaxSize = 20;
        Header(Find(panel, "Title"), 288f, 288f, 28f, 48f);
        Header(Find(panel, "Guide"), 48f, 48f, 136f, 76f);
        var close = (RectTransform)template.transform;
        close.anchorMin = close.anchorMax = Vector2.one;
        close.anchoredPosition = new Vector2(-144f, -52f);
        controls.anchorMin = new Vector2(0f, 0f);
        controls.anchorMax = new Vector2(1f, 0f);
        controls.anchoredPosition = new Vector2(0f, 192f);
        controls.sizeDelta = new Vector2(-96f, 52f);
        info.rectTransform.anchorMin = new Vector2(0f, 0f);
        info.rectTransform.anchorMax = new Vector2(1f, 0f);
        info.rectTransform.anchoredPosition = new Vector2(0f, 82f);
        info.rectTransform.sizeDelta = new Vector2(-96f, 116f);
        var navigation = Component<PuzzleMapNavigation>(viewport);
        var serialized = new SerializedObject(navigation);
        Bind(serialized, "view", _root.GetComponentInChildren<PuzzleMapView>(true));
        Bind(serialized, "inputActions", AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
        Bind(serialized, "content", content);
        Bind(serialized, "viewport", viewport);
        Bind(serialized, "zoomIn", plus);
        Bind(serialized, "zoomOut", minus);
        Bind(serialized, "fit", fit);
        Bind(serialized, "locate", locate);
        Bind(serialized, "zoomLabel", percent);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var view = new SerializedObject(_root.GetComponentInChildren<PuzzleMapView>(true));
        Bind(view, "navigation", navigation);
        var routes = view.FindProperty("routeTimes");
        for (int i = 0; i < routes.arraySize; i++)
        {
            var route = routes.GetArrayElementAtIndex(i);
            var label = route.FindPropertyRelative("label").objectReferenceValue as Text;
            string index = label.name.Substring("Time_".Length);
            var plate = Find(content, "TimePlate_" + index).GetComponent<Image>();
            route.FindPropertyRelative("plate").objectReferenceValue = plate;
            label.raycastTarget = false;
            plate.raycastTarget = false;
        }
        view.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(SerializedObject _target, string _field, Object _value)
    {
        _target.FindProperty(_field).objectReferenceValue = _value;
    }

    static void Header(RectTransform _rect, float _left, float _right, float _top, float _height)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = Vector2.one;
        _rect.anchoredPosition = new Vector2((_left - _right) * 0.5f, -_top - _height * 0.5f);
        _rect.sizeDelta = new Vector2(-_left - _right, _height);
    }

    static T Component<T>(RectTransform _rect) where T : UnityEngine.Component
    {
        return _rect.TryGetComponent<T>(out var component) ? component : _rect.gameObject.AddComponent<T>();
    }

    static RectTransform Rect(Transform _parent, string _name, Vector2 _position, Vector2 _size)
    {
        var child = _parent.Find(_name) as RectTransform;
        if (!child)
        {
            child = new GameObject(_name, typeof(RectTransform)).transform as RectTransform;
            child.SetParent(_parent, false);
        }
        child.anchorMin = child.anchorMax = child.pivot = new Vector2(0.5f, 0.5f);
        child.anchoredPosition = _position;
        child.sizeDelta = _size;
        return child;
    }

    static Text Label(Transform _parent, string _name, Font _font, string _text, Vector2 _position, Vector2 _size, int _fontSize)
    {
        var label = Component<Text>(Rect(_parent, _name, _position, _size));
        label.font = _font;
        label.text = _text;
        label.fontSize = _fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Presentation.PuzzleHeading;
        label.raycastTarget = false;
        return label;
    }

    static Button Button(Transform _parent, Button _template, string _name, string _text, float _x, float _width)
    {
        var existing = _parent.Find(_name);
        var button = existing ? existing.GetComponent<Button>() : Object.Instantiate(_template, _parent);
        button.name = _name;
        button.onClick = new Button.ButtonClickedEvent();
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        Place(button.transform, new Vector2(_x, 0f), new Vector2(_width, 48f));
        button.GetComponentInChildren<Text>(true).text = _text;
        return button;
    }

    static void Place(Transform _transform, Vector2 _position, Vector2 _size)
    {
        var rect = (RectTransform)_transform;
        rect.anchoredPosition = _position;
        rect.sizeDelta = _size;
    }

    static RectTransform Find(Transform _root, string _name)
    {
        return _root.GetComponentsInChildren<RectTransform>(true).First(_rect => _rect.name == _name);
    }
}
