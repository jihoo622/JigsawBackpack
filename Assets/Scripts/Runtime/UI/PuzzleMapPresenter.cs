using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleMapPresenter : MonoBehaviour
{
    [SerializeField] PuzzleMapView view;
    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;

    PuzzleInventoryService session;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        openButton.onClick.AddListener(OpenMap);
        closeButton.onClick.AddListener(CloseMap);
        foreach (var node in view.Nodes)
        {
            node.Hovered += ShowCity;
            node.Exited += ClearInformation;
            node.Clicked += OnNodeClicked;
        }
        GameEvents.CityArrived += OnCityArrived;
        Refresh();
    }

    void OnDisable()
    {
        openButton.onClick.RemoveListener(OpenMap);
        closeButton.onClick.RemoveListener(CloseMap);
        foreach (var node in view.Nodes)
        {
            node.Hovered -= ShowCity;
            node.Exited -= ClearInformation;
            node.Clicked -= OnNodeClicked;
        }
        GameEvents.CityArrived -= OnCityArrived;
        view.Close();
    }

    void OpenMap()
    {
        Refresh();
        view.Open();
    }

    void CloseMap()
    {
        view.Close();
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        Refresh();
        ClearInformation();
    }

    void Refresh()
    {
        view.ShowGuide(Presentation.PuzzleMapGuide());
        string currentCityId = session.CurrentCityId;
        foreach (var node in view.Nodes)
        {
            string cityId = node.CityId;
            bool isCurrent = string.Equals(cityId, currentCityId, System.StringComparison.Ordinal);
            bool isConnected = false;
            bool canAfford = false;
            if (!isCurrent)
            {
                isConnected = TableManager.Instance.HasRoute(currentCityId, cityId);
                if (isConnected)
                {
                    var eval = session.EvaluateTravel(cityId);
                    canAfford = eval.IsSuccess;
                }
            }

            node.Show(TableManager.Instance.CityPrice(cityId),
                session.HasVisitedCity(cityId),
                isCurrent,
                isConnected,
                canAfford);
        }
    }

    void ShowCity(string _cityId)
    {
        string currentCityId = session.CurrentCityId;
        bool isCurrent = string.Equals(_cityId, currentCityId, System.StringComparison.Ordinal);
        bool isConnected = false;
        bool highSpeed = false;
        long travelHours = 0;
        PuzzleTravelResult eval = default;
        if (!isCurrent)
        {
            isConnected = TableManager.Instance.TryGetRoute(currentCityId, _cityId, out var route);
            if (isConnected)
            {
                highSpeed = route.highSpeed;
                travelHours = PuzzleTimeManager.TravelHours(route.travelTimeWeight);
                eval = session.EvaluateTravel(_cityId);
            }
        }

        view.ShowInformation(Presentation.PuzzleMapInformation(
            TableManager.Instance.CityPrice(_cityId),
            session.HasVisitedCity(_cityId),
            isCurrent,
            isConnected,
            eval,
            travelHours,
            highSpeed));
    }

    void ClearInformation()
    {
        view.ShowInformation(Presentation.PUZZLE_MAP_HINT);
    }

    void OnNodeClicked(string _cityId)
    {
        if (!view.IsOpen || session.RunStatus != PuzzleRunStatus.InProgress ||
            string.Equals(_cityId, session.CurrentCityId, System.StringComparison.Ordinal))
        {
            return;
        }

        var eval = session.EvaluateTravel(_cityId);
        if (!eval.IsSuccess)
        {
            view.ShowInformation(Presentation.PuzzleTravelStatus(eval));
            return;
        }

        var travelResult = session.Travel(_cityId);
        if (travelResult.IsSuccess)
        {
            view.Close();
        }
        else
        {
            view.ShowInformation(Presentation.PuzzleTravelStatus(travelResult));
        }
    }
}
