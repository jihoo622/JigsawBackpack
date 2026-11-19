using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// S2: 다이제틱 마을 건물 버튼. 호버 하이라이트와 세계 안 상태 오브젝트(사전 배치 숨김)를 관리한다.
[DisallowMultipleComponent]
public sealed class TownBuildingButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum BuildingKind
    {
        Market = 0,
        Tavern = 1,
        Rest = 2,
        StationHall = 3,
        Workshop = 4,
        CityHall = 5
    }

    [SerializeField] BuildingKind kind;
    [SerializeField] Button button;
    [SerializeField] Image highlightBorder;
    [SerializeField] TextMeshProUGUI nameTag;
    [SerializeField] GameObject chalkboardObject;
    [SerializeField] TextMeshProUGUI chalkboardText;
    [SerializeField] GameObject rumorBubbleObject;
    [SerializeField] TextMeshProUGUI rumorBubbleText;
    [SerializeField] GameObject chimneySmokeObject;
    [SerializeField] GameObject clockObject;
    [SerializeField] GameObject windowGlowObject;
    [SerializeField] GameObject workshopVisual;
    [SerializeField] GameObject emptyLotVisual;

    public event Action<BuildingKind> Clicked;
    public event Action<BuildingKind, bool, string> HoveredChanged;

    public BuildingKind Kind => kind;
    bool isDisabled;
    string disabledReason;
    Color baseWallColor;
    bool baseWallColorCaptured;

    void OnEnable()
    {
        if (button && !baseWallColorCaptured && button.targetGraphic)
        {
            baseWallColor = button.targetGraphic.color;
            baseWallColorCaptured = true;
        }
        if (button)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
        SetHoverHighlight(false);
        RefreshNameTag();
    }

    void OnDisable()
    {
        if (button)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
        SetHoverHighlight(false);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        SetHoverHighlight(true);
        HoveredChanged?.Invoke(kind, true, isDisabled ? disabledReason : string.Empty);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        SetHoverHighlight(false);
        HoveredChanged?.Invoke(kind, false, isDisabled ? disabledReason : string.Empty);
    }

    public void SetDisabled(bool _disabled, string _reason)
    {
        isDisabled = _disabled;
        disabledReason = _reason;
        if (button && button.targetGraphic)
        {
            if (!baseWallColorCaptured)
            {
                baseWallColor = button.targetGraphic.color;
                baseWallColorCaptured = true;
            }
            var c = baseWallColor;
            c.a = _disabled ? baseWallColor.a * 0.48f : baseWallColor.a;
            button.targetGraphic.color = c;
        }
    }

    public void SetHoverHighlight(bool _hovered)
    {
        if (highlightBorder)
        {
            highlightBorder.enabled = _hovered;
        }
    }

    public void RefreshNameTag()
    {
        if (!nameTag)
        {
            return;
        }
        nameTag.text = kind switch
        {
            BuildingKind.Market => Presentation.BuildingMarket,
            BuildingKind.Tavern => Presentation.BuildingTavern,
            BuildingKind.Rest => Presentation.BuildingRest,
            BuildingKind.StationHall => Presentation.BuildingStation,
            BuildingKind.Workshop => Presentation.BuildingWorkshop,
            BuildingKind.CityHall => Presentation.BuildingCityHall,
            _ => string.Empty
        };
    }

    public void SetWorkshopAvailable(bool _hasWorkshop)
    {
        if (workshopVisual) workshopVisual.SetActive(_hasWorkshop);
        if (emptyLotVisual) emptyLotVisual.SetActive(!_hasWorkshop);
        if (chimneySmokeObject) chimneySmokeObject.SetActive(_hasWorkshop);
    }

    public void SetChalkboard(string _text)
    {
        bool active = !string.IsNullOrEmpty(_text);
        if (chalkboardObject) chalkboardObject.SetActive(active);
        if (chalkboardText) chalkboardText.text = active ? _text : string.Empty;
    }

    public void SetRumorBubble(string _text)
    {
        bool active = !string.IsNullOrEmpty(_text);
        if (rumorBubbleObject) rumorBubbleObject.SetActive(active);
        if (rumorBubbleText) rumorBubbleText.text = active ? _text : string.Empty;
    }

    public void SetWindowGlow(bool _lit)
    {
        if (windowGlowObject) windowGlowObject.SetActive(_lit);
    }

    public void SetClockVisible(bool _visible)
    {
        if (clockObject) clockObject.SetActive(_visible);
    }

    void OnButtonClicked()
    {
        Clicked?.Invoke(kind);
    }
}
