using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

public sealed class PuzzleMapNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ICanvasRaycastFilter
{
    [SerializeField] string cityId;
    [SerializeField] Image marker;
    [SerializeField] Text label;
    [SerializeField] Image stationRing;
    [SerializeField] bool interchange;
    [SerializeField] Image currentHalo;
    [SerializeField] Text currentBadge;
    [SerializeField] Image labelConnector;

    bool isCurrent;
    bool isTarget;
    bool isConnected;
    bool layoutPrepared;
    Vector2 preferredLabelDirection;
    float nameOffsetY, badgeOffsetY;
    readonly Vector3[] corners = new Vector3[4];
    PuzzleMapView map;
    string regionId;
    int labelImportance, regionPriority;
    bool IsEmphasized => isCurrent || isTarget;
    internal Vector2 LabelSize { get; private set; }
    internal Vector2 PreferredLabelDirection => preferredLabelDirection;

    internal string CityId => cityId;
    internal string RegionId => regionId;
    bool IsMajor => labelImportance >= BasicData.Int("ui.mapLabels.majorThreshold");
    internal event Action<string> Hovered;
    internal event Action<string> Exited;
    internal event Action<string> Clicked;

    void Awake()
    {
        map = GetComponentInParent<PuzzleMapView>();
    }

    public bool IsRaycastLocationValid(Vector2 _screenPoint, Camera _camera)
    {
        if (!ContainsMarker(_screenPoint, _camera))
        {
            return false;
        }
        if (map)
        {
            float distance = (RectTransformUtility.WorldToScreenPoint(_camera, stationRing.transform.position) - _screenPoint).sqrMagnitude;
            foreach (var node in map.Nodes)
            {
                if (node == this || !node.isActiveAndEnabled || !node.ContainsMarker(_screenPoint, _camera))
                {
                    continue;
                }
                float other = (RectTransformUtility.WorldToScreenPoint(_camera, node.stationRing.transform.position) - _screenPoint).sqrMagnitude;
                if (other < distance || (Mathf.Approximately(other, distance) && string.CompareOrdinal(node.cityId, cityId) < 0))
                {
                    return false;
                }
            }
        }
        return true;
    }

    bool ContainsMarker(Vector2 _screenPoint, Camera _camera)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(stationRing.rectTransform, _screenPoint, _camera);
    }

    internal long LabelPriority(string _selectedCityId)
    {
        int state = isCurrent ? BasicData.Int("ui.mapLabels.current")
            : cityId == _selectedCityId ? BasicData.Int("ui.mapLabels.selected")
            : isTarget ? BasicData.Int("ui.mapLabels.target")
            : isConnected ? BasicData.Int("ui.mapLabels.connected")
            : interchange ? BasicData.Int("ui.mapLabels.interchange")
            : BasicData.Int("ui.mapLabels.other");
        return (long)labelImportance + regionPriority + state;
    }

    internal bool ShouldShowLabel(float _zoom, string _selectedCityId)
    {
        return IsEmphasized || cityId == _selectedCityId || IsMajor
            || _zoom >= BasicData.Float("ui.mapLabels.detailMinZoom");
    }

    internal void PrepareLayout(float _zoom)
    {
        if (!layoutPrepared)
        {
            Vector2 authored = label.rectTransform.anchoredPosition;
            preferredLabelDirection = new Vector2(authored.x == 0f ? 0f : Mathf.Sign(authored.x),
                authored.y == 0f ? 0f : Mathf.Sign(authored.y));
            if (preferredLabelDirection == Vector2.zero)
            {
                preferredLabelDirection = Vector2.up;
            }
            layoutPrepared = true;
        }
        transform.localScale = Vector3.one / _zoom;
        ((RectTransform)transform).sizeDelta = Vector2.one * BasicData.Float("ui.PuzzleMapNodeView.prepareLayout");
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = Presentation.PuzzleMapStationFontSize(_zoom, IsEmphasized, IsMajor);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        float width = Mathf.Ceil(label.preferredWidth) + BasicData.Float("ui.PuzzleMapNodeView.prepareLayout2");
        CenterAnchor(label.rectTransform);
        label.rectTransform.sizeDelta = new Vector2(width, BasicData.Float("ui.PuzzleMapNodeView.rectTransformsizeDeltaHeight"));
        float nameHeight = Mathf.Max(label.rectTransform.sizeDelta.y, Mathf.Ceil(label.preferredHeight));
        label.rectTransform.sizeDelta = new Vector2(width, nameHeight);
        currentHalo.rectTransform.sizeDelta = Vector2.one * Presentation.PuzzleMapHaloSize;
        currentBadge.horizontalOverflow = HorizontalWrapMode.Overflow;
        currentBadge.verticalOverflow = VerticalWrapMode.Overflow;
        currentBadge.fontSize = Presentation.PuzzleMapBadgeFontSize;
        currentBadge.alignment = TextAnchor.MiddleCenter;
        currentBadge.raycastTarget = false;
        currentBadge.resizeTextForBestFit = false;
        CenterAnchor(currentBadge.rectTransform);
        float badgeWidth = Mathf.Ceil(currentBadge.preferredWidth) + BasicData.Float("ui.PuzzleMapNodeView.prepareLayout3");
        currentBadge.rectTransform.sizeDelta = new Vector2(Mathf.Max(badgeWidth, BasicData.Float("ui.PuzzleMapNodeView.rectTransformsizeDeltaWidth")), BasicData.Float("ui.PuzzleMapNodeView.rectTransformsizeDeltaHeight2"));
        float badgeHeight = Mathf.Max(currentBadge.rectTransform.sizeDelta.y, Mathf.Ceil(currentBadge.preferredHeight));
        currentBadge.rectTransform.sizeDelta = new Vector2(currentBadge.rectTransform.sizeDelta.x, badgeHeight);
        float gap = BasicData.Float("ui.PuzzleMapView.labelGap");
        nameOffsetY = IsEmphasized ? -(badgeHeight + gap) * 0.5f : 0f;
        badgeOffsetY = (nameHeight + gap) * 0.5f;
        LabelSize = new Vector2(IsEmphasized ? Mathf.Max(width, currentBadge.rectTransform.sizeDelta.x) : width,
            IsEmphasized ? nameHeight + badgeHeight + gap : nameHeight);
        CenterAnchor(labelConnector.rectTransform);
        labelConnector.raycastTarget = false;
    }

    static void CenterAnchor(RectTransform _rect)
    {
        _rect.anchorMin = _rect.anchorMax = _rect.pivot = Vector2.one * 0.5f;
        _rect.localScale = Vector3.one;
    }

    internal Vector2 MarkerPosition(RectTransform _viewport)
    {
        return _viewport.InverseTransformPoint(stationRing.transform.position);
    }

    internal Rect MarkerBounds(RectTransform _viewport)
    {
        Rect bounds = RectInViewport(stationRing.rectTransform, _viewport);
        if (IsEmphasized)
        {
            Rect halo = RectInViewport(currentHalo.rectTransform, _viewport);
            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, halo.xMin), Mathf.Min(bounds.yMin, halo.yMin),
                Mathf.Max(bounds.xMax, halo.xMax), Mathf.Max(bounds.yMax, halo.yMax));
        }
        return bounds;
    }

    Rect RectInViewport(RectTransform _rect, RectTransform _viewport)
    {
        _rect.GetWorldCorners(corners);
        Vector2 min = _viewport.InverseTransformPoint(corners[0]);
        Vector2 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = _viewport.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    internal void HideLabel()
    {
        label.gameObject.SetActive(false);
        currentBadge.gameObject.SetActive(false);
        labelConnector.gameObject.SetActive(false);
    }

    internal void PlaceLabel(Rect _footprint, Vector2 _start, Vector2 _end, RectTransform _viewport, Vector2 _direction)
    {
        preferredLabelDirection = _direction;
        Vector2 center = transform.InverseTransformPoint(_viewport.TransformPoint(_footprint.center));
        label.rectTransform.anchoredPosition = center + Vector2.up * nameOffsetY;
        currentBadge.rectTransform.anchoredPosition = center + Vector2.up * badgeOffsetY;
        Vector2 start = transform.InverseTransformPoint(_viewport.TransformPoint(_start));
        Vector2 end = transform.InverseTransformPoint(_viewport.TransformPoint(_end));
        Vector2 delta = end - start;
        var connector = labelConnector.rectTransform;
        connector.anchoredPosition = (start + end) * 0.5f;
        connector.sizeDelta = new Vector2(delta.magnitude, BasicData.Float("ui.PuzzleMapView.labelConnectorThickness"));
        connector.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        labelConnector.gameObject.SetActive(true);
        label.gameObject.SetActive(true);
        currentBadge.gameObject.SetActive(IsEmphasized);
    }

    internal void Show(PuzzleCityData _city, bool _visited, bool _current, bool _connected, bool _canAfford, bool _planningTarget, bool _hasRumor)
    {
        if (!string.Equals(cityId, _city.id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"지도 노드 ID와 도시가 다릅니다: {cityId} / {_city.id}");
        }
        isCurrent = _current;
        regionId = _city.regionId;
        labelImportance = _city.mapLabelImportance;
        regionPriority = TableManager.Instance.Region(regionId).mapLabelPriority;
        isTarget = _planningTarget;
        isConnected = _connected;
        bool emphasized = IsEmphasized;
        Color highlight = _current ? Presentation.PuzzleMapCurrent : Presentation.PuzzleMapTarget;
        label.supportRichText = true;
        label.text = Presentation.PuzzleMapStationName(_city.displayName, _planningTarget, !_visited && _hasRumor);
        label.color = emphasized ? highlight : Presentation.PuzzleHeading;
        labelConnector.color = emphasized ? highlight : Presentation.PuzzleMapLineColor(_city.regionId);
        label.fontStyle = emphasized || IsMajor ? FontStyle.Bold : FontStyle.Normal;
        currentHalo.gameObject.SetActive(emphasized);
        currentHalo.color = _current ? Presentation.PuzzleMapCurrentHalo : Presentation.PuzzleMapTargetHalo;
        currentBadge.gameObject.SetActive(emphasized);
        currentBadge.text = Presentation.PuzzleMapBadgeText(_current, _planningTarget);
        currentBadge.color = highlight;
        if (emphasized)
        {
            transform.SetAsLastSibling();
        }
        stationRing.rectTransform.localScale = Vector3.one * (emphasized ? BasicData.Float("ui.PuzzleMapNodeView.rectTransformlocalScale") : 1f);
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
        Exited?.Invoke(cityId);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (_eventData.button == PointerEventData.InputButton.Left)
        {
            Clicked?.Invoke(cityId);
        }
    }
}
