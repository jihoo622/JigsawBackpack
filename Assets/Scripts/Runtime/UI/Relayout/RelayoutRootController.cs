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
    [SerializeField] PuzzleStationActionsPresenter stationActionsPresenter;
    [SerializeField] StatusBannerView statusBannerView;

    bool defaultsCaptured;
    bool defaultGameplayLayoutActive;
    bool defaultTrainColumnActive;
    bool defaultFixedBottomBarActive;
    bool defaultCityNavigationActive;
    bool defaultJourneyFooterActive;
    bool defaultRumorSideHandleActive;

    void Awake()
    {
        CaptureDefaultsIfNeeded();
    }

    void OnEnable()
    {
        CaptureDefaultsIfNeeded();
        RelayoutSettings.Changed += ApplyRelayoutState;
        if (stationActionsPresenter)
        {
            stationActionsPresenter.ReturnedHome += OnStationReturnedHome;
        }
        ApplyRelayoutState(RelayoutSettings.Enabled);
    }

    void OnDisable()
    {
        RelayoutSettings.Changed -= ApplyRelayoutState;
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
        defaultGameplayLayoutActive = oldGameplayLayout && oldGameplayLayout.activeSelf;
        defaultTrainColumnActive = oldTrainColumn && oldTrainColumn.activeSelf;
        defaultFixedBottomBarActive = oldFixedBottomBar && oldFixedBottomBar.activeSelf;
        defaultCityNavigationActive = oldCityNavigation && oldCityNavigation.activeSelf;
        defaultJourneyFooterActive = oldJourneyFooter && oldJourneyFooter.activeSelf;
        defaultRumorSideHandleActive = oldRumorSideHandle && oldRumorSideHandle.activeSelf;
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
        }
    }

    public void OpenStationBuilding(TownBuildingButton.BuildingKind _kind)
    {
        if (_kind == TownBuildingButton.BuildingKind.CityHall)
        {
            statusBannerView?.ShowStatus(Presentation.BuildingCityHallPlaceholder);
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
}
