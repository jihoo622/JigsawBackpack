using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleMapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] string cityId;
    [SerializeField] Image marker;
    [SerializeField] Text label;

    internal string CityId => cityId;
    internal event Action<string> Hovered;
    internal event Action Exited;

    internal void Show(PuzzleCityData _city, bool _visited, bool _current)
    {
        label.text = Presentation.PuzzleMapNodeLabel(_city.displayName, _visited, _current);
        label.color = Presentation.PuzzleHeading;
        marker.color = _current ? Presentation.PuzzleMapCurrent
            : _visited ? Presentation.PuzzleMapVisited : Presentation.PuzzleMapUnknown;
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        Hovered?.Invoke(cityId);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        Exited?.Invoke();
    }
}
