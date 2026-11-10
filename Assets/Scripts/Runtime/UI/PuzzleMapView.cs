using UnityEngine;
using UnityEngine.UI;

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

    internal PuzzleMapNodeView[] Nodes => nodes;

    internal void ShowCurrentLocation(RectTransform _node)
    {
        navigation.SetCurrent(_node);
    }

    protected override void Awake()
    {
        title.text = Presentation.PUZZLE_MAP_TITLE;
        guide.text = Presentation.PUZZLE_MAP_GUIDE;
        openLabel.text = Presentation.PUZZLE_MAP_OPEN;
        closeLabel.text = Presentation.PUZZLE_MAP_CLOSE;
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
                routeTime.label.text = Presentation.PuzzleMapTime(PuzzleTimeManager.TravelHours(route.travelTimeWeight));
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
        ShowInformation(Presentation.PUZZLE_MAP_HINT);
        navigation.Open();
    }

    protected override void OnHide()
    {
        ShowInformation(string.Empty);
    }
}
