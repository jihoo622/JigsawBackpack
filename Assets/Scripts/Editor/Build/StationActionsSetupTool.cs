using System.Collections.Generic;
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
        var cityData = new SerializedObject(city);
        var template = (Button)cityData.FindProperty("clearButton").objectReferenceValue;
        var wait = (Button)cityData.FindProperty("waitButton").objectReferenceValue;
        var font = template.GetComponentInChildren<TMP_Text>(true).font;
        var canvas = inventory.GetComponentInParent<Canvas>();
        var existing = _scene.GetRootGameObjects().SelectMany(_root => _root.GetComponentsInChildren<PuzzleStationActionsView>(true)).SingleOrDefault();
        var host = existing ? (RectTransform)existing.transform : Rect(canvas.transform, "StationActionsPanel");
        var surface = Rect(host, "StationSurface");
        Component<Image>(surface).color = Presentation.PuzzleTravelCard;
        var title = Label(surface, "StationTitle", font, Presentation.StationActionsTitle);
        var back = Button(surface, template, "StationBack", Presentation.StationBack);
        back.interactable = true;
        var list = Rect(surface, "ActionsList");
        var marketButton = Button(list, template, "MarketAction", Presentation.StationMarket);
        var tavernButton = Button(list, template, "TavernAction", Presentation.StationTavern);
        tavernButton.interactable = true;
        ApplyActionIcon(marketButton, "Assets/Art/TownIcons/Market.png");
        ApplyActionIcon(tavernButton, "Assets/Art/TownIcons/Tavern.png");
        var restButton = Button(list, template, "RestAction", Presentation.StationRest);
        var travelButton = Button(list, template, "TravelAction", Presentation.StationTravel);
        var tavern = Rect(surface, "TavernContent");
        ApplyTavern(_scene, cityData, tavern);
        var rest = Rect(surface, "RestContent");
        var restDuration = Button(rest, template, "RestDuration", Presentation.StationRestDuration(TableManager.Instance.PuzzleTime.restMinutes));
        wait.transform.SetParent(rest, false);
        var hint = Label(rest, "RestHint", font, Presentation.StationRestHint);
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
        var butcherButton = Button(list, template, "ButcheryAction", Presentation.StationButchery);
        var butcherConfirm = Button(butcherView.transform, template, "ButcheryConfirm", Presentation.StationButcheryConfirm);
        Label(butcherButton.transform, "ActionDescription", font, ButcheryDescription());
        var modificationView = (PuzzleInventoryView)inventoryData.FindProperty("modificationView").objectReferenceValue;
        if (!modificationView)
        {
            var clone = Object.Instantiate(market.gameObject, surface);
            clone.name = "Modification";
            modificationView = clone.GetComponent<PuzzleInventoryView>();
        }
        modificationView.gameObject.name = "Modification";
        modificationView.transform.SetParent(surface, false);
        modificationView.transform.localScale = Vector3.one;
        var modificationButton = Button(list, template, "ModificationAction", Presentation.StationModification);
        var modificationModeButton = Button(modificationView.transform, template, "ModificationMode", ModificationModeName());
        var modificationConfirm = Button(modificationView.transform, template, "ModificationConfirm", Presentation.StationModificationConfirm);
        Label(modificationButton.transform, "ActionDescription", font, ModificationDescription());
        market.transform.SetParent(surface, false);
        var marketData = new SerializedObject(market);
        ((TMP_Text)marketData.FindProperty("boardTitle").objectReferenceValue).gameObject.SetActive(false);
        ((TMP_Text)marketData.FindProperty("itemDetails").objectReferenceValue).gameObject.SetActive(false);
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
        Bind(data, "butcheryHint", new SerializedObject(butcherView).FindProperty("boardValue").objectReferenceValue);
        Bind(data, "modificationContent", modificationView.gameObject);
        Bind(data, "modificationButton", modificationButton);
        Bind(data, "modificationModeButton", modificationModeButton);
        Bind(data, "modificationConfirmButton", modificationConfirm);
        Bind(data, "modificationLabel", modificationButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "modificationModeLabel", modificationModeButton.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "modificationConfirmLabel", modificationConfirm.GetComponentInChildren<TextMeshProUGUI>(true));
        Bind(data, "modificationHint", new SerializedObject(modificationView).FindProperty("boardValue").objectReferenceValue);
        data.ApplyModifiedPropertiesWithoutUndo();
        Bind(inventoryData, "butcheryView", butcherView);
        Bind(inventoryData, "modificationView", modificationView);
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
        modificationView.gameObject.SetActive(false);
        modificationButton.gameObject.SetActive(false);
        back.gameObject.SetActive(false);
        PuzzleUiLayoutTool.ApplyTo(_scene);
    }

    static string ButcheryDescription()
    {
        var action = TableManager.Instance.CityActions.FirstOrDefault(_entry => _entry.type == "Butchery");
        return action != null ? action.description : string.Empty;
    }

    static string ModificationDescription()
    {
        var action = TableManager.Instance.CityActions.FirstOrDefault(_entry => _entry.type == "Modification");
        return action != null ? action.description : string.Empty;
    }

    static string ModificationModeName()
    {
        var action = TableManager.Instance.CityActions.FirstOrDefault(_entry => _entry.type == "Modification");
        var modes = action?.modes ?? System.Array.Empty<PuzzleActionModeData>();
        return modes.Length > 0 ? modes[0].displayName : string.Empty;
    }

    static void ApplyActionIcon(Button _button, string _spritePath)
    {
        var icon = Component<Image>(Rect(_button.transform, "ActionIcon"));
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

    static void Bind(SerializedObject _data, string _field, Object _value)
    {
        _data.FindProperty(_field).objectReferenceValue = _value;
    }

    static T Component<T>(RectTransform _rect) where T : UnityEngine.Component
    {
        return _rect.TryGetComponent<T>(out var component) ? component : _rect.gameObject.AddComponent<T>();
    }

    static RectTransform Rect(Transform _parent, string _name)
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
        return rect;
    }

    static Button Button(Transform _parent, Button _template, string _name, string _text)
    {
        var existing = _parent.Find(_name);
        var button = existing ? existing.GetComponent<Button>() : Object.Instantiate(_template, _parent);
        button.name = _name;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        button.onClick = new Button.ButtonClickedEvent();
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.text = _text;
        label.fontSize = BasicData.Float("ui.layout.buttonFont");
        return button;
    }

    static TextMeshProUGUI Label(Transform _parent, string _name, TMP_FontAsset _font, string _text)
    {
        var label = Component<TextMeshProUGUI>(Rect(_parent, _name));
        label.font = _font;
        label.text = _text;
        label.fontSize = BasicData.Float("ui.layout.bodyFont");
        label.alignment = TextAlignmentOptions.Center;
        label.color = Presentation.PuzzleHeading;
        label.raycastTarget = false;
        return label;
    }
}
