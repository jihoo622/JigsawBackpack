using System.Linq;
using System;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

public static class RumorLogSetupTool
{
    const string PREFAB_PATH = "Assets/Art/PuzzleTradeNotebook.prefab";
    const string MAP_PATH = "Assets/Art/PuzzleWorldMap.prefab";
    const string SCENE_PATH = "Assets/Scenes/Game.unity";
    const string HOST_NAME = "RumorLogPanel";

    [MenuItem("Tools/MyJG/Apply Rumor Log Panel")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        TableManager.Instance.RequireValid();
        var root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            Configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        var map = PrefabUtility.LoadPrefabContents(MAP_PATH);
        try
        {
            foreach (var oldView in map.GetComponentsInChildren<PuzzleTradeNotebookView>(true))
            {
                UnityEngine.Object.DestroyImmediate(oldView.gameObject);
            }
            RemoveBookButtons(map);
            PrefabUtility.SaveAsPrefabAsset(map, MAP_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(map);
        }
        var scene = SceneManager.GetSceneByPath(SCENE_PATH);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        }
        try
        {
            var presenter = scene.GetRootGameObjects().SelectMany(_root =>
                _root.GetComponentsInChildren<PuzzleInventoryPresenter>(true)).Single();
            var canvas = presenter.GetComponentInParent<Canvas>();
            var views = scene.GetRootGameObjects().SelectMany(_root =>
                _root.GetComponentsInChildren<PuzzleTradeNotebookView>(true)).ToArray();
            var existing = views.FirstOrDefault(_view => _view.name == HOST_NAME && _view.transform.parent == canvas.transform);
            foreach (var oldView in views)
            {
                if (oldView != existing)
                {
                    UnityEngine.Object.DestroyImmediate(oldView.gameObject);
                }
            }
            if (!existing)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                existing = instance.GetComponent<PuzzleTradeNotebookView>();
            }
            existing.name = HOST_NAME;
            Stretch((RectTransform)existing.transform);
            existing.gameObject.SetActive(true);
            var logCanvas = existing.GetComponent<Canvas>();
            logCanvas.overrideSorting = true;
            logCanvas.sortingOrder = (int)N("sortingOrder");
            PrefabUtility.RecordPrefabInstancePropertyModifications(logCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(existing.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(existing.gameObject);
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                RemoveBookButtons(sceneRoot);
            }
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

    static float N(string _key)
    {
        return BasicData.Float("ui.rumorLog." + _key);
    }

    static T Find<T>(GameObject _root, string _oldName, string _name) where T : Component
    {
        return _root.GetComponentsInChildren<T>(true).First(_item => _item.name == _name || _item.name == _oldName);
    }

    static void Configure(GameObject _root)
    {
        var body = Find<RectTransform>(_root, "Open Book Cover", "LogPanel");
        var title = Find<TextMeshProUGUI>(_root, "Notebook Title", "LogTitle");
        var message = Find<TextMeshProUGUI>(_root, "Rumor Message", "ConversationLog");
        var count = Find<TextMeshProUGUI>(_root, "Record Position", "LogCount");
        var collapse = Find<UnityEngine.UI.Button>(_root, "Close Notebook", "CollapseLog");
        var toggle = Find<UnityEngine.UI.Button>(_root, "Previous City", "ToggleLog");
        _root.name = HOST_NAME;
        Stretch((RectTransform)_root.transform);
        _root.GetComponent<UnityEngine.UI.Image>().enabled = false;
        var group = _root.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = group.blocksRaycasts = true;
        var canvas = Add<Canvas>(_root);
        canvas.overrideSorting = true;
        canvas.sortingOrder = (int)N("sortingOrder");
        Add<UnityEngine.UI.GraphicRaycaster>(_root);
        foreach (Transform child in body)
        {
            child.gameObject.SetActive(false);
        }
        body.name = "LogPanel";
        body.anchorMin = body.anchorMax = body.pivot = new Vector2(1f, 0.5f);
        body.anchoredPosition = new Vector2(-N("rightMargin"), N("centerY"));
        body.sizeDelta = new Vector2(N("panelWidth"), N("panelHeight"));
        body.localScale = Vector3.one;
        var surface = body.GetComponent<UnityEngine.UI.Image>();
        surface.sprite = null;
        surface.type = UnityEngine.UI.Image.Type.Simple;
        surface.color = Presentation.SharedColor("ui.layout.surface");
        surface.raycastTarget = true;

        var header = Rect(body, "LogHeader");
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = new Vector2(0f, -N("padding"));
        header.sizeDelta = new Vector2(-N("padding") * 2f, N("headerHeight"));
        var headerLayout = Add<UnityEngine.UI.HorizontalLayoutGroup>(header.gameObject);
        headerLayout.spacing = N("gap");
        headerLayout.childControlWidth = headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        title.transform.SetParent(header, false);
        title.name = "LogTitle";
        title.transform.SetAsFirstSibling();
        StyleText(title, N("titleFontSize"), TextAlignmentOptions.MidlineLeft);
        var titleLayout = Add<UnityEngine.UI.LayoutElement>(title.gameObject);
        titleLayout.minWidth = 0f;
        titleLayout.flexibleWidth = 1f;
        title.text = Presentation.RumorLogTitle;
        count.transform.SetParent(header, false);
        count.name = "LogCount";
        StyleText(count, N("fontSize"), TextAlignmentOptions.MidlineRight);
        FixedWidth(count.gameObject, N("countWidth"));
        count.text = Presentation.RumorLogCount(0);
        ConfigureButton(collapse, header, "CollapseLog");
        FixedWidth(collapse.gameObject, N("collapseWidth"));
        var collapseLabel = collapse.GetComponentInChildren<TextMeshProUGUI>(true);
        StyleText(collapseLabel, N("fontSize"), TextAlignmentOptions.Center);
        collapseLabel.text = Presentation.RumorLogCollapse;
        collapse.transform.SetAsLastSibling();

        ConfigureButton(toggle, _root.transform, "ToggleLog");
        var toggleRect = (RectTransform)toggle.transform;
        toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-N("rightMargin"), N("centerY"));
        toggleRect.sizeDelta = new Vector2(N("toggleWidth"), N("toggleHeight"));
        var toggleLabel = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
        StyleText(toggleLabel, N("fontSize"), TextAlignmentOptions.Center);
        toggleLabel.text = Presentation.RumorLogCollapsed(0);

        var scrollRoot = Rect(body, "LogScroll");
        Stretch(scrollRoot);
        scrollRoot.offsetMin = Vector2.one * N("padding");
        scrollRoot.offsetMax = new Vector2(-N("padding"), -(N("padding") * 2f + N("headerHeight")));
        var scroll = Add<UnityEngine.UI.ScrollRect>(scrollRoot.gameObject);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = N("scrollSensitivity");
        var viewport = Rect(scrollRoot, "Viewport");
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-N("scrollbarWidth") - N("gap"), 0f);
        Add<UnityEngine.UI.Image>(viewport.gameObject).raycastTarget = true;
        Add<UnityEngine.UI.Mask>(viewport.gameObject).showMaskGraphic = false;
        var content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = content.sizeDelta = Vector2.zero;
        var contentLayout = Add<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject);
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        var fitter = Add<UnityEngine.UI.ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        message.transform.SetParent(content, false);
        message.name = "ConversationLog";
        StyleText(message, N("fontSize"), TextAlignmentOptions.TopLeft);
        message.textWrappingMode = TextWrappingModes.Normal;
        message.text = Presentation.RumorLogEmpty;
        scroll.content = content;
        scroll.viewport = viewport;

        var barRect = Rect(scrollRoot, "Scrollbar");
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(N("scrollbarWidth"), 0f);
        var barBackground = Add<UnityEngine.UI.Image>(barRect.gameObject);
        barBackground.color = Presentation.SharedColor("ui.layout.button.normal");
        barBackground.raycastTarget = true;
        var bar = Add<UnityEngine.UI.Scrollbar>(barRect.gameObject);
        var handle = Rect(barRect, "Handle");
        Stretch(handle);
        var handleImage = Add<UnityEngine.UI.Image>(handle.gameObject);
        handleImage.color = Presentation.SharedColor("ui.layout.accent");
        bar.handleRect = handle;
        bar.targetGraphic = handleImage;
        bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;

        var view = _root.GetComponent<PuzzleTradeNotebookView>();
        var fields = new SerializedObject(view);
        fields.FindProperty("startOpen").boolValue = false;
        fields.FindProperty("root").objectReferenceValue = body.gameObject;
        fields.FindProperty("toggleButton").objectReferenceValue = toggle;
        fields.FindProperty("collapseButton").objectReferenceValue = collapse;
        fields.FindProperty("toggleLabel").objectReferenceValue = toggleLabel;
        fields.FindProperty("titleLabel").objectReferenceValue = title;
        fields.FindProperty("countLabel").objectReferenceValue = count;
        fields.FindProperty("collapseLabel").objectReferenceValue = collapseLabel;
        fields.FindProperty("logText").objectReferenceValue = message;
        fields.FindProperty("scroll").objectReferenceValue = scroll;
        fields.ApplyModifiedPropertiesWithoutUndo();
        var presenter = Add<PuzzleTradeNotebookPresenter>(_root);
        var presenterData = new SerializedObject(presenter);
        presenterData.FindProperty("view").objectReferenceValue = view;
        presenterData.ApplyModifiedPropertiesWithoutUndo();
        body.gameObject.SetActive(false);
        toggle.gameObject.SetActive(true);
        _root.SetActive(true);
    }

    static void RemoveBookButtons(GameObject _root)
    {
        foreach (var button in _root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (button.name == "Notebook Cover")
            {
                UnityEngine.Object.DestroyImmediate(button.gameObject);
            }
        }
    }

    static void ConfigureButton(UnityEngine.UI.Button _button, Transform _parent, string _name)
    {
        _button.name = _name;
        _button.transform.SetParent(_parent, false);
        _button.gameObject.SetActive(true);
        _button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        PuzzleUiLayoutTool.Button(_button);
    }

    static void StyleText(TextMeshProUGUI _text, float _size, TextAlignmentOptions _alignment)
    {
        _text.gameObject.SetActive(true);
        _text.richText = true;
        _text.fontSize = _size;
        _text.enableAutoSizing = false;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.alignment = _alignment;
        _text.color = Presentation.SharedColor("ui.layout.text");
        _text.raycastTarget = false;
        _text.margin = Vector4.zero;
    }

    static void FixedWidth(GameObject _host, float _width)
    {
        var element = Add<UnityEngine.UI.LayoutElement>(_host);
        element.minWidth = element.preferredWidth = _width;
        element.flexibleWidth = 0f;
    }

    static RectTransform Rect(Transform _parent, string _name)
    {
        var child = _parent.Find(_name) as RectTransform;
        if (!child)
        {
            var host = new GameObject(_name, typeof(RectTransform));
            host.layer = _parent.gameObject.layer;
            child = (RectTransform)host.transform;
            child.SetParent(_parent, false);
        }
        child.gameObject.SetActive(true);
        return child;
    }

    static void Stretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.pivot = Vector2.one * 0.5f;
        _rect.anchoredPosition = _rect.sizeDelta = Vector2.zero;
        _rect.localScale = Vector3.one;
    }

    static T Add<T>(GameObject _host) where T : Component
    {
        return _host.TryGetComponent<T>(out var component) ? component : _host.AddComponent<T>();
    }
}
