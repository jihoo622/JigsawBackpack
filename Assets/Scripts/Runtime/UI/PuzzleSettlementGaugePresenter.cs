using System;
using UnityEngine;

// 시간 칸 정산 표시의 값 계산. 낮·밤 막대와 같은 창(지금 턴의 가운데를 고정한 앞뒤 12시간)에 정산 시각 눈금을 놓는다.
// 표시선은 움직이지 않고 턴이 지나면 눈금이 한 칸씩 밀린다. 진행 띠는 지금부터 다음 정산까지다.
// 주기는 시간 시스템의 실제 정산 주기(테이블 activationIntervalHours), 예상치는 실제 정산이 쓰는 평가(TrainSynergyDelta)다.
// 턴이 바뀌거나 예상치가 바뀔 때만 다시 그린다.
public sealed class PuzzleSettlementGaugePresenter : MonoBehaviour
{
    [SerializeField] PuzzleSettlementGaugeView view;

    PuzzleInventoryService session;
    float[] tickPositions;
    long renderedStep = long.MinValue;
    decimal renderedEstimate = decimal.MinValue;
    string label;

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
        GameEvents.PuzzleActionCommitted += OnActionCommitted;
        GameEvents.PuzzleRunStarted += OnRunStarted;
        Invalidate();
        Render();
    }

    void OnDisable()
    {
        GameEvents.PuzzleActionCommitted -= OnActionCommitted;
        GameEvents.PuzzleRunStarted -= OnRunStarted;
    }

    void Update()
    {
        Render();
    }

    // 배치·구매·판매 등 확정 조작마다 열차 시너지 합이 바뀐다.
    void OnActionCommitted(GameEvents.PuzzleActionInfo _info)
    {
        if (_info.session == session)
        {
            Invalidate();
        }
    }

    void OnRunStarted(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session == session)
        {
            Invalidate();
        }
    }

    void Invalidate()
    {
        renderedStep = long.MinValue;
    }

    void Render()
    {
        if (session == null || session.Time == null)
        {
            return;
        }
        double interval = TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours;
        if (interval <= 0d)
        {
            view.Clear(Presentation.GaugeInline(0, 0m));
            return;
        }
        decimal estimate = session.TrainSynergyDelta;
        long step = session.Time.ElapsedHours;
        if (step == renderedStep && estimate == renderedEstimate)
        {
            return;
        }
        renderedStep = step;
        renderedEstimate = estimate;

        const double DAY = PuzzleTimeManager.HOURS_PER_DAY;
        const double HALF = DAY / 2d;
        double now = session.Time.ElapsedHours + PuzzleTimeManager.HOURS_PER_TURN / 2d;
        // 정산은 경과 시간이 주기의 배수인 시각이다. 창 안의 경계를 지난 것부터 차례로 놓고 다음 정산을 강조한다.
        double next = (Math.Floor(now / interval) + 1d) * interval;
        int count = 0;
        int nextIndex = -1;
        for (double at = Math.Ceiling((now - HALF) / interval) * interval;
            at <= now + HALF && count < tickPositions.Length; at += interval)
        {
            if (at <= 0d)
            {
                continue;
            }
            if (nextIndex < 0 && at >= next)
            {
                nextIndex = count;
            }
            tickPositions[count] = (float)(0.5d + (at - now) / DAY);
            count++;
        }
        view.Render(0.5f, (float)(0.5d + (next - now) / DAY), tickPositions, count, nextIndex);
        label = Presentation.GaugeInline((long)(next - session.Time.ElapsedHours), estimate);
        view.SetLabel(label);
    }
}
