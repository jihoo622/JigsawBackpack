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
