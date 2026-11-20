using UnityEngine;

// R21: RelayoutSettings.Enabled에 따라 신규 고정 프레임/부동 창 루트와 구형 UI 루트를 전환한다.
// S2~S5 동안 마을 건물 클릭 시 기존 역 하위 패널을 임시로 열어 기능을 유지한다.
[DisallowMultipleComponent]
public sealed class RelayoutRootController : MonoBehaviour
{
    [SerializeField] GameObject worldSceneCanvas;
    [SerializeField] GameObject fixedFrameCanvas;
    [SerializeField] GameObject floatingWindowCanvas;
    [SerializeField] GameObject oldGameplayLayout;
    [SerializeField] GameObject oldTrainColumn;
    [SerializeField] GameObject oldFixedBottomBar;
    [SerializeField] GameObject oldCityNavigation;
    [SerializeField] GameObject oldJourneyFooter;
    [SerializeField] GameObject oldRumorSideHandle;
    [SerializeField] GameObject oldRumorLogPanel;
    [SerializeField] GameObject oldPuzzleWorldMap;
    [SerializeField] GameObject oldPuzzleSynergyChart;
    [SerializeField] PuzzleStationActionsPresenter stationActionsPresenter;
    [SerializeField] StatusBannerView statusBannerView;
    [SerializeField] NotebookWindowView notebookWindow;
    [SerializeField] MarketWindowView marketWindow;
    [SerializeField] LensWindowView lensWindow;
    [SerializeField] TavernWindowView tavernWindow;
    [SerializeField] RestWindowView restWindow;
    [SerializeField] StationHallWindowView stationHallWindow;
    [SerializeField] WorkshopWindowView workshopWindow;
    [SerializeField] CityHallWindowView cityHallWindow;
    [SerializeField] TravelBannerView travelBanner;

    bool defaultsCaptured;
    bool defaultGameplayLayoutActive;
    bool defaultTrainColumnActive;
    bool defaultFixedBottomBarActive;
    bool defaultCityNavigationActive;
    bool defaultJourneyFooterActive;
    bool defaultRumorSideHandleActive;
    bool defaultRumorLogPanelActive;
    bool defaultPuzzleWorldMapActive;
    bool defaultPuzzleSynergyChartActive;

    void Awake()
    {
        CaptureDefaultsIfNeeded();
    }

    // 게임을 시작하면 마을 창(시장·술집·여관·역사·생산시설·시청)과 렌즈 창은 닫힌 채로 시작한다.
    // 부동 창은 열림 상태를 저장해 다음 실행에 되살리는데, 마을 창을 여러 개 띄울 수 있게 되면서 지난 실행의 창이 전부 열린 채로 시작했다.
    // 시설 창은 그 역에 들어가야 쓰는 창이므로 되살리지 않는다. 시장은 도착 처리(PuzzleInventoryPresenter)가 입장시킨 경우만 열어 둔다.
    // 화차 창·수첩 같은 개인 창은 그대로 되살린다. 모든 OnEnable 뒤에 정리하도록 Start에서 한다.
    void Start()
    {
        if (!RelayoutSettings.Enabled)
        {
            return;
        }
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (marketWindow && marketWindow.IsOpen && (session == null || !session.CanTradeInMarket)) marketWindow.SetOpen(false);
        if (tavernWindow && tavernWindow.IsOpen) tavernWindow.SetOpen(false);
        if (restWindow && restWindow.IsOpen) restWindow.SetOpen(false);
        if (stationHallWindow && stationHallWindow.IsOpen) stationHallWindow.SetOpen(false);
        if (workshopWindow && workshopWindow.IsOpen) workshopWindow.SetOpen(false);
        if (cityHallWindow && cityHallWindow.IsOpen) cityHallWindow.SetOpen(false);
        if (lensWindow && lensWindow.IsOpen) lensWindow.SetOpen(false);
    }

    void OnEnable()
    {
        CaptureDefaultsIfNeeded();
        RelayoutSettings.Changed += ApplyRelayoutState;
        GameEvents.MapToggleRequested += OnMapToggleRequested;
        GameEvents.MapRequested += OnMapRequested;
        GameEvents.MapFocusRequested += OnMapFocusRequested;
        GameEvents.RoutePlanRequested += OnRoutePlanRequested;
        GameEvents.NotebookOpenRequested += OnNotebookOpenRequested;
        if (stationActionsPresenter)
        {
            stationActionsPresenter.ReturnedHome += OnStationReturnedHome;
        }
        if (stationHallWindow)
        {
            stationHallWindow.DepartureRequested += OnDepartureRequested;
        }
        ApplyRelayoutState(RelayoutSettings.Enabled);
    }

    void OnDisable()
    {
        if (stationHallWindow)
        {
            stationHallWindow.DepartureRequested -= OnDepartureRequested;
        }
        RelayoutSettings.Changed -= ApplyRelayoutState;
        GameEvents.MapToggleRequested -= OnMapToggleRequested;
        GameEvents.MapRequested -= OnMapRequested;
        GameEvents.MapFocusRequested -= OnMapFocusRequested;
        GameEvents.RoutePlanRequested -= OnRoutePlanRequested;
        GameEvents.NotebookOpenRequested -= OnNotebookOpenRequested;
        if (stationActionsPresenter)
        {
            stationActionsPresenter.ReturnedHome -= OnStationReturnedHome;
        }
    }

    void CaptureDefaultsIfNeeded()
    {
        if (defaultsCaptured)
        {
            return;
        }
        defaultsCaptured = true;
        if (!oldPuzzleWorldMap)
        {
            var maps = FindObjectsByType<PuzzleMapPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] && !maps[i].IsEmbeddedInNotebook)
                {
                    oldPuzzleWorldMap = maps[i].gameObject;
                    break;
                }
            }
        }
        defaultGameplayLayoutActive = oldGameplayLayout && oldGameplayLayout.activeSelf;
        defaultTrainColumnActive = oldTrainColumn && oldTrainColumn.activeSelf;
        defaultFixedBottomBarActive = oldFixedBottomBar && oldFixedBottomBar.activeSelf;
        defaultCityNavigationActive = oldCityNavigation && oldCityNavigation.activeSelf;
        defaultJourneyFooterActive = oldJourneyFooter && oldJourneyFooter.activeSelf;
        defaultRumorSideHandleActive = oldRumorSideHandle && oldRumorSideHandle.activeSelf;
        defaultRumorLogPanelActive = oldRumorLogPanel && oldRumorLogPanel.activeSelf;
        defaultPuzzleWorldMapActive = oldPuzzleWorldMap && oldPuzzleWorldMap.activeSelf;
        defaultPuzzleSynergyChartActive = oldPuzzleSynergyChart && oldPuzzleSynergyChart.activeSelf;
    }

    public void ApplyRelayoutState(bool _enabled)
    {
        CaptureDefaultsIfNeeded();

        if (_enabled)
        {
            var presenter = stationActionsPresenter
                ? stationActionsPresenter
                : SceneServices.Find<PuzzleStationActionsPresenter>(gameObject.scene);
            presenter?.CloseForRelayoutSwitch();
        }

        if (worldSceneCanvas) worldSceneCanvas.SetActive(_enabled);
        if (fixedFrameCanvas) fixedFrameCanvas.SetActive(_enabled);
        if (floatingWindowCanvas) floatingWindowCanvas.SetActive(_enabled);

        if (_enabled)
        {
            if (oldCityNavigation) oldCityNavigation.SetActive(false);
            if (oldJourneyFooter) oldJourneyFooter.SetActive(false);
            if (oldRumorSideHandle) oldRumorSideHandle.SetActive(false);
            if (oldRumorLogPanel) oldRumorLogPanel.SetActive(false);
            if (oldPuzzleWorldMap) oldPuzzleWorldMap.SetActive(false);
            if (oldPuzzleSynergyChart) oldPuzzleSynergyChart.SetActive(false);
            if (oldGameplayLayout) oldGameplayLayout.SetActive(false);
        }
        else
        {
            if (oldGameplayLayout) oldGameplayLayout.SetActive(defaultGameplayLayoutActive);
            if (oldTrainColumn) oldTrainColumn.SetActive(defaultTrainColumnActive);
            if (oldFixedBottomBar) oldFixedBottomBar.SetActive(defaultFixedBottomBarActive);
            if (oldCityNavigation) oldCityNavigation.SetActive(defaultCityNavigationActive);
            if (oldJourneyFooter) oldJourneyFooter.SetActive(defaultJourneyFooterActive);
            if (oldRumorSideHandle) oldRumorSideHandle.SetActive(defaultRumorSideHandleActive);
            if (oldRumorLogPanel) oldRumorLogPanel.SetActive(defaultRumorLogPanelActive);
            if (oldPuzzleWorldMap) oldPuzzleWorldMap.SetActive(defaultPuzzleWorldMapActive);
            if (oldPuzzleSynergyChart) oldPuzzleSynergyChart.SetActive(defaultPuzzleSynergyChartActive);
        }

        var invPresenter = SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        invPresenter?.RefreshForRelayoutSwitch();
    }

    public void OpenStationBuilding(TownBuildingButton.BuildingKind _kind)
    {
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (_kind == TownBuildingButton.BuildingKind.CityHall && cityHallWindow)
        {
            cityHallWindow.SetOpen(true);
            return;
        }

        if (session == null || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            string runMsg = session != null ? Presentation.PuzzleRunStatusText(session.RunStatus) : string.Empty;
            if (!string.IsNullOrEmpty(runMsg)) statusBannerView?.ShowStatus(runMsg);
            return;
        }
        if (PuzzleMapPresenter.IsAnyTravelAnimating)
        {
            statusBannerView?.ShowStatus(Presentation.PuzzleTravelInProgress);
            return;
        }

        switch (_kind)
        {
            case TownBuildingButton.BuildingKind.Market when marketWindow:
                if (!session.IsMarketOpen)
                {
                    statusBannerView?.ShowStatus(Presentation.PuzzleNightMarket);
                    return;
                }
                session.TryEnterMarket();
                marketWindow.SetOpen(true);
                return;

            case TownBuildingButton.BuildingKind.Tavern when tavernWindow:
                if (session.TryEnterTavern())
                {
                    tavernWindow.SetOpen(true);
                }
                return;

            case TownBuildingButton.BuildingKind.Rest when restWindow:
                restWindow.SetOpen(true);
                return;

            case TownBuildingButton.BuildingKind.StationHall when stationHallWindow:
                if (session.TryEnterWagonFacility())
                {
                    stationHallWindow.SetOpen(true);
                }
                return;

            case TownBuildingButton.BuildingKind.Workshop when workshopWindow:
                if (WorkshopWindowView.ResolveKind(session) == WorkshopWindowView.WorkshopKind.None)
                {
                    statusBannerView?.ShowStatus(Presentation.BuildingWorkshopEmpty);
                    return;
                }
                workshopWindow.SetOpen(true);
                return;
        }

        var presenter = stationActionsPresenter
            ? stationActionsPresenter
            : SceneServices.Find<PuzzleStationActionsPresenter>(gameObject.scene);
        if (!presenter)
        {
            return;
        }

        // 조건 검사 및 진입을 먼저 시도하고, 성공한 경우에만 마을 장면을 숨기고 기존 역 패널을 연다.
        if (!presenter.TryOpenFromRelayout(_kind, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
            {
                statusBannerView?.ShowStatus(reason);
            }
            return;
        }

        if (worldSceneCanvas) worldSceneCanvas.SetActive(false);
        if (oldGameplayLayout) oldGameplayLayout.SetActive(true);
        if (oldTrainColumn) oldTrainColumn.SetActive(false);
        if (oldFixedBottomBar) oldFixedBottomBar.SetActive(false);
    }

    // 마을 창(시장·술집·여관·역사·생산시설·시청)은 여러 개를 함께 띄울 수 있다. 건물을 눌러도 다른 마을 창을 닫지 않는다.

    void OnDepartureRequested()
    {
        if (notebookWindow)
        {
            notebookWindow.OpenTab(NotebookWindowView.NotebookWindowTab.Map);
        }
    }

    void OnStationReturnedHome()
    {
        if (RelayoutSettings.Enabled)
        {
            if (oldGameplayLayout) oldGameplayLayout.SetActive(false);
            if (worldSceneCanvas) worldSceneCanvas.SetActive(true);
        }
    }

    void OnMapToggleRequested(GameEvents.MapToggleInfo _info)
    {
        if (!RelayoutSettings.Enabled || !notebookWindow || _info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        notebookWindow.ToggleMapTab();
    }

    void OnMapRequested(GameEvents.MapRequestInfo _info)
    {
        if (!RelayoutSettings.Enabled || !notebookWindow || _info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        notebookWindow.OpenTab(NotebookWindowView.NotebookWindowTab.Map);
    }

    void OnMapFocusRequested(GameEvents.MapFocusInfo _info)
    {
        if (!RelayoutSettings.Enabled || !notebookWindow || _info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        notebookWindow.FocusMap(_info);
    }

    void OnRoutePlanRequested(GameEvents.RoutePlanRequestInfo _info)
    {
        if (!RelayoutSettings.Enabled || !notebookWindow || _info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }
        notebookWindow.PlanRouteFromMap(_info);
    }

    void OnNotebookOpenRequested(PuzzleNotebookTab _tab)
    {
        if (!RelayoutSettings.Enabled || !notebookWindow)
        {
            return;
        }
        notebookWindow.OpenTab(_tab);
    }
}
