using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PuzzleMapTravelView : PanelBase
{
    [SerializeField] InputActionAsset inputActions;
    [SerializeField] GameObject confirmation;
    [SerializeField] GameObject journey;
    [SerializeField] TMPro.TextMeshProUGUI question;
    [SerializeField] TMPro.TextMeshProUGUI quote;
    [SerializeField] TMPro.TextMeshProUGUI confirmLabel;
    [SerializeField] TMPro.TextMeshProUGUI cancelLabel;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] TMPro.TextMeshProUGUI journeyTitle;
    [SerializeField] TMPro.TextMeshProUGUI journeyRoute;
    [SerializeField] TMPro.TextMeshProUGUI progressLabel;
    [SerializeField] RectTransform train;
    [SerializeField] RectTransform departure;
    [SerializeField] RectTransform destination;
    // 선로 양 끝 아래의 출발역·도착역 이름.
    [SerializeField] TMPro.TextMeshProUGUI departureName;
    [SerializeField] TMPro.TextMeshProUGUI destinationName;
    // 달리는 동안 기차가 위아래로 흔들리는 폭과 횟수(연출 한 번 동안).
    [SerializeField] float trainBobHeight = 3f;
    [SerializeField] int trainBobCount = 6;

    InputActionAsset actions;
    InputAction cancel;
    GameObject previousSelection;
    bool confirming;
    bool selectionCaptured;
    // 이번 이동의 출발·도착 경과 시간. 진행 막대 아래 시계를 그 사이로 흘린다.
    long departureHours;
    long arrivalHours;

    internal event Action Confirmed;
    internal event Action Canceled;

    protected override void Awake()
    {
        actions = Instantiate(inputActions);
        cancel = actions.FindAction("UI/Cancel", true);
        question.text = Presentation.PuzzleTravelQuestion;
        confirmLabel.text = Presentation.PuzzleTravelConfirm;
        cancelLabel.text = Presentation.PuzzleTravelCancel;
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
    }

    protected override void OnDisable()
    {
        confirmButton.onClick.RemoveListener(OnConfirm);
        cancelButton.onClick.RemoveListener(OnCancel);
        base.OnDisable();
    }

    void OnDestroy()
    {
        if (actions)
        {
            Destroy(actions);
        }
    }

    public void ShowConfirmation(string _quote)
    {
        quote.text = _quote;
        confirming = true;
        confirmation.SetActive(true);
        journey.SetActive(false);
        Open();
    }

    // 이동 연출을 시작한다. 경로 줄에 출발→도착·운임·소요 턴을 보이고, 시계는 출발 시각에서 도착 시각까지 흐른다.
    internal void BeginJourney(string _fromName, string _toName, long _cost, long _departureHours, long _arrivalHours)
    {
        confirming = false;
        confirmation.SetActive(false);
        journey.SetActive(true);
        departureHours = _departureHours;
        arrivalHours = Math.Max(_departureHours, _arrivalHours);
        journeyTitle.text = Presentation.PuzzleTravelInProgress;
        journeyRoute.text = Presentation.PuzzleTravelJourney(_fromName, _toName, _cost, arrivalHours - departureHours);
        if (departureName)
        {
            departureName.text = _fromName;
        }
        if (destinationName)
        {
            destinationName.text = _toName;
        }
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        ShowProgress(0f);
    }

    // 기차가 출발 지점에서 도착 지점까지 달리고, 아래 시계가 출발 시각에서 도착 시각까지 흐른다.
    // 진행 막대는 두지 않는다. 카드(Journey)의 RectMask2D가 카드 밖으로 나가는 기차 그림을 자른다.
    internal void ShowProgress(float _progress)
    {
        float value = Mathf.Clamp01(_progress);
        // 달리는 동안만 살짝 흔들고 출발·도착 지점에서는 선로 위에 바로 선다.
        float bob = Mathf.Abs(Mathf.Sin(value * Mathf.PI * Mathf.Max(0, trainBobCount))) * trainBobHeight;
        train.anchoredPosition = Vector2.Lerp(departure.anchoredPosition, destination.anchoredPosition, value) + Vector2.up * bob;
        progressLabel.text = Presentation.PuzzleTravelClock(departureHours + (arrivalHours - departureHours) * (double)value);
    }

    internal void ShowArrival()
    {
        ShowProgress(1f);
        journeyTitle.text = Presentation.PuzzleTravelArrived;
    }

    protected override void OnShow()
    {
        if (EventSystem.current)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            selectionCaptured = true;
            EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        }
    }

    protected override void OnHide()
    {
        confirming = false;
        confirmation.SetActive(false);
        journey.SetActive(false);
        if (selectionCaptured && EventSystem.current)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (!selected || selected.transform.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
            }
        }
        selectionCaptured = false;
        previousSelection = null;
    }

    void OnConfirm()
    {
        if (IsOpen && confirming)
        {
            Confirmed?.Invoke();
        }
    }

    public bool TryConsumeCancel()
    {
        if (IsOpen && confirming)
        {
            OnCancel();
            return true;
        }
        return false;
    }

    void OnCancel()
    {
        if (IsOpen && confirming)
        {
            Canceled?.Invoke();
        }
    }
}
