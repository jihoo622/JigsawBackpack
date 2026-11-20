using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// S5: 시장 창(MarketWindow, 13×9 56px), 행선지 렌즈 창(LensWindow), 도구줄 렌즈 버튼 및 GameCanvas 배선.
public static class RelayoutS5Builder
{
    const string MENU = "Tools/MyJG/UI/Relayout/S5 Build Market And Lens";
    const string RELAYOUT_DIR = "Assets/Art/UI/Relayout";
    const string MARKET_WINDOW_PREFAB = RELAYOUT_DIR + "/MarketWindow.prefab";
    const string LENS_WINDOW_PREFAB = RELAYOUT_DIR + "/LensWindow.prefab";
    const string QUICK_TOOLBAR_PREFAB = RELAYOUT_DIR + "/QuickToolbar.prefab";
    const string PALETTE_ASSET = "Assets/Resources/UI/UIPalette.asset";
    const string GAMEPLAY_LAYOUT_PREFAB = "Assets/Art/UI/GameplayLayout.prefab";
    const string GAME_CANVAS_PREFAB = "Assets/Art/UI/Scene/GameCanvas.prefab";

    [MenuItem(MENU)]
    static void BuildFromMenu()
    {
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        var log = new StringBuilder();
        if (!Directory.Exists(RELAYOUT_DIR))
        {
            Directory.CreateDirectory(RELAYOUT_DIR);
        }

        var palette = AssetDatabase.LoadAssetAtPath<UIPalette>(PALETTE_ASSET);
        if (!palette)
        {
            throw new System.InvalidOperationException($"UIPalette 에셋이 없습니다: {PALETTE_ASSET}");
        }

        FindTemplates(out var buttonTemplate, out var labelTemplate);
        var marketPrefab = BuildMarketWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var lensPrefab = BuildLensWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var toolbarPrefab = UpdateQuickToolbarPrefab(log, palette, buttonTemplate, labelTemplate);
        UpdateGameCanvas(log, marketPrefab, lensPrefab, toolbarPrefab);

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static GameObject BuildMarketWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var layoutRoot = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        var rootGo = RelayoutWindowKit.CreateWindow("MarketWindow", null, string.Empty, 0f, 0f, 0f, 0f, false,
            out var floatingWin, out var titleBar, out var titleText, out var closeBtn);
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            // 13×9 보드(56px 셀 = 728×504) + 시세 띠 + 하단 안내에 맞춘 고정 크기(764×648).
            SetTopLeftRect(rootRect, 1136f, 136f, 764f, 648f);

            titleText.text = "시장";

            // 기존 GameplayLayout의 Market Inventory 복제
            var oldMarketInv = FindDeep(layoutRoot.transform, "Market Inventory");
            GameObject clonedMarketInv = Object.Instantiate(oldMarketInv.gameObject, rootRect);
            clonedMarketInv.name = "MarketInventoryBody";
            clonedMarketInv.SetActive(true);
            var bodyRt = (RectTransform)clonedMarketInv.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(8f, 42f); // 하단 줄(8~34) 위 간격 8px
            bodyRt.offsetMax = new Vector2(-8f, -32f);

            var invView = clonedMarketInv.GetComponent<PuzzleInventoryView>();
            var invSo = new SerializedObject(invView);

            HideNamedChild(clonedMarketInv.transform, "BoardTitle");
            HideNamedChild(clonedMarketInv.transform, "BoardValue");
            HideNamedChild(clonedMarketInv.transform, "MoveGuide");
            HideNamedChild(clonedMarketInv.transform, "BoardInfo");

            var infoPanel = FindDeep(clonedMarketInv.transform, "MarketInfoPanel") as RectTransform;
            if (infoPanel)
            {
                infoPanel.anchorMin = new Vector2(0f, 1f);
                infoPanel.anchorMax = new Vector2(1f, 1f);
                infoPanel.pivot = new Vector2(0.5f, 1f);
                infoPanel.anchoredPosition = Vector2.zero;
                infoPanel.sizeDelta = new Vector2(0f, 68f);
            }

            var tradePrefs = invSo.FindProperty("boardTradePreferences").objectReferenceValue as CityTradePreferencesView;
            if (tradePrefs)
            {
                var tpRt = (RectTransform)tradePrefs.transform;
                tpRt.SetParent(bodyRt, false);
                tpRt.anchorMin = new Vector2(0f, 1f);
                tpRt.anchorMax = new Vector2(1f, 1f);
                tpRt.pivot = new Vector2(0.5f, 1f);
                tpRt.anchoredPosition = new Vector2(0f, -2f);
                tpRt.sizeDelta = new Vector2(-8f, 64f);
                // 좌/우 2열이 740px 폭 안에 들어오도록 행 위치를 조정한다.
                FitTradePrefRow(tpRt, "RegionPrice", 4f, 0f, 376f);
                FitTradePrefRow(tpRt, "ExpensiveRow", 4f, -24f, 376f);
                FitTradePrefRow(tpRt, "WasteRow", 4f, -48f, 376f);
                FitTradePrefRow(tpRt, "CommonRow", 388f, 0f, 348f);
                FitTradePrefRow(tpRt, "RareRow", 388f, -24f, 348f);
            }

            var divider = FindDeep(clonedMarketInv.transform, "HeaderDivider") as RectTransform;
            if (divider)
            {
                divider.anchorMin = new Vector2(0f, 1f);
                divider.anchorMax = new Vector2(1f, 1f);
                divider.pivot = new Vector2(0.5f, 1f);
                divider.anchoredPosition = new Vector2(0f, -66f);
                divider.sizeDelta = new Vector2(-8f, 2f);
            }

            // 13×9 보드 뷰포트 (740×504 → 56px 셀)
            var boardImg = invSo.FindProperty("board").objectReferenceValue as Image;
            var boardRt = boardImg.rectTransform;
            boardImg.enabled = false;

            var viewport = NewRect("BoardViewport", bodyRt);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 2f);
            viewport.offsetMax = new Vector2(-4f, -68f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("BoardContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(740f, 504f);

            // 순방향 이동으로 boardCells → placementSlots → outlineSlots → synergyArrows 순서를 보존한다.
            while (boardRt.childCount > 0)
            {
                boardRt.GetChild(0).SetParent(content, false);
            }

            // 가격표 라벨을 placementSlots[0] 및 ghostSlots[0] 템플릿에 부착 (EnsureCapacity가 전체 복제)
            AttachPriceLabelToSlots(invSo.FindProperty("placementSlots"), _labelTemplate, _palette);
            AttachPriceLabelToSlots(invSo.FindProperty("ghostSlots"), _labelTemplate, _palette);

            invSo.FindProperty("dynamicCellSizing").boolValue = true;
            invSo.FindProperty("boardViewport").objectReferenceValue = viewport;
            invSo.FindProperty("boardContent").objectReferenceValue = content;
            invSo.FindProperty("minDynamicCellSize").floatValue = 40f;
            invSo.FindProperty("maxDynamicCellSize").floatValue = 56f;

            var candProp = invSo.FindProperty("candidateMarkers");
            if (candProp != null && candProp.isArray)
            {
                for (int c = 0; c < candProp.arraySize; c++)
                {
                    var markerRt = candProp.GetArrayElementAtIndex(c).objectReferenceValue as RectTransform;
                    if (!markerRt) continue;
                    RelayoutWindowKit.StyleCandidateMarker(markerRt,
                        new Color(_palette.GoldAccent.r, _palette.GoldAccent.g, _palette.GoldAccent.b, 0.22f), _palette.GoldAccent);
                }
            }
            invSo.ApplyModifiedPropertiesWithoutUndo();

            // 하단 1줄 조작 안내 바 (26px)
            var bottomBar = NewRect("BottomBar", rootRect);
            bottomBar.anchorMin = new Vector2(0f, 0f);
            bottomBar.anchorMax = new Vector2(1f, 0f);
            bottomBar.pivot = new Vector2(0.5f, 0f);
            // 창 여백 8px(기준값)을 좌·우·아래에 둔다.
            bottomBar.anchoredPosition = new Vector2(0f, 8f);
            bottomBar.sizeDelta = new Vector2(-16f, 26f);
            var bbImg = bottomBar.gameObject.AddComponent<Image>();
            bbImg.color = _palette.Panel2;
            bbImg.raycastTarget = false;
            BindPalette(bottomBar.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var hintLbl = CreateLabel(_labelTemplate, bottomBar, "HintText", 13f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            hintLbl.text = Presentation.MarketWindowHint;
            hintLbl.rectTransform.anchorMin = Vector2.zero;
            hintLbl.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            hintLbl.rectTransform.offsetMin = new Vector2(10f, 0f);
            hintLbl.rectTransform.offsetMax = Vector2.zero;

            var restockLbl = CreateLabel(_labelTemplate, bottomBar, "RestockText", 13f,
                TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.GoldAccent);
            restockLbl.fontStyle = FontStyles.Bold;
            restockLbl.text = Presentation.MarketWindowRestock;
            restockLbl.rectTransform.anchorMin = new Vector2(0.75f, 0f);
            restockLbl.rectTransform.anchorMax = Vector2.one;
            restockLbl.rectTransform.offsetMin = Vector2.zero;
            restockLbl.rectTransform.offsetMax = new Vector2(-10f, 0f);

            var fwSo = new SerializedObject(floatingWin);
            fwSo.FindProperty("windowId").stringValue = "market";
            fwSo.FindProperty("titleBar").objectReferenceValue = titleBar;
            fwSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            fwSo.FindProperty("defaultPosition").vector2Value = new Vector2(1136f, 136f);
            fwSo.FindProperty("defaultSize").vector2Value = new Vector2(764f, 648f);
            fwSo.FindProperty("startOpen").boolValue = false;
            fwSo.ApplyModifiedPropertiesWithoutUndo();

            var marketView = rootGo.AddComponent<MarketWindowView>();
            var mvSo = new SerializedObject(marketView);
            mvSo.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            mvSo.FindProperty("titleText").objectReferenceValue = titleText;
            mvSo.FindProperty("tradePreferencesView").objectReferenceValue = tradePrefs;
            mvSo.FindProperty("inventoryView").objectReferenceValue = invView;
            mvSo.FindProperty("hintText").objectReferenceValue = hintLbl;
            mvSo.FindProperty("restockText").objectReferenceValue = restockLbl;
            mvSo.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, MARKET_WINDOW_PREFAB);
            _log.AppendLine($"MarketWindow 프리팹 → {MARKET_WINDOW_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(layoutRoot);
        }
    }

    static void FitTradePrefRow(RectTransform _parent, string _name, float _x, float _y, float _w)
    {
        var row = _parent.Find(_name) as RectTransform;
        if (!row) return;
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(_x, _y);
        row.sizeDelta = new Vector2(_w, 22f);
    }

    static void AttachPriceLabelToSlots(SerializedProperty _slotsProp, TextMeshProUGUI _labelTemplate, UIPalette _palette)
    {
        if (_slotsProp == null || !_slotsProp.isArray)
        {
            return;
        }
        for (int i = 0; i < _slotsProp.arraySize; i++)
        {
            var slot = _slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as PuzzleSlotVisual;
            if (!slot) continue;
            var slotSo = new SerializedObject(slot);
            var chipProp = slotSo.FindProperty("deltaChip");
            var deltaProp = slotSo.FindProperty("deltaLabel");
            var existingChip = chipProp.objectReferenceValue as GameObject;
            var existing = deltaProp.objectReferenceValue as TextMeshProUGUI;
            if (!existingChip)
            {
                var chipGo = new GameObject("PriceChip", typeof(RectTransform), typeof(Image));
                chipGo.layer = 5;
                var chipRt = (RectTransform)chipGo.transform;
                chipRt.SetParent(slot.transform, false);
                chipRt.anchorMin = new Vector2(1f, 0f);
                chipRt.anchorMax = new Vector2(1f, 0f);
                chipRt.pivot = new Vector2(1f, 0f);
                chipRt.anchoredPosition = new Vector2(-4f, 4f);
                // 128px 슬롯 기준 92×48 → 56px 셀에서 약 40×21px 칩, 32pt → 14pt. 높이는 글자 크기 × 1.5.
                chipRt.sizeDelta = new Vector2(92f, 48f);

                var chipImg = chipGo.GetComponent<Image>();
                chipImg.sprite = null;
                chipImg.type = Image.Type.Simple;
                chipImg.color = _palette.Panel;
                chipImg.raycastTarget = false;
                BindPalette(chipGo, _palette, UIPalette.ColorRole.Panel);

                var chipOut = chipGo.AddComponent<Outline>();
                chipOut.effectColor = _palette.Border;
                chipOut.effectDistance = new Vector2(2f, -2f);

                existing = CreateLabel(_labelTemplate, chipRt, "PriceLabel", 32f,
                    TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.GoldAccent);
                existing.fontStyle = FontStyles.Bold;
                existing.enableAutoSizing = false;
                existing.rectTransform.anchorMin = Vector2.zero;
                existing.rectTransform.anchorMax = Vector2.one;
                existing.rectTransform.offsetMin = new Vector2(4f, 0f);
                existing.rectTransform.offsetMax = new Vector2(-4f, 0f);
                chipGo.SetActive(false);

                chipProp.objectReferenceValue = chipGo;
                deltaProp.objectReferenceValue = existing;
                slotSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static GameObject BuildLensWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = RelayoutWindowKit.CreateWindow("LensWindow", null, string.Empty, 0f, 0f, 0f, 0f, false,
            out var floatingWin, out var titleBar, out var titleText, out var closeBtn);
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 26f, 134f, 510f, 366f);

            titleText.text = Presentation.LensWindowTitle;

            var bodyRt = NewRect("LensBody", rootRect);
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(10f, 8f);
            bodyRt.offsetMax = new Vector2(-10f, -34f);

            var itemTitle = CreateLabel(_labelTemplate, bodyRt, "ItemTitle", 15f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TitleText);
            itemTitle.fontStyle = FontStyles.Bold;
            itemTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            itemTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            itemTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            itemTitle.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            itemTitle.rectTransform.sizeDelta = new Vector2(0f, 22f);

            var tradeLbl = CreateLabel(_labelTemplate, bodyRt, "TradeLabel", 14f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            tradeLbl.richText = true;
            tradeLbl.rectTransform.anchorMin = new Vector2(0f, 1f);
            tradeLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            tradeLbl.rectTransform.pivot = new Vector2(0.5f, 1f);
            tradeLbl.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            tradeLbl.rectTransform.sizeDelta = new Vector2(0f, 22f);

            var placeLbl = CreateLabel(_labelTemplate, bodyRt, "PlacementLabel", 13f,
                TextAlignmentOptions.TopLeft, _palette, UIPalette.ColorRole.TextSub);
            placeLbl.richText = true;
            placeLbl.rectTransform.anchorMin = new Vector2(0f, 1f);
            placeLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            placeLbl.rectTransform.pivot = new Vector2(0.5f, 1f);
            placeLbl.rectTransform.anchoredPosition = new Vector2(0f, -44f);
            placeLbl.rectTransform.sizeDelta = new Vector2(0f, 38f);

            // 3개 탭 (현재 / 방문 도시 / 소문 도시)
            string[] tabNames = { "현재", "방문 도시", "소문 도시" };
            var tabBtns = new Button[3];
            var tabLbls = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                var tb = CreateSmallButton(_buttonTemplate, _labelTemplate, bodyRt, $"Tab_{i}",
                    i * 164f, 84f, 158f, 26f, tabNames[i], 13f, _palette);
                tabBtns[i] = tb;
                tabLbls[i] = tb.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            // 스크롤 가능한 행선지 비교 테이블 영역
            var scrollGo = new GameObject("DestinationScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(bodyRt, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 22f);
            scrollRt.offsetMax = new Vector2(0f, -114f);

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;

            var vp = NewRect("Viewport", scrollRt);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;
            vp.gameObject.AddComponent<RectMask2D>();
            var vpImg = vp.gameObject.AddComponent<Image>();
            vpImg.color = _palette.Panel2;
            BindPalette(vp.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var content = NewRect("Content", vp);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 200f);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var destLbl = CreateLabel(_labelTemplate, content, "DestinationLabel", 13f,
                TextAlignmentOptions.TopLeft, _palette, UIPalette.ColorRole.Text);
            TextLinkHandler.Attach(destLbl);
            destLbl.textWrappingMode = TextWrappingModes.Normal;

            sr.viewport = vp;
            sr.content = content;

            var footLbl = CreateLabel(_labelTemplate, bodyRt, "FootnoteLabel", 12f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            footLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
            footLbl.rectTransform.anchorMax = new Vector2(1f, 0f);
            footLbl.rectTransform.pivot = new Vector2(0.5f, 0f);
            footLbl.rectTransform.anchoredPosition = Vector2.zero;
            footLbl.rectTransform.sizeDelta = new Vector2(0f, 20f);

            var routeLens = rootGo.AddComponent<PuzzleRouteLensView>();
            var rlSo = new SerializedObject(routeLens);
            rlSo.FindProperty("root").objectReferenceValue = bodyRt.gameObject;
            rlSo.FindProperty("titleLabel").objectReferenceValue = itemTitle;
            rlSo.FindProperty("tradeLabel").objectReferenceValue = tradeLbl;
            rlSo.FindProperty("placementLabel").objectReferenceValue = placeLbl;
            rlSo.FindProperty("destinationLabel").objectReferenceValue = destLbl;
            rlSo.FindProperty("footnoteLabel").objectReferenceValue = footLbl;
            var tbProp = rlSo.FindProperty("tabButtons");
            tbProp.arraySize = 3;
            var tlProp = rlSo.FindProperty("tabLabels");
            tlProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                tbProp.GetArrayElementAtIndex(i).objectReferenceValue = tabBtns[i];
                tlProp.GetArrayElementAtIndex(i).objectReferenceValue = tabLbls[i];
            }
            rlSo.ApplyModifiedPropertiesWithoutUndo();

            var fwSo = new SerializedObject(floatingWin);
            fwSo.FindProperty("windowId").stringValue = "lens";
            fwSo.FindProperty("titleBar").objectReferenceValue = titleBar;
            fwSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            fwSo.FindProperty("defaultPosition").vector2Value = new Vector2(26f, 134f);
            fwSo.FindProperty("defaultSize").vector2Value = new Vector2(510f, 366f);
            fwSo.FindProperty("startOpen").boolValue = false;
            fwSo.ApplyModifiedPropertiesWithoutUndo();

            var lensView = rootGo.AddComponent<LensWindowView>();
            var lvSo = new SerializedObject(lensView);
            lvSo.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            lvSo.FindProperty("titleText").objectReferenceValue = titleText;
            lvSo.FindProperty("routeLensView").objectReferenceValue = routeLens;
            lvSo.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, LENS_WINDOW_PREFAB);
            _log.AppendLine($"LensWindow 프리팹 → {LENS_WINDOW_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static GameObject UpdateQuickToolbarPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var root = PrefabUtility.LoadPrefabContents(QUICK_TOOLBAR_PREFAB);
        try
        {
            var rootRect = (RectTransform)root.transform;
            var qtView = root.GetComponent<QuickToolbarView>();
            var so = new SerializedObject(qtView);

            // 노선도·배경만 보기 버튼은 없앴다(단축키 M·H만 유지, R44). 수첩 · 렌즈 · 창 배치 초기화 · 단축키 안내 순서, 간격 4.
            foreach (var removed in new[] { "MapButton", "BackgroundOnlyButton" })
            {
                var old = root.transform.Find(removed);
                if (old) Object.DestroyImmediate(old.gameObject);
            }
            so.FindProperty("mapButton").objectReferenceValue = null;
            so.FindProperty("mapLabel").objectReferenceValue = null;
            so.FindProperty("backgroundOnlyButton").objectReferenceValue = null;
            so.FindProperty("backgroundOnlyLabel").objectReferenceValue = null;
            RepositionToolbarChild(root.transform, "NotebookButton", 8f, 80f);
            RepositionToolbarChild(root.transform, "ResetLayoutButton", 174f, 104f);
            RepositionToolbarChild(root.transform, "ShortcutHint", 286f, 216f); // 오른쪽 여백 8px

            var oldLens = root.transform.Find("LensButton");
            if (oldLens) Object.DestroyImmediate(oldLens.gameObject);

            var lensBtn = CreateSmallButton(_buttonTemplate, _labelTemplate, rootRect, "LensButton",
                92f, 5f, 78f, 28f, Presentation.QuickToolbarLens, 14f, _palette);
            var lensLbl = lensBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            // 다른 도구줄 버튼(수첩·창 배치 초기화)과 같은 굵은 14pt로 맞춘다.
            lensLbl.fontStyle = FontStyles.Bold;

            so.FindProperty("lensButton").objectReferenceValue = lensBtn;
            so.FindProperty("lensLabel").objectReferenceValue = lensLbl;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, QUICK_TOOLBAR_PREFAB);
            _log.AppendLine($"QuickToolbar 업데이트 (렌즈 버튼 추가) → {QUICK_TOOLBAR_PREFAB}");
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void RepositionToolbarChild(Transform _root, string _name, float _left, float _width)
    {
        var child = _root.Find(_name) as RectTransform;
        if (!child) return;
        SetTopLeftRect(child, _left, 5f, _width, 28f);
    }

    static void UpdateGameCanvas(
        StringBuilder _log,
        GameObject _marketPrefab,
        GameObject _lensPrefab,
        GameObject _toolbarPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);
        try
        {
            var fixedCanvas = root.transform.Find("FixedFrameCanvas");
            GameObject toolbarInst = null;
            if (fixedCanvas)
            {
                RemoveChildrenNamed(fixedCanvas, "QuickToolbar");
                toolbarInst = (GameObject)PrefabUtility.InstantiatePrefab(_toolbarPrefab, fixedCanvas);
            }

            var floatingCanvas = root.transform.Find("FloatingWindowCanvas");
            GameObject marketInst = null;
            GameObject lensInst = null;
            if (floatingCanvas)
            {
                RemoveChildrenNamed(floatingCanvas, "MarketWindow");
                RemoveChildrenNamed(floatingCanvas, "LensWindow");
                marketInst = (GameObject)PrefabUtility.InstantiatePrefab(_marketPrefab, floatingCanvas);
                lensInst = (GameObject)PrefabUtility.InstantiatePrefab(_lensPrefab, floatingCanvas);
            }

            var marketView = marketInst ? marketInst.GetComponent<MarketWindowView>() : null;
            var lensView = lensInst ? lensInst.GetComponent<LensWindowView>() : null;

            if (toolbarInst)
            {
                var qtView = toolbarInst.GetComponent<QuickToolbarView>();
                if (qtView)
                {
                    var qtSo = new SerializedObject(qtView);
                    qtSo.FindProperty("notebookWindowView").objectReferenceValue = root.GetComponentInChildren<NotebookWindowView>(true);
                    qtSo.FindProperty("lensWindowView").objectReferenceValue = lensView;
                    qtSo.FindProperty("windowManager").objectReferenceValue = root.GetComponentInChildren<FloatingWindowManager>(true);
                    qtSo.FindProperty("inventoryPresenter").objectReferenceValue = root.GetComponent<PuzzleInventoryPresenter>();
                    qtSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var relayoutController = root.GetComponent<RelayoutRootController>();
            if (relayoutController)
            {
                var rcSo = new SerializedObject(relayoutController);
                rcSo.FindProperty("marketWindow").objectReferenceValue = marketView;
                rcSo.FindProperty("lensWindow").objectReferenceValue = lensView;
                rcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var invPresenter = root.GetComponent<PuzzleInventoryPresenter>();
            if (invPresenter)
            {
                var pSo = new SerializedObject(invPresenter);
                pSo.FindProperty("relayoutMarketView").objectReferenceValue = marketView ? marketView.InventoryView : null;
                pSo.FindProperty("marketWindowView").objectReferenceValue = marketView;
                pSo.FindProperty("lensWindowView").objectReferenceValue = lensView;
                pSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB);
            _log.AppendLine($"GameCanvas 업데이트 (MarketWindow, LensWindow, QuickToolbar 연결) → {GAME_CANVAS_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void HideNamedChild(Transform _root, string _name)
    {
        var t = FindDeep(_root, _name);
        if (t) t.gameObject.SetActive(false);
    }

    static void RemoveChildrenNamed(Transform _parent, string _name)
    {
        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            var c = _parent.GetChild(i);
            if (c.name == _name)
            {
                Object.DestroyImmediate(c.gameObject);
            }
        }
    }

    static void FindTemplates(out Button _buttonTemplate, out TextMeshProUGUI _labelTemplate)
    {
        _buttonTemplate = null;
        _labelTemplate = null;
        var gameplayRoot = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        try
        {
            _buttonTemplate = gameplayRoot.GetComponentInChildren<Button>(true);
            _labelTemplate = gameplayRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(gameplayRoot);
        }
    }

    static Button CreateSmallButton(
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate,
        RectTransform _parent,
        string _name,
        float _left,
        float _top,
        float _width,
        float _height,
        string _text,
        float _fontSize,
        UIPalette _palette)
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
        SetTopLeftRect((RectTransform)go.transform, _left, _top, _width, _height);
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        if (btn.targetGraphic is Image img)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = _palette.Panel2;
            BindPalette(go, _palette, UIPalette.ColorRole.Panel2);
        }
        var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!label)
        {
            label = CreateLabel(_labelTemplate, (RectTransform)go.transform, "Label", _fontSize,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        }
        // 템플릿 글자 상자는 템플릿 버튼 크기 그대로라 새 버튼보다 클 수 있다. 버튼 칸에 맞춘다.
        RelayoutWindowKit.Stretch(label.rectTransform);
        label.enableAutoSizing = false;
        label.fontSize = _fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.text = _text;
        label.color = _palette.Text;
        label.raycastTarget = false;
        return btn;
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
        }
        else
        {
            go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_parent, false);
        }
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = _fontSize;
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = _alignment;
        tmp.color = _role switch
        {
            UIPalette.ColorRole.TitleText => _palette.TitleText,
            UIPalette.ColorRole.TextSub => _palette.TextSub,
            UIPalette.ColorRole.GoldAccent => _palette.GoldAccent,
            _ => _palette.Text
        };
        tmp.raycastTarget = false;
        BindPalette(go, _palette, _role);
        return tmp;
    }

    static void BindPalette(GameObject _go, UIPalette _palette, UIPalette.ColorRole _role)
    {
        var binder = _go.GetComponent<UIPaletteBinder>() ?? _go.AddComponent<UIPaletteBinder>();
        var so = new SerializedObject(binder);
        so.FindProperty("palette").objectReferenceValue = _palette;
        so.FindProperty("role").enumValueIndex = (int)_role;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform NewRect(string _name, Transform _parent)
    {
        var go = new GameObject(_name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_parent, false);
        return rt;
    }

    static void SetTopLeftRect(RectTransform _rt, float _left, float _top, float _width, float _height)
    {
        _rt.anchorMin = new Vector2(0f, 1f);
        _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot = new Vector2(0f, 1f);
        _rt.anchoredPosition = new Vector2(_left, -_top);
        _rt.sizeDelta = new Vector2(_width, _height);
    }

    static Transform FindDeep(Transform _root, string _name)
    {
        if (!_root) return null;
        if (_root.name == _name) return _root;
        for (int i = 0; i < _root.childCount; i++)
        {
            var found = FindDeep(_root.GetChild(i), _name);
            if (found) return found;
        }
        return null;
    }
}
