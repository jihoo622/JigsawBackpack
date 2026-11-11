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
            header.sizeDelta = new Vector2(header.sizeDelta.x, BasicData.Float("editor.TimePlanningSetupTool.Vector2.number1"));
            Place((TMP_Text)data.FindProperty("cityName").objectReferenceValue, new Vector2(-BasicData.Float("editor.TimePlanningSetupTool.Vector2.number2"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number3")), new Vector2(BasicData.Float("editor.TimePlanningSetupTool.Vector2.number4"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number5")), BasicData.Int("editor.TimePlanningSetupTool.Vector2.number6"));
            Place((TMP_Text)data.FindProperty("timeLabel").objectReferenceValue, new Vector2(-BasicData.Float("editor.TimePlanningSetupTool.Vector2.number7"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number8")), new Vector2(BasicData.Float("editor.TimePlanningSetupTool.Vector2.number9"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number10")), BasicData.Int("editor.TimePlanningSetupTool.Vector2.number11"));
            ((TMP_Text)data.FindProperty("timeLabel").objectReferenceValue).text = Presentation.PuzzleTime(
                new PuzzleTimeManager(TableManager.Instance.PuzzleTrade.deadlineHours, TableManager.Instance.PuzzleTime));
            Place((TMP_Text)data.FindProperty("funds").objectReferenceValue, new Vector2(BasicData.Float("editor.TimePlanningSetupTool.Vector2.number12"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number13")), new Vector2(BasicData.Float("editor.TimePlanningSetupTool.Vector2.number14"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number15")), BasicData.Int("editor.TimePlanningSetupTool.Vector2.number16"));
            Place((TMP_Text)data.FindProperty("tradeStatus").objectReferenceValue, new Vector2(0f, -BasicData.Float("editor.TimePlanningSetupTool.Vector2.number17")), new Vector2(BasicData.Float("editor.TimePlanningSetupTool.Vector2.number18"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number19")), BasicData.Int("editor.TimePlanningSetupTool.Vector2.number20"));
            var preview = Button(header, template, "PreviewDestination", new Vector2(-BasicData.Float("editor.TimePlanningSetupTool.Vector2.number21"), -BasicData.Float("editor.TimePlanningSetupTool.Vector2.number22")), new Vector2(BasicData.Float("editor.TimePlanningSetupTool.Vector2.number23"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number24")));
            var wait = data.FindProperty("waitButton").objectReferenceValue as Button;
            if (!wait)
            {
                wait = Button(header, template, "WaitForDaylight", new Vector2(-BasicData.Float("editor.TimePlanningSetupTool.Vector2.number25"), -BasicData.Float("editor.TimePlanningSetupTool.Vector2.number26")), new Vector2(BasicData.Float("station.wait.width"), BasicData.Float("editor.TimePlanningSetupTool.Vector2.number27")));
            }
            var previewLabel = preview.GetComponentInChildren<TextMeshProUGUI>(true);
            var waitLabel = wait.GetComponentInChildren<TextMeshProUGUI>(true);
            previewLabel.text = Presentation.PuzzlePreviewDestination(null, null);
            waitLabel.text = Presentation.PuzzleWaitDaylight;
            previewLabel.fontSize = waitLabel.fontSize = BasicData.Float("editor.TimePlanningSetupTool.Vector2.number28");
            wait.interactable = false;
            data.FindProperty("waitButton").objectReferenceValue = wait;
            data.FindProperty("waitLabel").objectReferenceValue = waitLabel;
            data.FindProperty("previewButton").objectReferenceValue = preview;
            data.FindProperty("previewLabel").objectReferenceValue = previewLabel;
            data.ApplyModifiedPropertiesWithoutUndo();
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

    static void Place(TMP_Text _text, Vector2 _position, Vector2 _size, float _fontSize)
    {
        _text.rectTransform.anchoredPosition = _position;
        _text.rectTransform.sizeDelta = _size;
        _text.fontSize = _fontSize;
    }

    static Button Button(Transform _parent, Button _template, string _name, Vector2 _position, Vector2 _size)
    {
        var existing = _parent.Find(_name);
        var button = existing ? existing.GetComponent<Button>() : Object.Instantiate(_template, _parent);
        button.name = _name;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = _position;
        rect.sizeDelta = _size;
        button.onClick = new Button.ButtonClickedEvent();
        return button;
    }
}
