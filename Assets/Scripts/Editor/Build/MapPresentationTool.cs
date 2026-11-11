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
        int pixelsPerTile = BasicData.Int("editor.MapPresentationTool.Pixelspertile");
        var texture = new Texture2D(width * pixelsPerTile, height * pixelsPerTile, TextureFormat.RGB24, false);
        var pixels = new Color32[texture.width * texture.height];
        var layers = data["layers"];
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int tileX = x / pixelsPerTile;
                int tileY = y / pixelsPerTile;
                int index = tileY * width + tileX;
                Color color = Presentation.PuzzleMapOcean;
                if ((int)layers["land"][index] == 1)
                {
                    int region = (int)data["regionIndex"][index];
                    Color tint = region < 0 ? Presentation.PuzzleMapLand :
                        Presentation.PuzzleMapLineColor((string)data["regionIndexToId"][region]);
                    color = Color.Lerp(Presentation.PuzzleMapLand, tint, BasicData.Float("editor.MapPresentationTool.Texture2D.number1"));
                    float elevation = (float)layers["elevationNormalized"][index];
                    color *= BasicData.Float("editor.MapPresentationTool.Texture2D.number2") + elevation * BasicData.Float("editor.MapPresentationTool.Texture2D.number3");
                    if ((int)layers["river"][index] == 1)
                    {
                        color = Presentation.PuzzleMapRiver;
                    }
                    bool edge = tileX > 0 && (int)data["regionIndex"][index - 1] != region && x % pixelsPerTile == 0;
                    edge |= tileY > 0 && (int)data["regionIndex"][index - width] != region && y % pixelsPerTile == 0;
                    if (edge || (int)layers["coast"][index] == 1)
                    {
                        color = Color.Lerp(color, Presentation.PuzzleMapTerrainBorder, edge ? BasicData.Float("editor.MapPresentationTool.Texture2D.number4") : BasicData.Float("editor.MapPresentationTool.Texture2D.number5"));
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
        viewport.offsetMin = new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number1"), BasicData.Float("editor.MapPresentationTool.Vector2.number2"));
        viewport.offsetMax = new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number3"), -BasicData.Float("editor.MapPresentationTool.Vector2.number4"));
        var backdrop = Component<Image>(viewport);
        backdrop.color = Presentation.PuzzleMapOcean;
        backdrop.raycastTarget = true;
        Component<RectMask2D>(viewport);
        var content = Rect(viewport, "MapContent", Vector2.zero, new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number5"), BasicData.Float("editor.MapPresentationTool.Vector2.number6")));
        content.localScale = Vector3.one * BasicData.Float("editor.MapPresentationTool.Vector2.number7");

        var terrainRect = Rect(content, "TerrainBackground", new Vector2(0f, 0f), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number8"), BasicData.Float("editor.MapPresentationTool.Vector2.number9")));
        var image = Component<RawImage>(terrainRect);
        image.texture = _terrain;
        image.color = Presentation.PuzzleUnmodifiedTint;
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
            var haloRect = Rect(node.transform, "CurrentHalo", ring.rectTransform.anchoredPosition, new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number10"), BasicData.Float("editor.MapPresentationTool.Vector2.number11")));
            haloRect.SetAsFirstSibling();
            var halo = Component<Image>(haloRect);
            halo.sprite = ring.sprite;
            halo.color = Presentation.PuzzleMapCurrentHalo;
            halo.raycastTarget = false;
            var badge = Label(node.transform, "CurrentBadge", font, Presentation.PuzzleMapCurrentText,
                ring.rectTransform.anchoredPosition + new Vector2(0f, BasicData.Float("editor.MapPresentationTool.Vector2.number12")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number13"), BasicData.Float("editor.MapPresentationTool.Vector2.number14")), BasicData.Int("editor.MapPresentationTool.Vector2.number15"));
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

        Place(Find(panel, "Title"), new Vector2(0f, BasicData.Float("editor.MapPresentationTool.Vector2.number16")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number17"), BasicData.Float("editor.MapPresentationTool.Vector2.number18")));
        Place(template.transform, new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number19"), BasicData.Float("editor.MapPresentationTool.Vector2.number20")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number21"), BasicData.Float("editor.MapPresentationTool.Vector2.number22")));
        Place(Find(panel, "Guide"), new Vector2(0f, BasicData.Float("editor.MapPresentationTool.Vector2.number23")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number24"), BasicData.Float("editor.MapPresentationTool.Vector2.number25")));
        Find(panel, "Guide").GetComponent<Text>().fontSize = BasicData.Int("editor.MapPresentationTool.Vector2.number26");
        Place(Find(panel, "CityInformation"), new Vector2(0f, -BasicData.Float("editor.MapPresentationTool.Vector2.number27")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number28"), BasicData.Float("editor.map.cityInformationHeight")));
        int regionIndex = 0;
        foreach (var region in new[] { "region.mine", "region.ranch", "region.industry", "region.capital", "region.coast", "region.farm", "region.common" })
        {
            float x = -BasicData.Float("editor.MapPresentationTool.Vector2.number29") + regionIndex * BasicData.Float("editor.MapPresentationTool.Vector2.number30");
            Place(Find(labels, "LineSwatch_" + region), new Vector2(x, BasicData.Float("editor.MapPresentationTool.Vector2.number31")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number32"), BasicData.Float("editor.MapPresentationTool.Vector2.number33")));
            var label = Find(labels, "LineName_" + region);
            Place(label, new Vector2(x + BasicData.Float("editor.MapPresentationTool.Vector2.number34"), BasicData.Float("editor.MapPresentationTool.Vector2.number35")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number36"), BasicData.Float("editor.MapPresentationTool.Vector2.number37")));
            label.GetComponent<Text>().fontSize = BasicData.Int("editor.MapPresentationTool.Vector2.number38");
            regionIndex++;
        }

        labels.anchorMin = Vector2.zero;
        labels.anchorMax = Vector2.one;
        labels.offsetMin = new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number39"), 0f);
        labels.offsetMax = new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number40"), 0f);
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
            rect.anchorMin = new Vector2(regionIndex / BasicData.Float("editor.MapPresentationTool.Vector2.number41"), 1f);
            rect.anchorMax = new Vector2((regionIndex + 1f) / BasicData.Float("editor.MapPresentationTool.Vector2.number42"), 1f);
            rect.anchoredPosition = new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number43"), -BasicData.Float("editor.MapPresentationTool.Vector2.number44"));
            rect.sizeDelta = new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number45"), BasicData.Float("editor.MapPresentationTool.Vector2.number46"));
            var swatch = Find(labels, "LineSwatch_" + order[regionIndex]);
            swatch.anchorMin = swatch.anchorMax = new Vector2(regionIndex / BasicData.Float("editor.MapPresentationTool.Vector2.number47"), 1f);
            swatch.anchoredPosition = new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number48"), -BasicData.Float("editor.MapPresentationTool.Vector2.number49"));
        }

        var controls = Rect(panel, "MapControls", new Vector2(0f, -BasicData.Float("editor.MapPresentationTool.Vector2.number50")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number51"), BasicData.Float("editor.MapPresentationTool.Vector2.number52")));
        var minus = Button(controls, template, "ZoomOut", Presentation.PuzzleMapZoomOut, -BasicData.Float("editor.MapPresentationTool.Vector2.number53"), BasicData.Float("editor.MapPresentationTool.Vector2.number54"));
        var percent = Label(controls, "ZoomPercent", font, Presentation.PuzzleMapZoom(BasicData.Float("editor.MapPresentationTool.Vector2.number55")), new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number56"), 0f), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number57"), BasicData.Float("editor.MapPresentationTool.Vector2.number58")), BasicData.Int("editor.MapPresentationTool.Vector2.number59"));
        var plus = Button(controls, template, "ZoomIn", Presentation.PuzzleMapZoomIn, -BasicData.Float("editor.MapPresentationTool.Vector2.number60"), BasicData.Float("editor.MapPresentationTool.Vector2.number61"));
        var fit = Button(controls, template, "FitMap", Presentation.PuzzleMapFit, BasicData.Float("editor.MapPresentationTool.Vector2.number62"), BasicData.Float("editor.MapPresentationTool.Vector2.number63"));
        var locate = Button(controls, template, "LocateCurrent", Presentation.PuzzleMapLocate, BasicData.Float("editor.MapPresentationTool.Vector2.number64"), BasicData.Float("editor.MapPresentationTool.Vector2.number65"));
        Place(Find(panel, "CityInformation"), new Vector2(0f, -BasicData.Float("editor.MapPresentationTool.Vector2.number66")), new Vector2(BasicData.Float("editor.MapPresentationTool.Vector2.number67"), BasicData.Float("editor.MapPresentationTool.Vector2.number68")));
        var info = Find(panel, "CityInformation").GetComponent<Text>();
        info.fontSize = BasicData.Int("editor.MapPresentationTool.Vector2.number69");
        info.resizeTextForBestFit = true;
        info.resizeTextMinSize = BasicData.Int("editor.MapPresentationTool.Vector2.number70");
        info.resizeTextMaxSize = BasicData.Int("editor.MapPresentationTool.Vector2.number71");
        Header(Find(panel, "Title"), BasicData.Float("editor.MapPresentationTool.Vector2.number72"), BasicData.Float("editor.MapPresentationTool.Vector2.number73"), BasicData.Float("editor.MapPresentationTool.Vector2.number74"), BasicData.Float("editor.MapPresentationTool.Vector2.number75"));
        Header(Find(panel, "Guide"), BasicData.Float("editor.MapPresentationTool.Vector2.number76"), BasicData.Float("editor.MapPresentationTool.Vector2.number77"), BasicData.Float("editor.MapPresentationTool.Vector2.number78"), BasicData.Float("editor.MapPresentationTool.Vector2.number79"));
        var close = (RectTransform)template.transform;
        close.anchorMin = close.anchorMax = Vector2.one;
        close.anchoredPosition = new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number80"), -BasicData.Float("editor.MapPresentationTool.Vector2.number81"));
        var open = Find(_root.transform, "OpenMap");
        open.anchorMin = close.anchorMin;
        open.anchorMax = close.anchorMax;
        open.pivot = close.pivot;
        Place(open, close.anchoredPosition, close.sizeDelta);
        controls.anchorMin = new Vector2(0f, 0f);
        controls.anchorMax = new Vector2(1f, 0f);
        controls.anchoredPosition = new Vector2(0f, BasicData.Float("editor.MapPresentationTool.Vector2.number82"));
        controls.sizeDelta = new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number83"), BasicData.Float("editor.MapPresentationTool.Vector2.number84"));
        info.rectTransform.anchorMin = new Vector2(0f, 0f);
        info.rectTransform.anchorMax = new Vector2(1f, 0f);
        info.rectTransform.anchoredPosition = new Vector2(0f, BasicData.Float("editor.MapPresentationTool.Vector2.number85"));
        info.rectTransform.sizeDelta = new Vector2(-BasicData.Float("editor.MapPresentationTool.Vector2.number86"), BasicData.Float("editor.MapPresentationTool.Vector2.number87"));
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
        Place(button.transform, new Vector2(_x, 0f), new Vector2(_width, BasicData.Float("editor.MapPresentationTool.Vector2.number88")));
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
