using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상단 시간 칸 아랫줄의 정산 표시. 지금을 가운데 고정한 24시간 낮·밤 막대 위에 정산 시각 눈금을 긋고(다음 정산은 강조색),
// 막대 아래 띠로 지금부터 다음 정산까지를 칠한다. 오른쪽 글자는 남은 시간·예상치다.
// 눈금은 프리팹에 미리 배치한 Image를 켜고 끈다(런타임 생성 금지). 값은 PuzzleSettlementGaugePresenter가 넣는다.
public sealed class PuzzleSettlementGaugeView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    // 진행 띠. 지금(가운데)에서 다음 정산까지라 창 끝을 넘지 않으므로 progressA만 쓴다. progressB는 꺼 둔다.
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

    // _from·_to: 창(0~1) 위 진행 띠 범위(지금 → 다음 정산). _ticks: 창 위 눈금 위치(개수 _tickCount), _accent: 다음 정산 눈금.
    internal void Render(float _from, float _to, float[] _ticks, int _tickCount, int _accent)
    {
        progressA.gameObject.SetActive(true);
        SetSpan(progressA, _from, Mathf.Min(_to, 1f));
        progressB.gameObject.SetActive(false);
        for (int i = 0; i < ticks.Length; i++)
        {
            bool used = i < _tickCount;
            if (ticks[i].gameObject.activeSelf != used)
            {
                ticks[i].gameObject.SetActive(used);
            }
            if (!used)
            {
                continue;
            }
            ticks[i].anchorMin = new Vector2(_ticks[i], ticks[i].anchorMin.y);
            ticks[i].anchorMax = new Vector2(_ticks[i], ticks[i].anchorMax.y);
            ticks[i].anchoredPosition = new Vector2(0f, ticks[i].anchoredPosition.y);
            if (ticks[i].TryGetComponent<Graphic>(out var graphic))
            {
                graphic.color = i == _accent ? Presentation.PuzzleTimeAccent : Presentation.GaugeTick;
            }
        }
    }

    internal void SetLabel(string _text)
    {
        label.text = _text;
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

    static void SetSpan(Image _segment, float _from, float _to)
    {
        var rect = _segment.rectTransform;
        rect.anchorMin = new Vector2(_from, rect.anchorMin.y);
        rect.anchorMax = new Vector2(_to, rect.anchorMax.y);
        rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
        rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
    }
}
