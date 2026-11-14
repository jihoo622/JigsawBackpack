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

    [Header("E3 Tags and Badges")]
    [SerializeField] GameObject tagChipsContainer;
    [SerializeField] Image tagPlate0;
    [SerializeField] Text tagLabel0;
    [SerializeField] Image tagPlate1;
    [SerializeField] Text tagLabel1;
    [SerializeField] Text rumorBadge;
    [SerializeField] Text questBadge;

    bool isCurrent;
    bool isTarget;
    // 진행 중 의뢰의 목적지. 줌과 관계없이 이름·의뢰 표식을 보인다.
    bool isQuestTarget;
    bool hasRumor;
    bool hasAnyTag;
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
    bool IsMajor(int _majorThreshold) => labelImportance >= _majorThreshold;
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

    internal long LabelPriority(string _selectedCityId, in PuzzleMapView.NodeLayoutSettings _settings)
    {
        int state = isCurrent ? _settings.labelPriorityCurrent
            : cityId == _selectedCityId ? _settings.labelPrioritySelected
            : isTarget || isQuestTarget ? _settings.labelPriorityTarget
            : isConnected ? _settings.labelPriorityConnected
            : interchange ? _settings.labelPriorityInterchange
            : _settings.labelPriorityOther;
        return (long)labelImportance + regionPriority + state;
    }

    // 주요 역·현재·목표·선택 역은 줌과 관계없이 이름을 표시해야 한다.
    internal bool IsLabelRequired(string _selectedCityId, in PuzzleMapView.NodeLayoutSettings _settings)
    {
        return IsEmphasized || isQuestTarget || cityId == _selectedCityId || IsMajor(_settings.majorStationThreshold);
    }

    internal bool ShouldShowLabel(float _zoom, string _selectedCityId, in PuzzleMapView.NodeLayoutSettings _settings)
    {
        return IsLabelRequired(_selectedCityId, _settings) || _zoom >= _settings.labelDetailMinZoom;
    }

    internal void PrepareLayout(float _zoom, in PuzzleMapView.NodeLayoutSettings _settings)
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
        ((RectTransform)transform).sizeDelta = Vector2.one * _settings.nodeSize;
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = _settings.CalculateStationFontSize(_zoom, IsEmphasized, IsMajor(_settings.majorStationThreshold));
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        float width = Mathf.Ceil(label.preferredWidth) + _settings.stationLabelExtraWidth;
        CenterAnchor(label.rectTransform);
        label.rectTransform.sizeDelta = new Vector2(width, _settings.stationLabelHeight);
        float nameHeight = Mathf.Max(label.rectTransform.sizeDelta.y, Mathf.Ceil(label.preferredHeight));
        label.rectTransform.sizeDelta = new Vector2(width, nameHeight);
        currentHalo.rectTransform.sizeDelta = Vector2.one * _settings.stationHaloSize;
        currentBadge.horizontalOverflow = HorizontalWrapMode.Overflow;
        currentBadge.verticalOverflow = VerticalWrapMode.Overflow;
        currentBadge.fontSize = _settings.stationBadgeFontSize;
        currentBadge.alignment = TextAnchor.MiddleCenter;
        currentBadge.raycastTarget = false;
        currentBadge.resizeTextForBestFit = false;
        CenterAnchor(currentBadge.rectTransform);
        float badgeWidth = Mathf.Ceil(currentBadge.preferredWidth) + _settings.stationBadgeExtraWidth;
        currentBadge.rectTransform.sizeDelta = new Vector2(Mathf.Max(badgeWidth, _settings.stationBadgeMinWidth), _settings.stationBadgeHeight);
        float badgeHeight = Mathf.Max(currentBadge.rectTransform.sizeDelta.y, Mathf.Ceil(currentBadge.preferredHeight));
        currentBadge.rectTransform.sizeDelta = new Vector2(currentBadge.rectTransform.sizeDelta.x, badgeHeight);
        float gap = _settings.labelGap;
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
        if (tagChipsContainer) tagChipsContainer.SetActive(false);
        if (rumorBadge) rumorBadge.gameObject.SetActive(false);
        if (questBadge) questBadge.gameObject.SetActive(false);
    }

    internal void PlaceLabel(Rect _footprint, Vector2 _start, Vector2 _end, RectTransform _viewport, Vector2 _direction, float _connectorThickness)
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
        connector.sizeDelta = new Vector2(delta.magnitude, _connectorThickness);
        connector.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        labelConnector.gameObject.SetActive(true);
        label.gameObject.SetActive(true);

        if (tagChipsContainer)
        {
            var tcRt = tagChipsContainer.GetComponent<RectTransform>();
            if (tcRt)
            {
                tcRt.anchoredPosition = center + Vector2.up * (nameOffsetY - label.rectTransform.sizeDelta.y * 0.5f - 14f);
            }
            tagChipsContainer.SetActive(hasAnyTag);
        }
        LayoutBadges(center);
    }

    void LayoutBadges(Vector2 center)
    {
        var activeBadges = new System.Collections.Generic.List<Text>();
        if (IsEmphasized && currentBadge) activeBadges.Add(currentBadge);
        if (hasRumor && rumorBadge) activeBadges.Add(rumorBadge);
        if (isQuestTarget && questBadge) activeBadges.Add(questBadge);

        float totalW = 0f;
        for (int i = 0; i < activeBadges.Count; i++)
        {
            totalW += activeBadges[i].rectTransform.sizeDelta.x + (i > 0 ? 4f : 0f);
        }

        float curX = center.x - totalW * 0.5f;
        foreach (var b in activeBadges)
        {
            b.gameObject.SetActive(true);
            float bw = b.rectTransform.sizeDelta.x;
            b.rectTransform.anchoredPosition = new Vector2(curX + bw * 0.5f, center.y + badgeOffsetY);
            curX += bw + 4f;
        }

        if (!IsEmphasized && currentBadge) currentBadge.gameObject.SetActive(false);
        if (!hasRumor && rumorBadge) rumorBadge.gameObject.SetActive(false);
        if (!isQuestTarget && questBadge) questBadge.gameObject.SetActive(false);
    }

    internal void Show(PuzzleCityData _city, bool _visited, bool _current, bool _connected, bool _canAfford, bool _planningTarget, bool _hasRumor,
        string _questBadge = null)
    {
        var settings = map ? map.GetNodeLayoutSettings() : default;
        Show(_city, _visited, _current, _connected, _canAfford, _planningTarget, _hasRumor, settings, _questBadge);
    }

    // _questBadge: 이 역이 목적지인 진행 중 의뢰의 표식(남은 시간 포함). 없으면 null.
    internal void Show(PuzzleCityData _city, bool _visited, bool _current, bool _connected, bool _canAfford, bool _planningTarget, bool _hasRumor, in PuzzleMapView.NodeLayoutSettings _settings,
        string _questBadge = null)
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
        hasRumor = _hasRumor;
        isQuestTarget = !string.IsNullOrEmpty(_questBadge);
        bool emphasized = IsEmphasized;
        Color highlight = _current ? Presentation.PuzzleMapCurrent : Presentation.PuzzleMapTarget;
        label.supportRichText = true;
        label.text = Presentation.QuestMapStation(
            Presentation.PuzzleMapStationName(_city.displayName, _planningTarget, !_visited && _hasRumor), _questBadge);
        label.color = emphasized ? highlight : Presentation.PuzzleHeading;
        labelConnector.color = emphasized ? highlight : Presentation.PuzzleMapLineColor(_city.regionId);
        label.fontStyle = emphasized || IsMajor(_settings.majorStationThreshold) ? FontStyle.Bold : FontStyle.Normal;
        currentHalo.gameObject.SetActive(emphasized);
        currentHalo.color = _current ? Presentation.PuzzleMapCurrentHalo : Presentation.PuzzleMapTargetHalo;
        currentBadge.gameObject.SetActive(emphasized);
        currentBadge.text = Presentation.PuzzleMapBadgeText(_current, _planningTarget);
        currentBadge.color = highlight;

        if (rumorBadge)
        {
            rumorBadge.text = Presentation.MapRumorBadge;
            rumorBadge.color = Presentation.PuzzleMapCurrent;
            rumorBadge.gameObject.SetActive(_hasRumor);
        }

        if (questBadge)
        {
            questBadge.text = "의뢰 목적지";
            questBadge.color = Presentation.QuestMapMarker;
            questBadge.gameObject.SetActive(isQuestTarget);
        }

        // 선호 태그 칩 설정 (상위 2개)
        var tagPrices = _city.tagPrices ?? System.Array.Empty<PuzzleCityTagPriceData>();
        var preferredTags = new System.Collections.Generic.List<PuzzleCityTagPriceData>();
        foreach (var tp in tagPrices)
        {
            if (tp != null && (tp.sellPercent > 100 || tp.buyPercent > 100))
            {
                preferredTags.Add(tp);
            }
        }
        preferredTags.Sort((a, b) => b.sellPercent.CompareTo(a.sellPercent));

        hasAnyTag = preferredTags.Count > 0;
        if (tagChipsContainer)
        {
            if (preferredTags.Count >= 1)
            {
                if (tagPlate0) tagPlate0.gameObject.SetActive(true);
                if (tagLabel0)
                {
                    string tagName = Presentation.PuzzleTagLabel(preferredTags[0].tag);
                    tagLabel0.text = _visited ? $"{tagName} {preferredTags[0].sellPercent}%" : tagName;
                }
            }
            else
            {
                if (tagPlate0) tagPlate0.gameObject.SetActive(false);
            }

            if (preferredTags.Count >= 2)
            {
                if (tagPlate1) tagPlate1.gameObject.SetActive(true);
                if (tagLabel1)
                {
                    string tagName = Presentation.PuzzleTagLabel(preferredTags[1].tag);
                    tagLabel1.text = _visited ? $"{tagName} {preferredTags[1].sellPercent}%" : tagName;
                }
            }
            else
            {
                if (tagPlate1) tagPlate1.gameObject.SetActive(false);
            }
            tagChipsContainer.SetActive(hasAnyTag);
        }
        if (emphasized)
        {
            transform.SetAsLastSibling();
        }
        stationRing.rectTransform.localScale = Vector3.one * (emphasized ? _settings.nodeEmphasizedScale : 1f);
        stationRing.color = emphasized ? highlight :
            isQuestTarget ? Presentation.QuestMapMarker :
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
