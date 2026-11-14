using UnityEngine;

public sealed class PuzzleStationActionsPresenter : MonoBehaviour
{
    [SerializeField] PuzzleStationActionsView view;
    PuzzleInventoryService session;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        view.MarketRequested += OpenMarket;
        view.TavernRequested += OpenTavern;
        view.RestRequested += OpenRest;
        view.TravelRequested += OpenMap;
        view.RestTimeRequested += Rest;
        view.ButcheryRequested += OpenButchery;
        view.ButcheryConfirmed += ConfirmButchery;
        view.ModificationRequested += OpenModification;
        view.ModificationModeRequested += ToggleModificationMode;
        view.ModificationConfirmed += ConfirmModification;
        view.RefiningRequested += OpenRefining;
        view.RefiningConfirmed += ConfirmRefining;
        view.ActionBackRequested += OnBack;
        GameEvents.CityArrived += OnArrived;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.ButcheryChanged += OnButcheryChanged;
        GameEvents.ModificationChanged += OnModificationChanged;
        GameEvents.RefiningChanged += OnRefiningChanged;
        GameEvents.FundsChanged += OnFundsChanged;
        Refresh();
    }

    void OnDisable()
    {
        view.MarketRequested -= OpenMarket;
        view.TavernRequested -= OpenTavern;
        view.RestRequested -= OpenRest;
        view.TravelRequested -= OpenMap;
        view.RestTimeRequested -= Rest;
        view.ButcheryRequested -= OpenButchery;
        view.ButcheryConfirmed -= ConfirmButchery;
        view.ModificationRequested -= OpenModification;
        view.ModificationModeRequested -= ToggleModificationMode;
        view.ModificationConfirmed -= ConfirmModification;
        view.RefiningRequested -= OpenRefining;
        view.RefiningConfirmed -= ConfirmRefining;
        view.ActionBackRequested -= OnBack;
        GameEvents.CityArrived -= OnArrived;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.ButcheryChanged -= OnButcheryChanged;
        GameEvents.ModificationChanged -= OnModificationChanged;
        GameEvents.RefiningChanged -= OnRefiningChanged;
        GameEvents.FundsChanged -= OnFundsChanged;
    }

    void Refresh()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        // 도축 버튼이 씬에 없는 판은 조회조차 하지 않는다. 상태 조회가 상태를 만들기 때문이다.
        bool available = view.HasButcheryButton && session.HasButchery();
        PuzzleButcheryPhase phase = available ? session.ButcheryPhase() : PuzzleButcheryPhase.Idle;
        long butcheryCost = available ? session.ButcheryCost() : -1;
        bool butcheryNoFunds = available && phase == PuzzleButcheryPhase.Idle && butcheryCost >= 0 &&
            session.Funds < butcheryCost;
        string hint = !available
            ? string.Empty
            : butcheryNoFunds
                ? Presentation.PuzzleButcheryInsufficientFunds(butcheryCost)
                : Presentation.StationButcheryStatus(phase, session.ButcheryRemainingMinutes());
        if (!view.ShowingButchery)
        {
            session.CloseButchery();
        }
        bool modificationAvailable = view.HasModificationButton && session.HasModification();
        PuzzleActionModeData selectedMode = null;
        bool modificationModeSelected = modificationAvailable && session.TryGetSelectedModificationMode(out selectedMode);
        bool modificationConfirmable = modificationAvailable && session.ModificationConfirmable();
        long modificationCost = modificationModeSelected ? selectedMode.cost : -1;
        bool modificationNoFunds = modificationAvailable && modificationModeSelected && modificationCost >= 0 &&
            session.Funds < modificationCost;
        string modificationModeName = modificationModeSelected
            ? selectedMode.displayName
            : FirstModificationModeName();
        string modificationHint = !modificationAvailable
            ? string.Empty
            : modificationNoFunds
                ? Presentation.PuzzleModificationInsufficientFunds(modificationCost)
                : Presentation.PuzzleModificationStatus(session);
        if (!view.ShowingModification)
        {
            session.CloseModification();
        }
        // 정제 버튼이 씬에 없는 판은 조회조차 하지 않는다. 상태 조회가 상태를 만들기 때문이다.
        bool refiningAvailable = view.HasRefiningButton && session.HasRefining();
        PuzzleRefiningPhase refiningPhase = refiningAvailable ? session.RefiningPhase() : PuzzleRefiningPhase.Idle;
        long refiningCost = refiningAvailable ? session.RefiningCost() : -1;
        bool refiningNoFunds = refiningAvailable && refiningPhase == PuzzleRefiningPhase.Idle && refiningCost >= 0 &&
            session.Funds < refiningCost;
        string refiningHint = !refiningAvailable
            ? string.Empty
            : refiningNoFunds
                ? Presentation.PuzzleRefiningInsufficientFunds(refiningCost)
                : Presentation.StationRefiningStatus(refiningPhase, session.RefiningRemainingMinutes());
        if (!view.ShowingRefining)
        {
            session.CloseRefining();
        }
        view.Refresh(session.IsMarketOpen, inProgress, TableManager.Instance.PuzzleTime.restMinutes,
            available, available && phase != PuzzleButcheryPhase.Working,
            available && session.ButcheryConfirmable(), hint, butcheryCost);
        view.RefreshModification(inProgress, modificationAvailable, modificationModeSelected,
            modificationConfirmable, modificationModeName, modificationHint, modificationCost);
        view.RefreshRefining(inProgress, refiningAvailable,
            refiningAvailable && refiningPhase != PuzzleRefiningPhase.Working,
            refiningAvailable && session.RefiningConfirmable(), refiningHint, refiningCost);
        bool hasAnyFacility = (available && view.HasButcheryButton) ||
                              (modificationAvailable && view.HasModificationButton) ||
                              (refiningAvailable && view.HasRefiningButton);
        view.UpdateFacilityCard(hasAnyFacility && inProgress);
    }

    string FirstModificationModeName()
    {
        var modes = session.ModificationModes();
        return modes.Count > 0 ? modes[0].displayName : string.Empty;
    }

    void OpenMarket()
    {
        if (session.IsMarketOpen)
        {
            view.ShowMarket();
        }
    }

    void OpenRest()
    {
        view.ShowRest();
    }

    void OpenTavern()
    {
        if (session.RunStatus == PuzzleRunStatus.InProgress)
        {
            view.ShowTavern();
        }
    }

    // View의 content를 먼저 켜고 세션에 조작 허락을 받는다. 열리기 전 View는 초기화·좌표가 준비되지 않는다.
    void OpenButchery()
    {
        view.ShowButchery();
        if (!session.OpenButchery())
        {
            view.ShowHome();
        }
        Refresh();
    }

    void ConfirmButchery()
    {
        if (!session.TryConfirmButchery(out string reason))
        {
            Refresh();
            view.ShowButcheryNotice(reason);
            return;
        }
        Refresh();
    }

    void OpenModification()
    {
        view.ShowModification();
        if (!session.OpenModification())
        {
            view.ShowHome();
        }
        Refresh();
    }

    void ToggleModificationMode()
    {
        if (!view.HasModificationButton || !session.HasModification())
        {
            return;
        }
        var modes = session.ModificationModes();
        if (modes.Count == 0)
        {
            return;
        }
        session.TryToggleModificationMode(modes[0].id, out _);
    }

    void ConfirmModification()
    {
        if (!session.TryConfirmModification(out _, out PuzzleModificationFailure failure))
        {
            Refresh();
            view.ShowModificationNotice(Presentation.PuzzleModificationReason(failure, session));
            return;
        }
        Refresh();
    }

    // View의 content를 먼저 켜고 세션에 조작 허락을 받는다. 열리기 전 View는 초기화·좌표가 준비되지 않는다.
    void OpenRefining()
    {
        view.ShowRefining();
        if (!session.OpenRefining())
        {
            view.ShowHome();
        }
        Refresh();
    }

    void ConfirmRefining()
    {
        if (!session.TryConfirmRefining(out string reason))
        {
            Refresh();
            view.ShowRefiningNotice(reason);
            return;
        }
        Refresh();
    }

    void OnBack()
    {
        if (view.ShowingButchery && !session.TryLeaveButchery(out string reason))
        {
            view.ShowButcheryNotice(reason);
            return;
        }
        if (view.ShowingModification && !session.CanLeaveModification())
        {
            view.ShowModificationNotice(Presentation.PuzzleModificationReturnItems());
            return;
        }
        if (view.ShowingRefining && !session.TryLeaveRefining(out string refiningReason))
        {
            view.ShowRefiningNotice(refiningReason);
            return;
        }

        session.CloseButchery();
        session.CloseModification();
        session.CloseRefining();
        view.ShowHome();
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            view.SetInteraction(!_info.open);
            return;
        }
        if (_info.panel == view.PanelName)
        {
            if (!_info.open)
            {
                session.CloseButchery();
                session.CloseModification();
                session.CloseRefining();
            }
            Refresh();
        }
    }

    void OnButcheryChanged(GameEvents.ButcheryInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnModificationChanged(GameEvents.ModificationInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnRefiningChanged(GameEvents.RefiningInfo _info)
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

    void OpenMap()
    {
        GameEvents.RaiseMapRequested(gameObject.scene.handle);
    }

    void Rest()
    {
        if (session.RunStatus == PuzzleRunStatus.InProgress)
        {
            bool advanced = session.AdvanceMinutes(TableManager.Instance.PuzzleTime.restMinutes, PuzzleActionKind.Rest);
            view.ShowRestNotice(advanced ? Presentation.StationRestHint : Presentation.PuzzleTimeOverflow);
        }
    }

    void OnArrived(GameEvents.CityArrivalInfo _info)
    {
        view.ShowHome();
        Refresh();
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }
}
