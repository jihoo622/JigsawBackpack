using UnityEngine;

// 다음 정산 게이지의 값 계산. 남은 시간과 채운 칸은 시간 시스템의 실제 정산 주기(테이블 activationIntervalHours)에서,
// 예상치는 실제 정산이 쓰는 평가(TrainSynergyDelta)를 재사용한다.
public sealed class PuzzleSettlementGaugePresenter : MonoBehaviour
{
    [SerializeField] PuzzleSettlementGaugeView view;

    PuzzleInventoryService session;

    void OnEnable()
    {
        if (!view)
        {
            Debug.LogError("정산 게이지 Presenter에 View 참조가 필요합니다.", this);
            enabled = false;
            return;
        }
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        view.Initialize();
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PuzzleActionCommitted += OnActionCommitted;
        GameEvents.PuzzleRunStarted += OnRunStarted;
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PuzzleActionCommitted -= OnActionCommitted;
        GameEvents.PuzzleRunStarted -= OnRunStarted;
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    // 배치·구매·판매 등 확정 조작마다 열차 시너지 합이 바뀐다.
    void OnActionCommitted(GameEvents.PuzzleActionInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnRunStarted(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        long interval = (long)TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours *
            PuzzleTimeManager.MINUTES_PER_HOUR;
        if (interval <= 0)
        {
            view.Clear(Presentation.GaugeUntilSettlement(0));
            return;
        }

        long elapsed = session.ElapsedTime;
        long elapsedInCycle = elapsed % interval;
        long remaining = interval - elapsedInCycle;
        int filled = view.CellCount == 0 ? 0 : (int)(elapsedInCycle / (interval / view.CellCount));
        view.Render(Presentation.GaugeUntilSettlement(remaining), session.TrainSynergyDelta, filled);
    }
}
