using System.Collections.Generic;
using UnityEngine;

// 경로 계획 창의 UI 논리 (Presenter).
// 세션 모델 상태를 읽어 View에 표시를 갱신하고, 사용자 입력을 서비스 모델에 전달한다.
public sealed class PuzzleRoutePlanPresenter : MonoBehaviour
{
    [SerializeField] PuzzleRoutePlanView view;

    PuzzleInventoryService session;
    bool isAutoTraveling;

    public event System.Action AutoTravelRequested;
    public event System.Action StopRequested;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();

        if (view)
        {
            view.AutoTravelClicked += OnAutoTravelClicked;
            view.StopClicked += OnStopClicked;
            view.CancelClicked += OnCancelClicked;
            view.StopToggled += OnStopToggled;
        }

        GameEvents.RoutePlanChanged += OnRoutePlanChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.PanelVisibility += OnPanelVisibility;

        Refresh();
    }

    void OnDisable()
    {
        if (view)
        {
            view.AutoTravelClicked -= OnAutoTravelClicked;
            view.StopClicked -= OnStopClicked;
            view.CancelClicked -= OnCancelClicked;
            view.StopToggled -= OnStopToggled;
        }

        GameEvents.RoutePlanChanged -= OnRoutePlanChanged;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.PanelVisibility -= OnPanelVisibility;
    }

    public void SetAutoTraveling(bool _traveling)
    {
        isAutoTraveling = _traveling;
        Refresh();
    }

    void OnAutoTravelClicked()
    {
        AutoTravelRequested?.Invoke();
    }

    void OnStopClicked()
    {
        StopRequested?.Invoke();
    }

    void OnCancelClicked()
    {
        if (session != null)
        {
            session.CancelRoutePlan();
        }
    }

    void OnStopToggled(string _cityId, bool _stop)
    {
        if (session != null)
        {
            session.SetRouteStop(_cityId, _stop);
        }
    }

    void OnRoutePlanChanged(GameEvents.RoutePlanInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnFundsChanged(GameEvents.FundsInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.open)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (!view)
        {
            return;
        }

        var plan = session != null ? session.RoutePlan : null;
        if (plan == null || plan.Cities == null || plan.Cities.Count < 2)
        {
            view.Hide();
            return;
        }

        var tables = TableManager.Instance;
        string startCityId = plan.Cities[0];
        string destCityId = plan.DestinationCityId;
        string startName = tables.CityPrice(startCityId).displayName;
        string destName = tables.CityPrice(destCityId).displayName;
        string title = Presentation.RoutePlanTitle(startName, destName);

        var quote = session.QuoteRoutePlan();
        string summary = Presentation.RoutePlanSummary(quote);
        string warning = quote.AffordableNow ? string.Empty : Presentation.RoutePlanAffordableWarning;

        int totalWaypoints = plan.Cities.Count - 1;
        int maxSlots = view.SlotCount;
        int displayCount = Mathf.Min(totalWaypoints, maxSlots);

        var stations = new List<PuzzleRoutePlanView.StationDisplayData>(displayCount);
        for (int i = 0; i < displayCount; i++)
        {
            string cityId = plan.Cities[i + 1];
            bool isDestination = (i + 1 == plan.Cities.Count - 1);
            bool isStop = plan.IsStop(cityId);
            string stopText = isStop ? Presentation.RouteStopText : Presentation.RoutePassText;

            var cityPrice = tables.CityPrice(cityId);
            // 전장의 안개: 의뢰로 드러난 경로의 모르는 경유역은 이름·지역을 숨긴다.
            bool hidden = session.CityVisibility(cityId) == PuzzleMapVisibility.Hidden;
            Color cityColor = Presentation.PuzzleMapLineColor(hidden ? "region.common" : cityPrice.regionId);

            stations.Add(new PuzzleRoutePlanView.StationDisplayData(
                cityId,
                hidden ? Presentation.RouteUnknownStation : cityPrice.displayName,
                cityColor,
                isStop,
                isDestination,
                stopText));
        }

        string overflowText = totalWaypoints > maxSlots
            ? Presentation.RouteMoreStations(totalWaypoints - maxSlots)
            : string.Empty;

        view.Show(title, summary, warning, stations, isAutoTraveling, overflowText);
    }
}
