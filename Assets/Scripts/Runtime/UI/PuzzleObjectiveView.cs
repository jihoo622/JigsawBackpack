using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleObjectiveView : PanelBase
{
    [SerializeField] Image background;
    [SerializeField] Image backdrop;
    [SerializeField] Image costPlate;
    [SerializeField] Button confirmButton;
    [SerializeField] TextMeshProUGUI costLabel;
    [SerializeField] TextMeshProUGUI objectiveLabel;
    [SerializeField] TextMeshProUGUI confirmLabel;

    internal void ShowObjective(long _targetFunds, long _deadlineHours)
    {
        background.color = Presentation.PuzzleObjectiveBackground;
        backdrop.color = Presentation.PuzzleObjectiveBackdrop;
        costPlate.color = Presentation.PuzzleObjectiveSurface;
        confirmButton.image.color = Presentation.PuzzleObjectiveSurface;
        costLabel.text = Presentation.PuzzleClearButton(_targetFunds);
        if (objectiveLabel)
        {
            objectiveLabel.textWrappingMode = TextWrappingModes.Normal;
            objectiveLabel.overflowMode = TextOverflowModes.Overflow;
            objectiveLabel.text = Presentation.PuzzleObjectiveHint(_deadlineHours);
            // 두 줄 안내문이 잘리지 않도록 라벨 높이를 글자 높이에 맞춘다(최소 72).
            var rt = objectiveLabel.rectTransform;
            float height = Mathf.Max(72f, objectiveLabel.preferredHeight + 12f);
            if (!Mathf.Approximately(rt.sizeDelta.y, height))
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            }
        }
        // 구형 '상환 목표' 지도 미리보기 이미지를 숨긴다. 지도는 RawImage라 Image 검사에 걸리지 않으므로 따로 숨긴다.
        var imgs = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var img = imgs[i];
            if (img && img != background && img != backdrop && img != costPlate &&
                (!confirmButton || img != confirmButton.image) && img.sprite != null)
            {
                img.gameObject.SetActive(false);
            }
        }
        var rawImgs = GetComponentsInChildren<RawImage>(true);
        for (int i = 0; i < rawImgs.Length; i++)
        {
            if (rawImgs[i])
            {
                rawImgs[i].gameObject.SetActive(false);
            }
        }
        confirmLabel.text = Presentation.PuzzleObjectiveConfirm;
        Open();
    }

    protected override void OnShow()
    {
        confirmButton.Select();
    }

    protected override void OnHide()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem && eventSystem.currentSelectedGameObject == confirmButton.gameObject)
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }
}
