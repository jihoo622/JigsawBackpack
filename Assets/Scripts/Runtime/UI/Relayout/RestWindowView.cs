using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S6: 여관 부동 창. 1~4턴 휴식 시간 조정·실행, 아침까지 대기, 결산 미리보기.
[DisallowMultipleComponent]
public sealed class RestWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI turnLabel;
    [SerializeField] Button earlierButton;
    [SerializeField] Button laterButton;
    [SerializeField] Button restButton;
    [SerializeField] TextMeshProUGUI restButtonLabel;
    [SerializeField] Button waitMorningButton;
    [SerializeField] TextMeshProUGUI waitMorningLabel;
    [SerializeField] TextMeshProUGUI settlementPreviewText;
    [SerializeField] TextMeshProUGUI statusText;

    int restTurns = 1;
    PuzzleInventoryService session;

    public FloatingWindow FloatingWindow => floatingWindow;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (earlierButton) earlierButton.onClick.AddListener(OnEarlier);
        if (laterButton) laterButton.onClick.AddListener(OnLater);
        if (restButton) restButton.onClick.AddListener(OnRest);
        if (waitMorningButton) waitMorningButton.onClick.AddListener(OnWaitMorning);
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.PuzzleRunSettled += OnRunEnded;
        GameEvents.PuzzleRunEnded += OnRunEnded;
        PuzzleMapPresenter.TravelAnimatingChanged += OnTravelAnimatingChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (earlierButton) earlierButton.onClick.RemoveListener(OnEarlier);
        if (laterButton) laterButton.onClick.RemoveListener(OnLater);
        if (restButton) restButton.onClick.RemoveListener(OnRest);
        if (waitMorningButton) waitMorningButton.onClick.RemoveListener(OnWaitMorning);
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.PuzzleRunSettled -= OnRunEnded;
        GameEvents.PuzzleRunEnded -= OnRunEnded;
        PuzzleMapPresenter.TravelAnimatingChanged -= OnTravelAnimatingChanged;
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (_open)
        {
            session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
            if (TableManager.Instance != null && TableManager.Instance.IsValid)
            {
                restTurns = Mathf.Clamp(TableManager.Instance.RequireValid().PuzzleTime.restTurns, 1, PuzzleInventoryService.MaxRestTurns);
            }
        }
        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
        if (_open)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session == null || session.Time == null)
        {
            return;
        }
        if (titleText)
        {
            titleText.text = Presentation.RestWindowTitle(session.CityData?.displayName ?? string.Empty);
        }
        bool inProgress = session.RunStatus == PuzzleRunStatus.InProgress && !PuzzleMapPresenter.IsAnyTravelAnimating;
        bool canRest = inProgress && session.CanRest(restTurns);
        if (turnLabel)
        {
            turnLabel.text = Presentation.StationRestUntil(session.TurnSlotAfter(restTurns), restTurns);
        }
        if (earlierButton) earlierButton.interactable = inProgress;
        if (laterButton) laterButton.interactable = inProgress;
        if (restButton) restButton.interactable = canRest;
        if (restButtonLabel) restButtonLabel.text = Presentation.StationRest;

        int turnsUntilDay = (int)session.Time.TurnsUntilDaylight;
        bool canWaitMorning = inProgress && session.Time.IsNight && turnsUntilDay > 0 && session.CanRest(turnsUntilDay);
        if (waitMorningButton) waitMorningButton.interactable = canWaitMorning;
        if (waitMorningLabel) waitMorningLabel.text = "아침까지 대기";

        if (settlementPreviewText)
        {
            settlementPreviewText.text = Presentation.CityHallGoalFormat(
                session.Funds, session.TargetFunds, session.Time.RemainingTurns);
        }
        if (statusText)
        {
            statusText.text = Presentation.RestWindowHint;
        }
    }

    void OnEarlier() => ShiftTurns(-1);
    void OnLater() => ShiftTurns(1);

    void ShiftTurns(int _delta)
    {
        int max = PuzzleInventoryService.MaxRestTurns;
        restTurns = ((restTurns - 1 + _delta) % max + max) % max + 1;
        Refresh();
    }

    void OnRest()
    {
        if (!PuzzleMapPresenter.IsAnyTravelAnimating && session != null && session.Rest(restTurns))
        {
            Refresh();
        }
    }

    void OnWaitMorning()
    {
        if (PuzzleMapPresenter.IsAnyTravelAnimating || session == null || session.Time == null || !session.Time.IsNight)
        {
            return;
        }
        int turns = (int)session.Time.TurnsUntilDaylight;
        if (turns > 0 && session.Rest(turns))
        {
            Refresh();
        }
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info) => Refresh();
    void OnCityArrived(GameEvents.CityArrivalInfo _info) => Refresh();
    void OnFundsChanged(GameEvents.FundsInfo _info) => Refresh();
    void OnRunEnded(GameEvents.PuzzleRunInfo _result) => Refresh();
    void OnTravelAnimatingChanged(bool _animating) => Refresh();
}
