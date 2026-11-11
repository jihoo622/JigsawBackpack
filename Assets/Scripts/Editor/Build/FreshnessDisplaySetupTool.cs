using TMPro;
using UnityEditor;
using UnityEngine;

public static class FreshnessDisplaySetupTool
{
    const string SLOT_PATH = "Assets/Art/PuzzleTriangles/PuzzleSlot.prefab";

    [MenuItem("Tools/MyJG/Apply Freshness Display")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        TableManager.Instance.RequireValid();
        var root = PrefabUtility.LoadPrefabContents(SLOT_PATH);
        try
        {
            var slot = root.GetComponent<PuzzleSlotVisual>();
            var child = root.transform.Find("Freshness");
            var label = child ? child.GetComponent<TextMeshProUGUI>() : null;
            if (!label)
            {
                var host = new GameObject("Freshness", typeof(RectTransform), typeof(TextMeshProUGUI));
                host.transform.SetParent(root.transform, false);
                label = host.GetComponent<TextMeshProUGUI>();
            }
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Medium SDF.asset");
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.pivot = Vector2.zero;
            float width = ((RectTransform)root.transform).rect.width;
            label.fontSize = width * BasicData.Float("ui.freshness.fontScale");
            label.rectTransform.sizeDelta = new Vector2(width * BasicData.Float("ui.freshness.widthScale"),
                width * BasicData.Float("ui.freshness.heightScale"));
            label.text = string.Empty;
            label.enabled = false;
            var data = new SerializedObject(slot);
            data.FindProperty("freshnessLabel").objectReferenceValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SLOT_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
