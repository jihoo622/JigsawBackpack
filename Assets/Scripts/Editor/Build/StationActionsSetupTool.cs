using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StationActionsSetupTool
{
    const string SCENE_PATH = "Assets/Scenes/Game.unity";

    [MenuItem("Tools/MyJG/Apply Station Actions UI")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            return;
        }
        var scene = SceneManager.GetSceneByPath(SCENE_PATH);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        }
        try
        {
            ApplyTo(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static void ApplyTo(Scene _scene)
    {
        var inventory = _scene.GetRootGameObjects().SelectMany(_root => _root.GetComponentsInChildren<PuzzleInventoryPresenter>(true)).Single();
        var inventoryData = new SerializedObject(inventory);
        var market = (PuzzleInventoryView)inventoryData.FindProperty("marketView").objectReferenceValue;
        var city = (PuzzleCityView)inventoryData.FindProperty("cityView").objectReferenceValue;
        var trainBar = (PuzzleTrainBarView)inventoryData.FindProperty("trainBarView").objectReferenceValue;
        var cityData = new SerializedObject(city);
        var template = (Button)cityData.FindProperty("clearButton").objectReferenceValue;
        var wait = (Button)cityData.FindProperty("waitButton").objectReferenceValue;
        var font = template.GetComponentInChildren<TMP_Text>(true).font;
        var canvas = inventory.GetComponentInParent<Canvas>();
        var existing = _scene.GetRootGameObjects().SelectMany(_root => _root.GetComponentsInChildren<PuzzleStationActionsView>(true)).SingleOrDefault();
        var host = existing ? (RectTransform)existing.transform : Rect(canvas.transform, "StationActionsPanel", new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number1"), 0f), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number2"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number3")));
        var surface = Rect(host, "StationSurface", Vector2.zero, new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number4"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number5")));
        Component<Image>(surface).color = Presentation.PuzzleTravelCard;
        var title = Label(surface, "StationTitle", font, Presentation.StationActionsTitle, new Vector2(-BasicData.Float("editor.StationActionsSetupTool.Vector2.number6"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number7")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number8"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number9")), BasicData.Float("editor.StationActionsSetupTool.Vector2.number10"));
        var back = Button(surface, template, "StationBack", Presentation.StationBack, new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number11"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number12")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number13"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number14")));
        back.interactable = true;
        var list = Rect(surface, "ActionsList", new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number15")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number16"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number17")));
        var marketButton = Button(list, template, "MarketAction", Presentation.StationMarket, new Vector2(0f, BasicData.Float("editor.StationActionsSetupTool.Vector2.number18")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number19"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number20")));
        var tavernButton = Button(list, template, "TavernAction", Presentation.StationTavern, new Vector2(0f, BasicData.Float("editor.StationActionsSetupTool.Vector2.number21")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number22"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number23")));
        tavernButton.interactable = true;
        ApplyActionIcon(marketButton, "Assets/Art/TownIcons/Market.png");
        ApplyActionIcon(tavernButton, "Assets/Art/TownIcons/Tavern.png");
        var restButton = Button(list, template, "RestAction", Presentation.StationRest, new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number24")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number25"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number26")));
        var travelButton = Button(list, template, "TravelAction", Presentation.StationTravel, new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number27")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number28"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number29")));
        var tavern = Rect(surface, "TavernContent", new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number30")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number31"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number32")));
        ApplyTavern(_scene, cityData, tavern);
        var rest = Rect(surface, "RestContent", new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number33")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number34"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number35")));
        var restDuration = Button(rest, template, "RestDuration", Presentation.StationRestDuration(TableManager.Instance.PuzzleTime.restMinutes), new Vector2(0f, BasicData.Float("editor.StationActionsSetupTool.Vector2.number36")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number37"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number38")));
        wait.transform.SetParent(rest, false);
        var waitRect = (RectTransform)wait.transform;
        waitRect.anchorMin = waitRect.anchorMax = waitRect.pivot = Vector2.one * 0.5f;
        waitRect.anchoredPosition = new Vector2(0f, BasicData.Float("editor.StationActionsSetupTool.Vector2.number39"));
        waitRect.sizeDelta = new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number40"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number41"));
        var hint = Label(rest, "RestHint", font, Presentation.StationRestHint, new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number42")), new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number43"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number44")), BasicData.Float("editor.StationActionsSetupTool.Vector2.number45"));
        var butcherView = (PuzzleInventoryView)inventoryData.FindProperty("butcheryView").objectReferenceValue;
        if (!butcherView)
        {
            var clone = Object.Instantiate(market.gameObject, surface);
            clone.name = "Butchery";
            butcherView = clone.GetComponent<PuzzleInventoryView>();
        }
        butcherView.gameObject.name = "Butchery";
        butcherView.transform.SetParent(surface, false);
        butcherView.transform.localScale = Vector3.one;
        var butcherButton = Button(list, template, "ButcheryAction", Presentation.StationButchery, Vector2.zero, new Vector2(0f, BasicData.Float("ui.layout.actionHeight")));
        var butcherConfirm = Button(butcherView.transform, template, "ButcheryConfirm", Presentation.StationButcheryConfirm, new Vector2(0f, -BasicData.Float("ui.layout.butcheryButtonBottom")), new Vector2(0f, BasicData.Float("ui.layout.butcheryButtonHeight")));
        var butcherHint = Label(butcherButton.transform, "ActionDescription", font, ButcheryDescription(), Vector2.zero, new Vector2(0f, BasicData.Float("ui.layout.smallHeight")), BasicData.Float("ui.layout.smallFont"));
        market.transform.SetParent(surface, false);
        ((RectTransform)market.transform).anchoredPosition = new Vector2(0f, -BasicData.Float("editor.StationActionsSetupTool.Vector2.number46"));
        market.transform.localScale = Vector3.one;
        var marketData = new SerializedObject(market);
        var board = (Image)marketData.FindProperty("board").objectReferenceValue;
        board.rectTransform.anchorMin = board.rectTransform.anchorMax = board.rectTransform.pivot = new Vector2(0.5f, 1f);
        board.rectTransform.anchoredPosition = new Vector2(0f, BasicData.Float("station.market.boardY"));
        board.rectTransform.sizeDelta = new Vector2(BasicData.Float("station.market.boardWidth"), BasicData.Float("station.market.boardHeight"));
        ((TMP_Text)marketData.FindProperty("boardTitle").objectReferenceValue).gameObject.SetActive(false);
        var value = (TMP_Text)marketData.FindProperty("boardValue").objectReferenceValue;
        value.rectTransform.anchoredPosition = new Vector2(0f, BasicData.Float("station.market.valueY"));
        value.rectTransform.sizeDelta = new Vector2(BasicData.Float("station.market.valueWidth"), BasicData.Float("station.market.valueHeight"));
        value.fontSize = BasicData.Float("station.market.valueFontSize");
        var guide = (TMP_Text)marketData.FindProperty("moveGuide").objectReferenceValue;
        guide.rectTransform.anchoredPosition = new Vector2(0f, BasicData.Float("station.market.guideY"));
        guide.rectTransform.sizeDelta = new Vector2(BasicData.Float("station.market.guideWidth"), BasicData.Float("station.market.guideHeight"));
        guide.fontSize = BasicData.Float("station.market.guideFontSize");
        ((TMP_Text)marketData.FindProperty("itemDetails").objectReferenceValue).gameObject.SetActive(false);
        var bar = (RectTransform)trainBar.transform;
        bar.anchoredPosition = new Vector2(-BasicData.Float("editor.StationActionsSetupTool.Vector2.number48"), -BasicData.Float("editor.StationActionsSetupTool.Vector2.number49"));
        bar.sizeDelta = new Vector2(BasicData.Float("editor.StationActionsSetupTool.Vector2.number50"), BasicData.Float("editor.StationActionsSetupTool.Vector2.number51"));

        var view = Component<PuzzleStationActionsView>(host);
        var data = new SerializedObject(view);
        Bind(data, "interaction", Component<CanvasGroup>(surface));
        data.FindProperty("startOpen").boolValue = true;
        Bind(data, "root", surface.gameObject);
        Bind(data, "actionsList", list.gameObject);
        Bind(data, "marketContent", market.gameObject);
        Bind(data, "restContent", rest.gameObject);
        Bind(data, "tavernContent", tavern);
        Bind(data, "title", title);
        Bind(data, "marketButton", marketButton);
        Bind(data, "tavernButton", tavernButton);
        Bind(data, "restButton", restButton);
        Bind(data, "travelButton", travelButton);
        Bind(data, "backButton", back);
        Bind(data, "restDurationButton", restDuration);
        Bind(data, "marketLabel", marketButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "tavernLabel", tavernButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "restLabel", restButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "travelLabel", travelButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "backLabel", back.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "restDurationLabel", restDuration.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "restHint", hint);
        Bind(data, "butcheryContent", butcherView.gameObject);
        Bind(data, "butcheryButton", butcherButton);
        Bind(data, "butcheryConfirmButton", butcherConfirm);
        Bind(data, "butcheryLabel", butcherButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "butcheryConfirmLabel", butcherConfirm.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "butcheryHint", butcherHint);
        data.ApplyModifiedPropertiesWithoutUndo();
        Bind(inventoryData, "butcheryView", butcherView);
        inventoryData.ApplyModifiedPropertiesWithoutUndo();
        var presenter = Component<PuzzleStationActionsPresenter>(host);
        var presenterData = new SerializedObject(presenter);
        Bind(presenterData, "view", view);
        presenterData.ApplyModifiedPropertiesWithoutUndo();
        list.gameObject.SetActive(true);
        market.gameObject.SetActive(false);
        rest.gameObject.SetActive(false);
        tavern.gameObject.SetActive(false);
        butcherView.gameObject.SetActive(false);
        butcherButton.gameObject.SetActive(false);
        back.gameObject.SetActive(false);
        PuzzleUiLayoutTool.ApplyTo(_scene);
    }

    static string ButcheryDescription()
    {
        var action = TableManager.Instance.CityActions.FirstOrDefault(_entry => _entry.type == "Butchery");
        return action != null ? action.description : string.Empty;
    }

    static void ApplyActionIcon(Button _button, string _spritePath)
    {
        var icon = Component<Image>(Rect(_button.transform, "ActionIcon",
            new Vector2(BasicData.Float("station.action.iconX"), 0f), Vector2.one * BasicData.Float("station.action.iconSize")));
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_spritePath);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = Presentation.PuzzleUnmodifiedTint;
    }

    static void ApplyTavern(Scene _scene, SerializedObject _city, RectTransform _content)
    {
        var talk = (Button)_city.FindProperty("talkButton").objectReferenceValue;
        var title = (TMP_Text)_city.FindProperty("tavernTitle").objectReferenceValue;
        var rumor = (TMP_Text)_city.FindProperty("rumorLabel").objectReferenceValue;
        var page = rumor.GetComponentInParent<CanvasGroup>(true);
        page.transform.SetParent(_content, false);
        var rect = (RectTransform)page.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        page.alpha = 1f;
        page.interactable = page.blocksRaycasts = true;
        page.GetComponent<Image>().color = Presentation.PuzzleTravelCard;
        foreach (Transform child in page.transform)
        {
            child.gameObject.SetActive(child == title.transform || child == rumor.transform || child == talk.transform);
        }
        PlaceTavernControl(title.rectTransform, "title");
        PlaceTavernControl(rumor.rectTransform, "rumor");
        PlaceTavernControl((RectTransform)talk.transform, "talk");
        title.fontSize = BasicData.Float("station.tavern.title.fontSize");
        rumor.fontSize = BasicData.Float("station.tavern.rumor.fontSize");
        title.color = rumor.color = Presentation.PuzzleHeading;
        rumor.enableAutoSizing = false;
        title.text = Presentation.TavernTitle(TableManager.Instance.Cities.Single(_city => _city.id == TableManager.Instance.Puzzle.startCity).displayName);
        rumor.text = Presentation.TavernUnreadRumor;
        talk.GetComponentInChildren<TMP_Text>(true).text = Presentation.TavernTalk;
        page.gameObject.SetActive(true);
        foreach (var transform in _scene.GetRootGameObjects().SelectMany(_root => _root.GetComponentsInChildren<Transform>(true)))
        {
            if (transform.name == "Town Main" || transform.name == "Open Town")
            {
                transform.gameObject.SetActive(false);
            }
        }
    }

    static void PlaceTavernControl(RectTransform _rect, string _key)
    {
        _rect.anchorMin = _rect.anchorMax = _rect.pivot = Vector2.one * 0.5f;
        _rect.anchoredPosition = new Vector2(0f, BasicData.Float("station.tavern." + _key + ".y"));
        _rect.sizeDelta = new Vector2(BasicData.Float("station.tavern." + _key + ".width"), BasicData.Float("station.tavern." + _key + ".height"));
    }

    static void Bind(SerializedObject _data, string _field, Object _value)
    {
        _data.FindProperty(_field).objectReferenceValue = _value;
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
            rect = _parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(_child => _child.name == _name);
        }
        if (!rect)
        {
            rect = new GameObject(_name, typeof(RectTransform)).transform as RectTransform;
            rect.SetParent(_parent, false);
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = _position;
        rect.sizeDelta = _size;
        return rect;
    }

    static Button Button(Transform _parent, Button _template, string _name, string _text, Vector2 _position, Vector2 _size)
    {
        var existing = _parent.Find(_name);
        var button = existing ? existing.GetComponent<Button>() : Object.Instantiate(_template, _parent);
        button.name = _name;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = _position;
        rect.sizeDelta = _size;
        button.onClick = new Button.ButtonClickedEvent();
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.text = _text;
        label.fontSize = BasicData.Float("editor.StationActionsSetupTool.Button.number1");
        return button;
    }

    static TextMeshProUGUI Label(Transform _parent, string _name, TMP_FontAsset _font, string _text, Vector2 _position, Vector2 _size, float _fontSize)
    {
        var label = Component<TextMeshProUGUI>(Rect(_parent, _name, _position, _size));
        label.font = _font;
        label.text = _text;
        label.fontSize = _fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Presentation.PuzzleHeading;
        label.raycastTarget = false;
        return label;
    }
}
