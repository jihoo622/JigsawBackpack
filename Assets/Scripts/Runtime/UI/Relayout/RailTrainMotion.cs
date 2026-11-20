using System.Collections;
using UnityEngine;

// 가운데 선로의 기차(기관차 + 화차 목록) 이동 연출.
// 기관차가 위쪽이므로 기차는 위로 달린다.
// 출발: 제자리에서 점점 빨라지며 위로 빠져나간다. 도착(정차): 아래에서 들어와 감속해 제자리에 멈춘다.
// 경로 자동 이동의 통과역은 멈추지 않는다: 아래에서 들어와 그대로 위로 지나간다.
// 움직이는 것은 그림 위치뿐이고 게임 상태와 무관하다. 선로 루트의 RectMask2D가 선로 밖으로 나간 부분을 가린다.
[DisallowMultipleComponent]
public sealed class RailTrainMotion : MonoBehaviour
{
    [SerializeField] RectTransform[] movingParts;
    [Tooltip("위로 빠져나가고 아래에서 들어오는 거리(px). 0이면 선로 높이를 쓴다.")]
    [SerializeField] float travelDistance;
    [Tooltip("도착해 멈출 때까지 걸리는 시간(초)")]
    [SerializeField] float arrivalSeconds = 0.9f;

    Vector2[] basePositions;
    float offset;
    Coroutine routine;

    // 지금 제자리에 서 있지 않은지(출발 중·떠나 있음·도착 중)
    public bool IsAway => routine != null || !Mathf.Approximately(offset, 0f);

    float Distance
    {
        get
        {
            if (travelDistance > 0f)
            {
                return travelDistance;
            }
            var rt = transform as RectTransform;
            return rt ? rt.rect.height : 1000f;
        }
    }

    void Awake()
    {
        CaptureBase();
    }

    void OnDisable()
    {
        StopRoutine();
        SetOffset(0f);
    }

    void CaptureBase()
    {
        if (basePositions != null || movingParts == null)
        {
            return;
        }
        basePositions = new Vector2[movingParts.Length];
        for (int i = 0; i < movingParts.Length; i++)
        {
            basePositions[i] = movingParts[i] ? movingParts[i].anchoredPosition : Vector2.zero;
        }
    }

    // 출발 연출. 제자리에서 떠나면 가속, 통과역에서 이어 가면(아래에서 대기 중) 같은 속도로 지나간다.
    public void Depart(float _duration)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        CaptureBase();
        StopRoutine();
        float from = offset;
        bool passingThrough = from < 0f;
        routine = StartCoroutine(Animate(from, Distance, Mathf.Max(0.05f, _duration),
            passingThrough ? (System.Func<float, float>)Linear : EaseIn, false));
    }

    // 다음 역에 닿았을 때. 정차하면 아래에서 들어와 멈추고, 통과하면 아래에서 대기했다가 다음 출발 때 그대로 지나간다.
    public void Arrive(bool _stop)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        CaptureBase();
        StopRoutine();
        if (!_stop)
        {
            SetOffset(-Distance);
            return;
        }
        routine = StartCoroutine(Animate(-Distance, 0f, arrivalSeconds, EaseOut, true));
    }

    // 이동이 취소·실패했을 때. 떠나 있으면 아래에서 다시 들어와 제자리에 멈춘다. 이미 서 있거나 도착 중이면 그대로 둔다.
    public void ReturnToStation()
    {
        if (!isActiveAndEnabled || arriving || Mathf.Approximately(offset, 0f) && routine == null)
        {
            return;
        }
        Arrive(true);
    }

    bool arriving;

    IEnumerator Animate(float _from, float _to, float _duration, System.Func<float, float> _ease, bool _arrival)
    {
        arriving = _arrival;
        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOffset(Mathf.LerpUnclamped(_from, _to, _ease(Mathf.Clamp01(elapsed / _duration))));
            yield return null;
        }
        SetOffset(_to);
        arriving = false;
        routine = null;
    }

    void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        arriving = false;
    }

    void SetOffset(float _offset)
    {
        offset = _offset;
        if (movingParts == null || basePositions == null)
        {
            return;
        }
        for (int i = 0; i < movingParts.Length; i++)
        {
            if (movingParts[i])
            {
                movingParts[i].anchoredPosition = basePositions[i] + new Vector2(0f, _offset);
            }
        }
    }

    static float Linear(float _t) => _t;
    static float EaseIn(float _t) => _t * _t * _t;
    // 마지막에 부드럽게 서도록 감속한다.
    static float EaseOut(float _t)
    {
        float inv = 1f - _t;
        return 1f - inv * inv * inv;
    }
}
