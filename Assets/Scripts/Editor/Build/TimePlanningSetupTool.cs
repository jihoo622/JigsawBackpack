using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TimePlanningSetupTool
{
    const string SCENE_PATH = "Assets/Scenes/Game.unity";
    const string INVENTORY_PATH = "Assets/Art/PuzzleInventoryUI.prefab";

    [MenuItem("Tools/MyJG/Apply Time Planning")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            return;
        }
        EditorApplication.ExecuteMenuItem("Tools/MyJG/Tables/Reload Tables");
        TableManager.Instance.RequireValid();
        ApplyInventory();
        RailMapLayoutTool.Apply();
        var scene = SceneManager.GetSceneByPath(SCENE_PATH);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        }
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(_root => _root.GetComponentsInChildren<PuzzleCityView>(true)).Single();
            var data = new SerializedObject(view);
            var template = (Button)data.FindProperty("clearButton").objectReferenceValue;
            var header = (RectTransform)view.transform;
            ((TMP_Text)data.FindProperty("timeLabel").objectReferenceValue).text = Presentation.PuzzleTime(
                new PuzzleTimeManager(TableManager.Instance.PuzzleTrade.deadlineHours, TableManager.Instance.PuzzleTime));
            var wait = data.FindProperty("waitButton").objectReferenceValue as Button;
            if (!wait)
            {
                wait = Button(header, template, "WaitForDaylight");
            }
            var waitLabel = wait.GetComponentInChildren<TextMeshProUGUI>(true);
            waitLabel.text = Presentation.PuzzleWaitDaylight;
            ((TMP_Text)data.FindProperty("clearLabel").objectReferenceValue).text =
                Presentation.PuzzleClearButton(TableManager.Instance.PuzzleTrade.gameClearCost);
            wait.interactable = false;
            data.FindProperty("waitButton").objectReferenceValue = wait;
            data.FindProperty("waitLabel").objectReferenceValue = waitLabel;
            data.ApplyModifiedPropertiesWithoutUndo();
            PuzzleUiLayoutTool.ApplyTo(scene);
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

    static void ApplyInventory()
    {
        var root = PrefabUtility.LoadPrefabContents(INVENTORY_PATH);
        try
        {
            foreach (var view in root.GetComponentsInChildren<PuzzleInventoryView>(true))
            {
                var group = view.GetComponent<CanvasGroup>();
                if (!group)
                {
                    group = view.gameObject.AddComponent<CanvasGroup>();
                }
                group.alpha = 1f;
                group.interactable = group.blocksRaycasts = true;
                var data = new SerializedObject(view);
                data.FindProperty("interaction").objectReferenceValue = group;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, INVENTORY_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Button Button(Transform _parent, Button _template, string _name)
    {
        var existing = _parent.Find(_name);
        var button = existing ? existing.GetComponent<Button>() : Object.Instantiate(_template, _parent);
        button.name = _name;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        button.onClick = new Button.ButtonClickedEvent();
        return button;
    }
}
