using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed partial class ItemEditorWindow
{
    string validatedPuzzle, validatedSynergy, validatedIcons;
    ItemEditorSource validatedSource;
    ItemEditorDefinitionReview definitionReview;
    readonly Dictionary<string, float> validationAnchors = new Dictionary<string, float>(StringComparer.Ordinal);
    string pendingValidationField;
    float validationContentStart;

    ItemEditorDefinitionReview DefinitionReview()
    {
        string icons = IconsJson(draft.icons);
        if (definitionReview == null || validatedSource != source || validatedPuzzle != draft.puzzle ||
            validatedSynergy != draft.synergy || validatedIcons != icons)
        {
            definitionReview = ItemEditorValidation.Review(source, draft);
            validatedSource = source;
            validatedPuzzle = draft.puzzle;
            validatedSynergy = draft.synergy;
            validatedIcons = icons;
        }
        return definitionReview;
    }

    void DrawValidationIssues(IReadOnlyList<ItemEditorIssue> _issues)
    {
        var linkStyle = new GUIStyle(EditorStyles.miniButton)
        {
            wordWrap = true
        };
        foreach (bool existing in new[] { false, true })
        {
            var issues = _issues.Where(_issue => _issue.existing == existing).ToArray();
            if (issues.Length == 0)
            {
                continue;
            }
            GUILayout.Label((existing ? "원본에도 있는 오류" : "초안에서 추가된 오류") + " (" + issues.Length + ")", EditorStyles.boldLabel);
            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox("[" + issue.stablePath + "] " + issue.issue.Message, MessageType.Error);
                if (!string.IsNullOrEmpty(issue.assetPath) && GUILayout.Button("인벤토리 에셋 선택", EditorStyles.miniButton))
                {
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(issue.assetPath);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                foreach (string id in issue.pieceIds.Where(_id => Pieces.Any(_piece => (string)_piece["id"] == _id)))
                {
                    string field = string.IsNullOrEmpty(issue.field) ? "references" : issue.field;
                    string label = "필드로 이동 · " + id + " / " + field;
                    if (GUILayout.Button(label, linkStyle,
                        GUILayout.Height(Mathf.Max(22f, linkStyle.CalcHeight(new GUIContent(label), 300f)))))
                    {
                        SelectValidationIssue(issue, id, field);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
    }

    void SelectValidationIssue(ItemEditorIssue _issue, string _id, string _field)
    {
        selectedId = _id;
        search = "";
        showEffectTags = true;
        showGrowth = true;
        showBreeding = true;
        showReferences = true;
        showAdvanced = true;
        showCellDetails = true;
        effectSearch = "";
        var cell = Regex.Match(_issue.issue.Path, @"\.cells\[(\d+)\]");
        if (cell.Success)
        {
            selectedCellIndex = int.Parse(cell.Groups[1].Value);
        }
        pendingValidationField = _field == "slotCount" ? "cells" : _field;
        shapeValidationDirty = true;
        GUI.FocusControl(null);
        Repaint();
    }

    void BeginValidationFields()
    {
        if (Event.current.type == EventType.Repaint)
        {
            validationAnchors.Clear();
        }
        validationContentStart = GUILayoutUtility.GetRect(0f, 0f).y;
    }

    void MarkValidationField(string _field)
    {
        var marker = GUILayoutUtility.GetRect(0f, 0f);
        if (Event.current.type == EventType.Repaint)
        {
            validationAnchors[_field] = marker.y - validationContentStart;
        }
        GUI.SetNextControlName("ItemEditor." + _field);
    }

    void CompleteValidationFocus()
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(pendingValidationField))
        {
            return;
        }
        string field = validationAnchors.ContainsKey(pendingValidationField) ? pendingValidationField : "advanced";
        if (validationAnchors.TryGetValue(field, out float offset))
        {
            detailsScroll.y = Mathf.Max(0f, offset);
            GUI.FocusControl("ItemEditor." + field);
            pendingValidationField = null;
            Repaint();
        }
    }
}
