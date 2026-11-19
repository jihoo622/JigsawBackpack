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
        ApplyRelayoutState(RelayoutSettings.Enabled);
    }

    void OnDisable()
    {
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
        if (_kind == TownBuildingButton.BuildingKind.CityHall)
        {
            statusBannerView?.ShowStatus(Presentation.BuildingCityHallPlaceholder);
            return;
        }

        if (_kind == TownBuildingButton.BuildingKind.Market && marketWindow)
        {
            var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
            if (session == null || session.RunStatus != PuzzleRunStatus.InProgress)
            {
                string runMsg = session != null ? Presentation.PuzzleRunStatusText(session.RunStatus) : string.Empty;
                if (!string.IsNullOrEmpty(runMsg)) statusBannerView?.ShowStatus(runMsg);
                return;
            }
            if (!session.IsMarketOpen)
            {
                statusBannerView?.ShowStatus(Presentation.PuzzleNightMarket);
                return;
            }
            session.TryEnterMarket();
            marketWindow.SetOpen(true);
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
