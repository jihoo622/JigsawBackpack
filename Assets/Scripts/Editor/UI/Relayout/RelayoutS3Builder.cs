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
    // 화차 한 칸 크기(목업 car shell 81px). 높이 80 = 위 여백 6 + 미니 격자 51 + 간격 4 + 라벨 14 + 아래 여백 5.
    const float CAR_WIDTH = 81f;
    const float CAR_HEIGHT = 80f;
    const float CAR_SPACING = 8f;
    // 개인 화차 창 규격(목업 .train-window: left 26, top 516, 510×544).
    const float TRAIN_WINDOW_LEFT = 26f;
    const float TRAIN_WINDOW_TOP = 516f;
    const float TRAIN_WINDOW_WIDTH = 510f;
    const float TRAIN_WINDOW_HEIGHT = 544f;

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

            // 선로 자갈밭 배경(목업 --scene-rail-bed #1c2026). 평면 사각형만 쓴다.
            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.110f, 0.125f, 0.149f, 1f);
            bg.raycastTarget = true;

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // 침목(목업 --scene-rail-sleeper #453222): 폭 75, 높이 9, 32px 간격. 레일보다 먼저 그려 뒤에 깔리게 한다.
            var sleeperColor = new Color(0.271f, 0.196f, 0.133f, 1f);
            for (float y = 16f; y < 998f; y += 32f)
            {
                AddStaticRect(rootRect, $"Sleeper_{(int)y}", 8f, y, 75f, 9f, sleeperColor);
            }

            // 레일 2줄(목업 --scene-rail-steel #94a3b8): x 19 / 67, 폭 5, 전체 높이. 화차는 이 위에 놓인다.
            var railColor = new Color(0.580f, 0.639f, 0.722f, 1f);
            AddStaticRect(rootRect, "RailLineLeft", 19f, 0f, 5f, 998f, railColor);
            AddStaticRect(rootRect, "RailLineRight", 67f, 0f, 5f, 998f, railColor);

            // 기관차(목업 74×96 SVG): 보일러 (x+15, y+10, 44×48), 운전실 (x+11, y+58, 52×34). 원점 (9, 28).
            const float locoX = 9f;
            const float locoY = 28f;
            var boiler = AddStaticRect(rootRect, "LocomotiveBoiler", locoX + 15f, locoY + 10f, 44f, 48f,
                new Color(0.573f, 0.251f, 0.055f, 1f));
            var boilerOutline = boiler.gameObject.AddComponent<Outline>();
            boilerOutline.effectColor = new Color(0.961f, 0.620f, 0.043f, 1f);
            boilerOutline.effectDistance = new Vector2(2f, -2f);

            var loco = AddStaticRect(rootRect, "Locomotive", locoX + 11f, locoY + 58f, 52f, 34f,
                new Color(0.706f, 0.325f, 0.035f, 1f));
            var locoOutline = loco.gameObject.AddComponent<Outline>();
            locoOutline.effectColor = new Color(0.988f, 0.827f, 0.302f, 1f);
            locoOutline.effectDistance = new Vector2(2f, -2f);
            var locoLbl = CreateLabel(_labelTemplate, loco, "Label", 11f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
            locoLbl.fontStyle = FontStyles.Bold;
            locoLbl.enableAutoSizing = false;
            // 기존 빌더에서 쓰던 '기관차' 문구다. BasicData 키가 없어 그대로 두었다(보고서 참고).
            locoLbl.text = "기관차";
            Stretch(locoLbl.rectTransform);

            // 위로 스크롤 버튼 ▲ (선로 맨 위 오버레이)
            var upBtn = CreateSmallButton(_buttonTemplate, _labelTemplate, rootRect, "ScrollUpButton",
                6f, 2f, 79f, 22f, "▲", 12f, _palette);

            // 스크롤 뷰포트 (y: 128 ~ 968, height: 840). 기관차 아래부터 화차가 나온다.
            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform));
            scrollGo.layer = 5;
            scrollGo.transform.SetParent(rootRect, false);
            var scrollRectRt = (RectTransform)scrollGo.transform;
            SetTopLeftRect(scrollRectRt, 0f, 128f, 91f, 840f);
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
            content.sizeDelta = new Vector2(0f, 840f);

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
            // 화차 간격 8px(연결기 자리)·화차 높이 80px. 목업 car shell 81px 폭에 맞춘다.

            var stripView = rootGo.AddComponent<RailStripView>();
            var insRect = NewRect("InsertionLine", content);
            insRect.anchorMin = new Vector2(0f, 1f);
            insRect.anchorMax = new Vector2(1f, 1f);
            insRect.pivot = new Vector2(0.5f, 0.5f);
            insRect.anchoredPosition = Vector2.zero;
            insRect.sizeDelta = new Vector2(-4f, 2f);
            var insImg = insRect.gameObject.AddComponent<Image>();
            insImg.color = _palette.GoldAccent;
            insImg.raycastTarget = false;
            insRect.gameObject.SetActive(false);

            var so = new SerializedObject(stripView);
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("viewport").objectReferenceValue = viewport;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("scrollUpButton").objectReferenceValue = upBtn;
            so.FindProperty("scrollDownButton").objectReferenceValue = downBtn;
            so.FindProperty("topFade").objectReferenceValue = topFadeRect.GetComponent<Image>();
            so.FindProperty("bottomFade").objectReferenceValue = bfImg;
            so.FindProperty("insertionLine").objectReferenceValue = insImg;
            so.FindProperty("carHeight").floatValue = CAR_HEIGHT;
            so.FindProperty("carSpacing").floatValue = CAR_SPACING;
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
        carRect.anchoredPosition = new Vector2(0f, -(8f + _poolIndex * (CAR_HEIGHT + CAR_SPACING)));
        carRect.sizeDelta = new Vector2(CAR_WIDTH, CAR_HEIGHT);

        var bg = carRect.gameObject.AddComponent<Image>();
        bg.color = _palette.Panel;
        var btn = carRect.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;

        // 선택·호버 채움 틴트(반투명 금색). 테두리는 아래 4변이 맡는다.
        var hlRect = NewRect("HighlightFill", carRect);
        Stretch(hlRect);
        var hlImg = hlRect.gameObject.AddComponent<Image>();
        hlImg.color = new Color(_palette.GoldAccent.r, _palette.GoldAccent.g, _palette.GoldAccent.b, 0.14f);
        hlImg.raycastTarget = false;
        hlImg.enabled = false;

        // 금색 테두리 4변(2px 평면 사각형). 선택 때만 RailWagonCarView가 켠다.
        var edges = new Image[4];
        edges[0] = AddEdgeRect(carRect, "BorderTop", 0f, 0f, CAR_WIDTH, 2f, _palette);
        edges[1] = AddEdgeRect(carRect, "BorderRight", CAR_WIDTH - 2f, 0f, 2f, CAR_HEIGHT, _palette);
        edges[2] = AddEdgeRect(carRect, "BorderBottom", 0f, CAR_HEIGHT - 2f, CAR_WIDTH, 2f, _palette);
        edges[3] = AddEdgeRect(carRect, "BorderLeft", 0f, 0f, 2f, CAR_HEIGHT, _palette);

        // 미니 격자 뒤 어두운 받침(목업 --mg-bg #0d1219). 격자 영역보다 살짝 크다.
        AddStaticRect(carRect, "MiniGridBacking", 2f, 4f, 77f, 55f, new Color(0.051f, 0.071f, 0.098f, 1f));

        // 7×5 미니 격자(9px 칸, 간격 1.5px → 칸 간격 10.5px). y=0이 아래쪽으로 보드와 같은 방향이다.
        var gridContainer = NewRect("MiniGrid", carRect);
        SetTopLeftRect(gridContainer, 4.5f, 6f, 72f, 51f);
        var cells = new Image[RailWagonCarView.MINI_CELL_COUNT];
        const float cellSize = 9f;
        const float stride = 10.5f;
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

        // 라벨은 미니 격자 아래 한 줄('번호 · 적재 · 가치')에 둔다(목업 car-label).
        var lbl = CreateLabel(_labelTemplate, carRect, "Label", 9f,
            TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        lbl.enableWordWrapping = false;
        lbl.enableAutoSizing = false;
        lbl.overflowMode = TextOverflowModes.Ellipsis;
        SetTopLeftRect(lbl.rectTransform, 2f, 62f, 77f, 14f);

        // 화차 연결기: 다음 칸과의 8px 간격에 놓는 평면 사각형(목업 car-coupler 대용).
        AddStaticRect(carRect, "Coupler", (CAR_WIDTH - 10f) * 0.5f, CAR_HEIGHT, 10f, CAR_SPACING,
            new Color(0.392f, 0.455f, 0.545f, 1f));

        var view = carRect.gameObject.AddComponent<RailWagonCarView>();
        var so = new SerializedObject(view);
        so.FindProperty("rectTransform").objectReferenceValue = carRect;
        so.FindProperty("button").objectReferenceValue = btn;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("highlightBorder").objectReferenceValue = hlImg;
        so.FindProperty("label").objectReferenceValue = lbl;
        var edgeProp = so.FindProperty("borderEdges");
        edgeProp.arraySize = edges.Length;
        for (int i = 0; i < edges.Length; i++)
        {
            edgeProp.GetArrayElementAtIndex(i).objectReferenceValue = edges[i];
        }
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
        var rootGo = RelayoutWindowKit.CreateWindow("TrainWindow", null, string.Empty, 0f, 0f, 0f, 0f, false,
            out var floatingWin, out var titleBar, out var titleText, out var closeBtn);
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            // 목업 concept-3b 개인 창: (26, 516) 510×544. 56px 칸 7×5 보드(392×280)가 중심이다.
            SetTopLeftRect(rootRect, TRAIN_WINDOW_LEFT, TRAIN_WINDOW_TOP, TRAIN_WINDOW_WIDTH, TRAIN_WINDOW_HEIGHT);

            titleText.text = "1번 · 고장난 칸 · 적재 0/12";
            // 제목줄 오른쪽: 닫기(24) + 렌즈(44) 버튼과 간격
            RelayoutWindowKit.ReserveTitleRight(titleText, 84f);

            var lensBtn = CreateSmallButton(_buttonTemplate, _labelTemplate, titleBar, "LensButton",
                0f, 0f, 44f, 22f, "렌즈", 12f, _palette);
            var lRt = (RectTransform)lensBtn.transform;
            lRt.anchorMin = new Vector2(1f, 0.5f);
            lRt.anchorMax = new Vector2(1f, 0.5f);
            lRt.pivot = new Vector2(1f, 0.5f);
            lRt.anchoredPosition = new Vector2(-32f, 0f);
            var lensLbl = lensBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            // 기존 GameplayLayout의 Train Inventory 복제
            var oldTrainInv = FindDeep(layoutRoot.transform, "Train Inventory");
            GameObject clonedTrainInv = Object.Instantiate(oldTrainInv.gameObject, rootRect);
            clonedTrainInv.name = "TrainInventoryBody";
            // 본문: 보드(392×280) + 우측 열(88px, 간격 10px) = 490×280, 제목줄과 값 줄 아래 (10, 66).
            var bodyRt = (RectTransform)clonedTrainInv.transform;
            // 값 줄(38~64) 아래 간격 8px.
            SetTopLeftRect(bodyRt, 10f, 72f, 490f, 280f);

            var invView = clonedTrainInv.GetComponent<PuzzleInventoryView>();
            var invSo = new SerializedObject(invView);

            // 기존 보드 제목·이동 안내·화차 아이콘·고정 구분선/배경은 창 규격에 맞게 정리한다.
            var oldTitle = FindDeep(clonedTrainInv.transform, "BoardTitle");
            if (oldTitle) oldTitle.gameObject.SetActive(false);
            var oldGuide = FindDeep(clonedTrainInv.transform, "MoveGuide");
            if (oldGuide) oldGuide.gameObject.SetActive(false);
            var oldWagonImg = FindDeep(clonedTrainInv.transform, "WagonImage");
            if (oldWagonImg) oldWagonImg.gameObject.SetActive(false);
            var oldNotice = FindDeep(clonedTrainInv.transform, "SynergySettlementNotice") as RectTransform;
            if (oldNotice)
            {
                oldNotice.SetParent(bodyRt, false);
                oldNotice.anchorMin = new Vector2(1f, 1f);
                oldNotice.anchorMax = new Vector2(1f, 1f);
                oldNotice.pivot = new Vector2(1f, 1f);
                oldNotice.anchoredPosition = new Vector2(-6f, -2f);
                oldNotice.sizeDelta = new Vector2(120f, 20f);
                oldNotice.gameObject.SetActive(false);
            }
            var oldEffects = FindDeep(clonedTrainInv.transform, "WagonActiveEffects");
            if (oldEffects) oldEffects.gameObject.SetActive(false);
            var oldRefrigBtn = FindDeep(clonedTrainInv.transform, "Wagon Refrigeration Button");
            if (oldRefrigBtn) oldRefrigBtn.gameObject.SetActive(false);
            var oldLocoArt = FindDeep(clonedTrainInv.transform, "LocomotiveArtwork");
            if (oldLocoArt) oldLocoArt.gameObject.SetActive(false);

            // 값 줄(목업 .train-meta-row 왼쪽): 기존 보드 가치 텍스트를 창 상단 값 줄로 옮긴다.
            var oldBoardValue = invSo.FindProperty("boardValue").objectReferenceValue as TextMeshProUGUI;
            if (oldBoardValue)
            {
                var bvRt = oldBoardValue.rectTransform;
                bvRt.SetParent(rootRect, false);
                // 왼쪽 절반(10~247). 오른쪽 절반(255~500)의 다음 정산 요약과 8px 띄운다.
                SetTopLeftRect(bvRt, 10f, 38f, 237f, 26f);
                oldBoardValue.fontSize = 14f;
                oldBoardValue.enableAutoSizing = false;
                oldBoardValue.enableWordWrapping = false;
                oldBoardValue.overflowMode = TextOverflowModes.Ellipsis;
                oldBoardValue.alignment = TextAlignmentOptions.MidlineLeft;
                oldBoardValue.margin = Vector4.zero;
                // 옛 보드의 예시 문구는 이 칸보다 길다. 실행 중 값으로 채워지므로 비워 둔다.
                oldBoardValue.text = string.Empty;
            }
            // 값 줄(목업 오른쪽): 다음 정산 요약을 같은 줄 오른쪽에 둔다. 2줄 렌즈 요약과 겹치지 않는다.
            var nextSettlementLbl = CreateLabel(_labelTemplate, rootRect, "NextSettlementSummary", 13f,
                TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.Positive);
            nextSettlementLbl.fontStyle = FontStyles.Bold;
            nextSettlementLbl.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            nextSettlementLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
            nextSettlementLbl.rectTransform.pivot = new Vector2(1f, 1f);
            nextSettlementLbl.rectTransform.anchoredPosition = new Vector2(-10f, -38f);
            nextSettlementLbl.rectTransform.sizeDelta = new Vector2(-10f, 26f);
            nextSettlementLbl.enableWordWrapping = false;
            nextSettlementLbl.overflowMode = TextOverflowModes.Ellipsis;
            nextSettlementLbl.alignment = TextAlignmentOptions.MidlineRight;

            // 목업에 없는 숙련 칩과 구분선은 숨긴다(값·정산 줄과 보드가 겹치지 않도록).
            var oldChips = FindDeep(clonedTrainInv.transform, "ProficiencyChips");
            if (oldChips) oldChips.gameObject.SetActive(false);
            var oldDivider = FindDeep(clonedTrainInv.transform, "HeaderDivider");
            if (oldDivider) oldDivider.gameObject.SetActive(false);

            // 보드 뷰포트: 392×280(7×5 × 56px). 셀 크기는 뷰포트에서 56px로 계산된다.
            var boardImg = invSo.FindProperty("board").objectReferenceValue as Image;
            var boardRt = boardImg.rectTransform;
            boardImg.enabled = false;

            var viewport = NewRect("BoardViewport", bodyRt);
            SetTopLeftRect(viewport, 0f, 0f, 392f, 280f);
            viewport.gameObject.AddComponent<RectMask2D>();

            // BoardContent는 좌상단 고정 앵커(0,1)를 사용해 sizeDelta가 부모 크기에 더해지지 않도록 한다.
            var content = NewRect("BoardContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(392f, 280f);

            // 우측 열(88×280): 위는 시너지 요약 카드(목업 held-delta-card), 아래는 조작 안내 박스(목업 controls-hint).
            var synergyCard = NewRect("SynergySummaryCard", bodyRt);
            SetTopLeftRect(synergyCard, 402f, 0f, 88f, 96f);
            var scImg = synergyCard.gameObject.AddComponent<Image>();
            scImg.color = _palette.Panel2;
            scImg.raycastTarget = false;
            BindPalette(synergyCard.gameObject, _palette, UIPalette.ColorRole.Panel2);
            var scOutline = synergyCard.gameObject.AddComponent<Outline>();
            scOutline.effectColor = _palette.Border;
            scOutline.effectDistance = new Vector2(1f, -1f);

            var synergyLabel = CreateLabel(_labelTemplate, synergyCard, "SynergyLabel", 10f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.TextSub);
            synergyLabel.enableAutoSizing = false;
            SetTopLeftRect(synergyLabel.rectTransform, 2f, 8f, 84f, 16f);

            var synergyNumber = CreateLabel(_labelTemplate, synergyCard, "SynergyNumber", 21f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Positive);
            synergyNumber.fontStyle = FontStyles.Bold;
            synergyNumber.enableAutoSizing = false;
            SetTopLeftRect(synergyNumber.rectTransform, 2f, 30f, 84f, 34f);
            synergyNumber.text = string.Empty; // 실행 중 값으로 채운다

            var hintBox = NewRect("ControlsHint", bodyRt);
            SetTopLeftRect(hintBox, 402f, 184f, 88f, 96f);
            var hbImg = hintBox.gameObject.AddComponent<Image>();
            hbImg.color = _palette.Panel;
            hbImg.raycastTarget = false;
            BindPalette(hintBox.gameObject, _palette, UIPalette.ColorRole.Panel);
            var hbOutline = hintBox.gameObject.AddComponent<Outline>();
            hbOutline.effectColor = _palette.Border;
            hbOutline.effectDistance = new Vector2(1f, -1f);

            var hintText = CreateLabel(_labelTemplate, hintBox, "ControlsHintText", 10f,
                TextAlignmentOptions.TopLeft, _palette, UIPalette.ColorRole.TextSub);
            hintText.enableAutoSizing = false;
            hintText.enableWordWrapping = true;
            SetTopLeftRect(hintText.rectTransform, 6f, 6f, 76f, 84f);

            // 보드 하위 슬롯들을 BoardContent로 순서대로 이동 (boardCells → placementSlots → outlineSlots → synergyArrows → PlacementCandidates)
            while (boardRt.childCount > 0)
            {
                boardRt.GetChild(0).SetParent(content, false);
            }

            invSo.FindProperty("dynamicCellSizing").boolValue = true;
            invSo.FindProperty("boardViewport").objectReferenceValue = viewport;
            invSo.FindProperty("boardContent").objectReferenceValue = content;
            invSo.FindProperty("minDynamicCellSize").floatValue = 40f;
            invSo.FindProperty("maxDynamicCellSize").floatValue = 72f;

            // S5: 배치 후보 표식을 상단 선만 그려지던 Flat.Outline에서 H16 채움 사각형+외곽선으로 교정한다.
            var candProp = invSo.FindProperty("candidateMarkers");
            if (candProp != null && candProp.isArray)
            {
                for (int c = 0; c < candProp.arraySize; c++)
                {
                    var markerRt = candProp.GetArrayElementAtIndex(c).objectReferenceValue as RectTransform;
                    if (!markerRt) continue;
                    markerRt.anchorMin = new Vector2(0.5f, 0.5f);
                    markerRt.anchorMax = new Vector2(0.5f, 0.5f);
                    markerRt.pivot = new Vector2(0.5f, 0.5f);
                    markerRt.localScale = Vector3.one;
                    var mImg = markerRt.GetComponent<Image>();
                    if (mImg)
                    {
                        mImg.enabled = true;
                        mImg.sprite = null;
                        mImg.type = Image.Type.Simple;
                        mImg.color = c == 0
                            ? new Color(_palette.GoldAccent.r, _palette.GoldAccent.g, _palette.GoldAccent.b, 0.28f)
                            : new Color(_palette.GoldAccent.r, _palette.GoldAccent.g, _palette.GoldAccent.b, 0.18f);
                        mImg.raycastTarget = false;
                    }
                    for (int ch = 0; ch < markerRt.childCount; ch++)
                    {
                        var childImg = markerRt.GetChild(ch).GetComponent<Image>();
                        if (childImg) childImg.enabled = false;
                    }
                    var mOut = markerRt.GetComponent<Outline>() ?? markerRt.gameObject.AddComponent<Outline>();
                    mOut.effectColor = _palette.GoldAccent;
                    mOut.effectDistance = new Vector2(2f, -2f);
                }
            }
            invSo.ApplyModifiedPropertiesWithoutUndo();

            // 하단 2줄 렌즈 요약 (R14)
            // 렌즈 요약 footer (목업 .train-lens-footer): 아래쪽 (10, 356) 490×178, 2줄(견적·다음 정산 Δ).
            var lensFooter = NewRect("LensFooter", rootRect);
            // 보드(72~352) 아래 간격 8px, 창 아래 여백 8px.
            SetTopLeftRect(lensFooter, 10f, 360f, 490f, 176f);
            var lfBg = lensFooter.gameObject.AddComponent<Image>();
            lfBg.color = _palette.Panel2;
            lfBg.raycastTarget = false;
            BindPalette(lensFooter.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var tradeLine = CreateLabel(_labelTemplate, lensFooter, "LensTradeLine", 13f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            tradeLine.overflowMode = TextOverflowModes.Ellipsis;
            tradeLine.rectTransform.anchorMin = new Vector2(0f, 1f);
            tradeLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            tradeLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            tradeLine.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            tradeLine.rectTransform.sizeDelta = new Vector2(-20f, 22f);

            var spotLine = CreateLabel(_labelTemplate, lensFooter, "LensSpotLine", 13f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            spotLine.overflowMode = TextOverflowModes.Ellipsis;
            spotLine.rectTransform.anchorMin = new Vector2(0f, 1f);
            spotLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            spotLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            spotLine.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            spotLine.rectTransform.sizeDelta = new Vector2(-20f, 22f);

            // FloatingWindow 직렬화 (목업 규격 510×544, 기본 위치 (26, 516))
            var fwSo = new SerializedObject(floatingWin);
            fwSo.FindProperty("windowId").stringValue = "train";
            fwSo.FindProperty("titleBar").objectReferenceValue = titleBar;
            fwSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            fwSo.FindProperty("defaultPosition").vector2Value = new Vector2(TRAIN_WINDOW_LEFT, TRAIN_WINDOW_TOP);
            fwSo.FindProperty("defaultSize").vector2Value = new Vector2(TRAIN_WINDOW_WIDTH, TRAIN_WINDOW_HEIGHT);
            fwSo.FindProperty("startOpen").boolValue = false;
            fwSo.ApplyModifiedPropertiesWithoutUndo();

            // TrainWindowView 직렬화
            var winView = rootGo.AddComponent<TrainWindowView>();
            var wvSo = new SerializedObject(winView);
            wvSo.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            wvSo.FindProperty("titleText").objectReferenceValue = titleText;
            wvSo.FindProperty("nextSettlementText").objectReferenceValue = nextSettlementLbl;
            wvSo.FindProperty("synergyLabelText").objectReferenceValue = synergyLabel;
            wvSo.FindProperty("synergyNumberText").objectReferenceValue = synergyNumber;
            wvSo.FindProperty("controlsHintText").objectReferenceValue = hintText;
            wvSo.FindProperty("lensTradeLine").objectReferenceValue = tradeLine;
            wvSo.FindProperty("lensSpotLine").objectReferenceValue = spotLine;
            wvSo.FindProperty("inventoryView").objectReferenceValue = invView;
            wvSo.FindProperty("lensButton").objectReferenceValue = lensBtn;
            wvSo.FindProperty("lensLabel").objectReferenceValue = lensLbl;
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
            int maxTrainWindows = Mathf.Max(1, BasicData.Int("ui.maxTrainWindows"));
            var trainWindows = new TrainWindowView[maxTrainWindows];
            TrainWindowManager twManager = null;
            if (floatingCanvas)
            {
                for (int c = floatingCanvas.childCount - 1; c >= 0; c--)
                {
                    if (floatingCanvas.GetChild(c).name.StartsWith("TrainWindow"))
                    {
                        Object.DestroyImmediate(floatingCanvas.GetChild(c).gameObject);
                    }
                }
                for (int i = 0; i < maxTrainWindows; i++)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(_trainWindowPrefab, floatingCanvas);
                    inst.name = $"TrainWindow_{i}";
                    var fw = inst.GetComponent<FloatingWindow>();
                    if (fw)
                    {
                        var fwSo = new SerializedObject(fw);
                        fwSo.FindProperty("windowId").stringValue = $"train.{i}";
                        fwSo.FindProperty("startOpen").boolValue = false;
                        fwSo.FindProperty("defaultPosition").vector2Value = TrainWindowManager.GetDefaultSlotPosition(i);
                        fwSo.FindProperty("defaultSize").vector2Value = new Vector2(TRAIN_WINDOW_WIDTH, TRAIN_WINDOW_HEIGHT);
                        fwSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    trainWindows[i] = inst.GetComponent<TrainWindowView>();
                }
                twManager = floatingCanvas.GetComponent<TrainWindowManager>();
                if (!twManager)
                {
                    twManager = floatingCanvas.gameObject.AddComponent<TrainWindowManager>();
                }
                var tmSo = new SerializedObject(twManager);
                var winProp = tmSo.FindProperty("windows");
                winProp.arraySize = maxTrainWindows;
                for (int i = 0; i < maxTrainWindows; i++)
                {
                    winProp.GetArrayElementAtIndex(i).objectReferenceValue = trainWindows[i];
                }
                tmSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var invPresenter = root.GetComponent<PuzzleInventoryPresenter>();
            if (invPresenter)
            {
                var pSo = new SerializedObject(invPresenter);
                var firstWin = trainWindows.Length > 0 ? trainWindows[0] : null;
                pSo.FindProperty("relayoutTrainView").objectReferenceValue = firstWin ? firstWin.InventoryView : null;
                pSo.FindProperty("railStripView").objectReferenceValue = railInst ? railInst.GetComponent<RailStripView>() : null;
                pSo.FindProperty("trainWindowView").objectReferenceValue = firstWin;
                pSo.FindProperty("trainWindowManager").objectReferenceValue = twManager;
                pSo.FindProperty("townSceneView").objectReferenceValue = root.GetComponentInChildren<TownSceneView>(true);
                pSo.FindProperty("statusBannerView").objectReferenceValue = root.GetComponentInChildren<StatusBannerView>(true);
                pSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB);
            _log.AppendLine($"GameCanvas 업데이트 (RailStrip, TrainWindow×{maxTrainWindows} 연결) → {GAME_CANVAS_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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

    // 화차 테두리 한 변. 기본은 꺼 두고 선택 때 RailWagonCarView가 켠다.
    static Image AddEdgeRect(RectTransform _car, string _name, float _left, float _top, float _width, float _height,
        UIPalette _palette)
    {
        var rect = AddStaticRect(_car, _name, _left, _top, _width, _height, _palette.GoldAccent);
        var img = rect.GetComponent<Image>();
        img.enabled = false;
        return img;
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
