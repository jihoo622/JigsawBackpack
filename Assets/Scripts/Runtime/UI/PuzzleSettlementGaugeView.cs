using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상단 시간 칸 아랫줄의 정산 표시. 24시간 낮·밤 막대 위에 정산 시각 눈금을 긋고(다음 정산은 강조색),
// 막대 아래 띠로 이번 정산 주기의 진행을 채운다. 오른쪽 글자는 남은 시간·예상치다.
// 눈금은 프리팹에 미리 배치한 Image를 켜고 끈다(런타임 생성 금지). 값은 PuzzleSettlementGaugePresenter가 넣는다.
public sealed class PuzzleSettlementGaugeView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    // 진행 띠. 자정을 넘는 주기는 두 칸으로 나눈다.
    [SerializeField] Image progressA;
    [SerializeField] Image progressB;
    [SerializeField] RectTransform[] ticks;

    internal int TickCount => ticks == null ? 0 : ticks.Length;

    internal void Initialize()
    {
        if (!label || !progressA || !progressB || ticks == null || ticks.Length == 0 ||
            Array.Exists(ticks, _tick => _tick == null))
        {
            throw new InvalidOperationException("정산 표시 View에 글자·진행 띠 2개·미리 배치한 눈금 참조가 필요합니다.");
        }
        label.color = Presentation.PuzzleHeading;
        progressA.color = Presentation.PuzzleTimeAccent;
        progressB.color = Presentation.PuzzleTimeAccent;
    }

    // _start·_now: 하루(0~1) 위 이번 주기 시작과 지금. _ticks: 다음 정산부터 차례로 하루 위 위치(개수는 _tickCount).
    internal void Render(string _text, float _start, float _now, float[] _ticks, int _tickCount)
    {
        label.text = _text;
        ShowProgress(_start, _now);
        for (int i = 0; i < ticks.Length; i++)
        {
            bool used = i < _tickCount;
            ticks[i].gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }
            ticks[i].anchorMin = new Vector2(_ticks[i], ticks[i].anchorMin.y);
            ticks[i].anchorMax = new Vector2(_ticks[i], ticks[i].anchorMax.y);
            ticks[i].anchoredPosition = new Vector2(0f, ticks[i].anchoredPosition.y);
            if (ticks[i].TryGetComponent<Graphic>(out var graphic))
            {
                graphic.color = i == 0 ? Presentation.PuzzleTimeAccent : Presentation.GaugeTick;
            }
        }
    }

    internal void Clear(string _text)
    {
        label.text = _text;
        progressA.gameObject.SetActive(false);
        progressB.gameObject.SetActive(false);
        for (int i = 0; i < ticks.Length; i++)
        {
            ticks[i].gameObject.SetActive(false);
        }
    }

    void ShowProgress(float _start, float _now)
    {
        bool wraps = _now < _start;
        progressA.gameObject.SetActive(true);
        SetSpan(progressA, _start, wraps ? 1f : _now);
        progressB.gameObject.SetActive(wraps);
        if (wraps)
        {
            SetSpan(progressB, 0f, _now);
        }
    }

    static void SetSpan(Image _segment, float _from, float _to)
    {
        var rect = _segment.rectTransform;
        rect.anchorMin = new Vector2(_from, rect.anchorMin.y);
        rect.anchorMax = new Vector2(_to, rect.anchorMax.y);
        rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
        rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
    }
}
