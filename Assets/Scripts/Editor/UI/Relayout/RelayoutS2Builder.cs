using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// S2: 고정 프레임(TopHUD, QuickToolbar, StatusBanner, PlatformScene, TownScene), R21 전환 스위치,
// 일시정지 개발용 토글을 프리팹에 멱등적으로 구성한다.
public static class RelayoutS2Builder
{
    const string MENU_BUILD = "Tools/MyJG/UI/Relayout/S2 Build Fixed Frame";
    const string MENU_TOGGLE = "Tools/MyJG/UI/Relayout/Toggle Relayout";

    const string PALETTE_PATH = "Assets/Resources/UI/UIPalette.asset";
    const string RELAYOUT_DIR = "Assets/Art/UI/Relayout";
    const string TOP_HUD_PREFAB = "Assets/Art/UI/Relayout/TopHUD.prefab";
    const string TOOLBAR_PREFAB = "Assets/Art/UI/Relayout/QuickToolbar.prefab";
    const string BANNER_PREFAB = "Assets/Art/UI/Relayout/StatusBanner.prefab";
    const string PLATFORM_PREFAB = "Assets/Art/UI/Relayout/PlatformScene.prefab";
    const string TOWN_PREFAB = "Assets/Art/UI/Relayout/TownScene.prefab";

    const string PAUSE_PREFAB = "Assets/Art/UI/Scene/PauseCanvas.prefab";
    const string CITY_NAV_PREFAB = "Assets/Art/UI/City Navigation.prefab";
    const string GAME_CANVAS_PREFAB = "Assets/Art/UI/Scene/GameCanvas.prefab";

    const string RELAYOUT_TOGGLE_BTN = "RelayoutToggleButton";
    const string WORLD_CANVAS_NAME = "WorldSceneCanvas";
    const string FIXED_CANVAS_NAME = "FixedFrameCanvas";

    [MenuItem(MENU_TOGGLE)]
    static void ToggleRelayoutMenu()
    {
        bool next = !RelayoutSettings.Enabled;
        RelayoutSettings.Enabled = next;
        Menu.SetChecked(MENU_TOGGLE, next);
        Debug.Log($"RelayoutSettings.Enabled = {next}");
    }

    [MenuItem(MENU_TOGGLE, true)]
    static bool ToggleRelayoutMenuValidate()
    {
        Menu.SetChecked(MENU_TOGGLE, RelayoutSettings.Enabled);
        return true;
    }

    [MenuItem(MENU_BUILD)]
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

        BuildPauseRelayoutToggle(log);

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

            var topHudPrefab = BuildTopHudPrefab(log, palette, buttonTemplate, labelTemplate);
            var toolbarPrefab = BuildQuickToolbarPrefab(log, palette, buttonTemplate, labelTemplate);
            var bannerPrefab = BuildStatusBannerPrefab(log, palette, labelTemplate);
            var platformPrefab = BuildPlatformScenePrefab(log, palette);
            var townPrefab = BuildTownScenePrefab(log, palette, labelTemplate);

            UpdateGameCanvas(log, topHudPrefab, toolbarPrefab, bannerPrefab, platformPrefab, townPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(pauseRoot);
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static void BuildPauseRelayoutToggle(StringBuilder _log)
    {
        var root = PrefabUtility.LoadPrefabContents(PAUSE_PREFAB);
        try
        {
            var menu = root.GetComponentInChildren<PauseMenu>(true);
            if (!menu)
            {
                return;
            }
            var so = new SerializedObject(menu);
            var anchorBtn = so.FindProperty("resetLayoutButton").objectReferenceValue as Button;
            if (!anchorBtn)
            {
                anchorBtn = so.FindProperty("candidateToggleButton").objectReferenceValue as Button;
            }
            if (!anchorBtn)
            {
                return;
            }

            var panel = (RectTransform)anchorBtn.transform.parent;
            var existing = panel.Find(RELAYOUT_TOGGLE_BTN);
            GameObject toggleGo;
            if (existing)
            {
                toggleGo = existing.gameObject;
            }
            else
            {
                toggleGo = Object.Instantiate(anchorBtn.gameObject, panel);
                toggleGo.name = RELAYOUT_TOGGLE_BTN;
                toggleGo.SetActive(true);
                toggleGo.transform.SetSiblingIndex(anchorBtn.transform.GetSiblingIndex() + 1);
                MakeRoomBelow(panel, (RectTransform)anchorBtn.transform, (RectTransform)toggleGo.transform);
            }

            var button = toggleGo.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            var label = toggleGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label)
            {
                label.text = "새 화면 배치: 꺼짐";
            }

            so.FindProperty("relayoutToggleButton").objectReferenceValue = button;
            so.FindProperty("relayoutToggleLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PAUSE_PREFAB);
            _log.AppendLine($"일시정지 '새 화면 배치' 토글 → {PAUSE_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static GameObject BuildTopHudPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var cityNavRoot = PrefabUtility.LoadPrefabContents(CITY_NAV_PREFAB);
        var rootGo = new GameObject("TopHUD", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 16f, 12f, 1888f, 56f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(_palette.Panel.r, _palette.Panel.g, _palette.Panel.b, 0.94f);
            BindPalette(rootGo, _palette, UIPalette.ColorRole.Panel, true);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // 도시/지역명 (장식 점 제거, 평면 텍스트)
            var cityLabel = CreateLabel(_labelTemplate, rootRect, "CityName", 20f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            cityLabel.fontStyle = FontStyles.Bold;
            cityLabel.richText = true;
            cityLabel.overflowMode = TextOverflowModes.Ellipsis;
            SetTopLeftRect(cityLabel.rectTransform, 18f, 8f, 342f, 40f);

            AddDivider(rootRect, 372f, _palette);

            // 남은 기한
            var deadlineTitle = CreateLabel(_labelTemplate, rootRect, "DeadlineTitle", 12f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.TextSub);
            deadlineTitle.text = "남은 기한";
            SetTopLeftRect(deadlineTitle.rectTransform, 388f, 8f, 140f, 18f);

            var deadlineLabel = CreateLabel(_labelTemplate, rootRect, "DeadlineValue", 16f,
                TextAlignmentOptions.Left, _palette, UIPalette.ColorRole.Text);
            deadlineLabel.fontStyle = FontStyles.Bold;
            SetTopLeftRect(deadlineLabel.rectTransform, 388f, 26f, 160f, 24f);

            AddDivider(rootRect, 558f, _palette);

            // 기존 City Navigation의 Game Time(게이지+정산)과 Reputation 복제 재사용
            var oldGameTime = cityNavRoot.transform.Find("Game Time");
            GameObject clonedGameTime = null;
            if (oldGameTime)
            {
                clonedGameTime = Object.Instantiate(oldGameTime.gameObject, rootRect);
                clonedGameTime.name = "Game Time";
                var gtRect = (RectTransform)clonedGameTime.transform;
                SetTopLeftRect(gtRect, 574f, 4f, 408f, 48f);
            }

            // 기존 Game Time 내부 참조 추출
            TextMeshProUGUI timeLabel = null;
            Image dnBar = null, nightA = null, nightB = null;
            RectTransform marker = null;
            if (clonedGameTime)
            {
                timeLabel = FindDeep(clonedGameTime.transform, "Time")?.GetComponent<TextMeshProUGUI>()
                    ?? clonedGameTime.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                dnBar = FindDeep(clonedGameTime.transform, "DayNightBar")?.GetComponent<Image>();
                nightA = FindDeep(clonedGameTime.transform, "NightA")?.GetComponent<Image>();
                nightB = FindDeep(clonedGameTime.transform, "NightB")?.GetComponent<Image>();
                marker = FindDeep(clonedGameTime.transform, "Marker") as RectTransform;
            }

            // 평판 (Reputation 복제)
            var oldRep = cityNavRoot.transform.Find("Reputation");
            if (oldRep)
            {
                var clonedRep = Object.Instantiate(oldRep.gameObject, rootRect);
                clonedRep.name = "Reputation";
                SetTopLeftRect((RectTransform)clonedRep.transform, 1086f, 8f, 130f, 40f);
            }

            // 보유 자금
            var fundsTitle = CreateLabel(_labelTemplate, rootRect, "FundsTitle", 12f,
                TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.TextSub);
            fundsTitle.text = "보유 자금";
            SetTopLeftRect(fundsTitle.rectTransform, 1230f, 8f, 150f, 18f);

            var fundsLabel = CreateLabel(_labelTemplate, rootRect, "FundsValue", 20f,
                TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.GoldAccent);
            fundsLabel.fontStyle = FontStyles.Bold;
            SetTopLeftRect(fundsLabel.rectTransform, 1230f, 24f, 150f, 26f);

            // 결산·목표 버튼
            var clearBtn = CreatePillButton(_buttonTemplate, _labelTemplate, rootRect, "ClearButton",
                1400f, 8f, 260f, 40f, "결산 · 목표 1,500$", 16f, _palette, out var clearLbl);

            // 일시정지 버튼
            var pauseBtn = CreatePillButton(_buttonTemplate, _labelTemplate, rootRect, "PauseButton",
                1676f, 8f, 196f, 40f, "일시정지 (Esc)", 16f, _palette, out var pauseLbl);

            var view = rootGo.AddComponent<TopHudView>();
            var so = new SerializedObject(view);
            so.FindProperty("cityName").objectReferenceValue = cityLabel;
            so.FindProperty("timeLabel").objectReferenceValue = timeLabel;
            so.FindProperty("dayNightBar").objectReferenceValue = dnBar;
            so.FindProperty("nightSegmentA").objectReferenceValue = nightA;
            so.FindProperty("nightSegmentB").objectReferenceValue = nightB;
            so.FindProperty("clockMarker").objectReferenceValue = marker;
            so.FindProperty("deadlineTitle").objectReferenceValue = deadlineTitle;
            so.FindProperty("deadlineLabel").objectReferenceValue = deadlineLabel;
            so.FindProperty("funds").objectReferenceValue = fundsLabel;
            so.FindProperty("clearButton").objectReferenceValue = clearBtn;
            so.FindProperty("clearLabel").objectReferenceValue = clearLbl;
            so.FindProperty("pauseButton").objectReferenceValue = pauseBtn;
            so.FindProperty("pauseLabel").objectReferenceValue = pauseLbl;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, TOP_HUD_PREFAB);
            _log.AppendLine($"TopHUD 프리팹 → {TOP_HUD_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
            PrefabUtility.UnloadPrefabContents(cityNavRoot);
        }
    }

    static GameObject BuildQuickToolbarPrefab(
        StringBuilder _log,
        UIPalette _palette,
        Button _buttonTemplate,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = new GameObject("QuickToolbar", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 26f, 82f, 510f, 38f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(_palette.Panel.r, _palette.Panel.g, _palette.Panel.b, 0.92f);
            BindPalette(rootGo, _palette, UIPalette.ColorRole.Panel, true);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            var nbBtn = CreatePillButton(_buttonTemplate, _labelTemplate, rootRect, "NotebookButton",
                8f, 5f, 92f, 28f, "수첩 (N)", 14f, _palette, out var nbLbl);
            var mapBtn = CreatePillButton(_buttonTemplate, _labelTemplate, rootRect, "MapButton",
                106f, 5f, 102f, 28f, "노선도 (M)", 14f, _palette, out var mapLbl);
            var bgBtn = CreatePillButton(_buttonTemplate, _labelTemplate, rootRect, "BackgroundOnlyButton",
                214f, 5f, 104f, 28f, "배경만 보기", 14f, _palette, out var bgLbl);
            var resetBtn = CreatePillButton(_buttonTemplate, _labelTemplate, rootRect, "ResetLayoutButton",
                324f, 5f, 114f, 28f, "창 배치 초기화", 14f, _palette, out var resetLbl);

            var hintLbl = CreateLabel(_labelTemplate, rootRect, "ShortcutHint", 12f,
                TextAlignmentOptions.Right, _palette, UIPalette.ColorRole.TextSub);
            hintLbl.text = "1~6";
            SetTopLeftRect(hintLbl.rectTransform, 442f, 7f, 60f, 24f);

            var view = rootGo.AddComponent<QuickToolbarView>();
            var so = new SerializedObject(view);
            so.FindProperty("notebookButton").objectReferenceValue = nbBtn;
            so.FindProperty("notebookLabel").objectReferenceValue = nbLbl;
            so.FindProperty("mapButton").objectReferenceValue = mapBtn;
            so.FindProperty("mapLabel").objectReferenceValue = mapLbl;
            so.FindProperty("backgroundOnlyButton").objectReferenceValue = bgBtn;
            so.FindProperty("backgroundOnlyLabel").objectReferenceValue = bgLbl;
            so.FindProperty("resetLayoutButton").objectReferenceValue = resetBtn;
            so.FindProperty("resetLayoutLabel").objectReferenceValue = resetLbl;
            so.FindProperty("shortcutHintLabel").objectReferenceValue = hintLbl;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, TOOLBAR_PREFAB);
            _log.AppendLine($"QuickToolbar 프리팹 → {TOOLBAR_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static GameObject BuildStatusBannerPrefab(
        StringBuilder _log,
        UIPalette _palette,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = new GameObject("StatusBanner", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 660f, 90f, 460f, 34f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(_palette.Panel2.r, _palette.Panel2.g, _palette.Panel2.b, 0.94f);
            bg.raycastTarget = false;
            BindPalette(rootGo, _palette, UIPalette.ColorRole.Panel2, true);

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.GoldAccent;
            outline.effectDistance = new Vector2(1f, -1f);

            var text = CreateLabel(_labelTemplate, rootRect, "StatusText", 14f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
            text.enableWordWrapping = false;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);

            var view = rootGo.AddComponent<StatusBannerView>();
            var so = new SerializedObject(view);
            so.FindProperty("statusText").objectReferenceValue = text;
            so.FindProperty("root").objectReferenceValue = rootGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            rootGo.SetActive(false);
            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, BANNER_PREFAB);
            _log.AppendLine($"StatusBanner 프리팹 → {BANNER_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static GameObject BuildPlatformScenePrefab(StringBuilder _log, UIPalette _palette)
    {
        var rootGo = new GameObject("PlatformScene", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 26f, 134f, 510f, 929f);

            // 평면 플랫폼 바닥 사각형
            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.227f, 0.255f, 0.302f, 1f);
            bg.raycastTarget = false;

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // 주행 중 속도선 (사전 배치 숨김)
            var speedLines = NewRect("TravelSpeedLines", rootRect);
            Stretch(speedLines);
            for (int i = 0; i < 6; i++)
            {
                AddStaticRect(speedLines, $"Line_{i}", 60f + i * 75f, 80f + (i % 2) * 140f, 2f, 220f,
                    new Color(0.94f, 0.96f, 0.98f, 0.25f));
            }
            speedLines.gameObject.SetActive(false);

            var view = rootGo.AddComponent<PlatformSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("travelSpeedLines").objectReferenceValue = speedLines.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, PLATFORM_PREFAB);
            _log.AppendLine($"PlatformScene 프리팹(평면 사각형) → {PLATFORM_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static GameObject BuildTownScenePrefab(
        StringBuilder _log,
        UIPalette _palette,
        TextMeshProUGUI _labelTemplate)
    {
        var rootGo = new GameObject("TownScene", typeof(RectTransform));
        try
        {
            rootGo.layer = 5;
            var rootRect = (RectTransform)rootGo.transform;
            SetTopLeftRect(rootRect, 653f, 82f, 1247f, 977f);

            // 평면 마을 바닥 사각형
            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.239f, 0.290f, 0.227f, 1f);
            bg.raycastTarget = false;

            var outline = rootGo.AddComponent<Outline>();
            outline.effectColor = _palette.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // 6개 평면 사각형 건물 (concept-3b 좌표)
            var bMarket = CreateBuilding(rootRect, _labelTemplate, _palette, "Building_Market",
                TownBuildingButton.BuildingKind.Market, 825f, 50f, 275f, 205f,
                new Color(0.118f, 0.161f, 0.231f, 1f), _palette.GoldAccent, "시장 (1)", hasStatusLine: true);

            var bTavern = CreateBuilding(rootRect, _labelTemplate, _palette, "Building_Tavern",
                TownBuildingButton.BuildingKind.Tavern, 95f, 555f, 220f, 175f,
                new Color(0.271f, 0.102f, 0.012f, 1f), new Color(0.961f, 0.620f, 0.043f, 1f),
                "술집 (2)", hasStatusLine: true);

            var bRest = CreateBuilding(rootRect, _labelTemplate, _palette, "Building_Rest",
                TownBuildingButton.BuildingKind.Rest, 435f, 555f, 220f, 175f,
                new Color(0.118f, 0.227f, 0.373f, 1f), new Color(0.376f, 0.647f, 0.980f, 1f),
                "여관 (3)", hasStatusLine: true);

            var bStation = CreateBuilding(rootRect, _labelTemplate, _palette, "Building_StationHall",
                TownBuildingButton.BuildingKind.StationHall, 90f, 235f, 215f, 165f,
                new Color(0.486f, 0.176f, 0.071f, 1f), new Color(0.992f, 0.729f, 0.455f, 1f),
                "역사 (4)", hasStatusLine: true);

            var bWorkshop = CreateBuilding(rootRect, _labelTemplate, _palette, "Building_Workshop",
                TownBuildingButton.BuildingKind.Workshop, 825f, 310f, 270f, 180f,
                new Color(0.153f, 0.153f, 0.165f, 1f), new Color(0.380f, 0.647f, 0.980f, 1f),
                "생산시설 (5)", hasStatusLine: true, hasEmptyLot: true);

            var bCityHall = CreateBuilding(rootRect, _labelTemplate, _palette, "Building_CityHall",
                TownBuildingButton.BuildingKind.CityHall, 435f, 50f, 240f, 175f,
                new Color(0.200f, 0.255f, 0.333f, 1f), new Color(0.796f, 0.835f, 0.882f, 1f),
                "시청 (6)", hasStatusLine: true);

            var view = rootGo.AddComponent<TownSceneView>();
            var so = new SerializedObject(view);
            var arr = so.FindProperty("buildings");
            TownBuildingButton[] list = { bMarket, bTavern, bRest, bStation, bWorkshop, bCityHall };
            arr.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rootGo, TOWN_PREFAB);
            _log.AppendLine($"TownScene 프리팹(평면 사각형 건물) → {TOWN_PREFAB}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(rootGo);
        }
    }

    static TownBuildingButton CreateBuilding(
        RectTransform _parent,
        TextMeshProUGUI _labelTemplate,
        UIPalette _palette,
        string _name,
        TownBuildingButton.BuildingKind _kind,
        float _left,
        float _top,
        float _width,
        float _height,
        Color _wallColor,
        Color _strokeColor,
        string _labelText,
        bool hasStatusLine = false,
        bool hasRumorLine = false,
        bool hasEmptyLot = false)
    {
        var root = NewRect(_name, _parent);
        SetTopLeftRect(root, _left, _top, _width, _height);

        var wallImg = root.gameObject.AddComponent<Image>();
        wallImg.color = _wallColor;

        var baseBorder = root.gameObject.AddComponent<Outline>();
        baseBorder.effectColor = _strokeColor;
        baseBorder.effectDistance = new Vector2(2f, -2f);

        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = wallImg;

        // 호버 하이라이트 테두리 (금색, 기본 비활성)
        var highlightGo = NewRect("HoverHighlight", root);
        Stretch(highlightGo);
        var hlImg = highlightGo.gameObject.AddComponent<Image>();
        hlImg.color = new Color(_palette.GoldAccent.r, _palette.GoldAccent.g, _palette.GoldAccent.b, 0.16f);
        hlImg.raycastTarget = false;
        var hlOutline = highlightGo.gameObject.AddComponent<Outline>();
        hlOutline.effectColor = _palette.GoldAccent;
        hlOutline.effectDistance = new Vector2(2f, -2f);
        hlImg.enabled = false;

        // 라벨
        var tagBg = AddStaticRect(root, "NameTagBg", 16f, _height * 0.5f - 16f, _width - 32f, 32f,
            new Color(0.059f, 0.090f, 0.141f, 0.9f));
        var nameTag = CreateLabel(_labelTemplate, tagBg, "NameTag", 16f,
            TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        nameTag.fontStyle = FontStyles.Bold;
        nameTag.text = _labelText;
        Stretch(nameTag.rectTransform);

        var comp = root.gameObject.AddComponent<TownBuildingButton>();
        var so = new SerializedObject(comp);
        so.FindProperty("kind").enumValueIndex = (int)_kind;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("highlightBorder").objectReferenceValue = hlImg;
        so.FindProperty("nameTag").objectReferenceValue = nameTag;

        if (hasStatusLine)
        {
            var statusObj = AddStaticRect(root, "StatusLine", 16f, _height - 40f, _width - 32f, 24f, _palette.Panel);
            var statusText = CreateLabel(_labelTemplate, statusObj, "StatusLineText", 12f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.GoldAccent);
            Stretch(statusText.rectTransform);
            statusObj.gameObject.SetActive(false);
            so.FindProperty("chalkboardObject").objectReferenceValue = statusObj.gameObject;
            so.FindProperty("chalkboardText").objectReferenceValue = statusText;
        }

        if (hasRumorLine)
        {
            var rumorObj = AddStaticRect(root, "RumorStatusLine", 16f, _height - 40f, _width - 32f, 24f, _palette.Panel);
            var rumorText = CreateLabel(_labelTemplate, rumorObj, "RumorStatusText", 12f,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.GoldAccent);
            Stretch(rumorText.rectTransform);
            rumorObj.gameObject.SetActive(false);
            so.FindProperty("rumorBubbleObject").objectReferenceValue = rumorObj.gameObject;
            so.FindProperty("rumorBubbleText").objectReferenceValue = rumorText;
        }

        if (hasEmptyLot)
        {
            var emptyLot = AddStaticRect(root, "EmptyLotVisual", 12f, 12f, _width - 24f, _height - 24f,
                new Color(0.212f, 0.271f, 0.212f, 0.65f));
            emptyLot.SetSiblingIndex(0);
            emptyLot.gameObject.SetActive(false);
            so.FindProperty("emptyLotVisual").objectReferenceValue = emptyLot.gameObject;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    static void UpdateGameCanvas(
        StringBuilder _log,
        GameObject _topHudPrefab,
        GameObject _toolbarPrefab,
        GameObject _bannerPrefab,
        GameObject _platformPrefab,
        GameObject _townPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(GAME_CANVAS_PREFAB);
        try
        {
            var worldCanvas = EnsureSubCanvas(root.transform, WORLD_CANVAS_NAME, 1);
            RemoveChildrenNamed(worldCanvas.transform, "PlatformScene");
            RemoveChildrenNamed(worldCanvas.transform, "TownScene");
            var platformInst = (GameObject)PrefabUtility.InstantiatePrefab(_platformPrefab, worldCanvas.transform);
            var townInst = (GameObject)PrefabUtility.InstantiatePrefab(_townPrefab, worldCanvas.transform);

            var fixedCanvas = EnsureSubCanvas(root.transform, FIXED_CANVAS_NAME, 10);
            RemoveChildrenNamed(fixedCanvas.transform, "TopHUD");
            RemoveChildrenNamed(fixedCanvas.transform, "QuickToolbar");
            RemoveChildrenNamed(fixedCanvas.transform, "StatusBanner");
            var topHudInst = (GameObject)PrefabUtility.InstantiatePrefab(_topHudPrefab, fixedCanvas.transform);
            var toolbarInst = (GameObject)PrefabUtility.InstantiatePrefab(_toolbarPrefab, fixedCanvas.transform);
            var bannerInst = (GameObject)PrefabUtility.InstantiatePrefab(_bannerPrefab, fixedCanvas.transform);

            // 정렬 순서 보장: World(0) < Fixed(10) < Floating(50) < Overlay(200)
            var invPresenter = root.GetComponent<PuzzleInventoryPresenter>();
            var topHudView = topHudInst.GetComponent<TopHudView>();
            if (topHudView && invPresenter)
            {
                var tSo = new SerializedObject(topHudView);
                tSo.FindProperty("inventoryPresenter").objectReferenceValue = invPresenter;
                tSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var cityView = root.GetComponentInChildren<PuzzleCityView>(true);
            var statusBannerView = bannerInst.GetComponent<StatusBannerView>();
            if (cityView)
            {
                var cSo = new SerializedObject(cityView);
                cSo.FindProperty("topHudView").objectReferenceValue = topHudView;
                cSo.FindProperty("statusBannerView").objectReferenceValue = statusBannerView;
                cSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var qtView = toolbarInst.GetComponent<QuickToolbarView>();
            if (qtView)
            {
                var qSo = new SerializedObject(qtView);
                qSo.FindProperty("notebookView").objectReferenceValue = root.GetComponentInChildren<PuzzleTradeNotebookView>(true);
                qSo.FindProperty("windowManager").objectReferenceValue = root.GetComponentInChildren<FloatingWindowManager>(true);
                qSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var controller = root.GetComponent<RelayoutRootController>();
            if (!controller)
            {
                controller = root.AddComponent<RelayoutRootController>();
            }
            var townView = townInst.GetComponent<TownSceneView>();
            if (townView)
            {
                var tvSo = new SerializedObject(townView);
                tvSo.FindProperty("rootController").objectReferenceValue = controller;
                tvSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var rSo = new SerializedObject(controller);
            rSo.FindProperty("worldSceneCanvas").objectReferenceValue = worldCanvas;
            rSo.FindProperty("fixedFrameCanvas").objectReferenceValue = fixedCanvas;
            rSo.FindProperty("floatingWindowCanvas").objectReferenceValue = FindDeep(root.transform, "FloatingWindowCanvas")?.gameObject;
            rSo.FindProperty("oldGameplayLayout").objectReferenceValue = FindDeep(root.transform, "GameplayLayout")?.gameObject;
            rSo.FindProperty("oldTrainColumn").objectReferenceValue = FindDeep(root.transform, "TrainColumn")?.gameObject;
            rSo.FindProperty("oldFixedBottomBar").objectReferenceValue = FindDeep(root.transform, "FixedBottomBar")?.gameObject;
            rSo.FindProperty("oldCityNavigation").objectReferenceValue = FindDeep(root.transform, "City Navigation")?.gameObject;
            rSo.FindProperty("oldJourneyFooter").objectReferenceValue = FindDeep(root.transform, "JourneyFooter")?.gameObject;
            rSo.FindProperty("oldRumorSideHandle").objectReferenceValue = FindDeep(root.transform, "SideHandle")?.gameObject;
            rSo.FindProperty("stationActionsPresenter").objectReferenceValue = root.GetComponentInChildren<PuzzleStationActionsPresenter>(true);
            rSo.FindProperty("statusBannerView").objectReferenceValue = statusBannerView;
            rSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GAME_CANVAS_PREFAB);
            _log.AppendLine($"GameCanvas 업데이트 (WorldSceneCanvas:0, FixedFrameCanvas:10, RelayoutRootController) → {GAME_CANVAS_PREFAB}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Button CreatePillButton(
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
        SetTopLeftRect((RectTransform)go.transform, _left, _top, _width, _height);
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        if (btn.targetGraphic is Image img)
        {
            img.color = _palette.Panel2;
            BindPalette(go, _palette, UIPalette.ColorRole.Panel2);
        }
        _label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!_label)
        {
            _label = CreateLabel(_labelTemplate, (RectTransform)go.transform, "Label", _fontSize,
                TextAlignmentOptions.Center, _palette, UIPalette.ColorRole.Text);
        }
        Stretch(_label.rectTransform);
        _label.rectTransform.offsetMin = new Vector2(6f, 0f);
        _label.rectTransform.offsetMax = new Vector2(-6f, 0f);
        _label.enableAutoSizing = false;
        _label.fontSize = _fontSize;
        _label.text = _text;
        _label.color = _palette.Text;
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
        Color _color,
        Sprite _sprite = null)
    {
        var rect = NewRect(_name, _parent);
        SetTopLeftRect(rect, _left, _top, _width, _height);
        var img = rect.gameObject.AddComponent<Image>();
        img.sprite = _sprite;
        img.color = _color;
        img.raycastTarget = false;
        return rect;
    }

    static void AddDivider(RectTransform _parent, float _left, UIPalette _palette)
    {
        var div = AddStaticRect(_parent, $"Divider_{(int)_left}", _left, 12f, 1f, 32f, _palette.Border);
        BindPalette(div.gameObject, _palette, UIPalette.ColorRole.Border);
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

    static void BindPalette(GameObject _go, UIPalette _palette, UIPalette.ColorRole _role, bool _preserveAlpha = false)
    {
        var binder = _go.GetComponent<UIPaletteBinder>();
        if (!binder)
        {
            binder = _go.AddComponent<UIPaletteBinder>();
        }
        var so = new SerializedObject(binder);
        so.FindProperty("palette").objectReferenceValue = _palette;
        so.FindProperty("role").enumValueIndex = (int)_role;
        so.FindProperty("preserveAlpha").boolValue = _preserveAlpha;
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
