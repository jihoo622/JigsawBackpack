using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleMapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] string cityId;
    [SerializeField] Image marker;
    [SerializeField] Text label;
    [SerializeField] Image stationRing;
    [SerializeField] bool interchange;
    [SerializeField] Image currentHalo;
    [SerializeField] Text currentBadge;

    internal string CityId => cityId;
    internal event Action<string> Hovered;
    internal event Action Exited;
    internal event Action<string> Clicked;

    internal void Show(PuzzleCityData _city, bool _visited, bool _current, bool _connected, bool _canAfford, bool _planningTarget)
    {
        label.text = Presentation.PuzzleMapNodeLabel(Presentation.PuzzleMapStationName(_city.displayName, _planningTarget),
            _visited, _current, _connected, _canAfford);
        label.color = _current ? Presentation.PuzzleMapCurrent : Presentation.PuzzleHeading;
        label.fontStyle = _current ? FontStyle.Bold : FontStyle.Normal;
        currentHalo.gameObject.SetActive(_current);
        currentHalo.color = Presentation.PuzzleMapCurrentHalo;
        currentBadge.gameObject.SetActive(_current);
        currentBadge.text = Presentation.PUZZLE_MAP_CURRENT;
        currentBadge.color = Presentation.PuzzleMapCurrent;
        if (_current)
        {
            transform.SetAsLastSibling();
        }
        stationRing.rectTransform.localScale = Vector3.one * (_current ? 1.5f : 1f);
        stationRing.color = _current ? Presentation.PuzzleMapCurrent : _planningTarget ? Presentation.PuzzleMapCurrent :
            interchange ? Presentation.PuzzleMapInterchange : Presentation.PuzzleMapLineColor(_city.regionId);
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
