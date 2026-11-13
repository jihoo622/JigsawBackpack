using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed partial class ItemEditorWindow : EditorWindow
{
    const float ITEM_LIST_WIDTH = 250f;
    const float ITEM_ROW_MIN_HEIGHT = 56f;
    [SerializeField] ItemEditorDraft draft;
    [SerializeField] ItemEditorSource source;
    [SerializeField] string originalIcons;
    [SerializeField] string selectedId;
    [SerializeField] string search = "";
    JObject document;
    JObject synergyDocument;
    Vector2 listScroll, detailsScroll, rawScroll, reviewScroll;
    string message;
    ItemEditorSaveReview review;

    IEnumerable<JObject> Pieces => document?["pieces"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>();
    JObject Selected => Pieces.FirstOrDefault(_piece => (string)_piece["id"] == selectedId);
    bool Dirty => draft && source != null &&
        (draft.puzzle != source.puzzle || draft.synergy != SourceJson<PuzzleSynergyTableData>() ||
        IconsJson(draft.icons) != originalIcons);

    [MenuItem("Tools/MyJG/Item Editor")]
    public static void Open()
    {
        GetWindow<ItemEditorWindow>("Item Editor").minSize = new Vector2(1080f, 660f);
    }

    void OnEnable()
    {
        minSize = new Vector2(1080f, 660f);
        saveChangesMessage = "아이템 초안을 저장할까요? 품목·번식 테이블과 모든 인벤토리의 Sprite 참조를 함께 반영합니다.";
        Undo.undoRedoPerformed += RestoreDraft;
        if (draft && source != null)
        {
            RestoreDraft();
        }
        else
        {
            Reload();
        }
    }

    void OnDisable()
    {
        ReleaseCanvasDrag();
        Undo.undoRedoPerformed -= RestoreDraft;
    }

    void OnDestroy()
    {
        if (draft)
        {
            Undo.ClearUndo(draft);
            DestroyImmediate(draft);
        }
    }

    static string Json(JObject _value)
    {
        return _value.ToString(Formatting.Indented) + "\n";
    }

    static string IconsJson(IEnumerable<ItemEditorIcon> _icons)
    {
        return JsonConvert.SerializeObject(_icons.OrderBy(_icon => _icon.pieceId, StringComparer.Ordinal));
    }

    static List<ItemEditorIcon> CopyIcons(IEnumerable<ItemEditorIcon> _icons)
    {
        return _icons.Select(_icon => new ItemEditorIcon
        {
            pieceId = _icon.pieceId,
            spriteGuid = _icon.spriteGuid,
            spriteLocalId = _icon.spriteLocalId
        }).ToList();
    }

    void Reload()
    {
        try
        {
            var next = ItemEditorStore.Capture();
            if (!draft)
            {
                draft = CreateInstance<ItemEditorDraft>();
                draft.hideFlags = HideFlags.HideAndDontSave;
            }
            Undo.ClearUndo(draft);
            source = next;
            draft.puzzle = source.puzzle;
            draft.synergy = SourceJson<PuzzleSynergyTableData>();
            draft.effectMigrationBaseline = null;
            draft.effectMigrationOriginal = null;
            draft.icons = CopyIcons(source.icons);
            originalIcons = IconsJson(draft.icons);
            message = null;
            RestoreDraft();
        }
        catch (Exception exception)
        {
            message = "원본 읽기 실패: " + exception.Message;
        }
    }

    void RestoreDraft()
    {
        ReleaseCanvasDrag(false);
        try
        {
            document = JObject.Parse(draft.puzzle);
            if (string.IsNullOrEmpty(draft.synergy))
            {
                draft.synergy = SourceJson<PuzzleSynergyTableData>();
            }
            synergyDocument = JObject.Parse(draft.synergy);
            if (Selected == null)
            {
                selectedId = Pieces.OrderBy(_piece => (string)_piece["id"], StringComparer.Ordinal)
                    .Select(_piece => (string)_piece["id"]).FirstOrDefault();
            }
            review = null;
            shapeValidationDirty = true;
            hasUnsavedChanges = Dirty;
            Repaint();
        }
        catch (Exception exception)
        {
            message = "초안 읽기 실패: " + exception.Message;
        }
    }

    void Change(string _name, Action _edit)
    {
        string before = EditorJsonUtility.ToJson(draft);
        Undo.RecordObject(draft, _name);
        try
        {
            _edit();
            draft.puzzle = JToken.DeepEquals(document, JObject.Parse(source.puzzle)) ? source.puzzle : Json(document);
            draft.synergy = JToken.DeepEquals(synergyDocument, JObject.Parse(SourceJson<PuzzleSynergyTableData>()))
                ? SourceJson<PuzzleSynergyTableData>() : Json(synergyDocument);
            shapeValidationDirty = true;
            hasUnsavedChanges = Dirty;
            review = null;
            message = null;
        }
        catch (Exception exception)
        {
            EditorJsonUtility.FromJsonOverwrite(before, draft);
            RestoreDraft();
            message = exception.Message;
        }
        Repaint();
    }

    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(!draft || document == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("저장 검토", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    review = ItemEditorStore.Review(source, draft);
                }
                using (new EditorGUI.DisabledScope(review == null || !review.IsValid))
                {
                    if (GUILayout.Button("검토한 내용 저장", EditorStyles.toolbarButton, GUILayout.Width(115f)))
                    {
                        SaveDraft();
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("초안 내보내기", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                {
                    ExportDraft();
                }
            }
            if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(80f)) && ConfirmReload())
            {
                Reload();
                GUIUtility.ExitGUI();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(Dirty ? "미저장 초안" : "원본과 동일", EditorStyles.miniLabel);
        }
        if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }
        if (!draft || document == null)
        {
            return;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawList();
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawPreview();
                DrawReview();
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(350f)))
            {
                detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                BeginValidationFields();
                DrawDetails();
                EditorGUILayout.EndScrollView();
                CompleteValidationFocus();
            }
        }
    }

    void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(ITEM_LIST_WIDTH)))
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("신규"))
                {
                    CreatePiece(false);
                    GUIUtility.ExitGUI();
                }
                using (new EditorGUI.DisabledScope(Selected == null))
                {
                    if (GUILayout.Button("복제"))
                    {
                        CreatePiece(true);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            var itemStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 0f,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            float rowWidth = ITEM_LIST_WIDTH - GUI.skin.verticalScrollbar.fixedWidth - 8f;
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            foreach (var piece in Pieces.OrderBy(_piece => (string)_piece["id"], StringComparer.Ordinal))
            {
                string id = (string)piece["id"];
                string name = (string)piece["displayName"] ?? "";
                if (!string.IsNullOrEmpty(search) &&
                    id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                bool selected = id == selectedId;
                var content = new GUIContent(name + "\n" + id);
                float rowHeight = Mathf.Max(ITEM_ROW_MIN_HEIGHT, itemStyle.CalcHeight(content, rowWidth));
                if (GUILayout.Toggle(selected, content, itemStyle, GUILayout.Height(rowHeight)) && !selected)
                {
                    selectedId = id;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("품목을 바꿔도 초안은 유지됩니다. 저장은 전체 초안에 적용됩니다.", MessageType.None);
        }
    }

    void DrawPreview()
    {
        var piece = Selected;
        if (piece == null)
        {
            EditorGUILayout.HelpBox("품목을 선택하거나 새로 만드세요.", MessageType.Info);
            return;
        }
        GUILayout.Label((string)piece["displayName"], EditorStyles.boldLabel);
        DrawItemCanvas(piece);
        showRaw = EditorGUILayout.Foldout(showRaw, "품목 원본 · 읽기 전용", true);
        if (showRaw)
        {
            rawScroll = EditorGUILayout.BeginScrollView(rawScroll, GUILayout.Height(120f));
            string raw = piece.ToString(Formatting.Indented);
            EditorGUILayout.SelectableLabel(raw, EditorStyles.textArea,
                GUILayout.MinHeight(EditorStyles.textArea.CalcHeight(new GUIContent(raw), Mathf.Max(180f, position.width - 640f))));
            EditorGUILayout.EndScrollView();
        }
    }

    void DrawDetails()
    {
        var piece = Selected;
        if (piece == null)
        {
            return;
        }
        GUILayout.Label("기본 정보", EditorStyles.boldLabel);
        bool isNew = !JObject.Parse(source.puzzle)["pieces"].OfType<JObject>()
            .Any(_piece => (string)_piece["id"] == selectedId);
        using (new EditorGUI.DisabledScope(!isNew))
        {
            MarkValidationField("id");
            string nextId = EditorGUILayout.DelayedTextField("ID", selectedId);
            if (nextId != selectedId)
            {
                Change("품목 ID", () =>
                {
                    if (string.IsNullOrWhiteSpace(nextId) || nextId != nextId.Trim() ||
                        Pieces.Any(_piece => (string)_piece["id"] == nextId))
                    {
                        throw new InvalidOperationException("앞뒤 공백이 없는 고유 ID를 입력하세요.");
                    }
                    var icon = draft.icons.Single(_icon => _icon.pieceId == selectedId);
                    icon.pieceId = nextId;
                    piece["id"] = nextId;
                    RenameDraftPieceReferences(selectedId, nextId);
                    selectedId = nextId;
                });
            }
        }
        if (!isNew)
        {
            EditorGUILayout.LabelField("기존 ID는 참조 보존을 위해 잠겨 있습니다.", EditorStyles.wordWrappedMiniLabel);
        }
        string name = (string)piece["displayName"] ?? "";
        MarkValidationField("displayName");
        string nextName = EditorGUILayout.TextField("이름", name);
        if (name != nextName)
        {
            Change("품목 이름", () => piece["displayName"] = nextName);
        }
        EditorGUILayout.LabelField("설명");
        string description = (string)piece["description"] ?? "";
        MarkValidationField("description");
        string nextDescription = EditorGUILayout.TextArea(description, GUILayout.MinHeight(65f));
        if (description != nextDescription)
        {
            Change("품목 설명", () => piece["description"] = nextDescription);
        }
        int value = (int?)piece["baseValue"] ?? -1;
        MarkValidationField("baseValue");
        int nextValue = EditorGUILayout.IntField("기본 가치", value);
        if (nextValue != value)
        {
            Change("품목 기본 가치", () => piece["baseValue"] = nextValue);
        }
        EditorGUILayout.LabelField("음수 가치 허용 · -1은 누락 판정값", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(isNew))
        {
            bool excluded = (bool?)piece["excludeFromMarket"] ?? false;
            MarkValidationField("excludeFromMarket");
            bool nextExcluded = EditorGUILayout.Toggle("자동 시장 재고에서 제외", excluded);
            if (excluded != nextExcluded)
            {
                Change("시장 재고 제외", () => piece["excludeFromMarket"] = nextExcluded);
            }
        }
        if (isNew)
        {
            EditorGUILayout.HelpBox("새 품목은 미출시 상태로 저장합니다. 시작 객차와 시장 재고에는 자동 추가하지 않습니다.", MessageType.None);
        }
        DrawDefinitionFields(piece);
        EditorGUILayout.Space();
        GUILayout.Label("이미지", EditorStyles.boldLabel);
        var currentIcon = draft.icons.FirstOrDefault(_icon => _icon.pieceId == selectedId);
        var sprite = currentIcon?.Resolve();
        EditorGUI.BeginChangeCheck();
        MarkValidationField("sprite");
        var nextSprite = EditorGUILayout.ObjectField("Sprite", sprite, typeof(Sprite), false) as Sprite;
        if (EditorGUI.EndChangeCheck())
        {
            SetSprite(piece, nextSprite);
        }
        MarkValidationField("visualKey");
        string nextKey = EditorGUILayout.DelayedTextField("이미지 키", (string)piece["visualKey"]);
        if (nextKey != (string)piece["visualKey"])
        {
            Change("품목 이미지 키", () => piece["visualKey"] = nextKey);
        }
        string visualKey = (string)piece["visualKey"];
        var shared = Pieces.Where(_piece => _piece != piece && (string)_piece["visualKey"] == visualKey)
            .Select(_piece => (string)_piece["id"]).ToArray();
        if (shared.Length != 0)
        {
            EditorGUILayout.HelpBox("이미지 키 공유: " + string.Join(", ", shared) +
                "\nSprite를 바꾸면 이 품목에 새 이미지 키를 발급합니다.", MessageType.Info);
        }
        if (currentIcon != null && !string.IsNullOrEmpty(currentIcon.spriteGuid))
        {
            EditorGUILayout.LabelField("GUID", currentIcon.spriteGuid, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("로컬 파일 ID", currentIcon.spriteLocalId.ToString());
        }
        DrawGeometryFields(piece, sprite);
        DrawEffectFields(piece);
        DrawGrowthFields(piece);
        DrawBreedingFields(piece);
        DrawPieceReferences(piece);
        DrawAdvancedFields(piece);
        DrawDefinitionErrors();
    }

    void SetSprite(JObject _piece, Sprite _sprite)
    {
        try
        {
            var next = ItemEditorIcon.FromSprite(selectedId, _sprite);
            ItemEditorStore.WatchSprite(source, _sprite);
            Change("품목 Sprite", () =>
            {
                string key = (string)_piece["visualKey"];
                if (Pieces.Any(_other => _other != _piece && (string)_other["visualKey"] == key))
                {
                    _piece["visualKey"] = Unique("cargo." + selectedId, Pieces.Select(_other => (string)_other["visualKey"]));
                }
                draft.icons.RemoveAll(_icon => _icon.pieceId == selectedId);
                draft.icons.Add(next);
                Vector2 size = IconSize(_piece);
                if (lockSpriteAspect && _sprite && size.x > 0f && size.y > 0f)
                {
                    float height = size.x * _sprite.rect.height / _sprite.rect.width;
                    RequireFinite(size.x, height);
                    _piece["iconHeight"] = height;
                }
            });
        }
        catch (Exception exception)
        {
            message = exception.Message;
        }
    }

    static string Unique(string _stem, IEnumerable<string> _existing)
    {
        var existing = new HashSet<string>(_existing, StringComparer.Ordinal);
        string result = _stem;
        for (int suffix = 2; existing.Contains(result); suffix++)
        {
            result = _stem + "." + suffix;
        }
        return result;
    }

    void CreatePiece(bool _duplicate)
    {
        Change(_duplicate ? "품목 복제" : "품목 신규 작성", () =>
        {
            var template = Selected;
            string id = Unique(_duplicate ? selectedId + ".copy" : "piece.new", Pieces.Select(_piece => (string)_piece["id"]));
            JObject piece;
            ItemEditorIcon icon;
            if (_duplicate)
            {
                piece = (JObject)template.DeepClone();
                piece["displayName"] = (string)template["displayName"] + " 복사본";
                var original = draft.icons.Single(_entry => _entry.pieceId == selectedId);
                icon = new ItemEditorIcon { pieceId = id, spriteGuid = original.spriteGuid, spriteLocalId = original.spriteLocalId };
            }
            else
            {
                var basic = JObject.Parse(source.files.Single(_file => _file.tableType == typeof(BasicDataTableData).AssemblyQualifiedName).source);
                var durability = basic["groups"].OfType<JObject>().Single(_group => (string)_group["id"] == "rules.durability")
                    ["numbers"].OfType<JObject>().Single(_row => (string)_row["id"] == "maximum")["value"];
                piece = new JObject
                {
                    ["displayName"] = "",
                    ["description"] = "",
                    ["baseValue"] = 0,
                    ["durability"] = (int)durability,
                    ["tags"] = new JArray(),
                    ["synergyEffects"] = new JArray(),
                    ["preservationEffect"] = new JObject
                    {
                        ["enabled"] = false,
                        ["reductionPercent"] = 0
                    },
                    ["slotCount"] = 1,
                    ["cells"] = new JArray(new JObject
                    {
                        ["x"] = 0, ["y"] = 0,
                        ["north"] = "Flat", ["east"] = "Flat", ["south"] = "Flat", ["west"] = "Flat"
                    })
                };
                icon = new ItemEditorIcon { pieceId = id };
            }
            piece["id"] = id;
            piece["visualKey"] = Unique("cargo." + id, Pieces.Select(_other => (string)_other["visualKey"]));
            piece["excludeFromMarket"] = true;
            ((JArray)document["pieces"]).Add(piece);
            draft.icons.Add(icon);
            selectedId = id;
            search = "";
        });
    }

    void DrawReview()
    {
        if (review == null)
        {
            return;
        }
        GUILayout.Label(review.IsValid ? "저장 검토 · 반영 범위" : "저장 불가 · 오류와 반영 범위", EditorStyles.boldLabel);
        reviewScroll = EditorGUILayout.BeginScrollView(reviewScroll, GUILayout.Height(190f));
        DrawValidationIssues(review.issues);
        var definitionErrors = new HashSet<string>(review.issues.Select(_issue => _issue.issue.ToString()), StringComparer.Ordinal);
        foreach (string error in review.errors.Where(_error => !definitionErrors.Contains(_error)))
        {
            EditorGUILayout.HelpBox("저장 조건 · " + error, MessageType.Error);
        }
        foreach (string piece in review.pieces)
        {
            EditorGUILayout.LabelField(piece, EditorStyles.wordWrappedLabel);
        }
        foreach (string path in review.files)
        {
            EditorGUILayout.LabelField(path, EditorStyles.wordWrappedMiniLabel);
        }
        if (review.IsValid && review.files.Count == 0)
        {
            EditorGUILayout.LabelField("반영할 파일 변경이 없습니다.");
        }
        EditorGUILayout.EndScrollView();
    }

    bool SaveDraft()
    {
        try
        {
            source = ItemEditorStore.Save(source, draft);
            draft.puzzle = source.puzzle;
            draft.synergy = SourceJson<PuzzleSynergyTableData>();
            draft.effectMigrationBaseline = null;
            draft.effectMigrationOriginal = null;
            originalIcons = IconsJson(draft.icons);
            RestoreDraft();
            message = "품목·번식 테이블과 인벤토리 Sprite 참조를 저장했습니다.";
            return true;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            hasUnsavedChanges = Dirty;
            return false;
        }
    }

    bool ConfirmReload()
    {
        if (!Dirty)
        {
            return true;
        }
        int answer = EditorUtility.DisplayDialogComplex("미저장 아이템 초안", "다시 읽으면 모든 미저장 초안이 사라집니다.",
            "저장 후 다시 읽기", "취소", "버리고 다시 읽기");
        return answer == 2 || (answer == 0 && ReviewAndSave());
    }

    bool ReviewAndSave()
    {
        review = ItemEditorStore.Review(source, draft);
        Repaint();
        if (!review.IsValid)
        {
            message = "저장 검토의 오류를 먼저 해결하세요. 초안은 유지합니다.";
            return false;
        }
        string summary = string.Join("\n", review.pieces.Concat(review.files));
        return EditorUtility.DisplayDialog("아이템 저장 범위", string.IsNullOrEmpty(summary) ? "반영할 변경이 없습니다." : summary,
            "저장", "취소") && SaveDraft();
    }

    public override void SaveChanges()
    {
        if (!ReviewAndSave())
        {
            throw new InvalidOperationException("아이템 초안을 저장하지 못했습니다. 편집 창을 유지합니다.");
        }
        base.SaveChanges();
    }

    public override void DiscardChanges()
    {
        draft.puzzle = source.puzzle;
        draft.synergy = SourceJson<PuzzleSynergyTableData>();
        draft.effectMigrationBaseline = null;
        draft.effectMigrationOriginal = null;
        draft.icons = CopyIcons(source.icons);
        originalIcons = IconsJson(draft.icons);
        Undo.ClearUndo(draft);
        RestoreDraft();
        base.DiscardChanges();
    }

    void ExportDraft()
    {
        string path = EditorUtility.SaveFilePanel("아이템 초안 보관", "", "item-draft.json", "json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        try
        {
            string assetRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            string destination = Path.GetFullPath(path);
            if (destination.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("원본 에셋 보호를 위해 Assets 밖에 초안을 보관하세요.");
            }
            var export = new JObject
            {
                ["puzzle"] = document.DeepClone(),
                ["synergy"] = synergyDocument.DeepClone(),
                ["icons"] = JArray.FromObject(draft.icons)
            };
            File.WriteAllText(path, Json(export), new UTF8Encoding(false));
            message = "초안을 보관했습니다: " + path;
        }
        catch (Exception exception)
        {
            message = "초안 보관 실패: " + exception.Message;
        }
    }
}
