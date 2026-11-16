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

    internal void ShowObjective(long _cost, string _cityName)
    {
        background.color = Presentation.PuzzleObjectiveBackground;
        backdrop.color = Presentation.PuzzleObjectiveBackdrop;
        costPlate.color = Presentation.PuzzleObjectiveSurface;
        confirmButton.image.color = Presentation.PuzzleObjectiveSurface;
        costLabel.text = Presentation.PuzzleClearButton(_cost);
        objectiveLabel.text = Presentation.PuzzleObjectiveHint(_cityName);
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
