using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 하단 정보 바의 평판 칸. 누르면 Clicked를 올린다(Presenter가 수첩 숙련 탭을 연다).
public sealed class TrainventoryReputationView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI label;

    internal event Action Clicked;

    void OnEnable()
    {
        if (button)
        {
            button.onClick.AddListener(OnClicked);
        }
    }

    void OnDisable()
    {
        if (button)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }

    void OnClicked()
    {
        Clicked?.Invoke();
    }

    internal void Render(int _reputation)
    {
        if (!button || !label)
        {
            throw new InvalidOperationException("정보 바 평판 칸에 버튼·글자 참조를 연결하세요.");
        }
        label.text = Presentation.InfoBarReputation(_reputation);
        label.color = Presentation.PuzzleHeading;
    }
}
