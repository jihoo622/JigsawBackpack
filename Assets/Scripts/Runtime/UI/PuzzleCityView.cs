using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleCityView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI cityName;
    [SerializeField] TextMeshProUGUI timeLabel;
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

    internal void Initialize(long _clearCost)
    {
        if (!timeLabel || !timeLabel.font || !cityName || !cityName.font ||
            !funds || !funds.font || !tradeStatus || !tradeStatus.font ||
            !clearButton || !clearButton.targetGraphic || !clearLabel || !clearLabel.font)
        {
            throw new InvalidOperationException("도시 View에 도시 이름, 시간, 자금과 거래 표시 참조가 필요합니다.");
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
    }

    internal void ShowTime(PuzzleTimeManager _time)
    {
        timeLabel.text = Presentation.PuzzleTime(_time);
    }

    internal void ShowFunds(long _funds, bool _canClear)
    {
        funds.text = Presentation.PuzzleFunds(_funds);
        clearButton.interactable = _canClear;
    }

    internal void ShowTradeStatus(string _status)
    {
        tradeStatus.text = _status;
    }
}
