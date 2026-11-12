using UnityEngine;

public sealed class PuzzleStationActionsPresenter : MonoBehaviour
{
    [SerializeField] PuzzleStationActionsView view;
    PuzzleInventoryService session;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        view.MarketRequested += OpenMarket;
        view.TavernRequested += OpenTavern;
        view.RestRequested += OpenRest;
        view.TravelRequested += OpenMap;
        view.RestTimeRequested += Rest;
        GameEvents.CityArrived += OnArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PanelVisibility += OnPanelVisibility;
        Refresh();
    }

    void OnDisable()
    {
        view.MarketRequested -= OpenMarket;
        view.TavernRequested -= OpenTavern;
        view.RestRequested -= OpenRest;
        view.TravelRequested -= OpenMap;
        view.RestTimeRequested -= Rest;
        GameEvents.CityArrived -= OnArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PanelVisibility -= OnPanelVisibility;
    }

    void Refresh()
    {
        view.Refresh(session.IsMarketOpen, session.RunStatus == PuzzleRunStatus.InProgress, TableManager.Instance.PuzzleTime.restMinutes);
    }

    void OpenMarket()
    {
        if (session.IsMarketOpen)
        {
            view.ShowMarket();
        }
    }

    void OpenRest()
    {
        view.ShowRest();
    }

    void OpenTavern()
    {
        if (session.RunStatus == PuzzleRunStatus.InProgress)
        {
            view.ShowTavern();
        }
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            view.SetInteraction(!_info.open);
        }
    }

    void OpenMap()
    {
        GameEvents.RaiseMapRequested(gameObject.scene.handle);
    }

    void Rest()
    {
        session.AdvanceMinutes(TableManager.Instance.PuzzleTime.restMinutes, PuzzleActionKind.Rest);
    }

    void OnArrived(GameEvents.CityArrivalInfo _info)
    {
        view.ShowHome();
        Refresh();
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }
}
