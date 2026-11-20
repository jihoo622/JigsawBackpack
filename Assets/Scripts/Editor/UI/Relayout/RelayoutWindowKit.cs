using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 개편 부동 창의 공용 틀 프리팹(FloatingWindowFrame)과 배치 기준값(UILayoutTokens)을 관리한다.
// 모든 창 프리팹은 이 틀의 프리팹 변형(Variant)으로 만들어진다. 틀의 제목줄·닫기·바탕·테두리를 바꾸면 모든 창에 반영된다.
// 틀 프리팹은 처음 한 번만 새로 만들고, 그 뒤에는 같은 오브젝트를 고쳐 저장한다(변형의 참조가 끊기지 않도록).
public static class RelayoutWindowKit
{
    public const string FRAME_PREFAB = "Assets/Art/UI/Relayout/FloatingWindowFrame.prefab";
    public const string TOKENS_PATH = "Assets/Resources/UI/UILayoutTokens.asset";
    const string PALETTE_PATH = "Assets/Resources/UI/UIPalette.asset";

    [MenuItem("Tools/MyJG/UI/Relayout/Build Window Frame")]
    static void Menu()
    {
        var frame = EnsureFramePrefab(null);
        Debug.Log("[RelayoutWindowKit] 틀 프리팹: " + AssetDatabase.GetAssetPath(frame));
    }

    public static UILayoutTokens LoadTokens()
    {
        var tokens = AssetDatabase.LoadAssetAtPath<UILayoutTokens>(TOKENS_PATH);
        if (tokens) return tokens;
        tokens = ScriptableObject.CreateInstance<UILayoutTokens>();
        AssetDatabase.CreateAsset(tokens, TOKENS_PATH);
        AssetDatabase.SaveAssets();
        return tokens;
    }

    // 틀 프리팹을 만들거나(없을 때) 기준값·팔레트에 맞게 고쳐 저장한다.
    public static GameObject EnsureFramePrefab(TextMeshProUGUI _labelTemplate)
    {
        var tokens = LoadTokens();
        var palette = AssetDatabase.LoadAssetAtPath<UIPalette>(PALETTE_PATH);
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(FRAME_PREFAB))
        {
            var created = new GameObject("FloatingWindowFrame", typeof(RectTransform));
            try
            {
                BuildFrameHierarchy(created, _labelTemplate);
                PrefabUtility.SaveAsPrefabAsset(created, FRAME_PREFAB);
            }
            finally
            {
                Object.DestroyImmediate(created);
            }
        }

        var root = PrefabUtility.LoadPrefabContents(FRAME_PREFAB);
        try
        {
            ApplyFrame(root, tokens, palette);
            PrefabUtility.SaveAsPrefabAsset(root, FRAME_PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(FRAME_PREFAB);
    }

    // 틀 프리팹의 변형을 장면에 하나 만들고 창 고유 값을 넣는다. 호출한 쪽이 내용을 붙인 뒤 SaveAsPrefabAsset으로 저장한다.
    public static GameObject CreateWindow(
        string _name,
        string _windowId,
        string _title,
        float _x,
        float _y,
        float _w,
        float _h,
        bool _startOpen,
        out FloatingWindow _floatingWindow,
        out RectTransform _titleBar,
        out TextMeshProUGUI _titleText,
        out Button _closeButton)
    {
        var frame = AssetDatabase.LoadAssetAtPath<GameObject>(FRAME_PREFAB);
        if (!frame) frame = EnsureFramePrefab(null);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(frame);
        go.name = _name;
        var rt = (RectTransform)go.transform;
        SetTopLeftRect(rt, _x, _y, _w, _h);

        _floatingWindow = go.GetComponent<FloatingWindow>();
        _titleBar = (RectTransform)go.transform.Find("TitleBar");
        _titleText = _titleBar.Find("TitleText").GetComponent<TextMeshProUGUI>();
        _closeButton = _titleBar.Find("CloseButton").GetComponent<Button>();
        _titleText.text = _title;

        var so = new SerializedObject(_floatingWindow);
        if (!string.IsNullOrEmpty(_windowId)) so.FindProperty("windowId").stringValue = _windowId;
        so.FindProperty("defaultPosition").vector2Value = new Vector2(_x, _y);
        so.FindProperty("defaultSize").vector2Value = new Vector2(_w, _h);
        so.FindProperty("startOpen").boolValue = _startOpen;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    // 제목 글자 오른쪽 끝을 비워 둘 폭을 정한다(닫기 버튼 외에 제목줄 버튼이 더 있을 때).
    public static void ReserveTitleRight(TextMeshProUGUI _titleText, float _rightReserve)
    {
        var t = LoadTokens();
        _titleText.rectTransform.offsetMax = new Vector2(-_rightReserve, 0f);
        _titleText.rectTransform.offsetMin = new Vector2(t.titleInset, 0f);
    }

    static void BuildFrameHierarchy(GameObject _root, TextMeshProUGUI _labelTemplate)
    {
        _root.layer = 5;
        _root.AddComponent<Image>();
        _root.AddComponent<Outline>();
        var fw = _root.AddComponent<FloatingWindow>();

        var titleBar = NewRect("TitleBar", _root.transform);
        titleBar.gameObject.AddComponent<Image>();
        titleBar.gameObject.AddComponent<FloatingWindowDragHandle>();

        NewLabel(_labelTemplate, titleBar, "TitleText");

        var close = NewRect("CloseButton", titleBar);
        var closeImg = close.gameObject.AddComponent<Image>();
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        NewLabel(_labelTemplate, close, "Label");

        // 창 내용의 기준 영역. 제목줄 아래, 창 여백 안쪽.
        NewRect("Content", _root.transform);

        var so = new SerializedObject(fw);
        so.FindProperty("windowId").stringValue = "frame";
        so.FindProperty("titleBar").objectReferenceValue = titleBar;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("startOpen").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 기준값과 팔레트를 틀에 적용한다. 오브젝트를 새로 만들지 않으므로 여러 번 실행해도 같다.
    static void ApplyFrame(GameObject _root, UILayoutTokens _t, UIPalette _p)
    {
        var rootRt = (RectTransform)_root.transform;
        SetTopLeftRect(rootRt, 120f, 120f, 480f, 400f);

        var bg = _root.GetComponent<Image>();
        bg.raycastTarget = true;
        Paint(bg, _p, UIPalette.ColorRole.Panel);

        var outline = _root.GetComponent<Outline>();
        outline.effectColor = _p ? _p.Border : Color.gray;
        outline.effectDistance = new Vector2(_t.borderWidth, -_t.borderWidth);

        var titleBar = (RectTransform)rootRt.Find("TitleBar");
        titleBar.anchorMin = new Vector2(0f, 1f);
        titleBar.anchorMax = new Vector2(1f, 1f);
        titleBar.pivot = new Vector2(0.5f, 1f);
        titleBar.anchoredPosition = Vector2.zero;
        titleBar.sizeDelta = new Vector2(0f, _t.titleBarHeight);
        Paint(titleBar.GetComponent<Image>(), _p, UIPalette.ColorRole.Panel2);

        var title = titleBar.Find("TitleText").GetComponent<TextMeshProUGUI>();
        title.fontSize = _t.fontTitle;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.raycastTarget = false;
        PaintText(title, _p, UIPalette.ColorRole.TitleText);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.offsetMin = new Vector2(_t.titleInset, 0f);
        titleRt.offsetMax = new Vector2(-(_t.closeButtonSize + _t.closeButtonInset * 2f), 0f);

        var close = (RectTransform)titleBar.Find("CloseButton");
        close.anchorMin = new Vector2(1f, 0.5f);
        close.anchorMax = new Vector2(1f, 0.5f);
        close.pivot = new Vector2(1f, 0.5f);
        close.anchoredPosition = new Vector2(-_t.closeButtonInset, 0f);
        close.sizeDelta = new Vector2(_t.closeButtonSize, _t.closeButtonSize);
        var closeImg = close.GetComponent<Image>();
        closeImg.sprite = null;
        closeImg.type = Image.Type.Simple;
        Paint(closeImg, _p, UIPalette.ColorRole.Panel);
        var closeBtn = close.GetComponent<Button>();
        var colors = closeBtn.colors;
        colors.disabledColor = colors.normalColor;
        closeBtn.colors = colors;

        var closeLabel = close.Find("Label").GetComponent<TextMeshProUGUI>();
        closeLabel.text = "X";
        closeLabel.fontSize = _t.fontButton;
        closeLabel.fontStyle = FontStyles.Bold;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.enableWordWrapping = false;
        closeLabel.raycastTarget = false;
        PaintText(closeLabel, _p, UIPalette.ColorRole.Text);
        Stretch(closeLabel.rectTransform);

        var content = (RectTransform)rootRt.Find("Content");
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(_t.windowPadding, _t.windowPadding);
        content.offsetMax = new Vector2(-_t.windowPadding, -_t.ContentTop);

        ApplyFocusBorder(rootRt, _t, _p);
    }

    public const string FOCUS_BORDER_NAME = "FocusBorder";

    // 포커스된 창 표시(R57): 기본 테두리(Outline, 창 밖 borderWidth) 자리에 금색 선 4개를 겹쳐 그린다. 평소에는 꺼 둔다.
    // Outline 색을 바꾸면 반투명 바탕 뒤로 그 색이 비쳐 창 전체가 물들어서 선을 따로 둔다.
    static void ApplyFocusBorder(RectTransform _rootRt, UILayoutTokens _t, UIPalette _p)
    {
        var border = _rootRt.Find(FOCUS_BORDER_NAME) as RectTransform;
        if (!border)
        {
            border = NewRect(FOCUS_BORDER_NAME, _rootRt);
        }
        Stretch(border);
        border.SetAsLastSibling();
        float w = _t.borderWidth;
        Color gold = _p ? _p.GoldAccent : new Color(0.9412f, 0.7059f, 0.1608f, 1f);
        FocusEdge(border, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(w * 2f, w), gold);
        FocusEdge(border, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(w * 2f, w), gold);
        FocusEdge(border, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(w, 0f), gold);
        FocusEdge(border, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(w, 0f), gold);
        border.gameObject.SetActive(false);
    }

    static void FocusEdge(RectTransform _parent, string _side, Vector2 _min, Vector2 _max, Vector2 _pivot, Vector2 _size, Color _color)
    {
        var t = _parent.Find(_side) as RectTransform;
        if (!t)
        {
            t = NewRect(_side, _parent);
            t.gameObject.AddComponent<Image>();
        }
        t.anchorMin = _min;
        t.anchorMax = _max;
        t.pivot = _pivot;
        t.anchoredPosition = Vector2.zero;
        t.sizeDelta = _size;
        var img = t.GetComponent<Image>();
        img.sprite = null;
        img.color = _color;
        img.raycastTarget = false;
    }

    const string FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSansKR-Medium SDF.asset";

    static TextMeshProUGUI NewLabel(TextMeshProUGUI _template, Transform _parent, string _name)
    {
        var label = NewLabelCore(_template, _parent, _name);
        // 템플릿 없이 만들어도 한글이 나오도록 글꼴을 고정한다.
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
        if (font) label.font = font;
        return label;
    }

    static TextMeshProUGUI NewLabelCore(TextMeshProUGUI _template, Transform _parent, string _name)
    {
        GameObject go;
        if (_template)
        {
            go = Object.Instantiate(_template.gameObject, _parent);
            go.name = _name;
            go.SetActive(true);
            // 틀 글자에는 템플릿의 팔레트 연결 외 다른 컴포넌트가 필요 없다.
            foreach (var c in go.GetComponents<Component>())
            {
                if (c is Transform || c is CanvasRenderer || c is TextMeshProUGUI) continue;
                Object.DestroyImmediate(c);
            }
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_parent, false);
        }
        go.layer = 5;
        return go.GetComponent<TextMeshProUGUI>();
    }

    static void Paint(Graphic _g, UIPalette _p, UIPalette.ColorRole _role)
    {
        if (!_p) return;
        _g.color = _p.GetColor(_role);
        BindPalette(_g.gameObject, _p, _role);
    }

    static void PaintText(TextMeshProUGUI _t, UIPalette _p, UIPalette.ColorRole _role) => Paint(_t, _p, _role);

    static void BindPalette(GameObject _go, UIPalette _palette, UIPalette.ColorRole _role)
    {
        var binder = _go.GetComponent<UIPaletteBinder>();
        if (!binder) binder = _go.AddComponent<UIPaletteBinder>();
        var so = new SerializedObject(binder);
        so.FindProperty("palette").objectReferenceValue = _palette;
        so.FindProperty("role").enumValueIndex = (int)_role;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    const string CANDIDATE_EDGE_PREFIX = "CandidateEdge_";

    // 배치 후보 표식: 옅은 채움 + 2px 테두리 선 4개.
    // uGUI Outline 효과는 테두리만이 아니라 채움 전체를 불투명한 효과 색으로 네 번 더 그려서 반투명 채움이 불투명해진다. 그래서 쓰지 않는다.
    public static void StyleCandidateMarker(RectTransform _marker, Color _fill, Color _border, float _borderWidth = 2f)
    {
        var outline = _marker.GetComponent<Outline>();
        if (outline) Object.DestroyImmediate(outline, true);

        var img = _marker.GetComponent<Image>();
        if (img)
        {
            img.enabled = true;
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = _fill;
            img.raycastTarget = false;
        }
        // 옛 표식의 자식 그림(윗선 등)은 끄고, 테두리 선만 쓴다.
        for (int i = 0; i < _marker.childCount; i++)
        {
            var child = _marker.GetChild(i);
            if (child.name.StartsWith(CANDIDATE_EDGE_PREFIX)) continue;
            var childImg = child.GetComponent<Image>();
            if (childImg) childImg.enabled = false;
        }
        Edge(_marker, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, _borderWidth), _border);
        Edge(_marker, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, _borderWidth), _border);
        Edge(_marker, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(_borderWidth, 0f), _border);
        Edge(_marker, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(_borderWidth, 0f), _border);
    }

    static void Edge(RectTransform _parent, string _side, Vector2 _min, Vector2 _max, Vector2 _pivot, Vector2 _size, Color _color)
    {
        var t = _parent.Find(CANDIDATE_EDGE_PREFIX + _side) as RectTransform;
        if (!t)
        {
            t = NewRect(CANDIDATE_EDGE_PREFIX + _side, _parent);
            t.gameObject.AddComponent<Image>();
        }
        t.anchorMin = _min;
        t.anchorMax = _max;
        t.pivot = _pivot;
        t.anchoredPosition = Vector2.zero;
        t.sizeDelta = _size;
        var img = t.GetComponent<Image>();
        img.enabled = true;
        img.sprite = null;
        img.color = _color;
        img.raycastTarget = false;
    }

    public static RectTransform NewRect(string _name, Transform _parent)
    {
        var go = new GameObject(_name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_parent, false);
        return rt;
    }

    public static void Stretch(RectTransform _rt)
    {
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.one;
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }

    public static void SetTopLeftRect(RectTransform _rt, float _x, float _y, float _w, float _h)
    {
        _rt.anchorMin = new Vector2(0f, 1f);
        _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot = new Vector2(0f, 1f);
        _rt.anchoredPosition = new Vector2(_x, -_y);
        _rt.sizeDelta = new Vector2(_w, _h);
    }
}
