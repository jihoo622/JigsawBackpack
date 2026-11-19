using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleMapView : PanelBase
{
    [System.Serializable]
    public struct NodeLayoutSettings
    {
        public float nodeSize;
        public float nodeEmphasizedScale;
        public float stationHaloSize;
        public int stationBadgeFontSize;
        public float stationLabelExtraWidth;
        public float stationLabelHeight;
        public float stationBadgeExtraWidth;
        public float stationBadgeMinWidth;
        public float stationBadgeHeight;
        public float labelGap;
        public float labelConnectorThickness;
        public int stationFontSizeEmphasized;
        public int stationFontSizeMajor;
        public float stationFontSizeThreshold;
        public int stationFontSizeSmall;
        public int stationFontSizeNormal;
        public int majorStationThreshold;
        public float labelDetailMinZoom;
        public int labelPriorityCurrent;
        public int labelPrioritySelected;
        public int labelPriorityTarget;
        public int labelPriorityConnected;
        public int labelPriorityInterchange;
        public int labelPriorityOther;

        public int CalculateStationFontSize(float _zoom, bool _emphasized, bool _major)
        {
            return _emphasized ? stationFontSizeEmphasized
                : _major ? stationFontSizeMajor
                : _zoom < stationFontSizeThreshold ? stationFontSizeSmall : stationFontSizeNormal;
        }
    }

    [System.Serializable]
    sealed class RegionVisual
    {
        public string regionId;
        public TMPro.TextMeshProUGUI label;
        public Image area;
    }

    // 축소 배율에서 역 이름 대신 보이는 지도 위 지역명(지역 역들의 가운데).
    [System.Serializable]
    sealed class MapRegionLabelVisual
    {
        public string regionId;
        public TMPro.TextMeshProUGUI label;
        public Image plate;
    }

    [System.Serializable]
    sealed class RouteTimeVisual
    {
        public string fromCityId, toCityId;
        public TMPro.TextMeshProUGUI label;
        public Image plate;
        public bool manualPosition;
        [NonSerialized] public Vector2 position;
    }

    [System.Serializable]
    sealed class LineVisual
    {
        public string regionId;
        public Image[] segments;
    }

    [SerializeField] TMPro.TextMeshProUGUI title;
    [SerializeField] TMPro.TextMeshProUGUI guide;
    [SerializeField] TMPro.TextMeshProUGUI information;
    [SerializeField] CityTradePreferencesView tradePreferences;
    [SerializeField] TMPro.TextMeshProUGUI questInformation;
    // 닫기 버튼은 다른 화면과 같은 공용 헤더 버튼(HeaderButton, TMP 라벨)이다.
    [SerializeField] TMPro.TextMeshProUGUI closeLabel;
    [SerializeField] Image background;
    [SerializeField] Image[] routeLines;
    [SerializeField] PuzzleMapNodeView[] nodes;
    [SerializeField] RegionVisual[] regions;
    [SerializeField] MapRegionLabelVisual[] regionLabels;
    [SerializeField] RouteTimeVisual[] routeTimes;
    [SerializeField] LineVisual[] lines;
    [SerializeField] PuzzleMapNavigation navigation;

    [Header("Fog of War")]
    // 들르지 않은 지역을 덮는 고지도 층(RawImage). 지형 배경 바로 위·선로 아래에 두고 지형과 같은 범위를 덮는다.
    [SerializeField] RawImage fogOfWar;
    // 고지도 원본(지형 칸당 1픽셀, 읽기 허용). Tools/MyJG/Generate Map Old Terrain Image가 만든다.
    [SerializeField] Texture2D oldMapTexture;
    // 들른 역 주변과 달려 본 노선 양옆을 걷어 내는 반지름(지도 좌표). 경계는 칸 단위 계단으로 흐려진다.
    [SerializeField] float fogExploredRadius = 90f;
    [SerializeField] float fogRouteRadius = 28f;
    // 노선 외곽선 두께(화면 기준, 한쪽). 노선 아래 외곽선 층(SubwayMapTrackOutlines)의 조각이 이만큼 넓고 길다.
    [SerializeField] float trackOutlineWidth = 2f;
    [SerializeField] float fogEdgeSoftness = 24f;
    // 경계의 계단 수. 클수록 부드럽고 작을수록 도트처럼 딱딱하다.
    [SerializeField] int fogEdgeSteps = 3;

    [Header("Town Tooltip")]
    [SerializeField] PuzzleItemTooltipView travelPreviewTooltip;

    Texture2D fogTexture;
    // 고지도 원본 픽셀의 사본. 알파만 안개 농도로 바꿔 쓴다.
    Color32[] fogPixels;
    readonly List<Vector3> fogHoles = new List<Vector3>();
    // 달려 본 노선의 선분(안개 층 좌표의 양 끝).
    readonly List<(Vector2 From, Vector2 To)> fogSegments = new List<(Vector2 From, Vector2 To)>();

    readonly List<string> tooltipInfoLines = new List<string>();
    readonly List<string> tooltipTradeLines = new List<string>();
    readonly List<(string label, string value)> tooltipStatRows = new List<(string label, string value)>();

    readonly Dictionary<int, List<Image>> routeTrackImages = new Dictionary<int, List<Image>>();
    readonly Dictionary<Image, Color> originalTrackColors = new Dictionary<Image, Color>();
    // 강조가 없을 때 선분의 색. 달려 본 노선은 지역 색을 어둡게, 달려 보지 않은 노선은 짙은 회색이다.
    readonly Dictionary<Image, Color> baseTrackColors = new Dictionary<Image, Color>();
    // 노선 조각 → 같은 자리의 외곽선 조각(TrackOutline_i_j)과 그 조각의 원래 길이.
    readonly Dictionary<Image, Image> trackOutlines = new Dictionary<Image, Image>();
    readonly Dictionary<Image, float> trackLengths = new Dictionary<Image, float>();
    int highlightedRouteIndex = -1;
    readonly HashSet<int> highlightedPathIndices = new HashSet<int>();
    // 강조한 선분은 같은 부모 안에서 맨 위로 올려 다른 노선에 가려 끊겨 보이지 않게 한다. 올리기 전 형제 순서를 기억해 되돌린다.
    readonly Dictionary<Image, int> raisedTrackSiblings = new Dictionary<Image, int>();

    [Header("Map Viewport Padding")]
    [SerializeField] float viewportPaddingLeft = 8f;
    [SerializeField] float viewportPaddingBottom = 8f;
    [SerializeField] float viewportPaddingRight = 8f;
    [SerializeField] float viewportPaddingTop = 8f;

    [Header("Time Label Layout")]
    [SerializeField] float timeLabelMinZoom = 0.85f;
    [SerializeField] float timeLabelPaddingX = 16f;
    [SerializeField] float timeLabelHeight = 28f;
    [SerializeField] int timeFontSize = 18;
    [SerializeField] float timeOffsetX = 28f;
    [SerializeField] float timeOffsetX2 = 28f;
    [SerializeField] float timeOffsetY = 24f;
    [SerializeField] float timeOffsetY2 = 24f;

    [Header("Track Visual")]
    [SerializeField] float trackThicknessMultiplier = 0.65f;

    [Header("Label Search & Layout")]
    [SerializeField] float labelGap = 6f;
    [SerializeField] float labelSearchDistance = 60f;
    [SerializeField] float labelSearchStep = 12f;
    [SerializeField] float labelConnectorThickness = 2f;

    [Header("Station Node Layout & Visual")]
    [SerializeField] float nodeSize = 44f;
    [SerializeField] float nodeEmphasizedScale = 1.5f;
    [SerializeField] float stationHaloSize = 56f;
    [SerializeField] int stationBadgeFontSize = 16;
    [SerializeField] float stationLabelExtraWidth = 12f;
    [SerializeField] float stationLabelHeight = 28f;
    [SerializeField] float stationBadgeExtraWidth = 12f;
    [SerializeField] float stationBadgeMinWidth = 160f;
    [SerializeField] float stationBadgeHeight = 24f;
    // 도시 이름 아래 판매 칩: 글자 폭 + 좌우 여백 합, 칩 사이 간격

    [Header("Station Font Sizes")]
    [SerializeField] int stationFontSizeEmphasized = 24;
    [SerializeField] int stationFontSizeMajor = 22;
    [SerializeField] float stationFontSizeThreshold = 0.8f;
    [SerializeField] int stationFontSizeSmall = 18;
    [SerializeField] int stationFontSizeNormal = 22;

    [Header("Map Label Priorities & Policies")]
    [SerializeField] int majorStationThreshold = 200;
    [SerializeField] float labelDetailMinZoom = 0.85f;
    // 이 배율보다 작으면 역 이름 대신 지역명을 보인다(현재·목표·의뢰 목적지·선택 역 이름은 남긴다).
    [SerializeField] float regionLabelMaxZoom = 0.85f;
    [SerializeField] int regionLabelFontSize = 20;
    [SerializeField] float regionLabelPaddingX = 16f;
    [SerializeField] float regionLabelHeight = 32f;
    // 지역 가운데에서 빈자리를 찾는 최대 거리·간격(화면 px)
    [SerializeField] float regionLabelSearchDistance = 240f;
    [SerializeField] float regionLabelSearchStep = 16f;
    [SerializeField] float regionBalanceBelowZoom = 1.0f;
    [SerializeField] int regionCrowdingPenalty = 180;
    [SerializeField] int labelPriorityCurrent = 10000;
    [SerializeField] int labelPrioritySelected = 9000;
    [SerializeField] int labelPriorityTarget = 500;
    [SerializeField] int labelPriorityConnected = 80;
    [SerializeField] int labelPriorityInterchange = 40;
    [SerializeField] int labelPriorityOther = 0;

    [Header("Map Route Priorities")]
    [SerializeField] int routePriorityCurrentSelected = 3;
    [SerializeField] int routePriorityCurrent = 2;
    [SerializeField] int routePrioritySelected = 1;
    [SerializeField] int routePriorityOther = 0;

    readonly List<Rect> occupied = new List<Rect>();
    readonly Dictionary<PuzzleMapNodeView, Rect> markerBounds = new Dictionary<PuzzleMapNodeView, Rect>();
    readonly List<(Vector2 start, Vector2 end)> labelLinks = new List<(Vector2 start, Vector2 end)>();
    readonly List<PuzzleMapNodeView> pendingLabels = new List<PuzzleMapNodeView>();
    readonly HashSet<PuzzleMapNodeView> requiredLabels = new HashSet<PuzzleMapNodeView>();
    bool requiredMarkersOnly;
    readonly Dictionary<string, int> regionLabelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    // 스크립트 재로드 뒤에도 확대가 적용되기 전의 두께를 유지한다.
    float[] trackThickness;
    PuzzleMapNodeView[] layoutNodes;
    RouteTimeVisual[] layoutRoutes;
    readonly HashSet<RouteTimeVisual> manualTimeLabels = new HashSet<RouteTimeVisual>();
    string currentCityId;
    string selectedCityId;
    float LabelGap => labelGap;
    static readonly Vector2[] LABEL_DIRECTIONS =
    {
        Vector2.up, Vector2.right, Vector2.down, Vector2.left,
        new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
    };
    Vector2[] TimeOffsets => new Vector2[]
    {
        Vector2.zero, new Vector2(0f, timeOffsetY), new Vector2(0f, -timeOffsetY2), new Vector2(timeOffsetX, 0f), new Vector2(-timeOffsetX2, 0f)
    };

    public int TimeFontSize => timeFontSize;
    public int MajorStationThreshold => majorStationThreshold;

    public int CalculateStationFontSize(float _zoom, bool _emphasized, bool _major)
    {
        return _emphasized ? stationFontSizeEmphasized
            : _major ? stationFontSizeMajor
            : _zoom < stationFontSizeThreshold ? stationFontSizeSmall : stationFontSizeNormal;
    }

    public NodeLayoutSettings GetNodeLayoutSettings()
    {
        return new NodeLayoutSettings
        {
            nodeSize = nodeSize,
            nodeEmphasizedScale = nodeEmphasizedScale,
            stationHaloSize = stationHaloSize,
            stationBadgeFontSize = stationBadgeFontSize,
            stationLabelExtraWidth = stationLabelExtraWidth,
            stationLabelHeight = stationLabelHeight,
            stationBadgeExtraWidth = stationBadgeExtraWidth,
            stationBadgeMinWidth = stationBadgeMinWidth,
            stationBadgeHeight = stationBadgeHeight,
            labelGap = labelGap,
            labelConnectorThickness = labelConnectorThickness,
            stationFontSizeEmphasized = stationFontSizeEmphasized,
            stationFontSizeMajor = stationFontSizeMajor,
            stationFontSizeThreshold = stationFontSizeThreshold,
            stationFontSizeSmall = stationFontSizeSmall,
            stationFontSizeNormal = stationFontSizeNormal,
            majorStationThreshold = majorStationThreshold,
            labelDetailMinZoom = labelDetailMinZoom,
            labelPriorityCurrent = labelPriorityCurrent,
            labelPrioritySelected = labelPrioritySelected,
            labelPriorityTarget = labelPriorityTarget,
            labelPriorityConnected = labelPriorityConnected,
            labelPriorityInterchange = labelPriorityInterchange,
            labelPriorityOther = labelPriorityOther
        };
    }

    internal PuzzleMapNodeView[] Nodes => nodes;

    internal void SetInteraction(bool _enabled)
    {
        navigation.enabled = _enabled;
    }

    internal void ShowCurrentLocation(PuzzleMapNodeView _node)
    {
        currentCityId = _node.CityId;
        navigation.SetCurrent(_node.transform as RectTransform);
        _node.transform.SetAsLastSibling();
    }

    internal void RefreshLayout()
    {
        navigation.RefreshLayout();
    }

    internal void HighlightCity(string _cityId)
    {
        selectedCityId = _cityId;
        RefreshLayout();
    }

    internal PuzzleMapNavigation Navigation => navigation;

    internal void FocusCity(string _cityId)
    {
        if (navigation)
        {
            navigation.FocusCity(_cityId);
        }
    }

    internal void FocusRegion(string _regionId)
    {
        if (navigation)
        {
            navigation.FocusRegion(_regionId);
        }
    }

    void PrepareLayout()
    {
        if (layoutNodes != null)
        {
            return;
        }
        layoutNodes = (PuzzleMapNodeView[])nodes.Clone();
        layoutRoutes = (RouteTimeVisual[])routeTimes.Clone();
        // UGUI는 형제 순서로 그린다. 복제된 배경이 글자 위를 덮지 않도록 층을 고정한다.
        foreach (var route in layoutRoutes)
        {
            if (route.plate)
            {
                route.plate.transform.SetAsLastSibling();
            }
        }
        foreach (var route in layoutRoutes)
        {
            route.label.transform.SetAsLastSibling();
            route.position = route.label.rectTransform.anchoredPosition;
        }
        // 지역명은 선로·역 표식 위에 그린다(판 → 글자 순).
        if (regionLabels != null)
        {
            foreach (var region in regionLabels)
            {
                region.plate.transform.SetAsLastSibling();
                region.label.transform.SetAsLastSibling();
            }
        }
    }

    void UpdateTrackThickness(float _zoom)
    {
        if (routeLines == null)
        {
            return;
        }

        if (trackThickness == null || trackThickness.Length != routeLines.Length)
        {
            trackThickness = new float[routeLines.Length];
            for (int i = 0; i < routeLines.Length; i++)
            {
                var track = routeLines[i];
                if (track)
                {
                    trackThickness[i] = track.rectTransform.sizeDelta.y;
                }
            }
        }

        for (int i = 0; i < routeLines.Length; i++)
        {
            var track = routeLines[i];
            if (!track)
            {
                continue;
            }

            Vector2 size = track.rectTransform.sizeDelta;
            size.y = trackThickness[i] * trackThicknessMultiplier / _zoom;
            track.rectTransform.sizeDelta = size;
            if (trackOutlines.TryGetValue(track, out var outline) && outline)
            {
                // 외곽선은 화면에서 일정한 두께가 되도록 배율로 나눈다. 끝도 그만큼 늘려 역 쪽 끝을 감싼다.
                float border = trackOutlineWidth / _zoom;
                float length = trackLengths.TryGetValue(track, out float baseLength) ? baseLength : size.x;
                outline.rectTransform.sizeDelta = new Vector2(length + border * 2f, size.y + border * 2f);
            }
        }
    }

    internal void UpdateMapLayout(float _zoom, RectTransform _viewport)
    {
        PrepareLayout();
        occupied.Clear();
        markerBounds.Clear();
        labelLinks.Clear();
        Rect bounds = _viewport.rect;
        bounds = Rect.MinMaxRect(bounds.xMin + viewportPaddingLeft, bounds.yMin + viewportPaddingBottom, bounds.xMax - viewportPaddingRight, bounds.yMax - viewportPaddingTop);
        var nodeSettings = GetNodeLayoutSettings();
        foreach (var node in layoutNodes)
        {
            // 전장의 안개로 숨긴 역은 배치·겹침 계산에서 뺀다.
            if (!node.gameObject.activeSelf)
            {
                continue;
            }
            node.PrepareLayout(_zoom, nodeSettings);
            markerBounds.Add(node, node.MarkerBounds(_viewport));
        }
        manualTimeLabels.Clear();
        if (_zoom >= timeLabelMinZoom)
        {
            foreach (var route in layoutRoutes)
            {
                if (!route.manualPosition || !IsReachableRoute(route))
                {
                    continue;
                }
                Vector2 size = PrepareTimeLabel(route, _zoom);
                Rect box = Box(_viewport.InverseTransformPoint(route.label.transform.position), size);
                if (Available(box, bounds))
                {
                    occupied.Add(box);
                    manualTimeLabels.Add(route);
                }
            }
        }
        pendingLabels.Clear();
        requiredLabels.Clear();
        regionLabelCounts.Clear();
        bool regionMode = _zoom < regionLabelMaxZoom;
        foreach (var node in layoutNodes)
        {
            node.HideLabel();
            if (!node.gameObject.activeSelf)
            {
                continue;
            }
            bool required = regionMode ? node.IsLabelEssential(selectedCityId) : node.IsLabelRequired(selectedCityId, nodeSettings);
            if (required)
            {
                requiredLabels.Add(node);
            }
            bool show = regionMode ? required : node.ShouldShowLabel(_zoom, selectedCityId, nodeSettings);
            if (show && bounds.Contains(node.MarkerPosition(_viewport)))
            {
                pendingLabels.Add(node);
            }
        }
        long regionPenalty = _zoom < regionBalanceBelowZoom
            ? regionCrowdingPenalty : 0;
        while (pendingLabels.Count > 0)
        {
            PuzzleMapNodeView best = null;
            long bestScore = long.MinValue;
            foreach (var candidate in pendingLabels)
            {
                regionLabelCounts.TryGetValue(candidate.RegionId, out int count);
                long score = candidate.LabelPriority(selectedCityId, nodeSettings) - count * regionPenalty;
                if (best == null || score > bestScore || (score == bestScore && string.CompareOrdinal(candidate.CityId, best.CityId) < 0))
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            pendingLabels.Remove(best);
            if (PlaceNodeLabel(best, bounds, _viewport) || (requiredLabels.Contains(best) && PlaceRequiredNodeLabel(best, bounds, _viewport)))
            {
                regionLabelCounts.TryGetValue(best.RegionId, out int count);
                regionLabelCounts[best.RegionId] = count + 1;
            }
        }
        LayoutRegionLabels(regionMode, _zoom, bounds, _viewport);

        UpdateTrackThickness(_zoom);
        Array.Sort(layoutRoutes, (_a, _b) =>
        {
            int priority = RoutePriority(_b).CompareTo(RoutePriority(_a));
            if (priority != 0)
            {
                return priority;
            }
            int from = string.CompareOrdinal(_a.fromCityId, _b.fromCityId);
            return from != 0 ? from : string.CompareOrdinal(_a.toCityId, _b.toCityId);
        });
        foreach (var route in layoutRoutes)
        {
            var label = route.label;
            if (!IsReachableRoute(route))
            {
                label.gameObject.SetActive(false);
                if (route.plate)
                {
                    route.plate.gameObject.SetActive(false);
                }
                continue;
            }
            Vector2 size = PrepareTimeLabel(route, _zoom);
            bool visible = route.manualPosition && manualTimeLabels.Contains(route);
            if (route.plate)
            {
                var plate = route.plate.rectTransform;
                plate.localScale = Vector3.one / _zoom;
                plate.sizeDelta = size;
                if (!route.manualPosition && _zoom >= timeLabelMinZoom)
                {
                    Vector2 origin = _viewport.InverseTransformPoint(label.transform.position);
                    foreach (var offset in TimeOffsets)
                    {
                        Rect candidate = Box(origin + offset, size);
                        if (Available(candidate, bounds))
                        {
                            label.rectTransform.anchoredPosition = route.position + offset / _zoom;
                            occupied.Add(candidate);
                            visible = true;
                            break;
                        }
                    }
                }
                plate.anchoredPosition = label.rectTransform.anchoredPosition;
                route.plate.gameObject.SetActive(visible);
            }
            label.gameObject.SetActive(visible);
        }
    }

    // 지역명은 그 지역 역 표식들의 가운데에서 바깥으로 넓혀 가며 역 표식·이름·연결선과 겹치지 않는 첫 자리에 둔다.
    // 역 이름(필수)을 먼저 놓은 뒤 호출한다. 자리가 없으면 겹쳐 그리지 않고 숨긴다.
    void LayoutRegionLabels(bool _visible, float _zoom, Rect _bounds, RectTransform _viewport)
    {
        if (regionLabels == null)
        {
            return;
        }
        var tables = TableManager.Instance;
        foreach (var region in regionLabels)
        {
            Vector2 center = Vector2.zero;
            int count = 0;
            if (_visible)
            {
                foreach (var node in layoutNodes)
                {
                    if (node.gameObject.activeSelf && tables.HasCity(node.CityId) &&
                        tables.CityPrice(node.CityId).regionId == region.regionId)
                    {
                        center += node.MarkerPosition(_viewport);
                        count++;
                    }
                }
            }
            var label = region.label;
            bool placed = false;
            if (count > 0)
            {
                center /= count;
                label.fontSize = regionLabelFontSize;
                label.enableAutoSizing = false;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                label.overflowMode = TMPro.TextOverflowModes.Overflow;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                Vector2 size = new Vector2(Mathf.Ceil(label.preferredWidth) + regionLabelPaddingX, regionLabelHeight);
                if (TryFindRegionLabelSpot(center, size, _bounds, out Rect spot))
                {
                    occupied.Add(spot);
                    Vector3 local = label.rectTransform.parent.InverseTransformPoint(_viewport.TransformPoint(spot.center));
                    foreach (var rect in new[] { label.rectTransform, region.plate.rectTransform })
                    {
                        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                        rect.localScale = Vector3.one / _zoom;
                        rect.sizeDelta = size;
                        rect.anchoredPosition = local;
                    }
                    placed = true;
                }
            }
            label.gameObject.SetActive(placed);
            region.plate.gameObject.SetActive(placed);
        }
    }

    bool TryFindRegionLabelSpot(Vector2 _center, Vector2 _size, Rect _bounds, out Rect _spot)
    {
        _spot = Box(_center, _size);
        if (Available(_spot, _bounds))
        {
            return true;
        }
        for (float distance = regionLabelSearchStep; distance <= regionLabelSearchDistance; distance += regionLabelSearchStep)
        {
            foreach (var direction in LABEL_DIRECTIONS)
            {
                _spot = Box(_center + direction.normalized * distance, _size);
                if (Available(_spot, _bounds))
                {
                    return true;
                }
            }
        }
        return false;
    }

    Vector2 PrepareTimeLabel(RouteTimeVisual _route, float _zoom)
    {
        var label = _route.label;
        label.rectTransform.localScale = Vector3.one / _zoom;
        label.rectTransform.anchoredPosition = _route.position;
        label.fontSize = timeFontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        Vector2 size = new Vector2(Mathf.Ceil(label.preferredWidth) + timeLabelPaddingX, timeLabelHeight);
        label.rectTransform.sizeDelta = size;
        return size;
    }

    // 줌 아웃으로 역 표식이 빽빽해지면 빈자리가 없다. 필수 이름은 다른 이름과 겹치지 않는 선에서 일반 역 표식 위에 놓는다.
    bool PlaceRequiredNodeLabel(PuzzleMapNodeView _node, Rect _bounds, RectTransform _viewport)
    {
        requiredMarkersOnly = true;
        try
        {
            return PlaceNodeLabel(_node, _bounds, _viewport);
        }
        finally
        {
            requiredMarkersOnly = false;
        }
    }

    bool BlocksLabel(PuzzleMapNodeView _markerOwner)
    {
        return !requiredMarkersOnly || requiredLabels.Contains(_markerOwner);
    }

    bool PlaceNodeLabel(PuzzleMapNodeView _node, Rect _bounds, RectTransform _viewport)
    {
        Vector2 position = _node.MarkerPosition(_viewport);
        if (!_bounds.Contains(position))
        {
            _node.HideLabel();
            return false;
        }
        float step = labelSearchStep;
        float distance = labelSearchDistance;
        // 직전 방향(최초에는 프리팹 배치 방향)의 공간을 먼저 찾아 방향 전환을 최소화한다.
        for (int i = 0; i <= LABEL_DIRECTIONS.Length; i++)
        {
            Vector2 direction = i == 0 ? _node.PreferredLabelDirection : LABEL_DIRECTIONS[i - 1];
            if (i > 0 && direction == _node.PreferredLabelDirection)
            {
                continue;
            }
            for (float extra = 0f; extra <= distance; extra += step)
            {
                Vector2 normal = direction.normalized;
                float radius = BoundaryDistance(markerBounds[_node], position, normal);
                Vector2 offset = Vector2.Scale(direction, _node.LabelSize * 0.5f)
                    + normal * (radius + LabelGap * 2f + extra);
                Rect candidate = Box(position + offset, _node.LabelSize);
                if (!Available(candidate, _bounds))
                {
                    continue;
                }
                Vector2 end = new Vector2(Mathf.Clamp(position.x, candidate.xMin, candidate.xMax),
                    Mathf.Clamp(position.y, candidate.yMin, candidate.yMax));
                Vector2 towardLabel = (end - position).normalized;
                Vector2 start = position + towardLabel *
                    (BoundaryDistance(markerBounds[_node], position, towardLabel) + LabelGap * 0.5f);
                if (!ConnectorAvailable(_node, start, end))
                {
                    continue;
                }
                occupied.Add(candidate);
                labelLinks.Add((start, end));
                _node.PlaceLabel(candidate, start, end, _viewport, direction, labelConnectorThickness);
                return true;
            }
        }
        // 우선순위가 높아도 겹쳐 표시하지 않는다. 역 표식은 남기고 호버한 역을 우선 배치한다.
        _node.HideLabel();
        return false;
    }

    static float BoundaryDistance(Rect _rect, Vector2 _origin, Vector2 _direction)
    {
        float x = _direction.x > 0f ? (_rect.xMax - _origin.x) / _direction.x
            : _direction.x < 0f ? (_rect.xMin - _origin.x) / _direction.x : float.PositiveInfinity;
        float y = _direction.y > 0f ? (_rect.yMax - _origin.y) / _direction.y
            : _direction.y < 0f ? (_rect.yMin - _origin.y) / _direction.y : float.PositiveInfinity;
        return Mathf.Min(x, y);
    }

    bool ConnectorAvailable(PuzzleMapNodeView _owner, Vector2 _start, Vector2 _end)
    {
        foreach (var marker in markerBounds)
        {
            if (marker.Key != _owner && BlocksLabel(marker.Key) && SegmentOverlaps(_start, _end, Padded(marker.Value)))
            {
                return false;
            }
        }
        foreach (var rect in occupied)
        {
            if (SegmentOverlaps(_start, _end, Padded(rect)))
            {
                return false;
            }
        }
        foreach (var link in labelLinks)
        {
            if (LinksCross(_start, _end, link.start, link.end))
            {
                return false;
            }
        }
        return true;
    }

    static bool SegmentOverlaps(Vector2 _start, Vector2 _end, Rect _rect)
    {
        Vector2 delta = _end - _start;
        float first = 0f;
        float last = 1f;
        return ClipAxis(_start.x, delta.x, _rect.xMin, _rect.xMax, ref first, ref last)
            && ClipAxis(_start.y, delta.y, _rect.yMin, _rect.yMax, ref first, ref last);
    }

    static bool ClipAxis(float _origin, float _delta, float _min, float _max, ref float _first, ref float _last)
    {
        if (Mathf.Approximately(_delta, 0f))
        {
            return _origin >= _min && _origin <= _max;
        }
        float a = (_min - _origin) / _delta;
        float b = (_max - _origin) / _delta;
        _first = Mathf.Max(_first, Mathf.Min(a, b));
        _last = Mathf.Min(_last, Mathf.Max(a, b));
        return _first <= _last;
    }

    static bool LinksCross(Vector2 _a, Vector2 _b, Vector2 _c, Vector2 _d)
    {
        Vector2 first = _b - _a;
        Vector2 second = _d - _c;
        float cross = first.x * second.y - first.y * second.x;
        if (Mathf.Approximately(cross, 0f))
        {
            Vector2 separation = _c - _a;
            if (!Mathf.Approximately(separation.x * first.y - separation.y * first.x, 0f))
            {
                return false;
            }
            return SegmentOverlaps(_a, _b, Rect.MinMaxRect(Mathf.Min(_c.x, _d.x), Mathf.Min(_c.y, _d.y),
                Mathf.Max(_c.x, _d.x), Mathf.Max(_c.y, _d.y)));
        }
        Vector2 offset = _c - _a;
        float t = (offset.x * second.y - offset.y * second.x) / cross;
        float u = (offset.x * first.y - offset.y * first.x) / cross;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    bool IsReachableRoute(RouteTimeVisual _route)
    {
        if (string.IsNullOrEmpty(currentCityId))
        {
            return false;
        }
        var tables = TableManager.Instance;
        return _route.fromCityId == currentCityId
            ? tables.HasRoute(currentCityId, _route.toCityId)
            : _route.toCityId == currentCityId && tables.HasRoute(currentCityId, _route.fromCityId);
    }

    int RoutePriority(RouteTimeVisual _route)
    {
        bool current = _route.fromCityId == currentCityId || _route.toCityId == currentCityId;
        bool selected = _route.fromCityId == selectedCityId || _route.toCityId == selectedCityId;
        return current && selected ? routePriorityCurrentSelected
            : current ? routePriorityCurrent
            : selected ? routePrioritySelected
            : routePriorityOther;
    }

    bool Available(Rect _candidate, Rect _bounds)
    {
        if (_candidate.xMin < _bounds.xMin || _candidate.yMin < _bounds.yMin ||
            _candidate.xMax > _bounds.xMax || _candidate.yMax > _bounds.yMax)
        {
            return false;
        }
        Rect padded = Padded(_candidate);
        foreach (var marker in markerBounds)
        {
            if (BlocksLabel(marker.Key) && padded.Overlaps(marker.Value))
            {
                return false;
            }
        }
        foreach (var rect in occupied)
        {
            if (padded.Overlaps(rect))
            {
                return false;
            }
        }
        foreach (var link in labelLinks)
        {
            if (SegmentOverlaps(link.start, link.end, padded))
            {
                return false;
            }
        }
        return true;
    }

    Rect Padded(Rect _rect)
    {
        return Rect.MinMaxRect(_rect.xMin - LabelGap, _rect.yMin - LabelGap,
            _rect.xMax + LabelGap, _rect.yMax + LabelGap);
    }

    static Rect Box(Vector2 _center, Vector2 _size)
    {
        return new Rect(_center - _size * 0.5f, _size);
    }

    protected override void Awake()
    {
        title.text = Presentation.PuzzleMapTitle;
        guide.text = Presentation.PuzzleMapGuideText;
        closeLabel.text = Presentation.PuzzleMapClose;
        title.color = guide.color = information.color = closeLabel.color = Presentation.PuzzleHeading;
        background.color = Presentation.PuzzleBackground;
        if (lines != null)
        {
            foreach (var line in lines)
            {
                foreach (var segment in line.segments)
                {
                    segment.color = Presentation.PuzzleMapLineColor(line.regionId);
                }
            }
        }
        var tables = TableManager.Instance;
        tables.RequireValid();
        foreach (var region in regions)
        {
            region.label.text = Presentation.PuzzleMapRegionLabel(tables.Region(region.regionId));
            region.label.color = Presentation.PuzzleMapLineColor(region.regionId);
            region.area.color = Presentation.PuzzleMapLineColor(region.regionId);
        }
        if (regionLabels != null)
        {
            foreach (var region in regionLabels)
            {
                region.label.text = tables.Region(region.regionId).displayName;
                region.label.color = Presentation.PuzzleMapLineColor(region.regionId);
                region.label.raycastTarget = false;
                region.plate.color = Presentation.MapLabelPlate;
                region.plate.raycastTarget = false;
            }
        }
        foreach (var routeTime in routeTimes)
        {
            routeTime.label.color = Presentation.PuzzleHeading;
        }
        RefreshRouteFares(0);

        if (routeLines != null)
        {
            // 선 조각 이름의 번호(Track_{railIndex}_{구간})는 레이아웃의 railIndex라 routeTimes 배열 순번과 다를 수 있다.
            // 같은 노선의 시간 라벨 이름(Time_{railIndex})으로 railIndex → routeTimes 순번을 잇고, 강조는 순번으로 찾는다.
            var routeByRail = new Dictionary<int, int>();
            for (int i = 0; i < routeTimes.Length; i++)
            {
                if (routeTimes[i]?.label && TryParseIndex(routeTimes[i].label.name, "Time_", out int railIndex))
                {
                    routeByRail[railIndex] = i;
                }
            }
            foreach (var track in routeLines)
            {
                if (!track) continue;
                originalTrackColors[track] = track.color;
                baseTrackColors[track] = track.color;
                trackLengths[track] = track.rectTransform.sizeDelta.x;
                var mapContent = track.transform.parent ? track.transform.parent.parent : null;
                var outlineParent = mapContent ? mapContent.Find("SubwayMapTrackOutlines") : null;
                var outline = outlineParent ? outlineParent.Find("TrackOutline_" + track.name.Substring("Track_".Length)) : null;
                if (outline && outline.TryGetComponent<Image>(out var outlineImage))
                {
                    outlineImage.color = Presentation.PuzzleMapTrackOutline;
                    outlineImage.raycastTarget = false;
                    trackOutlines[track] = outlineImage;
                }
                if (TryParseIndex(track.name, "Track_", out int railIndex) &&
                    routeByRail.TryGetValue(railIndex, out int routeIndex))
                {
                    if (!routeTrackImages.TryGetValue(routeIndex, out var list))
                    {
                        list = new List<Image>();
                        routeTrackImages[routeIndex] = list;
                    }
                    list.Add(track);
                }
            }
        }

        base.Awake();
    }

    // "접두사{번호}" 또는 "접두사{번호}_{구간}"에서 번호를 읽는다.
    static bool TryParseIndex(string _name, string _prefix, out int _index)
    {
        _index = -1;
        if (string.IsNullOrEmpty(_name) || !_name.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string rest = _name.Substring(_prefix.Length);
        int cut = rest.IndexOf('_');
        return int.TryParse(cut >= 0 ? rest.Substring(0, cut) : rest, out _index);
    }

    // 노선 운임 라벨을 실제 차감액으로 다시 쓴다. 적재 칸 요금이 들어가므로 기차 화물이 바뀌면 다시 불러야 한다.
    internal void RefreshRouteFares(int _trainCells)
    {
        if (routeTimes == null)
        {
            return;
        }
        var tables = TableManager.Instance;
        foreach (var routeTime in routeTimes)
        {
            if (routeTime?.label && tables.TryGetRoute(routeTime.fromCityId, routeTime.toCityId, out var route))
            {
                routeTime.label.text = Presentation.PuzzleMapRouteFare(route, tables.PuzzleRoute, _trainCells);
            }
        }
    }

    internal void ShowTownTooltip(string _toCityId, PuzzleTravelResult _eval, bool _isCurrent, bool _isConnected,
        RectTransform _nodeRt, PuzzleInventoryService _session)
    {
        if (!travelPreviewTooltip || !_nodeRt || _session == null)
        {
            HideTravelPreview();
            return;
        }

        Vector2 screenPosition = NodeScreenPosition(_nodeRt);
        Color titleColor = Presentation.EntityPreviewTitleColor(EntityLinkKind.City, _toCityId);
        var tables = TableManager.Instance;
        bool visited = _session.HasVisitedCity(_toCityId);
        PuzzleCityTradePreferencesData? tradePrefs = visited && tables.HasCity(_toCityId)
            ? Presentation.GetCityTradePreferences(tables.CityPrice(_toCityId))
            : null;
        Presentation.BuildTownTooltipRumorLines(_toCityId, _session, tooltipTradeLines, out string rumorHeader);
        string headerStatus = Presentation.BuildTownHeaderStatus(_toCityId, _session);

        if (_isConnected && !_isCurrent)
        {
            string destName = tables.HasCity(_toCityId) ? tables.CityPrice(_toCityId).displayName : _toCityId;
            BuildTravelStatRows(_toCityId, _eval, _session);
            BuildTownSummaryLines(_toCityId, _eval, _isConnected, _session);
            travelPreviewTooltip.ShowEntityInfo($"{destName}까지", titleColor, tooltipInfoLines, screenPosition,
                _statRows: tooltipStatRows, _effectHeader: rumorHeader, _effectLines: tooltipTradeLines,
                _tradePreferences: tradePrefs, _headerStatus: headerStatus);
            return;
        }

        Presentation.EntityPreview(EntityLinkKind.City, _toCityId, _session, out string title, tooltipStatRows, tooltipInfoLines,
            _includeCurrentLine: false);
        if (!_isCurrent && !_isConnected)
        {
            tooltipInfoLines.Add("직행 선로 없음");
        }
        travelPreviewTooltip.ShowEntityInfo(title, titleColor, tooltipInfoLines, screenPosition,
            _statRows: tooltipStatRows, _effectHeader: rumorHeader, _effectLines: tooltipTradeLines,
            _tradePreferences: tradePrefs, _headerStatus: headerStatus);
    }

    Vector2 NodeScreenPosition(RectTransform _nodeRt)
    {
        var rootCanvas = GetComponentInParent<Canvas>(true);
        Camera cam = rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, _nodeRt.position);
    }

    void BuildTravelStatRows(string _toCityId, PuzzleTravelResult _eval, PuzzleInventoryService _session)
    {
        tooltipStatRows.Clear();
        if (Presentation.TryGetCityRegionRow(_toCityId, out var regionRow))
        {
            tooltipStatRows.Add(regionRow);
        }
        tooltipStatRows.Add(("소요시간", Presentation.TurnSpan(_eval.TravelHours)));

        string fareText = $"<color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipRed)}>{_eval.Cost:N0}$</color>";
        if (_eval.IsSuccess || _eval.Failure == PuzzleTravelFailure.InsufficientFunds)
        {
            int costPerCell = TableManager.Instance.PuzzleRoute.costPerCell;
            fareText += $"\n<size=70%>(기본 {_eval.BaseCost:N0} + 거리 {_eval.DistanceCost:N0} + {_eval.Cells:N0}칸×{costPerCell:N0})</size>";
        }
        tooltipStatRows.Add(("운임비", fareText));

        string arrivalStr = Presentation.GameTimestamp(_session.ElapsedTime + _eval.TravelHours);
        if (_eval.ArrivalIsNight)
        {
            arrivalStr += $" <color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipRed)}>(야간)</color>";
        }
        tooltipStatRows.Add(("도착 예정", arrivalStr));
        if (_session.Time != null && _session.Time.HasDeadline)
        {
            long remaining = _session.Time.DeadlineHours - (_session.ElapsedTime + _eval.TravelHours);
            string deadlineStr;
            // 영업 기간이 끝나는 시각에 도착하는 이동은 허용하므로 남은 0은 초과가 아니다.
            if (remaining >= 0)
            {
                string formatted = Presentation.TimeDeadlineValue(remaining);
                deadlineStr = Presentation.TimeDeadlineWarning(remaining)
                    ? $"<color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipGold)}>{formatted}</color>"
                    : formatted;
            }
            else
            {
                deadlineStr = $"<color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipRed)}>기한 초과!</color>";
            }
            tooltipStatRows.Add(("남은 기한", deadlineStr));
        }
    }

    void BuildTownSummaryLines(string _toCityId, PuzzleTravelResult _eval, bool _isConnected, PuzzleInventoryService _session)
    {
        tooltipInfoLines.Clear();
        bool visited = _session.HasVisitedCity(_toCityId);
        if (!visited)
        {
            tooltipInfoLines.Add("미방문 마을");
        }
        if (!_eval.IsSuccess && _eval.Failure != PuzzleTravelFailure.InsufficientFunds)
        {
            tooltipInfoLines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipRed)}>{Presentation.PuzzleTravelStatus(_eval)}</color>");
        }
    }

    internal void HideTravelPreview()
    {
        if (travelPreviewTooltip)
        {
            travelPreviewTooltip.Close();
        }
    }

    internal void HighlightRoute(string _cityA, string _cityB)
    {
        ClearRouteHighlight();
        if (string.IsNullOrEmpty(_cityA) || string.IsNullOrEmpty(_cityB) || routeTimes == null)
        {
            ApplyTrackOrder();
            return;
        }

        for (int i = 0; i < routeTimes.Length; i++)
        {
            var r = routeTimes[i];
            if (r == null) continue;
            if ((string.Equals(r.fromCityId, _cityA, StringComparison.Ordinal) && string.Equals(r.toCityId, _cityB, StringComparison.Ordinal)) ||
                (string.Equals(r.fromCityId, _cityB, StringComparison.Ordinal) && string.Equals(r.toCityId, _cityA, StringComparison.Ordinal)))
            {
                highlightedRouteIndex = i;
                if (routeTrackImages.TryGetValue(i, out var imgs))
                {
                    Color highlightColor = Presentation.PuzzleMapCurrent;
                    foreach (var img in imgs)
                    {
                        if (img)
                        {
                            img.color = highlightColor;
                        }
                    }
                }
                break;
            }
        }
        ApplyTrackOrder();
    }

    internal void ClearRouteHighlight()
    {
        if (highlightedRouteIndex >= 0)
        {
            if (routeTrackImages.TryGetValue(highlightedRouteIndex, out var imgs))
            {
                bool inPath = highlightedPathIndices.Contains(highlightedRouteIndex);
                Color highlightColor = Presentation.PuzzleMapCurrent;
                foreach (var img in imgs)
                {
                    if (img)
                    {
                        if (inPath)
                        {
                            img.color = highlightColor;
                        }
                        else if (baseTrackColors.TryGetValue(img, out var baseColor))
                        {
                            img.color = baseColor;
                        }
                    }
                }
            }
            highlightedRouteIndex = -1;
        }
        ApplyTrackOrder();
    }

    internal void HighlightPath(IReadOnlyList<string> _cities)
    {
        ClearPathHighlight();
        if (_cities == null || _cities.Count < 2 || routeTimes == null)
        {
            ApplyTrackOrder();
            return;
        }

        Color highlightColor = Presentation.PuzzleMapCurrent;
        for (int c = 0; c < _cities.Count - 1; c++)
        {
            string cityA = _cities[c];
            string cityB = _cities[c + 1];
            if (string.IsNullOrEmpty(cityA) || string.IsNullOrEmpty(cityB))
            {
                continue;
            }

            for (int i = 0; i < routeTimes.Length; i++)
            {
                var r = routeTimes[i];
                if (r == null)
                {
                    continue;
                }
                if ((string.Equals(r.fromCityId, cityA, StringComparison.Ordinal) && string.Equals(r.toCityId, cityB, StringComparison.Ordinal)) ||
                    (string.Equals(r.fromCityId, cityB, StringComparison.Ordinal) && string.Equals(r.toCityId, cityA, StringComparison.Ordinal)))
                {
                    highlightedPathIndices.Add(i);
                    if (routeTrackImages.TryGetValue(i, out var imgs))
                    {
                        foreach (var img in imgs)
                        {
                            if (img)
                            {
                                img.color = highlightColor;
                            }
                        }
                    }
                    break;
                }
            }
        }
        ApplyTrackOrder();
    }

    // 강조 중인 선분(경로 → 호버 노선 순)만 맨 위로 올리고, 나머지 올렸던 선분은 원래 순서로 되돌린다.
    void ApplyTrackOrder()
    {
        if (raisedTrackSiblings.Count > 0)
        {
            var restore = new List<KeyValuePair<Image, int>>(raisedTrackSiblings);
            restore.Sort((_a, _b) => _a.Value.CompareTo(_b.Value));
            foreach (var pair in restore)
            {
                if (pair.Key)
                {
                    pair.Key.transform.SetSiblingIndex(pair.Value);
                }
            }
            raisedTrackSiblings.Clear();
        }
        // 모두 원래 순서인 상태에서 올릴 선분의 위치를 먼저 기록한 뒤 올린다(되돌릴 때 위치 오름차순으로 복원).
        var raise = new List<Image>();
        foreach (int index in highlightedPathIndices)
        {
            CollectTracks(index, raise);
        }
        if (highlightedRouteIndex >= 0)
        {
            CollectTracks(highlightedRouteIndex, raise);
        }
        foreach (var img in raise)
        {
            if (!raisedTrackSiblings.ContainsKey(img))
            {
                raisedTrackSiblings.Add(img, img.transform.GetSiblingIndex());
            }
        }
        foreach (var img in raise)
        {
            img.transform.SetAsLastSibling();
        }
    }

    void CollectTracks(int _routeIndex, List<Image> _into)
    {
        if (!routeTrackImages.TryGetValue(_routeIndex, out var imgs))
        {
            return;
        }
        foreach (var img in imgs)
        {
            if (img)
            {
                _into.Add(img);
            }
        }
    }

    // 전장의 안개: 모르는 노선의 선분을 숨긴다. 노선마다 선분 묶음은 Awake에서 시간 라벨 이름으로 이어 두었다.
    // 달려 본 노선은 지역 색 그대로 밝게, 아는데 달려 보지 않은 노선은 짙은 회색으로 그린다. 강조 중인 선분은 강조색을 유지한다.
    // 외곽선 조각도 노선과 함께 켜고 끈다.
    internal void SetKnownRoutes(Func<string, string, bool> _isKnown, Func<string, string, bool> _isTraveled)
    {
        Color untraveled = Presentation.PuzzleMapUntraveledTrack;
        for (int i = 0; i < routeTimes.Length; i++)
        {
            if (routeTimes[i] == null || !routeTrackImages.TryGetValue(i, out var images))
            {
                continue;
            }
            bool known = _isKnown(routeTimes[i].fromCityId, routeTimes[i].toCityId);
            bool traveled = _isTraveled(routeTimes[i].fromCityId, routeTimes[i].toCityId);
            bool highlighted = i == highlightedRouteIndex || highlightedPathIndices.Contains(i);
            foreach (var image in images)
            {
                if (!image)
                {
                    continue;
                }
                image.enabled = known;
                if (trackOutlines.TryGetValue(image, out var outline) && outline)
                {
                    outline.enabled = known;
                }
                Color baseColor = untraveled;
                if (traveled && originalTrackColors.TryGetValue(image, out var original))
                {
                    baseColor = original;
                }
                baseTrackColors[image] = baseColor;
                if (!highlighted)
                {
                    image.color = baseColor;
                }
            }
        }
    }

    // 전장의 안개 층을 다시 그린다. 고지도 원본을 칸 단위(점 필터)로 덮고, 들른 역 주변과 달려 본 노선 양옆만 걷어 낸다.
    // 달려 보지 않은 노선·이웃 역·위치만 아는 역(핵심 도시·소문·의뢰)은 걷지 않고 표식·선만 안개 위에 그린다.
    internal void ShowFog(Func<string, bool> _isExplored, Func<string, string, bool> _isTraveled)
    {
        if (!fogOfWar || !oldMapTexture)
        {
            return;
        }
        var fogRect = fogOfWar.rectTransform;
        Rect rect = fogRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }
        fogHoles.Clear();
        foreach (var node in nodes)
        {
            if (!node)
            {
                continue;
            }
            if (!_isExplored(node.CityId))
            {
                continue;
            }
            Vector2 local = fogRect.InverseTransformPoint(node.transform.position);
            fogHoles.Add(new Vector3(local.x, local.y, fogExploredRadius));
        }
        fogSegments.Clear();
        for (int i = 0; i < routeTimes.Length; i++)
        {
            if (routeTimes[i] == null || !_isTraveled(routeTimes[i].fromCityId, routeTimes[i].toCityId) ||
                !routeTrackImages.TryGetValue(i, out var images))
            {
                continue;
            }
            foreach (var image in images)
            {
                if (!image)
                {
                    continue;
                }
                // 선분 이미지는 가로 길이 방향으로 놓여 있다. 양 끝 중앙을 안개 층 좌표로 옮긴다.
                var segment = image.rectTransform;
                Rect r = segment.rect;
                Vector2 from = fogRect.InverseTransformPoint(segment.TransformPoint(new Vector3(r.xMin, r.center.y, 0f)));
                Vector2 to = fogRect.InverseTransformPoint(segment.TransformPoint(new Vector3(r.xMax, r.center.y, 0f)));
                fogSegments.Add((from, to));
            }
        }

        int width = oldMapTexture.width;
        int height = oldMapTexture.height;
        if (!fogTexture || fogTexture.width != width || fogTexture.height != height)
        {
            if (fogTexture)
            {
                Destroy(fogTexture);
            }
            fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "MapFogOfWar"
            };
            fogPixels = oldMapTexture.GetPixels32();
        }

        float softness = Mathf.Max(1f, fogEdgeSoftness);
        int steps = Mathf.Max(1, fogEdgeSteps);
        for (int y = 0; y < height; y++)
        {
            float py = rect.yMin + (y + 0.5f) / height * rect.height;
            for (int x = 0; x < width; x++)
            {
                float px = rect.xMin + (x + 0.5f) / width * rect.width;
                float clear = 0f;
                for (int i = 0; i < fogHoles.Count; i++)
                {
                    var hole = fogHoles[i];
                    float dx = px - hole.x;
                    float dy = py - hole.y;
                    float open = 1f - Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - hole.z) / softness);
                    if (open > clear)
                    {
                        clear = open;
                    }
                }
                var pixelPoint = new Vector2(px, py);
                for (int i = 0; i < fogSegments.Count && clear < 1f; i++)
                {
                    float distance = DistanceToSegment(pixelPoint, fogSegments[i].From, fogSegments[i].To);
                    float open = 1f - Mathf.Clamp01((distance - fogRouteRadius) / softness);
                    if (open > clear)
                    {
                        clear = open;
                    }
                }
                // 칸 단위 도트 경계: 걷힘 정도를 몇 단계로만 나눈다.
                float stepped = Mathf.Floor(clear * steps + 0.5f) / steps;
                int index = y * width + x;
                var pixel = fogPixels[index];
                pixel.a = (byte)Mathf.RoundToInt(255f * (1f - stepped));
                fogPixels[index] = pixel;
            }
        }
        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false);
        fogOfWar.texture = fogTexture;
        fogOfWar.color = Color.white;
        fogOfWar.raycastTarget = false;
    }

    static float DistanceToSegment(Vector2 _point, Vector2 _from, Vector2 _to)
    {
        Vector2 line = _to - _from;
        float lengthSquared = line.sqrMagnitude;
        float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(_point - _from, line) / lengthSquared) : 0f;
        return Vector2.Distance(_point, _from + line * t);
    }

    void OnDestroy()
    {
        if (fogTexture)
        {
            Destroy(fogTexture);
        }
    }

    internal void ClearPathHighlight()
    {
        if (highlightedPathIndices.Count == 0)
        {
            ApplyTrackOrder();
            return;
        }

        foreach (int index in highlightedPathIndices)
        {
            if (index == highlightedRouteIndex)
            {
                continue;
            }
            if (routeTrackImages.TryGetValue(index, out var imgs))
            {
                foreach (var img in imgs)
                {
                    if (img && baseTrackColors.TryGetValue(img, out var baseColor))
                    {
                        img.color = baseColor;
                    }
                }
            }
        }
        highlightedPathIndices.Clear();
        ApplyTrackOrder();
    }

    internal void ShowGuide(string _text)
    {
        if (guide)
        {
            guide.text = _text;
        }
    }

    internal void ShowInformation(string _text)
    {
        tradePreferences?.Hide();
        if (questInformation)
        {
            questInformation.text = string.Empty;
            questInformation.gameObject.SetActive(false);
        }
        information.richText = true;
        information.text = _text;
    }

    internal void ShowVisitedInformation(string _headingText, PuzzleCityTradePreferencesData _tradePreferences, string _questText)
    {
        information.richText = true;
        information.text = _headingText;
        tradePreferences?.Show(_tradePreferences);
        if (questInformation)
        {
            bool hasQuest = !string.IsNullOrEmpty(_questText);
            questInformation.gameObject.SetActive(hasQuest);
            if (hasQuest)
            {
                questInformation.richText = true;
                questInformation.text = _questText;
            }
        }
    }

    protected override void OnShow()
    {
        HideTravelPreview();
        ShowInformation(Presentation.PuzzleMapHint);
        navigation.Open();
    }

    protected override void OnHide()
    {
        ShowInformation(string.Empty);
        HideTravelPreview();
        ClearRouteHighlight();
        ClearPathHighlight();
    }
}
