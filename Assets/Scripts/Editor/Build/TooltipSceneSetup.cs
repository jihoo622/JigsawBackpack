using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TooltipSceneSetup
{
    const string FONT_PATH = "Assets/TextMesh Pro/Fonts/NotoSansKR-Medium.ttf";
    const string OUTPUT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Medium SDF.asset";
    const string BADGE_SPRITE_PATH = "Assets/Art/TooltipBadgeRoundedRect.png";
    const string SCENE_PATH = "Assets/Scenes/Game.unity";

    static TooltipSceneSetup()
    {
        EditorApplication.delayCall += EnsureAll;
    }

    [MenuItem("Tools/MyJG/Setup Tooltip in Game Scene")]
    public static void EnsureAll()
    {
        if (Application.isPlaying)
        {
            return;
        }

        var fontAsset = EnsureFontAsset();
        SetupGameScene(fontAsset);
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
            font, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            1024, 1024,
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
            var futureValProp = existingView ? new SerializedObject(existingView).FindProperty("futureValueText") : null;
            if (catRowProp == null || catRowProp.objectReferenceValue == null ||
                futureValProp == null || futureValProp.objectReferenceValue == null)
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
            tooltipObj = CreateTooltipHierarchy(canvas.transform, _fontAsset);
            sceneModified = true;
        }

        var tooltipView = tooltipObj.GetComponent<PuzzleItemTooltipView>();
        if (tooltipView)
        {
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
        rootRect.sizeDelta = new Vector2(300f, 200f);

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
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 2. Header
        var header = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        header.transform.SetParent(root.transform, false);

        var headerImage = header.GetComponent<Image>();
        headerImage.color = Presentation.PuzzleTooltipHeaderBackground;
        headerImage.raycastTarget = false;

        var headerLayout = header.GetComponent<VerticalLayoutGroup>();
        headerLayout.padding = new RectOffset(16, 16, 12, 12);
        headerLayout.spacing = 8;
        headerLayout.childAlignment = TextAnchor.UpperLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;

        var headerFitter = header.GetComponent<ContentSizeFitter>();
        headerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        headerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var itemName = CreateTextObject("ItemName", header.transform, _fontAsset, "물건 이름");

        // Category Row & Badges
        var categoryRow = new GameObject("CategoryRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        categoryRow.transform.SetParent(header.transform, false);

        var catRowLayout = categoryRow.GetComponent<HorizontalLayoutGroup>();
        catRowLayout.spacing = 6;
        catRowLayout.childAlignment = TextAnchor.MiddleLeft;
        catRowLayout.childControlWidth = true;
        catRowLayout.childControlHeight = true;
        catRowLayout.childForceExpandWidth = false;
        catRowLayout.childForceExpandHeight = false;

        var catRowFitter = categoryRow.GetComponent<ContentSizeFitter>();
        catRowFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        catRowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        const int BADGE_COUNT = 3;
        var badgeImages = new Image[BADGE_COUNT];
        var badgeTexts = new TextMeshProUGUI[BADGE_COUNT];
        for (int i = 0; i < BADGE_COUNT; i++)
        {
            CreateBadgeObject($"Badge_{i}", categoryRow.transform, badgeSprite, _fontAsset, "목재", out badgeImages[i], out badgeTexts[i]);
        }

        // 3. Divider
        var divider = new GameObject("DividerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        divider.transform.SetParent(root.transform, false);

        var dividerImage = divider.GetComponent<Image>();
        dividerImage.color = Presentation.PuzzleTooltipDivider;
        dividerImage.raycastTarget = false;

        var dividerLayout = divider.GetComponent<LayoutElement>();
        dividerLayout.minHeight = 2;
        dividerLayout.preferredHeight = 2;
        dividerLayout.flexibleWidth = 1;

        // 4. Body
        var body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
        body.transform.SetParent(root.transform, false);

        var bodyLayout = body.GetComponent<VerticalLayoutGroup>();
        bodyLayout.padding = new RectOffset(16, 16, 14, 16);
        bodyLayout.spacing = 10;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        var actualVal = CreateTextObject("ActualValue", body.transform, _fontAsset, "실제 가치 : $ 100");
        var sellRate = CreateTextObject("SellRate", body.transform, _fontAsset, "판매 배율 : 100%");
        var buyRate = CreateTextObject("BuyRate", body.transform, _fontAsset, "구매 배율 : 100%");

        // 5. Synergy Section
        var synergySection = new GameObject("SynergySection", typeof(RectTransform), typeof(VerticalLayoutGroup));
        synergySection.transform.SetParent(body.transform, false);

        var synSectionLayout = synergySection.GetComponent<VerticalLayoutGroup>();
        synSectionLayout.padding = new RectOffset(0, 0, 6, 0);
        synSectionLayout.spacing = 6;
        synSectionLayout.childControlWidth = true;
        synSectionLayout.childControlHeight = true;
        synSectionLayout.childForceExpandWidth = true;
        synSectionLayout.childForceExpandHeight = false;

        var synHeader = CreateTextObject("SynergyHeader", synergySection.transform, _fontAsset, "적용 중인 시너지 목록");

        var synergyList = new GameObject("SynergyList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        synergyList.transform.SetParent(synergySection.transform, false);

        var synListLayout = synergyList.GetComponent<VerticalLayoutGroup>();
        synListLayout.spacing = 4;
        synListLayout.childControlWidth = true;
        synListLayout.childControlHeight = true;
        synListLayout.childForceExpandWidth = true;
        synListLayout.childForceExpandHeight = false;

        var slots = new TextMeshProUGUI[6];
        for (int i = 0; i < 6; i++)
        {
            slots[i] = CreateTextObject($"Slot_{i}", synergyList.transform, _fontAsset, "• 아이템 이름 : +20%");
        }

        var futureValue = CreateTextObject("FutureValue", synergySection.transform, _fontAsset, "미래 가치 : $ 100");

        // Wire serialized fields on PuzzleItemTooltipView
        var view = root.GetComponent<PuzzleItemTooltipView>();
        var viewSerialized = new SerializedObject(view);
        viewSerialized.FindProperty("canvas").objectReferenceValue = _canvasTransform.GetComponent<Canvas>();
        viewSerialized.FindProperty("panelRect").objectReferenceValue = rootRect;
        viewSerialized.FindProperty("canvasGroup").objectReferenceValue = rootGroup;
        viewSerialized.FindProperty("headerBackground").objectReferenceValue = headerImage;
        viewSerialized.FindProperty("dividerImage").objectReferenceValue = dividerImage;
        viewSerialized.FindProperty("bodyBackground").objectReferenceValue = rootImage;
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

        viewSerialized.FindProperty("actualValueText").objectReferenceValue = actualVal;
        viewSerialized.FindProperty("sellRateText").objectReferenceValue = sellRate;
        viewSerialized.FindProperty("buyRateText").objectReferenceValue = buyRate;
        viewSerialized.FindProperty("synergySection").objectReferenceValue = synergySection;
        viewSerialized.FindProperty("synergyHeaderText").objectReferenceValue = synHeader;
        viewSerialized.FindProperty("synergyListRoot").objectReferenceValue = synergyList.GetComponent<RectTransform>();

        var slotsProp = viewSerialized.FindProperty("synergySlots");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }

        viewSerialized.FindProperty("futureValueText").objectReferenceValue = futureValue;

        if (_fontAsset != null)
        {
            viewSerialized.FindProperty("fontAsset").objectReferenceValue = _fontAsset;
        }
        viewSerialized.ApplyModifiedProperties();

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
        layout.padding = new RectOffset(12, 12, 4, 4);
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
        tmp.fontSize = 18f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 24f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.color = Presentation.PuzzleTooltipText;
        return tmp;
    }
}
