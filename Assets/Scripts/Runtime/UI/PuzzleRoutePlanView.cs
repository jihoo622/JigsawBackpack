using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

// 경로 계획 창의 UI 표시 (View). 프리팹에 미리 배치한 슬롯을 켜고 끄며 상태를 표시한다.
public sealed class PuzzleRoutePlanView : MonoBehaviour
{
    [Serializable]
    public sealed class StationSlot
    {
        public GameObject root;
        public TextMeshProUGUI cityName;
        public Toggle stopToggle;
        public TextMeshProUGUI stopLabel;
    }

    public readonly struct StationDisplayData
    {
        public readonly string cityId;
        public readonly string cityName;
        public readonly Color cityColor;
        public readonly bool isStop;
        public readonly bool isDestination;
        public readonly string stopLabelText;

        public StationDisplayData(string _cityId, string _cityName, Color _cityColor, bool _isStop, bool _isDestination, string _stopLabelText)
        {
            cityId = _cityId;
            cityName = _cityName;
            cityColor = _cityColor;
            isStop = _isStop;
            isDestination = _isDestination;
            stopLabelText = _stopLabelText;
        }
    }

    [Header("Panel Root")]
    [SerializeField] GameObject panelRoot;

    [Header("Header & Summary")]
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI summaryLabel;
    [SerializeField] TextMeshProUGUI warningLabel;

    [Header("Station Slots")]
    [SerializeField] StationSlot[] slots = Array.Empty<StationSlot>();
    [SerializeField] GameObject overflowRoot;
    [SerializeField] TextMeshProUGUI overflowLabel;

    [Header("Buttons")]
    [SerializeField] Button autoTravelButton;
    [SerializeField] TextMeshProUGUI autoTravelButtonLabel;
    [SerializeField] Button stopButton;
    [SerializeField] TextMeshProUGUI stopButtonLabel;
    [SerializeField] Button cancelButton;
    [SerializeField] TextMeshProUGUI cancelButtonLabel;

    public event Action AutoTravelClicked;
    public event Action StopClicked;
    public event Action CancelClicked;
    public event Action<string, bool> StopToggled;

    readonly List<string> boundSlotCityIds = new List<string>();
    bool suppressToggleEvents;

    public int SlotCount
    {
        get
        {
            return slots != null ? slots.Length : 0;
        }
    }

    public bool IsVisible
    {
        get
        {
            return (panelRoot ? panelRoot : gameObject).activeSelf;
        }
    }

    void Awake()
    {
        if (autoTravelButton)
        {
            autoTravelButton.onClick.AddListener(OnAutoTravelButtonClicked);
        }
        if (stopButton)
        {
            stopButton.onClick.AddListener(OnStopButtonClicked);
        }
        if (cancelButton)
        {
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                if (slots[i] != null && slots[i].stopToggle)
                {
                    slots[i].stopToggle.onValueChanged.AddListener(_isOn => OnSlotToggleChanged(index, _isOn));
                }
            }
        }
    }

    void OnAutoTravelButtonClicked()
    {
        AutoTravelClicked?.Invoke();
    }

    void OnStopButtonClicked()
    {
        StopClicked?.Invoke();
    }

    void OnCancelButtonClicked()
    {
        CancelClicked?.Invoke();
    }

    void OnSlotToggleChanged(int _slotIndex, bool _isOn)
    {
        if (suppressToggleEvents)
        {
            return;
        }
        if (_slotIndex >= 0 && _slotIndex < boundSlotCityIds.Count)
        {
            string cityId = boundSlotCityIds[_slotIndex];
            if (!string.IsNullOrEmpty(cityId))
            {
                StopToggled?.Invoke(cityId, _isOn);
            }
        }
    }

    public void Show(
        string _title,
        string _summary,
        string _warning,
        IReadOnlyList<StationDisplayData> _stations,
        bool _isAutoTraveling,
        string _overflowText)
    {
        (panelRoot ? panelRoot : gameObject).SetActive(true);

        if (titleLabel)
        {
            titleLabel.text = _title;
        }
        if (summaryLabel)
        {
            summaryLabel.text = _summary;
        }
        if (warningLabel)
        {
            bool hasWarning = !string.IsNullOrEmpty(_warning);
            warningLabel.gameObject.SetActive(hasWarning);
            if (hasWarning)
            {
                warningLabel.text = _warning;
                warningLabel.color = Presentation.RoutePlanWarningColor;
            }
        }

        if (autoTravelButton)
        {
            autoTravelButton.gameObject.SetActive(!_isAutoTraveling);
        }
        if (autoTravelButtonLabel)
        {
            autoTravelButtonLabel.text = Presentation.RouteAutoTravelButton;
        }

        if (stopButton)
        {
            stopButton.gameObject.SetActive(_isAutoTraveling);
        }
        if (stopButtonLabel)
        {
            stopButtonLabel.text = Presentation.RouteStopButton;
        }

        if (cancelButton)
        {
            cancelButton.gameObject.SetActive(!_isAutoTraveling);
        }
        if (cancelButtonLabel)
        {
            cancelButtonLabel.text = Presentation.RouteCancelButton;
        }

        suppressToggleEvents = true;
        boundSlotCityIds.Clear();

        int stationCount = _stations != null ? _stations.Count : 0;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }
                if (i < stationCount)
                {
                    var data = _stations[i];
                    boundSlotCityIds.Add(data.cityId);
                    if (slot.root)
                    {
                        slot.root.SetActive(true);
                    }
                    if (slot.cityName)
                    {
                        slot.cityName.text = data.cityName;
                        slot.cityName.color = data.cityColor;
                    }
                    if (slot.stopToggle)
                    {
                        slot.stopToggle.SetIsOnWithoutNotify(data.isStop);
                        slot.stopToggle.interactable = !data.isDestination && !_isAutoTraveling;
                    }
                    if (slot.stopLabel)
                    {
                        slot.stopLabel.text = data.stopLabelText;
                    }
                }
                else
                {
                    if (slot.root)
                    {
                        slot.root.SetActive(false);
                    }
                }
            }
        }

        suppressToggleEvents = false;

        bool hasOverflow = !string.IsNullOrEmpty(_overflowText);
        if (overflowRoot)
        {
            overflowRoot.SetActive(hasOverflow);
        }
        if (overflowLabel)
        {
            overflowLabel.gameObject.SetActive(hasOverflow);
            if (hasOverflow)
            {
                overflowLabel.text = _overflowText;
            }
        }
    }

    public void Hide()
    {
        (panelRoot ? panelRoot : gameObject).SetActive(false);
    }
}
