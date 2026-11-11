using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

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
    readonly Dictionary<Image, float> trackThickness = new Dictionary<Image, float>();
    PuzzleMapNodeView[] layoutNodes;
    RouteTimeVisual[] layoutRoutes;
    string currentCityId;
    string selectedCityId;
    static float LabelGap => BasicData.Float("ui.PuzzleMapView.LabelGap");
    static readonly Vector2[] LABEL_DIRECTIONS =
    {
        Vector2.up, Vector2.right, Vector2.down, Vector2.left,
        new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
    };
    static Vector2[] TimeOffsets => new Vector2[]
    {
        Vector2.zero, new Vector2(0f, BasicData.Float("ui.PuzzleMapView.Vector2.number1")), new Vector2(0f, -BasicData.Float("ui.PuzzleMapView.Vector2.number2")), new Vector2(BasicData.Float("ui.PuzzleMapView.Vector2.number3"), 0f), new Vector2(-BasicData.Float("ui.PuzzleMapView.Vector2.number4"), 0f)
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
        occupied.Clear();
        Rect bounds = _viewport.rect;
        bounds = Rect.MinMaxRect(bounds.xMin + BasicData.Float("ui.PuzzleMapView.UpdateMapLayout.number1"), bounds.yMin + BasicData.Float("ui.PuzzleMapView.UpdateMapLayout.number2"), bounds.xMax - BasicData.Float("ui.PuzzleMapView.UpdateMapLayout.number3"), bounds.yMax - BasicData.Float("ui.PuzzleMapView.UpdateMapLayout.number4"));
        foreach (var node in layoutNodes)
        {
            node.PrepareLayout(_zoom);
            Vector2 position = node.MarkerPosition(_viewport);
            occupied.Add(Box(position, Vector2.one * node.MarkerRadius * 2f));
        }
        Array.Sort(layoutNodes, (_a, _b) =>
        {
            int priority = _b.LabelPriority(selectedCityId).CompareTo(_a.LabelPriority(selectedCityId));
            return priority != 0 ? priority : string.CompareOrdinal(_a.CityId, _b.CityId);
        });
        foreach (var node in layoutNodes)
        {
            Vector2 position = node.MarkerPosition(_viewport);
            Vector2 size = node.LabelSize;
            bool visible = false;
            Vector2 offset = Vector2.zero;
            if (bounds.Contains(position))
            {
                foreach (var direction in LABEL_DIRECTIONS)
                {
                    offset = Vector2.Scale(direction, size * 0.5f + Vector2.one * (node.MarkerRadius + LabelGap));
                    Rect candidate = Box(position + offset, size);
                    if (Available(candidate, bounds))
                    {
                        occupied.Add(candidate);
                        visible = true;
                        break;
                    }
                }
                if (!visible && node.LabelPriority(selectedCityId) >= BasicData.Int("ui.PuzzleMapView.UpdateMapLayout.number5"))
                {
                    Vector2 center = position + Vector2.up * (node.MarkerRadius + LabelGap + size.y * 0.5f);
                    center.x = Mathf.Clamp(center.x, bounds.xMin + size.x * 0.5f, bounds.xMax - size.x * 0.5f);
                    center.y = Mathf.Clamp(center.y, bounds.yMin + size.y * 0.5f, bounds.yMax - size.y * 0.5f);
                    offset = center - position;
                    occupied.Add(Box(center, size));
                    visible = true;
                }
            }
            node.PlaceLabel(visible, offset);
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
            label.rectTransform.localScale = Vector3.one / _zoom;
            label.rectTransform.anchoredPosition = route.position;
            label.fontSize = Presentation.PuzzleMapTimeFontSize;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Vector2 size = new Vector2(Mathf.Ceil(label.preferredWidth) + BasicData.Float("ui.PuzzleMapView.Vector2.number5"), BasicData.Float("ui.PuzzleMapView.Vector2.number6"));
            label.rectTransform.sizeDelta = size;
            bool visible = false;
            if (route.plate)
            {
                var plate = route.plate.rectTransform;
                plate.localScale = Vector3.one / _zoom;
                plate.sizeDelta = size;
                if (_zoom >= BasicData.Float("ui.PuzzleMapView.Vector2.number7"))
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

    int RoutePriority(RouteTimeVisual _route)
    {
        bool current = _route.fromCityId == currentCityId || _route.toCityId == currentCityId;
        bool selected = _route.fromCityId == selectedCityId || _route.toCityId == selectedCityId;
        return current && selected ? BasicData.Int("ui.PuzzleMapView.RoutePriority.number1") : current ? 2 : selected ? 1 : 0;
    }

    bool Available(Rect _candidate, Rect _bounds)
    {
        if (_candidate.xMin < _bounds.xMin || _candidate.yMin < _bounds.yMin ||
            _candidate.xMax > _bounds.xMax || _candidate.yMax > _bounds.yMax)
        {
            return false;
        }
        Rect padded = Rect.MinMaxRect(_candidate.xMin - LabelGap, _candidate.yMin - LabelGap,
            _candidate.xMax + LabelGap, _candidate.yMax + LabelGap);
        foreach (var rect in occupied)
        {
            if (padded.Overlaps(rect))
            {
                return false;
            }
        }
        return true;
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
