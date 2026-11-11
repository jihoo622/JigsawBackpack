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

    bool isCurrent;
    bool isTarget;
    bool isConnected;
    bool IsEmphasized => isCurrent || isTarget;
    internal Vector2 LabelSize { get; private set; }

    internal string CityId => cityId;
    internal event Action<string> Hovered;
    internal event Action Exited;
    internal event Action<string> Clicked;

    internal int LabelPriority(string _selectedCityId)
    {
        return isCurrent ? 5 : cityId == _selectedCityId ? 4 : isTarget ? 3 : isConnected ? 2 : interchange ? 1 : 0;
    }

    internal void PrepareLayout(float _zoom)
    {
        transform.localScale = Vector3.one / _zoom;
        ((RectTransform)transform).sizeDelta = Vector2.one * BasicData.Float("ui.PuzzleMapNodeView.PrepareLayout.number1");
        label.raycastTarget = false;
        label.fontSize = Presentation.PuzzleMapStationFontSize(_zoom, IsEmphasized);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = false;
        float width = Mathf.Ceil(label.preferredWidth) + BasicData.Float("ui.PuzzleMapNodeView.PrepareLayout.number2");
        label.rectTransform.sizeDelta = new Vector2(width, BasicData.Float("ui.PuzzleMapNodeView.Vector2.number1"));
        currentHalo.rectTransform.sizeDelta = Vector2.one * Presentation.PuzzleMapHaloSize;
        currentBadge.horizontalOverflow = HorizontalWrapMode.Overflow;
        currentBadge.verticalOverflow = VerticalWrapMode.Overflow;
        currentBadge.fontSize = Presentation.PuzzleMapBadgeFontSize;
        float badgeWidth = Mathf.Ceil(currentBadge.preferredWidth) + BasicData.Float("ui.PuzzleMapNodeView.Vector2.number2");
        currentBadge.rectTransform.sizeDelta = new Vector2(Mathf.Max(badgeWidth, BasicData.Float("ui.PuzzleMapNodeView.Vector2.number3")), BasicData.Float("ui.PuzzleMapNodeView.Vector2.number4"));
        LabelSize = new Vector2(IsEmphasized ? Mathf.Max(width, Mathf.Max(badgeWidth, BasicData.Float("ui.PuzzleMapNodeView.Vector2.number5"))) : width, IsEmphasized ? BasicData.Float("ui.PuzzleMapNodeView.Vector2.number6") : BasicData.Float("ui.PuzzleMapNodeView.Vector2.number7"));
    }

    internal Vector2 MarkerPosition(RectTransform _viewport)
    {
        return _viewport.InverseTransformPoint(stationRing.transform.position);
    }

    internal float MarkerRadius => IsEmphasized ? BasicData.Float("ui.PuzzleMapNodeView.MarkerRadius.number1") : BasicData.Float("ui.PuzzleMapNodeView.MarkerRadius.number2");

    internal void PlaceLabel(bool _visible, Vector2 _footprintOffset)
    {
        label.gameObject.SetActive(_visible);
        currentBadge.gameObject.SetActive(_visible && IsEmphasized);
        Vector2 offset = _footprintOffset - (IsEmphasized ? new Vector2(0f, BasicData.Float("ui.PuzzleMapNodeView.Vector2.number8")) : Vector2.zero);
        label.rectTransform.anchoredPosition = offset;
        currentBadge.rectTransform.anchoredPosition = offset + new Vector2(0f, BasicData.Float("ui.PuzzleMapNodeView.Vector2.number9"));
    }

    internal void Show(PuzzleCityData _city, bool _visited, bool _current, bool _connected, bool _canAfford, bool _planningTarget)
    {
        isCurrent = _current;
        isTarget = _planningTarget;
        isConnected = _connected;
        bool emphasized = IsEmphasized;
        Color highlight = _current ? Presentation.PuzzleMapCurrent : Presentation.PuzzleMapTarget;
        label.text = Presentation.PuzzleMapStationName(_city.displayName, _planningTarget);
        label.color = emphasized ? highlight : Presentation.PuzzleHeading;
        label.fontStyle = emphasized ? FontStyle.Bold : FontStyle.Normal;
        currentHalo.gameObject.SetActive(emphasized);
        currentHalo.color = _current ? Presentation.PuzzleMapCurrentHalo : Presentation.PuzzleMapTargetHalo;
        currentBadge.gameObject.SetActive(emphasized);
        currentBadge.text = Presentation.PuzzleMapBadgeText(_current, _planningTarget);
        currentBadge.color = highlight;
        if (emphasized)
        {
            transform.SetAsLastSibling();
        }
        stationRing.rectTransform.localScale = Vector3.one * (emphasized ? BasicData.Float("ui.PuzzleMapNodeView.Show.number1") : 1f);
        stationRing.color = emphasized ? highlight :
            Presentation.PuzzleMapLineColor(_city.regionId);
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
