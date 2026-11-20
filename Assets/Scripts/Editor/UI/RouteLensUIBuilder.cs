using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 행선지 렌즈 UI를 프리팹에 사전 배치하고 참조를 연결한다. 런타임 UI 생성 금지 규칙 때문에 렌즈 패널·후보 표식·설정 토글을
// 에디터에서 미리 만든다. 여러 번 실행해도 같은 결과가 나오도록 이미 있는 오브젝트는 지우고 다시 만든다.
// 위치: Docs/Research/ROUTE_LENS_IMPLEMENTATION_PLAN.md P0 실측(렌즈 영역 64, 716, 684×208)
public static class RouteLensUIBuilder
{
    const string MENU = "Tools/MyJG/UI/Build Route Lens UI";
    const string BOARD_PREFAB = "Assets/Art/PuzzleInventoryUI.prefab";
    const string LAYOUT_PREFAB = "Assets/Art/UI/GameplayLayout.prefab";
    const string CANVAS_PREFAB = "Assets/Art/UI/Scene/GameCanvas.prefab";
    const string PAUSE_PREFAB = "Assets/Art/UI/Scene/PauseCanvas.prefab";

    const string CANDIDATES_NAME = "PlacementCandidates";
    const string LENS_NAME = "RouteLens";
    const string TOGGLE_NAME = "CandidateToggleButton";
    const int CANDIDATE_COUNT = 3;

    // 기차 열(40, 140) 안에서 렌즈 영역(64, 716)의 좌상단 오프셋과 크기.
    static readonly Vector2 LENS_OFFSET = new Vector2(24f, -576f);
    static readonly Vector2 LENS_SIZE = new Vector2(684f, 208f);
    static readonly Color GOLD = new Color(0.89f, 0.70f, 0.25f, 1f);

    [MenuItem(MENU)]
    static void BuildFromMenu()
    {
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        var log = new StringBuilder();
        BuildCandidateMarkers(log);
        BuildLensPanel(log);
        WirePresenter(log);
        BuildPauseToggle(log);
        AssetDatabase.SaveAssets();
        // 기초데이터 계약 재생성은 스크립트를 다시 컴파일하게 만들므로 여기서 하지 않는다.
        // 새 문구 키를 추가했다면 Tools/MyJG/Tables/Generate Basic Data Contract를 따로 실행한다.
        return log.ToString();
    }

    // 공용 보드 프리팹의 Board 아래에 후보 표식 3개를 둔다. 기차 보드만 쓰지만 공용 원본이라 모든 보드에 생긴다(기본 비활성).
    static void BuildCandidateMarkers(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(BOARD_PREFAB);
        try
        {
            var view = root.GetComponentInChildren<PuzzleInventoryView>(true);
            var viewSo = new SerializedObject(view);
            var board = (viewSo.FindProperty("board").objectReferenceValue as Image).rectTransform;
            var outline = viewSo.FindProperty("flatOutline").objectReferenceValue as Sprite;
            var labelTemplate = viewSo.FindProperty("boardValue").objectReferenceValue as TextMeshProUGUI;

            RemoveChild(board, CANDIDATES_NAME);
            var container = NewRect(CANDIDATES_NAME, board);
            Stretch(container);

            var markers = new RectTransform[CANDIDATE_COUNT];
            var labels = new TextMeshProUGUI[CANDIDATE_COUNT];
            for (int i = 0; i < CANDIDATE_COUNT; i++)
            {
                var marker = NewRect($"Candidate{i + 1}", container);
                var image = marker.gameObject.AddComponent<Image>();
                image.sprite = outline;
                image.type = outline && outline.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                image.color = i == 0 ? GOLD : new Color(GOLD.r, GOLD.g, GOLD.b, 0.75f);
                image.raycastTarget = false;

                var label = CloneLabel(labelTemplate, marker, "Rank");
                label.fontSize = i == 0 ? 18f : 14f;
                label.color = GOLD;
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
                label.rectTransform.sizeDelta = new Vector2(80f, 24f);
                marker.gameObject.SetActive(false);
                markers[i] = marker;
                labels[i] = label;
            }

            SetArray(viewSo, "candidateMarkers", markers);
            SetArray(viewSo, "candidateLabels", labels);
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BOARD_PREFAB);
            _log.AppendLine($"후보 표식 {CANDIDATE_COUNT}개 → {BOARD_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 기차 열 안, 이동 안내 아래 빈 영역에 렌즈 패널을 둔다(E1).
    static void BuildLensPanel(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(LAYOUT_PREFAB);
        try
        {
            var trainColumn = FindDeep(root.transform, "TrainColumn");
            var trainView = trainColumn.GetComponentInChildren<PuzzleInventoryView>(true);
            var trainSo = new SerializedObject(trainView);
            var textTemplate = trainSo.FindProperty("boardValue").objectReferenceValue as TextMeshProUGUI;
            var buttonTemplate = FindDeep(trainView.transform, "Wagon Refrigeration Button")?.GetComponent<Button>();
            var panelImage = FindDeep(root.transform, "MarketInfoPanel")?.GetComponent<Image>();

            RemoveChild(trainColumn, LENS_NAME);
            var lens = NewRect(LENS_NAME, trainColumn);
            lens.anchorMin = lens.anchorMax = lens.pivot = new Vector2(0f, 1f);
            lens.anchoredPosition = LENS_OFFSET;
            lens.sizeDelta = LENS_SIZE;
            var background = lens.gameObject.AddComponent<Image>();
            if (panelImage)
            {
                background.sprite = panelImage.sprite;
                background.type = panelImage.type;
                background.color = panelImage.color;
            }
            else
            {
                background.color = new Color(0.12f, 0.14f, 0.17f, 0.92f);
            }
            background.raycastTarget = false;

            var layout = lens.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = NewRect("Header", lens);
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 6f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            var headerElement = header.gameObject.AddComponent<LayoutElement>();
            headerElement.preferredHeight = 30f;

            var title = CloneLabel(textTemplate, header, "Title");
            title.fontSize = 20f;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var tabs = new Button[3];
            var tabLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = Object.Instantiate(buttonTemplate.gameObject, header);
                tab.name = $"Tab{i}";
                tab.SetActive(true);
                var element = tab.GetComponent<LayoutElement>() ?? tab.AddComponent<LayoutElement>();
                element.preferredWidth = 96f;
                element.preferredHeight = 28f;
                tabs[i] = tab.GetComponent<Button>();
                tabs[i].onClick.RemoveAllListeners();
                // 복제한 원본 버튼의 저장된(persistent) 연결도 지운다. RemoveAllListeners는 코드로 붙인 연결만 지운다.
                while (tabs[i].onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(tabs[i].onClick, 0);
                tabLabels[i] = tab.GetComponentInChildren<TextMeshProUGUI>(true);
                tabLabels[i].fontSize = 16f;
            }

            var trade = Line(textTemplate, lens, "Trade", 18f);
            var placement = Line(textTemplate, lens, "Placement", 18f);
            var destination = Line(textTemplate, lens, "Destination", 16f);
            var footnote = Line(textTemplate, lens, "Footnote", 14f);
            footnote.color = new Color(footnote.color.r, footnote.color.g, footnote.color.b, 0.65f);

            var view = lens.gameObject.AddComponent<PuzzleRouteLensView>();
            var so = new SerializedObject(view);
            so.FindProperty("root").objectReferenceValue = lens.gameObject;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("tradeLabel").objectReferenceValue = trade;
            so.FindProperty("placementLabel").objectReferenceValue = placement;
            so.FindProperty("destinationLabel").objectReferenceValue = destination;
            so.FindProperty("footnoteLabel").objectReferenceValue = footnote;
            SetArray(so, "tabButtons", tabs);
            SetArray(so, "tabLabels", tabLabels);
            so.ApplyModifiedPropertiesWithoutUndo();
            lens.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, LAYOUT_PREFAB);
            _log.AppendLine($"렌즈 패널 → {LAYOUT_PREFAB} TrainColumn/{LENS_NAME} (탭 원본: {(buttonTemplate ? buttonTemplate.name : "없음")}, 배경: {(panelImage ? "MarketInfoPanel" : "기본색")})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WirePresenter(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(CANVAS_PREFAB);
        try
        {
            var presenter = root.GetComponentInChildren<PuzzleInventoryPresenter>(true);
            var lens = root.GetComponentsInChildren<PuzzleRouteLensView>(true).FirstOrDefault();
            var so = new SerializedObject(presenter);
            so.FindProperty("routeLens").objectReferenceValue = lens;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CANVAS_PREFAB);
            _log.AppendLine($"Presenter.routeLens = {(lens ? lens.name : "없음")} → {CANVAS_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 하단 메뉴 버튼이 없으므로 Esc 일시정지 메뉴에 후보 표시 토글을 둔다(D4).
    static void BuildPauseToggle(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(PAUSE_PREFAB);
        try
        {
            var menu = root.GetComponentInChildren<PauseMenu>(true);
            var so = new SerializedObject(menu);
            var restart = so.FindProperty("restartButton").objectReferenceValue as Button;
            var buttonProperty = so.FindProperty("candidateToggleButton");
            var labelProperty = so.FindProperty("candidateToggleLabel");
            if (!restart || buttonProperty == null || labelProperty == null)
            {
                _log.AppendLine("일시정지 토글 건너뜀: restartButton 또는 candidateToggle 필드 없음");
                return;
            }

            // 패널에 레이아웃 그룹이 없어 처음 만들 때만 아래 요소를 한 칸 내린다. 이미 있으면 참조만 다시 연결해 반복 실행해도 같다.
            var panel = (RectTransform)restart.transform.parent;
            var existing = panel.Find(TOGGLE_NAME);
            GameObject toggle;
            if (existing)
            {
                toggle = existing.gameObject;
            }
            else
            {
                toggle = Object.Instantiate(restart.gameObject, panel);
                toggle.name = TOGGLE_NAME;
                toggle.transform.SetSiblingIndex(restart.transform.GetSiblingIndex() + 1);
                MakeRoomBelow(panel, (RectTransform)restart.transform, (RectTransform)toggle.transform);
            }
            var button = toggle.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            // 복제한 원본 버튼의 저장된(persistent) 연결도 지운다. RemoveAllListeners는 코드로 붙인 연결만 지운다.
            while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
            buttonProperty.objectReferenceValue = button;
            labelProperty.objectReferenceValue = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PAUSE_PREFAB);
            _log.AppendLine($"일시정지 후보 표시 토글 → {PAUSE_PREFAB} (부모 레이아웃: {(restart.transform.parent.GetComponent<LayoutGroup>() ? "있음" : "없음 — 위치 확인 필요")})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 중앙 앵커로 놓인 패널에서 기준 버튼 바로 아래에 새 버튼 자리를 만든다. 패널을 한 칸만큼 키우고 전체를 위로 반 칸 올려 가운데를 유지한다.
    static void MakeRoomBelow(RectTransform _panel, RectTransform _anchor, RectTransform _inserted)
    {
        float step = _anchor.sizeDelta.y + 16f;
        float anchorY = _anchor.anchoredPosition.y;
        foreach (RectTransform child in _panel)
        {
            if (child != _inserted && child.anchoredPosition.y < anchorY)
            {
                child.anchoredPosition -= new Vector2(0f, step);
            }
        }
        _inserted.anchoredPosition = new Vector2(_anchor.anchoredPosition.x, anchorY - step);
        _panel.sizeDelta += new Vector2(0f, step);
        foreach (RectTransform child in _panel)
        {
            child.anchoredPosition += new Vector2(0f, step * 0.5f);
        }
    }

    static TextMeshProUGUI Line(TextMeshProUGUI _template, Transform _parent, string _name, float _size)
    {
        var label = CloneLabel(_template, _parent, _name);
        label.fontSize = _size;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    // 문구 템플릿의 글꼴·재질을 그대로 쓰는 새 라벨. 템플릿의 자식은 지운다.
    static TextMeshProUGUI CloneLabel(TextMeshProUGUI _template, Transform _parent, string _name)
    {
        var go = Object.Instantiate(_template.gameObject, _parent);
        go.name = _name;
        go.SetActive(true);
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }
        foreach (var behaviour in go.GetComponents<MonoBehaviour>().Where(_component => !(_component is TextMeshProUGUI)).ToList())
        {
            Object.DestroyImmediate(behaviour);
        }
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = string.Empty;
        label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return label;
    }

    static RectTransform NewRect(string _name, Transform _parent)
    {
        var go = new GameObject(_name, typeof(RectTransform));
        go.layer = _parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        return rect;
    }

    static void Stretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = _rect.offsetMax = Vector2.zero;
    }

    static void RemoveChild(Transform _parent, string _name)
    {
        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            if (_parent.GetChild(i).name == _name)
            {
                Object.DestroyImmediate(_parent.GetChild(i).gameObject);
            }
        }
    }

    static Transform FindDeep(Transform _parent, string _name)
    {
        foreach (Transform child in _parent)
        {
            if (child.name == _name)
            {
                return child;
            }
            var found = FindDeep(child, _name);
            if (found)
            {
                return found;
            }
        }
        return null;
    }

    static void SetArray<T>(SerializedObject _so, string _field, IReadOnlyList<T> _values) where T : Object
    {
        var property = _so.FindProperty(_field);
        property.arraySize = _values.Count;
        for (int i = 0; i < _values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
        }
    }
}
