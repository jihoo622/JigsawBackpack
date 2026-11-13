using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class ItemEditorWindow
{
    [SerializeField] bool showReferences = true;
    ItemEditorSource referenceSource;
    string referencedPuzzle, referencedSynergy, referencedId;
    readonly List<string> directPieceReferences = new List<string>();
    readonly List<string> tagReferences = new List<string>();

    void DrawPieceReferences(JObject _piece)
    {
        string id = (string)_piece["id"];
        if (referenceSource != source || referencedPuzzle != draft.puzzle || referencedSynergy != draft.synergy || referencedId != id)
        {
            directPieceReferences.Clear();
            tagReferences.Clear();
            var tags = new HashSet<string>(_piece["tags"].Values<string>(), StringComparer.Ordinal);
            foreach (var pair in ItemEditorStore.DraftSources(source, draft).OrderBy(_pair => _pair.Key.Name, StringComparer.Ordinal))
            {
                var table = JObject.Parse(pair.Value);
                if (pair.Key == typeof(PuzzleTableData))
                {
                    foreach (var piece in table["pieces"].OfType<JObject>())
                    {
                        if ((string)piece["adultPieceId"] == id)
                        {
                            directPieceReferences.Add("PuzzleTableData.pieces[" + (string)piece["id"] + "].adultPieceId");
                        }
                    }
                    table.Remove("pieces");
                    table.Remove("tagPresentations");
                }
                CollectItemReferences(table, pair.Key.Name, id, tags, false);
            }
            directPieceReferences.Sort(StringComparer.Ordinal);
            tagReferences.Sort(StringComparer.Ordinal);
            referenceSource = source;
            referencedPuzzle = draft.puzzle;
            referencedSynergy = draft.synergy;
            referencedId = id;
        }
        EditorGUILayout.Space();
        if ((bool?)_piece["excludeFromMarket"] == true && directPieceReferences.Any(_reference =>
            _reference.Contains(".stockProfiles[") || _reference.Contains(".market.startingPlacements[")))
        {
            EditorGUILayout.HelpBox("시장 제외 품목에 기존 시장 참조가 있습니다. 아래 참조처의 원본 테이블에서 정합성을 맞춰주세요.", MessageType.Error);
        }
        if ((bool?)_piece["exemptFromModification"] == true && directPieceReferences.Any(_reference =>
            _reference.StartsWith(nameof(PuzzleCityActionTableData), StringComparison.Ordinal)))
        {
            EditorGUILayout.HelpBox("개조 매핑 검사 제외를 켜도 기존 마을 행동의 입력·산출 참조가 남습니다. 등록된 변환은 아래에서 확인하세요.", MessageType.Warning);
        }
        MarkValidationField("references");
        showReferences = EditorGUILayout.Foldout(showReferences,
            "참조 현황 · ID " + directPieceReferences.Count + " / 태그 " + tagReferences.Count, true);
        if (!showReferences)
        {
            return;
        }
        EditorGUILayout.HelpBox("현재 초안 기준의 읽기 전용 참조입니다. 도시 재고·시작 배치·도축·개조 등록은 해당 원본 테이블에서 편집하세요.", MessageType.None);
        foreach (string reference in directPieceReferences)
        {
            DrawReferenceLine(reference);
        }
        if (directPieceReferences.Count == 0)
        {
            EditorGUILayout.LabelField("이 품목 ID를 참조하는 항목이 없습니다.", EditorStyles.wordWrappedMiniLabel);
        }
        if (tagReferences.Count > 0)
        {
            GUILayout.Label("현재 태그로 연결되는 가격·조건", EditorStyles.boldLabel);
            foreach (string reference in tagReferences)
            {
                DrawReferenceLine(reference);
            }
        }
    }

    static void DrawReferenceLine(string _reference)
    {
        EditorGUILayout.SelectableLabel(_reference, EditorStyles.wordWrappedMiniLabel,
            GUILayout.Height(Mathf.Max(20f, EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(_reference), 310f))));
    }

    void CollectItemReferences(JToken _token, string _path, string _id, HashSet<string> _tags, bool _tagContext)
    {
        if (_token is JObject row)
        {
            string rowId = (string)row["id"];
            string path = string.IsNullOrEmpty(rowId) ? _path : _path + "(" + rowId + ")";
            foreach (var property in row.Properties())
            {
                bool tagContext = property.Name.EndsWith("Tag", StringComparison.Ordinal) ||
                    property.Name.EndsWith("Tags", StringComparison.Ordinal) || property.Name == "tag" ||
                    (property.Name == "value" && (rowId ?? "").EndsWith("Tag", StringComparison.Ordinal));
                CollectItemReferences(property.Value, path + "." + property.Name, _id, _tags, tagContext);
            }
        }
        else if (_token is JArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                CollectItemReferences(array[index], _path + "[" + index + "]", _id, _tags, _tagContext);
            }
        }
        else if (_token.Type == JTokenType.String)
        {
            string value = (string)_token;
            if (value == _id)
            {
                directPieceReferences.Add(_path);
            }
            if (_tagContext)
            {
                foreach (string tag in value.Split(',').Select(_tag => _tag.Trim()).Where(_tags.Contains))
                {
                    tagReferences.Add(_path + " → " + tag);
                }
            }
        }
    }
}
