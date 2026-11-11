using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MapTravelSetupTool
{
    const string PREFAB_PATH = "Assets/Art/PuzzleWorldMap.prefab";

    [MenuItem("Tools/MyJG/Apply Map Travel UI")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            ApplyTo(root);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyTo(GameObject _root)
    {
        var panel = _root.GetComponentsInChildren<RectTransform>(true).First(_rect => _rect.name == "MapPanel");
        var template = _root.GetComponentsInChildren<Button>(true).First(_button => _button.name == "CloseMap");
        var font = template.GetComponentInChildren<Text>(true).font;
        var host = Rect(panel, Presentation.PUZZLE_TRAVEL_PANEL_NAME, Vector2.zero, Vector2.zero);
        Stretch(host);
        host.SetAsLastSibling();
        var canvas = Component<Canvas>(host);
        canvas.overrideSorting = true;
        canvas.sortingOrder = BasicData.Int("editor.MapTravelSetupTool.ApplyTo.number1");
        Component<GraphicRaycaster>(host);
        var surface = Rect(host, "TravelOverlay", Vector2.zero, Vector2.zero);
        Stretch(surface);
        var dim = Component<Image>(surface);
        dim.color = Presentation.PuzzleTravelDim;
        dim.raycastTarget = true;

        var confirm = Card(surface, "Confirmation", new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number1"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number2")));
        var question = Label(confirm, "Question", font, Presentation.PuzzleTravelQuestion, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.Vector2.number3")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number4"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number5")), BasicData.Int("editor.MapTravelSetupTool.Vector2.number6"));
        var quote = Label(confirm, "Quote", font, string.Empty, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.Vector2.number7")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number8"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number9")), BasicData.Int("editor.MapTravelSetupTool.Vector2.number10"));
        var yes = Button(confirm, template, "ConfirmTravel", Presentation.PuzzleTravelConfirm, new Vector2(-BasicData.Float("editor.MapTravelSetupTool.Vector2.number11"), -BasicData.Float("editor.MapTravelSetupTool.Vector2.number12")));
        var no = Button(confirm, template, "CancelTravel", Presentation.PuzzleTravelCancel, new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number13"), -BasicData.Float("editor.MapTravelSetupTool.Vector2.number14")));
        yes.GetComponent<Image>().color = Presentation.PuzzleMapCurrent;
        yes.GetComponentInChildren<Text>(true).color = Presentation.PuzzleBackground;
        yes.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = no };
        no.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yes };
        var journey = Card(surface, "Journey", new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number15"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number16")));
        var title = Label(journey, "JourneyTitle", font, Presentation.PuzzleTravelInProgress, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.Vector2.number17")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number18"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number19")), BasicData.Int("editor.MapTravelSetupTool.Vector2.number20"));
        var route = Label(journey, "JourneyRoute", font, string.Empty, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.Vector2.number21")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number22"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number23")), BasicData.Int("editor.MapTravelSetupTool.Vector2.number24"));
        var track = Rect(journey, "Track", new Vector2(0f, -BasicData.Float("editor.MapTravelSetupTool.Vector2.number25")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number26"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number27")));
        Component<Image>(track).color = Presentation.PuzzleMapUnknown;
        for (int i = 0; i < BasicData.Int("editor.MapTravelSetupTool.Vector2.number28"); i++)
        {
            var sleeper = Rect(journey, "Sleeper_" + i, new Vector2(-BasicData.Float("editor.MapTravelSetupTool.Vector2.number29") + i * BasicData.Float("editor.MapTravelSetupTool.Vector2.number30"), -BasicData.Float("editor.MapTravelSetupTool.Vector2.number31")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number32"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number33")));
            var graphic = Component<Image>(sleeper);
            graphic.color = Presentation.PuzzleMapUnknown;
            graphic.raycastTarget = false;
        }
        var start = Rect(journey, "Departure", new Vector2(-BasicData.Float("editor.MapTravelSetupTool.Vector2.number34"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number35")), Vector2.one);
        var end = Rect(journey, "Destination", new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number36"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number37")), Vector2.one);
        var train = Rect(journey, "Train", start.anchoredPosition, new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number38"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number39")));
        var body = Rect(train, "Body", new Vector2(-BasicData.Float("editor.MapTravelSetupTool.Vector2.number40"), 0f), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number41"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number42")));
        Component<Image>(body).color = Presentation.PuzzleMapCurrent;
        var cab = Rect(train, "Cab", new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number43"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number44")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number45"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number46")));
        Component<Image>(cab).color = Presentation.PuzzleMapCurrent;
        var window = Rect(train, "Window", new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number47"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number48")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number49"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number50")));
        Component<Image>(window).color = Presentation.PuzzleTravelCard;
        var wheelSprite = _root.GetComponentsInChildren<Image>(true).First(_image => _image.name == "Marker").sprite;
        for (int i = 0; i < BasicData.Int("editor.MapTravelSetupTool.Vector2.number51"); i++)
        {
            var wheel = Rect(train, "Wheel_" + i, new Vector2(-BasicData.Float("editor.MapTravelSetupTool.Vector2.number52") + i * BasicData.Float("editor.MapTravelSetupTool.Vector2.number53"), -BasicData.Float("editor.MapTravelSetupTool.Vector2.number54")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number55"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number56")));
            var graphic = Component<Image>(wheel);
            graphic.sprite = wheelSprite;
            graphic.color = Presentation.PuzzleHeading;
        }
        var progressBack = Rect(journey, "ProgressTrack", new Vector2(0f, -BasicData.Float("editor.MapTravelSetupTool.Vector2.number57")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number58"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number59")));
        var progressBackImage = Component<Image>(progressBack);
        progressBackImage.sprite = null;
        progressBackImage.type = Image.Type.Simple;
        progressBackImage.color = Presentation.PuzzleCell;
        var legacyProgress = journey.Find("Progress");
        if (legacyProgress)
        {
            legacyProgress.SetParent(progressBack, false);
        }
        var progressRect = Rect(progressBack, "Progress", Vector2.zero, Vector2.zero);
        progressRect.anchorMin = Vector2.zero;
        progressRect.anchorMax = new Vector2(0f, 1f);
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = Vector2.zero;
        var progress = Component<Image>(progressRect);
        progress.sprite = null;
        progress.type = Image.Type.Simple;
        progress.fillAmount = 1f;
        progress.color = Presentation.PuzzleMapCurrent;
        var percent = Label(journey, "Percent", font, Presentation.PuzzleTravelProgress(0f), new Vector2(0f, -BasicData.Float("editor.MapTravelSetupTool.Vector2.number60")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number61"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number62")), BasicData.Int("editor.MapTravelSetupTool.Vector2.number63"));
        foreach (var graphic in journey.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        var view = Component<PuzzleMapTravelView>(host);
        var data = new SerializedObject(view);
        data.FindProperty("startOpen").boolValue = false;
        Bind(data, "root", surface.gameObject);
        Bind(data, "inputActions", AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
        Bind(data, "confirmation", confirm.gameObject);
        Bind(data, "journey", journey.gameObject);
        Bind(data, "question", question);
        Bind(data, "quote", quote);
        Bind(data, "confirmLabel", yes.GetComponentInChildren<Text>(true));
        Bind(data, "cancelLabel", no.GetComponentInChildren<Text>(true));
        Bind(data, "confirmButton", yes);
        Bind(data, "cancelButton", no);
        Bind(data, "journeyTitle", title);
        Bind(data, "journeyRoute", route);
        Bind(data, "progressLabel", percent);
        Bind(data, "progress", progress);
        Bind(data, "train", train);
        Bind(data, "departure", start);
        Bind(data, "destination", end);
        data.ApplyModifiedPropertiesWithoutUndo();
        var presenter = new SerializedObject(_root.GetComponentInChildren<PuzzleMapPresenter>(true));
        Bind(presenter, "travelView", view);
        presenter.ApplyModifiedPropertiesWithoutUndo();
        confirm.gameObject.SetActive(false);
        journey.gameObject.SetActive(false);
        surface.gameObject.SetActive(false);
    }

    static RectTransform Card(Transform _parent, string _name, Vector2 _size)
    {
        var rect = Rect(_parent, _name, Vector2.zero, _size);
        var image = Component<Image>(rect);
        image.color = Presentation.PuzzleTravelCard;
        image.raycastTarget = true;
        return rect;
    }

    static Button Button(Transform _parent, Button _template, string _name, string _text, Vector2 _position)
    {
        var existing = _parent.Find(_name);
        var button = existing ? existing.GetComponent<Button>() : Object.Instantiate(_template, _parent);
        button.name = _name;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = _position;
        rect.sizeDelta = new Vector2(BasicData.Float("editor.MapTravelSetupTool.Vector2.number64"), BasicData.Float("editor.MapTravelSetupTool.Vector2.number65"));
        button.onClick = new Button.ButtonClickedEvent();
        button.GetComponentInChildren<Text>(true).text = _text;
        return button;
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
        label.supportRichText = true;
        return label;
    }

    static void Bind(SerializedObject _target, string _field, Object _value)
    {
        _target.FindProperty(_field).objectReferenceValue = _value;
    }

    static T Component<T>(RectTransform _rect) where T : UnityEngine.Component
    {
        return _rect.TryGetComponent<T>(out var component) ? component : _rect.gameObject.AddComponent<T>();
    }

    static RectTransform Rect(Transform _parent, string _name, Vector2 _position, Vector2 _size)
    {
        var rect = _parent.Find(_name) as RectTransform;
        if (!rect)
        {
            rect = new GameObject(_name, typeof(RectTransform)).transform as RectTransform;
            rect.SetParent(_parent, false);
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = _position;
        rect.sizeDelta = _size;
        return rect;
    }

    static void Stretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = _rect.offsetMax = Vector2.zero;
    }
}
