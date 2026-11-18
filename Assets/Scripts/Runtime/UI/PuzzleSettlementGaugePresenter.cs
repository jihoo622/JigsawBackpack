using UnityEngine;

// 시간 칸 정산 표시의 값 계산. 남은 시간·눈금·진행 띠는 시간 시스템의 실제 정산 주기(테이블 activationIntervalTurns)에서,
// 예상치는 실제 정산이 쓰는 평가(TrainSynergyDelta)를 재사용한다.
public sealed class PuzzleSettlementGaugePresenter : MonoBehaviour
{
    [SerializeField] PuzzleSettlementGaugeView view;

    PuzzleInventoryService session;
    float[] tickPositions;

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
        tickPositions = new float[view.TickCount];
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
        long interval = (long)TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalTurns;
        if (interval <= 0)
        {
            view.Clear(Presentation.GaugeInline(0, 0m));
            return;
        }

        long elapsedTurns = session.ElapsedTurns;
        long elapsedInCycle = elapsedTurns % interval;
        long remaining = interval - elapsedInCycle;
        // 막대는 하루(0~1)다. 정산 시각은 경과 턴 기준 주기라 지금 위치에서 거꾸로·앞으로 잰다.
        const float DAY = PuzzleTimeManager.TURNS_PER_DAY;
        float now = session.Time.TurnOfDay / DAY;
        float start = Mathf.Repeat(now - elapsedInCycle / DAY, 1f);
        int count = 0;
        for (long offset = remaining; offset <= PuzzleTimeManager.TURNS_PER_DAY && count < view.TickCount; offset += interval)
        {
            tickPositions[count] = Mathf.Repeat(now + offset / DAY, 1f);
            count++;
        }
        view.Render(Presentation.GaugeInline(remaining, session.TrainSynergyDelta), start, now, tickPositions, count);
    }
}
