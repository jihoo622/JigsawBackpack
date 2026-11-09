using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleMapView : PanelBase
{
    [SerializeField] Text title;
    [SerializeField] Text guide;
    [SerializeField] Text information;
    [SerializeField] Text openLabel;
    [SerializeField] Text closeLabel;
    [SerializeField] Image background;
    [SerializeField] Image[] routeLines;
    [SerializeField] PuzzleMapNodeView[] nodes;

    internal PuzzleMapNodeView[] Nodes => nodes;

    protected override void Awake()
    {
        title.text = Presentation.PUZZLE_MAP_TITLE;
        guide.text = Presentation.PUZZLE_MAP_GUIDE;
        openLabel.text = Presentation.PUZZLE_MAP_OPEN;
        closeLabel.text = Presentation.PUZZLE_MAP_CLOSE;
        title.color = guide.color = information.color = openLabel.color = closeLabel.color = Presentation.PuzzleHeading;
        background.color = Presentation.PuzzleBackground;
        if (routeLines != null)
        {
            foreach (var line in routeLines)
            {
                if (line)
                {
                    line.color = Presentation.PuzzleMapRoute;
                }
            }
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
    }

    protected override void OnHide()
    {
        ShowInformation(string.Empty);
    }
}
