using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 고정 UI 오른쪽의 지도 토글 버튼. 지도가 열려 있으면 라벨이 "지도 닫기"로 바뀌고 테두리가 강조된다.
public sealed class PuzzleMapToggleButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI hintLabel;
    [SerializeField] Image highlightBorder;
    [SerializeField] Image background;
    [SerializeField] string mapPanelName = "PuzzleWorldMap";

    void OnEnable()
    {
        if (!button || !titleLabel)
        {
            Debug.LogError("지도 토글 버튼에 버튼·제목 라벨 참조가 필요합니다.", this);
            enabled = false;
            return;
        }
        button.onClick.AddListener(OnClicked);
        GameEvents.PanelVisibility += OnPanelVisibility;
        // 지도 패널은 startOpen=false로 시작한다. 열림 여부 이벤트 전까지 닫힌 상태로 표시한다.
        UpdateVisual(false);
    }

    void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        GameEvents.PanelVisibility -= OnPanelVisibility;
    }

    void OnClicked()
    {
        GameEvents.RaiseMapToggleRequested(gameObject.scene.handle);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.panel == mapPanelName)
        {
            UpdateVisual(_info.open);
        }
    }

    void UpdateVisual(bool _open)
    {
        titleLabel.text = _open ? Presentation.MapToggleClose : Presentation.PuzzleMapOpen;
        if (background)
        {
            background.color = Presentation.PuzzleBackground;
        }
        if (hintLabel)
        {
            hintLabel.text = Presentation.MapToggleHint;
            hintLabel.color = Presentation.PuzzleHeading;
        }
        if (highlightBorder)
        {
            highlightBorder.enabled = _open;
            highlightBorder.color = Presentation.PuzzleMapCurrent;
        }
    }
}
