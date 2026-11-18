using System;
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
        view.WagonShopRequested += OpenWagonShop;
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
        GameEvents.PuzzleRunEnded += OnRunEnded;
        Refresh();
    }

    void OnDisable()
    {
        view.MarketRequested -= OpenMarket;
        view.WagonShopRequested -= OpenWagonShop;
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
        GameEvents.PuzzleRunEnded -= OnRunEnded;
    }

    void Refresh()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        // 도축 버튼이 씬에 없는 판은 조회조차 하지 않는다. 상태 조회가 상태를 만들기 때문이다.
        var butchery = view.HasButcheryButton ? session.ButcheryAction() : null;
        bool available = butchery != null;
        PuzzleButcheryPhase phase = available ? session.ButcheryPhase() : PuzzleButcheryPhase.Idle;
        long butcheryCost = available ? session.ButcheryCost() : -1;
        bool butcheryNoFunds = available && phase == PuzzleButcheryPhase.Idle && butcheryCost >= 0 &&
            session.Funds < butcheryCost;
        string hint = !available
            ? string.Empty
            : butcheryNoFunds
                ? Presentation.PuzzleButcheryInsufficientFunds(butcheryCost)
                : Presentation.StationButcheryStatus(phase, session.ButcheryRemainingTurns());
        if (!view.ShowingButchery)
        {
            session.CloseButchery();
        }
        var modification = view.HasModificationButton ? session.ModificationAction() : null;
        bool modificationAvailable = modification != null;
        PuzzleModificationPhase modificationPhase = modificationAvailable
            ? session.ModificationPhase()
            : PuzzleModificationPhase.Idle;
        PuzzleActionModeData selectedMode = null;
        bool modificationModeSelected = modificationAvailable && session.TryGetSelectedModificationMode(out selectedMode);
        // 작업대 확정은 역 행동 1턴을 쓴다. 기한을 넘기면 확정할 수 없다.
        bool canSpendTurn = session.CanSpendStationTurn;
        bool modificationConfirmable = modificationAvailable && session.ModificationConfirmable() && canSpendTurn;
        long modificationCost = modificationModeSelected ? selectedMode.cost : -1;
        bool modificationNoFunds = modificationAvailable && modificationPhase == PuzzleModificationPhase.Idle &&
            modificationModeSelected && modificationCost >= 0 && session.Funds < modificationCost;
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
        var refining = view.HasRefiningButton ? session.RefiningAction() : null;
        bool refiningAvailable = refining != null;
        PuzzleRefiningPhase refiningPhase = refiningAvailable ? session.RefiningPhase() : PuzzleRefiningPhase.Idle;
        long refiningCost = refiningAvailable ? session.RefiningCost() : -1;
        bool refiningNoFunds = refiningAvailable && refiningPhase == PuzzleRefiningPhase.Idle && refiningCost >= 0 &&
            session.Funds < refiningCost;
        string refiningHint = !refiningAvailable
            ? string.Empty
            : refiningNoFunds
                ? Presentation.PuzzleRefiningInsufficientFunds(refiningCost)
                : Presentation.StationRefiningStatus(refiningPhase, session.RefiningRemainingTurns());
        if (!view.ShowingRefining)
        {
            session.CloseRefining();
        }
        view.Refresh(session.IsMarketOpen, session.MarketEntered, inProgress, canSpendTurn,
            TableManager.Instance.PuzzleTime.restTurns,
            available, available && phase != PuzzleButcheryPhase.Working,
            available && session.ButcheryConfirmable() && canSpendTurn, hint, butcheryCost,
            Presentation.StationActionDescription(butchery));
        view.RefreshModification(inProgress, modificationAvailable, modificationPhase, modificationModeSelected,
            modificationConfirmable, modificationModeName, modificationHint, modificationCost,
            Presentation.StationActionDescription(modification));
        view.RefreshRefining(inProgress, refiningAvailable,
            refiningAvailable && refiningPhase != PuzzleRefiningPhase.Working,
            refiningAvailable && session.RefiningConfirmable() && canSpendTurn, refiningHint, refiningCost,
            Presentation.StationActionDescription(refining));
        bool hasAnyFacility = available || modificationAvailable || refiningAvailable;
        view.UpdateFacilityCard(hasAnyFacility && inProgress);
    }

    string FirstModificationModeName()
    {
        var modes = session.ModificationModes();
        return modes.Count > 0 ? modes[0].displayName : string.Empty;
    }

    // 시장은 들어갈 때 역 행동 1턴을 쓴다. 술집·화차 시설은 진입이 무료이고 소문 듣기와 화차 구매가 1턴을 쓴다.
    // 시장에 뒤로 갔다가 다시 들어가면 턴을 다시 쓰지 않는다. 다른 역 행동을 고르면 다시 쓴다.
    void OpenMarket()
    {
        if (session.TryEnterMarket())
        {
            view.ShowMarket();
        }
    }

    void OpenWagonShop()
    {
        if (session.RunStatus == PuzzleRunStatus.InProgress && session.TryEnterWagonFacility())
        {
            view.ShowWagonShop();
        }
    }

    void OpenRest()
    {
        session.ForgetPaidFacility();
        view.ShowRest();
    }

    void OpenTavern()
    {
        if (session.RunStatus == PuzzleRunStatus.InProgress && session.TryEnterTavern())
        {
            view.ShowTavern();
        }
    }

    // View의 content를 먼저 켜고 세션에 조작 허락을 받는다. 열리기 전 View는 초기화·좌표가 준비되지 않는다.
    void OpenButchery()
    {
        session.ForgetPaidFacility();
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
        SpendWorkTurn(OpenButchery, session.ButcheryAction()?.id);
    }

    void OpenModification()
    {
        session.ForgetPaidFacility();
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
        session.CloseModification();
        Refresh();
        SpendWorkTurn(OpenModification, session.ModificationAction()?.id);
    }

    // View의 content를 먼저 켜고 세션에 조작 허락을 받는다. 열리기 전 View는 초기화·좌표가 준비되지 않는다.
    void OpenRefining()
    {
        session.ForgetPaidFacility();
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
        SpendWorkTurn(OpenRefining, session.RefiningAction()?.id);
    }

    // 작업을 확정하면 역 행동 1턴을 쓴다. 작업대를 닫고 턴을 보낸 뒤, 같은 역에 그대로 있으면 결과를 수령할 작업대를 다시 연다.
    void SpendWorkTurn(Action _openWorkbench, string _actionId)
    {
        if (!view.IsOpen || session.RunStatus != PuzzleRunStatus.InProgress)
        {
            return;
        }
        string cityId = session.CurrentCityId;
        ShowHome();
        if (!session.WaitForWorkbench(_actionId, session.StationActionTurns))
        {
            return;
        }
        if (isActiveAndEnabled && view.IsOpen && session.RunStatus == PuzzleRunStatus.InProgress &&
            session.CurrentCityId == cityId)
        {
            _openWorkbench();
        }
    }

    public bool TryConsumeCancel()
    {
        if (view && view.ShowingSubscreen)
        {
            OnBack();
            return true;
        }
        return false;
    }

    void OnBack()
    {
        if (session == null)
        {
            session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        }
        if (view.ShowingButchery && (session != null && !session.TryLeaveButchery(out string reason)))
        {
            view.ShowButcheryNotice(reason);
            return;
        }
        if (view.ShowingModification && (session != null && !session.CanLeaveModification()))
        {
            view.ShowModificationNotice(Presentation.PuzzleModificationReturnItems());
            return;
        }
        if (view.ShowingRefining && (session != null && !session.TryLeaveRefining(out string refiningReason)))
        {
            view.ShowRefiningNotice(refiningReason);
            return;
        }

        ShowHome();
    }

    void ShowHome()
    {
        if (session != null)
        {
            session.CloseButchery();
            session.CloseModification();
            session.CloseRefining();
        }
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
        if (session.RunStatus == PuzzleRunStatus.InProgress && session.CanSpendStationTurn)
        {
            bool advanced = session.AdvanceTurns(TableManager.Instance.PuzzleTime.restTurns, PuzzleActionKind.Rest);
            view.ShowRestNotice(advanced ? Presentation.StationRestHint : Presentation.PuzzleTimeOverflow);
        }
    }

    void OnArrived(GameEvents.CityArrivalInfo _info)
    {
        view.ShowHome();
        Refresh();
    }

    void OnRunEnded(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session == session)
        {
            ShowHome();
        }
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }
}
