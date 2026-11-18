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
        view.RestEarlierRequested += ShowEarlierRest;
        view.RestLaterRequested += ShowLaterRest;
        GameEvents.RestChanged += OnRestChanged;
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
        GameEvents.JourneyChanged += OnJourneyChanged;
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
        view.RestEarlierRequested -= ShowEarlierRest;
        view.RestLaterRequested -= ShowLaterRest;
        GameEvents.RestChanged -= OnRestChanged;
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
        GameEvents.JourneyChanged -= OnJourneyChanged;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.ButcheryChanged -= OnButcheryChanged;
        GameEvents.ModificationChanged -= OnModificationChanged;
        GameEvents.RefiningChanged -= OnRefiningChanged;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.PuzzleRunEnded -= OnRunEnded;
    }

    int restTargetHour;
    long shownRestStep = long.MinValue;

    void Refresh()
    {
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress;
        // 도축 버튼이 씬에 없는 판은 조회조차 하지 않는다. 상태 조회가 상태를 만들기 때문이다.
        var butchery = view.HasButcheryButton ? session.ButcheryAction() : null;
        bool available = butchery != null;
        PuzzleButcheryPhase phase = available ? session.ButcheryPhase() : PuzzleButcheryPhase.Idle;
        // 확인 버튼 견적은 맡기기 전(대기·화물 있음)에만 보인다. 맡긴 뒤에는 상태 문구가 남은 시간을 보인다.
        long butcheryCost = -1;
        long butcheryHours = 0;
        if (available && session.TryQuoteButchery(out long quotedButcheryFee, out long quotedButcheryHours))
        {
            butcheryCost = quotedButcheryFee;
            butcheryHours = quotedButcheryHours;
        }
        bool butcheryNoFunds = butcheryCost >= 0 && session.Funds < butcheryCost;
        string hint = !available
            ? string.Empty
            : butcheryNoFunds
                ? Presentation.PuzzleButcheryInsufficientFunds(butcheryCost)
                : Presentation.StationButcheryStatus(phase, session.ButcheryRemainingHours());
        if (!view.ShowingButchery)
        {
            session.CloseButchery();
        }
        var modification = view.HasModificationButton ? session.ModificationAction() : null;
        bool modificationAvailable = modification != null;
        PuzzleModificationPhase modificationPhase = modificationAvailable
            ? session.ModificationPhase()
            : PuzzleModificationPhase.Idle;
        TrainventoryWorkData selectedMode = null;
        bool modificationModeSelected = modificationAvailable && session.TryGetSelectedModificationMode(out selectedMode);
        // 역 시설·작업장은 시간을 쓰지 않는다. 이동 중에는 쓸 수 없다.
        bool canSpendHour = !session.IsTraveling;
        bool modificationConfirmable = modificationAvailable && session.ModificationConfirmable() && canSpendHour;
        long modificationCost = -1;
        long modificationHours = 0;
        if (modificationAvailable && session.TryQuoteModification(out long quotedModificationFee, out long quotedModificationHours))
        {
            modificationCost = quotedModificationFee;
            modificationHours = quotedModificationHours;
        }
        bool modificationNoFunds = modificationCost >= 0 && session.Funds < modificationCost;
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
        long refiningCost = -1;
        long refiningHours = 0;
        if (refiningAvailable && session.TryQuoteRefining(out long quotedRefiningFee, out long quotedRefiningHours))
        {
            refiningCost = quotedRefiningFee;
            refiningHours = quotedRefiningHours;
        }
        bool refiningNoFunds = refiningCost >= 0 && session.Funds < refiningCost;
        string refiningHint = !refiningAvailable
            ? string.Empty
            : refiningNoFunds
                ? Presentation.PuzzleRefiningInsufficientFunds(refiningCost)
                : Presentation.StationRefiningStatus(refiningPhase, session.RefiningRemainingHours());
        if (!view.ShowingRefining)
        {
            session.CloseRefining();
        }
        view.Refresh(session.IsMarketOpen && canSpendHour, session.MarketEntered, inProgress, canSpendHour,
            available, available && phase != PuzzleButcheryPhase.Working,
            available && session.ButcheryConfirmable() && canSpendHour, hint, butcheryCost, butcheryHours,
            Presentation.StationActionDescription(butchery));
        view.RefreshModification(inProgress, modificationAvailable, modificationPhase, modificationModeSelected,
            modificationConfirmable, modificationModeName, modificationHint, modificationCost, modificationHours,
            Presentation.StationActionDescription(modification));
        view.RefreshRefining(inProgress, refiningAvailable,
            refiningAvailable && refiningPhase != PuzzleRefiningPhase.Working,
            refiningAvailable && session.RefiningConfirmable() && canSpendHour, refiningHint, refiningCost, refiningHours,
            Presentation.StationActionDescription(refining));
        bool hasAnyFacility = available || modificationAvailable || refiningAvailable;
        view.UpdateFacilityCard(hasAnyFacility && inProgress);
        RefreshRest();
    }

    string FirstModificationModeName()
    {
        var modes = session.ModificationModes();
        return modes.Count > 0 ? modes[0].displayName : string.Empty;
    }

    // 시장·술집·화차 시설은 시간을 쓰지 않고 들어간다. 이동 중이거나 판이 끝났으면 화면을 열지 않는다.
    // 뒤로 갔다가 같은 시설에 다시 들어가면 시간을 다시 쓰지 않는다. 다른 역 행동을 고르면 다시 쓴다.
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

    // 휴식 화면을 열 때마다 목표 시각을 지금 + restHours로 다시 잡는다. 쉬는 중이면 그 목표를 그대로 보인다.
    void OpenRest()
    {
        session.LeaveFacility();
        if (!session.IsResting && session.Time != null)
        {
            restTargetHour = (session.Time.Hour + (int)TableManager.Instance.RequireValid().PuzzleTime.restHours) %
                PuzzleTimeManager.HOURS_PER_DAY;
        }
        view.ShowRest();
        RefreshRest();
    }

    void ShiftRestTarget(int _hours)
    {
        if (session.IsResting)
        {
            return;
        }
        restTargetHour = ((restTargetHour + _hours) % PuzzleTimeManager.HOURS_PER_DAY + PuzzleTimeManager.HOURS_PER_DAY) %
            PuzzleTimeManager.HOURS_PER_DAY;
        RefreshRest();
    }

    void ShowEarlierRest()
    {
        ShiftRestTarget(-1);
    }

    void ShowLaterRest()
    {
        ShiftRestTarget(1);
    }

    // 쉬는 동안 남은 시간 글자를 화면 시계 단위(10분)로 갱신한다.
    void Update()
    {
        if (session != null && session.Time != null && view && view.ShowingRest &&
            session.Time.DisplayStep != shownRestStep)
        {
            RefreshRest();
        }
    }

    void RefreshRest()
    {
        if (session == null || session.Time == null)
        {
            return;
        }
        shownRestStep = session.Time.DisplayStep;
        if (session.IsResting)
        {
            long target = session.RestTargetHours.Value;
            view.RefreshRest(Presentation.StationRestStop(session.Time.HourAt(target),
                Presentation.ClockDuration(target - session.Time.ContinuousHours)), false, true);
            return;
        }
        view.RefreshRest(Presentation.StationRestUntil(restTargetHour,
                Presentation.ClockDuration(session.RestRemainingHoursFor(restTargetHour))),
            session.RunStatus == PuzzleRunStatus.InProgress && !session.IsTraveling,
            session.CanRestUntil(restTargetHour));
    }

    void OnRestChanged(GameEvents.RestInfo _info)
    {
        if (_info.session == session)
        {
            RefreshRest();
        }
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
        session.LeaveFacility();
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
        session.LeaveFacility();
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
        session.LeaveFacility();
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

    // 쉬는 중이면 멈추고, 아니면 고른 시각까지 빨리 감기를 시작한다.
    void Rest()
    {
        if (session.IsResting)
        {
            session.StopRest();
        }
        else if (!session.StartRest(restTargetHour))
        {
            view.ShowRestNotice(Presentation.PuzzleTimeOverflow);
        }
        RefreshRest();
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

    // 출발하면 역 시설을 쓸 수 없고 도착하면 다시 쓸 수 있다.
    void OnJourneyChanged(GameEvents.JourneyInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
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
