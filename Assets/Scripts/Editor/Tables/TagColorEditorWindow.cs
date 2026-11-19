using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BasicDataTableData.json의 presentation.tags 색상을 배지 미리보기·대비율과 함께 편집하고 저장하는 에디터 창.
/// </summary>
public sealed class TagColorEditorWindow : EditorWindow
{
    const string MENU = "Tools/MyJG/Tag Color Editor";
    const string TAGS_GROUP_ID = "presentation.tags";
    const string TOOLTIP_GROUP_ID = "presentation.tooltip";
    const string QUEST_GROUP_ID = "presentation.quest";
    const string UI_FONT_PATH = "Assets/TextMesh Pro/Fonts/NotoSansKR-Medium.ttf";
    static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

    sealed class TagColorEntry
    {
        public string id;
        public string label;
        public string note;
        public bool isBackground;
        public bool isQuest;
        public Color color;
        public Color originalColor;
        public string hexInput;
    }

    readonly List<TagColorEntry> entries = new List<TagColorEntry>();
    Color tooltipBackground = new Color32(30, 32, 41, 255);
    Color tooltipText = new Color32(246, 239, 223, 255);
    Color questMarkerColor = new Color32(102, 217, 255, 255);
    Vector2 scroll;
    string statusMessage;
    MessageType statusType = MessageType.None;
    Texture2D whiteTexture;
    GUIStyle badgeStyle;
    Font uiFont;

    [MenuItem(MENU)]
    static void Open()
    {
        var window = GetWindow<TagColorEditorWindow>("Tag Color Editor");
        window.minSize = new Vector2(640f, 460f);
    }

    void OnEnable()
    {
        LoadFromDisk();
    }

    void OnDisable()
    {
        if (whiteTexture)
        {
            DestroyImmediate(whiteTexture);
            whiteTexture = null;
        }
    }

    void OnGUI()
    {
        DrawToolbar();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("태그 배지 배경색 (실제 툴팁·상점 배지 미리보기)", _entry => _entry.isBackground);
        EditorGUILayout.Space(8f);
        DrawSection("태그 전용 글자색 설정", _entry => !_entry.isBackground);
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        bool dirty = entries.Any(_entry => !ColorsEqual(_entry.color, _entry.originalColor));
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("다시 불러오기", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                LoadFromDisk();
            }

            GUI.enabled = dirty;
            if (GUILayout.Button("되돌리기", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                RevertEdits();
            }
            if (GUILayout.Button("저장 (JSON 반영)", EditorStyles.toolbarButton, GUILayout.Width(115f)))
            {
                SaveToDisk();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            GUILayout.Label(dirty ? "수정됨 (미저장)" : "저장됨", EditorStyles.miniLabel);
        }
    }

    void DrawSection(string _header, Predicate<TagColorEntry> _filter)
    {
        EditorGUILayout.LabelField(_header, EditorStyles.boldLabel);
        foreach (var entry in entries)
        {
            if (!_filter(entry))
            {
                continue;
            }
            DrawEntryRow(entry);
        }
    }

    void DrawEntryRow(TagColorEntry _entry)
    {
        bool changed = !ColorsEqual(_entry.color, _entry.originalColor);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            string title = changed ? $"* {_entry.label}" : _entry.label;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(175f)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_entry.id, EditorStyles.miniLabel);
            }

            if (_entry.isBackground)
            {
                Color textColor = ResolveBadgeTextColor(_entry);
                DrawBadgePreview(_entry.label, _entry.color, textColor);
                float contrast = ContrastRatio(_entry.color, textColor);
                Color prevGui = GUI.contentColor;
                GUI.contentColor = contrast >= 4.5f ? new Color(0.55f, 0.95f, 0.58f)
                    : contrast >= 3.0f ? new Color(1f, 0.86f, 0.35f)
                    : new Color(1f, 0.45f, 0.45f);
                GUILayout.Label($"대비 {contrast:0.0}:1", EditorStyles.miniLabel, GUILayout.Width(68f));
                GUI.contentColor = prevGui;
            }
            else
            {
                EditorGUILayout.LabelField(_entry.note ?? string.Empty, EditorStyles.miniLabel, GUILayout.Width(210f));
            }

            EditorGUI.BeginChangeCheck();
            Color picked = EditorGUILayout.ColorField(GUIContent.none, _entry.color, true, false, false, GUILayout.Width(64f));
            if (EditorGUI.EndChangeCheck())
            {
                _entry.color = picked;
                _entry.hexInput = ToHex(picked);
            }

            EditorGUI.BeginChangeCheck();
            string editedHex = EditorGUILayout.TextField(_entry.hexInput, GUILayout.Width(76f));
            if (EditorGUI.EndChangeCheck())
            {
                _entry.hexInput = editedHex;
                if (TryParseHex(editedHex, out Color parsed))
                {
                    _entry.color = parsed;
                }
            }

            if (_entry.isQuest && GUILayout.Button("마커 색 동기화", GUILayout.Width(96f)))
            {
                _entry.color = questMarkerColor;
                _entry.hexInput = ToHex(questMarkerColor);
            }
        }
    }

    void DrawBadgePreview(string _text, Color _background, Color _textColor)
    {
        Rect plateRect = GUILayoutUtility.GetRect(136f, 28f, GUILayout.Width(136f), GUILayout.Height(28f));
        DrawSolidRect(plateRect, tooltipBackground);

        Rect badgeRect = new Rect(plateRect.x + 12f, plateRect.y + 4f, plateRect.width - 24f, plateRect.height - 8f);
        DrawSolidRect(badgeRect, _background);

        if (!uiFont)
        {
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(UI_FONT_PATH);
        }
        badgeStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            font = uiFont
        };
        if (uiFont && badgeStyle.font != uiFont)
        {
            badgeStyle.font = uiFont;
        }
        badgeStyle.normal.textColor = _textColor;
        badgeStyle.hover.textColor = _textColor;
        GUI.Label(badgeRect, _text, badgeStyle);
    }

    void DrawSolidRect(Rect _rect, Color _color)
    {
        if (!whiteTexture)
        {
            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }
        Color prev = GUI.color;
        GUI.color = _color;
        GUI.DrawTexture(_rect, whiteTexture);
        GUI.color = prev;
    }

    Color ResolveBadgeTextColor(TagColorEntry _entry)
    {
        return _entry.isQuest ? tooltipBackground : tooltipText;
    }

    void LoadFromDisk()
    {
        entries.Clear();
        string path = TableManager.EditorAssetPath<BasicDataTableData>();
        if (!File.Exists(path))
        {
            statusMessage = "파일을 찾을 수 없습니다: " + path;
            statusType = MessageType.Error;
            return;
        }

        var root = JObject.Parse(File.ReadAllText(path, UTF8));
        var groups = root["groups"]?.OfType<JObject>().ToList() ?? new List<JObject>();
        var tooltipGroup = groups.FirstOrDefault(_group => (string)_group["id"] == TOOLTIP_GROUP_ID);
        var questGroup = groups.FirstOrDefault(_group => (string)_group["id"] == QUEST_GROUP_ID);
        var tagsGroup = groups.FirstOrDefault(_group => (string)_group["id"] == TAGS_GROUP_ID);
        if (tagsGroup == null)
        {
            statusMessage = "presentation.tags 그룹을 찾지 못했습니다.";
            statusType = MessageType.Error;
            return;
        }

        tooltipBackground = ReadColor(tooltipGroup, "bodyBackground", tooltipBackground);
        tooltipText = ReadColor(tooltipGroup, "text", tooltipText);
        questMarkerColor = ReadColor(questGroup, "mapMarker", questMarkerColor);

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var textRow in tagsGroup["texts"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            string tid = (string)textRow["id"] ?? string.Empty;
            string val = (string)textRow["value"] ?? string.Empty;
            if (tid.EndsWith("Label", StringComparison.Ordinal))
            {
                labels[tid.Substring(0, tid.Length - "Label".Length)] = val;
            }
        }

        foreach (var colorRow in tagsGroup["colors"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            string id = (string)colorRow["id"] ?? string.Empty;
            Color color = ParseJObjectColor(colorRow);
            bool isBackground = id.EndsWith("Background", StringComparison.Ordinal) ||
                id == "puzzleTagBackgroundColorColor";
            entries.Add(new TagColorEntry
            {
                id = id,
                label = ResolveDisplayName(id, labels),
                note = ResolveNote(id),
                isBackground = isBackground,
                isQuest = id == "questBackground",
                color = color,
                originalColor = color,
                hexInput = ToHex(color)
            });
        }

        statusMessage = null;
        statusType = MessageType.None;
    }

    void RevertEdits()
    {
        foreach (var entry in entries)
        {
            entry.color = entry.originalColor;
            entry.hexInput = ToHex(entry.originalColor);
        }
        statusMessage = "저장 전 원본으로 되돌렸습니다.";
        statusType = MessageType.Info;
    }

    void SaveToDisk()
    {
        string path = TableManager.EditorAssetPath<BasicDataTableData>();
        string raw = File.ReadAllText(path, UTF8);
        string newline = raw.Contains("\r\n") ? "\r\n" : "\n";
        var root = JObject.Parse(raw);
        var tagsGroup = root["groups"]?.OfType<JObject>()
            .FirstOrDefault(_group => (string)_group["id"] == TAGS_GROUP_ID);
        if (tagsGroup == null)
        {
            statusMessage = "presentation.tags 그룹을 찾지 못했습니다.";
            statusType = MessageType.Error;
            return;
        }

        var byId = entries.ToDictionary(_entry => _entry.id, StringComparer.Ordinal);
        foreach (var colorRow in tagsGroup["colors"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            string id = (string)colorRow["id"] ?? string.Empty;
            if (!byId.TryGetValue(id, out var entry))
            {
                continue;
            }
            colorRow["r"] = Math.Round(entry.color.r, 6);
            colorRow["g"] = Math.Round(entry.color.g, 6);
            colorRow["b"] = Math.Round(entry.color.b, 6);
        }

        string serialized = root.ToString(Formatting.Indented).Replace("\r\n", "\n").Replace("\n", newline) + newline;
        var issues = TableManager.ValidateSourceIssues(new Dictionary<Type, string>
        {
            { typeof(BasicDataTableData), serialized }
        });
        if (issues.Count > 0)
        {
            statusMessage = "테이블 검증 실패로 저장하지 않았습니다:\n" + string.Join("\n", issues.Select(_issue => _issue.ToString()));
            statusType = MessageType.Error;
            return;
        }

        File.WriteAllText(path, serialized, UTF8);
        AssetDatabase.Refresh();
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            TableManager.ReloadAfterTableEdit();
        }

        foreach (var entry in entries)
        {
            entry.originalColor = entry.color;
        }
        statusMessage = "BasicDataTableData.json에 태그 색상을 저장했습니다.";
        statusType = MessageType.Info;
    }

    static string ResolveDisplayName(string _id, Dictionary<string, string> _labels)
    {
        if (_id == "puzzleTagBackgroundColorColor")
        {
            return "기타 기본 태그";
        }
        if (_id == "freshBackground")
        {
            return _labels.TryGetValue("refrigerator", out string freshLabel) ? freshLabel : "신선";
        }
        if (_id == "freshTextColor")
        {
            return "신선 글자색";
        }
        if (_id == "puzzleTagTextColor")
        {
            return "상성표 기본 글자색";
        }
        if (_id.EndsWith("Background", StringComparison.Ordinal))
        {
            string prefix = _id.Substring(0, _id.Length - "Background".Length);
            if (_labels.TryGetValue(prefix, out string label))
            {
                return label;
            }
        }
        return _id;
    }

    static string ResolveNote(string _id)
    {
        return _id switch
        {
            "freshTextColor" => "커스텀 태그 자동 대비용 어두운 글자색",
            "puzzleTagTextColor" => "커스텀 태그 자동 대비용 밝은 글자색",
            _ => string.Empty
        };
    }

    static Color ReadColor(JObject _group, string _id, Color _fallback)
    {
        var row = _group?["colors"]?.OfType<JObject>().FirstOrDefault(_item => (string)_item["id"] == _id);
        return row != null ? ParseJObjectColor(row) : _fallback;
    }

    static Color ParseJObjectColor(JObject _row)
    {
        return new Color(
            (float?)_row["r"] ?? 1f,
            (float?)_row["g"] ?? 1f,
            (float?)_row["b"] ?? 1f,
            1f);
    }

    static bool ColorsEqual(Color _a, Color _b)
    {
        return Mathf.Abs(_a.r - _b.r) < 0.0005f
            && Mathf.Abs(_a.g - _b.g) < 0.0005f
            && Mathf.Abs(_a.b - _b.b) < 0.0005f;
    }

    static string ToHex(Color _color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(_color);
    }

    static bool TryParseHex(string _hex, out Color _color)
    {
        _color = default;
        if (string.IsNullOrWhiteSpace(_hex))
        {
            return false;
        }
        string normalized = _hex.Trim();
        if (!normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = "#" + normalized;
        }
        if (normalized.Length != 7)
        {
            return false;
        }
        if (!int.TryParse(normalized.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) ||
            !int.TryParse(normalized.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) ||
            !int.TryParse(normalized.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
        {
            return false;
        }
        _color = new Color(r / 255f, g / 255f, b / 255f, 1f);
        return true;
    }

    static float ContrastRatio(Color _a, Color _b)
    {
        float la = RelativeLuminance(_a);
        float lb = RelativeLuminance(_b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    static float RelativeLuminance(Color _c)
    {
        Color lin = _c.linear;
        return lin.r * 0.2126f + lin.g * 0.7152f + lin.b * 0.0722f;
    }
}
