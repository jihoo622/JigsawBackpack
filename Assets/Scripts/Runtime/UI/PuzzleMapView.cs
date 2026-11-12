using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleMapView : PanelBase
{
    [System.Serializable]
    sealed class RegionVisual
    {
        public string regionId;
        public Text label;
        public Image area;
    }

    [System.Serializable]
    sealed class RouteTimeVisual
    {
        public string fromCityId, toCityId;
        public Text label;
        public Image plate;
        [NonSerialized] public Vector2 position;
    }

    [System.Serializable]
    sealed class LineVisual
    {
        public string regionId;
        public Image[] segments;
    }

    [SerializeField] Text title;
    [SerializeField] Text guide;
    [SerializeField] Text information;
    [SerializeField] Text openLabel;
    [SerializeField] Text closeLabel;
    [SerializeField] Image background;
    [SerializeField] Image[] routeLines;
    [SerializeField] PuzzleMapNodeView[] nodes;
    [SerializeField] RegionVisual[] regions;
    [SerializeField] RouteTimeVisual[] routeTimes;
    [SerializeField] LineVisual[] lines;
    [SerializeField] PuzzleMapNavigation navigation;

    readonly List<Rect> occupied = new List<Rect>();
    readonly Dictionary<PuzzleMapNodeView, Rect> markerBounds = new Dictionary<PuzzleMapNodeView, Rect>();
    readonly List<(Vector2 start, Vector2 end)> labelLinks = new List<(Vector2 start, Vector2 end)>();
    readonly List<PuzzleMapNodeView> pendingLabels = new List<PuzzleMapNodeView>();
    readonly Dictionary<string, int> regionLabelCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<Image, float> trackThickness = new Dictionary<Image, float>();
    PuzzleMapNodeView[] layoutNodes;
    RouteTimeVisual[] layoutRoutes;
    string currentCityId;
    string selectedCityId;
    float labelGap;
    float LabelGap => labelGap;
    static readonly Vector2[] LABEL_DIRECTIONS =
    {
        Vector2.up, Vector2.right, Vector2.down, Vector2.left,
        new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
    };
    static Vector2[] TimeOffsets => new Vector2[]
    {
        Vector2.zero, new Vector2(0f, BasicData.Float("ui.PuzzleMapView.puzzleMapViewY")), new Vector2(0f, -BasicData.Float("ui.PuzzleMapView.puzzleMapViewY2")), new Vector2(BasicData.Float("ui.PuzzleMapView.puzzleMapViewX"), 0f), new Vector2(-BasicData.Float("ui.PuzzleMapView.puzzleMapViewX2"), 0f)
    };

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

    void PrepareLayout()
    {
        if (layoutNodes != null)
        {
            return;
        }
        layoutNodes = (PuzzleMapNodeView[])nodes.Clone();
        layoutRoutes = (RouteTimeVisual[])routeTimes.Clone();
        foreach (var route in layoutRoutes)
        {
            route.position = route.label.rectTransform.anchoredPosition;
        }
        foreach (var track in routeLines)
        {
            trackThickness[track] = track.rectTransform.sizeDelta.y;
        }
    }

    internal void UpdateMapLayout(float _zoom, RectTransform _viewport)
    {
        PrepareLayout();
        labelGap = BasicData.Float("ui.PuzzleMapView.labelGap");
        occupied.Clear();
        markerBounds.Clear();
        labelLinks.Clear();
        Rect bounds = _viewport.rect;
        bounds = Rect.MinMaxRect(bounds.xMin + BasicData.Float("ui.PuzzleMapView.updateMapLayout"), bounds.yMin + BasicData.Float("ui.PuzzleMapView.updateMapLayout2"), bounds.xMax - BasicData.Float("ui.PuzzleMapView.updateMapLayout3"), bounds.yMax - BasicData.Float("ui.PuzzleMapView.updateMapLayout4"));
        foreach (var node in layoutNodes)
        {
            node.PrepareLayout(_zoom);
            markerBounds.Add(node, node.MarkerBounds(_viewport));
        }
        pendingLabels.Clear();
        regionLabelCounts.Clear();
        foreach (var node in layoutNodes)
        {
            node.HideLabel();
            if (node.ShouldShowLabel(_zoom, selectedCityId) && bounds.Contains(node.MarkerPosition(_viewport)))
            {
                pendingLabels.Add(node);
            }
        }
        long regionPenalty = _zoom < BasicData.Float("ui.mapLabels.regionBalanceBelowZoom")
            ? BasicData.Int("ui.mapLabels.regionCrowdingPenalty") : 0;
        while (pendingLabels.Count > 0)
        {
            PuzzleMapNodeView best = null;
            long bestScore = long.MinValue;
            foreach (var candidate in pendingLabels)
            {
                regionLabelCounts.TryGetValue(candidate.RegionId, out int count);
                long score = candidate.LabelPriority(selectedCityId) - count * regionPenalty;
                if (best == null || score > bestScore || (score == bestScore && string.CompareOrdinal(candidate.CityId, best.CityId) < 0))
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            pendingLabels.Remove(best);
            if (PlaceNodeLabel(best, bounds, _viewport))
            {
                regionLabelCounts.TryGetValue(best.RegionId, out int count);
                regionLabelCounts[best.RegionId] = count + 1;
            }
        }

        foreach (var track in routeLines)
        {
            Vector2 size = track.rectTransform.sizeDelta;
            size.y = Presentation.PuzzleMapTrackThickness(trackThickness[track]) / _zoom;
            track.rectTransform.sizeDelta = size;
        }
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
            label.rectTransform.localScale = Vector3.one / _zoom;
            label.rectTransform.anchoredPosition = route.position;
            label.fontSize = Presentation.PuzzleMapTimeFontSize;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Vector2 size = new Vector2(Mathf.Ceil(label.preferredWidth) + BasicData.Float("ui.PuzzleMapView.updateMapLayoutX"), BasicData.Float("ui.PuzzleMapView.updateMapLayoutY"));
            label.rectTransform.sizeDelta = size;
            bool visible = false;
            if (route.plate)
            {
                var plate = route.plate.rectTransform;
                plate.localScale = Vector3.one / _zoom;
                plate.sizeDelta = size;
                if (_zoom >= BasicData.Float("ui.PuzzleMapView.updateMapLayoutAbovePercent2"))
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

    bool PlaceNodeLabel(PuzzleMapNodeView _node, Rect _bounds, RectTransform _viewport)
    {
        Vector2 position = _node.MarkerPosition(_viewport);
        if (!_bounds.Contains(position))
        {
            _node.HideLabel();
            return false;
        }
        float step = BasicData.Float("ui.PuzzleMapView.labelSearchStep");
        float distance = BasicData.Float("ui.PuzzleMapView.labelSearchDistance");
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
                _node.PlaceLabel(candidate, start, end, _viewport, direction);
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
            if (marker.Key != _owner && SegmentOverlaps(_start, _end, Padded(marker.Value)))
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
        return current && selected ? BasicData.Int("ui.mapRoutes.currentSelected")
            : current ? BasicData.Int("ui.mapRoutes.current")
            : selected ? BasicData.Int("ui.mapRoutes.selected")
            : BasicData.Int("ui.mapRoutes.other");
    }

    bool Available(Rect _candidate, Rect _bounds)
    {
        if (_candidate.xMin < _bounds.xMin || _candidate.yMin < _bounds.yMin ||
            _candidate.xMax > _bounds.xMax || _candidate.yMax > _bounds.yMax)
        {
            return false;
        }
        Rect padded = Padded(_candidate);
        foreach (var marker in markerBounds.Values)
        {
            if (padded.Overlaps(marker))
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
        title.color = guide.color = information.color = openLabel.color = closeLabel.color = Presentation.PuzzleHeading;
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
        foreach (var routeTime in routeTimes)
        {
            if (tables.TryGetRoute(routeTime.fromCityId, routeTime.toCityId, out var route))
            {
                routeTime.label.text = Presentation.PuzzleMapTime(PuzzleTimeManager.TravelMinutes(route, tables.PuzzleTime));
            }
            routeTime.label.color = Presentation.PuzzleHeading;
        }
        base.Awake();
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
        information.supportRichText = true;
        information.text = _text;
    }

    protected override void OnShow()
    {
        ShowInformation(Presentation.PuzzleMapHint);
        navigation.Open();
    }

    protected override void OnHide()
    {
        ShowInformation(string.Empty);
    }
}
