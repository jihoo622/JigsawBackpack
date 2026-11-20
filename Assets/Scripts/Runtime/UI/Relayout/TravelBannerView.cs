using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S6: 이동 배너. Relayout ON에서 기존 지도 이동 패널 대신 출발 확인(운임/소요 턴)과 이동 진행 막대를 표시한다.
[DisallowMultipleComponent]
public sealed class TravelBannerView : MonoBehaviour
{
    [SerializeField] GameObject confirmation;
    [SerializeField] GameObject journey;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] TextMeshProUGUI quoteText;
    [SerializeField] Button confirmButton;
    [SerializeField] TextMeshProUGUI confirmLabel;
    [SerializeField] Button cancelButton;
    [SerializeField] TextMeshProUGUI cancelLabel;
    [SerializeField] TextMeshProUGUI journeyTitleText;
    [SerializeField] TextMeshProUGUI journeyRouteText;
    [SerializeField] TextMeshProUGUI progressLabel;
    [SerializeField] RectTransform trainIcon;
    [SerializeField] RectTransform departureAnchor;
    [SerializeField] RectTransform destinationAnchor;
    [SerializeField] TextMeshProUGUI departureNameText;
    [SerializeField] TextMeshProUGUI destinationNameText;

    bool confirming;
    long departureHours;
    long arrivalHours;

    internal event Action Confirmed;
    internal event Action Canceled;

    // 역 이동 확인·진행 팝업은 게임 화면의 모든 UI(부동 창 50, 지도 크게 보기, 결산 200, 툴팁 270~272, 우클릭 메뉴 280,
    // 목표 안내·작업 대기 290) 위에 그린다. 일시정지 메뉴(PauseCanvas 300)만 이보다 위다.
    const int SORTING_ORDER = 295;

    void Awake()
    {
        // 자기 Canvas로 정렬 순서를 덮어쓴다. 중첩 Canvas는 입력을 받으려면 GraphicRaycaster가 따로 있어야 한다.
        var ownCanvas = GetComponent<Canvas>();
        if (!ownCanvas)
        {
            ownCanvas = gameObject.AddComponent<Canvas>();
        }
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder = SORTING_ORDER;
        if (!GetComponent<GraphicRaycaster>())
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
        if (questionText) questionText.text = Presentation.PuzzleTravelQuestion;
        if (confirmLabel) confirmLabel.text = Presentation.PuzzleTravelConfirm;
        if (cancelLabel) cancelLabel.text = Presentation.PuzzleTravelCancel;
    }

    void OnEnable()
    {
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton) cancelButton.onClick.AddListener(OnCancel);
    }

    void OnDisable()
    {
        if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirm);
        if (cancelButton) cancelButton.onClick.RemoveListener(OnCancel);
    }

    public bool TryConsumeCancel()
    {
        if (gameObject.activeSelf && confirming)
        {
            OnCancel();
            return true;
        }
        return false;
    }

    public void ShowConfirmation(string _quote)
    {
        confirming = true;
        if (quoteText) quoteText.text = _quote;
        if (confirmation) confirmation.SetActive(true);
        if (journey) journey.SetActive(false);
        gameObject.SetActive(true);
    }

    internal void BeginJourney(string _fromName, string _toName, long _cost, long _departureHours, long _arrivalHours)
    {
        confirming = false;
        if (confirmation) confirmation.SetActive(false);
        if (journey) journey.SetActive(true);
        departureHours = _departureHours;
        arrivalHours = Math.Max(_departureHours, _arrivalHours);
        if (journeyTitleText) journeyTitleText.text = Presentation.PuzzleTravelInProgress;
        if (journeyRouteText) journeyRouteText.text = Presentation.PuzzleTravelJourney(_fromName, _toName, _cost, arrivalHours - departureHours);
        if (departureNameText) departureNameText.text = _fromName;
        if (destinationNameText) destinationNameText.text = _toName;
        gameObject.SetActive(true);
        ShowProgress(0f);
    }

    internal void ShowProgress(float _progress)
    {
        float clamped = Mathf.Clamp01(_progress);
        if (trainIcon && departureAnchor && destinationAnchor)
        {
            Vector3 pos = Vector3.Lerp(departureAnchor.position, destinationAnchor.position, clamped);
            pos.y += Mathf.Sin(clamped * Mathf.PI * 6f) * 3f;
            trainIcon.position = pos;
        }
        if (progressLabel)
        {
            progressLabel.text = Presentation.PuzzleTravelClock(departureHours + (arrivalHours - departureHours) * (double)clamped);
        }
    }

    internal void ShowArrival()
    {
        if (journeyTitleText) journeyTitleText.text = Presentation.PuzzleTravelArrived;
    }

    public void Close()
    {
        confirming = false;
        gameObject.SetActive(false);
    }

    void OnConfirm() => Confirmed?.Invoke();
    void OnCancel()
    {
        Close();
        Canceled?.Invoke();
    }
}
