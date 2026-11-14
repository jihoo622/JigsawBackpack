using System.Collections;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleMapPresenter : MonoBehaviour
{
    [SerializeField] PuzzleMapView view;
    [SerializeField] Button closeButton;
    [SerializeField] PuzzleMapTravelView travelView;

    PuzzleInventoryService session;
    string pendingCityId;
    string hoveredCityId;
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
    }

    void OnMapToggleRequested(GameEvents.MapToggleInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
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
                session.HasRumorForCity(cityId),
                view.GetNodeLayoutSettings(),
                Presentation.QuestMapBadge(session, cityId));
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

        string information = Presentation.PuzzleMapInformation(
            TableManager.Instance.CityPrice(_cityId),
            session.HasVisitedCity(_cityId),
            isCurrent,
            isConnected,
            eval,
            TableManager.Instance.Region(TableManager.Instance.CityPrice(_cityId).regionId),
            session.TavernRumors);
        string quests = Presentation.QuestMapInformation(session, _cityId);
        view.ShowInformation(string.IsNullOrEmpty(quests) ? information
            : information + BasicData.Text("presentation.quest.separator") + quests);

        if (isConnected && !isCurrent)
        {
            var node = System.Array.Find(view.Nodes, n => n && string.Equals(n.CityId, _cityId, System.StringComparison.Ordinal));
            if (node)
            {
                long deadline = session.Time != null && session.Time.HasDeadline ? session.Time.DeadlineMinutes : 0;
                view.ShowTravelPreview(currentCityId, _cityId, eval, (RectTransform)node.transform, session.ElapsedTime, deadline);
                view.HighlightRoute(currentCityId, _cityId);
            }
        }
        else
        {
            view.HideTravelPreview();
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
