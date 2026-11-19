using UnityEngine;
using UnityEngine.InputSystem;

// S2: 우측 다이제틱 마을 장면(653, 82, 1247, 977). 건물 6종 클릭·호버 상태·숫자키(1~6) 입력을 처리한다.
[DisallowMultipleComponent]
public sealed class TownSceneView : MonoBehaviour
{
    [SerializeField] TownBuildingButton[] buildings;
    [SerializeField] RelayoutRootController rootController;
    [SerializeField] PuzzleInventoryPresenter inventoryPresenter;
    [SerializeField] StatusBannerView statusBannerView;

    bool textInputFocused;
    string activeHoverReason;

    void OnEnable()
    {
        if (buildings != null)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i])
                {
                    buildings[i].Clicked += OnBuildingClicked;
                    buildings[i].HoveredChanged += OnBuildingHoveredChanged;
                }
            }
        }
        GameEvents.TextInputFocusChanged += OnTextInputFocusChanged;
        GameEvents.TimeAdvanced += OnGameStateChanged;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.PuzzleRunEnded += OnRunChanged;
        GameEvents.PuzzleRunContinued += OnRunChanged;
        RefreshBuildingStates();
    }

    void OnDisable()
    {
        if (buildings != null)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i])
                {
                    buildings[i].Clicked -= OnBuildingClicked;
                    buildings[i].HoveredChanged -= OnBuildingHoveredChanged;
                }
            }
        }
        GameEvents.TextInputFocusChanged -= OnTextInputFocusChanged;
        GameEvents.TimeAdvanced -= OnGameStateChanged;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.PuzzleRunEnded -= OnRunChanged;
        GameEvents.PuzzleRunContinued -= OnRunChanged;
        textInputFocused = false;
    }

    void Update()
    {
        if (!RelayoutSettings.Enabled || textInputFocused)
        {
            return;
        }
        var inv = inventoryPresenter ? inventoryPresenter : SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        if (!inv || inv.AreShortcutsBlocked())
        {
            return;
        }
        var kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }

        if (kb.digit1Key.wasPressedThisFrame) OnBuildingClicked(TownBuildingButton.BuildingKind.Market);
        else if (kb.digit2Key.wasPressedThisFrame) OnBuildingClicked(TownBuildingButton.BuildingKind.Tavern);
        else if (kb.digit3Key.wasPressedThisFrame) OnBuildingClicked(TownBuildingButton.BuildingKind.Rest);
        else if (kb.digit4Key.wasPressedThisFrame) OnBuildingClicked(TownBuildingButton.BuildingKind.StationHall);
        else if (kb.digit5Key.wasPressedThisFrame) OnBuildingClicked(TownBuildingButton.BuildingKind.Workshop);
        else if (kb.digit6Key.wasPressedThisFrame) OnBuildingClicked(TownBuildingButton.BuildingKind.CityHall);
    }

    void OnTextInputFocusChanged(GameEvents.TextInputFocusInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle)
        {
            textInputFocused = _info.focused;
        }
    }

    void OnGameStateChanged(GameEvents.TimeAdvanceInfo _info) => RefreshBuildingStates();
    void OnCityArrived(GameEvents.CityArrivalInfo _info) => RefreshBuildingStates();
    void OnRunChanged(GameEvents.PuzzleRunInfo _info) => RefreshBuildingStates();

    public void RefreshBuildingStates()
    {
        if (buildings == null)
        {
            return;
        }
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        bool inProgress = session != null && session.RunStatus == PuzzleRunStatus.InProgress;
        bool marketOpen = session != null && session.IsMarketOpen;
        bool hasWorkshop = session != null &&
            (session.HasButchery() || session.HasModification() || session.HasRefining());

        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (!b)
            {
                continue;
            }
            if (b.Kind == TownBuildingButton.BuildingKind.Workshop)
            {
                b.SetWorkshopAvailable(hasWorkshop);
            }

            if (!inProgress)
            {
                string reason = session != null ? Presentation.PuzzleRunStatusText(session.RunStatus) : string.Empty;
                b.SetDisabled(true, reason);
                continue;
            }

            switch (b.Kind)
            {
                case TownBuildingButton.BuildingKind.Market:
                    b.SetDisabled(!marketOpen, marketOpen ? string.Empty : Presentation.PuzzleNightMarket);
                    break;
                case TownBuildingButton.BuildingKind.Workshop:
                    b.SetDisabled(!hasWorkshop, hasWorkshop ? string.Empty : Presentation.BuildingWorkshopEmpty);
                    break;
                default:
                    b.SetDisabled(false, string.Empty);
                    break;
            }
        }
    }

    void OnBuildingHoveredChanged(TownBuildingButton.BuildingKind _kind, bool _entered, string _disabledReason)
    {
        var banner = statusBannerView ? statusBannerView : SceneServices.Find<StatusBannerView>(gameObject.scene);
        if (!banner)
        {
            return;
        }
        if (_entered && !string.IsNullOrEmpty(_disabledReason))
        {
            activeHoverReason = _disabledReason;
            banner.ShowStatus(_disabledReason);
        }
        else if (!_entered && !string.IsNullOrEmpty(activeHoverReason) && activeHoverReason == _disabledReason)
        {
            activeHoverReason = string.Empty;
            banner.ShowStatus(string.Empty);
        }
    }

    void OnBuildingClicked(TownBuildingButton.BuildingKind _kind)
    {
        var inv = inventoryPresenter ? inventoryPresenter : SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        if (!inv || inv.AreShortcutsBlocked())
        {
            return;
        }
        var controller = rootController ? rootController : SceneServices.Find<RelayoutRootController>(gameObject.scene);
        controller?.OpenStationBuilding(_kind);
    }
}
