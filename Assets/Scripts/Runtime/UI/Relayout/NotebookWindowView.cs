using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S4: 5탭(지도/소문/의뢰/숙련/상성표) 통합 수첩 부동 창(16, 128, 522, 928).
[DisallowMultipleComponent]
public sealed class NotebookWindowView : MonoBehaviour
{
    public enum NotebookWindowTab
    {
        Map = 0,
        Rumors = 1,
        Quests = 2,
        Proficiency = 3,
        Synergy = 4
    }

    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button[] tabButtons;
    [SerializeField] TextMeshProUGUI[] tabLabels;
    [SerializeField] Image[] tabUnderlines;
    [SerializeField] GameObject mapTabContent;
    [SerializeField] GameObject textTabsContent;
    [SerializeField] GameObject synergyTabContent;
    [SerializeField] PuzzleTradeNotebookView embeddedNotebookView;
    [SerializeField] PuzzleTradeNotebookPresenter embeddedNotebookPresenter;
    [SerializeField] PuzzleMapView embeddedMapView;
    [SerializeField] PuzzleMapPresenter embeddedMapPresenter;
    [SerializeField] PuzzleSynergyChartView embeddedSynergyView;
    [SerializeField] RectTransform mapViewport;
    [SerializeField] MapDestinationPinView[] pinPool;
    [SerializeField] TextMeshProUGUI pinOverflowLabel;
    [SerializeField] UIPalette palette;

    readonly List<(PuzzleMapNodeView node, MapDestinationPinView pin)> activePins = new();
    readonly List<Rect> placedPinRects = new();
    readonly List<(int index, Vector2 nodePos)> pinLayoutWork = new();
    NotebookWindowTab currentTab = NotebookWindowTab.Map;
    PuzzleItemDefinition lastCargoDef;
    decimal lastCargoValue;
    bool lastCargoOnTrain;
    int? lastCargoInstanceId;

    public FloatingWindow FloatingWindow => floatingWindow;
    public NotebookWindowTab CurrentTab => currentTab;

    void Awake()
    {
        CreateMapExpandButton();
        ApplyMapDetailVisibility();
        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                if (tabButtons[i])
                {
                    tabButtons[i].onClick.AddListener(() => SelectTab((NotebookWindowTab)idx));
                }
            }
        }
    }

    void OnEnable()
    {
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.PuzzleActionCommitted += OnBoardOrTradeChanged;
        GameEvents.TradeCompleted += OnTradeCompleted;
        RefreshTabLabels();
        ApplyTabVisuals();
    }

    void OnDisable()
    {
        SetMapExpanded(false);
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.PuzzleActionCommitted -= OnBoardOrTradeChanged;
        GameEvents.TradeCompleted -= OnTradeCompleted;
    }

    void LateUpdate()
    {
        FitExpandedMapFrame();
        if (activePins.Count == 0 || !mapTabContent || !mapTabContent.activeInHierarchy || !mapViewport)
        {
            return;
        }
        LayoutActivePins();
    }

    void LayoutActivePins()
    {
        placedPinRects.Clear();
        pinLayoutWork.Clear();

        RectTransform pinsParent = null;
        for (int i = 0; i < activePins.Count; i++)
        {
            var (node, pin) = activePins[i];
            if (!node || !pin || !node.gameObject.activeInHierarchy)
            {
                if (pin) pin.Hide();
                continue;
            }
            if (!pinsParent)
            {
                pinsParent = pin.transform.parent as RectTransform;
            }
            if (!pinsParent)
            {
                continue;
            }
            Vector2 nodeLocal = pinsParent.InverseTransformPoint(node.transform.position);
            pinLayoutWork.Add((i, nodeLocal));
            // 도시 노드·라벨 영역(110×26)도 겹침 회피 대상으로 등록한다.
            placedPinRects.Add(new Rect(nodeLocal.x - 55f, nodeLocal.y - 14f, 110f, 28f));
        }

        if (pinLayoutWork.Count == 0)
        {
            return;
        }

        // 아래쪽 노드부터 배치한 뒤 위로 26px씩 스태킹해 겹침을 방지한다.
        pinLayoutWork.Sort((_a, _b) =>
        {
            int cmp = _a.nodePos.y.CompareTo(_b.nodePos.y);
            return cmp != 0 ? cmp : _a.nodePos.x.CompareTo(_b.nodePos.x);
        });

        const float pinW = 180f;
        const float pinH = 22f;
        const float padX = 4f;
        const float stepY = 26f;

        for (int k = 0; k < pinLayoutWork.Count; k++)
        {
            var (idx, nodeLocal) = pinLayoutWork[k];
            var pin = activePins[idx].pin;
            float x = nodeLocal.x;
            float y = nodeLocal.y + 22f;

            for (int guard = 0; guard < 12; guard++)
            {
                var candidate = new Rect(x - pinW * 0.5f - padX, y, pinW + padX * 2f, pinH + 2f);
                bool overlaps = false;
                for (int r = 0; r < placedPinRects.Count; r++)
                {
                    if (candidate.Overlaps(placedPinRects[r]))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps)
                {
                    break;
                }
                y += stepY;
            }

            pin.transform.position = pinsParent.TransformPoint(new Vector3(x, y, 0f));
            pin.SetLeader(Mathf.Max(0f, y - nodeLocal.y), pin.BorderColor);
            placedPinRects.Add(new Rect(x - pinW * 0.5f - padX, y, pinW + padX * 2f, pinH + 2f));
        }
    }

    public void ToggleLastTab()
    {
        if (floatingWindow && floatingWindow.IsOpen)
        {
            floatingWindow.Close();
        }
        else
        {
            OpenTab(currentTab);
        }
    }

    public void ToggleMapTab()
    {
        if (floatingWindow && floatingWindow.IsOpen && currentTab == NotebookWindowTab.Map)
        {
            floatingWindow.Close();
        }
        else
        {
            OpenTab(NotebookWindowTab.Map);
        }
    }

    public void OpenTab(PuzzleNotebookTab _tab)
    {
        NotebookWindowTab windowTab = _tab switch
        {
            PuzzleNotebookTab.Quests => NotebookWindowTab.Quests,
            PuzzleNotebookTab.Proficiency => NotebookWindowTab.Proficiency,
            PuzzleNotebookTab.Synergy => NotebookWindowTab.Synergy,
            _ => NotebookWindowTab.Rumors
        };
        OpenTab(windowTab);
    }

    public void OpenTab(NotebookWindowTab _tab)
    {
        if (floatingWindow)
        {
            floatingWindow.Open();
        }
        SelectTab(_tab);
    }

    public void SelectTab(NotebookWindowTab _tab)
    {
        if (_tab != NotebookWindowTab.Map) SetMapExpanded(false);
        currentTab = _tab;
        bool isMap = _tab == NotebookWindowTab.Map;
        bool isSynergy = _tab == NotebookWindowTab.Synergy;
        bool isText = !isMap && !isSynergy;

        if (mapTabContent)
        {
            mapTabContent.SetActive(isMap);
        }
        if (textTabsContent)
        {
            textTabsContent.SetActive(isText);
        }
        if (synergyTabContent)
        {
            synergyTabContent.SetActive(isSynergy);
        }

        if (isMap)
        {
            if (embeddedMapPresenter)
            {
                embeddedMapPresenter.OpenFromNotebook();
            }
            RefreshDestinationPins();
        }
        else if (isSynergy)
        {
            if (embeddedSynergyView)
            {
                embeddedSynergyView.SetOpen(true);
            }
        }
        else if (embeddedNotebookView)
        {
            PuzzleNotebookTab legacyTab = _tab switch
            {
                NotebookWindowTab.Quests => PuzzleNotebookTab.Quests,
                NotebookWindowTab.Proficiency => PuzzleNotebookTab.Proficiency,
                _ => PuzzleNotebookTab.Rumors
            };
            embeddedNotebookView.SetOpen(true);
            embeddedNotebookView.SelectTabFromHost(legacyTab);
        }

        ApplyTabVisuals();
    }

    internal void FocusMap(GameEvents.MapFocusInfo _info)
    {
        OpenTab(NotebookWindowTab.Map);
        if (embeddedMapPresenter)
        {
            embeddedMapPresenter.FocusFromNotebook(_info);
        }
    }

    internal void PlanRouteFromMap(GameEvents.RoutePlanRequestInfo _info)
    {
        OpenTab(NotebookWindowTab.Map);
        SetMapExpanded(true);
        if (embeddedMapPresenter)
        {
            embeddedMapPresenter.PlanRouteFromNotebook(_info);
        }
    }

    // R6: 마지막으로 선택한 화물 기준으로 지도 탭 목적지 핀을 갱신한다.
    public void SetLastSelectedCargo(PuzzleItemDefinition _definition, decimal _value, bool _itemOnTrain, int? _instanceId = null)
    {
        lastCargoDef = _definition;
        lastCargoValue = _value;
        lastCargoOnTrain = _itemOnTrain;
        lastCargoInstanceId = _definition != null ? _instanceId : null;
        if (mapTabContent && mapTabContent.activeInHierarchy)
        {
            RefreshDestinationPins();
        }
    }

    public void ClearLastSelectedCargo()
    {
        if (lastCargoDef == null && !lastCargoInstanceId.HasValue && activePins.Count == 0)
        {
            return;
        }
        lastCargoDef = null;
        lastCargoValue = 0m;
        lastCargoOnTrain = false;
        lastCargoInstanceId = null;
        RefreshDestinationPins();
    }

    public void RefreshDestinationPins()
    {
        activePins.Clear();
        if (pinOverflowLabel)
        {
            pinOverflowLabel.gameObject.SetActive(false);
        }
        if (pinPool == null)
        {
            return;
        }
        for (int i = 0; i < pinPool.Length; i++)
        {
            if (pinPool[i]) pinPool[i].Hide();
        }

        if (!mapExpanded) return;

        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session != null)
        {
            SyncLastSelectedCargo(session);
        }
        if (lastCargoDef == null || !embeddedMapView || !mapViewport || session == null)
        {
            return;
        }

        var pal = palette ? palette : Resources.Load<UIPalette>("UI/UIPalette");
        var destinations = session.LensDestinations(lastCargoDef, lastCargoValue, lastCargoOnTrain);
        int pinIdx = 0;
        int overflowCount = 0;
        for (int i = 0; i < destinations.Count; i++)
        {
            var dest = destinations[i];
            if (dest.Level == PuzzleLensLevel.Unknown)
            {
                continue;
            }
            var node = Array.Find(embeddedMapView.Nodes,
                _n => _n && string.Equals(_n.CityId, dest.CityId, StringComparison.Ordinal));
            if (!node || !node.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (pinIdx >= pinPool.Length)
            {
                overflowCount++;
                continue;
            }

            string text = dest.Level == PuzzleLensLevel.Visited
                ? Presentation.NotebookMapPinVisited(dest.SellPercent ?? 100, dest.Quote, dest.Fare)
                : Presentation.NotebookMapPinRumor(dest.RumorMessage, dest.Fare);

            var pin = pinPool[pinIdx++];
            Color bgCol = pal ? pal.Panel : new Color(0.06f, 0.09f, 0.14f, 0.96f);
            Color borderCol = dest.Level == PuzzleLensLevel.Visited
                ? (pal ? pal.GoldAccent : Color.yellow)
                : (pal ? pal.BorderBright : Color.cyan);
            Color textCol = pal ? pal.Text : Color.white;

            pin.Show(text, bgCol, borderCol, textCol);
            activePins.Add((node, pin));
        }

        LayoutActivePins();

        if (overflowCount > 0 && pinOverflowLabel)
        {
            pinOverflowLabel.text = Presentation.NotebookMapPinOverflow(overflowCount);
            pinOverflowLabel.gameObject.SetActive(true);
        }
    }

    void SyncLastSelectedCargo(PuzzleInventoryService _session)
    {
        if (!lastCargoInstanceId.HasValue || lastCargoDef == null)
        {
            return;
        }

        int targetId = lastCargoInstanceId.Value;
        var wagons = _session.TrainWagons;
        if (wagons != null)
        {
            for (int w = 0; w < wagons.Count; w++)
            {
                var wagon = wagons[w];
                if (wagon != null && wagon.TryGetPlacement(targetId, out var trainPlacement))
                {
                    lastCargoDef = trainPlacement.Definition;
                    lastCargoValue = trainPlacement.FreshnessAdjustedValue;
                    lastCargoOnTrain = true;
                    return;
                }
            }
        }

        if (_session.Market != null && _session.Market.TryGetPlacement(targetId, out var marketPlacement))
        {
            lastCargoDef = marketPlacement.Definition;
            lastCargoValue = marketPlacement.FreshnessAdjustedValue;
            lastCargoOnTrain = false;
            return;
        }

        lastCargoDef = null;
        lastCargoValue = 0m;
        lastCargoOnTrain = false;
        lastCargoInstanceId = null;
    }

    void RefreshTabLabels()
    {
        if (titleText) titleText.text = Presentation.NotebookTitle;
        if (tabLabels == null || tabLabels.Length < 5) return;
        if (tabLabels[0]) tabLabels[0].text = Presentation.NotebookTabMap;
        if (tabLabels[1]) tabLabels[1].text = Presentation.RumorLogRumorTab;
        if (tabLabels[2]) tabLabels[2].text = Presentation.RumorLogQuestTab;
        if (tabLabels[3]) tabLabels[3].text = Presentation.RumorLogProficiencyTab;
        if (tabLabels[4]) tabLabels[4].text = Presentation.RumorLogSynergyTab;
    }

    void ApplyTabVisuals()
    {
        var pal = palette ? palette : Resources.Load<UIPalette>("UI/UIPalette");
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            bool selected = i == (int)currentTab;
            if (tabButtons[i] && tabButtons[i].targetGraphic is Image bg)
            {
                bg.color = selected
                    ? (pal ? pal.Panel2 : new Color(0.08f, 0.13f, 0.20f, 1f))
                    : (pal ? pal.Panel : new Color(0.06f, 0.09f, 0.14f, 1f));
            }
            if (tabLabels != null && i < tabLabels.Length && tabLabels[i])
            {
                tabLabels[i].color = selected
                    ? (pal ? pal.GoldAccent : Color.yellow)
                    : (pal ? pal.TextSub : Color.gray);
            }
            if (tabUnderlines != null && i < tabUnderlines.Length && tabUnderlines[i])
            {
                tabUnderlines[i].enabled = selected;
                if (pal) tabUnderlines[i].color = pal.GoldAccent;
            }
        }
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (mapTabContent && mapTabContent.activeInHierarchy) RefreshDestinationPins();
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        if (mapTabContent && mapTabContent.activeInHierarchy) RefreshDestinationPins();
    }

    void OnRumorRecorded(GameEvents.TavernRumorInfo _info)
    {
        if (mapTabContent && mapTabContent.activeInHierarchy) RefreshDestinationPins();
    }

    void OnBoardOrTradeChanged(GameEvents.PuzzleActionInfo _info)
    {
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session != null) SyncLastSelectedCargo(session);
        if (mapTabContent && mapTabContent.activeInHierarchy) RefreshDestinationPins();
    }

    void OnTradeCompleted(GameEvents.TradeInfo _info)
    {
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session != null) SyncLastSelectedCargo(session);
        if (mapTabContent && mapTabContent.activeInHierarchy) RefreshDestinationPins();
    }

    // Keep one map/presenter alive so expanding preserves route selection and travel.
    readonly List<MapRectState> compactMapRects = new();
    RectTransform expandedMapLayer;
    RectTransform expandedMapFrame;
    Transform compactMapParent;
    int compactMapSibling;
    Button mapExpandButton;
    bool mapExpanded;
    GridLayoutGroup mapLegendGrid;
    int compactLegendColumns;
    PuzzleItemTooltipView expandedTownTooltip;
    Transform townTooltipParent;

    void ApplyMapDetailVisibility()
    {
        if (!embeddedMapView) return;
        // Keep presenters active so route/travel state continues updating while compact.
        foreach (var rt in embeddedMapView.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name != "RoutePlan" && rt.name != "MapTravelPanel" &&
                rt.name != "MapTradePreferences" && rt.name != "CityQuestInformation") continue;
            var group = rt.GetComponent<CanvasGroup>();
            // Unity's missing-component objects can compare equal to null without being CLR null.
            // Use Unity's existence check rather than ?? before accessing native properties.
            if (!group) group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = mapExpanded ? 1f : 0f;
            group.blocksRaycasts = mapExpanded;
            group.interactable = mapExpanded;
        }
    }

    struct MapRectState
    {
        public RectTransform rect;
        public Vector2 min, max, pivot, position, size;
        public MapRectState(RectTransform value)
        {
            rect = value;
            min = value.anchorMin; max = value.anchorMax; pivot = value.pivot;
            position = value.anchoredPosition; size = value.sizeDelta;
        }
        public void Restore()
        {
            if (!rect) return;
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }

    void CreateMapExpandButton()
    {
        if (!mapViewport || !mapTabContent) return;
        var buttonObject = new GameObject("ExpandMap", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(mapTabContent.transform, false);
        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-8f, -108f);
        rect.sizeDelta = new Vector2(40f, 40f);
        buttonObject.GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.20f, 1f);
        mapExpandButton = buttonObject.GetComponent<Button>();
        mapExpandButton.onClick.AddListener(() => SetMapExpanded(!mapExpanded));

        // Draw the diagonal double arrow with UI geometry (independent of font glyph coverage).
        AddArrowStroke(rect, Vector2.zero, new Vector2(24f, 2f), 45f);
        AddArrowStroke(rect, new Vector2(8f, 4f), new Vector2(2f, 10f), 0f);
        AddArrowStroke(rect, new Vector2(4f, 8f), new Vector2(10f, 2f), 0f);
        AddArrowStroke(rect, new Vector2(-8f, -4f), new Vector2(2f, 10f), 0f);
        AddArrowStroke(rect, new Vector2(-4f, -8f), new Vector2(10f, 2f), 0f);
    }

    void AddArrowStroke(RectTransform parent, Vector2 position, Vector2 size, float angle)
    {
        var stroke = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        stroke.layer = gameObject.layer;
        stroke.transform.SetParent(parent, false);
        var rt = (RectTransform)stroke.transform;
        rt.anchoredPosition = position; rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var image = stroke.GetComponent<Image>();
        image.color = new Color(0.95f, 0.80f, 0.45f);
        image.raycastTarget = false;
    }

    public bool IsMapExpanded => mapExpanded;

    public void SetMapExpanded(bool expanded)
    {
        if (mapExpanded == expanded || !mapTabContent) return;
        if (expanded)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas) return;
            compactMapParent = mapTabContent.transform.parent;
            compactMapSibling = mapTabContent.transform.GetSiblingIndex();
            compactMapRects.Clear();
            compactMapRects.Add(new MapRectState((RectTransform)mapTabContent.transform));
            var layer = new GameObject("ExpandedNotebookMap", typeof(RectTransform), typeof(Image));
            layer.layer = gameObject.layer;
            expandedMapLayer = (RectTransform)layer.transform;
            expandedMapLayer.SetParent(canvas.transform, false);
            expandedMapLayer.anchorMin = Vector2.zero;
            expandedMapLayer.anchorMax = Vector2.one;
            expandedMapLayer.offsetMin = expandedMapLayer.offsetMax = Vector2.zero;
            layer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            var frame = new GameObject("MapFrame", typeof(RectTransform), typeof(Image));
            frame.layer = gameObject.layer;
            expandedMapFrame = (RectTransform)frame.transform;
            expandedMapFrame.SetParent(expandedMapLayer, false);
            expandedMapFrame.sizeDelta = new Vector2(1084f, 780f);
            frame.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.14f, 1f);
            mapTabContent.transform.SetParent(expandedMapFrame, false);
            var content = (RectTransform)mapTabContent.transform;
            content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            ApplyExpandedMapLayout();
            ((RectTransform)mapExpandButton.transform).anchoredPosition = new Vector2(-8f, -8f);
            mapExpanded = true;
            FitExpandedMapFrame();
            foreach (var tooltip in embeddedMapView.GetComponentsInChildren<PuzzleItemTooltipView>(true))
            {
                if (tooltip.name != "TownTooltip") continue;
                expandedTownTooltip = tooltip;
                townTooltipParent = tooltip.transform.parent;
                // Tooltip positioning uses canvas coordinates, so its parent must be that canvas.
                tooltip.Close();
                tooltip.transform.SetParent(canvas.transform, false);
                break;
            }
        }
        else
        {
            if (expandedTownTooltip)
            {
                expandedTownTooltip.Close();
                expandedTownTooltip.transform.SetParent(townTooltipParent, false);
                expandedTownTooltip = null;
            }
            // Restore while still active: reparenting must not disable the travel presenter.
            mapTabContent.transform.SetParent(compactMapParent, false);
            mapTabContent.transform.SetSiblingIndex(compactMapSibling);
            foreach (var state in compactMapRects) state.Restore();
            compactMapRects.Clear();
            if (mapLegendGrid) mapLegendGrid.constraintCount = compactLegendColumns;
            if (expandedMapLayer)
            {
                expandedMapLayer.gameObject.SetActive(false);
                Destroy(expandedMapLayer.gameObject);
            }
            mapExpanded = false;
            if (mapExpandButton) ((RectTransform)mapExpandButton.transform).anchoredPosition = new Vector2(-8f, -108f);
        }
        ApplyMapDetailVisibility();
        RefreshDestinationPins();
        Canvas.ForceUpdateCanvases();
        if (embeddedMapView)
        {
            var navigation = embeddedMapView.GetComponentInChildren<PuzzleMapNavigation>(true);
            if (navigation) navigation.RefreshLayout();
        }
        if (mapExpandButton) mapExpandButton.transform.SetAsLastSibling();
    }

    void FitExpandedMapFrame()
    {
        if (!mapExpanded || !expandedMapLayer) return;
        var available = expandedMapLayer.rect.size;
        float scale = Mathf.Min(1f, Mathf.Min(Mathf.Max(1f, available.x - 80f) / 1084f,
            Mathf.Max(1f, available.y - 112f) / 780f));
        expandedMapFrame.localScale = Vector3.one * scale;
        expandedMapFrame.anchorMin = expandedMapFrame.anchorMax = expandedMapFrame.pivot = new Vector2(0.5f, 0.5f);
        expandedMapFrame.anchoredPosition = Vector2.zero;
    }

    void ApplyExpandedMapLayout()
    {
        var panel = embeddedMapView ? embeddedMapView.GetComponentsInChildren<RectTransform>(true) : null;
        if (panel == null) return;
        foreach (var rt in panel)
        {
            // Original 1084 x 780 right action-panel layout from PuzzleWorldMap.prefab.
            switch (rt.name)
            {
                case "MapSearch": SetExpandedRect(rt, 694, -14, 214, 36); break;
                case "MapLegend": SetExpandedRect(rt, 24, -64, 1036, 40); break;
                case "MapViewport": SetExpandedRect(rt, 24, -112, 1036, 582); break;
                case "CityInformation": SetExpandedRect(rt, 24, -702, 1036, 26); break;
                case "MapControls": SetExpandedRect(rt, 24, -734, 1036, 40); break;
                case "MapTradePreferences": SetExpandedRect(rt, 24, -650, 1036, 56); break;
                case "CityQuestInformation": SetExpandedRect(rt, 24, -708, 1036, 22); break;
                case "ZoomOut": SetExpandedRect(rt, -294, 0, 64, 40, true); break;
                case "ZoomPercent": SetExpandedRect(rt, -206, 0, 100, 40, true); break;
                case "ZoomIn": SetExpandedRect(rt, -118, 0, 64, 40, true); break;
                case "FitMap": SetExpandedRect(rt, 56, 0, 216, 40, true); break;
                case "LocateCurrent": SetExpandedRect(rt, 304, 0, 216, 40, true); break;
                case "RoutePlan": case "MapTravelPanel":
                    compactMapRects.Add(new MapRectState(rt));
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                    break;
                case "TooltipClip":
                    SetExpandedRect(rt, 24, -112, 1036, 582);
                    break;
            }
        }
        mapLegendGrid = mapTabContent.GetComponentInChildren<GridLayoutGroup>(true);
        // Only the legend grid, never map/node grids.
        if (mapLegendGrid && mapLegendGrid.name == "SubwayMapLabels")
        {
            compactLegendColumns = mapLegendGrid.constraintCount;
            mapLegendGrid.constraintCount = 6;
        }
        else mapLegendGrid = null;
    }

    void SetExpandedRect(RectTransform rt, float x, float y, float width, float height, bool centered = false)
    {
        compactMapRects.Add(new MapRectState(rt));
        rt.anchorMin = rt.anchorMax = rt.pivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }
}
