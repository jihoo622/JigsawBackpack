using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class ItemEditorFile
{
    public string path;
    public string hash;
    public string tableType;
    public string source;
}

[Serializable]
public sealed class ItemEditorSource
{
    [NonSerialized] public ItemEditorDefinitionReview originalValidation;
    public string puzzle;
    public List<ItemEditorFile> files = new List<ItemEditorFile>();
    public List<string> prefabs = new List<string>();
    public List<string> scenes = new List<string>();
    public List<ItemEditorIcon> icons = new List<ItemEditorIcon>();
}

public sealed class ItemEditorSaveReview
{
    public readonly List<string> errors = new List<string>();
    public readonly List<ItemEditorIssue> issues = new List<ItemEditorIssue>();
    public readonly List<string> pieces = new List<string>();
    public readonly List<string> files = new List<string>();
    public readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
    public readonly Dictionary<string, Dictionary<string, Sprite>> viewIcons = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.Ordinal);
    public bool IsValid => errors.Count == 0;
}

public static class ItemEditorStore
{
    const string GAME_SCENE = "Assets/Scenes/Game.unity";
    const string VIEW_SCRIPT = "Assets/Scripts/Runtime/UI/PuzzleInventoryView.cs";
    static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

    public static ItemEditorSource Capture()
    {
        var source = new ItemEditorSource();
        AddTable<BasicDataTableData>(source);
        AddTable<GameplayTableData>(source);
        AddTable<FlowTableData>(source);
        AddTable<PuzzleTableData>(source);
        AddTable<PuzzleSynergyTableData>(source);
        AddTable<PuzzleCityActionTableData>(source);
        AddTable<PuzzleTradeTableData>(source);
        AddTable<PuzzleRouteTableData>(source);
        AddTable<PuzzleTimeTableData>(source);
        AddTable<TrainventoryQuestTableData>(source);
        source.puzzle = source.files.Single(_file => _file.tableType == typeof(PuzzleTableData).AssemblyQualifiedName).source;
        source.prefabs = InventoryAssets("t:Prefab");
        source.scenes = InventoryAssets("t:Scene");
        if (!source.scenes.Contains(GAME_SCENE))
        {
            throw new InvalidOperationException("Game 씬의 인벤토리 참조를 찾지 못했습니다. 씬 연결을 확인하세요.");
        }
        foreach (string path in source.prefabs.Concat(source.scenes))
        {
            WatchFile(source, path);
            WatchFile(source, path + ".meta");
        }

        var byKey = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        var conflictingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in source.prefabs.Concat(source.scenes))
        {
            VisitViews(path, _views =>
            {
                foreach (var view in _views)
                {
                    var icons = new SerializedObject(view).FindProperty("itemIcons");
                    for (int i = 0; i < icons.arraySize; i++)
                    {
                        var entry = icons.GetArrayElementAtIndex(i);
                        string key = entry.FindPropertyRelative("key").stringValue;
                        var sprite = entry.FindPropertyRelative("sprite").objectReferenceValue as Sprite;
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            continue;
                        }
                        if (byKey.TryGetValue(key, out var existing) && existing != sprite)
                        {
                            conflictingKeys.Add(key);
                        }
                        else
                        {
                            byKey[key] = sprite;
                        }
                        if (sprite)
                        {
                            WatchSprite(source, sprite);
                        }
                    }
                }
            });
        }
        foreach (var piece in JObject.Parse(source.puzzle)["pieces"].OfType<JObject>())
        {
            string key = (string)piece["visualKey"];
            Sprite sprite = null;
            if (key != null && !conflictingKeys.Contains(key))
            {
                byKey.TryGetValue(key, out sprite);
            }
            source.icons.Add(ItemEditorIcon.FromSprite((string)piece["id"], sprite));
        }
        return source;
    }

    static void AddTable<T>(ItemEditorSource _source)
    {
        string path = TableManager.EditorAssetPath<T>();
        WatchFile(_source, path);
        WatchFile(_source, path + ".meta");
        var file = _source.files.Single(_entry => _entry.path == path);
        file.tableType = typeof(T).AssemblyQualifiedName;
        file.source = File.ReadAllText(path);
    }

    static List<string> InventoryAssets(string _filter)
    {
        return AssetDatabase.FindAssets(_filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(_path => AssetDatabase.GetDependencies(_path, true).Contains(VIEW_SCRIPT))
            .OrderBy(_path => _path, StringComparer.Ordinal).ToList();
    }

    static string Hash(string _path)
    {
        using (var algorithm = SHA256.Create())
        using (var stream = File.OpenRead(_path))
        {
            return Convert.ToBase64String(algorithm.ComputeHash(stream));
        }
    }

    static void WatchFile(ItemEditorSource _source, string _path)
    {
        if (_source.files.Any(_file => _file.path == _path))
        {
            return;
        }
        _source.files.Add(new ItemEditorFile { path = _path, hash = Hash(_path) });
    }

    public static void WatchSprite(ItemEditorSource _source, Sprite _sprite)
    {
        ItemEditorIcon.FromSprite("", _sprite);
        if (_sprite)
        {
            string path = AssetDatabase.GetAssetPath(_sprite);
            WatchFile(_source, path);
            WatchFile(_source, path + ".meta");
        }
    }

    static void VisitViews(string _path, Action<PuzzleInventoryView[]> _visit)
    {
        if (_path.EndsWith(".prefab", StringComparison.Ordinal))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            if (!root)
            {
                throw new IOException("프리팹을 읽을 수 없습니다: " + _path);
            }
            _visit(root.GetComponentsInChildren<PuzzleInventoryView>(true));
            return;
        }
        var scene = SceneManager.GetSceneByPath(_path);
        bool preview = !scene.IsValid() || !scene.isLoaded;
        if (preview)
        {
            scene = EditorSceneManager.OpenPreviewScene(_path);
        }
        try
        {
            _visit(scene.GetRootGameObjects().SelectMany(_root =>
                _root.GetComponentsInChildren<PuzzleInventoryView>(true)).ToArray());
        }
        finally
        {
            if (preview)
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }

    static void CheckSources(ItemEditorSource _source, List<string> _errors)
    {
        foreach (var file in _source.files)
        {
            if (!File.Exists(file.path) || Hash(file.path) != file.hash)
            {
                _errors.Add("외부에서 원본이 변경되었습니다. 초안은 유지됩니다. 다시 읽기 전에 내용을 보관하세요: " + file.path);
            }
        }
        if (!InventoryAssets("t:Prefab").SequenceEqual(_source.prefabs) ||
            !InventoryAssets("t:Scene").SequenceEqual(_source.scenes))
        {
            _errors.Add("인벤토리가 있는 프리팹·씬 목록이 바뀌었습니다. 원본을 다시 읽으세요.");
        }
    }

    static void CheckEditorState(ItemEditorSource _source, List<string> _errors)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            _errors.Add("플레이·컴파일·에셋 갱신 중에는 저장할 수 없습니다.");
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && _source.prefabs.Contains(stage.assetPath))
        {
            _errors.Add("인벤토리 프리팹 편집 모드를 저장하고 닫은 뒤 다시 시도하세요: " + stage.assetPath);
        }
        foreach (string path in _source.prefabs)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root && (EditorUtility.IsDirty(root) || root.GetComponentsInChildren<Transform>(true)
                .Any(_transform => _transform.GetComponents<Component>().Any(_component => _component && EditorUtility.IsDirty(_component)))))
            {
                _errors.Add("인벤토리 프리팹에 저장하지 않은 변경이 있습니다: " + path);
            }
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.isDirty && !EditorSceneManager.IsPreviewScene(scene))
            {
                _errors.Add("열린 씬에 저장하지 않은 변경이 있습니다. 먼저 저장하거나 되돌리세요: " +
                    (string.IsNullOrEmpty(scene.path) ? scene.name : scene.path));
            }
        }
    }

    public static ItemEditorSaveReview Review(ItemEditorSource _source, ItemEditorDraft _draft)
    {
        var review = new ItemEditorSaveReview();
        try
        {
            CheckEditorState(_source, review.errors);
            CheckSources(_source, review.errors);
            var candidate = JObject.Parse(_draft.puzzle);
            var original = JObject.Parse(_source.puzzle);
            var pieces = candidate["pieces"] as JArray;
            if (pieces == null || pieces.Any(_piece => !(_piece is JObject)))
            {
                review.errors.Add("pieces는 품목 객체 목록이어야 합니다.");
                return review;
            }
            var sources = DraftSources(_source, _draft);
            var definitions = ItemEditorValidation.Review(_source, _draft);
            review.issues.AddRange(definitions.issues);
            review.errors.AddRange(definitions.issues.Select(_issue => _issue.issue.ToString()));
            foreach (var sprite in definitions.sprites)
            {
                review.sprites.Add(sprite.Key, sprite.Value);
            }
            ReviewBreeding(_source, _draft, review);
            if (!string.IsNullOrEmpty(_draft.effectMigrationBaseline))
            {
                var migrationSource = JObject.Parse(sources[typeof(PuzzleSynergyTableData)]);
                review.pieces.AddRange(ItemEditorEffectMigration.Compare(JObject.Parse(_draft.effectMigrationOriginal),
                    JObject.Parse(_draft.effectMigrationBaseline), migrationSource));
            }

            var existing = original["pieces"].OfType<JObject>().ToDictionary(_piece => (string)_piece["id"], StringComparer.Ordinal);
            var originalTags = new HashSet<string>(existing.Values.SelectMany(_piece => _piece["tags"].Values<string>()), StringComparer.Ordinal);
            var customTags = new HashSet<string>((candidate["tagPresentations"] as JArray ?? new JArray())
                .OfType<JObject>().Select(_tag => (string)_tag["id"]), StringComparer.Ordinal);
            var icons = _draft.icons.GroupBy(_icon => _icon.pieceId, StringComparer.Ordinal)
                .ToDictionary(_group => _group.Key, _group => _group.ToArray(), StringComparer.Ordinal);
            foreach (var piece in pieces.OfType<JObject>())
            {
                string id = (string)piece["id"];
                string key = (string)piece["visualKey"];
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key))
                {
                    review.errors.Add("모든 품목에 ID와 이미지 키가 필요합니다.");
                    continue;
                }
                bool isNew = !existing.TryGetValue(id, out var old);
                foreach (string tag in piece["tags"].Values<string>())
                {
                    if (!originalTags.Contains(tag) && !Presentation.HasBuiltInPuzzleTag(tag) && !customTags.Contains(tag))
                    {
                        review.errors.Add(id + ": 새 태그 '" + tag + "'의 한글 표시명과 색이 누락되었습니다.");
                    }
                }
                if (isNew || !JToken.DeepEquals(old, piece))
                {
                    review.pieces.Add(id + (isNew ? " (신규, 시장 자동 등록 없음)" : " (수정)"));
                }
                if (isNew && (bool?)piece["excludeFromMarket"] != true)
                {
                    review.errors.Add(id + ": 새 품목은 excludeFromMarket: true로 저장해야 합니다.");
                }
                if (!icons.TryGetValue(id, out var matches) || matches.Length != 1)
                {
                    continue;
                }
                var originalIcon = _source.icons.SingleOrDefault(_icon => _icon.pieceId == id);
                if (!isNew && originalIcon != null &&
                    (originalIcon.spriteGuid != matches[0].spriteGuid || originalIcon.spriteLocalId != matches[0].spriteLocalId) &&
                    JToken.DeepEquals(old, piece))
                {
                    review.pieces.Add(id + " (Sprite 수정)");
                }
            }
            foreach (string id in existing.Keys)
            {
                if (!pieces.OfType<JObject>().Any(_piece => (string)_piece["id"] == id))
                {
                    review.errors.Add("기존 품목 ID 변경·삭제는 지원하지 않습니다: " + id);
                }
            }
            var originalOther = (JObject)original.DeepClone();
            var candidateOther = (JObject)candidate.DeepClone();
            originalOther.Remove("pieces");
            candidateOther.Remove("pieces");
            originalOther.Remove("tagPresentations");
            candidateOther.Remove("tagPresentations");
            if (!JToken.DeepEquals(originalOther, candidateOther))
            {
                review.errors.Add("품목·태그 표시 정의 이외의 도시·시장·객차 데이터가 달라졌습니다.");
            }
            if (!JToken.DeepEquals(original["tagPresentations"], candidate["tagPresentations"]))
            {
                review.pieces.Add("PuzzleTableData.tagPresentations (태그 표시 정의 수정)");
            }
            if (!JToken.DeepEquals(original, candidate))
            {
                review.files.Add(TableManager.EditorAssetPath<PuzzleTableData>());
            }
            foreach (string path in _source.prefabs.Concat(_source.scenes))
            {
                VisitViews(path, _views =>
                {
                    bool changes = false;
                    foreach (var view in _views)
                    {
                        review.viewIcons.Add(ViewIdentity(path, view), ReadViewIcons(view)
                            .Where(_icon => !string.IsNullOrWhiteSpace(_icon.key))
                            .GroupBy(_icon => _icon.key, StringComparer.Ordinal)
                            .ToDictionary(_group => _group.Key, _group => _group.First().sprite, StringComparer.Ordinal));
                        var iconErrors = new List<string>();
                        changes |= UpdateIcons(view, review.sprites, iconErrors, path, false);
                        foreach (string error in iconErrors)
                        {
                            var issue = new GameTableIssue(ViewIdentity(path, view) + ".itemIcons", error);
                            review.issues.Add(new ItemEditorIssue
                            {
                                issue = issue,
                                stablePath = issue.Path,
                                existing = true,
                                assetPath = path
                            });
                            review.errors.Add(issue.ToString());
                        }
                    }
                    if (changes)
                    {
                        review.files.Add(path);
                    }
                });
            }
            foreach (string path in review.files)
            {
                if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0 || !AssetDatabase.IsOpenForEdit(path))
                {
                    review.errors.Add("쓰기 가능한 파일이 아닙니다: " + path);
                }
            }
        }
        catch (Exception exception)
        {
            review.errors.Add("저장 검토 실패: " + exception.Message);
        }
        return review;
    }

    public static Dictionary<Type, string> DraftSources(ItemEditorSource _source, ItemEditorDraft _draft)
    {
        var sources = _source.files.Where(_file => _file.tableType != null)
            .ToDictionary(_file => Type.GetType(_file.tableType, true), _file => _file.source);
        sources[typeof(PuzzleTableData)] = _draft.puzzle;
        if (!string.IsNullOrEmpty(_draft.synergy))
        {
            sources[typeof(PuzzleSynergyTableData)] = _draft.synergy;
        }
        return sources;
    }

    static void ReviewBreeding(ItemEditorSource _source, ItemEditorDraft _draft, ItemEditorSaveReview _review)
    {
        var original = JObject.Parse(_source.files.Single(_file =>
            _file.tableType == typeof(PuzzleSynergyTableData).AssemblyQualifiedName).source);
        var candidate = JObject.Parse(DraftSources(_source, _draft)[typeof(PuzzleSynergyTableData)]);
        var oldBreeding = original["animals"]?["breeding"];
        var breeding = candidate["animals"]?["breeding"];
        if (!JToken.DeepEquals(oldBreeding, breeding))
        {
            _review.files.Add(TableManager.EditorAssetPath<PuzzleSynergyTableData>());
            foreach (var rule in (breeding as JArray ?? new JArray()).OfType<JObject>())
            {
                if (!(oldBreeding as JArray ?? new JArray()).Any(_old => JToken.DeepEquals(_old, rule)))
                {
                    _review.pieces.Add("번식 수정 " + (string)rule["parentPieceId"] + " → " +
                        (string)rule["offspringPieceId"] + " × " + (int?)rule["offspringCount"]);
                }
            }
            foreach (var rule in (oldBreeding as JArray ?? new JArray()).OfType<JObject>())
            {
                if (!(breeding as JArray ?? new JArray()).OfType<JObject>()
                    .Any(_new => (string)_new["parentPieceId"] == (string)rule["parentPieceId"]))
                {
                    _review.pieces.Add("번식 제거 " + (string)rule["parentPieceId"]);
                }
            }
        }
        (original["animals"] as JObject)?.Remove("breeding");
        (candidate["animals"] as JObject)?.Remove("breeding");
        if (!JToken.DeepEquals(original, candidate))
        {
            _review.errors.Add("이 창에서는 PuzzleSynergyTableData.animals.breeding만 수정할 수 있습니다. 공통 규칙·시간·수치는 원본을 유지하세요.");
        }
    }

    static bool UpdateIcons(PuzzleInventoryView _view, Dictionary<string, Sprite> _sprites,
        List<string> _errors, string _path, bool _apply)
    {
        var serialized = new SerializedObject(_view);
        var array = serialized.FindProperty("itemIcons");
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < array.arraySize; i++)
        {
            var entry = array.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("key").stringValue;
            var sprite = entry.FindPropertyRelative("sprite").objectReferenceValue;
            if (string.IsNullOrWhiteSpace(key) || !indices.TryAdd(key, i))
            {
                _errors.Add(_path + " / " + _view.name + ": 빈 이미지 키 또는 중복 키: " + key);
            }
            else if (!sprite && !_sprites.ContainsKey(key))
            {
                _errors.Add(_path + " / " + _view.name + ": Sprite 누락: " + key);
            }
        }
        bool changed = false;
        foreach (var pair in _sprites.OrderBy(_pair => _pair.Key, StringComparer.Ordinal))
        {
            if (!indices.TryGetValue(pair.Key, out int index))
            {
                changed = true;
                if (!_apply)
                {
                    continue;
                }
                index = array.arraySize++;
                array.GetArrayElementAtIndex(index).FindPropertyRelative("key").stringValue = pair.Key;
            }
            var sprite = array.GetArrayElementAtIndex(index).FindPropertyRelative("sprite");
            if (sprite.objectReferenceValue != pair.Value)
            {
                changed = true;
                if (_apply)
                {
                    sprite.objectReferenceValue = pair.Value;
                }
            }
        }
        if (_apply && changed)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(_view))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(_view);
            }
        }
        return changed;
    }

    sealed class SceneEdit
    {
        public string path;
        public Scene scene;
        public bool wasLoaded;
    }

    sealed class ViewIcon
    {
        public string key;
        public Sprite sprite;
    }

    static List<ViewIcon> ReadViewIcons(PuzzleInventoryView _view)
    {
        var icons = new SerializedObject(_view).FindProperty("itemIcons");
        var result = new List<ViewIcon>();
        for (int index = 0; index < icons.arraySize; index++)
        {
            var icon = icons.GetArrayElementAtIndex(index);
            result.Add(new ViewIcon
            {
                key = icon.FindPropertyRelative("key").stringValue,
                sprite = icon.FindPropertyRelative("sprite").objectReferenceValue as Sprite
            });
        }
        return result;
    }

    static string ViewIdentity(string _path, PuzzleInventoryView _view)
    {
        var indices = new List<int>();
        for (var transform = _view.transform; transform != null; transform = transform.parent)
        {
            indices.Add(transform.GetSiblingIndex());
        }
        indices.Reverse();
        return _path + " / " + string.Join("/", indices) + " / " +
            Array.IndexOf(_view.GetComponents<PuzzleInventoryView>(), _view);
    }

    static void VerifySavedAssets(ItemEditorSource _source, ItemEditorDraft _draft, ItemEditorSaveReview _review)
    {
        var expected = DraftSources(_source, _draft);
        foreach (var type in new[] { typeof(PuzzleTableData), typeof(PuzzleSynergyTableData) })
        {
            string path = _source.files.Single(_file => _file.tableType == type.AssemblyQualifiedName).path;
            if (!JToken.DeepEquals(JObject.Parse(expected[type]), JObject.Parse(File.ReadAllText(path))))
            {
                throw new IOException("저장 후 JSON이 검토한 전체 초안과 다릅니다: " + path);
            }
        }
        var pieces = JObject.Parse(_draft.puzzle)["pieces"].ToObject<PuzzlePieceData[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (string path in _source.prefabs.Concat(_source.scenes))
        {
            VisitViews(path, _views =>
            {
                foreach (var view in _views)
                {
                    string identity = ViewIdentity(path, view);
                    seen.Add(identity);
                    var rules = new GameTableRules(errors);
                    var icons = rules.Index(ReadViewIcons(view).ToArray(), _icon => _icon.key, identity + ".itemIcons");
                    rules.ValidateItemVisuals(pieces, _key => _key != null && icons.TryGetValue(_key, out var icon) && icon.sprite, identity);
                    if (icons.Values.Any(_icon => !_icon.sprite))
                    {
                        errors.Add(identity + ": 저장 후 Sprite가 없는 아이콘 항목이 있습니다.");
                    }
                    foreach (var pair in _review.sprites)
                    {
                        if (!icons.TryGetValue(pair.Key, out var icon) || icon.sprite != pair.Value)
                        {
                            errors.Add(identity + ": 저장 후 Sprite 참조가 초안과 다릅니다: " + pair.Key);
                        }
                    }
                    if (!_review.viewIcons.TryGetValue(identity, out var original))
                    {
                        errors.Add("저장 검토에 없던 인벤토리가 있습니다: " + identity);
                        continue;
                    }
                    var expectedKeys = new HashSet<string>(original.Keys.Concat(_review.sprites.Keys), StringComparer.Ordinal);
                    if (!expectedKeys.SetEquals(icons.Keys))
                    {
                        errors.Add(identity + ": 저장 후 이미지 키 목록이 검토 범위와 다릅니다.");
                    }
                    foreach (var pair in original.Where(_pair => !_review.sprites.ContainsKey(_pair.Key)))
                    {
                        if (!icons.TryGetValue(pair.Key, out var icon) || icon.sprite != pair.Value)
                        {
                            errors.Add(identity + ": 편집 범위 밖의 Sprite 참조가 바뀌었습니다: " + pair.Key);
                        }
                    }
                }
            });
        }
        if (!seen.SetEquals(_review.viewIcons.Keys))
        {
            errors.Add("저장 전후 인벤토리 View 목록이 다릅니다.");
        }
        if (errors.Count != 0)
        {
            throw new IOException(string.Join("\n", errors));
        }
    }

    public static ItemEditorSource Save(ItemEditorSource _source, ItemEditorDraft _draft)
    {
        var review = Review(_source, _draft);
        if (!review.IsValid)
        {
            throw new InvalidOperationException(string.Join("\n", review.errors));
        }
        var conflicts = new List<string>();
        CheckEditorState(_source, conflicts);
        CheckSources(_source, conflicts);
        if (conflicts.Count != 0)
        {
            throw new IOException(string.Join("\n", conflicts));
        }
        var backups = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var sceneEdits = new List<SceneEdit>();
        var active = SceneManager.GetActiveScene();
        try
        {
            var tableWrites = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TableManager.EditorAssetPath<PuzzleTableData>()] = _draft.puzzle,
                [TableManager.EditorAssetPath<PuzzleSynergyTableData>()] = _draft.synergy
            };
            foreach (var write in tableWrites.Where(_write => review.files.Contains(_write.Key)))
            {
                BackUp(write.Key, _source, backups);
                File.WriteAllText(write.Key, write.Value, UTF8);
            }
            foreach (string path in _source.prefabs.OrderBy(_path => AssetDatabase.GetDependencies(_path, true).Length)
                .ThenBy(_path => _path, StringComparer.Ordinal))
            {
                if (!review.files.Contains(path))
                {
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    var errors = new List<string>();
                    foreach (var view in root.GetComponentsInChildren<PuzzleInventoryView>(true))
                    {
                        changed |= UpdateIcons(view, review.sprites, errors, path, true);
                    }
                    if (errors.Count != 0)
                    {
                        throw new InvalidOperationException(string.Join("\n", errors));
                    }
                    if (changed)
                    {
                        BackUp(path, _source, backups);
                        if (!PrefabUtility.SaveAsPrefabAsset(root, path))
                        {
                            throw new IOException("프리팹 저장 실패: " + path);
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            foreach (string path in _source.scenes.Where(_path => review.files.Contains(_path)))
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                var edit = new SceneEdit { path = path, scene = scene, wasLoaded = loaded };
                sceneEdits.Add(edit);
                BackUp(path, _source, backups);
                bool changed = false;
                var errors = new List<string>();
                foreach (var view in scene.GetRootGameObjects().SelectMany(_root =>
                    _root.GetComponentsInChildren<PuzzleInventoryView>(true)))
                {
                    changed |= UpdateIcons(view, review.sprites, errors, path, true);
                }
                if (errors.Count != 0)
                {
                    throw new InvalidOperationException(string.Join("\n", errors));
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, path))
                    {
                        throw new IOException("씬 저장 실패: " + path);
                    }
                }
            }
            TableManager.ReloadAfterTableEdit();
            TableManager.Instance.RequireValid();
            VerifySavedAssets(_source, _draft, review);
            foreach (var edit in sceneEdits.Where(_edit => !_edit.wasLoaded))
            {
                if (!EditorSceneManager.CloseScene(edit.scene, true))
                {
                    throw new IOException("작업용 씬을 닫지 못했습니다: " + edit.path);
                }
            }
            return Capture();
        }
        catch (Exception exception)
        {
            var errors = new List<string> { exception.Message };
            foreach (var backup in backups)
            {
                try
                {
                    if (!File.Exists(backup.Key) || !File.ReadAllBytes(backup.Key).SequenceEqual(backup.Value))
                    {
                        File.WriteAllBytes(backup.Key, backup.Value);
                    }
                }
                catch (Exception restoreException)
                {
                    errors.Add("파일 복구 실패: " + backup.Key + ": " + restoreException.Message);
                }
            }
            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception refreshException)
            {
                errors.Add("에셋 갱신 실패: " + refreshException.Message);
            }
            Scene safetyScene = default;
            if (sceneEdits.Any(_edit => _edit.wasLoaded) && SceneManager.sceneCount == 1)
            {
                try
                {
                    safetyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                }
                catch (Exception sceneException)
                {
                    errors.Add("씬 복구 준비 실패: " + sceneException.Message);
                }
            }
            foreach (var edit in sceneEdits)
            {
                try
                {
                    var scene = SceneManager.GetSceneByPath(edit.path);
                    if (scene.IsValid() && scene.isLoaded && !EditorSceneManager.CloseScene(scene, true))
                    {
                        throw new IOException("편집 씬을 닫지 못했습니다.");
                    }
                    if (edit.wasLoaded)
                    {
                        EditorSceneManager.OpenScene(edit.path, OpenSceneMode.Additive);
                    }
                }
                catch (Exception restoreException)
                {
                    errors.Add("씬 복구 실패: " + edit.path + ": " + restoreException.Message);
                }
            }
            if (safetyScene.IsValid() && safetyScene.isLoaded && SceneManager.sceneCount > 1)
            {
                try
                {
                    if (!EditorSceneManager.CloseScene(safetyScene, true))
                    {
                        errors.Add("복구용 빈 씬을 닫지 못했습니다.");
                    }
                }
                catch (Exception closeException)
                {
                    errors.Add("복구용 씬 정리 실패: " + closeException.Message);
                }
            }
            TableManager.Reload();
            throw new IOException("저장에 실패했습니다. 이번 작업 파일의 복구를 시도했으며 초안은 유지합니다.\n" + string.Join("\n", errors), exception);
        }
        finally
        {
            var previousActive = string.IsNullOrEmpty(active.path) ? active : SceneManager.GetSceneByPath(active.path);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
    }

    static void BackUp(string _path, ItemEditorSource _source, Dictionary<string, byte[]> _backups)
    {
        foreach (string path in new[] { _path, _path + ".meta" })
        {
            if (!_backups.ContainsKey(path))
            {
                var original = _source.files.Single(_file => _file.path == path);
                if (Hash(path) != original.hash)
                {
                    throw new IOException("저장 직전에 외부 변경을 감지했습니다: " + path);
                }
                _backups.Add(path, File.ReadAllBytes(path));
            }
        }
    }
}
