using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleCityView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI cityName;
    [SerializeField] TextMeshProUGUI timeLabel;
    // 시계 아래 하루(0~24시) 막대: 바탕은 낮, 밤 구간 두 칸(자정을 넘는 밤을 위해 둘), 현재 시각 표시선.
    // 위치는 가로 앵커 비율로만 정하며 밤 구간은 시간 테이블이 원본이다.
    [SerializeField] Image dayNightBar;
    [SerializeField] Image nightSegmentA;
    [SerializeField] Image nightSegmentB;
    [SerializeField] RectTransform clockMarker;
    [SerializeField] TextMeshProUGUI deadlineTitle;
    [SerializeField] TextMeshProUGUI deadlineLabel;
    [SerializeField] TextMeshProUGUI funds;
    [SerializeField] TextMeshProUGUI tradeStatus;
    [SerializeField] Button clearButton;
    [SerializeField] TextMeshProUGUI clearLabel;
    [SerializeField] Button waitButton;
    [SerializeField] TextMeshProUGUI waitLabel;

    internal event Action WaitRequested;

    void OnEnable()
    {
        if (!waitButton || !talkButton)
        {
            Debug.LogError("도시 View에 대기·술집 대화 버튼 참조가 필요합니다.", this);
            enabled = false;
            return;
        }
        waitButton.onClick.AddListener(OnWait);
        talkButton.onClick.AddListener(HearRumor);
    }

    void OnDisable()
    {
        StopFundsAnimation();
        if (waitButton)
        {
            waitButton.onClick.RemoveListener(OnWait);
        }
        if (talkButton)
        {
            talkButton.onClick.RemoveListener(HearRumor);
        }
    }

    void OnWait()
    {
        WaitRequested?.Invoke();
    }

    internal void ShowWaitAction(bool _canWait)
    {
        waitButton.interactable = _canWait;
        waitLabel.text = Presentation.PuzzleWaitDaylight;
    }

    [SerializeField] Button talkButton;
    [SerializeField] TextMeshProUGUI tavernTitle;
    [SerializeField] TextMeshProUGUI rumorLabel;

    Func<string> getRumor;
    long? displayedFunds;
    long targetFunds;
    Coroutine fundsAnimation;

    internal void Initialize(long _clearCost)
    {
        if (!timeLabel || !timeLabel.font || !cityName || !cityName.font ||
            !funds || !funds.font || !tradeStatus || !tradeStatus.font ||
            !clearButton || !clearButton.targetGraphic || !clearLabel || !clearLabel.font)
        {
            throw new InvalidOperationException("도시 View에 도시 이름, 시계, 자금과 거래 표시 참조가 필요합니다.");
        }

        clearLabel.text = Presentation.PuzzleClearButton(_clearCost);
    }

    internal void InitializeTown(Func<string> _getRumor)
    {
        if (!talkButton || !tavernTitle || !rumorLabel)
        {
            throw new InvalidOperationException("도시 View에 술집 대화 버튼·제목·소식 표시 참조가 필요합니다.");
        }
        getRumor = _getRumor;
        talkButton.GetComponentInChildren<TextMeshProUGUI>(true).text = Presentation.TavernTalk;
    }

    internal void SetTown(string _name, bool _available)
    {
        tavernTitle.text = Presentation.TavernTitle(_name);
        // 새 마을에 도착하면 표시를 비우고 다시 소문을 들을 수 있게 한다.
        rumorLabel.text = Presentation.TavernUnreadRumor;
        talkButton.interactable = _available;
    }

    #region 술집 대화 표시

    // 버튼을 누를 때마다 1턴을 써서 소문을 듣는다. 같은 방문에서 반복해서 들을 수 있다.
    void HearRumor()
    {
        // 1. 소식을 받는다. 들을 소문이 없거나 턴을 낼 수 없으면 null이 돌아온다.
        string rumor = getRumor();

        // 2. 새 소식을 들었을 때만 대화 영역을 바꾼다. 듣지 못하면 지금까지 표시를 그대로 둔다.
        if (!string.IsNullOrEmpty(rumor))
        {
            rumorLabel.text = rumor;
        }
    }

    #endregion

    internal void ShowCity(string _name)
    {
        cityName.text = _name;
    }

    void ShowDayNight(PuzzleTimeManager _time)
    {
        if (!dayNightBar || !nightSegmentA || !nightSegmentB || !clockMarker)
        {
            throw new InvalidOperationException("도시 View에 낮·밤 막대·밤 구간 2개·현재 시각 표시선 참조가 필요합니다.");
        }
        dayNightBar.color = Presentation.TimeDayBar;
        nightSegmentA.color = Presentation.TimeNightBar;
        nightSegmentB.color = Presentation.TimeNightBar;
        float day = PuzzleTimeManager.TURNS_PER_DAY;
        float start = _time.NightStartTurnOfDay / day;
        float end = start + _time.NightLengthTurns / day;
        // 자정을 넘는 밤은 [시작, 1)과 [0, 끝-1) 두 칸으로 나눈다.
        SetSpan(nightSegmentA, start, Mathf.Min(end, 1f));
        nightSegmentB.gameObject.SetActive(end > 1f);
        if (end > 1f)
        {
            SetSpan(nightSegmentB, 0f, end - 1f);
        }
        float now = _time.TurnOfDay / day;
        clockMarker.anchorMin = new Vector2(now, clockMarker.anchorMin.y);
        clockMarker.anchorMax = new Vector2(now, clockMarker.anchorMax.y);
        clockMarker.anchoredPosition = new Vector2(0f, clockMarker.anchoredPosition.y);
        if (clockMarker.TryGetComponent<Graphic>(out var markerGraphic))
        {
            markerGraphic.color = Presentation.PuzzleTimeAccent;
        }
    }

    static void SetSpan(Image _segment, float _from, float _to)
    {
        var rect = _segment.rectTransform;
        rect.anchorMin = new Vector2(_from, 0f);
        rect.anchorMax = new Vector2(_to, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    internal void ShowTime(PuzzleTimeManager _time)
    {
        timeLabel.text = Presentation.PuzzleTime(_time);
        ShowDayNight(_time);
        if (deadlineTitle)
        {
            deadlineTitle.text = Presentation.TimeDeadlineTitle;
        }
        if (!deadlineLabel)
        {
            return;
        }
        if (!_time.HasDeadline)
        {
            deadlineLabel.gameObject.SetActive(false);
            if (deadlineTitle)
            {
                deadlineTitle.gameObject.SetActive(false);
            }
            return;
        }
        deadlineLabel.gameObject.SetActive(true);
        if (deadlineTitle)
        {
            deadlineTitle.gameObject.SetActive(true);
            deadlineTitle.color = Presentation.PuzzleHeading;
        }
        deadlineLabel.text = Presentation.TimeDeadlineValue(_time.RemainingTurns);
        deadlineLabel.color = Presentation.TimeDeadlineWarning(_time.RemainingTurns)
            ? Presentation.PuzzleTooltipRed : Presentation.PuzzleHeading;
    }

    internal void ShowFunds(long _funds, bool _canClear)
    {
        clearButton.interactable = _canClear;
        funds.richText = true;
        if (!displayedFunds.HasValue)
        {
            displayedFunds = _funds;
            targetFunds = _funds;
            funds.text = Presentation.PuzzleFunds(_funds);
            return;
        }

        if (fundsAnimation != null && _funds == targetFunds)
        {
            return;
        }

        if (fundsAnimation == null && _funds == displayedFunds.Value)
        {
            funds.text = Presentation.PuzzleFunds(_funds);
            return;
        }

        if (fundsAnimation != null)
        {
            StopCoroutine(fundsAnimation);
            fundsAnimation = null;
        }

        long from = displayedFunds.Value;
        targetFunds = _funds;
        if (!isActiveAndEnabled)
        {
            displayedFunds = _funds;
            funds.text = Presentation.PuzzleFunds(_funds);
            return;
        }

        fundsAnimation = StartCoroutine(AnimateFunds(from, _funds));
    }

    IEnumerator AnimateFunds(long _before, long _after)
    {
        const float countSeconds = 0.55f;
        const float holdSeconds = 0.85f;
        Color highlight = _after < _before ? Presentation.PuzzleTooltipRed : Presentation.PuzzleTooltipGreen;
        string color = ColorUtility.ToHtmlStringRGB(highlight);
        for (float elapsed = 0f; elapsed < holdSeconds; elapsed += Time.unscaledDeltaTime)
        {
            decimal progress = (decimal)Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / countSeconds));
            long value = (long)Math.Round(_before * (1m - progress) + _after * progress, MidpointRounding.AwayFromZero);
            displayedFunds = value;
            funds.text = $"<color=#{color}>{Presentation.PuzzleFunds(value)}</color>";
            yield return null;
        }

        displayedFunds = _after;
        funds.text = Presentation.PuzzleFunds(_after);
        fundsAnimation = null;
    }

    void StopFundsAnimation()
    {
        if (fundsAnimation != null)
        {
            StopCoroutine(fundsAnimation);
            fundsAnimation = null;
            displayedFunds = targetFunds;
            if (funds)
            {
                funds.text = Presentation.PuzzleFunds(targetFunds);
            }
        }
    }

    internal void ShowTradeStatus(string _status)
    {
        tradeStatus.richText = true;
        tradeStatus.text = _status;
    }
}
