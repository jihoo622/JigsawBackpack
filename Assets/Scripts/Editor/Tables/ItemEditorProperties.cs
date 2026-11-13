using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class ItemEditorWindow
{
    [SerializeField] bool showGrowth = true;
    [SerializeField] bool showBreeding = true;
    [SerializeField] bool showAdvanced;
    [SerializeField] bool showNewTag;
    [SerializeField] bool showDefinitionErrors = true;
    [SerializeField] string newTagId = "";
    [SerializeField] string newTagName = "";
    [SerializeField] Color newTagColor = Color.white;

    void DrawIntProperty(JObject _owner, string _field, string _label, int _missing = 0)
    {
        int current = (int?)_owner[_field] ?? _missing;
        if (ReferenceEquals(_owner, Selected))
        {
            MarkValidationField(_field);
        }
        int next = EditorGUILayout.DelayedIntField(_label, current);
        if (next != current)
        {
            Change(_label, () => _owner[_field] = next);
        }
    }

    void DrawDescriptionProperty(JObject _owner, string _field, string _label)
    {
        EditorGUILayout.LabelField(_label);
        string current = (string)_owner[_field] ?? "";
        if (ReferenceEquals(_owner, Selected))
        {
            MarkValidationField(_field);
        }
        string next = EditorGUILayout.TextArea(current, GUILayout.MinHeight(55f));
        if (next != current)
        {
            Change(_label, () => _owner[_field] = next);
        }
    }

    void DrawPieceSelector(JObject _owner, string _field, string _label)
    {
        string current = (string)_owner[_field] ?? "";
        var ids = new List<string> { "" };
        var labels = new List<string> { "선택 없음" };
        foreach (var piece in Pieces.OrderBy(_piece => (string)_piece["id"], StringComparer.Ordinal))
        {
            ids.Add((string)piece["id"]);
            labels.Add((string)piece["displayName"] + " (" + (string)piece["id"] + ")");
        }
        if (!ids.Contains(current))
        {
            ids.Add(current);
            labels.Add("누락된 품목 · " + current);
        }
        int index = ids.IndexOf(current);
        if (ReferenceEquals(_owner, Selected))
        {
            MarkValidationField(_field);
        }
        int next = EditorGUILayout.Popup(_label, index, labels.ToArray());
        if (next != index)
        {
            Change(_label, () => _owner[_field] = ids[next]);
        }
    }

    void DrawDefinitionFields(JObject _piece)
    {
        bool exempt = (bool?)_piece["exemptFromModification"] ?? false;
        MarkValidationField("exemptFromModification");
        bool nextExempt = EditorGUILayout.Toggle("개조 매핑 검사에서 제외", exempt);
        if (nextExempt != exempt)
        {
            Change("개조 매핑 제외", () => _piece["exemptFromModification"] = nextExempt);
        }
        EditorGUILayout.HelpBox("개조 제외는 매핑 누락 검사만 면제합니다. 이미 등록된 변환은 참조 현황에 표시됩니다.", MessageType.None);
        EditorGUILayout.Space();
        GUILayout.Label("내구도·유통기한", EditorStyles.boldLabel);
        DrawIntProperty(_piece, "durability", "시작 내구도", -1);
        var group = SourceTable<BasicDataTableData>()["groups"].OfType<JObject>()
            .Single(_group => (string)_group["id"] == "rules.durability");
        var maximum = group["numbers"].OfType<JObject>().Single(_row => (string)_row["id"] == "maximum")["value"];
        string foodTag = EditorText("rules.items.durabilityTag");
        bool food = _piece["tags"].Values<string>().Contains(foodTag);
        EditorGUILayout.LabelField("모든 품목의 저장 범위 1~" + maximum + " · 신선도 표시 대상 " + foodTag,
            EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(!food))
        {
            DrawIntProperty(_piece, "shelfLifeHours", "유통기한 (시간)");
        }
        EditorGUILayout.HelpBox(food ? "0시간은 시간에 따른 유통기한 소모가 없습니다. 양수이면 품목별 신선 유지 수치가 적용됩니다."
            : foodTag + " 태그가 없어 유통기한 소모·신선도 표시가 적용되지 않습니다. 저장된 수치는 유지됩니다.", MessageType.None);
    }

    void DrawGrowthFields(JObject _piece)
    {
        EditorGUILayout.Space();
        showGrowth = EditorGUILayout.Foldout(showGrowth, "성장", true);
        if (!showGrowth)
        {
            return;
        }
        bool baby = _piece["tags"].Values<string>().Contains(PuzzleItemDefinition.BABY_TAG);
        bool configured = !string.IsNullOrEmpty((string)_piece["adultPieceId"]) ||
            !string.IsNullOrEmpty((string)_piece["growthDescription"]) ||
            ((int?)_piece["growthHours"] ?? 0) != 0 || ((int?)_piece["growthBlockedHours"] ?? 0) != 0;
        using (new EditorGUI.DisabledScope(!baby && !configured))
        {
            DrawPieceSelector(_piece, "adultPieceId", "성체 품목");
            DrawIntProperty(_piece, "growthHours", "성장 완료 (시간)");
            DrawIntProperty(_piece, "growthBlockedHours", "완료 전 공간 확인 (시간)");
            DrawDescriptionProperty(_piece, "growthDescription", "성장 설명");
        }
        EditorGUILayout.HelpBox("성장 설정에는 Baby 태그와 Baby가 없는 성체가 필요합니다. 성장 시간 > 0, 공간 확인 시간은 1~성장 시간입니다. 설명에 {0} 성장 시간, {1} 성체 이름, {2} 대기 시간 인자를 모두 넣으세요.", MessageType.None);
        if (!baby)
        {
            EditorGUILayout.HelpBox(configured ? "Baby 태그 없이 성장 설정이 있습니다. 태그를 추가하거나 성장 필드를 비워주세요."
                : "Baby 태그를 선택하면 성장 필드를 편집할 수 있습니다.", configured ? MessageType.Error : MessageType.Info);
        }
    }

    void DrawBreedingFields(JObject _piece)
    {
        EditorGUILayout.Space();
        MarkValidationField("breeding");
        showBreeding = EditorGUILayout.Foldout(showBreeding, "번식", true);
        if (!showBreeding)
        {
            return;
        }
        var animals = (JObject)synergyDocument["animals"];
        var breeding = animals["breeding"] as JArray;
        string id = (string)_piece["id"];
        var related = (breeding ?? new JArray()).OfType<JObject>().Where(_rule =>
            (string)_rule["parentPieceId"] == id || (string)_rule["offspringPieceId"] == id).ToArray();
        foreach (var rule in related)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("PuzzleSynergyTableData.animals.breeding", EditorStyles.wordWrappedMiniLabel);
                DrawPieceSelector(rule, "parentPieceId", "부모 품목");
                DrawPieceSelector(rule, "offspringPieceId", "자식 품목");
                DrawIntProperty(rule, "offspringCount", "자식 수량");
                DrawDescriptionProperty(rule, "description", "번식 설명");
                if (GUILayout.Button("이 번식 규칙 제거"))
                {
                    Change("번식 규칙 제거", () => rule.Remove());
                    GUIUtility.ExitGUI();
                }
            }
        }
        var tags = _piece["tags"].Values<string>().ToArray();
        bool parent = tags.Contains((string)animals["animalTag"]) && !tags.Contains(PuzzleItemDefinition.BABY_TAG);
        bool hasRule = (breeding ?? new JArray()).OfType<JObject>().Any(_rule => (string)_rule["parentPieceId"] == id);
        using (new EditorGUI.DisabledScope(!parent || hasRule))
        {
            if (GUILayout.Button("이 품목의 번식 규칙 추가"))
            {
                Change("번식 규칙 추가", () =>
                {
                    if (breeding == null)
                    {
                        breeding = new JArray();
                        animals["breeding"] = breeding;
                    }
                    breeding.Add(new JObject
                    {
                        ["parentPieceId"] = id,
                        ["offspringPieceId"] = "",
                        ["offspringCount"] = 0,
                        ["description"] = ""
                    });
                });
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.HelpBox("부모는 Baby가 없는 " + (string)animals["animalTag"] + " 품목이며 부모 ID당 한 규칙이 필요합니다. 자식도 동물 태그가 필요하고 수량은 양수입니다. 설명에는 {0} 번식 시간, {1} 자식 이름, {2} 수량을 넣으세요.", MessageType.None);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("공통 번식 시간 (시간)", (int)animals["breedingHours"]);
            EditorGUILayout.IntField("공통 완료 전 대기 (시간)", (int)animals["completionHoldHours"]);
            EditorGUILayout.IntField("공통 배설 주기 (일)", (int)animals["wasteIntervalDays"]);
            EditorGUILayout.IntField("공통 배설 수량", (int)animals["wasteCountPerAnimal"]);
            EditorGUILayout.TextField("공통 배설 품목", (string)animals["wastePieceId"]);
            EditorGUILayout.LabelField("공통 배설 설명 (읽기 전용)");
            EditorGUILayout.TextArea((string)animals["wasteDescription"] ?? "", GUILayout.MinHeight(55f));
        }
    }

    PuzzleTagPresentationData[] CustomTagPresentations()
    {
        return (document["tagPresentations"] as JArray ?? new JArray()).ToObject<PuzzleTagPresentationData[]>()
            .Where(_tag => _tag != null).ToArray();
    }

    void DrawNewTagFields(JObject _piece)
    {
        showNewTag = EditorGUILayout.Foldout(showNewTag, "새 태그 등록", true);
        if (!showNewTag)
        {
            return;
        }
        newTagId = EditorGUILayout.TextField("태그 ID", newTagId);
        newTagName = EditorGUILayout.TextField("한글 표시명", newTagName);
        newTagColor = EditorGUILayout.ColorField(new GUIContent("배경색"), newTagColor, true, true, false);
        var entry = new PuzzleTagPresentationData
        {
            id = newTagId,
            displayName = newTagName,
            r = newTagColor.r, g = newTagColor.g, b = newTagColor.b, a = newTagColor.a
        };
        var errors = new List<string>();
        new GameTableRules(errors).ValidateTagPresentation(entry, "새 태그");
        bool exists = Pieces.Concat(SourceTable<PuzzleTableData>()["pieces"].OfType<JObject>())
            .SelectMany(_entry => _entry["tags"].Values<string>()).Concat(CustomTagPresentations().Select(_tag => _tag.id))
            .Any(_tag => string.Equals(_tag, newTagId, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            errors.Add("이미 사용 중인 태그 ID입니다.");
        }
        foreach (string error in errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
        using (new EditorGUI.DisabledScope(errors.Count != 0))
        {
            if (GUILayout.Button("태그 등록 후 이 품목에 선택"))
            {
                Change("새 태그 등록", () =>
                {
                    var registry = document["tagPresentations"] as JArray;
                    if (registry == null)
                    {
                        registry = new JArray();
                        document["tagPresentations"] = registry;
                    }
                    registry.Add(JObject.FromObject(entry));
                    ((JArray)_piece["tags"]).Add(entry.id);
                });
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.HelpBox("표시 정의는 PuzzleTableData.tagPresentations에 저장됩니다. 새 태그의 가격·시너지·개조 참조는 전체 테이블 검토로 확인합니다.", MessageType.None);
    }

    void RenameDraftPieceReferences(string _before, string _after)
    {
        foreach (var piece in Pieces.Where(_piece => (string)_piece["adultPieceId"] == _before))
        {
            piece["adultPieceId"] = _after;
        }
        foreach (var rule in (synergyDocument["animals"]?["breeding"] as JArray ?? new JArray()).OfType<JObject>())
        {
            foreach (string key in new[] { "parentPieceId", "offspringPieceId" })
            {
                if ((string)rule[key] == _before)
                {
                    rule[key] = _after;
                }
            }
        }
    }

    void DrawAdvancedFields(JObject _piece)
    {
        EditorGUILayout.Space();
        MarkValidationField("advanced");
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "고급 속성·추가 필드", true);
        if (!showAdvanced)
        {
            return;
        }
        EditorGUILayout.LabelField("slotCount는 셀 수에서 자동 산출합니다. cells·아이콘 크기/오프셋은 모양 도구에서 편집합니다.", EditorStyles.wordWrappedLabel);
        var handled = new HashSet<string>(new[]
        {
            "id", "displayName", "description", "baseValue", "visualKey", "tags", "durability", "shelfLifeHours",
            "excludeFromMarket", "exemptFromModification", "adultPieceId", "growthDescription", "growthHours",
            "growthBlockedHours", "synergyEffects", "preservationEffect", "slotCount", "cells",
            "iconWidth", "iconHeight", "iconOffsetX", "iconOffsetY"
        }, StringComparer.Ordinal);
        var defaults = JObject.FromObject(new PuzzlePieceData());
        foreach (string key in defaults.Properties().Select(_property => _property.Name)
            .Union(_piece.Properties().Select(_property => _property.Name), StringComparer.Ordinal)
            .Where(_key => !handled.Contains(_key)).OrderBy(_key => _key, StringComparer.Ordinal).ToArray())
        {
            var field = typeof(PuzzlePieceData).GetField(key);
            var token = _piece[key];
            EditorGUILayout.LabelField(key, field == null ? "원본 추가 필드 · 읽기 전용" : "정의 필드 · JSON 값");
            if (field == null)
            {
                string raw = token.ToString(Formatting.Indented);
                EditorGUILayout.SelectableLabel(raw, EditorStyles.textArea,
                    GUILayout.Height(Mathf.Max(35f, EditorStyles.textArea.CalcHeight(new GUIContent(raw), 310f))));
                continue;
            }
            if (token == null)
            {
                EditorGUILayout.LabelField("원본에서 생략된 필드", EditorStyles.miniLabel);
                if (GUILayout.Button(key + " 기본 표현 추가"))
                {
                    Change("정의 필드 추가", () => _piece[key] = defaults[key].DeepClone());
                }
                continue;
            }
            string current = token.ToString(Formatting.None);
            MarkValidationField(key);
            string next = EditorGUILayout.DelayedTextField(current);
            if (current != next)
            {
                Change("추가 정의 필드", () =>
                {
                    var value = JToken.Parse(next);
                    value.ToObject(field.FieldType);
                    _piece[key] = value;
                });
            }
        }
    }

    void DrawDefinitionErrors()
    {
        var validation = DefinitionReview();
        EditorGUILayout.Space();
        showDefinitionErrors = EditorGUILayout.Foldout(showDefinitionErrors, "품목·참조·이미지 오류 (" + validation.issues.Count + ")", true);
        if (showDefinitionErrors)
        {
            DrawValidationIssues(validation.issues);
        }
    }
}
