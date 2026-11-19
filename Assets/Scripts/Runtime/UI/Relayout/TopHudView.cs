using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S2: 상단 고정 HUD 바(16, 12, 1888, 56). PuzzleCityView에서 분리된 HUD 전용 뷰.
[DisallowMultipleComponent]
public sealed class TopHudView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI cityName;
    [SerializeField] TextMeshProUGUI timeLabel;
    [SerializeField] Image dayNightBar;
    [SerializeField] Image nightSegmentA;
    [SerializeField] Image nightSegmentB;
    [SerializeField] RectTransform clockMarker;
    [SerializeField] TextMeshProUGUI deadlineTitle;
    [SerializeField] TextMeshProUGUI deadlineLabel;
    [SerializeField] TextMeshProUGUI funds;
    [SerializeField] Button clearButton;
    [SerializeField] TextMeshProUGUI clearLabel;
    [SerializeField] Button pauseButton;
    [SerializeField] TextMeshProUGUI pauseLabel;
    [SerializeField] PuzzleInventoryPresenter inventoryPresenter;
    [SerializeField] PauseMenu pauseMenu;

    long? displayedFunds;
    long targetFunds;
    Coroutine fundsAnimation;

    void OnEnable()
    {
        if (clearButton)
        {
            clearButton.onClick.AddListener(OnClearClicked);
        }
        if (pauseButton)
        {
            pauseButton.onClick.AddListener(OnPauseClicked);
        }
        if (pauseLabel)
        {
            pauseLabel.text = Presentation.TopHudPause;
        }
    }

    void OnDisable()
    {
        StopFundsAnimation();
        if (clearButton)
        {
            clearButton.onClick.RemoveListener(OnClearClicked);
        }
        if (pauseButton)
        {
            pauseButton.onClick.RemoveListener(OnPauseClicked);
        }
    }

    public void Initialize(long _clearCost)
    {
        if (clearLabel)
        {
            clearLabel.text = Presentation.PuzzleClearButton(_clearCost);
        }
        if (pauseLabel)
        {
            pauseLabel.text = Presentation.TopHudPause;
        }
    }

    public void ShowCity(string _name)
    {
        if (cityName)
        {
            cityName.text = _name;
        }
    }

    public void ShowTime(PuzzleTimeManager _time)
    {
        if (_time == null)
        {
            return;
        }
        if (timeLabel)
        {
            timeLabel.text = Presentation.PuzzleTime(_time);
        }
        ShowDayNight(_time);

        if (deadlineTitle)
        {
            deadlineTitle.text = Presentation.TimeDeadlineTitle;
            deadlineTitle.gameObject.SetActive(_time.HasDeadline);
            if (_time.HasDeadline)
            {
                deadlineTitle.color = Presentation.PuzzleHeading;
            }
        }
        if (deadlineLabel)
        {
            deadlineLabel.gameObject.SetActive(_time.HasDeadline);
            if (_time.HasDeadline)
            {
                deadlineLabel.text = Presentation.TimeDeadlineValue(_time.RemainingHours);
                deadlineLabel.color = Presentation.TimeDeadlineWarning(_time.RemainingHours)
                    ? Presentation.PuzzleTooltipRed
                    : Presentation.PuzzleHeading;
            }
        }
    }

    public void ShowDayNight(PuzzleTimeManager _time)
    {
        if (!dayNightBar || !nightSegmentA || !nightSegmentB || !clockMarker)
        {
            return;
        }
        dayNightBar.color = Presentation.TimeDayBar;
        nightSegmentA.color = Presentation.TimeNightBar;
        nightSegmentB.color = Presentation.TimeNightBar;
        float day = PuzzleTimeManager.HOURS_PER_DAY;
        float now = _time.HourOfDay + PuzzleTimeManager.HOURS_PER_TURN / 2f;
        float start = Mathf.Repeat(_time.NightStartHourOfDay - now + day / 2f, day) / day;
        float end = start + _time.NightLengthHours / day;
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

    public void ShowEndless()
    {
        if (clearLabel)
        {
            clearLabel.text = Presentation.PuzzleEndlessLabel;
        }
    }

    public void ShowFunds(long _funds, bool _canClear)
    {
        if (clearButton)
        {
            clearButton.interactable = _canClear;
        }
        if (!funds)
        {
            return;
        }
        funds.richText = true;
        if (!displayedFunds.HasValue)
        {
            displayedFunds = _funds;
            targetFunds = _funds;
            funds.text = Presentation.RunEndMoney(_funds);
            return;
        }
        if (fundsAnimation != null && _funds == targetFunds)
        {
            return;
        }
        if (fundsAnimation == null && _funds == displayedFunds.Value)
        {
            funds.text = Presentation.RunEndMoney(_funds);
            return;
        }
        StopFundsAnimation();

        long from = displayedFunds.Value;
        targetFunds = _funds;
        if (!isActiveAndEnabled)
        {
            displayedFunds = _funds;
            funds.text = Presentation.RunEndMoney(_funds);
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
            funds.text = $"<color=#{color}>{Presentation.RunEndMoney(value)}</color>";
            yield return null;
        }
        displayedFunds = _after;
        funds.text = Presentation.RunEndMoney(_after);
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
                funds.text = Presentation.RunEndMoney(targetFunds);
            }
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

    void OnClearClicked()
    {
        var presenter = inventoryPresenter ? inventoryPresenter : SceneServices.Find<PuzzleInventoryPresenter>(gameObject.scene);
        presenter?.ClearGame();
    }

    void OnPauseClicked()
    {
        var runEnd = SceneServices.Find<RunEndPresenter>(gameObject.scene);
        if (runEnd && runEnd.IsOpen)
        {
            return;
        }
        var pause = pauseMenu ? pauseMenu : SceneServices.Find<PauseMenu>(gameObject.scene);
        pause?.Open();
    }
}
