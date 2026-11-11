using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public sealed class PuzzleMapPresenter : MonoBehaviour
{
    [SerializeField] PuzzleMapView view;
    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    [SerializeField] PuzzleMapTravelView travelView;

    PuzzleInventoryService session;
    string pendingCityId;
    Coroutine travelRoutine;
    TravelPhase phase;

    enum TravelPhase
    {
        None, Confirming, Traveling, Arrived
    }

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
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.MapRequested += OnMapRequested;
        travelView.Confirmed += ConfirmTravel;
        travelView.Canceled += AbortTravel;
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
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.MapRequested -= OnMapRequested;
        travelView.Confirmed -= ConfirmTravel;
        travelView.Canceled -= AbortTravel;
        AbortTravel();
        view.Close();
    }

    void OpenMap()
    {
        Refresh();
        view.Open();
    }

    void OnMapRequested(GameEvents.MapRequestInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle && phase == TravelPhase.None)
        {
            OpenMap();
        }
    }

    void CloseMap()
    {
        if (phase == TravelPhase.None)
        {
            view.Close();
        }
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        Refresh();
        ClearInformation();
    }

    void Refresh()
    {
        view.ShowGuide(Presentation.PuzzleMapGuide(session.ElapsedTime));
        string targetCityId = TableManager.Instance.Puzzle.planningTargetCityId;
        PuzzleMapNodeView currentNode = null;
        string currentCityId = session.CurrentCityId;
        foreach (var node in view.Nodes)
        {
            string cityId = node.CityId;
            bool isCurrent = string.Equals(cityId, currentCityId, System.StringComparison.Ordinal);
            if (isCurrent)
            {
                currentNode = node;
            }
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
                canAfford,
                !string.IsNullOrEmpty(targetCityId) && string.Equals(cityId, targetCityId, System.StringComparison.Ordinal));
        }
        if (currentNode)
        {
            view.ShowCurrentLocation(currentNode);
        }
        view.RefreshLayout();
    }

    void ShowCity(string _cityId)
    {
        if (phase != TravelPhase.None)
        {
            return;
        }
        view.HighlightCity(_cityId);
        string currentCityId = session.CurrentCityId;
        bool isCurrent = string.Equals(_cityId, currentCityId, System.StringComparison.Ordinal);
        bool isConnected = false;
        PuzzleTravelResult eval = default;
        if (!isCurrent)
        {
            isConnected = TableManager.Instance.TryGetRoute(currentCityId, _cityId, out var route);
            if (isConnected)
            {
                eval = session.EvaluateTravel(_cityId);
            }
        }

        view.ShowInformation(Presentation.PuzzleMapInformation(
            TableManager.Instance.CityPrice(_cityId),
            session.HasVisitedCity(_cityId),
            isCurrent,
            isConnected,
            eval,
            TableManager.Instance.Region(TableManager.Instance.CityPrice(_cityId).regionId)));
    }

    void ClearInformation()
    {
        if (phase != TravelPhase.None)
        {
            return;
        }
        view.HighlightCity(null);
        view.ShowInformation(Presentation.PuzzleMapHint);
    }

    void OnNodeClicked(string _cityId)
    {
        if (phase != TravelPhase.None || !view.IsOpen || session.RunStatus != PuzzleRunStatus.InProgress ||
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

        pendingCityId = _cityId;
        session.SetPreviewDestination(_cityId);
        phase = TravelPhase.Confirming;
        view.SetInteraction(false);
        travelView.ShowConfirmation(Presentation.PuzzleTravelConfirmation(
            TableManager.Instance.CityPrice(session.CurrentCityId).displayName,
            TableManager.Instance.CityPrice(_cityId).displayName, eval));
    }

    void ConfirmTravel()
    {
        if (phase != TravelPhase.Confirming || !view.IsOpen)
        {
            return;
        }
        var evaluation = session.EvaluateTravel(pendingCityId);
        if (!evaluation.IsSuccess)
        {
            AbortTravel();
            view.ShowInformation(Presentation.PuzzleTravelStatus(evaluation));
            return;
        }
        phase = TravelPhase.Traveling;
        travelView.BeginJourney(Presentation.PuzzleTravelJourney(
            TableManager.Instance.CityPrice(session.CurrentCityId).displayName,
            TableManager.Instance.CityPrice(pendingCityId).displayName));
        travelRoutine = StartCoroutine(TravelTo(pendingCityId));
    }

    IEnumerator TravelTo(string _cityId)
    {
        float elapsed = 0f;
        float duration = TableManager.Instance.PuzzleTrade.travelEffectSeconds;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            travelView.ShowProgress(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        // 이동은 연출 완료 후 한 번만 확정한다. 닫힘·비활성화는 이 지점 전에 대기 작업을 중단한다.
        var travelResult = session.Travel(_cityId);
        if (travelResult.IsSuccess)
        {
            phase = TravelPhase.Arrived;
            travelView.ShowArrival();
            elapsed = 0f;
            duration = TableManager.Instance.PuzzleTrade.travelArrivalHoldSeconds;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            travelRoutine = null;
            AbortTravel();
            view.Close();
        }
        else
        {
            travelRoutine = null;
            AbortTravel();
            view.ShowInformation(Presentation.PuzzleTravelStatus(travelResult));
        }
    }

    void AbortTravel()
    {
        if (travelRoutine != null)
        {
            StopCoroutine(travelRoutine);
            travelRoutine = null;
        }
        phase = TravelPhase.None;
        pendingCityId = null;
        travelView.Close();
        view.SetInteraction(true);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (!_info.open && (_info.panel == view.PanelName ||
            (_info.panel == Presentation.PUZZLE_TRAVEL_PANEL_NAME && phase != TravelPhase.None)))
        {
            AbortTravel();
        }
    }
}
