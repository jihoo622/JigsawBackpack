using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 타이틀 화면 표시(View). 배치는 프리팹(Assets/Art/UI/Title/TitleScreen.prefab)이 원본이고 문구는 TitlePresenter가 채운다.
public sealed class TitleView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button startButton;
    [SerializeField] TextMeshProUGUI startLabel;
    [SerializeField] Button quitButton;
    [SerializeField] TextMeshProUGUI quitLabel;

    public event Action StartRequested;
    public event Action QuitRequested;

    void OnEnable()
    {
        startButton.onClick.AddListener(OnStart);
        quitButton.onClick.AddListener(OnQuit);
    }

    void OnDisable()
    {
        startButton.onClick.RemoveListener(OnStart);
        quitButton.onClick.RemoveListener(OnQuit);
    }

    public void SetTexts(string _title, string _start, string _quit)
    {
        titleText.text = _title;
        startLabel.text = _start;
        quitLabel.text = _quit;
    }

    // 키보드·패드 Submit이 바로 시작 버튼에 닿게 선택해 둔다.
    public void FocusStart()
    {
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
    }

    // 씬 전환 중 중복 입력을 막는다.
    public void SetInteractable(bool _interactable)
    {
        startButton.interactable = _interactable;
        quitButton.interactable = _interactable;
    }

    void OnStart()
    {
        StartRequested?.Invoke();
    }

    void OnQuit()
    {
        QuitRequested?.Invoke();
    }
}
