using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 씬을 수정·저장하는 설정 도구이므로 컴파일 후 자동 실행하지 않고 메뉴에서만 호출한다.
public static class TooltipSceneSetup
{
    const string FONT_PATH = "Assets/TextMesh Pro/Fonts/NotoSansKR-Medium.ttf";
    const string OUTPUT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Medium SDF.asset";
    const string BADGE_SPRITE_PATH = "Assets/Art/TooltipBadgeRoundedRect.png";
    const string SCENE_PATH = "Assets/Scenes/Game.unity";
    const string TOOLTIP_PREFAB_PATH = "Assets/Art/PuzzleItemTooltip.prefab";

    [MenuItem("Tools/MyJG/Setup Tooltip in Game Scene")]
    public static void EnsureAll()
    {
        if (Application.isPlaying)
        {
            return;
        }

        var fontAsset = EnsureFontAsset();
        SetupGameScene(fontAsset);
        TooltipDetailsSetupTool.Apply();
    }

    public static TMP_FontAsset EnsureFontAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OUTPUT_PATH);
        if (existing != null)
        {
            UpdateDefaultFont(existing);
            return existing;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        if (font == null)
        {
            Debug.LogError($"[TooltipSceneSetup] Font not found at {FONT_PATH}");
            return null;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font, BasicData.Int("editor.tooltip.fontSamplingSize"), BasicData.Int("editor.TooltipSceneSetup.ensureFontAsset"),
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            BasicData.Int("editor.TooltipSceneSetup.ensureFontAsset2"), BasicData.Int("editor.TooltipSceneSetup.ensureFontAsset3"),
            AtlasPopulationMode.Dynamic, true);

        if (fontAsset == null)
        {
            Debug.LogError("[TooltipSceneSetup] Failed to create TMP_FontAsset");
            return null;
        }

        string dir = Path.GetDirectoryName(OUTPUT_PATH);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        AssetDatabase.CreateAsset(fontAsset, OUTPUT_PATH);
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex != null)
                {
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                }
            }
        }
        if (fontAsset.material != null)
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[TooltipSceneSetup] Created NotoSansKR TMP_FontAsset at {OUTPUT_PATH}");

        UpdateDefaultFont(fontAsset);
        return fontAsset;
    }

    public static Sprite EnsureBadgeSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(BADGE_SPRITE_PATH);
        if (existing != null)
        {
            return existing;
        }

        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    static void UpdateDefaultFont(TMP_FontAsset _fontAsset)
    {
        var settings = TMP_Settings.instance;
        if (settings != null && TMP_Settings.defaultFontAsset != _fontAsset)
        {
            var serialized = new SerializedObject(settings);
            var prop = serialized.FindProperty("m_defaultFontAsset");
            if (prop != null)
            {
                prop.objectReferenceValue = _fontAsset;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
    }

    static void SetupGameScene(TMP_FontAsset _fontAsset)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != SCENE_PATH)
        {
            if (!File.Exists(SCENE_PATH))
            {
                return;
            }
            activeScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        }

        var presenter = UnityEngine.Object.FindAnyObjectByType<PuzzleInventoryPresenter>();
        if (!presenter)
        {
            return;
        }

        var canvas = presenter.GetComponentInParent<Canvas>();
        if (!canvas)
        {
            return;
        }

        var tooltipTransform = canvas.transform.Find("PuzzleItemTooltip");
        bool sceneModified = false;
        if (tooltipTransform)
        {
            var existingView = tooltipTransform.GetComponent<PuzzleItemTooltipView>();
            var catRowProp = existingView ? new SerializedObject(existingView).FindProperty("categoryRow") : null;
            var futureValProp = existingView ? new SerializedObject(existingView).FindProperty("futureValueLabelText") : null;
            var freshnessProp = existingView ? new SerializedObject(existingView).FindProperty("freshnessLabelText") : null;
            var splitRowProp = existingView ? new SerializedObject(existingView).FindProperty("actualValueLabelText") : null;
            var bodyProp = existingView ? new SerializedObject(existingView).FindProperty("bodyRect") : null;
            var futureRowGrandParent = (futureValProp?.objectReferenceValue as TextMeshProUGUI)?.transform.parent?.parent;
            if (catRowProp == null || catRowProp.objectReferenceValue == null ||
                futureValProp == null || futureValProp.objectReferenceValue == null ||
                freshnessProp == null || freshnessProp.objectReferenceValue == null ||
                splitRowProp == null || splitRowProp.objectReferenceValue == null ||
                bodyProp == null || bodyProp.objectReferenceValue == null ||
                futureRowGrandParent != bodyProp.objectReferenceValue)
            {
                UnityEngine.Object.DestroyImmediate(tooltipTransform.gameObject);
                tooltipTransform = null;
            }
        }

        GameObject tooltipObj;
        if (tooltipTransform)
        {
            tooltipObj = tooltipTransform.gameObject;
        }
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOOLTIP_PREFAB_PATH);
            tooltipObj = prefab
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)
                : CreateTooltipHierarchy(canvas.transform, _fontAsset);
            sceneModified = true;
        }

        var tooltipView = tooltipObj.GetComponent<PuzzleItemTooltipView>();
        if (tooltipView)
        {
            var viewSerialized = new SerializedObject(tooltipView);
            var canvasProp = viewSerialized.FindProperty("canvas");
            if (canvasProp.objectReferenceValue != canvas)
            {
                canvasProp.objectReferenceValue = canvas;
                viewSerialized.ApplyModifiedProperties();
                sceneModified = true;
            }
            var presenterSerialized = new SerializedObject(presenter);
            var tooltipProp = presenterSerialized.FindProperty("tooltipView");
            if (tooltipProp != null && tooltipProp.objectReferenceValue != tooltipView)
            {
                tooltipProp.objectReferenceValue = tooltipView;
                presenterSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(presenter);
                sceneModified = true;
            }
        }

        if (sceneModified)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[TooltipSceneSetup] Configured PuzzleItemTooltip in Game scene successfully.");
        }
    }

    static GameObject CreateTooltipHierarchy(Transform _canvasTransform, TMP_FontAsset _fontAsset)
    {
        var badgeSprite = EnsureBadgeSprite();

        // 1. Root
        var root = new GameObject("PuzzleItemTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(PuzzleItemTooltipView));
        root.transform.SetParent(_canvasTransform, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(BasicData.Float("editor.TooltipSceneSetup.rootRectsizeDeltaWidth"), BasicData.Float("editor.TooltipSceneSetup.rootRectsizeDeltaHeight"));

        var rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        var rootImage = root.GetComponent<Image>();
        rootImage.color = Presentation.PuzzleTooltipBodyBackground;
        rootImage.raycastTarget = false;

        var rootLayout = root.GetComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.spacing = 0;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        var rootFitter = root.GetComponent<ContentSizeFitter>();
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 2. Header
        var header = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        header.transform.SetParent(root.transform, false);

        var headerImage = header.GetComponent<Image>();
        headerImage.color = Presentation.PuzzleTooltipHeaderBackground;
        headerImage.raycastTarget = false;

        var headerLayout = header.GetComponent<VerticalLayoutGroup>();
        headerLayout.padding = new RectOffset(BasicData.Int("editor.TooltipSceneSetup.headerLayoutpaddingPaddingLeft"), BasicData.Int("editor.TooltipSceneSetup.headerLayoutpaddingPaddingRight"), BasicData.Int("editor.TooltipSceneSetup.headerLayoutpaddingPaddingTop"), BasicData.Int("editor.TooltipSceneSetup.headerLayoutpaddingPaddingBottom"));
        headerLayout.spacing = BasicData.Int("editor.TooltipSceneSetup.headerLayoutspacing");
        headerLayout.childAlignment = TextAnchor.UpperLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;

        var headerFitter = header.GetComponent<ContentSizeFitter>();
        headerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        headerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var itemName = CreateTextObject("ItemName", header.transform, _fontAsset, string.Empty);
        itemName.alignment = TextAlignmentOptions.TopLeft;

        // Category Row & Badges
        var categoryRow = new GameObject("CategoryRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        categoryRow.transform.SetParent(header.transform, false);

        var catRowLayout = categoryRow.GetComponent<HorizontalLayoutGroup>();
        catRowLayout.spacing = BasicData.Int("editor.TooltipSceneSetup.catRowLayoutspacing");
        catRowLayout.childAlignment = TextAnchor.UpperLeft;
        catRowLayout.childControlWidth = true;
        catRowLayout.childControlHeight = true;
        catRowLayout.childForceExpandWidth = false;
        catRowLayout.childForceExpandHeight = false;

        var catRowFitter = categoryRow.GetComponent<ContentSizeFitter>();
        catRowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        catRowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int badgeCount = BasicData.Int("editor.TooltipSceneSetup.badgecount");
        var badgeImages = new Image[badgeCount];
        var badgeTexts = new TextMeshProUGUI[badgeCount];
        for (int i = 0; i < badgeCount; i++)
        {
            CreateBadgeObject($"Badge_{i}", categoryRow.transform, badgeSprite, _fontAsset, string.Empty, out badgeImages[i], out badgeTexts[i]);
        }

        // 3. Divider
        var divider = new GameObject("DividerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        divider.transform.SetParent(root.transform, false);

        var dividerImage = divider.GetComponent<Image>();
        dividerImage.color = Presentation.PuzzleTooltipDivider;
        dividerImage.raycastTarget = false;

        var dividerLayout = divider.GetComponent<LayoutElement>();
        dividerLayout.minHeight = BasicData.Int("editor.tooltip.dividerHeight");
        dividerLayout.preferredHeight = BasicData.Int("editor.tooltip.dividerHeight");
        dividerLayout.flexibleWidth = 1;

        // 4. Body
        var body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
        body.transform.SetParent(root.transform, false);

        var bodyLayout = body.GetComponent<VerticalLayoutGroup>();
        bodyLayout.padding = new RectOffset(BasicData.Int("editor.TooltipSceneSetup.bodyLayoutpaddingPaddingLeft"), BasicData.Int("editor.TooltipSceneSetup.bodyLayoutpaddingPaddingRight"), BasicData.Int("editor.TooltipSceneSetup.bodyLayoutpaddingPaddingTop"), BasicData.Int("editor.TooltipSceneSetup.bodyLayoutpaddingPaddingBottom"));
        bodyLayout.spacing = BasicData.Int("editor.TooltipSceneSetup.bodyLayoutspacing");
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        CreateValueRow("ActualValueRow", body.transform, _fontAsset, "실제 가치", "$ 100",
            out TextMeshProUGUI actualValLabel, out TextMeshProUGUI actualValValue);
        CreateValueRow("FreshnessRow", body.transform, _fontAsset, "신선도", "•",
            out TextMeshProUGUI freshnessLabel, out TextMeshProUGUI freshnessValue);
        CreateBottomRule(freshnessLabel.transform.parent);
        CreateValueRow("ShelfLifeRow", body.transform, _fontAsset, "신선도 하락까지", "D<space=0.08em>3 0시간",
            out TextMeshProUGUI shelfLifeLabel, out TextMeshProUGUI shelfLifeValue);
        CreateBottomRule(shelfLifeLabel.transform.parent);
        CreateValueRow("SellRateRow", body.transform, _fontAsset, "판매 배율", "100%",
            out TextMeshProUGUI sellRateLabel, out TextMeshProUGUI sellRateValue);
        CreateValueRow("BuyRateRow", body.transform, _fontAsset, "판매 금액", "$ 100",
            out TextMeshProUGUI buyRateLabel, out TextMeshProUGUI buyRateValue);
        CreateValueRow("FutureValueRow", body.transform, _fontAsset, "미래 가치", "$ 100",
            out TextMeshProUGUI futureLabel, out TextMeshProUGUI futureValue);

        // 5. Synergy Section
        var synergySection = new GameObject("SynergySection", typeof(RectTransform), typeof(VerticalLayoutGroup));
        synergySection.transform.SetParent(body.transform, false);

        var synSectionLayout = synergySection.GetComponent<VerticalLayoutGroup>();
        synSectionLayout.padding = new RectOffset(0, 0, BasicData.Int("editor.TooltipSceneSetup.synSectionLayoutpaddingPaddingTop"), 0);
        synSectionLayout.spacing = BasicData.Int("editor.TooltipSceneSetup.synSectionLayoutspacing");
        synSectionLayout.childControlWidth = true;
        synSectionLayout.childControlHeight = true;
        synSectionLayout.childForceExpandWidth = true;
        synSectionLayout.childForceExpandHeight = false;

        var synHeader = CreateTextObject("SynergyHeader", synergySection.transform, _fontAsset, Presentation.PuzzleTooltipValueSynergyHeader());
        synHeader.enableAutoSizing = false;

        var synergyList = new GameObject("SynergyList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        synergyList.transform.SetParent(synergySection.transform, false);

        var synListLayout = synergyList.GetComponent<VerticalLayoutGroup>();
        synListLayout.spacing = BasicData.Int("editor.tooltip.synergySpacing");
        synListLayout.childControlWidth = true;
        synListLayout.childControlHeight = true;
        synListLayout.childForceExpandWidth = true;
        synListLayout.childForceExpandHeight = false;

        var slots = new TextMeshProUGUI[BasicData.Int("editor.TooltipSceneSetup.createTooltipHierarchy")];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = CreateTextObject($"Slot_{i}", synergyList.transform, _fontAsset, "• 식량과 함께하면 가치가 올라갑니다.");
            slots[i].enableAutoSizing = false;
            slots[i].textWrappingMode = TextWrappingModes.Normal;
        }

        // Wire serialized fields on PuzzleItemTooltipView
        var view = root.GetComponent<PuzzleItemTooltipView>();
        var viewSerialized = new SerializedObject(view);
        viewSerialized.FindProperty("canvas").objectReferenceValue = _canvasTransform.GetComponent<Canvas>();
        viewSerialized.FindProperty("panelRect").objectReferenceValue = rootRect;
        viewSerialized.FindProperty("canvasGroup").objectReferenceValue = rootGroup;
        viewSerialized.FindProperty("itemNameText").objectReferenceValue = itemName;

        viewSerialized.FindProperty("categoryRow").objectReferenceValue = categoryRow.GetComponent<RectTransform>();
        var badgeImagesProp = viewSerialized.FindProperty("categoryBadgeImages");
        badgeImagesProp.arraySize = badgeImages.Length;
        for (int i = 0; i < badgeImages.Length; i++)
        {
            badgeImagesProp.GetArrayElementAtIndex(i).objectReferenceValue = badgeImages[i];
        }
        var badgeTextsProp = viewSerialized.FindProperty("categoryBadgeTexts");
        badgeTextsProp.arraySize = badgeTexts.Length;
        for (int i = 0; i < badgeTexts.Length; i++)
        {
            badgeTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = badgeTexts[i];
        }

        viewSerialized.FindProperty("bodyRect").objectReferenceValue = body.GetComponent<RectTransform>();
        viewSerialized.FindProperty("actualValueLabelText").objectReferenceValue = actualValLabel;
        viewSerialized.FindProperty("actualValueValueText").objectReferenceValue = actualValValue;
        viewSerialized.FindProperty("freshnessLabelText").objectReferenceValue = freshnessLabel;
        viewSerialized.FindProperty("freshnessValueText").objectReferenceValue = freshnessValue;
        freshnessLabel.transform.parent.gameObject.SetActive(false);
        viewSerialized.FindProperty("shelfLifeLabelText").objectReferenceValue = shelfLifeLabel;
        viewSerialized.FindProperty("shelfLifeValueText").objectReferenceValue = shelfLifeValue;
        shelfLifeLabel.transform.parent.gameObject.SetActive(false);
        viewSerialized.FindProperty("sellRateLabelText").objectReferenceValue = sellRateLabel;
        viewSerialized.FindProperty("sellRateValueText").objectReferenceValue = sellRateValue;
        viewSerialized.FindProperty("buyRateLabelText").objectReferenceValue = buyRateLabel;
        viewSerialized.FindProperty("buyRateValueText").objectReferenceValue = buyRateValue;
        viewSerialized.FindProperty("synergySection").objectReferenceValue = synergySection;
        viewSerialized.FindProperty("synergyHeaderText").objectReferenceValue = synHeader;
        viewSerialized.FindProperty("synergyListRoot").objectReferenceValue = synergyList.GetComponent<RectTransform>();

        var slotsProp = viewSerialized.FindProperty("synergySlots");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }

        viewSerialized.FindProperty("futureValueLabelText").objectReferenceValue = futureLabel;
        viewSerialized.FindProperty("futureValueValueText").objectReferenceValue = futureValue;

        viewSerialized.ApplyModifiedProperties();
        TooltipDetailsSetupTool.ApplyTo(view);

        root.SetActive(false);
        return root;
    }

    static void CreateBadgeObject(string _name, Transform _parent, Sprite _badgeSprite, TMP_FontAsset _fontAsset, string _sample,
        out Image _badgeImage, out TextMeshProUGUI _badgeText)
    {
        var badgeObj = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        badgeObj.transform.SetParent(_parent, false);

        _badgeImage = badgeObj.GetComponent<Image>();
        _badgeImage.sprite = _badgeSprite;
        _badgeImage.type = Image.Type.Sliced;
        _badgeImage.color = Presentation.PuzzleTagBackgroundColor("Wood");
        _badgeImage.raycastTarget = false;

        var layout = badgeObj.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(BasicData.Int("editor.TooltipSceneSetup.layoutpaddingPaddingLeft"), BasicData.Int("editor.TooltipSceneSetup.layoutpaddingPaddingRight"), BasicData.Int("editor.tooltip.badgeVerticalPadding"), BasicData.Int("editor.tooltip.badgeVerticalPadding"));
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        var fitter = badgeObj.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _badgeText = CreateTextObject("Label", badgeObj.transform, _fontAsset, _sample);
        _badgeText.alignment = TextAlignmentOptions.Center;
        _badgeText.color = Presentation.PuzzleTagTextColor("Wood");
    }

    static TextMeshProUGUI CreateTextObject(string _name, Transform _parent, TMP_FontAsset _fontAsset, string _sample)
    {
        var obj = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(_parent, false);

        var tmp = obj.GetComponent<TextMeshProUGUI>();
        if (_fontAsset)
        {
            tmp.font = _fontAsset;
        }
        tmp.text = _sample;
        tmp.fontSize = BasicData.Float("editor.TooltipSceneSetup.tmpfontSizeFontSize");
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = BasicData.Float("editor.TooltipSceneSetup.tmpfontSizeMinFontSize");
        tmp.fontSizeMax = BasicData.Float("editor.TooltipSceneSetup.tmpfontSizeMaxFontSize");
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.color = Presentation.PuzzleTooltipText;
        return tmp;
    }

    static void CreateBottomRule(Transform _row)
    {
        var rule = new GameObject("Bottom Rule", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rect = rule.GetComponent<RectTransform>();
        rect.SetParent(_row, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.anchoredPosition = new Vector2(0f, -5f);
        rect.sizeDelta = new Vector2(0f, 1f);
        var image = rule.GetComponent<Image>();
        image.color = Presentation.PuzzleTooltipDivider;
        image.raycastTarget = false;
        rule.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    // 라벨은 왼쪽, 수치는 오른쪽으로 붙이는 2-텍스트 행.
    static void CreateValueRow(string _name, Transform _parent, TMP_FontAsset _fontAsset, string _label, string _value,
        out TextMeshProUGUI _labelText, out TextMeshProUGUI _valueText)
    {
        var row = new GameObject(_name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_parent, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        _labelText = CreateTextObject("Label", row.transform, _fontAsset, _label);
        _labelText.alignment = TextAlignmentOptions.MidlineLeft;
        _labelText.enableAutoSizing = false;
        var labelLayout = _labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;

        _valueText = CreateTextObject("Value", row.transform, _fontAsset, _value);
        _valueText.alignment = TextAlignmentOptions.MidlineRight;
        _valueText.enableAutoSizing = false;
    }
}
