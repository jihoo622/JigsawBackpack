using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// S3: 중앙 세로 선로(RailStrip) 및 기차 화차 부동 창(TrainWindow, 동적 셀 40~72px + ScrollRect)을
// 프리팹에 멱등적으로 구성하고 PuzzleInventoryPresenter에 연결한다.
public static class RelayoutS3Builder
{
    const string MENU = "Tools/MyJG/UI/Relayout/S3 Build Rail And Train";
    const string PALETTE_PATH = "Assets/Resources/UI/UIPalette.asset";
    const string RELAYOUT_DIR = "Assets/Art/UI/Relayout";
    const string RAIL_STRIP_PREFAB = "Assets/Art/UI/Relayout/RailStrip.prefab";
    const string TRAIN_WINDOW_PREFAB = "Assets/Art/UI/Relayout/TrainWindow.prefab";
    const string PAUSE_PREFAB = "Assets/Art/UI/Scene/PauseCanvas.prefab";
    const string GAMEPLAY_LAYOUT_PREFAB = "Assets/Art/UI/GameplayLayout.prefab";
    const string GAME_CANVAS_PREFAB = "Assets/Art/UI/Scene/GameCanvas.prefab";

    const int POOL_SIZE = 14;

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

        var palette = AssetDatabase.LoadAssetAtPath<UIPalette>(PALETTE_PATH);
        if (!palette)
        {
            palette = ScriptableObject.CreateInstance<UIPalette>();
            AssetDatabase.CreateAsset(palette, PALETTE_PATH);
        }
        else if (palette.MiniGridCargoA.a <= 0.01f)
        {
            var pSo = new SerializedObject(palette);
            pSo.FindProperty("miniGridEmpty").colorValue = new Color(0.2784f, 0.3333f, 0.4118f, 1f);
            pSo.FindProperty("miniGridCargoA").colorValue = new Color(0.9804f, 0.8000f, 0.0824f, 1f);
            pSo.FindProperty("miniGridCargoB").colorValue = new Color(0.7922f, 0.5412f, 0.0157f, 1f);
            pSo.FindProperty("miniGridBlocked").colorValue = new Color(0.0588f, 0.0902f, 0.1412f, 1f);
            pSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(palette);
        }

        Button buttonTemplate = null;
        TextMeshProUGUI labelTemplate = null;
        var pauseRoot = PrefabUtility.LoadPrefabContents(PAUSE_PREFAB);
        try
        {
            var menu = pauseRoot.GetComponentInChildren<PauseMenu>(true);
            if (menu)
            {
                var so = new SerializedObject(menu);
                buttonTemplate = so.FindProperty("restartButton").objectReferenceValue as Button;
                labelTemplate = so.FindProperty("panelTitle").objectReferenceValue as TextMeshProUGUI;
            }

            var railStripPrefab = BuildRailStripPrefab(log, palette, buttonTemplate, labelTemplate);
            var trainWindowPrefab = BuildTrainWindowPrefab(log, palette, buttonTemplate, labelTemplate);
            UpdateGameCanvas(log, railStripPrefab, trainWindowPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pauseRoot);
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static GameObject BuildRailStripPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = new GameObject("RailStrip", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 549f, 82f, 91f, 998f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.118f, 0.141f, 0.188f, 0.96f);
            bg.raycastTarget = true;

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // 세로 레일 2줄 배경
            AddStaticRect(rootRect, "RailLineLeft", 18f, 0f, 3f, 998f, new Color(0.28f, 0.34f, 0.43f, 0.6f));
            AddStaticRect(rootRect, "RailLineRight", 70f, 0f, 3f, 998f, new Color(0.28f, 0.34f, 0.43f, 0.6f));

            // 기관차 헤더 (맨 위 80px, 평면 사각형)
            var loco = AddStaticRect(rootRect, "Locomotive", 6f, 6f, 79f, 74f, new Color(0.471f, 0.208f, 0.059f, 1f));
            var locoOutline = loco.gameObject.AddComponent<Outline>();
            locoOutline.effectColor = _palette.Border;
            locoOutline.effectDistance = new Vector2(1f, -1f);
            var locoLbl = CreateLabel(_labelTemplate, loco, "Label", 14f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.GoldAccent);
            locoLbl.fontStyle = FontStyles.Bold;
            locoLbl.text = "기관차";
            Stretch(locoLbl.rectTransform);

            // 위로 스크롤 버튼 ▲
            var upBtn = CreateSmallButton(_buttonTemplate, _labelTemplate, rootRect, "ScrollUpButton",
                6f, 84f, 79f, 22f, "▲", 12f, _palette);

            // 스크롤 뷰포트 (y: 110 ~ 968, height: 858)
            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform));
            scrollGo.layer = 5;
            scrollGo.transform.SetParent(rootRect, false);
            var scrollRectRt = (RectTransform)scrollGo.transform;
            SetTopLeftRect(scrollRectRt, 0f, 110f, 91f, 858f);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 36f;

            var viewport = NewRect("Viewport", scrollRectRt);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            vpImg.raycastTarget = true;

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 858f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            // 가상화 풀 슬롯 (POOL_SIZE개 사전 배치)
            var pool = new RailWagonCarView[POOL_SIZE];
            for (int i = 0; i < POOL_SIZE; i++)
            {
                pool[i] = CreateRailWagonCarSlot(content, i, _palette, _buttonTemplate, _labelTemplate);
            }

            // 상/하단 페이드
            var topFadeRect = AddStaticRect(viewport, "TopFade", 0f, 0f, 91f, 14f, new Color(0.06f, 0.09f, 0.14f, 0.7f));
            var bottomFadeRect = NewRect("BottomFade", viewport);
            bottomFadeRect.anchorMin = new Vector2(0f, 0f);
            bottomFadeRect.anchorMax = new Vector2(1f, 0f);
            bottomFadeRect.pivot = new Vector2(0.5f, 0f);
            bottomFadeRect.anchoredPosition = Vector2.zero;
            bottomFadeRect.sizeDelta = new Vector2(0f, 14f);
            var bfImg = bottomFadeRect.gameObject.AddComponent<Image>();
            bfImg.color = new Color(0.06f, 0.09f, 0.14f, 0.7f);
            bfImg.raycastTarget = false;

            // 아래로 스크롤 버튼 ▼
            var downBtn = CreateSmallButton(_buttonTemplate, _labelTemplate, rootRect, "ScrollDownButton",
                6f, 972f, 79f, 22f, "▼", 12f, _palette);

            var stripView = rootGo.AddComponent<RailStripView>();
            var so = new SerializedObject(stripView);
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("viewport").objectReferenceValue = viewport;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("scrollUpButton").objectReferenceValue = upBtn;
            so.FindProperty("scrollDownButton").objectReferenceValue = downBtn;
            so.FindProperty("topFade").objectReferenceValue = topFadeRect.GetComponent<Image>();
            so.FindProperty("bottomFade").objectReferenceValue = bfImg;
            so.FindProperty("carHeight").floatValue = 102f;
            var poolProp = so.FindProperty("carPool");
            poolProp.arraySize = pool.Length;
            for (int i = 0; i < pool.Length; i++)
            {
                poolProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, RAIL_STRIP_PREFAB);
            _log.AppendLine($"RailStrip 프리팹 (풀 {POOL_SIZE}개) → {RAIL_STRIP_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static RailWagonCarView CreateRailWagonCarSlot(
        RectTransform _parent,
        int _poolIndex,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var carRect = NewRect($"RailWagonCar_{_poolIndex}", _parent);
        carRect.anchorMin = new Vector2(0.5f, 1f);
        carRect.anchorMax = new Vector2(0.5f, 1f);
        carRect.pivot = new Vector2(0.5f, 1f);
        carRect.anchoredPosition = new Vector2(0f, -(8f + _poolIndex * 104f));
        carRect.sizeDelta = new Vector2(83f, 102f);

        var bg = carRect.gameObject.AddComponent<Image>();
        bg.color = _palette.Panel;
        var btn = carRect.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;

        var hlRect = NewRect("HighlightBorder", carRect);
        Stretch(hlRect);
        var hlImg = hlRect.gameObject.AddComponent<Image>();
        hlImg.color = new Color(_palette.GoldAccent.r, _palette.GoldAccent.g, _palette.GoldAccent.b, 0.14f);
        hlImg.raycastTarget = false;
        var hlOutline = hlRect.gameObject.AddComponent<Outline>();
        hlOutline.effectColor = _palette.GoldAccent;
        hlOutline.effectDistance = new Vector2(2f, -2f);
        hlImg.enabled = false;

        var lbl = CreateLabel(_labelTemplate, carRect, "Label", 12f,
            TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        lbl.enableWordWrapping = false;
        SetTopLeftRect(lbl.rectTransform, 2f, 3f, 79f, 32f);

        // 7×6 미니 격자 (8×8px, 간격 1.5px, y=0이 아래쪽)
        var gridContainer = NewRect("MiniGrid", carRect);
        SetTopLeftRect(gridContainer, 9f, 38f, 65f, 57f);
        var cells = new Image[RailWagonCarView.MINI_CELL_COUNT];
        const float cellSize = 8f;
        const float stride = 9.5f;
        for (int row = 0; row < RailWagonCarView.MINI_ROWS; row++)
        {
            for (int col = 0; col < RailWagonCarView.MINI_COLS; col++)
            {
                int idx = row * RailWagonCarView.MINI_COLS + col;
                var cRect = NewRect($"Cell_{col}_{row}", gridContainer);
                cRect.anchorMin = Vector2.zero;
                cRect.anchorMax = Vector2.zero;
                cRect.pivot = Vector2.zero;
                cRect.anchoredPosition = new Vector2(col * stride, row * stride);
                cRect.sizeDelta = new Vector2(cellSize, cellSize);
                var img = cRect.gameObject.AddComponent<Image>();
                img.color = _palette.MiniGridEmpty;
                img.raycastTarget = false;
                cells[idx] = img;
            }
        }

        var view = carRect.gameObject.AddComponent<RailWagonCarView>();
        var so = new SerializedObject(view);
        so.FindProperty("rectTransform").objectReferenceValue = carRect;
        so.FindProperty("button").objectReferenceValue = btn;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("highlightBorder").objectReferenceValue = hlImg;
        so.FindProperty("label").objectReferenceValue = lbl;
        so.FindProperty("palette").objectReferenceValue = _palette;
        var cellsProp = so.FindProperty("miniGridCells");
        cellsProp.arraySize = cells.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            cellsProp.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        carRect.gameObject.SetActive(false);
        return view;
    }

    static GameObject BuildTrainWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var layoutRoot = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        var rootGo = new GameObject("TrainWindow", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 26f, 516f, 510f, 544f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = _palette.Panel;
            BindPalette(rootGo, _palette, UIPalette.ColorRole.Panel);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(2f, -2f);

            var floatingWin = rootGo.AddComponent<FloatingWindow>();

            // 제목줄 (32px)
            var titleBar = NewRect("TitleBar", rootRect);
            titleBar.anchorMin = new Vector2(0f, 1f);
            titleBar.anchorMax = new Vector2(1f, 1f);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.anchoredPosition = Vector2.zero;
            titleBar.sizeDelta = new Vector2(0f, 32f);
            var tbImg = titleBar.gameObject.AddComponent<Image>();
            tbImg.color = _palette.Panel2;
            BindPalette(titleBar.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var dragHandle = titleBar.gameObject.AddComponent<FloatingWindowDragHandle>();
            new SerializedObject(dragHandle).ApplyModifiedPropertiesWithoutUndo();

            var titleText = CreateLabel(_labelTemplate, titleBar, "TitleText", 16f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TitleText);
            titleText.fontStyle = FontStyles.Bold;
            titleText.text = "1번 · 고장난 칸 · 적재 0/12";
            titleText.rectTransform.anchorMin = Vector2.zero;
            titleText.rectTransform.anchorMax = Vector2.one;
            titleText.rectTransform.offsetMin = new Vector2(12f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-36f, 0f);

            var closeBtn = CreateSmallButton(_buttonTemplate, _labelTemplate, titleBar, "CloseButton",
                0f, 0f, 24f, 24f, "X", 14f, _palette);
            var cRt = (RectTransform)closeBtn.transform;
            cRt.anchorMin = new Vector2(1f, 0.5f);
            cRt.anchorMax = new Vector2(1f, 0.5f);
            cRt.pivot = new Vector2(1f, 0.5f);
            cRt.anchoredPosition = new Vector2(-6f, 0f);

            // 기존 GameplayLayout의 Train Inventory 복제
            var oldTrainInv = FindDeep(layoutRoot.transform, "Train Inventory");
            GameObject clonedTrainInv = Object.Instantiate(oldTrainInv.gameObject, rootRect);
            clonedTrainInv.name = "TrainInventoryBody";
            var bodyRt = (RectTransform)clonedTrainInv.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(8f, 50f);
            bodyRt.offsetMax = new Vector2(-8f, -36f);

            var invView = clonedTrainInv.GetComponent<PuzzleInventoryView>();
            var invSo = new SerializedObject(invView);

            // 기존 보드 제목·이동 안내·화차 아이콘·고정 구분선/배경은 창 규격에 맞게 정리한다.
            var oldTitle = FindDeep(clonedTrainInv.transform, "BoardTitle");
            if (oldTitle) oldTitle.gameObject.SetActive(false);
            var oldGuide = FindDeep(clonedTrainInv.transform, "MoveGuide");
            if (oldGuide) oldGuide.gameObject.SetActive(false);
            var oldWagonImg = FindDeep(clonedTrainInv.transform, "WagonImage");
            if (oldWagonImg) oldWagonImg.gameObject.SetActive(false);
            var oldNotice = FindDeep(clonedTrainInv.transform, "SynergySettlementNotice");
            if (oldNotice) oldNotice.gameObject.SetActive(false);
            var oldEffects = FindDeep(clonedTrainInv.transform, "WagonActiveEffects");
            if (oldEffects) oldEffects.gameObject.SetActive(false);
            var oldRefrigBtn = FindDeep(clonedTrainInv.transform, "Wagon Refrigeration Button");
            if (oldRefrigBtn) oldRefrigBtn.gameObject.SetActive(false);

            var oldBoardValue = invSo.FindProperty("boardValue").objectReferenceValue as TextMeshProUGUI;
            if (oldBoardValue)
            {
                var bvRt = oldBoardValue.rectTransform;
                bvRt.anchorMin = new Vector2(0f, 1f);
                bvRt.anchorMax = new Vector2(1f, 1f);
                bvRt.pivot = new Vector2(0.5f, 1f);
                bvRt.anchoredPosition = new Vector2(0f, -2f);
                bvRt.sizeDelta = new Vector2(-12f, 24f);
                oldBoardValue.fontSize = 15f;
            }
            // 다음 정산 요약 라벨 추가 (화물 가치 줄 아래 단독 줄로 겹침 완전 방지)
            var nextSettlementLbl = CreateLabel(_labelTemplate, bodyRt, "NextSettlementSummary", 14f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Positive);
            nextSettlementLbl.rectTransform.anchorMin = new Vector2(0f, 1f);
            nextSettlementLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            nextSettlementLbl.rectTransform.pivot = new Vector2(0.5f, 1f);
            nextSettlementLbl.rectTransform.anchoredPosition = new Vector2(0f, -26f);
            nextSettlementLbl.rectTransform.sizeDelta = new Vector2(-12f, 22f);

            var oldChips = FindDeep(clonedTrainInv.transform, "ProficiencyChips") as RectTransform;
            if (oldChips)
            {
                oldChips.anchorMin = new Vector2(0f, 1f);
                oldChips.anchorMax = new Vector2(1f, 1f);
                oldChips.pivot = new Vector2(0.5f, 1f);
                oldChips.anchoredPosition = new Vector2(0f, -50f);
                oldChips.sizeDelta = new Vector2(-8f, 36f);
            }
            var oldDivider = FindDeep(clonedTrainInv.transform, "HeaderDivider") as RectTransform;
            if (oldDivider)
            {
                oldDivider.anchorMin = new Vector2(0f, 1f);
                oldDivider.anchorMax = new Vector2(1f, 1f);
                oldDivider.pivot = new Vector2(0.5f, 1f);
                oldDivider.anchoredPosition = new Vector2(0f, -88f);
                oldDivider.sizeDelta = new Vector2(-8f, 2f);
            }

            // 동적 셀 크기 + ScrollRect 구성
            var boardImg = invSo.FindProperty("board").objectReferenceValue as Image;
            var boardRt = boardImg.rectTransform;
            boardImg.enabled = false;
            var scrollGo = new GameObject("BoardScrollRect", typeof(RectTransform));
            scrollGo.layer = 5;
            scrollGo.transform.SetParent(bodyRt, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(4f, 4f);
            scrollRt.offsetMax = new Vector2(-4f, -94f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            var viewport = NewRect("BoardViewport", scrollRt);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            vpImg.raycastTarget = true;

            // BoardContent는 좌상단 고정 앵커(0,1)를 사용해 sizeDelta가 부모 크기에 더해지지 않도록 한다.
            var content = NewRect("BoardContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(478f, 360f);

            scroll.viewport = viewport;
            scroll.content = content;

            // 보드 하위 슬롯들을 BoardContent로 이동 (boardCells, placementSlots, outlineSlots, synergyArrows, PlacementCandidates)
            for (int i = boardRt.childCount - 1; i >= 0; i--)
            {
                boardRt.GetChild(i).SetParent(content, false);
            }

            invSo.FindProperty("dynamicCellSizing").boolValue = true;
            invSo.FindProperty("boardScrollRect").objectReferenceValue = scroll;
            invSo.FindProperty("boardViewport").objectReferenceValue = viewport;
            invSo.FindProperty("boardContent").objectReferenceValue = content;
            invSo.FindProperty("minDynamicCellSize").floatValue = 40f;
            invSo.FindProperty("maxDynamicCellSize").floatValue = 72f;
            invSo.FindProperty("ownerWindow").objectReferenceValue = floatingWin;
            invSo.ApplyModifiedPropertiesWithoutUndo();

            // 하단 2줄 렌즈 요약 (R14)
            var lensFooter = NewRect("LensFooter", rootRect);
            lensFooter.anchorMin = new Vector2(0f, 0f);
            lensFooter.anchorMax = new Vector2(1f, 0f);
            lensFooter.pivot = new Vector2(0.5f, 0f);
            lensFooter.anchoredPosition = new Vector2(0f, 4f);
            lensFooter.sizeDelta = new Vector2(-12f, 44f);
            var lfBg = lensFooter.gameObject.AddComponent<Image>();
            lfBg.color = _palette.Panel2;
            lfBg.raycastTarget = false;
            BindPalette(lensFooter.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var tradeLine = CreateLabel(_labelTemplate, lensFooter, "LensTradeLine", 14f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            tradeLine.overflowMode = TextOverflowModes.Ellipsis;
            tradeLine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            tradeLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            tradeLine.rectTransform.offsetMin = new Vector2(10f, 0f);
            tradeLine.rectTransform.offsetMax = new Vector2(-10f, -2f);

            var spotLine = CreateLabel(_labelTemplate, lensFooter, "LensSpotLine", 14f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            spotLine.overflowMode = TextOverflowModes.Ellipsis;
            spotLine.rectTransform.anchorMin = new Vector2(0f, 0f);
            spotLine.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            spotLine.rectTransform.offsetMin = new Vector2(10f, 2f);
            spotLine.rectTransform.offsetMax = new Vector2(-10f, 0f);

            // 4변(8px) + 우하단 모서리(18×18) 리사이즈 핸들
            AddResizeHandle(rootRect, floatingWin, "ResizeTop", FloatingWindowMath.ResizeEdge.Top,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-16f, 8f));
            AddResizeHandle(rootRect, floatingWin, "ResizeBottom", FloatingWindowMath.ResizeEdge.Bottom,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(-16f, 8f));
            AddResizeHandle(rootRect, floatingWin, "ResizeLeft", FloatingWindowMath.ResizeEdge.Left,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, -16f));
            AddResizeHandle(rootRect, floatingWin, "ResizeRight", FloatingWindowMath.ResizeEdge.Right,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(8f, -16f));
            AddResizeHandle(rootRect, floatingWin, "ResizeBottomRight", FloatingWindowMath.ResizeEdge.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(18f, 18f));

            // FloatingWindow 직렬화
            var fwSo = new SerializedObject(floatingWin);
            fwSo.FindProperty("windowId").stringValue = "train";
            fwSo.FindProperty("titleBar").objectReferenceValue = titleBar;
            fwSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            fwSo.FindProperty("defaultPosition").vector2Value = new Vector2(26f, 516f);
            fwSo.FindProperty("defaultSize").vector2Value = new Vector2(510f, 544f);
            fwSo.FindProperty("minSize").vector2Value = new Vector2(320f, 340f);
            fwSo.FindProperty("maxSize").vector2Value = new Vector2(960f, 960f);
            fwSo.FindProperty("startOpen").boolValue = true;
            fwSo.ApplyModifiedPropertiesWithoutUndo();

            // TrainWindowView 직렬화
            var winView = rootGo.AddComponent<TrainWindowView>();
            var wvSo = new SerializedObject(winView);
            wvSo.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            wvSo.FindProperty("titleText").objectReferenceValue = titleText;
            wvSo.FindProperty("nextSettlementText").objectReferenceValue = nextSettlementLbl;
            wvSo.FindProperty("lensTradeLine").objectReferenceValue = tradeLine;
            wvSo.FindProperty("lensSpotLine").objectReferenceValue = spotLine;
            wvSo.FindProperty("inventoryView").objectReferenceValue = invView;
            wvSo.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, TRAIN_WINDOW_PREFAB);
            _log.AppendLine($"TrainWindow 프리팹 → {TRAIN_WINDOW_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(layoutRoot);
        }
    }

    static void UpdateGameCanvas(StringBuilder _log, GameObject _railStripPrefab, GameObject _trainWindowPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);
        try
        {
            var fixedCanvas = root.transform.Find("FixedFrameCanvas");
            GameObject railInst = null;
            if (fixedCanvas)
            {
                RemoveChildrenNamed(fixedCanvas, "RailStrip");
                railInst = (GameObject)PrefabUtility.InstantiatePrefab(_railStripPrefab, fixedCanvas);
            }

            var floatingCanvas = root.transform.Find("FloatingWindowCanvas");
            GameObject trainWinInst = null;
            if (floatingCanvas)
            {
                RemoveChildrenNamed(floatingCanvas, "TrainWindow");
                trainWinInst = (GameObject)PrefabUtility.InstantiatePrefab(_trainWindowPrefab, floatingCanvas);
            }

            var invPresenter = root.GetComponent<PuzzleInventoryPresenter>();
            if (invPresenter)
            {
                var pSo = new SerializedObject(invPresenter);
                var trainWinView = trainWinInst ? trainWinInst.GetComponent<TrainWindowView>() : null;
                pSo.FindProperty("relayoutTrainView").objectReferenceValue = trainWinView ? trainWinView.InventoryView : null;
                pSo.FindProperty("railStripView").objectReferenceValue = railInst ? railInst.GetComponent<RailStripView>() : null;
                pSo.FindProperty("trainWindowView").objectReferenceValue = trainWinView;
                pSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB);
            _log.AppendLine($"GameCanvas 업데이트 (RailStrip, TrainWindow 연결) → {GAME_CANVAS_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void AddResizeHandle(
        RectTransform _parent,
        FloatingWindow _win,
        string _name,
        FloatingWindowMath.ResizeEdge _edge,
        Vector2 _anchorMin,
        Vector2 _anchorMax,
        Vector2 _pivot,
        Vector2 _pos,
        Vector2 _size)
    {
        var rt = NewRect(_name, _parent);
        rt.anchorMin = _anchorMin;
        rt.anchorMax = _anchorMax;
        rt.pivot = _pivot;
        rt.anchoredPosition = _pos;
        rt.sizeDelta = _size;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var handle = rt.gameObject.AddComponent<FloatingWindowResizeHandle>();
        var so = new SerializedObject(handle);
        so.FindProperty("window").objectReferenceValue = _win;
        so.FindProperty("edge").enumValueIndex = (int)_edge;
        so.ApplyModifiedPropertiesWithoutUndo();
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
            img.color = _palette.Panel2;
            BindPalette(go, _palette, UIPalette.ColorRole.Panel2);
        }
        var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!label)
        {
            label = CreateLabel(_labelTemplate, (RectTransform)go.transform, "Label", _fontSize,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        }
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.enableAutoSizing = false;
        label.fontSize = _fontSize;
        label.text = _text;
        label.color = _palette.Text;
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

    static RectTransform AddStaticRect(
        RectTransform _parent,
        string _name,
        float _left,
        float _top,
        float _width,
        float _height,
        Color _color)
    {
        var rect = NewRect(_name, _parent);
        SetTopLeftRect(rect, _left, _top, _width, _height);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = _color;
        img.raycastTarget = false;
        return rect;
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

    static void SetTopLeftRect(RectTransform _rect, float _left, float _top, float _width, float _height)
    {
        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.anchoredPosition = new Vector2(_left, -_top);
        _rect.sizeDelta = new Vector2(_width, _height);
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
}
