using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TooltipDetailsSetupTool
{
    const string PREFAB_PATH = "Assets/Art/PuzzleItemTooltip.prefab";
    const string SCENE_PATH = "Assets/Scenes/Game.unity";

    [MenuItem("Tools/MyJG/Apply Tooltip Trade Details")]
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
            ApplyTo(root.GetComponent<PuzzleItemTooltipView>());
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        var scene = SceneManager.GetSceneByPath(SCENE_PATH);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        }
        try
        {
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                foreach (var view in sceneRoot.GetComponentsInChildren<PuzzleItemTooltipView>(true))
                {
                    ApplyTo(view);
                }
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

    public static void ApplyTo(PuzzleItemTooltipView _view)
    {
        var data = new SerializedObject(_view);
        var sourceLabel = (TextMeshProUGUI)data.FindProperty("actualValueLabelText").objectReferenceValue;
        var body = (RectTransform)data.FindProperty("bodyRect").objectReferenceValue;
        var row = body.Find("BaseValueRow") as RectTransform;
        if (!row)
        {
            row = (RectTransform)Object.Instantiate(sourceLabel.transform.parent.gameObject, body).transform;
            row.name = "BaseValueRow";
        }
        PlaceBefore(row, sourceLabel.transform.parent);
        row.gameObject.SetActive(true);
        var label = row.Find("Label").GetComponent<TextMeshProUGUI>();
        var value = row.Find("Value").GetComponent<TextMeshProUGUI>();
        label.text = Presentation.PuzzleTooltipBaseValueLabel;
        value.text = string.Empty;
        data.FindProperty("baseValueLabelText").objectReferenceValue = label;
        data.FindProperty("baseValueValueText").objectReferenceValue = value;

        var active = body.Find("ActiveEffects")?.GetComponent<TextMeshProUGUI>();
        if (!active)
        {
            var template = (TextMeshProUGUI)data.FindProperty("synergyHeaderText").objectReferenceValue;
            active = Object.Instantiate(template, body);
            active.name = "ActiveEffects";
        }
        var section = (GameObject)data.FindProperty("synergySection").objectReferenceValue;
        PlaceBefore(active.transform, section.transform);
        active.textWrappingMode = TextWrappingModes.Normal;
        active.overflowMode = TextOverflowModes.Overflow;
        active.enableAutoSizing = false;
        active.raycastTarget = false;
        active.text = Presentation.PuzzleNoActiveEffects;
        data.FindProperty("activeEffectsText").objectReferenceValue = active;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(_view);
    }

    static void PlaceBefore(Transform _item, Transform _next)
    {
        int index = _next.GetSiblingIndex();
        if (_item.GetSiblingIndex() < index)
        {
            index--;
        }
        _item.SetSiblingIndex(index);
    }

}
