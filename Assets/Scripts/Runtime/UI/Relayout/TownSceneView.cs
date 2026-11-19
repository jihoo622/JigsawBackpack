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

    public bool IsScreenPointOverBuilding(TownBuildingButton.BuildingKind _kind, Vector2 _screenPos)
    {
        if (!gameObject.activeInHierarchy || buildings == null)
        {
            return false;
        }
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas && canvas.rootCanvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b && b.Kind == _kind && b.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, _screenPos, cam))
            {
                return true;
            }
        }
        return false;
    }

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
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.PuzzleActionCommitted += OnActionCommitted;
        GameEvents.PuzzleRunStarted += OnRunChanged;
        GameEvents.PuzzleRunSettled += OnRunChanged;
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
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.PuzzleActionCommitted -= OnActionCommitted;
        GameEvents.PuzzleRunStarted -= OnRunChanged;
        GameEvents.PuzzleRunSettled -= OnRunChanged;
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
    void OnFundsChanged(GameEvents.FundsInfo _info) => RefreshBuildingStates();
    void OnQuestChanged(GameEvents.QuestInfo _info) => RefreshBuildingStates();
    void OnRumorRecorded(GameEvents.TavernRumorInfo _info) => RefreshBuildingStates();
    void OnActionCommitted(GameEvents.PuzzleActionInfo _info) => RefreshBuildingStates();
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

        int deliverableQuests = 0;
        if (session != null)
        {
            var quests = session.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                if (q.State == TrainventoryQuestState.Active &&
                    string.Equals(q.TargetCityId, session.CurrentCityId, System.StringComparison.Ordinal))
                {
                    deliverableQuests++;
                }
            }
        }

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

            UpdateBuildingStatusLine(b, session, marketOpen, hasWorkshop, deliverableQuests);

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

    static void UpdateBuildingStatusLine(
        TownBuildingButton _button,
        PuzzleInventoryService _session,
        bool _marketOpen,
        bool _hasWorkshop,
        int _deliverableQuests)
    {
        if (_session == null || _session.CityData == null)
        {
            if (_button.Kind == TownBuildingButton.BuildingKind.Workshop)
            {
                // 생산시설은 도시 정보가 없어도 상태 줄을 항상 한 줄 보인다.
                ApplyWorkshopStatusLine(_button, null, false);
                return;
            }
            _button.SetChalkboard(string.Empty);
            _button.SetPersistentHighlight(false);
            return;
        }

        switch (_button.Kind)
        {
            case TownBuildingButton.BuildingKind.Market:
                if (_marketOpen)
                {
                    var prefs = Presentation.GetCityTradePreferences(_session.CityData);
                    _button.SetChalkboard(Presentation.BldgStatusMarketOpen(
                        _session.CityData.buyPercentOfBaseValue, prefs.ExpensiveTags.Count));
                }
                else
                {
                    _button.SetChalkboard(Presentation.BldgStatusMarketClosed);
                }
                _button.SetPersistentHighlight(false);
                break;

            case TownBuildingButton.BuildingKind.Tavern:
                if (_deliverableQuests > 0)
                {
                    _button.SetChalkboard(Presentation.BldgStatusTavernDeliverable(_deliverableQuests));
                    _button.SetPersistentHighlight(true);
                }
                else
                {
                    _button.SetChalkboard(Presentation.BldgStatusTavernOffers(_session.QuestOffers.Count));
                    _button.SetPersistentHighlight(false);
                }
                break;

            case TownBuildingButton.BuildingKind.Rest:
                _button.SetChalkboard(Presentation.BldgStatusRest);
                _button.SetPersistentHighlight(false);
                break;

            case TownBuildingButton.BuildingKind.StationHall:
                _button.SetChalkboard(Presentation.BldgStatusStation(_session.WagonCount, _session.MaxWagonCount));
                _button.SetPersistentHighlight(false);
                break;

            case TownBuildingButton.BuildingKind.Workshop:
                ApplyWorkshopStatusLine(_button, _session, _hasWorkshop);
                break;

            case TownBuildingButton.BuildingKind.CityHall:
                int goalPct = Mathf.Clamp(
                    Mathf.RoundToInt((float)_session.Funds * 100f / System.Math.Max(1L, _session.TargetFunds)),
                    0, 999);
                _button.SetChalkboard(Presentation.BldgStatusCityHall(_session.Reputation, goalPct));
                _button.SetPersistentHighlight(false);
                break;
        }
    }

    // 생산시설 상태 줄. 시설이 있으면 이름과 수수료, 없으면 '시설 없음(공터)'을 흐리게 보인다.
    static void ApplyWorkshopStatusLine(TownBuildingButton _button, PuzzleInventoryService _session, bool _hasWorkshop)
    {
        _button.SetPersistentHighlight(false);
        if (_hasWorkshop && _session != null)
        {
            var ws = _session.ButcheryAction() ?? _session.ModificationAction() ?? _session.RefiningAction();
            string title = ws != null ? ws.title : string.Empty;
            long fee = ws != null && ws.extraCost > 0 ? ws.extraCost : 0;
            _button.SetChalkboard(fee > 0 ? Presentation.BldgStatusWorkshop(title, fee) : title);
            _button.SetChalkboardDimmed(false);
            return;
        }

        _button.SetChalkboard(Presentation.BuildingWorkshopNoneLabel);
        _button.SetChalkboardDimmed(true);
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
