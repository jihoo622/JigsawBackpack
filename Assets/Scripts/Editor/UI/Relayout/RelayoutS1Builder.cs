using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// S1: 부동 창 시스템(FloatingWindowCanvas), UIPalette 에셋, 일시정지 '창 배치 초기화' 버튼,
// 분리된 결산 모달(RunEndModal) 및 OverlayCanvas를 프리팹에 멱등적으로 구성한다.
public static class RelayoutS1Builder
{
    const string MENU = "Tools/MyJG/UI/Relayout/S1 Build Window System";
    const string PALETTE_DIR = "Assets/Resources/UI";
    const string PALETTE_PATH = "Assets/Resources/UI/UIPalette.asset";
    const string RELAYOUT_PREFAB_DIR = "Assets/Art/UI/Relayout";
    const string RUN_END_PREFAB_PATH = "Assets/Art/UI/Relayout/RunEndModal.prefab";
    const string PAUSE_PREFAB_PATH = "Assets/Art/UI/Scene/PauseCanvas.prefab";
    const string GAME_CANVAS_PREFAB_PATH = "Assets/Art/UI/Scene/GameCanvas.prefab";

    const string RESET_LAYOUT_BTN_NAME = "ResetLayoutButton";
    const string FLOATING_CANVAS_NAME = "FloatingWindowCanvas";
    const string OVERLAY_CANVAS_NAME = "OverlayCanvas";
    const string RUN_END_MODAL_NAME = "RunEndModal";

    [MenuItem(MENU)]
    static void BuildFromMenu()
    {
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        var log = new StringBuilder();
        var palette = EnsurePaletteAsset(log);
        BuildPauseResetButton(log);
        var runEndPrefab = BuildRunEndModalPrefab(log, palette);
        UpdateGameCanvas(log, runEndPrefab);
        FitObjectiveModal(log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    const string OBJECTIVE_PREFAB_PATH = "Assets/Art/UI/RepaymentObjective.prefab";

    // 시작 목표 안내 창: 지도 미리보기를 숨긴 뒤(PuzzleObjectiveView) 1440×824 창에 큰 빈 자리가 남았다.
    // 목표 금액 · 안내문 · 확인 버튼만 담는 1000×360 창으로 줄이고(안내문 두 줄) 위에서 아래로 차례로 둔다(여백 24, 간격 16).
    static void FitObjectiveModal(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(OBJECTIVE_PREFAB_PATH);
        try
        {
            var popup = root.transform.Find("Overlay/Popup") as RectTransform;
            if (!popup)
            {
                _log.AppendLine("목표 안내 창: Overlay/Popup 없음, 건너뜀");
                return;
            }
            popup.sizeDelta = new Vector2(1000f, 360f);
            PlaceCentered(popup, "RepaymentCost", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(504f, 76f));
            PlaceCentered(popup, "Objective", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(940f, 104f));
            PlaceCentered(popup, "Confirm", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(264f, 72f));
            PrefabUtility.SaveAsPrefabAsset(root, OBJECTIVE_PREFAB_PATH);
            _log.AppendLine("목표 안내 창 크기 정리 → " + OBJECTIVE_PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void PlaceCentered(RectTransform _parent, string _name, Vector2 _anchor, Vector2 _pos, Vector2 _size)
    {
        var rt = _parent.Find(_name) as RectTransform;
        if (!rt) return;
        rt.anchorMin = _anchor;
        rt.anchorMax = _anchor;
        rt.pivot = _anchor;
        rt.anchoredPosition = _pos;
        rt.sizeDelta = _size;
    }

    static UIPalette EnsurePaletteAsset(StringBuilder _log)
    {
        if (!Directory.Exists(PALETTE_DIR))
        {
            Directory.CreateDirectory(PALETTE_DIR);
        }
        var palette = AssetDatabase.LoadAssetAtPath<UIPalette>(PALETTE_PATH);
        if (!palette)
        {
            palette = ScriptableObject.CreateInstance<UIPalette>();
            AssetDatabase.CreateAsset(palette, PALETTE_PATH);
            _log.AppendLine($"UIPalette 생성 → {PALETTE_PATH}");
        }
        else
        {
            _log.AppendLine($"UIPalette 확인 → {PALETTE_PATH}");
        }
        return palette;
    }

    static void BuildPauseResetButton(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(PAUSE_PREFAB_PATH);
        try
        {
            var menu = root.GetComponentInChildren<PauseMenu>(true);
            if (!menu)
            {
                _log.AppendLine($"일시정지 버튼 건너뜀: PauseMenu 없음 ({PAUSE_PREFAB_PATH})");
                return;
            }

            var so = new SerializedObject(menu);
            var anchorBtn = so.FindProperty("candidateToggleButton").objectReferenceValue as Button;
            if (!anchorBtn)
            {
                anchorBtn = so.FindProperty("restartButton").objectReferenceValue as Button;
            }
            if (!anchorBtn)
            {
                _log.AppendLine("일시정지 버튼 건너뜀: 기준 버튼 없음");
                return;
            }

            var panel = (RectTransform)anchorBtn.transform.parent;
            var existing = panel.Find(RESET_LAYOUT_BTN_NAME);
            GameObject resetGo;
            if (existing)
            {
                resetGo = existing.gameObject;
            }
            else
            {
                resetGo = Object.Instantiate(anchorBtn.gameObject, panel);
                resetGo.name = RESET_LAYOUT_BTN_NAME;
                resetGo.SetActive(true);
                resetGo.transform.SetSiblingIndex(anchorBtn.transform.GetSiblingIndex() + 1);
                MakeRoomBelow(panel, (RectTransform)anchorBtn.transform, (RectTransform)resetGo.transform);
            }

            var button = resetGo.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            // 복제한 원본 버튼의 저장된(persistent) 연결도 지운다. RemoveAllListeners는 코드로 붙인 연결만 지운다.
            while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
            var label = resetGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label)
            {
                label.text = "창 배치 초기화";
            }

            so.FindProperty("resetLayoutButton").objectReferenceValue = button;
            so.FindProperty("resetLayoutLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PAUSE_PREFAB_PATH);
            _log.AppendLine($"일시정지 '창 배치 초기화' 버튼 → {PAUSE_PREFAB_PATH}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static GameObject BuildRunEndModalPrefab(StringBuilder _log, UIPalette _palette)
    {
        if (!Directory.Exists(RELAYOUT_PREFAB_DIR))
        {
            Directory.CreateDirectory(RELAYOUT_PREFAB_DIR);
        }

        // 기존 PauseCanvas 프리팹에서 버튼·TMP 글꼴 템플릿을 가져와 프로젝트 규격(NotoSansKR-Medium SDF)을 유지한다.
        Button buttonTemplate = null;
        TextMeshProUGUI labelTemplate = null;
        var pauseTemplateRoot = PrefabUtility.LoadPrefabContents(PAUSE_PREFAB_PATH);
        try
        {
            var pauseMenu = pauseTemplateRoot.GetComponentInChildren<PauseMenu>(true);
            if (pauseMenu)
            {
                var pauseSo = new SerializedObject(pauseMenu);
                buttonTemplate = pauseSo.FindProperty("restartButton").objectReferenceValue as Button;
                labelTemplate = pauseSo.FindProperty("panelTitle").objectReferenceValue as TextMeshProUGUI;
            }
        }
        catch
        {
            // 템플릿 조회 실패 시 아래에서 안전하게 처리.
        }

        var rootGo = new GameObject(RUN_END_MODAL_NAME, typeof(RectTransform));
        try
        {
            var rootRect = (RectTransform)rootGo.transform;
            rootGo.layer = 5; // UI
            Stretch(rootRect);

            // 반투명 딤 배경
            var backdrop = rootGo.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.65f);
            backdrop.raycastTarget = true;

            // 모달 본체: (640, 210, 640, 660) 좌상단 원점 기준
            var modalRect = NewRect("ModalPanel", rootRect);
            modalRect.anchorMin = new Vector2(0f, 1f);
            modalRect.anchorMax = new Vector2(0f, 1f);
            modalRect.pivot = new Vector2(0f, 1f);
            modalRect.anchoredPosition = new Vector2(640f, -210f);
            modalRect.sizeDelta = new Vector2(640f, 660f);

            var modalBg = modalRect.gameObject.AddComponent<Image>();
            modalBg.color = _palette.Panel;
            BindPalette(modalRect.gameObject, _palette, UIPalette.ColorRole.Panel);

            var outline = modalRect.gameObject.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(2f, -2f);
            var borderLine = NewRect("TopAccent", modalRect);
            borderLine.anchorMin = new Vector2(0f, 1f);
            borderLine.anchorMax = new Vector2(1f, 1f);
            borderLine.pivot = new Vector2(0.5f, 1f);
            borderLine.anchoredPosition = Vector2.zero;
            borderLine.sizeDelta = new Vector2(0f, 4f);
            var topAccentImg = borderLine.gameObject.AddComponent<Image>();
            topAccentImg.color = _palette.GoldAccent;
            topAccentImg.raycastTarget = false;
            BindPalette(borderLine.gameObject, _palette, UIPalette.ColorRole.GoldAccent);

            // 헤더 (32pt Display)
            var headerText = CreateLabel(labelTemplate, modalRect, "HeaderText", 32f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.GoldAccent);
            headerText.fontStyle = FontStyles.Bold;
            headerText.text = "결산 결과";
            PlaceTop(headerText.rectTransform, 32f, 52f, 40f);

            // 구분선
            var divider = NewRect("Divider", modalRect);
            var divImg = divider.gameObject.AddComponent<Image>();
            divImg.color = _palette.Border;
            divImg.raycastTarget = false;
            BindPalette(divider.gameObject, _palette, UIPalette.ColorRole.Border);
            PlaceTop(divider, 96f, 2f, 40f);

            // 통계 5줄: 최종 잔액, 목표 자금, 목표 차액, 경과 턴, 완료 의뢰
            float rowY = 118f;
            const float rowH = 44f;
            const float rowGap = 8f;

            CreateStatRow(labelTemplate, modalRect, "FundsRow", rowY, rowH, "최종 잔액", _palette,
                out var fundsLabel, out var fundsValue);
            rowY += rowH + rowGap;

            CreateStatRow(labelTemplate, modalRect, "GoalRow", rowY, rowH, "목표 자금", _palette,
                out var goalLabel, out var goalValue);
            rowY += rowH + rowGap;

            CreateStatRow(labelTemplate, modalRect, "DiffRow", rowY, rowH, "목표 차액", _palette,
                out var diffLabel, out var diffValue);
            rowY += rowH + rowGap;

            CreateStatRow(labelTemplate, modalRect, "TurnsRow", rowY, rowH, "경과 턴", _palette,
                out var turnsLabel, out var turnsValue);
            rowY += rowH + rowGap;

            CreateStatRow(labelTemplate, modalRect, "QuestsRow", rowY, rowH, "완료 의뢰", _palette,
                out var questsLabel, out var questsValue);

            // 하단 버튼 3개: 무한 모드 계속(승리 시만), 다시 시작, 종료
            float btnY = 408f;
            const float btnW = 360f;
            const float btnH = 56f;
            const float btnGap = 14f;

            var endlessBtn = CreateButton(buttonTemplate, labelTemplate, modalRect, "EndlessButton",
                btnY, btnW, btnH, "무한 모드로 계속", _palette, out var endlessLbl);
            btnY += btnH + btnGap;

            var restartBtn = CreateButton(buttonTemplate, labelTemplate, modalRect, "RestartButton",
                btnY, btnW, btnH, "다시 시작", _palette, out var restartLbl);
            btnY += btnH + btnGap;

            var quitBtn = CreateButton(buttonTemplate, labelTemplate, modalRect, "QuitButton",
                btnY, btnW, btnH, "종료", _palette, out var quitLbl);

            var view = rootGo.AddComponent<RunEndView>();

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("startOpen").boolValue = false;
            viewSo.FindProperty("root").objectReferenceValue = rootGo;
            viewSo.FindProperty("headerText").objectReferenceValue = headerText;
            viewSo.FindProperty("fundsLabelText").objectReferenceValue = fundsLabel;
            viewSo.FindProperty("fundsValueText").objectReferenceValue = fundsValue;
            viewSo.FindProperty("goalLabelText").objectReferenceValue = goalLabel;
            viewSo.FindProperty("goalValueText").objectReferenceValue = goalValue;
            viewSo.FindProperty("diffLabelText").objectReferenceValue = diffLabel;
            viewSo.FindProperty("diffValueText").objectReferenceValue = diffValue;
            viewSo.FindProperty("turnsLabelText").objectReferenceValue = turnsLabel;
            viewSo.FindProperty("turnsValueText").objectReferenceValue = turnsValue;
            viewSo.FindProperty("questsLabelText").objectReferenceValue = questsLabel;
            viewSo.FindProperty("questsValueText").objectReferenceValue = questsValue;
            viewSo.FindProperty("endlessButton").objectReferenceValue = endlessBtn;
            viewSo.FindProperty("endlessLabel").objectReferenceValue = endlessLbl;
            viewSo.FindProperty("restartButton").objectReferenceValue = restartBtn;
            viewSo.FindProperty("restartLabel").objectReferenceValue = restartLbl;
            viewSo.FindProperty("quitButton").objectReferenceValue = quitBtn;
            viewSo.FindProperty("quitLabel").objectReferenceValue = quitLbl;
            viewSo.FindProperty("palette").objectReferenceValue = _palette;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            rootGo.SetActive(false);
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, RUN_END_PREFAB_PATH);
            _log.AppendLine($"RunEndModal 프리팹 생성 → {RUN_END_PREFAB_PATH}");
            return savedPrefab;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(pauseTemplateRoot);
        }
    }

    static void UpdateGameCanvas(StringBuilder _log, GameObject _runEndPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB_PATH);
        try
        {
            var floatingCanvas = EnsureSubCanvas(root.transform, FLOATING_CANVAS_NAME, 50);
            if (!floatingCanvas.GetComponent<FloatingWindowManager>())
            {
                floatingCanvas.AddComponent<FloatingWindowManager>();
            }

            var overlayCanvas = EnsureSubCanvas(root.transform, OVERLAY_CANVAS_NAME, 200);
            RemoveChildrenNamed(overlayCanvas.transform, RUN_END_MODAL_NAME);
            var modalInstance = (GameObject)PrefabUtility.InstantiatePrefab(_runEndPrefab, overlayCanvas.transform);
            modalInstance.name = RUN_END_MODAL_NAME;

            var inventoryInput = root.GetComponentInChildren<PuzzleInventoryInput>(true);
            var oldModalPresenter = modalInstance.GetComponent<RunEndPresenter>();
            if (oldModalPresenter) Object.DestroyImmediate(oldModalPresenter);
            var runEndPresenter = overlayCanvas.GetComponent<RunEndPresenter>() ?? overlayCanvas.AddComponent<RunEndPresenter>();
            var pSo = new SerializedObject(runEndPresenter);
            pSo.FindProperty("view").objectReferenceValue = modalInstance.GetComponent<RunEndView>();
            if (inventoryInput)
            {
                pSo.FindProperty("inventoryInput").objectReferenceValue = inventoryInput;
            }
            pSo.ApplyModifiedPropertiesWithoutUndo();

            var invPresenter = root.GetComponentInChildren<PuzzleInventoryPresenter>(true);
            if (invPresenter && runEndPresenter)
            {
                var iSo = new SerializedObject(invPresenter);
                var prop = iSo.FindProperty("runEndPresenter");
                if (prop != null)
                {
                    prop.objectReferenceValue = runEndPresenter;
                    iSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB_PATH);
            _log.AppendLine($"GameCanvas 업데이트 (FloatingWindowCanvas:50, OverlayCanvas:200, RunEndModal) → {GAME_CANVAS_PREFAB_PATH}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static GameObject EnsureSubCanvas(Transform _parent, string _name, int _sortingOrder)
    {
        var existing = _parent.Find(_name);
        GameObject go;
        if (existing)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform));
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
        }

        Stretch((RectTransform)go.transform);
        var canvas = go.GetComponent<Canvas>();
        if (!canvas)
        {
            canvas = go.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = _sortingOrder;
        if (!go.GetComponent<GraphicRaycaster>())
        {
            go.AddComponent<GraphicRaycaster>();
        }
        return go;
    }

    static void CreateStatRow(
        TextMeshProUGUI _template,
        RectTransform _parent,
        string _name,
        float _topY,
        float _height,
        string _defaultLabel,
        UIPalette _palette,
        out TextMeshProUGUI _label,
        out TextMeshProUGUI _value)
    {
        var row = NewRect(_name, _parent);
        var bg = row.gameObject.AddComponent<Image>();
        bg.color = _palette.Panel2;
        bg.raycastTarget = false;
        BindPalette(row.gameObject, _palette, UIPalette.ColorRole.Panel2);
        PlaceTop(row, _topY, _height, 40f);

        _label = CreateLabel(_template, row, "Label", 20f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
        _label.text = _defaultLabel;
        var lRect = _label.rectTransform;
        lRect.anchorMin = Vector2.zero;
        lRect.anchorMax = new Vector2(0.5f, 1f);
        lRect.offsetMin = new Vector2(20f, 0f);
        lRect.offsetMax = Vector2.zero;

        _value = CreateLabel(_template, row, "Value", 24f, TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.Text);
        _value.fontStyle = FontStyles.Bold;
        _value.text = "-";
        var vRect = _value.rectTransform;
        vRect.anchorMin = new Vector2(0.5f, 0f);
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = Vector2.zero;
        vRect.offsetMax = new Vector2(-20f, 0f);
    }

    static Button CreateButton(
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate,
        RectTransform _parent,
        string _name,
        float _topY,
        float _width,
        float _height,
        string _text,
        UIPalette _palette,
        out TextMeshProUGUI _label)
    {
        GameObject go;
        if (_buttonTemplate)
        {
            go = Object.Instantiate(_buttonTemplate.gameObject, _parent);
            go.name = _name;
            go.SetActive(true);
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_parent, false);
        }

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -_topY);
        rect.sizeDelta = new Vector2(_width, _height);

        var button = go.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        // 복제한 원본 버튼의 저장된(persistent) 연결도 지운다. RemoveAllListeners는 코드로 붙인 연결만 지운다.
        while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);

        _label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!_label)
        {
            _label = CreateLabel(_labelTemplate, rect, "Label", 20f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
            Stretch(_label.rectTransform);
        }
        _label.fontSize = 20f;
        _label.text = _text;
        return button;
    }

    static TextMeshProUGUI CreateLabel(
        TextMeshProUGUI _template,
        RectTransform _parent,
        string _name,
        float _fontSize,
        TextAlignmentOptions _alignment,
        UIPalette _palette,
        UIPalette.ColorRole _role)
    {
        GameObject go;
        if (_template)
        {
            go = Object.Instantiate(_template.gameObject, _parent);
            go.name = _name;
            go.SetActive(true);
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            }
            foreach (var mb in go.GetComponents<MonoBehaviour>().Where(_c => !(_c is TextMeshProUGUI)).ToList())
            {
                Object.DestroyImmediate(mb);
            }
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_parent, false);
        }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = _fontSize;
        tmp.alignment = _alignment;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        tmp.color = _palette.GetColor(_role);
        BindPalette(go, _palette, _role);
        return tmp;
    }

    static void BindPalette(GameObject _go, UIPalette _palette, UIPalette.ColorRole _role)
    {
        var binder = _go.GetComponent<UIPaletteBinder>();
        if (!binder)
        {
            binder = _go.AddComponent<UIPaletteBinder>();
        }
        var so = new SerializedObject(binder);
        so.FindProperty("palette").objectReferenceValue = _palette;
        so.FindProperty("role").enumValueIndex = (int)_role;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void PlaceTop(RectTransform _rect, float _topY, float _height, float _horizontalPadding)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(1f, 1f);
        _rect.pivot = new Vector2(0.5f, 1f);
        _rect.anchoredPosition = new Vector2(0f, -_topY);
        _rect.sizeDelta = new Vector2(-_horizontalPadding * 2f, _height);
    }

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

    static void RemoveChildrenNamed(Transform _parent, string _name)
    {
        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            if (_parent.GetChild(i).name == _name)
            {
                Object.DestroyImmediate(_parent.GetChild(i).gameObject);
            }
        }
    }
}
