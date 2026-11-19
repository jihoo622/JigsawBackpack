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
    // 시계 아래 24시간 막대: 지금을 가운데 고정하고 앞뒤 12시간을 보인다. 바탕은 낮, 밤 구간 두 칸(창 끝을 넘는 밤을 위해 둘),
    // 가운데 고정 현재 시각 표시선. 위치는 가로 앵커 비율로만 정하며 밤 구간은 시간 테이블이 원본이다.
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
    [SerializeField] TopHudView topHudView;
    [SerializeField] StatusBannerView statusBannerView;

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
    string heardRumor;
    long? displayedFunds;
    long targetFunds;
    Coroutine fundsAnimation;

    internal void Initialize(long _clearCost)
    {
        if (!timeLabel || !timeLabel.font || !cityName || !cityName.font ||
            !funds || !funds.font || !tradeStatus || !tradeStatus.font ||
            !clearButton || !clearButton.targetGraphic || !clearLabel || !clearLabel.font)
        {
            throw new InvalidOperationException("도시 View에 도시 이름, 시간, 자금과 거래 표시 참조가 필요합니다.");
        }

        clearLabel.text = Presentation.PuzzleClearButton(_clearCost);
        if (topHudView)
        {
            topHudView.Initialize(_clearCost);
        }
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
        heardRumor = null;
        rumorLabel.text = Presentation.TavernUnreadRumor;
        talkButton.interactable = _available;
    }

    #region 술집 대화 표시

    // 이번 방문의 소식을 받아 표시한 뒤, 다음 마을에 도착할 때까지 대화 버튼을 비활성화한다.
    void HearRumor()
    {
        // 1. 이번 방문에서 아직 소식을 듣지 않았다면 새 소식을 받아 저장한다.
        heardRumor ??= getRumor();

        // 2. 저장된 소식을 대화 영역에 표시한다.
        rumorLabel.text = heardRumor;

        // 3. 같은 방문에서 대화를 반복할 수 없도록 버튼을 비활성화한다.
        talkButton.interactable = false;
    }

    #endregion

    internal void ShowCity(string _name)
    {
        cityName.text = _name;
        if (topHudView)
        {
            topHudView.ShowCity(_name);
        }
    }

    // 지금 턴의 한가운데를 가운데 고정한 24시간 창(앞뒤 12시간)이다. 표시선은 움직이지 않고 턴이 지나면 낮·밤 구간이 한 칸 밀린다.
    internal void ShowDayNight(PuzzleTimeManager _time)
    {
        if (!dayNightBar || !nightSegmentA || !nightSegmentB || !clockMarker)
        {
            throw new InvalidOperationException("도시 View에 낮·밤 막대·밤 구간 2개·현재 시각 표시선 참조가 필요합니다.");
        }
        dayNightBar.color = Presentation.TimeDayBar;
        nightSegmentA.color = Presentation.TimeNightBar;
        nightSegmentB.color = Presentation.TimeNightBar;
        float day = PuzzleTimeManager.HOURS_PER_DAY;
        float now = _time.HourOfDay + PuzzleTimeManager.HOURS_PER_TURN / 2f;
        float start = Mathf.Repeat(_time.NightStartHourOfDay - now + day / 2f, day) / day;
        float end = start + _time.NightLengthHours / day;
        // 창 끝을 넘는 밤은 [시작, 1)과 [0, 끝-1) 두 칸으로 나눈다.
        SetSpan(nightSegmentA, start, Mathf.Min(end, 1f));
        nightSegmentB.gameObject.SetActive(end > 1f);
        if (end > 1f)
        {
            SetSpan(nightSegmentB, 0f, end - 1f);
        }
        clockMarker.anchorMin = new Vector2(0.5f, clockMarker.anchorMin.y);
        clockMarker.anchorMax = new Vector2(0.5f, clockMarker.anchorMax.y);
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
        if (topHudView)
        {
            topHudView.ShowTime(_time);
        }
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
        deadlineLabel.text = Presentation.TimeDeadlineValue(_time.RemainingHours);
        deadlineLabel.color = Presentation.TimeDeadlineWarning(_time.RemainingHours)
            ? Presentation.PuzzleTooltipRed : Presentation.PuzzleHeading;
    }

    // 무한 모드에는 결산이 없으므로 결산 버튼 자리에 모드 이름만 남긴다.
    internal void ShowEndless()
    {
        clearLabel.text = Presentation.PuzzleEndlessLabel;
        if (topHudView)
        {
            topHudView.ShowEndless();
        }
    }

    internal void ShowFunds(long _funds, bool _canClear)
    {
        if (topHudView)
        {
            topHudView.ShowFunds(_funds, _canClear);
        }
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
        if (statusBannerView)
        {
            statusBannerView.ShowStatus(_status);
        }
    }
}
