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
        foreach (var node in view.Nodes)
        {
            node.Show(TableManager.Instance.CityPrice(node.CityId), session.HasVisitedCity(node.CityId),
                session.CurrentCityId == node.CityId);
        }
    }

    void ShowCity(string _cityId)
    {
        view.ShowInformation(Presentation.PuzzleMapInformation(TableManager.Instance.CityPrice(_cityId),
            session.HasVisitedCity(_cityId)));
    }

    void ClearInformation()
    {
        view.ShowInformation(Presentation.PUZZLE_MAP_HINT);
    }
}
