using UnityEditor;

// GridLayoutGroup 기본 인스펙터는 하위 클래스 필드를 그리지 않는다. 칸 크기·열 제약은 컴포넌트가 계산하므로 숨긴다.
[CustomEditor(typeof(FlexibleGridLayoutGroup))]
[CanEditMultipleObjects]
public sealed class FlexibleGridLayoutGroupEditor : Editor
{
    static readonly string[] PROPERTIES =
    {
        "m_Padding", "m_Spacing", "m_StartCorner", "m_StartAxis", "m_ChildAlignment",
        "minCellWidth", "maxColumns", "cellHeight", "fillParentHeight", "maxCellHeight",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        for (int i = 0; i < PROPERTIES.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(PROPERTIES[i]);
            if (property.name == "maxCellHeight" && !serializedObject.FindProperty("fillParentHeight").boolValue)
            {
                continue;
            }
            EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
        FlexibleGridLayoutGroup grid = (FlexibleGridLayoutGroup)target;
        EditorGUILayout.HelpBox("열 " + grid.Columns + " · 칸 " + grid.cellSize.x.ToString("0") + "×" + grid.cellSize.y.ToString("0"), MessageType.None);
    }
}
