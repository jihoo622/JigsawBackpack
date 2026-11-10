using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleCityView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI cityName;
    [SerializeField] TextMeshProUGUI timeLabel;
    [SerializeField] Button previousButton;
    [SerializeField] TextMeshProUGUI previousLabel;
    [SerializeField] Button nextButton;
    [SerializeField] TextMeshProUGUI nextLabel;
    [SerializeField] TextMeshProUGUI funds;
    [SerializeField] TextMeshProUGUI tradeStatus;
    [SerializeField] Button clearButton;
    [SerializeField] TextMeshProUGUI clearLabel;

    internal void Initialize(bool _canTravel, long _clearCost)
    {
        if (!timeLabel || !timeLabel.font || !cityName || !cityName.font || !previousButton || !previousButton.targetGraphic ||
            !nextButton || !nextButton.targetGraphic || previousButton == nextButton ||
            !previousLabel || !previousLabel.font || !nextLabel || !nextLabel.font ||
            !funds || !funds.font || !tradeStatus || !tradeStatus.font ||
            !clearButton || !clearButton.targetGraphic || !clearLabel || !clearLabel.font)
        {
            throw new InvalidOperationException("도시 View에 도시 이름, 이동 버튼, 자금과 거래 표시 참조가 필요합니다.");
        }

        cityName.color = previousLabel.color = nextLabel.color = Presentation.PuzzleHeading;
        funds.color = tradeStatus.color = Presentation.PuzzleHeading;
        timeLabel.color = Presentation.PuzzleTimeAccent;
        clearLabel.color = Presentation.PuzzleHeading;
        clearLabel.text = Presentation.PuzzleClearButton(_clearCost);
        previousButton.colors = nextButton.colors = Presentation.PuzzleCityButtonColors;
        previousButton.interactable = nextButton.interactable = _canTravel;

        // 지도를 통한 그래프 이동으로 통합하여 구 링 이동 버튼은 비활성화/숨김 처리한다.
        previousButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
    }

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
