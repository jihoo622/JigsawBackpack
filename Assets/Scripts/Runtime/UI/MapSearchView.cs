using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MapSearchView : MonoBehaviour
{
    public const int SLOT_COUNT = 6;

    [Serializable]
    public struct SearchResultSlot
    {
        public GameObject root;
        public Button selectButton;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI kindText;
        public Button routeButton;
        public TextMeshProUGUI routeButtonLabel;
    }

    [SerializeField] TMP_InputField inputField;
    [SerializeField] GameObject resultRoot;
    [SerializeField] SearchResultSlot[] slots = new SearchResultSlot[SLOT_COUNT];
    [SerializeField] TextMeshProUGUI remainingCountText;
    [SerializeField] TextMeshProUGUI noResultsText;

    internal event Action<string> QueryChanged;
    internal event Action<string> QuerySubmitted;
    internal event Action<int> SlotSelected;
    internal event Action<int> RouteClicked;
    internal event Action InputFocused;
    internal event Action InputUnfocused;

    internal bool IsInputFocused => inputField != null && inputField.isFocused;
    internal bool IsResultOpen => resultRoot != null && resultRoot.activeSelf;

    void Awake()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onSubmit.AddListener(OnInputSubmitted);
            inputField.onSelect.AddListener(OnInputSelected);
            inputField.onDeselect.AddListener(OnInputDeselected);

            if (inputField.placeholder is TextMeshProUGUI placeholder)
            {
                placeholder.text = Presentation.SearchPlaceholder;
            }
        }

        if (noResultsText != null)
        {
            noResultsText.text = Presentation.SearchNoResults;
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                if (slots[i].selectButton != null)
                {
                    slots[i].selectButton.onClick.AddListener(() => SlotSelected?.Invoke(index));
                }
                if (slots[i].routeButton != null)
                {
                    slots[i].routeButton.onClick.AddListener(() => RouteClicked?.Invoke(index));
                }
                if (slots[i].routeButtonLabel != null)
                {
                    slots[i].routeButtonLabel.text = Presentation.SearchRouteButton;
                }
            }
        }

        HideResults();
    }

    void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnInputValueChanged);
            inputField.onSubmit.RemoveListener(OnInputSubmitted);
            inputField.onSelect.RemoveListener(OnInputSelected);
            inputField.onDeselect.RemoveListener(OnInputDeselected);
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].selectButton != null)
                {
                    slots[i].selectButton.onClick.RemoveAllListeners();
                }
                if (slots[i].routeButton != null)
                {
                    slots[i].routeButton.onClick.RemoveAllListeners();
                }
            }
        }
    }

    public void ShowResults(IReadOnlyList<SearchEntry> _matches, int _total, string _currentCityId)
    {
        if (resultRoot != null)
        {
            resultRoot.SetActive(true);
        }

        bool hasMatches = _matches != null && _matches.Count > 0;
        if (noResultsText != null)
        {
            noResultsText.gameObject.SetActive(!hasMatches);
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                bool active = hasMatches && i < _matches.Count;
                SetSlotActive(i, active);

                if (active)
                {
                    var entry = _matches[i];
                    if (slots[i].nameText != null)
                    {
                        slots[i].nameText.text = entry.name;
                    }
                    if (slots[i].kindText != null)
                    {
                        slots[i].kindText.text = Presentation.SearchKind(entry.kind);
                    }

                    bool showRoute = entry.kind == EntityLinkKind.City &&
                        !string.Equals(entry.id, _currentCityId, StringComparison.Ordinal);

                    if (slots[i].routeButton != null)
                    {
                        slots[i].routeButton.gameObject.SetActive(showRoute);
                    }
                    if (showRoute && slots[i].routeButtonLabel != null)
                    {
                        slots[i].routeButtonLabel.text = Presentation.SearchRouteButton;
                    }
                }
            }
        }

        int remaining = hasMatches ? _total - _matches.Count : 0;
        if (remainingCountText != null)
        {
            remainingCountText.gameObject.SetActive(remaining > 0);
            if (remaining > 0)
            {
                remainingCountText.text = Presentation.SearchRemainingCount(remaining);
            }
        }
    }

    public void HideResults()
    {
        if (resultRoot != null)
        {
            resultRoot.SetActive(false);
        }
        if (noResultsText != null)
        {
            noResultsText.gameObject.SetActive(false);
        }
        if (remainingCountText != null)
        {
            remainingCountText.gameObject.SetActive(false);
        }
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                SetSlotActive(i, false);
            }
        }
    }

    public void ClearAndClose()
    {
        if (inputField != null)
        {
            inputField.text = string.Empty;
        }
        HideResults();
        ClearFocus();
    }

    public void ClearFocus()
    {
        if (inputField != null)
        {
            inputField.DeactivateInputField();
        }
        if (EventSystem.current != null && inputField != null &&
            EventSystem.current.currentSelectedGameObject == inputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void SetSlotActive(int _index, bool _active)
    {
        if (_index < 0 || slots == null || _index >= slots.Length)
        {
            return;
        }

        if (slots[_index].root != null)
        {
            slots[_index].root.SetActive(_active);
        }
        else if (slots[_index].selectButton != null)
        {
            slots[_index].selectButton.gameObject.SetActive(_active);
        }
    }

    void OnInputValueChanged(string _text)
    {
        QueryChanged?.Invoke(_text);
    }

    void OnInputSubmitted(string _text)
    {
        QuerySubmitted?.Invoke(_text);
    }

    void OnInputSelected(string _text)
    {
        InputFocused?.Invoke();
    }

    void OnInputDeselected(string _text)
    {
        InputUnfocused?.Invoke();
    }
}
