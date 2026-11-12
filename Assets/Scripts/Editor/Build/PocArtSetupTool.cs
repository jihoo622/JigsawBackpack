using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 에디터에서만 아트를 배치한다. 런타임 View와 입력 참조는 기존 것을 사용한다.
public static class PocArtSetupTool
{
    const string ROOT = "Assets/Art/External/POC_06/";
    const string SCENE_PATH = "Assets/Scenes/Game.unity";
    const string MAP_PATH = "Assets/Art/PuzzleWorldMap.prefab";

    [MenuItem("Tools/MyJG/Apply POC Art Resources")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            return;
        }
        var scene = SceneManager.GetSceneByPath(SCENE_PATH);
        if (scene.isLoaded && scene.isDirty)
        {
            throw new InvalidOperationException("Game 씬의 미저장 편집을 먼저 저장한 뒤 아트를 적용하세요.");
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == MAP_PATH && stage.scene.isDirty)
        {
            throw new InvalidOperationException("지도 프리팹의 미저장 편집을 먼저 저장하세요.");
        }
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        }
        try
        {
            ApplyScene(scene);
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
        var root = PrefabUtility.LoadPrefabContents(MAP_PATH);
        try
        {
            ApplyTravel(root);
            PrefabUtility.SaveAsPrefabAsset(root, MAP_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Sprite Sprite(string _path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ROOT + _path);
        return sprite ? sprite : throw new InvalidOperationException("아트 스프라이트를 찾지 못했습니다: " + _path);
    }

    static RectTransform Rect(Transform _parent, string _name)
    {
        var child = _parent.Find(_name) as RectTransform;
        if (!child)
        {
            child = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
            child.SetParent(_parent, false);
            child.gameObject.layer = _parent.gameObject.layer;
        }
        return child;
    }

    static Image Artwork(Transform _parent, string _name, string _sprite)
    {
        var rect = Rect(_parent, _name);
        var image = rect.GetComponent<Image>();
        if (!image)
        {
            image = rect.gameObject.AddComponent<Image>();
        }
        image.sprite = Sprite(_sprite);
        image.color = Presentation.PuzzleUnmodifiedTint;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public static void ApplyScene(Scene _scene)
    {
        foreach (var root in _scene.GetRootGameObjects())
        {
            foreach (var bar in root.GetComponentsInChildren<PuzzleTrainBarView>(true))
            {
                var fields = new SerializedObject(bar);
                var slots = fields.FindProperty("wagonSlots");
                fields.FindProperty("fitRotatedWagonArtwork").boolValue = true;
                for (int i = 0; i < slots.arraySize; i++)
                {
                    var image = slots.GetArrayElementAtIndex(i).FindPropertyRelative("wagonImage").objectReferenceValue as Image;
                    if (!image)
                    {
                        continue;
                    }
                    image.sprite = Sprite("Trains/train_cargo.png");
                    image.color = Presentation.PuzzleUnmodifiedTint;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    image.rectTransform.sizeDelta = new Vector2(48f, 72f);
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var presenter in root.GetComponentsInChildren<PuzzleInventoryPresenter>(true))
            {
                var fields = new SerializedObject(presenter);
                var train = fields.FindProperty("trainView").objectReferenceValue as PuzzleInventoryView;
                if (!train)
                {
                    continue;
                }
                var image = Artwork(train.transform, "LocomotiveArtwork", "Trains/train_locomotive.png");
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-76f, -30f);
                rect.sizeDelta = new Vector2(28f, 84f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            foreach (var station in root.GetComponentsInChildren<PuzzleStationActionsView>(true))
            {
                var fields = new SerializedObject(station);
                string[] actions = { "market", "tavern", "rest", "travel" };
                string[] icons = { "Icons/UI/ui_cargo.png", "Icons/UI/ui_people.png", "Props/bench.png", "Icons/UI/ui_depart.png" };
                for (int i = 0; i < actions.Length; i++)
                {
                    var button = fields.FindProperty(actions[i] + "Button").objectReferenceValue as Button;
                    if (!button)
                    {
                        continue;
                    }
                    var image = Artwork(button.transform, "ActionIcon", icons[i]);
                    var rect = image.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.anchoredPosition = new Vector2(BasicData.Float("ui.layout.actionIconX"), 0f);
                    rect.sizeDelta = Vector2.one * BasicData.Float("ui.layout.actionIconSize");
                    var number = button.transform.Find("ActionNumber");
                    if (number)
                    {
                        number.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public static void ApplyTravel(GameObject _root)
    {
        var view = _root.GetComponentInChildren<PuzzleMapTravelView>(true);
        if (!view)
        {
            return;
        }
        var fields = new SerializedObject(view);
        var train = fields.FindProperty("train").objectReferenceValue as RectTransform;
        foreach (Transform child in train)
        {
            if (child.name != "ImportedTrainArtwork")
            {
                child.gameObject.SetActive(false);
            }
        }
        var rect = Rect(train, "ImportedTrainArtwork");
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(216f, 72f);
        var image = rect.GetComponent<RawImage>();
        if (!image)
        {
            image = rect.gameObject.AddComponent<RawImage>();
        }
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "Trains/side_train.png");
        if (!image.texture)
        {
            throw new InvalidOperationException("측면 열차 텍스처를 찾지 못했습니다.");
        }
        image.color = Presentation.PuzzleUnmodifiedTint;
        image.raycastTarget = false;
        // 원본은 왼쪽을 보고 있으므로 기존 왼쪽→오른쪽 이동 방향에 맞춘다.
        image.uvRect = new Rect(1f, 0f, -1f, 1f);
        rect.gameObject.SetActive(true);
    }
}
