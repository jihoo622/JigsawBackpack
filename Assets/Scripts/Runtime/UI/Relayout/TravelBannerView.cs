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

    // 확인 카드 배치. 질문·이동 정보는 왼쪽 위에서 아래로 쌓고, 확인·취소는 오른쪽 아래에 둔다.
    // 이동 정보는 여러 줄(경로·빈 줄·이동·도착·요금·자금)이라 줄 수만큼 카드 높이를 늘린다.
    const float PADDING = 16f;
    const float QUESTION_TOP = 12f;
    const float QUESTION_HEIGHT = 26f;
    const float QUOTE_TOP = 42f;
    const float BUTTON_GAP = 12f;
    const float MIN_HEIGHT = 148f;
    // 지도 위에 둘 때 지도 화면 가장자리와 띄우는 거리, 그리고 지도가 아주 좁을 때의 최소 폭.
    const float MAP_MARGIN = 8f;
    const float MIN_WIDTH = 360f;

    RectTransform followTarget;
    bool hasDefaultPlacement;
    Vector2 defaultAnchorMin;
    Vector2 defaultAnchorMax;
    Vector2 defaultPivot;
    Vector2 defaultPosition;
    Vector2 defaultSize;
    readonly Vector3[] targetCorners = new Vector3[4];

    // 팝업을 놓을 지도 화면(노선도 영역). 지정하면 표시 중 그 영역 위쪽 가운데에 붙어 따라다니고(수첩 이동·크게 보기 전환 포함),
    // 폭을 줄여 지도 영역을 벗어나지 않는다. 없거나 보이지 않으면 원래 자리(화면 위 가운데)에 둔다.
    internal void SetFollowTarget(RectTransform _target)
    {
        followTarget = _target;
        UpdatePlacement();
    }

    void LateUpdate()
    {
        UpdatePlacement();
    }

    void UpdatePlacement()
    {
        var rect = transform as RectTransform;
        var parent = rect ? rect.parent as RectTransform : null;
        if (!rect || !parent)
        {
            return;
        }
        if (!hasDefaultPlacement)
        {
            hasDefaultPlacement = true;
            defaultAnchorMin = rect.anchorMin;
            defaultAnchorMax = rect.anchorMax;
            defaultPivot = rect.pivot;
            defaultPosition = rect.anchoredPosition;
            defaultSize = rect.sizeDelta;
            CaptureJourneyLayout();
        }

        bool following = followTarget && followTarget.gameObject.activeInHierarchy;
        Vector2 mapMin = default;
        Vector2 mapMax = default;
        float width = defaultSize.x;
        if (following)
        {
            // 지도 화면 영역(부동 창 캔버스 좌표). 팝업 폭은 이 영역 안에 들어가도록 줄인다.
            followTarget.GetWorldCorners(targetCorners);
            mapMin = parent.InverseTransformPoint(targetCorners[0]);
            mapMax = parent.InverseTransformPoint(targetCorners[2]);
            width = Mathf.Min(defaultSize.x, Mathf.Max(MIN_WIDTH, mapMax.x - mapMin.x - MAP_MARGIN * 2f));
        }

        float height = confirming ? ConfirmationHeight(width) : defaultSize.y;
        rect.sizeDelta = new Vector2(width, height);
        if (confirming)
        {
            LayoutConfirmation(width);
        }
        else
        {
            LayoutJourney(width);
        }

        if (!following)
        {
            rect.anchorMin = defaultAnchorMin;
            rect.anchorMax = defaultAnchorMax;
            rect.pivot = defaultPivot;
            rect.anchoredPosition = defaultPosition;
            return;
        }

        // 지도 화면 위쪽 가운데에 붙인다. 폭은 위에서 지도 안으로 줄였고, 높이(최대 6줄 정보)는 지도 높이보다 작다.
        float x = (mapMin.x + mapMax.x) * 0.5f;
        float y = mapMax.y - MAP_MARGIN - height * 0.5f;
        // 앵커를 부모의 피벗에 두면 anchoredPosition이 부모 로컬 좌표와 같다.
        rect.anchorMin = rect.anchorMax = parent.pivot;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    // 이동 진행 카드의 요소들은 폭 640 기준으로 놓여 있다. 지도에 맞춰 폭이 줄면 가로 위치·폭만 같은 비율로 줄인다.
    // 기차 아이콘의 가로 위치는 진행도(ShowProgress)가 정하므로 건드리지 않는다.
    readonly System.Collections.Generic.List<(RectTransform rect, Vector2 position, Vector2 size)> journeyLayout = new();

    void CaptureJourneyLayout()
    {
        journeyLayout.Clear();
        if (!journey)
        {
            return;
        }
        foreach (Transform child in journey.transform)
        {
            if (child is RectTransform childRect && childRect != trainIcon)
            {
                journeyLayout.Add((childRect, childRect.anchoredPosition, childRect.sizeDelta));
            }
        }
    }

    void LayoutJourney(float _width)
    {
        float ratio = defaultSize.x > 0f ? _width / defaultSize.x : 1f;
        foreach (var (childRect, position, size) in journeyLayout)
        {
            if (!childRect)
            {
                continue;
            }
            childRect.anchoredPosition = new Vector2(position.x * ratio, position.y);
            // 점(출발·도착 표식)은 크기를 유지하고, 글자·선로처럼 넓은 요소만 폭을 줄인다.
            childRect.sizeDelta = size.x > 40f ? new Vector2(size.x * ratio, size.y) : size;
        }
    }

    float TextWidth(float _width)
    {
        float buttons = ButtonWidth(confirmButton) + ButtonWidth(cancelButton) + BUTTON_GAP;
        return Mathf.Max(80f, _width - PADDING * 2f - buttons - BUTTON_GAP);
    }

    static float ButtonWidth(Button _button) =>
        _button && _button.transform is RectTransform rect ? rect.sizeDelta.x : 0f;

    float ConfirmationHeight(float _width)
    {
        float quoteHeight = quoteText ? quoteText.GetPreferredValues(quoteText.text, TextWidth(_width), 0f).y : 0f;
        return Mathf.Max(MIN_HEIGHT, QUOTE_TOP + quoteHeight + PADDING);
    }

    void LayoutConfirmation(float _width)
    {
        float textWidth = TextWidth(_width);
        PlaceTopLeft(questionText, QUESTION_TOP, textWidth, QUESTION_HEIGHT);
        if (quoteText)
        {
            float quoteHeight = quoteText.GetPreferredValues(quoteText.text, textWidth, 0f).y;
            PlaceTopLeft(quoteText, QUOTE_TOP, textWidth, quoteHeight);
        }
        // 오른쪽 아래: 취소가 가장 오른쪽, 그 왼쪽에 확인.
        PlaceBottomRight(cancelButton, PADDING);
        PlaceBottomRight(confirmButton, PADDING + ButtonWidth(cancelButton) + BUTTON_GAP);
    }

    static void PlaceTopLeft(TextMeshProUGUI _text, float _top, float _width, float _height)
    {
        if (!_text)
        {
            return;
        }
        var rect = _text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(PADDING, -_top);
        rect.sizeDelta = new Vector2(_width, _height);
        // 가운데 세로 정렬이면 상자보다 줄이 많을 때 위로 넘쳐 질문 글자와 겹친다.
        _text.alignment = TextAlignmentOptions.TopLeft;
    }

    static void PlaceBottomRight(Button _button, float _right)
    {
        if (!_button || !(_button.transform is RectTransform rect))
        {
            return;
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-_right, PADDING);
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
        UpdatePlacement();
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
        UpdatePlacement();
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
