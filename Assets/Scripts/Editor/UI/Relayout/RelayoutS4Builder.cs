using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// S4: 5탭(지도/소문/의뢰/숙련/상성표) 통합 수첩 부동 창(NotebookWindow)을 프리팹에 멱등적으로 구성한다.
public static class RelayoutS4Builder
{
    const string MENU = "Tools/MyJG/UI/Relayout/S4 Build Notebook Window";
    const string PALETTE_PATH = "Assets/Resources/UI/UIPalette.asset";
    const string RELAYOUT_DIR = "Assets/Art/UI/Relayout";
    const string NOTEBOOK_PREFAB = "Assets/Art/UI/Relayout/NotebookWindow.prefab";
    const string PAUSE_PREFAB = "Assets/Art/UI/Scene/PauseCanvas.prefab";
    const string WORLD_MAP_PREFAB = "Assets/Art/PuzzleWorldMap.prefab";
    const string SYNERGY_CHART_PREFAB = "Assets/Art/PuzzleSynergyChart.prefab";
    const string GAME_CANVAS_PREFAB = "Assets/Art/UI/Scene/GameCanvas.prefab";

    const int PIN_POOL_SIZE = 16;

    // 지도 탭 임베드 레이아웃 (수첩 창 522px - 본문 여백 16px = 본문 폭 506px, 범례/컨트롤 좌우 여백 4px씩 = 498px)
    const float MAP_LEGEND_HEIGHT = 58f;          // 3행 x 18px + 행 간격 2px x 2
    const float MAP_LEGEND_CELL_W = 162f;         // 3열 x 162px + 열 간격 4px x 2 = 494px (498px 이내)
    const float MAP_LEGEND_CELL_H = 18f;
    const float MAP_LEGEND_GAP_X = 4f;
    const float MAP_LEGEND_GAP_Y = 2f;
    const int MAP_LEGEND_COLUMNS = 3;
    const float MAP_VIEWPORT_TOP_OFFSET = -106f;  // 검색줄(-4~-40) + 범례(-42~-100) 아래 여백 6px
    const float MAP_CONTROL_HEIGHT = 36f;
    const float MAP_CONTROL_BUTTON_HEIGHT = 32f;
    const float MAP_CONTROL_GAP = 4f;
    // 하단 구조(아래→위): 컨트롤 줄 4~40 → 여백 4 → 도시 정보 띠 44~110(3줄 고정) → 여백 6 → 지도 뷰포트
    const float MAP_INFO_BOTTOM = 44f;            // 도시 정보 띠 하단 (컨트롤 줄 상단 40 + 여백 4)
    const float MAP_INFO_HEIGHT = 66f;            // 14pt 3줄 + 여유
    const float MAP_INFO_FONT_SIZE = 14f;
    const float MAP_VIEWPORT_BOTTOM_OFFSET = 116f; // 정보 띠 상단 110 + 여백 6
    const float MAP_SIDE_INSET = 4f;              // 뷰포트 좌우 여백(뷰포트와 동일 영역을 쓰는 오버레이 기준)
    const float MAP_BOTTOM_CARD_BOTTOM = 120f;    // 뷰포트 하단 안쪽 카드(거래 선호·의뢰 요약)의 하단 위치

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

            var notebookPrefab = BuildNotebookWindowPrefab(log, palette, buttonTemplate, labelTemplate);
            UpdateGameCanvas(log, notebookPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pauseRoot);
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static GameObject BuildNotebookWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var mapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WORLD_MAP_PREFAB);
        var synergyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SYNERGY_CHART_PREFAB);
        var gcRoot = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);

        var rootGo = new GameObject("NotebookWindow", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 16f, 128f, 522f, 928f);

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
            titleBar.gameObject.AddComponent<FloatingWindowDragHandle>();

            var titleText = CreateLabel(_labelTemplate, titleBar, "TitleText", 16f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TitleText);
            titleText.fontStyle = FontStyles.Bold;
            titleText.text = "수첩";
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

            // 5탭 바 (지도 / 소문 / 의뢰 / 숙련 / 상성표)
            var tabBar = NewRect("TabBar", rootRect);
            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.anchoredPosition = new Vector2(0f, -34f);
            tabBar.sizeDelta = new Vector2(-12f, 36f);
            var hLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 4f;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = true;

            string[] tabNames = { "지도", "소문", "의뢰", "숙련", "상성표" };
            var tabButtons = new Button[5];
            var tabLabels = new TextMeshProUGUI[5];
            var tabUnderlines = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                var btn = CreateSmallButton(_buttonTemplate, _labelTemplate, tabBar, $"Tab_{i}",
                    0f, 0f, 90f, 36f, tabNames[i], 14f, _palette);
                tabButtons[i] = btn;
                tabLabels[i] = btn.GetComponentInChildren<TextMeshProUGUI>(true);

                var ul = NewRect("Underline", (RectTransform)btn.transform);
                ul.anchorMin = new Vector2(0f, 0f);
                ul.anchorMax = new Vector2(1f, 0f);
                ul.pivot = new Vector2(0.5f, 0f);
                ul.anchoredPosition = Vector2.zero;
                ul.sizeDelta = new Vector2(0f, 2f);
                var ulImg = ul.gameObject.AddComponent<Image>();
                ulImg.color = _palette.GoldAccent;
                ulImg.raycastTarget = false;
                tabUnderlines[i] = ulImg;
            }

            // 본문 컨테이너 (RectMask2D)
            var contentArea = NewRect("ContentArea", rootRect);
            contentArea.anchorMin = Vector2.zero;
            contentArea.anchorMax = Vector2.one;
            contentArea.offsetMin = new Vector2(8f, 8f);
            contentArea.offsetMax = new Vector2(-8f, -74f);
            contentArea.gameObject.AddComponent<RectMask2D>();

            var winView = rootGo.AddComponent<NotebookWindowView>();

            // 1) 지도 탭 콘텐츠 (PuzzleWorldMap 임베드 + 목적지 핀 풀)
            var mapContent = NewRect("MapTabContent", contentArea);
            Stretch(mapContent);
            GameObject mapInst = Object.Instantiate(mapAsset, mapContent);
            mapInst.name = "EmbeddedMap";
            Stretch((RectTransform)mapInst.transform);
            // 외부 Canvas가 있으면 의존성 순서(GraphicRaycaster → CanvasScaler → Canvas)대로 제거해 클립되게 한다.
            StripCanvases(mapInst);
            // 루트 Canvas가 빠지면 그 순간의 게임 뷰 크기가 루트 RectTransform에 남는다. 제거한 뒤 다시 창에 맞춘다.
            Stretch((RectTransform)mapInst.transform);

            var mapPanel = FindDeep(mapInst.transform, "MapPanel") as RectTransform;
            if (mapPanel)
            {
                Stretch(mapPanel);
                var hideNames = new[] { "Title", "Guide", "CloseMap" };
                foreach (string hn in hideNames)
                {
                    var h = mapPanel.Find(hn);
                    if (h) h.gameObject.SetActive(false);
                }
                var search = mapPanel.Find("MapSearch") as RectTransform;
                if (search)
                {
                    search.anchorMin = new Vector2(0f, 1f);
                    search.anchorMax = new Vector2(1f, 1f);
                    search.pivot = new Vector2(0.5f, 1f);
                    search.anchoredPosition = new Vector2(0f, -4f);
                    search.sizeDelta = new Vector2(-8f, 36f);
                }
                var vp = mapPanel.Find("MapViewport") as RectTransform;
                if (vp)
                {
                    vp.anchorMin = Vector2.zero;
                    vp.anchorMax = Vector2.one;
                    vp.offsetMin = new Vector2(MAP_SIDE_INSET, MAP_VIEWPORT_BOTTOM_OFFSET);
                    vp.offsetMax = new Vector2(-MAP_SIDE_INSET, MAP_VIEWPORT_TOP_OFFSET);
                    if (!vp.GetComponent<RectMask2D>()) vp.gameObject.AddComponent<RectMask2D>();
                }

                // 뷰포트와 같은 영역을 쓰는 오버레이(이동 확인·경로 계획): 원본은 MapPanel 전체(0~1)를 덮어 하단 정보/컨트롤 줄까지 가린다.
                // 뷰포트 영역으로 맞추고 클립해 창 밖·하단 줄로 새지 않게 한다.
                foreach (string overlayName in new[] { "MapTravelPanel", "RoutePlan" })
                {
                    var overlay = mapPanel.Find(overlayName) as RectTransform;
                    if (!overlay) continue;
                    FitToViewportRect(overlay);
                }

                // 도시 호버 툴팁(TownTooltip): 맵 루트의 중첩 프리팹으로 MapPanel 밖(루트 직계)에 있어 원본 캔버스 기준으로 떠서
                // 창 왼쪽 가장자리에 세로 막대처럼 삐져나온다. 뷰포트 영역 클립 컨테이너 아래로 옮겨 창 밖으로 나가지 않게 한다.
                var tooltipRt = mapInst.transform.Find("TownTooltip") as RectTransform;
                if (tooltipRt)
                {
                    var tooltipClip = NewRect("TooltipClip", mapPanel);
                    FitToViewportRect(tooltipClip);
                    tooltipRt.SetParent(tooltipClip, false);
                }
                // 범례: 원본은 SubwayMapLabels(HorizontalLayoutGroup) 한 줄에 7개 항목이 모두 들어가 우측 창 경계에서 잘린다.
                // 항목 부모인 SubwayMapLabels의 HLG를 3열 격자로 바꿔 3행으로 감싼다. 항목 글자는 12pt, 한 줄, 넘치면 말줄임.
                var legend = mapPanel.Find("MapLegend") as RectTransform;
                if (legend)
                {
                    legend.anchorMin = new Vector2(0f, 1f);
                    legend.anchorMax = new Vector2(1f, 1f);
                    legend.pivot = new Vector2(0.5f, 1f);
                    legend.anchoredPosition = new Vector2(0f, -42f);
                    legend.sizeDelta = new Vector2(-8f, MAP_LEGEND_HEIGHT);
                    // 이전 빌드가 MapLegend 자신에 달아 둔 격자는 제거한다(멱등성).
                    var staleGrid = legend.GetComponent<GridLayoutGroup>();
                    if (staleGrid) Object.DestroyImmediate(staleGrid);

                    var labelsRt = legend.Find("SubwayMapLabels") as RectTransform;
                    if (labelsRt)
                    {
                        Stretch(labelsRt);
                        var labelsHlg = labelsRt.GetComponent<HorizontalLayoutGroup>();
                        if (labelsHlg) Object.DestroyImmediate(labelsHlg);
                        var grid = labelsRt.GetComponent<GridLayoutGroup>() ?? labelsRt.gameObject.AddComponent<GridLayoutGroup>();
                        grid.padding = new RectOffset(0, 0, 0, 0);
                        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                        grid.cellSize = new Vector2(MAP_LEGEND_CELL_W, MAP_LEGEND_CELL_H);
                        grid.spacing = new Vector2(MAP_LEGEND_GAP_X, MAP_LEGEND_GAP_Y);
                        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        grid.constraintCount = MAP_LEGEND_COLUMNS;
                        grid.childAlignment = TextAnchor.MiddleLeft;
                    }

                    foreach (var legendText in legend.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (!legendText.name.StartsWith("LineName_")) continue;
                        legendText.enableAutoSizing = false;
                        legendText.fontSize = 12f;
                        legendText.enableWordWrapping = false;
                        legendText.overflowMode = TextOverflowModes.Ellipsis;
                    }
                }

                // 하단 컨트롤 줄: 원본은 1036px 기준 절대 좌표(중심 앵커)라 우측 '현재 위치'가 창 밖으로 밀린다.
                // 좌측 기준 고정 폭으로 순서대로 배치해 합계를 498px(본문 폭)에 맞춘다: 56+80+56+140+150 + 간격 4x4 = 498.
                var ctrl = mapPanel.Find("MapControls") as RectTransform;
                if (ctrl)
                {
                    ctrl.anchorMin = new Vector2(0f, 0f);
                    ctrl.anchorMax = new Vector2(1f, 0f);
                    ctrl.pivot = new Vector2(0.5f, 0f);
                    ctrl.anchoredPosition = new Vector2(0f, 4f);
                    ctrl.sizeDelta = new Vector2(-8f, MAP_CONTROL_HEIGHT);

                    string[] ctrlNames = { "ZoomOut", "ZoomPercent", "ZoomIn", "FitMap", "LocateCurrent" };
                    float[] ctrlWidths = { 56f, 80f, 56f, 140f, 150f };
                    float cursorX = 0f;
                    for (int c = 0; c < ctrlNames.Length; c++)
                    {
                        var childRt = ctrl.Find(ctrlNames[c]) as RectTransform;
                        if (!childRt) continue;
                        childRt.anchorMin = new Vector2(0f, 0.5f);
                        childRt.anchorMax = new Vector2(0f, 0.5f);
                        childRt.pivot = new Vector2(0f, 0.5f);
                        childRt.anchoredPosition = new Vector2(cursorX, 0f);
                        childRt.sizeDelta = new Vector2(ctrlWidths[c], MAP_CONTROL_BUTTON_HEIGHT);
                        cursorX += ctrlWidths[c] + MAP_CONTROL_GAP;
                    }
                }
                // 도시 정보 띠: 컨트롤 줄 위에 고정 높이(3줄)로 둔다. 자동 줄바꿈, 넘치면 말줄임(띠 안에서만 보임).
                var cityInfo = mapPanel.Find("CityInformation") as RectTransform;
                if (cityInfo)
                {
                    cityInfo.anchorMin = new Vector2(0f, 0f);
                    cityInfo.anchorMax = new Vector2(1f, 0f);
                    cityInfo.pivot = new Vector2(0.5f, 0f);
                    cityInfo.anchoredPosition = new Vector2(0f, MAP_INFO_BOTTOM);
                    cityInfo.sizeDelta = new Vector2(-8f, MAP_INFO_HEIGHT);
                    if (!cityInfo.GetComponent<RectMask2D>()) cityInfo.gameObject.AddComponent<RectMask2D>();

                    var infoText = cityInfo.GetComponent<TextMeshProUGUI>() ?? cityInfo.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (infoText)
                    {
                        infoText.enableAutoSizing = false;
                        infoText.fontSize = MAP_INFO_FONT_SIZE;
                        infoText.enableWordWrapping = true;
                        infoText.overflowMode = TextOverflowModes.Ellipsis;
                        infoText.alignment = TextAlignmentOptions.TopLeft;
                    }
                }

                // 뷰포트 안쪽 하단 카드: 원본은 정보 띠 아래(-650/-708, 상단 기준)라 뷰포트 영역에 겹친다.
                // 뷰포트 하단 안쪽(MAP_BOTTOM_CARD_BOTTOM 기준)으로 옮기고 폭 밖은 클립한다.
                var tradePrefs = mapPanel.Find("MapTradePreferences") as RectTransform;
                if (tradePrefs)
                {
                    tradePrefs.anchorMin = new Vector2(0f, 0f);
                    tradePrefs.anchorMax = new Vector2(1f, 0f);
                    tradePrefs.pivot = new Vector2(0.5f, 0f);
                    tradePrefs.anchoredPosition = new Vector2(0f, MAP_BOTTOM_CARD_BOTTOM);
                    tradePrefs.sizeDelta = new Vector2(-MAP_SIDE_INSET * 4f, 56f);
                    if (!tradePrefs.GetComponent<RectMask2D>()) tradePrefs.gameObject.AddComponent<RectMask2D>();
                }
                var questInfo = mapPanel.Find("CityQuestInformation") as RectTransform;
                if (questInfo)
                {
                    questInfo.anchorMin = new Vector2(0f, 0f);
                    questInfo.anchorMax = new Vector2(1f, 0f);
                    questInfo.pivot = new Vector2(0.5f, 0f);
                    questInfo.anchoredPosition = new Vector2(0f, MAP_BOTTOM_CARD_BOTTOM + 58f);
                    questInfo.sizeDelta = new Vector2(-MAP_SIDE_INSET * 4f, 22f);
                    if (!questInfo.GetComponent<RectMask2D>()) questInfo.gameObject.AddComponent<RectMask2D>();
                }
            }

            var mapViewport = FindDeep(mapInst.transform, "MapViewport") as RectTransform;
            var pinsLayer = NewRect("PinsLayer", mapViewport ? mapViewport : mapContent);
            Stretch(pinsLayer);
            var pinPool = new MapDestinationPinView[PIN_POOL_SIZE];
            for (int i = 0; i < PIN_POOL_SIZE; i++)
            {
                pinPool[i] = CreateDestinationPin(pinsLayer, i, _palette, _labelTemplate);
            }

            var overflowLbl = CreateLabel(_labelTemplate, pinsLayer, "PinOverflowLabel", 12f,
                TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.GoldAccent);
            overflowLbl.rectTransform.anchorMin = new Vector2(1f, 0f);
            overflowLbl.rectTransform.anchorMax = new Vector2(1f, 0f);
            overflowLbl.rectTransform.pivot = new Vector2(1f, 0f);
            overflowLbl.rectTransform.anchoredPosition = new Vector2(-8f, 8f);
            overflowLbl.rectTransform.sizeDelta = new Vector2(120f, 22f);
            overflowLbl.raycastTarget = false;
            overflowLbl.gameObject.SetActive(false);

            var embeddedMapPresenter = mapInst.GetComponentInChildren<PuzzleMapPresenter>(true);
            var embeddedMapView = mapInst.GetComponentInChildren<PuzzleMapView>(true);
            if (embeddedMapPresenter)
            {
                var mpSo = new SerializedObject(embeddedMapPresenter);
                mpSo.FindProperty("embeddedInNotebook").boolValue = true;
                mpSo.FindProperty("notebookWindow").objectReferenceValue = winView;
                mpSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 2) 소문/의뢰/숙련 탭 콘텐츠 (기존 RumorLogPanel 재사용)
            var textContent = NewRect("TextTabsContent", contentArea);
            Stretch(textContent);
            var oldRumor = gcRoot.transform.Find("RumorLogPanel");
            GameObject nbInst = Object.Instantiate(oldRumor.gameObject, textContent);
            nbInst.name = "EmbeddedNotebook";
            Stretch((RectTransform)nbInst.transform);
            var sideHandle = nbInst.transform.Find("SideHandle");
            if (sideHandle) sideHandle.gameObject.SetActive(false);

            var logPanel = nbInst.transform.Find("LogPanel") as RectTransform;
            if (logPanel)
            {
                logPanel.gameObject.SetActive(true);
                Stretch(logPanel);
                var hideNb = new[] { "LogHeader", "LogTabs", "TabDivider" };
                foreach (string hn in hideNb)
                {
                    var h = logPanel.Find(hn);
                    if (h) h.gameObject.SetActive(false);
                }
                var logScroll = logPanel.Find("LogScroll") as RectTransform;
                if (logScroll) Stretch(logScroll);
                var profPanel = logPanel.Find("ProficiencyPanel") as RectTransform;
                if (profPanel) Stretch(profPanel);
            }

            var embeddedNbView = nbInst.GetComponent<PuzzleTradeNotebookView>();
            if (embeddedNbView)
            {
                var nbvSo = new SerializedObject(embeddedNbView);
                nbvSo.FindProperty("embeddedInNotebook").boolValue = true;
                nbvSo.FindProperty("startOpen").boolValue = true;
                nbvSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var embeddedNbPres = nbInst.GetComponent<PuzzleTradeNotebookPresenter>();
            if (embeddedNbPres)
            {
                var nbpSo = new SerializedObject(embeddedNbPres);
                nbpSo.FindProperty("embeddedInNotebook").boolValue = true;
                nbpSo.FindProperty("notebookWindow").objectReferenceValue = winView;
                nbpSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3) 상성표 탭 콘텐츠 (불투명 배경 + ScrollRect로 겹침 원천 차단)
            var synContent = NewRect("SynergyTabContent", contentArea);
            Stretch(synContent);
            var synBg = synContent.gameObject.AddComponent<Image>();
            synBg.color = _palette.Panel;
            BindPalette(synContent.gameObject, _palette, UIPalette.ColorRole.Panel);

            var synScrollGo = new GameObject("SynergyScrollRect", typeof(RectTransform));
            synScrollGo.layer = 5;
            synScrollGo.transform.SetParent(synContent, false);
            var synScrollRt = (RectTransform)synScrollGo.transform;
            Stretch(synScrollRt);
            var synScroll = synScrollGo.AddComponent<ScrollRect>();
            synScroll.horizontal = true;
            // 상성표는 창보다 넓고 높이는 창 안에 들어간다. 세로 스크롤을 끄면 마우스 휠이 가로로 넘긴다.
            synScroll.vertical = false;
            synScroll.movementType = ScrollRect.MovementType.Clamped;
            synScroll.scrollSensitivity = 36f;

            // 넘겨 볼 수 있다는 것을 알 수 있게 아래쪽에 단색 가로 스크롤바를 둔다.
            var hBar = NewRect("HorizontalScrollbar", synScrollRt);
            hBar.anchorMin = new Vector2(0f, 0f);
            hBar.anchorMax = new Vector2(1f, 0f);
            hBar.pivot = new Vector2(0.5f, 0f);
            hBar.anchoredPosition = Vector2.zero;
            hBar.sizeDelta = new Vector2(0f, 12f);
            var hBarBg = hBar.gameObject.AddComponent<Image>();
            hBarBg.color = _palette.Border;
            var hSliding = NewRect("SlidingArea", hBar);
            Stretch(hSliding);
            var hHandle = NewRect("Handle", hSliding);
            Stretch(hHandle);
            var hHandleImg = hHandle.gameObject.AddComponent<Image>();
            hHandleImg.color = _palette.GoldAccent;
            var hScrollbar = hBar.gameObject.AddComponent<Scrollbar>();
            hScrollbar.direction = Scrollbar.Direction.LeftToRight;
            hScrollbar.handleRect = hHandle;
            hScrollbar.targetGraphic = hHandleImg;
            synScroll.horizontalScrollbar = hScrollbar;
            synScroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var synVp = NewRect("Viewport", synScrollRt);
            Stretch(synVp);
            synVp.gameObject.AddComponent<RectMask2D>();
            var synVpImg = synVp.gameObject.AddComponent<Image>();
            synVpImg.color = new Color(0f, 0f, 0f, 0f);
            synVpImg.raycastTarget = true;

            GameObject synInst = Object.Instantiate(synergyAsset, synVp);
            synInst.name = "EmbeddedSynergyChart";
            Stretch((RectTransform)synInst.transform);
            StripCanvases(synInst);
            Stretch((RectTransform)synInst.transform);

            var synPanel = synInst.transform.Find("ChartPanel") as RectTransform;
            if (synPanel)
            {
                synPanel.anchorMin = new Vector2(0f, 1f);
                synPanel.anchorMax = new Vector2(0f, 1f);
                synPanel.pivot = new Vector2(0f, 1f);
                synPanel.anchoredPosition = Vector2.zero;
                synScroll.viewport = synVp;
                synScroll.content = synPanel;
            }
            var embeddedSynView = synInst.GetComponent<PuzzleSynergyChartView>();
            if (embeddedSynView)
            {
                var svSo = new SerializedObject(embeddedSynView);
                svSo.FindProperty("startOpen").boolValue = true;
                svSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // FloatingWindow 직렬화 (고정 크기)
            var fwSo = new SerializedObject(floatingWin);
            fwSo.FindProperty("windowId").stringValue = "notebook";
            fwSo.FindProperty("titleBar").objectReferenceValue = titleBar;
            fwSo.FindProperty("closeButton").objectReferenceValue = closeBtn;
            fwSo.FindProperty("defaultPosition").vector2Value = new Vector2(16f, 128f);
            fwSo.FindProperty("defaultSize").vector2Value = new Vector2(522f, 928f);
            fwSo.FindProperty("startOpen").boolValue = false;
            fwSo.ApplyModifiedPropertiesWithoutUndo();

            // NotebookWindowView 직렬화
            var wvSo = new SerializedObject(winView);
            wvSo.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            wvSo.FindProperty("titleText").objectReferenceValue = titleText;
            SetObjectArray(wvSo, "tabButtons", tabButtons);
            SetObjectArray(wvSo, "tabLabels", tabLabels);
            SetObjectArray(wvSo, "tabUnderlines", tabUnderlines);
            wvSo.FindProperty("mapTabContent").objectReferenceValue = mapContent.gameObject;
            wvSo.FindProperty("textTabsContent").objectReferenceValue = textContent.gameObject;
            wvSo.FindProperty("synergyTabContent").objectReferenceValue = synContent.gameObject;
            wvSo.FindProperty("embeddedNotebookView").objectReferenceValue = embeddedNbView;
            wvSo.FindProperty("embeddedNotebookPresenter").objectReferenceValue = embeddedNbPres;
            wvSo.FindProperty("embeddedMapView").objectReferenceValue = embeddedMapView;
            wvSo.FindProperty("embeddedMapPresenter").objectReferenceValue = embeddedMapPresenter;
            wvSo.FindProperty("embeddedSynergyView").objectReferenceValue = embeddedSynView;
            wvSo.FindProperty("mapViewport").objectReferenceValue = mapViewport;
            SetObjectArray(wvSo, "pinPool", pinPool);
            wvSo.FindProperty("pinOverflowLabel").objectReferenceValue = overflowLbl;
            wvSo.FindProperty("palette").objectReferenceValue = _palette;
            wvSo.ApplyModifiedPropertiesWithoutUndo();

            mapContent.gameObject.SetActive(true);
            textContent.gameObject.SetActive(false);
            synContent.gameObject.SetActive(false);
            rootGo.SetActive(false);

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, NOTEBOOK_PREFAB);
            _log.AppendLine($"NotebookWindow 프리팹(5탭, 핀 {PIN_POOL_SIZE}개) → {NOTEBOOK_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(gcRoot);
        }
    }

    static MapDestinationPinView CreateDestinationPin(
        RectTransform _parent,
        int _index,
        UIPalette _palette,
        TextMeshProUGUI _labelTemplate)
    {
        var rt = NewRect($"Pin_{_index}", _parent);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(180f, 22f);

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = _palette.Panel;
        bg.raycastTarget = false;

        var border = rt.gameObject.AddComponent<Outline>();
        border.effectColor = _palette.GoldAccent;
        border.effectDistance = new Vector2(1f, -1f);

        var lbl = CreateLabel(_labelTemplate, rt, "Label", 12f,
            TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        lbl.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(lbl.rectTransform);
        lbl.rectTransform.offsetMin = new Vector2(4f, 0f);
        lbl.rectTransform.offsetMax = new Vector2(-4f, 0f);

        var leaderRt = NewRect("LeaderLine", rt);
        leaderRt.anchorMin = new Vector2(0.5f, 0f);
        leaderRt.anchorMax = new Vector2(0.5f, 0f);
        leaderRt.pivot = new Vector2(0.5f, 1f);
        leaderRt.anchoredPosition = Vector2.zero;
        leaderRt.sizeDelta = new Vector2(1f, 0f);
        var leaderImg = leaderRt.gameObject.AddComponent<Image>();
        leaderImg.color = _palette.GoldAccent;
        leaderImg.raycastTarget = false;
        leaderRt.gameObject.SetActive(false);

        var pin = rt.gameObject.AddComponent<MapDestinationPinView>();
        var so = new SerializedObject(pin);
        so.FindProperty("rectTransform").objectReferenceValue = rt;
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("border").objectReferenceValue = border;
        so.FindProperty("label").objectReferenceValue = lbl;
        so.FindProperty("leaderLine").objectReferenceValue = leaderImg;
        so.ApplyModifiedPropertiesWithoutUndo();
        rt.gameObject.SetActive(false);
        return pin;
    }

    static void UpdateGameCanvas(StringBuilder _log, GameObject _notebookPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);
        try
        {
            var floatingCanvas = root.transform.Find("FloatingWindowCanvas");
            GameObject nbInst = null;
            if (floatingCanvas)
            {
                RemoveChildrenNamed(floatingCanvas, "NotebookWindow");
                nbInst = (GameObject)PrefabUtility.InstantiatePrefab(_notebookPrefab, floatingCanvas);
            }
            var nbWinView = nbInst ? nbInst.GetComponent<NotebookWindowView>() : null;

            var invPresenter = root.GetComponent<PuzzleInventoryPresenter>();
            if (invPresenter && nbWinView)
            {
                var pSo = new SerializedObject(invPresenter);
                pSo.FindProperty("notebookWindowView").objectReferenceValue = nbWinView;
                pSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var qtView = root.GetComponentInChildren<QuickToolbarView>(true);
            if (qtView && nbWinView)
            {
                var qSo = new SerializedObject(qtView);
                qSo.FindProperty("notebookWindowView").objectReferenceValue = nbWinView;
                qSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var controller = root.GetComponent<RelayoutRootController>();
            if (controller)
            {
                var rSo = new SerializedObject(controller);
                rSo.FindProperty("notebookWindow").objectReferenceValue = nbWinView;
                rSo.FindProperty("oldRumorLogPanel").objectReferenceValue = root.transform.Find("RumorLogPanel")?.gameObject;
                rSo.FindProperty("oldPuzzleSynergyChart").objectReferenceValue = root.transform.Find("PuzzleSynergyChart")?.gameObject;
                var oldMap = root.GetComponentsInChildren<PuzzleMapPresenter>(true)
                    .FirstOrDefault(_p => _p && !_p.IsEmbeddedInNotebook);
                if (oldMap)
                {
                    rSo.FindProperty("oldPuzzleWorldMap").objectReferenceValue = oldMap.gameObject;
                }
                rSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB);
            _log.AppendLine($"GameCanvas 업데이트 (NotebookWindow 연결) → {GAME_CANVAS_PREFAB}");
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

    static void StripCanvases(GameObject _root)
    {
        foreach (var gr in _root.GetComponentsInChildren<GraphicRaycaster>(true)) Object.DestroyImmediate(gr);
        foreach (var cs in _root.GetComponentsInChildren<CanvasScaler>(true)) Object.DestroyImmediate(cs);
        foreach (var c in _root.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(c);
    }

    static void Stretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = _rect.offsetMax = Vector2.zero;
    }

    // 지도 뷰포트와 같은 사각형(좌우 여백 MAP_SIDE_INSET, 하단 MAP_VIEWPORT_BOTTOM_OFFSET, 상단 MAP_VIEWPORT_TOP_OFFSET)에 맞추고 RectMask2D로 클립한다.
    static void FitToViewportRect(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = new Vector2(MAP_SIDE_INSET, MAP_VIEWPORT_BOTTOM_OFFSET);
        _rect.offsetMax = new Vector2(-MAP_SIDE_INSET, MAP_VIEWPORT_TOP_OFFSET);
        if (!_rect.GetComponent<RectMask2D>()) _rect.gameObject.AddComponent<RectMask2D>();
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

    static void SetObjectArray<T>(SerializedObject _so, string _field, T[] _values) where T : Object
    {
        var prop = _so.FindProperty(_field);
        prop.arraySize = _values.Length;
        for (int i = 0; i < _values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
        }
    }
}
