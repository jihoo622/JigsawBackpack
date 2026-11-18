using System.Collections;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleMapPresenter : MonoBehaviour
{
    [SerializeField] PuzzleMapView view;
    [SerializeField] Button closeButton;
    [SerializeField] PuzzleMapTravelView travelView;
    [SerializeField] PuzzleRoutePlanPresenter routePlanPresenter;

    PuzzleInventoryService session;
    string pendingCityId;
    string hoveredCityId;
    Coroutine travelRoutine;
    Coroutine autoTravelRoutine;
    bool stopAutoTravelRequested;
    TravelPhase phase;

    enum TravelPhase
    {
        None, Confirming, Traveling, Arrived, AutoTraveling
    }

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        closeButton.onClick.AddListener(CloseMap);
        // 지도 열기/닫기 토글은 고정 UI의 지도 버튼(GameEvents.MapToggleRequested)이 요청한다.
        GameEvents.MapToggleRequested += OnMapToggleRequested;
        foreach (var node in view.Nodes)
        {
            node.Hovered += ShowCity;
            node.Exited += OnNodeExited;
            node.Clicked += OnNodeClicked;
        }
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.MapRequested += OnMapRequested;
        GameEvents.MapFocusRequested += OnMapFocusRequested;
        GameEvents.RoutePlanChanged += OnRoutePlanChanged;
        GameEvents.RoutePlanRequested += OnRoutePlanRequested;
        if (routePlanPresenter)
        {
            routePlanPresenter.AutoTravelRequested += StartAutoTravel;
            routePlanPresenter.StopRequested += RequestStopAutoTravel;
        }
        travelView.Confirmed += ConfirmTravel;
        travelView.Canceled += AbortTravel;
        Refresh();
    }

    void OnDisable()
    {
        closeButton.onClick.RemoveListener(CloseMap);
        GameEvents.MapToggleRequested -= OnMapToggleRequested;
        foreach (var node in view.Nodes)
        {
            node.Hovered -= ShowCity;
            node.Exited -= OnNodeExited;
            node.Clicked -= OnNodeClicked;
        }
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.MapRequested -= OnMapRequested;
        GameEvents.MapFocusRequested -= OnMapFocusRequested;
        GameEvents.RoutePlanChanged -= OnRoutePlanChanged;
        GameEvents.RoutePlanRequested -= OnRoutePlanRequested;
        if (routePlanPresenter)
        {
            routePlanPresenter.AutoTravelRequested -= StartAutoTravel;
            routePlanPresenter.StopRequested -= RequestStopAutoTravel;
        }
        travelView.Confirmed -= ConfirmTravel;
        travelView.Canceled -= AbortTravel;
        AbortTravel();
        hoveredCityId = null;
        view.Close();
    }

    void OpenMap()
    {
        Refresh();
        view.Open();
        RefreshRoutePlanHighlight();
        if (routePlanPresenter)
        {
            routePlanPresenter.Refresh();
        }
    }

    void OnMapToggleRequested(GameEvents.MapToggleInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle || phase == TravelPhase.Traveling)
        {
            return;
        }
        if (view.IsOpen)
        {
            CloseMap();
        }
        else if (phase == TravelPhase.None)
        {
            OpenMap();
        }
    }

    void OnMapRequested(GameEvents.MapRequestInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle && phase == TravelPhase.None)
        {
            OpenMap();
        }
    }

    void OnMapFocusRequested(GameEvents.MapFocusInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        if (phase != TravelPhase.None)
        {
            return;
        }

        bool hasCity = !string.IsNullOrEmpty(_info.cityId);
        bool hasRegion = !string.IsNullOrEmpty(_info.regionId);
        if (!hasCity && !hasRegion)
        {
            return;
        }

        if (hasCity)
        {
            if (!TableManager.Instance.HasCity(_info.cityId))
            {
                return;
            }
            var node = System.Array.Find(view.Nodes, _node => _node && string.Equals(_node.CityId, _info.cityId, System.StringComparison.Ordinal));
            if (!node)
            {
                return;
            }

            if (!view.IsOpen)
            {
                OpenMap();
            }

            view.FocusCity(_info.cityId);
            ShowCity(_info.cityId);
        }
        else if (hasRegion)
        {
            if (!TableManager.Instance.TryGetRegion(_info.regionId, out _))
            {
                return;
            }

            if (!view.IsOpen)
            {
                OpenMap();
            }

            ClearInformation();
            view.FocusRegion(_info.regionId);
        }
    }

    void OnRoutePlanRequested(GameEvents.RoutePlanRequestInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        if (phase != TravelPhase.None)
        {
            return;
        }
        if (!view.IsOpen)
        {
            OpenMap();
        }

        var planFailure = session.PlanRoute(_info.cityId);
        if (planFailure != PuzzleRoutePlanFailure.None)
        {
            view.ShowInformation(Presentation.RoutePlanFailureStatus(planFailure));
        }
        else
        {
            view.FocusCity(_info.cityId);
            ShowCity(_info.cityId);
        }
    }

    public bool TryConsumeTravelCancel()
    {
        // 연출 중인 이동은 취소하면 확정 전에 멈춘다.
        if (phase == TravelPhase.AutoTraveling)
        {
            RequestStopAutoTravel();
            return true;
        }
        if (travelView && travelView.TryConsumeCancel())
        {
            AbortTravel();
            return true;
        }
        if (phase == TravelPhase.Confirming)
        {
            AbortTravel();
            return true;
        }
        return false;
    }

    public bool TryConsumeMapCancel()
    {
        if (!view || !view.IsOpen)
        {
            return false;
        }
        if (phase != TravelPhase.None)
        {
            // 이동 연출 중에는 닫지 않고 소비만 한다.
            return true;
        }
        CloseMap();
        return true;
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
        RefreshRoutePlanHighlight();
        if (routePlanPresenter)
        {
            routePlanPresenter.Refresh();
        }
    }

    void OnRumorRecorded(GameEvents.TavernRumorInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        Refresh();
        if (view.IsOpen && !string.IsNullOrEmpty(hoveredCityId))
        {
            ShowCity(hoveredCityId);
        }
    }

    // 의뢰 수락·완료·실패와 시간 진행은 목적지 표식과 남은 시간을 바꾼다.
    void OnQuestChanged(GameEvents.QuestInfo _info)
    {
        if (_info.session == session)
        {
            RefreshQuestMarkers();
        }
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            RefreshQuestMarkers();
        }
    }

    void RefreshQuestMarkers()
    {
        Refresh();
        if (view.IsOpen && phase == TravelPhase.None && !string.IsNullOrEmpty(hoveredCityId))
        {
            ShowCity(hoveredCityId);
        }
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
            // 전장의 안개: 모르는 역은 숨긴다. 위치만 아는 역(소문·의뢰)은 미방문 역처럼 보인다.
            var visibility = session.CityVisibility(cityId);
            node.gameObject.SetActive(visibility != PuzzleMapVisibility.Hidden);
            if (visibility == PuzzleMapVisibility.Hidden)
            {
                continue;
            }
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
                !string.IsNullOrEmpty(targetCityId) && string.Equals(cityId, targetCityId, System.StringComparison.Ordinal),
                session.HasUnvisitedRumorForCity(cityId),
                view.GetNodeLayoutSettings(),
                Presentation.QuestMapBadge(session, cityId));
        }
        view.SetKnownRoutes(session.IsRouteKnown);
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
        hoveredCityId = _cityId;
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

        var city = TableManager.Instance.CityPrice(_cityId);
        var region = TableManager.Instance.Region(city.regionId);
        bool visited = session.HasVisitedCity(_cityId);
        string quests = Presentation.QuestMapInformation(session, _cityId);
        if (visited)
        {
            string heading = Presentation.PuzzleMapCityHeading(city, true, isCurrent, isConnected, eval, region);
            var tradePreferences = Presentation.GetCityTradePreferences(city);
            view.ShowVisitedInformation(heading, tradePreferences, quests);
        }
        else
        {
            string information = Presentation.PuzzleMapInformation(
                city,
                false,
                isCurrent,
                isConnected,
                eval,
                region,
                session.TavernRumors);
            view.ShowInformation(string.IsNullOrEmpty(quests) ? information
                : information + BasicData.Text("presentation.quest.separator") + quests);
        }

        var node = System.Array.Find(view.Nodes, n => n && string.Equals(n.CityId, _cityId, System.StringComparison.Ordinal));
        view.ShowTownTooltip(_cityId, eval, isCurrent, isConnected, node ? (RectTransform)node.transform : null, session);

        if (isConnected && !isCurrent)
        {
            view.HighlightRoute(currentCityId, _cityId);
        }
        else
        {
            view.ClearRouteHighlight();
        }
    }

    void ClearInformation()
    {
        if (phase != TravelPhase.None)
        {
            return;
        }
        hoveredCityId = null;
        view.HighlightCity(null);
        view.ShowInformation(Presentation.PuzzleMapHint);
        view.HideTravelPreview();
        view.ClearRouteHighlight();
    }

    void OnNodeExited(string _cityId)
    {
        if (string.Equals(hoveredCityId, _cityId, System.StringComparison.Ordinal))
        {
            ClearInformation();
        }
    }

    void OnNodeClicked(string _cityId)
    {
        if (phase != TravelPhase.None ||
            !view.IsOpen || session.RunStatus != PuzzleRunStatus.InProgress ||
            string.Equals(_cityId, session.CurrentCityId, System.StringComparison.Ordinal))
        {
            return;
        }

        var eval = session.EvaluateTravel(_cityId);
        if (!eval.IsSuccess)
        {
            if (eval.Failure == PuzzleTravelFailure.MissingRoute)
            {
                var planFailure = session.PlanRoute(_cityId);
                if (planFailure != PuzzleRoutePlanFailure.None)
                {
                    view.ShowInformation(Presentation.RoutePlanFailureStatus(planFailure));
                }
                return;
            }

            view.ShowInformation(Presentation.PuzzleTravelStatus(eval));
            return;
        }

        pendingCityId = _cityId;
        session.SetPreviewDestination(_cityId);
        phase = TravelPhase.Confirming;
        view.SetInteraction(false);
        view.HideTravelPreview();
        view.ClearRouteHighlight();
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

    // 짧은 이동 연출 뒤 이동을 한 번 확정한다(1턴). 닫힘·비활성화는 확정 전에 대기 작업을 멈춘다.
    IEnumerator TravelTo(string _cityId)
    {
        yield return PlayTravelEffect(TableManager.Instance.PuzzleTrade.travelEffectSeconds);

        var travelResult = session.Travel(_cityId);
        if (travelResult.IsSuccess)
        {
            phase = TravelPhase.Arrived;
            travelView.ShowArrival();
            yield return HoldArrival();
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

    IEnumerator PlayTravelEffect(float _duration)
    {
        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;
            travelView.ShowProgress(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _duration)));
            yield return null;
        }
        travelView.ShowProgress(1f);
    }

    IEnumerator HoldArrival()
    {
        float elapsed = 0f;
        float duration = TableManager.Instance.PuzzleTrade.travelArrivalHoldSeconds;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void AbortTravel()
    {
        if (travelRoutine != null)
        {
            StopCoroutine(travelRoutine);
            travelRoutine = null;
        }
        if (autoTravelRoutine != null)
        {
            StopCoroutine(autoTravelRoutine);
            autoTravelRoutine = null;
            if (session != null && session.IsPassingThrough)
            {
                session.StopAtCurrentCity();
            }
            session?.ReportAutoTravelStopped("user_stopped");
        }
        stopAutoTravelRequested = false;
        phase = TravelPhase.None;
        pendingCityId = null;
        travelView.Close();
        view.SetInteraction(true);
        if (routePlanPresenter)
        {
            routePlanPresenter.SetAutoTraveling(false);
        }
    }

    void OnRoutePlanChanged(GameEvents.RoutePlanInfo _info)
    {
        if (_info.session != session)
        {
            return;
        }
        RefreshRoutePlanHighlight();
    }

    void RefreshRoutePlanHighlight()
    {
        if (session != null && session.RoutePlan != null && session.RoutePlan.Cities != null && session.RoutePlan.Cities.Count >= 2)
        {
            view.HighlightPath(session.RoutePlan.Cities);
        }
        else
        {
            view.ClearPathHighlight();
        }
    }

    void StartAutoTravel()
    {
        if (phase != TravelPhase.None || !view.IsOpen || session == null ||
            session.RunStatus != PuzzleRunStatus.InProgress || session.RoutePlan == null ||
            session.RoutePlan.Cities == null || session.RoutePlan.Cities.Count < 2)
        {
            return;
        }

        phase = TravelPhase.AutoTraveling;
        stopAutoTravelRequested = false;
        view.SetInteraction(false);
        view.HideTravelPreview();
        view.ClearRouteHighlight();
        if (routePlanPresenter)
        {
            routePlanPresenter.SetAutoTraveling(true);
        }
        autoTravelRoutine = StartCoroutine(AutoTravelLoop());
    }

    public void RequestStopAutoTravel()
    {
        if (phase == TravelPhase.AutoTraveling)
        {
            stopAutoTravelRequested = true;
            view.ShowInformation(Presentation.RouteAutoTravelStoppingNotice);
        }
    }

    IEnumerator AutoTravelLoop()
    {
        while (true)
        {
            if (session.RunStatus != PuzzleRunStatus.InProgress)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("run_ended");
                view.ShowInformation(Presentation.RouteAutoTravelStoppedMessage("run_ended"));
                EndAutoTravel();
                yield break;
            }

            if (stopAutoTravelRequested)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("user_stopped");
                view.ShowInformation(Presentation.RouteAutoTravelStoppedMessage("user_stopped"));
                EndAutoTravel();
                yield break;
            }

            var plan = session.RoutePlan;
            if (plan == null || plan.Cities == null || plan.Cities.Count < 2)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("failed");
                view.ShowInformation(Presentation.RouteAutoTravelStoppedMessage("failed"));
                EndAutoTravel();
                yield break;
            }

            var eval = session.EvaluateNextRouteLeg();
            if (!eval.IsSuccess)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                string reason = eval.Failure == PuzzleTravelFailure.InsufficientFunds
                    ? "insufficient_funds"
                    : "failed";
                session.ReportAutoTravelStopped(reason);
                view.ShowInformation(eval.Failure == PuzzleTravelFailure.InsufficientFunds
                    ? Presentation.PuzzleTravelStatus(eval)
                    : Presentation.RouteAutoTravelStoppedMessage(reason));
                EndAutoTravel();
                yield break;
            }

            string nextCityId = plan.NextCityId;
            if (string.IsNullOrEmpty(nextCityId))
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("failed");
                view.ShowInformation(Presentation.RouteAutoTravelStoppedMessage("failed"));
                EndAutoTravel();
                yield break;
            }

            bool isStopLeg = plan.IsStop(nextCityId);
            string currentCityName = TableManager.Instance.CityPrice(session.CurrentCityId).displayName;
            string nextCityName = TableManager.Instance.CityPrice(nextCityId).displayName;
            travelView.BeginJourney(Presentation.PuzzleTravelJourney(currentCityName, nextCityName));
            // 통과 구간은 연출을 절반으로 줄인다. 구간마다 1턴이 흐른다.
            float baseDuration = TableManager.Instance.PuzzleTrade.travelEffectSeconds;
            yield return PlayTravelEffect(isStopLeg ? baseDuration : Mathf.Max(0.2f, baseDuration * 0.5f));
            var travelResult = session.TravelNextRouteLeg(out bool stopped);
            if (!travelResult.IsSuccess)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("failed");
                view.ShowInformation(Presentation.PuzzleTravelStatus(travelResult));
                EndAutoTravel();
                yield break;
            }

            if (stopped)
            {
                phase = TravelPhase.Arrived;
                travelView.ShowArrival();
                yield return HoldArrival();
                session.ReportAutoTravelStopped("stop_reached");
                EndAutoTravel();
                view.Close();
                yield break;
            }

            if (stopAutoTravelRequested)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("user_stopped");
                view.ShowInformation(Presentation.RouteAutoTravelStoppedMessage("user_stopped"));
                EndAutoTravel();
                yield break;
            }

            if (session.RunStatus != PuzzleRunStatus.InProgress)
            {
                if (session.IsPassingThrough)
                {
                    session.StopAtCurrentCity();
                }
                session.ReportAutoTravelStopped("run_ended");
                view.ShowInformation(Presentation.RouteAutoTravelStoppedMessage("run_ended"));
                EndAutoTravel();
                yield break;
            }

            Refresh();
            RefreshRoutePlanHighlight();
            if (routePlanPresenter)
            {
                routePlanPresenter.Refresh();
            }
        }
    }

    void EndAutoTravel()
    {
        if (autoTravelRoutine != null)
        {
            StopCoroutine(autoTravelRoutine);
            autoTravelRoutine = null;
        }
        stopAutoTravelRequested = false;
        phase = TravelPhase.None;
        travelView.Close();
        view.SetInteraction(true);
        if (routePlanPresenter)
        {
            routePlanPresenter.SetAutoTraveling(false);
            routePlanPresenter.Refresh();
        }
        Refresh();
        RefreshRoutePlanHighlight();
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
