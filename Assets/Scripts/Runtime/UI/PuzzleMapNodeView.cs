using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleMapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] string cityId;
    [SerializeField] Image marker;
    [SerializeField] Text label;

    internal string CityId => cityId;
    internal event Action<string> Hovered;
    internal event Action Exited;
    internal event Action<string> Clicked;

    internal void Show(PuzzleCityData _city, bool _visited, bool _current, bool _connected, bool _canAfford)
    {
        label.text = Presentation.PuzzleMapNodeLabel(_city.displayName, _visited, _current, _connected, _canAfford);
        label.color = Presentation.PuzzleHeading;
        marker.color = Presentation.PuzzleMapNodeMarkerColor(_current, _visited, _connected, _canAfford);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        Hovered?.Invoke(cityId);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        Exited?.Invoke();
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (_eventData.button == PointerEventData.InputButton.Left)
        {
            Clicked?.Invoke(cityId);
        }
    }
}
