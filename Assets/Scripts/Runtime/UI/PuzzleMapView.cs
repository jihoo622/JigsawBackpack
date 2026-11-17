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

    [Header("Town Tooltip")]
    [SerializeField] PuzzleItemTooltipView travelPreviewTooltip;

    readonly List<string> tooltipInfoLines = new List<string>();
    readonly List<(string label, string value)> tooltipStatRows = new List<(string label, string value)>();

    readonly Dictionary<int, List<Image>> routeTrackImages = new Dictionary<int, List<Image>>();
    readonly Dictionary<Image, Color> originalTrackColors = new Dictionary<Image, Color>();
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
                    if (tables.HasCity(node.CityId) && tables.CityPrice(node.CityId).regionId == region.regionId)
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
            if (tables.TryGetRoute(routeTime.fromCityId, routeTime.toCityId, out var route))
            {
                routeTime.label.text = Presentation.PuzzleMapTime(PuzzleTimeManager.TravelHours(route, tables.PuzzleTime));
            }
            routeTime.label.color = Presentation.PuzzleHeading;
        }

        if (routeLines != null)
        {
            foreach (var track in routeLines)
            {
                if (!track) continue;
                originalTrackColors[track] = track.color;
                string tName = track.name;
                if (tName.StartsWith("Track_", StringComparison.Ordinal))
                {
                    var parts = tName.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int rIdx))
                    {
                        if (!routeTrackImages.TryGetValue(rIdx, out var list))
                        {
                            list = new List<Image>();
                            routeTrackImages[rIdx] = list;
                        }
                        list.Add(track);
                    }
                }
            }
        }

        base.Awake();
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

        if (_isConnected && !_isCurrent)
        {
            var tables = TableManager.Instance;
            string destName = tables.HasCity(_toCityId) ? tables.CityPrice(_toCityId).displayName : _toCityId;
            BuildTravelStatRows(_toCityId, _eval, _session);
            BuildTownSummaryLines(_toCityId, _eval, _isConnected, _session);
            travelPreviewTooltip.ShowTravelPreview($"{destName}까지", titleColor, tooltipStatRows, tooltipInfoLines, screenPosition);
            return;
        }

        Presentation.EntityPreview(EntityLinkKind.City, _toCityId, _session, out string title, tooltipStatRows, tooltipInfoLines);
        if (!_isCurrent && !_isConnected)
        {
            tooltipInfoLines.Add("직행 선로 없음");
        }
        travelPreviewTooltip.ShowEntityInfo(title, titleColor, tooltipInfoLines, screenPosition, _statRows: tooltipStatRows);
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
        tooltipStatRows.Add(("소요시간", Presentation.PuzzleMapTime(_eval.TravelHours)));

        string fareText = $"<color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipRed)}>{_eval.Cost:N0}$</color>";
        tooltipStatRows.Add(("운임비", fareText));

        long arrivalTime = _session.ElapsedTime + _eval.TravelHours;
        string arrivalStr = Presentation.GameTimestamp(arrivalTime);
        if (_eval.ArrivalIsNight)
        {
            arrivalStr += $" <color=#{ColorUtility.ToHtmlStringRGB(Presentation.PuzzleTooltipRed)}>(야간)</color>";
        }
        tooltipStatRows.Add(("도착 예정", arrivalStr));
    }

    void BuildTownSummaryLines(string _toCityId, PuzzleTravelResult _eval, bool _isConnected, PuzzleInventoryService _session)
    {
        tooltipInfoLines.Clear();
        if (_session.Time != null && _session.Time.HasDeadline)
        {
            long arrivalTime = _session.ElapsedTime + _eval.TravelHours;
            long remaining = _session.Time.DeadlineHours - arrivalTime;
            string deadlineStr;
            if (remaining > 0)
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
            tooltipInfoLines.Add($"남은 기한: {deadlineStr}");
        }
        bool visited = _session.HasVisitedCity(_toCityId);
        tooltipInfoLines.Add(visited ? "방문한 마을" : "미방문 마을");
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
                        else if (originalTrackColors.TryGetValue(img, out var orig))
                        {
                            img.color = orig;
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
                    if (img && originalTrackColors.TryGetValue(img, out var orig))
                    {
                        img.color = orig;
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
