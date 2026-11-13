using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class ItemEditorWindow
{
    [SerializeField] string effectSearch = "";
    [SerializeField] string tagSearch = "";
    [SerializeField] bool showEffectTags;
    string editorBasicSource;
    Dictionary<string, string> editorTexts;

    string SourceJson<T>()
    {
        return source.files.Single(_file => _file.tableType == typeof(T).AssemblyQualifiedName).source;
    }

    JObject SourceTable<T>()
    {
        if (typeof(T) == typeof(PuzzleSynergyTableData))
        {
            return synergyDocument;
        }
        return JObject.Parse(SourceJson<T>());
    }

    string EditorText(string _key)
    {
        string current = source.files.Single(_file => _file.tableType == typeof(BasicDataTableData).AssemblyQualifiedName).source;
        if (editorTexts == null || editorBasicSource != current)
        {
            editorTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in JObject.Parse(current)["groups"].OfType<JObject>())
            {
                foreach (var row in (group["texts"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    editorTexts.Add((string)group["id"] + "." + (string)row["id"], (string)row["value"]);
                }
            }
            editorBasicSource = current;
        }
        if (editorTexts.TryGetValue(_key, out string value))
        {
            return value;
        }
        throw new InvalidOperationException("기초데이터 문구가 없습니다: " + _key);
    }

    void DrawEffectFields(JObject _piece)
    {
        EditorGUILayout.Space();
        MarkValidationField("synergyEffects");
        MarkValidationField("preservationEffect");
        GUILayout.Label("품목별 효과", EditorStyles.boldLabel);
        var catalogJson = SourceTable<PuzzleSynergyTableData>();
        var catalog = catalogJson.ToObject<PuzzleSynergyTableData>();
        bool legacy = Pieces.Any(_entry => ItemEditorEffectMigration.IsMissing(_entry["synergyEffects"]) ||
            ItemEditorEffectMigration.IsMissing(_entry["preservationEffect"]));
        if (legacy)
        {
            EditorGUILayout.HelpBox("명시적 효과 계약이 없는 품목이 있습니다. 태그로 자동 적용하지 않으며, 기존 효과를 이행한 후 저장해야 합니다.", MessageType.Warning);
            if (GUILayout.Button("기존 태그 효과를 전체 초안에 이행"))
            {
                bool migrated = false;
                Change("품목 효과 계약 이행", () =>
                {
                    draft.effectMigrationOriginal = Json(document);
                    ItemEditorEffectMigration.Migrate(document, catalogJson);
                    draft.effectMigrationBaseline = Json(document);
                    migrated = true;
                });
                if (migrated)
                {
                    review = ItemEditorStore.Review(source, draft);
                }
                GUIUtility.ExitGUI();
            }
        }
        DrawEffectTags(_piece);
        var links = _piece["synergyEffects"] as JArray;
        var preservation = _piece["preservationEffect"] as JObject;
        if (links == null || preservation == null)
        {
            return;
        }
        var tags = _piece["tags"].Values<string>().ToArray();
        EditorGUILayout.LabelField("가치 시너지 · 역할을 각각 선택", EditorStyles.boldLabel);
        MarkValidationField("synergyEffects");
        effectSearch = EditorGUILayout.TextField("규칙 검색", effectSearch);
        foreach (var rule in catalog.rules.OrderBy(_rule => _rule.id, StringComparer.Ordinal))
        {
            if (!string.IsNullOrEmpty(effectSearch) && rule.id.IndexOf(effectSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                rule.description.IndexOf(effectSearch, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(rule.id, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(rule.description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("대상 조건", string.IsNullOrEmpty(rule.excludedTargetTag)
                    ? rule.targetTag : "제외: " + rule.excludedTargetTag, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("이웃 조건", rule.neighborTag +
                    (string.IsNullOrEmpty(rule.excludedNeighborTag) ? "" : " / 제외: " + rule.excludedNeighborTag), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(rule.scope + " / " + rule.spatialRange + " / " + rule.multiplierPercent + "%", EditorStyles.miniLabel);
                var entry = links.OfType<JObject>().FirstOrDefault(_link => (string)_link["ruleId"] == rule.id);
                bool target = (bool?)entry?["target"] ?? false;
                bool sourceRole = (bool?)entry?["source"] ?? false;
                string targetLabel = "받는 대상 · " + rule.targetDescription;
                string sourceLabel = "주는 이웃 · " + rule.sourceDescription;
                bool nextTarget = EditorGUILayout.ToggleLeft(targetLabel, target, EditorStyles.wordWrappedLabel,
                    GUILayout.Height(Mathf.Max(20f, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(targetLabel), 300f))));
                bool nextSource = EditorGUILayout.ToggleLeft(sourceLabel, sourceRole, EditorStyles.wordWrappedLabel,
                    GUILayout.Height(Mathf.Max(20f, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(sourceLabel), 300f))));
                if (target != nextTarget || sourceRole != nextSource)
                {
                    Change("시너지 역할 선택", () =>
                    {
                        if (links.OfType<JObject>().Count(_link => (string)_link["ruleId"] == rule.id) > 1)
                        {
                            throw new InvalidOperationException("중복 규칙 연결을 원본에서 정리한 뒤 다시 읽으세요: " + rule.id);
                        }
                        var link = entry;
                        if (link == null)
                        {
                            link = new JObject
                            {
                                ["ruleId"] = rule.id
                            };
                            links.Add(link);
                        }
                        link["target"] = nextTarget;
                        link["source"] = nextSource;
                        if (!nextTarget && !nextSource)
                        {
                            link.Remove();
                        }
                    });
                }
                if (!PuzzlePieceEffects.EligibleTarget(tags, rule, catalog.animals.wasteTag))
                {
                    EditorGUILayout.HelpBox("현재 태그는 대상 조건을 만족하지 않습니다(배설물 면역 포함).",
                        nextTarget ? MessageType.Error : MessageType.None);
                }
                if (!PuzzlePieceEffects.EligibleSource(tags, rule))
                {
                    EditorGUILayout.HelpBox("현재 태그는 이웃 조건을 만족하지 않습니다.",
                        nextSource ? MessageType.Error : MessageType.None);
                }
            }
        }
        EditorGUILayout.HelpBox("상대 품목도 같은 규칙의 반대 역할과 태그 조건을 충족해야 적용됩니다. 양쪽 역할 해제는 효과 없음이며 공통 배율은 바꾸지 않습니다.", MessageType.None);
        GUILayout.Label("인접 음식의 신선 유지", EditorStyles.boldLabel);
        bool enabled = (bool?)preservation["enabled"] ?? false;
        MarkValidationField("preservationEffect");
        int reduction = (int?)preservation["reductionPercent"] ?? -1;
        bool nextEnabled = EditorGUILayout.Toggle("신선 효과 부여", enabled);
        if (nextEnabled != enabled)
        {
            Change("신선 효과 선택", () =>
            {
                preservation["enabled"] = nextEnabled;
                if (!nextEnabled)
                {
                    preservation["reductionPercent"] = 0;
                }
            });
            enabled = nextEnabled;
            reduction = (int)preservation["reductionPercent"];
        }
        using (new EditorGUI.DisabledScope(!enabled))
        {
            int nextReduction = EditorGUILayout.DelayedIntField("유통기한 소모 감소율 (%)", reduction);
            if (enabled && nextReduction != reduction)
            {
                Change("품목별 신선 감소율", () => preservation["reductionPercent"] = nextReduction);
            }
        }
        EditorGUILayout.LabelField("필수 태그: " + catalog.freshness.refrigerationTag + " · 허용 범위 0~100%", EditorStyles.wordWrappedMiniLabel);
        if (enabled)
        {
            EditorGUILayout.LabelField(Presentation.PuzzlePreservationDescription((int)preservation["reductionPercent"], EditorText), EditorStyles.wordWrappedLabel);
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("공통 객차 냉장 감소율 (%)", catalog.freshness.wagonRefrigerationReductionPercent);
            EditorGUILayout.IntField("공통 신선 정산 주기 (시간)", catalog.freshness.settlementIntervalHours);
            EditorGUILayout.IntField("공통 가치 정산 주기 (시간)", catalog.activationIntervalHours);
        }
        var errors = new List<string>();
        new GameTableRules(errors).ValidatePieceEffects(_piece.ToObject<PuzzlePieceData>(), catalog.rules,
            catalog.freshness, catalog.animals.wasteTag, (string)_piece["id"]);
        foreach (string error in errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    void DrawEffectTags(JObject _piece)
    {
        MarkValidationField("tags");
        showEffectTags = EditorGUILayout.Foldout(showEffectTags, "현재 태그와 효과 조건", true);
        if (!showEffectTags)
        {
            return;
        }
        tagSearch = EditorGUILayout.TextField("태그 검색", tagSearch);
        var tags = (JArray)_piece["tags"];
        var custom = CustomTagPresentations();
        var known = SourceTable<PuzzleTableData>()["pieces"].OfType<JObject>()
            .SelectMany(_entry => _entry["tags"].Values<string>())
            .Concat(Pieces.SelectMany(_entry => _entry["tags"].Values<string>()))
            .Concat(custom.Select(_tag => _tag.id)).Distinct(StringComparer.Ordinal)
            .OrderBy(_tag => _tag, StringComparer.Ordinal);
        foreach (string tag in known)
        {
            string label = Presentation.PuzzleTagLabel(tag, EditorText, custom) + " (" + tag + ")";
            if (!string.IsNullOrEmpty(tagSearch) && label.IndexOf(tagSearch, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            bool selected = tags.Values<string>().Contains(tag);
            bool next = EditorGUILayout.ToggleLeft(label, selected);
            if (selected != next)
            {
                Change("효과 조건 태그", () =>
                {
                    if (next)
                    {
                        tags.Add(tag);
                    }
                    else
                    {
                        foreach (var entry in tags.Where(_entry => (string)_entry == tag).ToArray())
                        {
                            entry.Remove();
                        }
                    }
                });
            }
        }
        DrawNewTagFields(_piece);
        EditorGUILayout.HelpBox("태그 변경은 상점 가격·개조·성장 조건에 영향을 줍니다. 전체 오류와 참조 현황을 확인하세요.", MessageType.None);
    }
}
