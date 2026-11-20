using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// S6: 마을 창 5종(TavernWindow, RestWindow, StationHallWindow, WorkshopWindow, CityHallWindow)과 TravelBanner 및 GameCanvas 배선.
public static class RelayoutS6Builder
{
    const string MENU = "Tools/MyJG/UI/Relayout/S6 Build Town Windows";
    const string RELAYOUT_DIR = "Assets/Art/UI/Relayout";
    const string TAVERN_PREFAB = RELAYOUT_DIR + "/TavernWindow.prefab";
    const string REST_PREFAB = RELAYOUT_DIR + "/RestWindow.prefab";
    const string STATION_HALL_PREFAB = RELAYOUT_DIR + "/StationHallWindow.prefab";
    const string WORKSHOP_PREFAB = RELAYOUT_DIR + "/WorkshopWindow.prefab";
    const string CITY_HALL_PREFAB = RELAYOUT_DIR + "/CityHallWindow.prefab";
    const string TRAVEL_BANNER_PREFAB = RELAYOUT_DIR + "/TravelBanner.prefab";
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
        var tavernPrefab = BuildTavernWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var restPrefab = BuildRestWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var stationHallPrefab = BuildStationHallWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var workshopPrefab = BuildWorkshopWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var cityHallPrefab = BuildCityHallWindowPrefab(log, palette, buttonTemplate, labelTemplate);
        var travelBannerPrefab = BuildTravelBannerPrefab(log, palette, buttonTemplate, labelTemplate);

        UpdateGameCanvas(log, tavernPrefab, restPrefab, stationHallPrefab, workshopPrefab, cityHallPrefab, travelBannerPrefab);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static GameObject BuildTavernWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var layoutRoot = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        var rootGo = CreateWindowShell("TavernWindow", "tavern", "술집", 1136f, 136f, 620f, 576f,
            _palette, _buttonTemplate, _labelTemplate, out var floatingWin, out var titleText);
        try
        {
            var rootRect = (RectTransform)rootGo.transform;

            // 상단 소문 바 (54px)
            var rumorBar = NewRect("RumorBar", rootRect);
            rumorBar.anchorMin = new Vector2(0f, 1f);
            rumorBar.anchorMax = new Vector2(1f, 1f);
            rumorBar.pivot = new Vector2(0.5f, 1f);
            rumorBar.anchoredPosition = new Vector2(0f, -38f); // 제목줄(30) 아래 8px
            rumorBar.sizeDelta = new Vector2(-16f, 54f);
            var rbImg = rumorBar.gameObject.AddComponent<Image>();
            rbImg.color = _palette.Panel2;
            BindPalette(rumorBar.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var rumorBtn = CreateButton(_buttonTemplate, rumorBar, "RumorButton", "소문 듣기", _palette, UIPalette.ColorRole.Panel);
            var rbRt = (RectTransform)rumorBtn.transform;
            rbRt.anchorMin = new Vector2(0f, 0.5f);
            rbRt.anchorMax = new Vector2(0f, 0.5f);
            rbRt.pivot = new Vector2(0f, 0.5f);
            rbRt.anchoredPosition = new Vector2(8f, 0f);
            rbRt.sizeDelta = new Vector2(110f, 36f);
            var rumorBtnLabel = rumorBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            var rumorText = CreateLabel(_labelTemplate, rumorBar, "RumorText", 13f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            rumorText.textWrappingMode = TextWrappingModes.Normal;
            rumorText.rectTransform.anchorMin = Vector2.zero;
            rumorText.rectTransform.anchorMax = Vector2.one;
            rumorText.rectTransform.offsetMin = new Vector2(128f, 4f);
            rumorText.rectTransform.offsetMax = new Vector2(-8f, -4f);

            // 중앙 9×5 의뢰 게시판 보드
            var marketInvView = FindDeep(layoutRoot.transform, "Market Inventory").GetComponent<PuzzleInventoryView>();
            var clonedBoard = Object.Instantiate(marketInvView.gameObject, rootRect, false);
            clonedBoard.name = "QuestBoardBody";
            clonedBoard.SetActive(true);
            var bodyRt = (RectTransform)clonedBoard.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            // 소문 줄(38~92) 아래 8px, 의뢰 정보 카드(8~132) 위 8px.
            bodyRt.offsetMin = new Vector2(12f, 140f);
            bodyRt.offsetMax = new Vector2(-12f, -100f);

            var invView = clonedBoard.GetComponent<PuzzleInventoryView>();
            PrepareClonedBoard(clonedBoard, invView, _palette, 504f, 320f, 40f, 52f);

            // 하단 의뢰 정보 카드(596×124): 게시판 화물에 마우스를 올리면 제목 · 네 칸(최단 이동·기한·보수·숙련) · 실패 조건을 보여 준다.
            // 올리지 않았을 때는 가운데 안내 문구만 보인다.
            var card = NewRect("QuestDetailCard", rootRect);
            SetAnchored(card, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2(0f, 8f);
            card.sizeDelta = new Vector2(-24f, 124f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = _palette.Panel2;
            cardImg.raycastTarget = false;
            BindPalette(card.gameObject, _palette, UIPalette.ColorRole.Panel2);

            var hintText = CreateLabel(_labelTemplate, card, "HintText", 13f, TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.TextSub);
            hintText.textWrappingMode = TextWrappingModes.Normal;
            hintText.rectTransform.anchorMin = Vector2.zero;
            hintText.rectTransform.anchorMax = Vector2.one;
            hintText.rectTransform.offsetMin = new Vector2(12f, 8f);
            hintText.rectTransform.offsetMax = new Vector2(-12f, -8f);

            var detailRoot = NewRect("Detail", card);
            detailRoot.anchorMin = Vector2.zero;
            detailRoot.anchorMax = Vector2.one;
            detailRoot.offsetMin = Vector2.zero;
            detailRoot.offsetMax = Vector2.zero;

            var detailTitle = CreateLabel(_labelTemplate, detailRoot, "DetailTitle", 16f, TextAlignmentOptions.MidlineLeft, _palette, UIPalette.ColorRole.TitleText);
            detailTitle.fontStyle = FontStyles.Bold;
            detailTitle.textWrappingMode = TextWrappingModes.NoWrap;
            detailTitle.overflowMode = TextOverflowModes.Ellipsis;
            SetAnchored(detailTitle.rectTransform, 0f, 1f, 1f, 1f, 12f, -10f, -24f, 24f);

            const float STAT_TOP = 40f, STAT_H = 44f, STAT_GAP = 8f, CARD_INNER = 572f;
            float statW = (CARD_INNER - STAT_GAP * 3f) / 4f;
            var statLabels = new TextMeshProUGUI[4];
            var statValues = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var cell = NewRect("Stat_" + i, detailRoot);
                SetAnchored(cell, 0f, 1f, 0f, 1f, 12f + i * (statW + STAT_GAP), -STAT_TOP, statW, STAT_H);
                var cellImg = cell.gameObject.AddComponent<Image>();
                cellImg.color = _palette.Panel;
                cellImg.raycastTarget = false;
                BindPalette(cell.gameObject, _palette, UIPalette.ColorRole.Panel);

                statLabels[i] = CreateLabel(_labelTemplate, cell, "Label", 11f, TextAlignmentOptions.MidlineLeft, _palette, UIPalette.ColorRole.TextSub);
                statLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                SetAnchored(statLabels[i].rectTransform, 0f, 1f, 1f, 1f, 8f, -4f, -16f, 16f);

                statValues[i] = CreateLabel(_labelTemplate, cell, "Value", 15f, TextAlignmentOptions.MidlineLeft, _palette, UIPalette.ColorRole.Text);
                statValues[i].fontStyle = FontStyles.Bold;
                statValues[i].textWrappingMode = TextWrappingModes.NoWrap;
                statValues[i].overflowMode = TextOverflowModes.Ellipsis;
                SetAnchored(statValues[i].rectTransform, 0f, 1f, 1f, 1f, 8f, -20f, -16f, 22f);
            }
            // 보수는 금색으로 강조한다.
            statValues[2].color = _palette.GoldAccent;
            BindPalette(statValues[2].gameObject, _palette, UIPalette.ColorRole.GoldAccent);

            var detailPenalty = CreateLabel(_labelTemplate, detailRoot, "DetailPenalty", 13f, TextAlignmentOptions.MidlineLeft, _palette, UIPalette.ColorRole.Text);
            detailPenalty.color = _palette.Negative;
            BindPalette(detailPenalty.gameObject, _palette, UIPalette.ColorRole.Negative);
            detailPenalty.textWrappingMode = TextWrappingModes.NoWrap;
            detailPenalty.overflowMode = TextOverflowModes.Ellipsis;
            SetAnchored(detailPenalty.rectTransform, 0f, 1f, 1f, 1f, 12f, -92f, -24f, 22f);
            detailRoot.gameObject.SetActive(false);

            var tv = rootGo.AddComponent<TavernWindowView>();
            var so = new SerializedObject(tv);
            so.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("rumorButton").objectReferenceValue = rumorBtn;
            so.FindProperty("rumorButtonLabel").objectReferenceValue = rumorBtnLabel;
            so.FindProperty("rumorText").objectReferenceValue = rumorText;
            so.FindProperty("questBoardView").objectReferenceValue = invView;
            so.FindProperty("hintText").objectReferenceValue = hintText;
            so.FindProperty("detailRoot").objectReferenceValue = detailRoot.gameObject;
            so.FindProperty("detailTitle").objectReferenceValue = detailTitle;
            so.FindProperty("detailPenalty").objectReferenceValue = detailPenalty;
            var labelsProp = so.FindProperty("detailStatLabels");
            var valuesProp = so.FindProperty("detailStatValues");
            labelsProp.arraySize = 4;
            valuesProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = statLabels[i];
                valuesProp.GetArrayElementAtIndex(i).objectReferenceValue = statValues[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, TAVERN_PREFAB);
            _log.AppendLine($"TavernWindow 프리팹 → {TAVERN_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(layoutRoot);
        }
    }

    static GameObject BuildRestWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = CreateWindowShell("RestWindow", "rest", "여관", 1136f, 136f, 440f, 300f,
            _palette, _buttonTemplate, _labelTemplate, out var floatingWin, out var titleText);
        try
        {
            var rootRect = (RectTransform)rootGo.transform;

            // 턴 조절 줄(-, 턴 표시, +)
            var earlierBtn = CreateButton(_buttonTemplate, rootRect, "EarlierButton", "-", _palette, UIPalette.ColorRole.Panel2);
            SetAnchored(earlierBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 20f, -42f, 44f, 38f);

            var turnLabel = CreateLabel(_labelTemplate, rootRect, "TurnLabel", 15f, TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
            SetAnchored(turnLabel.rectTransform, 0f, 1f, 0f, 1f, 72f, -42f, 296f, 38f);

            var laterBtn = CreateButton(_buttonTemplate, rootRect, "LaterButton", "+", _palette, UIPalette.ColorRole.Panel2);
            SetAnchored(laterBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 376f, -42f, 44f, 38f);

            // 실행 줄: 휴식과 아침까지 대기는 같은 크기·같은 버튼 스타일로 나란히 둔다.
            var restBtn = CreateButton(_buttonTemplate, rootRect, "RestButton", "휴식", _palette, UIPalette.ColorRole.Panel2);
            SetAnchored(restBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 20f, -92f, 192f, 40f);
            var restBtnLabel = restBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            var waitMorningBtn = CreateButton(_buttonTemplate, rootRect, "WaitMorningButton", "아침까지 대기", _palette, UIPalette.ColorRole.Panel2);
            SetAnchored(waitMorningBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 228f, -92f, 192f, 40f);
            var waitMorningLabel = waitMorningBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            var settlementText = CreateLabel(_labelTemplate, rootRect, "SettlementPreviewText", 14f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.GoldAccent);
            settlementText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(settlementText.rectTransform, 0f, 1f, 0f, 1f, 20f, -144f, 400f, 48f);

            // 도움말은 최대 4줄까지 들어가도록 높이를 넉넉히 둔다(창 하단에서 잘리지 않게).
            var statusText = CreateLabel(_labelTemplate, rootRect, "StatusText", 13f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(statusText.rectTransform, 0f, 1f, 0f, 1f, 20f, -200f, 400f, 80f);

            var rv = rootGo.AddComponent<RestWindowView>();
            var so = new SerializedObject(rv);
            so.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("turnLabel").objectReferenceValue = turnLabel;
            so.FindProperty("earlierButton").objectReferenceValue = earlierBtn;
            so.FindProperty("laterButton").objectReferenceValue = laterBtn;
            so.FindProperty("restButton").objectReferenceValue = restBtn;
            so.FindProperty("restButtonLabel").objectReferenceValue = restBtnLabel;
            so.FindProperty("waitMorningButton").objectReferenceValue = waitMorningBtn;
            so.FindProperty("waitMorningLabel").objectReferenceValue = waitMorningLabel;
            so.FindProperty("settlementPreviewText").objectReferenceValue = settlementText;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, REST_PREFAB);
            _log.AppendLine($"RestWindow 프리팹 → {REST_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static GameObject BuildStationHallWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var layoutRoot = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        var rootGo = CreateWindowShell("StationHallWindow", "station_hall", "역사", 1136f, 136f, 560f, 246f,
            _palette, _buttonTemplate, _labelTemplate, out var floatingWin, out var titleText);
        try
        {
            var rootRect = (RectTransform)rootGo.transform;
            var actionsView = layoutRoot.GetComponentInChildren<PuzzleStationActionsView>(true);
            var srcRowsProp = new SerializedObject(actionsView).FindProperty("wagonProductRows");
            // 상품 카드는 한 장씩 보여 주고 다음 버튼으로 넘긴다(페이지당 1개). 빈 슬롯이 남지 않도록 1개만 복제한다.
            int rowCount = srcRowsProp != null ? Mathf.Min(srcRowsProp.arraySize, 1) : 0;

            var dragPreviewGo = new GameObject("DragPreview", typeof(RectTransform), typeof(Image));
            dragPreviewGo.layer = 5;
            var dragPreviewRt = (RectTransform)dragPreviewGo.transform;
            dragPreviewRt.SetParent(rootRect, false);
            dragPreviewRt.sizeDelta = new Vector2(88f, 64f);
            var dragPreviewImg = dragPreviewGo.GetComponent<Image>();
            dragPreviewImg.raycastTarget = false;
            dragPreviewImg.preserveAspect = true;
            dragPreviewGo.SetActive(false);

            var clonedRows = new PuzzleWagonProductRow[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                var srcRow = srcRowsProp.GetArrayElementAtIndex(i).objectReferenceValue as PuzzleWagonProductRow;
                if (!srcRow) continue;
                var rowGo = Object.Instantiate(srcRow.gameObject, rootRect, false);
                rowGo.name = $"WagonProductRow_{i + 1}";
                var rt = (RectTransform)rowGo.transform;
                // 화차 카드: 제목줄(30) 아래 8px, 좌우 16px. 아래 판매·출발 줄과 8px.
                SetAnchored(rt, 0f, 1f, 0f, 1f, 16f, -38f - i * 128f, 528f, 120f);
                var rowComp = rowGo.GetComponent<PuzzleWagonProductRow>();
                var rSo = new SerializedObject(rowComp);
                rSo.FindProperty("dragPreview").objectReferenceValue = dragPreviewImg;
                rSo.FindProperty("dragPreviewBox").vector2Value = new Vector2(88f, 64f);
                rSo.FindProperty("dragPreviewOffset").vector2Value = Vector2.zero;
                // 카드 내부 배치: 왼쪽 그림, 오른쪽 이름·크기·가격(label)과 칸수 칩을 한 덩어리로 둔다.
                if (rSo.FindProperty("artwork").objectReferenceValue is Image art)
                {
                    // 그림은 90도 돌려 맞추는 상자(QuarterTurnFitter) 안에 있어 그림 자체 위치는 상자 가운데로 되돌아간다. 상자를 옮긴다.
                    var artBox = art.rectTransform.parent as RectTransform;
                    var artTarget = artBox && artBox.GetComponent<QuarterTurnFitter>() ? artBox : art.rectTransform;
                    SetAnchored(artTarget, 0f, 1f, 0f, 1f, 12f, -12f, 176f, 96f);
                }
                // 오른쪽 열(x 204, 폭 312): 이름·크기·가격(2줄) → 실을 수 있는 칸 칩 → 끌어 구매 안내.
                if (rSo.FindProperty("label").objectReferenceValue is TextMeshProUGUI cardLabel)
                {
                    SetAnchored(cardLabel.rectTransform, 0f, 1f, 0f, 1f, 204f, -12f, 312f, 52f);
                    cardLabel.enableAutoSizing = false;
                    cardLabel.fontSize = 14f;
                    cardLabel.richText = true;
                    cardLabel.alignment = TextAlignmentOptions.TopLeft;
                    cardLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    cardLabel.overflowMode = TextOverflowModes.Ellipsis;
                    cardLabel.color = _palette.Text;
                }
                var dragHint = CreateLabel(_labelTemplate, rt, "DragHint", 12f, TextAlignmentOptions.MidlineLeft, _palette, UIPalette.ColorRole.TextSub);
                dragHint.text = Presentation.WagonProductDragHint;
                dragHint.textWrappingMode = TextWrappingModes.NoWrap;
                SetAnchored(dragHint.rectTransform, 0f, 1f, 0f, 1f, 204f, -92f, 312f, 18f);
                if (rSo.FindProperty("chipRoot").objectReferenceValue is GameObject chip && chip.transform is RectTransform chipRt)
                {
                    SetAnchored(chipRt, 0f, 1f, 0f, 1f, 204f, -64f, 72f, 24f);
                    // 칩 글자 상자는 옛 카드 크기 그대로라 칩보다 크다. 칩 칸에 맞춘다.
                    foreach (var chipText in chipRt.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        RelayoutWindowKit.Stretch(chipText.rectTransform);
                        chipText.alignment = TextAlignmentOptions.Center;
                        chipText.enableAutoSizing = false;
                        chipText.fontSize = Mathf.Min(chipText.fontSize, 14f);
                        chipText.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                }
                if (rSo.FindProperty("selectionHighlight").objectReferenceValue is Image sel)
                {
                    sel.rectTransform.anchorMin = Vector2.zero;
                    sel.rectTransform.anchorMax = Vector2.one;
                    sel.rectTransform.offsetMin = Vector2.zero;
                    sel.rectTransform.offsetMax = Vector2.zero;
                }
                rSo.ApplyModifiedPropertiesWithoutUndo();
                clonedRows[i] = rowComp;
            }
            dragPreviewRt.SetAsLastSibling();

            // 페이저 줄: 카드 바로 아래, 페이지 표시 왼쪽 · 다음 버튼 오른쪽
            // 넘김(상품이 여러 장일 때만 보임)은 제목줄 오른쪽, 닫기 버튼 왼쪽에 둔다. 본문에 빈 줄이 남지 않는다.
            var titleBar = (RectTransform)titleText.transform.parent;
            var nextPageBtn = CreateButton(_buttonTemplate, titleBar, "NextPageButton", "다음", _palette, UIPalette.ColorRole.Panel);
            SetAnchored(nextPageBtn.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, -88f, 11f, 56f, 22f);
            var pageLabel = CreateLabel(_labelTemplate, titleBar, "PageLabel", 12f, TextAlignmentOptions.MidlineRight, _palette, UIPalette.ColorRole.TextSub);
            SetAnchored(pageLabel.rectTransform, 1f, 0.5f, 1f, 0.5f, -156f, 11f, 64f, 22f);
            RelayoutWindowKit.ReserveTitleRight(titleText, 164f);

            // 판매 드롭 구역과 출발 버튼: 같은 줄, 같은 높이
            var sellZone = NewRect("SellDropZone", rootRect);
            SetAnchored(sellZone, 0f, 1f, 0f, 1f, 16f, -166f, 360f, 72f);
            var sellImg = sellZone.gameObject.AddComponent<Image>();
            sellImg.color = _palette.Panel2;
            BindPalette(sellZone.gameObject, _palette, UIPalette.ColorRole.Panel2);
            var sellOutline = sellZone.gameObject.AddComponent<Outline>();
            sellOutline.effectColor = _palette.BorderBright;
            sellOutline.effectDistance = new Vector2(1f, -1f);

            var sellLabel = CreateLabel(_labelTemplate, sellZone, "SellZoneLabel", 13f, TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
            sellLabel.textWrappingMode = TextWrappingModes.Normal;
            sellLabel.rectTransform.anchorMin = Vector2.zero;
            sellLabel.rectTransform.anchorMax = Vector2.one;
            sellLabel.rectTransform.offsetMin = new Vector2(8f, 4f);
            sellLabel.rectTransform.offsetMax = new Vector2(-8f, -4f);

            var departBtn = CreateButton(_buttonTemplate, rootRect, "DepartButton", "출발 (지도)", _palette, UIPalette.ColorRole.GoldAccent);
            SetAnchored(departBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 392f, -166f, 152f, 72f);
            var departLabel = departBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            // 하단 중복 안내(판매 문구 반복)는 만들지 않는다. statusText는 null로 두면 뷰가 쓰지 않는다.

            var sh = rootGo.AddComponent<StationHallWindowView>();
            var so = new SerializedObject(sh);
            so.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            var rowsProp = so.FindProperty("productRows");
            rowsProp.arraySize = clonedRows.Length;
            for (int i = 0; i < clonedRows.Length; i++)
            {
                rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = clonedRows[i];
            }
            so.FindProperty("nextPageButton").objectReferenceValue = nextPageBtn;
            so.FindProperty("pageLabel").objectReferenceValue = pageLabel;
            so.FindProperty("sellDropZone").objectReferenceValue = sellZone;
            so.FindProperty("sellZoneLabel").objectReferenceValue = sellLabel;
            so.FindProperty("departButton").objectReferenceValue = departBtn;
            so.FindProperty("departButtonLabel").objectReferenceValue = departLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, STATION_HALL_PREFAB);
            _log.AppendLine($"StationHallWindow 프리팹 → {STATION_HALL_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(layoutRoot);
        }
    }

    static GameObject BuildWorkshopWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var canvasRoot = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);
        var rootGo = CreateWindowShell("WorkshopWindow", "workshop", "생산시설", 1136f, 136f, 560f, 480f,
            _palette, _buttonTemplate, _labelTemplate, out var floatingWin, out var titleText);
        try
        {
            var rootRect = (RectTransform)rootGo.transform;

            var descText = CreateLabel(_labelTemplate, rootRect, "DescriptionText", 13f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            descText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(descText.rectTransform, 0f, 1f, 0f, 1f, 16f, -38f, 528f, 40f);

            var invPresenter = canvasRoot.GetComponentInChildren<PuzzleInventoryPresenter>(true);
            var ipSo = new SerializedObject(invPresenter);

            var butcheryBoard = CloneWorkshopBoard(rootRect, "ButcherySection", ipSo.FindProperty("butcheryView").objectReferenceValue as PuzzleInventoryView, _palette, out var butcherySec);
            var modBoard = CloneWorkshopBoard(rootRect, "ModificationSection", ipSo.FindProperty("modificationView").objectReferenceValue as PuzzleInventoryView, _palette, out var modSec);
            var refBoard = CloneWorkshopBoard(rootRect, "RefiningSection", ipSo.FindProperty("refiningView").objectReferenceValue as PuzzleInventoryView, _palette, out var refSec);

            var hintText = CreateLabel(_labelTemplate, rootRect, "HintText", 13f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.GoldAccent);
            hintText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(hintText.rectTransform, 0f, 1f, 0f, 1f, 16f, -366f, 528f, 44f);

            var modeBtn = CreateButton(_buttonTemplate, rootRect, "ModeButton", "모드 전환", _palette, UIPalette.ColorRole.Panel2);
            SetAnchored(modeBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 16f, -420f, 168f, 42f);
            var modeLabel = modeBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            var confirmBtn = CreateButton(_buttonTemplate, rootRect, "ConfirmButton", "작업 실행", _palette, UIPalette.ColorRole.GoldAccent);
            SetAnchored(confirmBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 200f, -420f, 344f, 42f);
            var confirmLabel = confirmBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            var ww = rootGo.AddComponent<WorkshopWindowView>();
            var so = new SerializedObject(ww);
            so.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("descriptionText").objectReferenceValue = descText;
            so.FindProperty("hintText").objectReferenceValue = hintText;
            so.FindProperty("butcherySection").objectReferenceValue = butcherySec;
            so.FindProperty("modificationSection").objectReferenceValue = modSec;
            so.FindProperty("refiningSection").objectReferenceValue = refSec;
            so.FindProperty("butcheryBoardView").objectReferenceValue = butcheryBoard;
            so.FindProperty("modificationBoardView").objectReferenceValue = modBoard;
            so.FindProperty("refiningBoardView").objectReferenceValue = refBoard;
            so.FindProperty("modeButton").objectReferenceValue = modeBtn;
            so.FindProperty("modeButtonLabel").objectReferenceValue = modeLabel;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("confirmButtonLabel").objectReferenceValue = confirmLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, WORKSHOP_PREFAB);
            _log.AppendLine($"WorkshopWindow 프리팹 → {WORKSHOP_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(canvasRoot);
        }
    }

    static PuzzleInventoryView CloneWorkshopBoard(
        RectTransform _parent,
        string _sectionName,
        PuzzleInventoryView _srcView,
        UIPalette _palette,
        out GameObject _sectionGo)
    {
        var secRt = NewRect(_sectionName, _parent);
        SetAnchored(secRt, 0f, 1f, 0f, 1f, 16f, -84f, 528f, 272f);
        _sectionGo = secRt.gameObject;

        var cloned = Object.Instantiate(_srcView.gameObject, secRt, false);
        cloned.name = "Board";
        cloned.SetActive(true);
        var rt = (RectTransform)cloned.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var invView = cloned.GetComponent<PuzzleInventoryView>();
        PrepareClonedBoard(cloned, invView, _palette, 520f, 264f, 36f, 56f);
        return invView;
    }

    static GameObject BuildCityHallWindowPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var layoutRoot = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        var rootGo = CreateWindowShell("CityHallWindow", "city_hall", "시청", 1136f, 136f, 520f, 400f,
            _palette, _buttonTemplate, _labelTemplate, out var floatingWin, out var titleText);
        try
        {
            var rootRect = (RectTransform)rootGo.transform;

            var priceIndexText = CreateLabel(_labelTemplate, rootRect, "PriceIndexText", 14f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.GoldAccent);
            SetAnchored(priceIndexText.rectTransform, 0f, 1f, 0f, 1f, 16f, -42f, 488f, 28f);

            var marketInvView = FindDeep(layoutRoot.transform, "Market Inventory").GetComponent<PuzzleInventoryView>();
            var srcTradePrefs = new SerializedObject(marketInvView).FindProperty("boardTradePreferences").objectReferenceValue as CityTradePreferencesView;

            CityTradePreferencesView clonedPrefs = null;
            if (srcTradePrefs)
            {
                var tpGo = Object.Instantiate(srcTradePrefs.gameObject, rootRect, false);
                tpGo.name = "TradePreferences";
                var tpRt = (RectTransform)tpGo.transform;
                // 한 줄에 하나씩 전체 폭으로 둔다. 두 열로 나누면 품목 표가 많을 때 칸을 넘친다.
                SetAnchored(tpRt, 0f, 1f, 0f, 1f, 16f, -78f, 488f, 120f);
                FitTradePrefRow(tpRt, "RegionPrice", 0f, 0f, 488f);
                FitTradePrefRow(tpRt, "ExpensiveRow", 0f, -24f, 488f);
                FitTradePrefRow(tpRt, "WasteRow", 0f, -48f, 488f);
                FitTradePrefRow(tpRt, "CommonRow", 0f, -72f, 488f);
                FitTradePrefRow(tpRt, "RareRow", 0f, -96f, 488f);
                // 쌓는 순서는 형제 순서를 따른다: 물가 · 선호 · 풍부 · 희귀 · 기피.
                string[] rowOrder = { "RegionPrice", "ExpensiveRow", "CommonRow", "RareRow", "WasteRow" };
                for (int r = 0; r < rowOrder.Length; r++)
                {
                    var row = tpRt.Find(rowOrder[r]);
                    if (row) row.SetSiblingIndex(r);
                }
                // 도시에 해당 품목이 없어 숨겨진 줄이 빈칸으로 남지 않도록 위에서부터 차례로 쌓는다.
                var vLayout = tpGo.GetComponent<VerticalLayoutGroup>() ?? tpGo.AddComponent<VerticalLayoutGroup>();
                vLayout.spacing = 2f;
                vLayout.padding = new RectOffset(0, 0, 0, 0);
                vLayout.childAlignment = TextAnchor.UpperLeft;
                vLayout.childControlWidth = true;
                vLayout.childControlHeight = false;
                vLayout.childForceExpandWidth = true;
                vLayout.childForceExpandHeight = false;
                clonedPrefs = tpGo.GetComponent<CityTradePreferencesView>();
            }

            var industryText = CreateLabel(_labelTemplate, rootRect, "IndustryText", 14f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            industryText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(industryText.rectTransform, 0f, 1f, 0f, 1f, 16f, -210f, 488f, 44f);

            var reputationText = CreateLabel(_labelTemplate, rootRect, "ReputationText", 14f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            reputationText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(reputationText.rectTransform, 0f, 1f, 0f, 1f, 16f, -264f, 488f, 44f);

            var settlementText = CreateLabel(_labelTemplate, rootRect, "SettlementText", 14f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.GoldAccent);
            settlementText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(settlementText.rectTransform, 0f, 1f, 0f, 1f, 16f, -318f, 488f, 60f);

            var ch = rootGo.AddComponent<CityHallWindowView>();
            var so = new SerializedObject(ch);
            so.FindProperty("floatingWindow").objectReferenceValue = floatingWin;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("priceIndexText").objectReferenceValue = priceIndexText;
            so.FindProperty("tradePreferencesView").objectReferenceValue = clonedPrefs;
            so.FindProperty("industryText").objectReferenceValue = industryText;
            so.FindProperty("reputationText").objectReferenceValue = reputationText;
            so.FindProperty("settlementText").objectReferenceValue = settlementText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, CITY_HALL_PREFAB);
            _log.AppendLine($"CityHallWindow 프리팹 → {CITY_HALL_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(layoutRoot);
        }
    }

    static GameObject BuildTravelBannerPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = new GameObject("TravelBanner", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -92f);
            rootRect.sizeDelta = new Vector2(640f, 148f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = _palette.Panel;
            BindPalette(rootGo, _palette, UIPalette.ColorRole.Panel);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.GoldAccent;
            outline.effectDistance = new Vector2(2f, -2f);

            // 확인 그룹
            var confirmGroup = NewRect("Confirmation", rootRect);
            confirmGroup.anchorMin = Vector2.zero;
            confirmGroup.anchorMax = Vector2.one;
            confirmGroup.offsetMin = Vector2.zero;
            confirmGroup.offsetMax = Vector2.zero;

            var qText = CreateLabel(_labelTemplate, confirmGroup, "QuestionText", 15f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TitleText);
            qText.fontStyle = FontStyles.Bold;
            SetAnchored(qText.rectTransform, 0f, 1f, 0f, 1f, 16f, -12f, 608f, 26f);

            var quoteText = CreateLabel(_labelTemplate, confirmGroup, "QuoteText", 14f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            quoteText.textWrappingMode = TextWrappingModes.Normal;
            SetAnchored(quoteText.rectTransform, 0f, 1f, 0f, 1f, 16f, -42f, 608f, 44f);

            var confirmBtn = CreateButton(_buttonTemplate, confirmGroup, "ConfirmButton", "출발", _palette, UIPalette.ColorRole.GoldAccent);
            SetAnchored(confirmBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 380f, -96f, 116f, 38f);
            var confirmLbl = confirmBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            var cancelBtn = CreateButton(_buttonTemplate, confirmGroup, "CancelButton", "취소", _palette, UIPalette.ColorRole.Panel2);
            SetAnchored(cancelBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 508f, -96f, 116f, 38f);
            var cancelLbl = cancelBtn.GetComponentInChildren<TextMeshProUGUI>(true);

            // 이동 진행 그룹
            var journeyGroup = NewRect("Journey", rootRect);
            journeyGroup.anchorMin = Vector2.zero;
            journeyGroup.anchorMax = Vector2.one;
            journeyGroup.offsetMin = Vector2.zero;
            journeyGroup.offsetMax = Vector2.zero;

            var jTitle = CreateLabel(_labelTemplate, journeyGroup, "JourneyTitle", 15f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TitleText);
            jTitle.fontStyle = FontStyles.Bold;
            SetAnchored(jTitle.rectTransform, 0f, 1f, 0f, 1f, 16f, -12f, 340f, 26f);

            var jRoute = CreateLabel(_labelTemplate, journeyGroup, "JourneyRoute", 13f, TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.Text);
            SetAnchored(jRoute.rectTransform, 0f, 1f, 0f, 1f, 280f, -12f, 344f, 26f);

            var trackLine = NewRect("TrackLine", journeyGroup);
            SetAnchored(trackLine, 0f, 1f, 0f, 1f, 48f, -68f, 544f, 4f);
            var tlImg = trackLine.gameObject.AddComponent<Image>();
            tlImg.color = _palette.BorderBright;

            var depAnchor = NewRect("DepartureAnchor", journeyGroup);
            SetAnchored(depAnchor, 0f, 1f, 0f, 1f, 48f, -68f, 12f, 12f);

            var dstAnchor = NewRect("DestinationAnchor", journeyGroup);
            SetAnchored(dstAnchor, 0f, 1f, 0f, 1f, 580f, -68f, 12f, 12f);

            var trainIcon = NewRect("TrainIcon", journeyGroup);
            SetAnchored(trainIcon, 0f, 1f, 0f, 1f, 48f, -58f, 24f, 24f);
            var tiImg = trainIcon.gameObject.AddComponent<Image>();
            tiImg.color = _palette.GoldAccent;

            var depName = CreateLabel(_labelTemplate, journeyGroup, "DepartureName", 13f, TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            SetAnchored(depName.rectTransform, 0f, 1f, 0f, 1f, 16f, -92f, 180f, 24f);

            var progressLbl = CreateLabel(_labelTemplate, journeyGroup, "ProgressLabel", 14f, TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.GoldAccent);
            SetAnchored(progressLbl.rectTransform, 0f, 1f, 0f, 1f, 200f, -92f, 240f, 24f);

            var dstName = CreateLabel(_labelTemplate, journeyGroup, "DestinationName", 13f, TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.TextSub);
            SetAnchored(dstName.rectTransform, 0f, 1f, 0f, 1f, 444f, -92f, 180f, 24f);

            var tb = rootGo.AddComponent<TravelBannerView>();
            var so = new SerializedObject(tb);
            so.FindProperty("confirmation").objectReferenceValue = confirmGroup.gameObject;
            so.FindProperty("journey").objectReferenceValue = journeyGroup.gameObject;
            so.FindProperty("questionText").objectReferenceValue = qText;
            so.FindProperty("quoteText").objectReferenceValue = quoteText;
            so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("confirmLabel").objectReferenceValue = confirmLbl;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("cancelLabel").objectReferenceValue = cancelLbl;
            so.FindProperty("journeyTitleText").objectReferenceValue = jTitle;
            so.FindProperty("journeyRouteText").objectReferenceValue = jRoute;
            so.FindProperty("progressLabel").objectReferenceValue = progressLbl;
            so.FindProperty("trainIcon").objectReferenceValue = trainIcon;
            so.FindProperty("departureAnchor").objectReferenceValue = depAnchor;
            so.FindProperty("destinationAnchor").objectReferenceValue = dstAnchor;
            so.FindProperty("departureNameText").objectReferenceValue = depName;
            so.FindProperty("destinationNameText").objectReferenceValue = dstName;
            so.ApplyModifiedPropertiesWithoutUndo();

            rootGo.SetActive(false);
            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, TRAVEL_BANNER_PREFAB);
            _log.AppendLine($"TravelBanner 프리팹 → {TRAVEL_BANNER_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static void UpdateGameCanvas(
        StringBuilder _log,
        GameObject _tavernPrefab,
        GameObject _restPrefab,
        GameObject _stationHallPrefab,
        GameObject _workshopPrefab,
        GameObject _cityHallPrefab,
        GameObject _travelBannerPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);
        try
        {
            var floatingCanvas = root.transform.Find("FloatingWindowCanvas");
            if (!floatingCanvas)
            {
                throw new System.InvalidOperationException("FloatingWindowCanvas를 찾을 수 없습니다. S2 빌더를 먼저 실행하세요.");
            }

            var tavernInst = ReplaceInstance(floatingCanvas, "TavernWindow", _tavernPrefab);
            var restInst = ReplaceInstance(floatingCanvas, "RestWindow", _restPrefab);
            var stationHallInst = ReplaceInstance(floatingCanvas, "StationHallWindow", _stationHallPrefab);
            var workshopInst = ReplaceInstance(floatingCanvas, "WorkshopWindow", _workshopPrefab);
            var cityHallInst = ReplaceInstance(floatingCanvas, "CityHallWindow", _cityHallPrefab);
            var travelBannerInst = ReplaceInstance(floatingCanvas, "TravelBanner", _travelBannerPrefab);

            var tavernView = tavernInst.GetComponent<TavernWindowView>();
            var restView = restInst.GetComponent<RestWindowView>();
            var stationHallView = stationHallInst.GetComponent<StationHallWindowView>();
            var workshopView = workshopInst.GetComponent<WorkshopWindowView>();
            var cityHallView = cityHallInst.GetComponent<CityHallWindowView>();
            var travelBannerView = travelBannerInst.GetComponent<TravelBannerView>();

            var relayoutCtrl = root.GetComponentInChildren<RelayoutRootController>(true);
            if (relayoutCtrl)
            {
                var rcSo = new SerializedObject(relayoutCtrl);
                rcSo.FindProperty("tavernWindow").objectReferenceValue = tavernView;
                rcSo.FindProperty("restWindow").objectReferenceValue = restView;
                rcSo.FindProperty("stationHallWindow").objectReferenceValue = stationHallView;
                rcSo.FindProperty("workshopWindow").objectReferenceValue = workshopView;
                rcSo.FindProperty("cityHallWindow").objectReferenceValue = cityHallView;
                rcSo.FindProperty("travelBanner").objectReferenceValue = travelBannerView;
                rcSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var invPresenter = root.GetComponentInChildren<PuzzleInventoryPresenter>(true);
            if (invPresenter)
            {
                var ipSo = new SerializedObject(invPresenter);
                ipSo.FindProperty("tavernWindowView").objectReferenceValue = tavernView;
                ipSo.FindProperty("stationHallWindowView").objectReferenceValue = stationHallView;
                ipSo.FindProperty("workshopWindowView").objectReferenceValue = workshopView;
                ipSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var mapPresenters = root.GetComponentsInChildren<PuzzleMapPresenter>(true);
            for (int i = 0; i < mapPresenters.Length; i++)
            {
                if (mapPresenters[i].IsEmbeddedInNotebook)
                {
                    var mpSo = new SerializedObject(mapPresenters[i]);
                    mpSo.FindProperty("travelBannerView").objectReferenceValue = travelBannerView;
                    mpSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB);
            _log.AppendLine($"GameCanvas 업데이트 (마을 창 5종 + TravelBanner 배선) → {GAME_CANVAS_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static GameObject ReplaceInstance(Transform _parent, string _name, GameObject _prefab)
    {
        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            var c = _parent.GetChild(i);
            if (c.name == _name)
            {
                Object.DestroyImmediate(c.gameObject);
            }
        }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, _parent);
        inst.name = _name;
        return inst;
    }

    static void PrepareClonedBoard(
        GameObject _clonedBoard,
        PuzzleInventoryView _invView,
        UIPalette _palette,
        float _contentW,
        float _contentH,
        float _minCell,
        float _maxCell)
    {
        HideNamedChild(_clonedBoard.transform, "BoardTitle");
        HideNamedChild(_clonedBoard.transform, "BoardValue");
        HideNamedChild(_clonedBoard.transform, "MoveGuide");
        HideNamedChild(_clonedBoard.transform, "BoardInfo");
        HideNamedChild(_clonedBoard.transform, "MarketInfoPanel");
        HideNamedChild(_clonedBoard.transform, "HeaderDivider");

        var invSo = new SerializedObject(_invView);
        invSo.FindProperty("boardTradePreferences").objectReferenceValue = null;

        var boardImg = invSo.FindProperty("board").objectReferenceValue as Image;
        var boardRt = boardImg.rectTransform;
        boardImg.enabled = false;

        var viewport = NewRect("BoardViewport", (RectTransform)_clonedBoard.transform);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("BoardContent", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(_contentW, _contentH);

        while (boardRt.childCount > 0)
        {
            boardRt.GetChild(0).SetParent(content, false);
        }

        // 클론한 보드 내의 불필요한 구형 헤더/배경/버튼 자식을 모두 숨긴다.
        var clonedImg = _clonedBoard.GetComponent<Image>();
        if (clonedImg) clonedImg.enabled = false;
        for (int i = 0; i < _clonedBoard.transform.childCount; i++)
        {
            var ch = _clonedBoard.transform.GetChild(i);
            if (ch == viewport || ch.name.Contains("DragBeam") || ch.name.Contains("Candidate") || ch.name.Contains("Ghost"))
            {
                continue;
            }
            ch.gameObject.SetActive(false);
        }

        invSo.FindProperty("dynamicCellSizing").boolValue = true;
        invSo.FindProperty("boardViewport").objectReferenceValue = viewport;
        invSo.FindProperty("boardContent").objectReferenceValue = content;
        invSo.FindProperty("minDynamicCellSize").floatValue = _minCell;
        invSo.FindProperty("maxDynamicCellSize").floatValue = _maxCell;

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
    }

    static GameObject CreateWindowShell(
        string _name,
        string _windowId,
        string _title,
        float _x,
        float _y,
        float _w,
        float _h,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate,
        out FloatingWindow _floatingWin,
        out TextMeshProUGUI _titleText)
    {
        // 공용 창 틀(FloatingWindowFrame)의 변형으로 만든다. 바탕·테두리·제목줄·닫기는 틀이 가진다.
        var rootGo = RelayoutWindowKit.CreateWindow(_name, _windowId, _title, _x, _y, _w, _h, false,
            out _floatingWin, out _, out _titleText, out _);
        rootGo.layer = 5;
        return rootGo;
    }

    static void FindTemplates(out Button _buttonTemplate, out TextMeshProUGUI _labelTemplate)
    {
        var layout = PrefabUtility.LoadPrefabContents(GAMEPLAY_LAYOUT_PREFAB);
        try
        {
            _buttonTemplate = layout.GetComponentInChildren<Button>(true);
            _labelTemplate = layout.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(layout);
        }
    }

    static void HideNamedChild(Transform _root, string _name)
    {
        var t = FindDeep(_root, _name);
        if (t) t.gameObject.SetActive(false);
    }

    static Transform FindDeep(Transform _root, string _name)
    {
        if (_root.name == _name) return _root;
        for (int i = 0; i < _root.childCount; i++)
        {
            var found = FindDeep(_root.GetChild(i), _name);
            if (found) return found;
        }
        return null;
    }

    static RectTransform NewRect(string _name, RectTransform _parent)
    {
        var go = new GameObject(_name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_parent, false);
        return rt;
    }

    static void SetTopLeftRect(RectTransform _rt, float _x, float _y, float _w, float _h)
    {
        _rt.anchorMin = new Vector2(0f, 1f);
        _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot = new Vector2(0f, 1f);
        _rt.anchoredPosition = new Vector2(_x, -_y);
        _rt.sizeDelta = new Vector2(_w, _h);
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

    static void SetAnchored(RectTransform _rt, float _minX, float _minY, float _maxX, float _maxY, float _x, float _y, float _w, float _h)
    {
        _rt.anchorMin = new Vector2(_minX, _minY);
        _rt.anchorMax = new Vector2(_maxX, _maxY);
        _rt.pivot = new Vector2(0f, 1f);
        _rt.anchoredPosition = new Vector2(_x, _y);
        _rt.sizeDelta = new Vector2(_w, _h);
    }

    static TextMeshProUGUI CreateLabel(
        TextMeshProUGUI _template,
        RectTransform _parent,
        string _name,
        float _fontSize,
        TextAlignmentOptions _align,
        UIPalette _palette,
        UIPalette.ColorRole _role)
    {
        var go = new GameObject(_name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = _template ? _template.font : null;
        tmp.fontSize = _fontSize;
        tmp.alignment = _align;
        tmp.color = ResolveColor(_palette, _role);
        tmp.raycastTarget = false;
        BindPalette(go, _palette, _role);
        return tmp;
    }

    static Button CreateButton(
        Button _template,
        RectTransform _parent,
        string _name,
        string _text,
        UIPalette _palette,
        UIPalette.ColorRole _bgRole)
    {
        var go = new GameObject(_name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_parent, false);
        var img = go.AddComponent<Image>();
        img.color = ResolveColor(_palette, _bgRole);
        BindPalette(go, _palette, _bgRole);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        // 비활성 색을 불투명하게 둔다. 기본값은 반투명이라 비활성 버튼(예: 낮의 '아침까지 대기')이 바탕 없이 글자만 보인다.
        var colors = btn.colors;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;

        var lbl = CreateLabel(_template ? _template.GetComponentInChildren<TextMeshProUGUI>(true) : null,
            rt, "Label", 14f, TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        lbl.text = _text;
        lbl.rectTransform.anchorMin = Vector2.zero;
        lbl.rectTransform.anchorMax = Vector2.one;
        lbl.rectTransform.offsetMin = Vector2.zero;
        lbl.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }

    static Color ResolveColor(UIPalette _palette, UIPalette.ColorRole _role)
    {
        return _role switch
        {
            UIPalette.ColorRole.Panel => _palette.Panel,
            UIPalette.ColorRole.Panel2 => _palette.Panel2,
            UIPalette.ColorRole.TitleText => _palette.TitleText,
            UIPalette.ColorRole.TextSub => _palette.TextSub,
            UIPalette.ColorRole.GoldAccent => _palette.GoldAccent,
            UIPalette.ColorRole.Border => _palette.Border,
            UIPalette.ColorRole.BorderBright => _palette.BorderBright,
            _ => _palette.Text
        };
    }

    static void BindPalette(GameObject _go, UIPalette _palette, UIPalette.ColorRole _role)
    {
        var binder = _go.GetComponent<UIPaletteBinder>() ?? _go.AddComponent<UIPaletteBinder>();
        var so = new SerializedObject(binder);
        so.FindProperty("palette").objectReferenceValue = _palette;
        so.FindProperty("role").enumValueIndex = (int)_role;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
