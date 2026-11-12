using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        var section = (GameObject)data.FindProperty("synergySection").objectReferenceValue;
        var activeSection = body.Find("ActiveEffectsSection") as RectTransform;
        if (!activeSection)
        {
            var sectionObj = new GameObject("ActiveEffectsSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
            activeSection = sectionObj.GetComponent<RectTransform>();
            activeSection.SetParent(body, false);
        }
        PlaceBefore(activeSection, section.transform);
        var activeSectionLayout = activeSection.GetComponent<VerticalLayoutGroup>();
        if (!activeSectionLayout)
        {
            activeSectionLayout = activeSection.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        activeSectionLayout.padding = new RectOffset(0, 0, 0, 0);
        activeSectionLayout.spacing = 6f;
        activeSectionLayout.childAlignment = TextAnchor.UpperLeft;
        activeSectionLayout.childForceExpandWidth = true;
        activeSectionLayout.childForceExpandHeight = false;
        activeSectionLayout.childControlWidth = true;
        activeSectionLayout.childControlHeight = true;
        activeSectionLayout.childScaleWidth = false;
        activeSectionLayout.childScaleHeight = false;

        var legacyActive = body.Find("ActiveEffects");
        if (legacyActive && legacyActive.parent != activeSection)
        {
            legacyActive.SetParent(activeSection, false);
        }
        var active = activeSection.Find("ActiveEffects")?.GetComponent<TextMeshProUGUI>();
        if (!active)
        {
            var template = (TextMeshProUGUI)data.FindProperty("synergyHeaderText").objectReferenceValue;
            active = Object.Instantiate(template, activeSection);
            active.name = "ActiveEffects";
        }
        var legacyRule = active.transform.Find("Bottom Rule");
        if (legacyRule)
        {
            Object.DestroyImmediate(legacyRule.gameObject);
        }
        active.transform.SetSiblingIndex(0);
        active.textWrappingMode = TextWrappingModes.Normal;
        active.overflowMode = TextOverflowModes.Overflow;
        active.enableAutoSizing = false;
        active.fontStyle = FontStyles.Normal;
        active.raycastTarget = false;
        active.text = Presentation.PuzzleTooltipActiveEffectsTitle;

        var message = activeSection.Find("ActiveEffectsMessage")?.GetComponent<TextMeshProUGUI>();
        if (!message)
        {
            var template = (TextMeshProUGUI)data.FindProperty("synergySlots").GetArrayElementAtIndex(0).objectReferenceValue;
            message = Object.Instantiate(template, activeSection);
            message.name = "ActiveEffectsMessage";
        }
        message.transform.SetSiblingIndex(1);
        message.color = Presentation.PuzzleTooltipText;
        message.text = Presentation.PuzzleNoActiveEffects;
        message.gameObject.SetActive(false);

        var activeList = activeSection.Find("ActiveEffectsList") as RectTransform;
        if (!activeList)
        {
            var listObj = new GameObject("ActiveEffectsList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            activeList = listObj.GetComponent<RectTransform>();
            activeList.SetParent(activeSection, false);
        }
        activeList.transform.SetSiblingIndex(2);
        var activeListLayout = activeList.GetComponent<VerticalLayoutGroup>();
        if (!activeListLayout)
        {
            activeListLayout = activeList.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        activeListLayout.padding = new RectOffset(0, 0, 0, 0);
        activeListLayout.spacing = 4f;
        activeListLayout.childAlignment = TextAnchor.UpperLeft;
        activeListLayout.childForceExpandWidth = true;
        activeListLayout.childForceExpandHeight = false;
        activeListLayout.childControlWidth = true;
        activeListLayout.childControlHeight = true;
        activeListLayout.childScaleWidth = false;
        activeListLayout.childScaleHeight = false;
        activeList.gameObject.SetActive(false);

        const int ACTIVE_ROW_COUNT = 6;
        var activeRowsProp = data.FindProperty("activeEffectRows");
        activeRowsProp.arraySize = ACTIVE_ROW_COUNT;
        for (int i = 0; i < ACTIVE_ROW_COUNT; i++)
        {
            EnsureActiveEffectRow(activeList, i, active, out var rowLabel, out var rowValue);
            var element = activeRowsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("labelText").objectReferenceValue = rowLabel;
            element.FindPropertyRelative("valueText").objectReferenceValue = rowValue;
        }

        EnsureBottomRule(activeSection, -8f);
        data.FindProperty("activeEffectsSection").objectReferenceValue = activeSection;
        data.FindProperty("activeEffectsText").objectReferenceValue = active;
        data.FindProperty("activeEffectsMessageText").objectReferenceValue = message;
        data.FindProperty("activeEffectsListRoot").objectReferenceValue = activeList;
        ApplyGeneralEffects(data, body, section);
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(_view);
    }

    static void ApplyGeneralEffects(SerializedObject _data, RectTransform _body, GameObject _synergySection)
    {
        var section = _body.Find("GeneralEffectsSection") as RectTransform;
        if (!section)
        {
            section = (RectTransform)Object.Instantiate(_synergySection, _body).transform;
            section.name = "GeneralEffectsSection";
        }
        PlaceBefore(section, _synergySection.transform);

        var header = section.Find("SynergyHeader").GetComponent<TextMeshProUGUI>();
        var list = (RectTransform)section.Find("SynergyList");
        var slots = list.GetComponentsInChildren<TextMeshProUGUI>(true);
        header.text = Presentation.PuzzleTooltipGeneralEffectHeader();
        section.gameObject.SetActive(false);

        _data.FindProperty("generalEffectSection").objectReferenceValue = section.gameObject;
        _data.FindProperty("generalEffectHeaderText").objectReferenceValue = header;
        _data.FindProperty("generalEffectListRoot").objectReferenceValue = list;
        var slotsProperty = _data.FindProperty("generalEffectSlots");
        slotsProperty.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].text = string.Empty;
            slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }
    }

    static void EnsureActiveEffectRow(RectTransform _listRoot, int _index, TextMeshProUGUI _template,
        out TextMeshProUGUI _label, out TextMeshProUGUI _value)
    {
        string rowName = $"Row_{_index}";
        var rowRect = _listRoot.Find(rowName) as RectTransform;
        if (!rowRect)
        {
            var rowObj = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.SetParent(_listRoot, false);
        }
        rowRect.gameObject.SetActive(false);
        var rowLayout = rowRect.GetComponent<HorizontalLayoutGroup>();
        if (!rowLayout)
        {
            rowLayout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childScaleWidth = false;
        rowLayout.childScaleHeight = false;

        _label = EnsureActiveRowText(rowRect, "Label", _template, TextAlignmentOptions.Left, TextWrappingModes.Normal, true);
        _value = EnsureActiveRowText(rowRect, "Value", _template, TextAlignmentOptions.Right, TextWrappingModes.NoWrap, false);
    }

    static TextMeshProUGUI EnsureActiveRowText(RectTransform _row, string _name, TextMeshProUGUI _template,
        TextAlignmentOptions _alignment, TextWrappingModes _wrapping, bool _flexibleWidth)
    {
        var text = _row.Find(_name)?.GetComponent<TextMeshProUGUI>();
        if (!text)
        {
            text = Object.Instantiate(_template, _row);
            text.name = _name;
        }
        text.fontSize = 18.7f;
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = false;
        text.alignment = _alignment;
        text.textWrappingMode = _wrapping;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.text = string.Empty;

        var layoutElement = text.GetComponent<LayoutElement>();
        if (_flexibleWidth)
        {
            if (!layoutElement)
            {
                layoutElement = text.gameObject.AddComponent<LayoutElement>();
            }
            layoutElement.flexibleWidth = 1f;
            layoutElement.layoutPriority = 1;
        }
        else if (layoutElement)
        {
            Object.DestroyImmediate(layoutElement);
        }
        return text;
    }

    static void EnsureBottomRule(RectTransform _parent, float _yOffset)
    {
        var existing = _parent.Find("Bottom Rule") as RectTransform;
        if (existing)
        {
            return;
        }
        var ruleObj = new GameObject("Bottom Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        var ruleRect = ruleObj.GetComponent<RectTransform>();
        ruleRect.SetParent(_parent, false);
        ruleRect.anchorMin = new Vector2(0f, 0f);
        ruleRect.anchorMax = new Vector2(1f, 0f);
        ruleRect.pivot = new Vector2(0.5f, 0.5f);
        ruleRect.anchoredPosition = new Vector2(0f, _yOffset);
        ruleRect.sizeDelta = new Vector2(0f, 1f);

        var image = ruleObj.GetComponent<Image>();
        image.color = Presentation.PuzzleTooltipDivider;
        image.raycastTarget = false;

        var layout = ruleObj.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;
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
