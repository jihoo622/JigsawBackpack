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
            CloseOtherTownWindows(_kind);
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
                CloseOtherTownWindows(_kind);
                marketWindow.SetOpen(true);
                return;

            case TownBuildingButton.BuildingKind.Tavern when tavernWindow:
                if (session.TryEnterTavern())
                {
                    CloseOtherTownWindows(_kind);
                    tavernWindow.SetOpen(true);
                }
                return;

            case TownBuildingButton.BuildingKind.Rest when restWindow:
                CloseOtherTownWindows(_kind);
                restWindow.SetOpen(true);
                return;

            case TownBuildingButton.BuildingKind.StationHall when stationHallWindow:
                if (session.TryEnterWagonFacility())
                {
                    CloseOtherTownWindows(_kind);
                    stationHallWindow.SetOpen(true);
                }
                return;

            case TownBuildingButton.BuildingKind.Workshop when workshopWindow:
                if (WorkshopWindowView.ResolveKind(session) == WorkshopWindowView.WorkshopKind.None)
                {
                    statusBannerView?.ShowStatus(Presentation.BuildingWorkshopEmpty);
                    return;
                }
                CloseOtherTownWindows(_kind);
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

    // 마을 창(시장·술집·여관·역사·생산시설·시청)은 겹치지 않도록 한 번에 하나만 연다.
    // _keep으로 넘긴 건물의 창을 제외한 나머지 마을 창을 모두 닫는다. 개인 창(화차·노트·렌즈)은 건드리지 않는다.
    void CloseOtherTownWindows(TownBuildingButton.BuildingKind _keep)
    {
        if (_keep != TownBuildingButton.BuildingKind.Market && marketWindow && marketWindow.IsOpen)
        {
            marketWindow.SetOpen(false);
        }
        if (_keep != TownBuildingButton.BuildingKind.Tavern && tavernWindow && tavernWindow.IsOpen)
        {
            tavernWindow.SetOpen(false);
        }
        if (_keep != TownBuildingButton.BuildingKind.Rest && restWindow && restWindow.IsOpen)
        {
            restWindow.SetOpen(false);
        }
        if (_keep != TownBuildingButton.BuildingKind.StationHall && stationHallWindow && stationHallWindow.IsOpen)
        {
            stationHallWindow.SetOpen(false);
        }
        if (_keep != TownBuildingButton.BuildingKind.Workshop && workshopWindow && workshopWindow.IsOpen)
        {
            workshopWindow.SetOpen(false);
        }
        if (_keep != TownBuildingButton.BuildingKind.CityHall && cityHallWindow && cityHallWindow.IsOpen)
        {
            cityHallWindow.SetOpen(false);
        }
    }

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
        var winTab = _tab switch
        {
            PuzzleNotebookTab.Quests => NotebookWindowView.NotebookWindowTab.Quests,
            PuzzleNotebookTab.Proficiency => NotebookWindowView.NotebookWindowTab.Proficiency,
            PuzzleNotebookTab.Synergy => NotebookWindowView.NotebookWindowTab.Synergy,
            _ => NotebookWindowView.NotebookWindowTab.Rumors
        };
        notebookWindow.OpenTab(winTab);
    }
}
