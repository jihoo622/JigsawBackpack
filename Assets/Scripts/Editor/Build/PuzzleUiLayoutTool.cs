using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared authoring pass: all runtime objects and layout components are saved in the scene.
public static class PuzzleUiLayoutTool
{
    const string SCENE_PATH = "Assets/Scenes/Game.unity";

    static float N(string _key)
    {
        return BasicData.Float("ui.layout." + _key);
    }

    static Color C(string _key)
    {
        return Presentation.SharedColor("ui.layout." + _key);
    }

    [MenuItem("Tools/MyJG/Apply UI Overhaul")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("UI 배치 적용은 플레이 종료 후 실행하세요.");
            return;
        }
        TableManager.ReloadAfterTableEdit();
        TableManager.Instance.RequireValid();
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

    public static void ApplyTo(Scene _scene)
    {
        var inventory = All<PuzzleInventoryPresenter>(_scene).Single();
        var fields = new SerializedObject(inventory);
        var station = All<PuzzleStationActionsView>(_scene).SingleOrDefault();
        if (!station)
        {
            return;
        }
        var canvas = inventory.GetComponentInParent<Canvas>();
        var city = (PuzzleCityView)fields.FindProperty("cityView").objectReferenceValue;
        var cityFields = new SerializedObject(city);
        var train = (PuzzleInventoryView)fields.FindProperty("trainView").objectReferenceValue;
        var market = (PuzzleInventoryView)fields.FindProperty("marketView").objectReferenceValue;
        var bar = (PuzzleTrainBarView)fields.FindProperty("trainBarView").objectReferenceValue;
        var stationFields = new SerializedObject(station);
        var font = ((TMP_Text)cityFields.FindProperty("cityName").objectReferenceValue).font;

        foreach (var scaler in All<CanvasScaler>(_scene))
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(N("referenceWidth"), N("referenceHeight"));
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
        var background = Rect(canvas.transform, "GameBackdrop");
        Fill(background, 0f, 0f, 0f, 0f);
        Image(background, "background");
        background.SetAsFirstSibling();

        var body = Rect(canvas.transform, "GameplayLayout");
        Fill(body, N("edge"), N("bodyTop"), N("edge"), N("bodyBottom"));
        var columns = Add<HorizontalLayoutGroup>(body);
        Configure(columns, N("gap"), 0);
        var left = Rect(body, "TrainColumn");
        var right = Rect(body, "StationColumn");
        Flex(left);
        Flex(right);
        var trainColumn = Add<VerticalLayoutGroup>(left);
        Configure(trainColumn, N("gap"), 0);
        var trainRect = (RectTransform)train.transform;
        trainRect.SetParent(left, false);
        Flex(trainRect);
        Image(trainRect, "surface");
        var barRect = (RectTransform)bar.transform;
        barRect.SetParent(left, false);
        Flex(barRect, N("wagonBarHeight"));
        Image(barRect, "surface");
        foreach (var label in bar.GetComponentsInChildren<TMP_Text>(true))
        {
            Fill(label.rectTransform, N("buttonVerticalPadding"), N("buttonVerticalPadding"), N("buttonVerticalPadding"), N("buttonVerticalPadding"));
            Text(label, N("buttonFont"), TextAlignmentOptions.Center);
        }

        var stationRect = (RectTransform)station.transform;
        stationRect.SetParent(right, false);
        Fill(stationRect, 0f, 0f, 0f, 0f);
        var surface = (RectTransform)((GameObject)stationFields.FindProperty("root").objectReferenceValue).transform;
        Fill(surface, 0f, 0f, 0f, 0f);
        Image(surface, "surface");
        Rule(surface);
        var title = (TMP_Text)stationFields.FindProperty("title").objectReferenceValue;
        Top(title.rectTransform, N("padding"), N("headerHeight") * 0.5f, N("stationTitleWidth"), N("titleHeight"), false);
        Text(title, N("titleFont"), TextAlignmentOptions.MidlineLeft);
        var back = (Button)stationFields.FindProperty("backButton").objectReferenceValue;
        back.interactable = true;
        Top((RectTransform)back.transform, N("padding"), N("headerHeight") * 0.5f, N("backWidth"), N("buttonHeight"), true);
        Button(back);
        var content = Rect(surface, "StationBody");
        Fill(content, N("padding"), N("headerHeight") + N("gap"), N("padding"), N("padding"));
        foreach (string field in new[] { "actionsList", "marketContent", "restContent", "tavernContent" })
        {
            var target = stationFields.FindProperty(field).objectReferenceValue;
            var rect = target is GameObject gameObject ? (RectTransform)gameObject.transform : (RectTransform)target;
            rect.SetParent(content, false);
            Fill(rect, 0f, 0f, 0f, 0f);
        }
        ApplyTrain(train);
        ApplyMarket(market);
        ApplyActions(stationFields, font);
        ApplyRest(stationFields, cityFields);
        ApplyTavern(cityFields);
        ApplyHeader((RectTransform)canvas.transform, city, cityFields);
        ApplyFooter((RectTransform)canvas.transform, cityFields);
        ApplyTooltip((PuzzleItemTooltipView)fields.FindProperty("tooltipView").objectReferenceValue);
        ApplyTravel(_scene);
        ApplyLayers(_scene);
        ((GameObject)stationFields.FindProperty("actionsList").objectReferenceValue).SetActive(true);
        ((GameObject)stationFields.FindProperty("marketContent").objectReferenceValue).SetActive(false);
        ((GameObject)stationFields.FindProperty("restContent").objectReferenceValue).SetActive(false);
        ((RectTransform)stationFields.FindProperty("tavernContent").objectReferenceValue).gameObject.SetActive(false);
        back.gameObject.SetActive(false);
        title.text = Presentation.StationActionsTitle;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(body);
        foreach (var component in All<Component>(_scene))
        {
            if (component && PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        EditorUtility.SetDirty(canvas);
    }

    static void ApplyTrain(PuzzleInventoryView _view)
    {
        var fields = new SerializedObject(_view);
        var title = (TMP_Text)fields.FindProperty("boardTitle").objectReferenceValue;
        title.gameObject.SetActive(true);
        StretchTop(title.rectTransform, N("padding"), N("titleY"), N("titleHeight"));
        Text(title, N("titleFont"), TextAlignmentOptions.MidlineLeft);
        Rule((RectTransform)_view.transform);
        var board = ((UnityEngine.UI.Image)fields.FindProperty("board").objectReferenceValue).rectTransform;
        TopCenter(board, N("trainBoardTop"), N("trainBoardSize"), N("trainBoardSize"), true);
        var value = (TMP_Text)fields.FindProperty("boardValue").objectReferenceValue;
        Bottom(value.rectTransform, N("trainValueY"), N("infoHeight"));
        Text(value, N("bodyFont"), TextAlignmentOptions.Center);
        var guide = (TMP_Text)fields.FindProperty("moveGuide").objectReferenceValue;
        Bottom(guide.rectTransform, N("trainGuideY"), N("smallHeight"));
        Text(guide, N("smallFont"), TextAlignmentOptions.Center, true);
        ((TMP_Text)fields.FindProperty("itemDetails").objectReferenceValue).gameObject.SetActive(false);
    }

    static void ApplyMarket(PuzzleInventoryView _view)
    {
        var fields = new SerializedObject(_view);
        var value = (TMP_Text)fields.FindProperty("boardValue").objectReferenceValue;
        StretchTop(value.rectTransform, 0f, N("marketInfoY"), N("marketInfoHeight"));
        Text(value, N("bodyFont"), TextAlignmentOptions.MidlineLeft);
        var board = ((UnityEngine.UI.Image)fields.FindProperty("board").objectReferenceValue).rectTransform;
        TopCenter(board, N("marketBoardTop"), N("marketBoardWidth"), N("marketBoardHeight"), true);
        var guide = (TMP_Text)fields.FindProperty("moveGuide").objectReferenceValue;
        Bottom(guide.rectTransform, N("marketGuideY"), N("smallHeight"));
        Text(guide, N("smallFont"), TextAlignmentOptions.Center, true);
    }

    static void ApplyActions(SerializedObject _fields, TMP_FontAsset _font)
    {
        var list = (GameObject)_fields.FindProperty("actionsList").objectReferenceValue;
        var layout = Add<VerticalLayoutGroup>((RectTransform)list.transform);
        Configure(layout, N("actionGap"), 0);
        foreach (string action in new[] { "market", "tavern", "rest", "travel" })
        {
            var button = (Button)_fields.FindProperty(action + "Button").objectReferenceValue;
            Flex((RectTransform)button.transform, N("actionHeight"));
            Button(button);
            var label = (TMP_Text)_fields.FindProperty(action + "Label").objectReferenceValue;
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.offsetMin = new Vector2(N("actionTextLeft"), -N("actionTitleBottom"));
            label.rectTransform.offsetMax = new Vector2(-N("padding"), -N("actionTextTop"));
            Text(label, N("actionFont"), TextAlignmentOptions.MidlineLeft);
            var description = Add<TextMeshProUGUI>(Rect(button.transform, "ActionDescription"));
            description.font = _font;
            description.text = Presentation.LayoutText(action + "Description");
            description.rectTransform.anchorMin = Vector2.zero;
            description.rectTransform.anchorMax = Vector2.one;
            description.rectTransform.offsetMin = new Vector2(N("actionTextLeft"), N("actionDescriptionBottom"));
            description.rectTransform.offsetMax = new Vector2(-N("padding"), -N("actionDescriptionTop"));
            Text(description, N("smallFont"), TextAlignmentOptions.MidlineLeft, true);
            var icon = button.transform.Find("ActionIcon") as RectTransform;
            if (icon)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = Vector2.one * 0.5f;
                icon.anchoredPosition = new Vector2(N("actionIconX"), 0f);
                icon.sizeDelta = Vector2.one * N("actionIconSize");
            }
            else
            {
                var badge = Add<TextMeshProUGUI>(Rect(button.transform, "ActionNumber"));
                badge.font = _font;
                badge.text = Presentation.LayoutText(action + "Badge");
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                badge.rectTransform.anchoredPosition = new Vector2(N("actionIconX"), 0f);
                badge.rectTransform.sizeDelta = Vector2.one * N("actionIconSize");
                Text(badge, N("actionFont"), TextAlignmentOptions.Center);
                badge.color = C("accent");
            }
        }
    }

    static void ApplyRest(SerializedObject _station, SerializedObject _city)
    {
        var root = (GameObject)_station.FindProperty("restContent").objectReferenceValue;
        var layout = Add<VerticalLayoutGroup>((RectTransform)root.transform);
        Configure(layout, N("gap"), (int)N("padding"));
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            Flex((RectTransform)button.transform, N("restButtonHeight"));
            Button(button);
        }
        var hint = (TMP_Text)_station.FindProperty("restHint").objectReferenceValue;
        Flex(hint.rectTransform, N("restHintHeight"));
        Text(hint, N("bodyFont"), TextAlignmentOptions.TopLeft, true);
        hint.transform.SetAsLastSibling();
    }

    static void ApplyTavern(SerializedObject _city)
    {
        var title = (TMP_Text)_city.FindProperty("tavernTitle").objectReferenceValue;
        var rumor = (TMP_Text)_city.FindProperty("rumorLabel").objectReferenceValue;
        var talk = (Button)_city.FindProperty("talkButton").objectReferenceValue;
        var layout = Add<VerticalLayoutGroup>((RectTransform)title.transform.parent);
        Configure(layout, N("gap"), (int)N("padding"));
        title.transform.SetAsFirstSibling();
        Flex(title.rectTransform, N("tavernTitleHeight"));
        Text(title, N("titleFont"), TextAlignmentOptions.MidlineLeft);
        Flex(rumor.rectTransform);
        Text(rumor, N("rumorFont"), TextAlignmentOptions.TopLeft);
        talk.transform.SetAsLastSibling();
        Flex((RectTransform)talk.transform, N("restButtonHeight"));
        Button(talk);
    }

    static void ApplyHeader(RectTransform _canvas, PuzzleCityView _city, SerializedObject _fields)
    {
        var header = (RectTransform)_city.transform;
        header.SetParent(_canvas, false);
        Header(header, N("top"), N("topHeight"));
        Image(header, "surface");
        var layout = Add<HorizontalLayoutGroup>(header);
        Configure(layout, N("gap"), (int)N("padding"));
        var city = (TMP_Text)_fields.FindProperty("cityName").objectReferenceValue;
        var time = (TMP_Text)_fields.FindProperty("timeLabel").objectReferenceValue;
        var funds = (TMP_Text)_fields.FindProperty("funds").objectReferenceValue;
        var clear = (Button)_fields.FindProperty("clearButton").objectReferenceValue;
        foreach (var child in new[] { city.transform, time.transform, funds.transform, clear.transform })
        {
            child.SetParent(header, false);
            child.SetAsLastSibling();
        }
        Flex(city.rectTransform);
        FixedWidth(time.rectTransform, N("timeWidth"));
        FixedWidth(funds.rectTransform, N("fundsWidth"));
        FixedWidth((RectTransform)clear.transform, N("clearWidth"));
        Text(city, N("titleFont"), TextAlignmentOptions.MidlineLeft);
        Text(time, N("bodyFont"), TextAlignmentOptions.MidlineLeft);
        Text(funds, N("titleFont"), TextAlignmentOptions.MidlineRight);
        funds.color = C("accent");
        Button(clear);
        var mapSpace = Rect(header, "MapButtonSpace");
        mapSpace.SetAsLastSibling();
        FixedWidth(mapSpace, N("mapWidth"));
    }

    static void ApplyFooter(RectTransform _canvas, SerializedObject _fields)
    {
        var footer = Rect(_canvas, "JourneyFooter");
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.anchoredPosition = new Vector2(0f, N("bottom"));
        footer.sizeDelta = new Vector2(-N("edge") * 2f, N("footerHeight"));
        Image(footer, "surface");
        var layout = Add<HorizontalLayoutGroup>(footer);
        Configure(layout, N("gap"), (int)N("footerPadding"));
        foreach (var button in _canvas.GetComponentsInChildren<Button>(true))
        {
            if (button.name == "PreviewDestination")
            {
                Object.DestroyImmediate(button.gameObject);
            }
        }
        var status = (TMP_Text)_fields.FindProperty("tradeStatus").objectReferenceValue;
        status.transform.SetParent(footer, false);
        Flex(status.rectTransform);
        Text(status, N("bodyFont"), TextAlignmentOptions.MidlineLeft);
    }

    static void ApplyTooltip(PuzzleItemTooltipView _tooltip)
    {
        if (!_tooltip)
        {
            return;
        }
        var rect = (RectTransform)_tooltip.transform;
        var data = new SerializedObject(_tooltip);
        var parent = (Canvas)data.FindProperty("canvas").objectReferenceValue;
        rect.SetParent(parent.transform, false);
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(N("tooltipWidth"), rect.sizeDelta.y);
        foreach (var fitter in _tooltip.GetComponentsInChildren<ContentSizeFitter>(true))
        {
            if (!fitter.name.StartsWith("Badge_"))
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }
        var canvas = Add<Canvas>(rect);
        canvas.overrideSorting = true;
        canvas.sortingOrder = (int)N("tooltipOrder");
    }

    static void ApplyLayers(Scene _scene)
    {
        foreach (var canvas in All<Canvas>(_scene))
        {
            if (canvas.name == "Pause Menu Canvas")
            {
                canvas.sortingOrder = (int)N("pauseOrder");
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    Button(button);
                }
            }
            else if (canvas.name == Presentation.PUZZLE_MAP_PANEL_NAME)
            {
                canvas.sortingOrder = (int)N("mapOrder");
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    Button(button);
                }
                var open = canvas.transform.Find("OpenMap") as RectTransform;
                open.anchorMin = open.anchorMax = Vector2.one;
                open.pivot = Vector2.one * 0.5f;
                open.anchoredPosition = new Vector2(-N("edge") - N("padding") - N("mapWidth") * 0.5f, -N("top") - N("topHeight") * 0.5f);
                open.sizeDelta = new Vector2(N("mapWidth"), N("mapButtonHeight"));
                Button(open.GetComponent<Button>());
            }
            else if (canvas.name == Presentation.PUZZLE_TRAVEL_PANEL_NAME)
            {
                canvas.sortingOrder = (int)N("travelOrder");
            }
        }
    }

    static void ApplyTravel(Scene _scene)
    {
        foreach (var view in All<PuzzleMapTravelView>(_scene))
        {
            var data = new SerializedObject(view);
            var card = ((GameObject)data.FindProperty("confirmation").objectReferenceValue).GetComponent<RectTransform>();
            card.sizeDelta = new Vector2(N("modalWidth"), N("modalHeight"));
            var question = (UnityEngine.UI.Text)data.FindProperty("question").objectReferenceValue;
            TopCenter(question.rectTransform, N("modalTitleY"), N("modalInnerWidth"), N("modalTitleHeight"), false);
            var quote = (UnityEngine.UI.Text)data.FindProperty("quote").objectReferenceValue;
            TopCenter(quote.rectTransform, N("modalQuoteY"), N("modalInnerWidth"), N("modalQuoteHeight"), false);
            quote.horizontalOverflow = HorizontalWrapMode.Wrap;
            quote.verticalOverflow = VerticalWrapMode.Truncate;
            quote.resizeTextForBestFit = true;
            quote.resizeTextMinSize = (int)N("minFont");
            quote.resizeTextMaxSize = (int)N("bodyFont");
            foreach (string field in new[] { "confirmButton", "cancelButton" })
            {
                var button = (Button)data.FindProperty(field).objectReferenceValue;
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = new Vector2((field == "confirmButton" ? -1f : 1f) * N("modalButtonX"), N("modalButtonY"));
                rect.sizeDelta = new Vector2(N("modalButtonWidth"), N("modalButtonHeight"));
            }
        }
    }

    static void Rule(RectTransform _parent)
    {
        var rule = Rect(_parent, "HeaderDivider");
        StretchTop(rule, N("padding"), N("headerHeight"), N("ruleThickness"));
        Image(rule, "border");
    }

    static void Button(Button _button)
    {
        _button.transition = Selectable.Transition.ColorTint;
        Color background = C("button.normal");
        var colors = _button.colors;
        colors.normalColor = Presentation.PuzzleUnmodifiedTint;
        colors.highlightedColor = Tint(C("button.highlight"), background);
        colors.pressedColor = Tint(C("button.pressed"), background);
        colors.selectedColor = Tint(C("button.highlight"), background);
        colors.disabledColor = Tint(C("button.disabled"), background);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = N("buttonFade");
        _button.colors = colors;
        if (_button.targetGraphic)
        {
            _button.targetGraphic.color = background;
        }
        foreach (var label in _button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.name == "ActionDescription")
            {
                continue;
            }
            if (label.name == "ActionNumber")
            {
                continue;
            }
            Fill(label.rectTransform, N("buttonPadding"), N("buttonVerticalPadding"), N("buttonPadding"), N("buttonVerticalPadding"));
            Text(label, N("buttonFont"), TextAlignmentOptions.Center);
        }
        foreach (var label in _button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            Fill(label.rectTransform, N("buttonPadding"), 0f, N("buttonPadding"), 0f);
            label.fontSize = (int)N("buttonFont");
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = (int)N("minFont");
            label.resizeTextMaxSize = (int)N("buttonFont");
            label.color = C("text");
            label.raycastTarget = false;
        }
    }

    // Selectable multiplies its tint by Graphic.color. Keep the authored background visible
    // in edit mode and derive state multipliers from the same absolute table colors.
    static Color Tint(Color _state, Color _background)
    {
        return new Color(
            _background.r > 0f ? _state.r / _background.r : 1f,
            _background.g > 0f ? _state.g / _background.g : 1f,
            _background.b > 0f ? _state.b / _background.b : 1f,
            _background.a > 0f ? _state.a / _background.a : 1f);
    }

    static void Text(TMP_Text _text, float _size, TextAlignmentOptions _alignment, bool _muted = false)
    {
        _text.fontSize = _size;
        _text.enableAutoSizing = true;
        _text.fontSizeMin = N("minFont");
        _text.fontSizeMax = _size;
        _text.alignment = _alignment;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.overflowMode = TextOverflowModes.Ellipsis;
        _text.color = C(_muted ? "muted" : "text");
        _text.raycastTarget = false;
        _text.margin = Vector4.zero;
    }

    static void Configure(HorizontalOrVerticalLayoutGroup _group, float _spacing, int _padding)
    {
        _group.padding = new RectOffset(_padding, _padding, _padding, _padding);
        _group.spacing = _spacing;
        _group.childAlignment = TextAnchor.UpperLeft;
        _group.childControlWidth = _group.childControlHeight = true;
        _group.childForceExpandWidth = _group.childForceExpandHeight = false;
    }

    static void Flex(RectTransform _rect, float _height = -1f)
    {
        var layout = Add<LayoutElement>(_rect);
        layout.minWidth = layout.minHeight = 0f;
        layout.preferredWidth = -1f;
        layout.flexibleWidth = 1f;
        layout.preferredHeight = _height;
        layout.flexibleHeight = _height < 0f ? 1f : 0f;
        _rect.localScale = Vector3.one;
    }

    static void FixedWidth(RectTransform _rect, float _width)
    {
        Flex(_rect);
        var layout = _rect.GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = _width;
        layout.flexibleWidth = 0f;
    }

    static void Header(RectTransform _rect, float _top, float _height)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = Vector2.one;
        _rect.pivot = new Vector2(0.5f, 1f);
        _rect.anchoredPosition = new Vector2(0f, -_top);
        _rect.sizeDelta = new Vector2(-N("edge") * 2f, _height);
    }

    static void Top(RectTransform _rect, float _edge, float _y, float _width, float _height, bool _right)
    {
        _rect.anchorMin = _rect.anchorMax = new Vector2(_right ? 1f : 0f, 1f);
        _rect.pivot = new Vector2(_right ? 1f : 0f, 0.5f);
        _rect.anchoredPosition = new Vector2(_right ? -_edge : _edge, -_y);
        _rect.sizeDelta = new Vector2(_width, _height);
    }

    static void TopCenter(RectTransform _rect, float _top, float _width, float _height, bool _topPivot)
    {
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 1f);
        _rect.pivot = new Vector2(0.5f, _topPivot ? 1f : 0.5f);
        _rect.anchoredPosition = new Vector2(0f, -_top);
        _rect.sizeDelta = new Vector2(_width, _height);
    }

    static void StretchTop(RectTransform _rect, float _padding, float _y, float _height)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = Vector2.one;
        _rect.pivot = Vector2.one * 0.5f;
        _rect.anchoredPosition = new Vector2(0f, -_y);
        _rect.sizeDelta = new Vector2(-_padding * 2f, _height);
    }

    static void Bottom(RectTransform _rect, float _y, float _height)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = new Vector2(1f, 0f);
        _rect.pivot = Vector2.one * 0.5f;
        _rect.anchoredPosition = new Vector2(0f, _y);
        _rect.sizeDelta = new Vector2(-N("padding") * 2f, _height);
    }

    static void Fill(RectTransform _rect, float _left, float _top, float _right, float _bottom)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = new Vector2(_left, _bottom);
        _rect.offsetMax = new Vector2(-_right, -_top);
        _rect.localScale = Vector3.one;
    }

    static void Image(RectTransform _rect, string _color)
    {
        var image = Add<UnityEngine.UI.Image>(_rect);
        image.color = C(_color);
        image.raycastTarget = false;
    }

    static RectTransform Rect(Transform _parent, string _name)
    {
        var rect = _parent.Find(_name) as RectTransform;
        if (!rect)
        {
            rect = (RectTransform)new GameObject(_name, typeof(RectTransform)).transform;
            rect.SetParent(_parent, false);
        }
        return rect;
    }

    static T Add<T>(RectTransform _rect) where T : Component
    {
        return _rect.TryGetComponent<T>(out var component) ? component : _rect.gameObject.AddComponent<T>();
    }

    static T[] All<T>(Scene _scene) where T : Component
    {
        return _scene.GetRootGameObjects().SelectMany(_root => _root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
