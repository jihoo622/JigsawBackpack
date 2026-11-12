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
            PocArtSetupTool.ApplyTravel(root);
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
        canvas.sortingOrder = BasicData.Int("editor.MapTravelSetupTool.canvassortingOrder");
        Component<GraphicRaycaster>(host);
        var surface = Rect(host, "TravelOverlay", Vector2.zero, Vector2.zero);
        Stretch(surface);
        var dim = Component<Image>(surface);
        dim.color = Presentation.PuzzleTravelDim;
        dim.raycastTarget = true;

        var confirm = Card(surface, "Confirmation", new Vector2(BasicData.Float("editor.MapTravelSetupTool.confirmationX"), BasicData.Float("editor.MapTravelSetupTool.confirmationY")));
        var question = Label(confirm, "Question", font, Presentation.PuzzleTravelQuestion, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.questionY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.questionWidth"), BasicData.Float("editor.MapTravelSetupTool.questionHeight")), BasicData.Int("editor.MapTravelSetupTool.questionFontSize"));
        var quote = Label(confirm, "Quote", font, string.Empty, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.quoteY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.quoteWidth"), BasicData.Float("editor.MapTravelSetupTool.quoteHeight")), BasicData.Int("editor.MapTravelSetupTool.quoteFontSize"));
        var yes = Button(confirm, template, "ConfirmTravel", Presentation.PuzzleTravelConfirm, new Vector2(-BasicData.Float("editor.MapTravelSetupTool.confirmTravelX"), -BasicData.Float("editor.MapTravelSetupTool.confirmTravelY")));
        var no = Button(confirm, template, "CancelTravel", Presentation.PuzzleTravelCancel, new Vector2(BasicData.Float("editor.MapTravelSetupTool.cancelTravelX"), -BasicData.Float("editor.MapTravelSetupTool.cancelTravelY")));
        yes.GetComponent<Image>().color = Presentation.PuzzleMapCurrent;
        yes.GetComponentInChildren<Text>(true).color = Presentation.PuzzleBackground;
        yes.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = no };
        no.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yes };
        var journey = Card(surface, "Journey", new Vector2(BasicData.Float("editor.MapTravelSetupTool.journeyX"), BasicData.Float("editor.MapTravelSetupTool.journeyY")));
        var title = Label(journey, "JourneyTitle", font, Presentation.PuzzleTravelInProgress, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.journeyTitleY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.journeyTitleWidth"), BasicData.Float("editor.MapTravelSetupTool.journeyTitleHeight")), BasicData.Int("editor.MapTravelSetupTool.journeyTitleFontSize"));
        var route = Label(journey, "JourneyRoute", font, string.Empty, new Vector2(0f, BasicData.Float("editor.MapTravelSetupTool.journeyRouteY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.journeyRouteWidth"), BasicData.Float("editor.MapTravelSetupTool.journeyRouteHeight")), BasicData.Int("editor.MapTravelSetupTool.journeyRouteFontSize"));
        var track = Rect(journey, "Track", new Vector2(0f, -BasicData.Float("editor.MapTravelSetupTool.trackY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.trackWidth"), BasicData.Float("editor.MapTravelSetupTool.trackHeight")));
        Component<Image>(track).color = Presentation.PuzzleMapUnknown;
        for (int i = 0; i < BasicData.Int("editor.MapTravelSetupTool.sleeperCount"); i++)
        {
            var sleeper = Rect(journey, "Sleeper_" + i, new Vector2(-BasicData.Float("editor.MapTravelSetupTool.sleeperX") + i * BasicData.Float("editor.MapTravelSetupTool.sleeperX2"), -BasicData.Float("editor.MapTravelSetupTool.sleeperY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.sleeperWidth"), BasicData.Float("editor.MapTravelSetupTool.sleeperHeight")));
            var graphic = Component<Image>(sleeper);
            graphic.color = Presentation.PuzzleMapUnknown;
            graphic.raycastTarget = false;
        }
        var start = Rect(journey, "Departure", new Vector2(-BasicData.Float("editor.MapTravelSetupTool.departureX"), BasicData.Float("editor.MapTravelSetupTool.departureY")), Vector2.one);
        var end = Rect(journey, "Destination", new Vector2(BasicData.Float("editor.MapTravelSetupTool.destinationX"), BasicData.Float("editor.MapTravelSetupTool.destinationY")), Vector2.one);
        var train = Rect(journey, "Train", start.anchoredPosition, new Vector2(BasicData.Float("editor.MapTravelSetupTool.trainX"), BasicData.Float("editor.MapTravelSetupTool.trainY")));
        var body = Rect(train, "Body", new Vector2(-BasicData.Float("editor.MapTravelSetupTool.bodyX"), 0f), new Vector2(BasicData.Float("editor.MapTravelSetupTool.bodyWidth"), BasicData.Float("editor.MapTravelSetupTool.bodyHeight")));
        Component<Image>(body).color = Presentation.PuzzleMapCurrent;
        var cab = Rect(train, "Cab", new Vector2(BasicData.Float("editor.MapTravelSetupTool.cabX"), BasicData.Float("editor.MapTravelSetupTool.cabY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.cabWidth"), BasicData.Float("editor.MapTravelSetupTool.cabHeight")));
        Component<Image>(cab).color = Presentation.PuzzleMapCurrent;
        var window = Rect(train, "Window", new Vector2(BasicData.Float("editor.MapTravelSetupTool.windowX"), BasicData.Float("editor.MapTravelSetupTool.windowY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.windowWidth"), BasicData.Float("editor.MapTravelSetupTool.windowHeight")));
        Component<Image>(window).color = Presentation.PuzzleTravelCard;
        var wheelSprite = _root.GetComponentsInChildren<Image>(true).First(_image => _image.name == "Marker").sprite;
        for (int i = 0; i < BasicData.Int("editor.MapTravelSetupTool.wheelCount"); i++)
        {
            var wheel = Rect(train, "Wheel_" + i, new Vector2(-BasicData.Float("editor.MapTravelSetupTool.wheelX") + i * BasicData.Float("editor.MapTravelSetupTool.wheelX2"), -BasicData.Float("editor.MapTravelSetupTool.wheelY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.wheelWidth"), BasicData.Float("editor.MapTravelSetupTool.wheelHeight")));
            var graphic = Component<Image>(wheel);
            graphic.sprite = wheelSprite;
            graphic.color = Presentation.PuzzleHeading;
        }
        var progressBack = Rect(journey, "ProgressTrack", new Vector2(0f, -BasicData.Float("editor.MapTravelSetupTool.progressTrackY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.progressTrackWidth"), BasicData.Float("editor.MapTravelSetupTool.progressTrackHeight")));
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
        var percent = Label(journey, "Percent", font, Presentation.PuzzleTravelProgress(0f), new Vector2(0f, -BasicData.Float("editor.MapTravelSetupTool.percentY")), new Vector2(BasicData.Float("editor.MapTravelSetupTool.percentWidth"), BasicData.Float("editor.MapTravelSetupTool.percentHeight")), BasicData.Int("editor.MapTravelSetupTool.percentFontSize"));
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
        rect.sizeDelta = new Vector2(BasicData.Float("editor.MapTravelSetupTool.rectsizeDeltaWidth"), BasicData.Float("editor.MapTravelSetupTool.rectsizeDeltaHeight"));
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
