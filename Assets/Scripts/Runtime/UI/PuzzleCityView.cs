using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleCityView : MonoBehaviour
{
    [SerializeField] Text cityName;
    [SerializeField] Button previousButton;
    [SerializeField] Text previousLabel;
    [SerializeField] Button nextButton;
    [SerializeField] Text nextLabel;
    [SerializeField] Text funds;
    [SerializeField] Text tradeStatus;

    internal void Initialize(bool _canTravel)
    {
        if (!cityName || !cityName.font || !previousButton || !previousButton.targetGraphic ||
            !nextButton || !nextButton.targetGraphic || previousButton == nextButton ||
            !previousLabel || !previousLabel.font || !nextLabel || !nextLabel.font ||
            !funds || !funds.font || !tradeStatus || !tradeStatus.font)
        {
            throw new InvalidOperationException("도시 View에 도시 이름, 이동 버튼, 자금과 거래 표시 참조가 필요합니다.");
        }

        cityName.color = previousLabel.color = nextLabel.color = Presentation.PuzzleHeading;
        funds.color = tradeStatus.color = Presentation.PuzzleHeading;
        previousLabel.text = Presentation.PUZZLE_PREVIOUS_CITY;
        nextLabel.text = Presentation.PUZZLE_NEXT_CITY;
        previousButton.colors = nextButton.colors = Presentation.PuzzleCityButtonColors;
        previousButton.interactable = nextButton.interactable = _canTravel;
    }

    internal void Subscribe(UnityAction _previous, UnityAction _next)
    {
        previousButton.onClick.AddListener(_previous);
        nextButton.onClick.AddListener(_next);
    }

    internal void Unsubscribe(UnityAction _previous, UnityAction _next)
    {
        if (previousButton)
        {
            previousButton.onClick.RemoveListener(_previous);
        }
        if (nextButton)
        {
            nextButton.onClick.RemoveListener(_next);
        }
    }

    internal void ShowCity(string _name)
    {
        cityName.text = _name;
    }

    internal void SetTravelEnabled(bool _enabled)
    {
        previousButton.interactable = nextButton.interactable = _enabled;
    }

    internal void ShowFunds(long _funds)
    {
        funds.text = Presentation.PuzzleFunds(_funds);
    }

    internal void ShowTradeStatus(string _status)
    {
        tradeStatus.text = _status;
    }
}
