using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 고정 UI 왼쪽의 상성표 보기 버튼. 상성표 패널(PuzzleSynergyChartPresenter)이 토글 응답한다.
public sealed class PuzzleSynergyChartToggleButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Image background;

    void OnEnable()
    {
        if (!button || !label)
        {
            Debug.LogError("상성표 보기 버튼에 버튼·라벨 참조가 필요합니다.", this);
            enabled = false;
            return;
        }
        label.text = Presentation.SynergyChartToggle;
        label.color = Presentation.PuzzleHeading;
        if (background)
        {
            background.color = Presentation.PuzzleBackground;
        }
        button.onClick.AddListener(OnClicked);
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
    }

    void OnClicked()
    {
        GameEvents.RaiseSynergyChartToggleRequested(gameObject.scene.handle);
    }
}
