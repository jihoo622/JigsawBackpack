using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 씬을 수정·저장하는 설정 도구이므로 컴파일 후 자동 실행하지 않고 메뉴에서만 호출한다.
public static class TrainBarSceneSetup
{
    const string SCENE_PATH = "Assets/Scenes/Game.unity";
    const string TRAIN_WAGON_SPRITE_PATH = "Assets/Art/TrainWagon.png";
    const string FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Medium SDF.asset";
    static int PoolSize => BasicData.Int("editor.TrainBarSceneSetup.poolsize");

    [MenuItem("Tools/MyJG/Setup Train Bar in Game Scene")]
    public static void EnsureAll()
    {
        if (Application.isPlaying)
        {
            return;
        }

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

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
        var wagonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TRAIN_WAGON_SPRITE_PATH);

        var barTransform = canvas.transform.Find("TrainBar");
        if (barTransform)
        {
            UnityEngine.Object.DestroyImmediate(barTransform.gameObject);
        }

        var trainBarObj = CreateTrainBarHierarchy(canvas.transform, fontAsset, wagonSprite);
        var trainBarView = trainBarObj.GetComponent<PuzzleTrainBarView>();

        var presenterSerialized = new SerializedObject(presenter);
        var barProp = presenterSerialized.FindProperty("trainBarView");
        if (barProp != null)
        {
            barProp.objectReferenceValue = trainBarView;
            presenterSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
        }

        PocArtSetupTool.ApplyScene(activeScene);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("[TrainBarSceneSetup] Successfully configured Train Bar in Game scene.");
    }

    static GameObject CreateTrainBarHierarchy(Transform _canvasTransform, TMP_FontAsset _fontAsset, Sprite _wagonSprite)
    {
        // 1. Root TrainBar
        var root = new GameObject("TrainBar", typeof(RectTransform), typeof(PuzzleTrainBarView));
        root.transform.SetParent(_canvasTransform, false);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -BasicData.Float("editor.TrainBarSceneSetup.rootRectanchoredPositionY"));
        rootRect.sizeDelta = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.rootRectsizeDeltaWidth"), BasicData.Float("editor.TrainBarSceneSetup.rootRectsizeDeltaHeight"));

        var trainBarView = root.GetComponent<PuzzleTrainBarView>();
        var serializedView = new SerializedObject(trainBarView);

        // 2. Add Wagon Button
        var addBtnObj = new GameObject("AddWagonButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        addBtnObj.transform.SetParent(root.transform, false);

        var addBtnRect = addBtnObj.GetComponent<RectTransform>();
        addBtnRect.sizeDelta = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.addBtnRectsizeDeltaWidth"), BasicData.Float("editor.TrainBarSceneSetup.addBtnRectsizeDeltaHeight"));
        addBtnRect.anchoredPosition = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.addBtnRectanchoredPositionX"), 0f);

        var addBtnImg = addBtnObj.GetComponent<Image>();
        addBtnImg.color = Presentation.PuzzleAddWagonBox;

        var addBtn = addBtnObj.GetComponent<Button>();
        var addColors = addBtn.colors;
        addColors.highlightedColor = Presentation.SharedColor("editor.TrainBarSceneSetup.addColorshighlightedColorColor");
        addColors.pressedColor = Presentation.SharedColor("editor.TrainBarSceneSetup.addColorspressedColorColor");
        addColors.disabledColor = Presentation.SharedColor("editor.TrainBarSceneSetup.addColorsdisabledColorColor");
        addBtn.colors = addColors;

        var addTextObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        addTextObj.transform.SetParent(addBtnObj.transform, false);
        var addTextRect = addTextObj.GetComponent<RectTransform>();
        addTextRect.anchorMin = Vector2.zero;
        addTextRect.anchorMax = Vector2.one;
        addTextRect.sizeDelta = Vector2.zero;

        var addText = addTextObj.GetComponent<TextMeshProUGUI>();
        if (_fontAsset)
        {
            addText.font = _fontAsset;
        }
        addText.alignment = TextAlignmentOptions.Center;
        addText.fontSize = BasicData.Int("editor.TrainBarSceneSetup.addTextfontSizeFontSize");
        addText.fontStyle = FontStyles.Bold;
        addText.color = Presentation.PuzzleUnmodifiedTint;
        addText.text = Presentation.PuzzleAddWagonLabel(TableManager.Instance.PuzzleTrade.wagonAddCostBase);

        // 3. Wagon Slots Pool
        var slotsProp = serializedView.FindProperty("wagonSlots");
        slotsProp.arraySize = PoolSize;

        for (int i = 0; i < PoolSize; i++)
        {
            var slotObj = new GameObject($"WagonSlot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            slotObj.transform.SetParent(root.transform, false);

            var slotRect = slotObj.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.slotRectsizeDeltaWidth"), BasicData.Float("editor.TrainBarSceneSetup.slotRectsizeDeltaHeight"));

            var bgImg = slotObj.GetComponent<Image>();
            bgImg.color = Presentation.SharedColor("editor.TrainBarSceneSetup.bgImgcolorColor"); // transparent raycast target

            var slotBtn = slotObj.GetComponent<Button>();

            // Highlight border behind wagon image
            var borderObj = new GameObject("HighlightBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderObj.transform.SetParent(slotObj.transform, false);
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.sizeDelta = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.borderRectsizeDeltaWidth"), BasicData.Float("editor.TrainBarSceneSetup.borderRectsizeDeltaHeight"));
            var borderImg = borderObj.GetComponent<Image>();
            borderImg.color = Presentation.PuzzleWagonSelectedBorder;
            borderImg.raycastTarget = false;
            borderImg.enabled = false;

            // Wagon Image
            var imgObj = new GameObject("WagonImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgObj.transform.SetParent(slotObj.transform, false);
            var imgRect = imgObj.GetComponent<RectTransform>();
            imgRect.sizeDelta = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.imgRectsizeDeltaWidth"), BasicData.Float("editor.TrainBarSceneSetup.imgRectsizeDeltaHeight"));
            var wagonImg = imgObj.GetComponent<Image>();
            wagonImg.sprite = _wagonSprite;
            wagonImg.preserveAspect = true;
            wagonImg.raycastTarget = false;

            // Number Text
            var numObj = new GameObject("NumberText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(slotObj.transform, false);
            var numRect = numObj.GetComponent<RectTransform>();
            numRect.sizeDelta = new Vector2(BasicData.Float("editor.TrainBarSceneSetup.numRectsizeDeltaWidth"), BasicData.Float("editor.TrainBarSceneSetup.numRectsizeDeltaHeight"));
            numRect.anchoredPosition = new Vector2(0f, BasicData.Float("editor.TrainBarSceneSetup.numRectanchoredPositionY")); // 약간 위쪽 목재 벽 중앙

            var numText = numObj.GetComponent<TextMeshProUGUI>();
            if (_fontAsset)
            {
                numText.font = _fontAsset;
            }
            numText.alignment = TextAlignmentOptions.Center;
            numText.fontSize = BasicData.Int("editor.TrainBarSceneSetup.numTextfontSizeFontSize");
            numText.fontStyle = FontStyles.Bold;
            numText.color = Presentation.PuzzleUnmodifiedTint;
            numText.outlineColor = Presentation.PuzzleTextOutline;
            numText.outlineWidth = BasicData.Float("editor.TrainBarSceneSetup.numTextoutlineWidth");
            numText.text = (i + 1).ToString();
            numText.raycastTarget = false;

            // Link to serialized property
            var elem = slotsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("root").objectReferenceValue = slotObj;
            elem.FindPropertyRelative("rectTransform").objectReferenceValue = slotRect;
            elem.FindPropertyRelative("button").objectReferenceValue = slotBtn;
            elem.FindPropertyRelative("wagonImage").objectReferenceValue = wagonImg;
            elem.FindPropertyRelative("highlightBorder").objectReferenceValue = borderImg;
            elem.FindPropertyRelative("numberText").objectReferenceValue = numText;

            slotObj.SetActive(false);
        }

        serializedView.FindProperty("addWagonButton").objectReferenceValue = addBtn;
        serializedView.FindProperty("addWagonRect").objectReferenceValue = addBtnRect;
        serializedView.FindProperty("addWagonBox").objectReferenceValue = addBtnImg;
        serializedView.FindProperty("addWagonText").objectReferenceValue = addText;

        serializedView.ApplyModifiedProperties();
        SetLayerRecursively(root, BasicData.Int("editor.TrainBarSceneSetup.createTrainBarHierarchy"));
        return root;
    }

    static void SetLayerRecursively(GameObject _obj, int _layer)
    {
        _obj.layer = _layer;
        foreach (Transform child in _obj.transform)
        {
            SetLayerRecursively(child.gameObject, _layer);
        }
    }
}
