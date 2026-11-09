using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleTrainBarView : MonoBehaviour
{
    const float WAGON_WIDTH = 150f;
    const float WAGON_HEIGHT = 60f;
    const float WAGON_SPACING = 50f;

    [Serializable]
    public sealed class WagonSlotUI
    {
        public GameObject root;
        public RectTransform rectTransform;
        public Button button;
        public Image wagonImage;
        public Image highlightBorder;
        public TextMeshProUGUI numberText;
    }

    [Header("Slots Pool")]
    [SerializeField] WagonSlotUI[] wagonSlots;

    [Header("Add Wagon Button")]
    [SerializeField] Button addWagonButton;
    [SerializeField] RectTransform addWagonRect;
    [SerializeField] Image addWagonBox;
    [SerializeField] TextMeshProUGUI addWagonText;

    [Header("Layout Settings")]
    [SerializeField] float originX = 350f;
    [SerializeField] float originY = 0f;

    public event Action<int> WagonSelected;
    public event Action AddWagonClicked;

    void Awake()
    {
        if (addWagonButton)
        {
            addWagonButton.onClick.AddListener(OnAddWagonClicked);
        }

        if (wagonSlots != null)
        {
            for (int i = 0; i < wagonSlots.Length; i++)
            {
                int index = i;
                if (wagonSlots[i]?.button != null)
                {
                    wagonSlots[i].button.onClick.AddListener(() => OnWagonSelected(index));
                }
            }
        }
    }

    void OnDestroy()
    {
        if (addWagonButton)
        {
            addWagonButton.onClick.RemoveListener(OnAddWagonClicked);
        }

        if (wagonSlots != null)
        {
            for (int i = 0; i < wagonSlots.Length; i++)
            {
                if (wagonSlots[i]?.button != null)
                {
                    wagonSlots[i].button.onClick.RemoveAllListeners();
                }
            }
        }
    }

    void OnWagonSelected(int _index)
    {
        WagonSelected?.Invoke(_index);
    }

    void OnAddWagonClicked()
    {
        AddWagonClicked?.Invoke();
    }

    public void Refresh(int _wagonCount, int _selectedIndex, long _nextCost, bool _canAfford)
    {
        if (wagonSlots == null)
        {
            return;
        }

        float step = WAGON_WIDTH + WAGON_SPACING;

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot == null || slot.root == null)
            {
                continue;
            }

            bool active = i < _wagonCount;
            slot.root.SetActive(active);

            if (active)
            {
                float x = originX - (i * step);
                if (slot.rectTransform != null)
                {
                    slot.rectTransform.anchoredPosition = new Vector2(x, originY);
                    slot.rectTransform.sizeDelta = new Vector2(WAGON_WIDTH, WAGON_HEIGHT);
                }

                if (slot.numberText != null)
                {
                    slot.numberText.text = (i + 1).ToString();
                }

                if (slot.highlightBorder != null)
                {
                    slot.highlightBorder.enabled = (i == _selectedIndex);
                    slot.highlightBorder.color = Presentation.PuzzleWagonSelectedBorder;
                }
            }
        }

        if (addWagonButton != null && addWagonRect != null)
        {
            float addX = originX - (_wagonCount * step);
            addWagonRect.anchoredPosition = new Vector2(addX, originY);
            addWagonRect.sizeDelta = new Vector2(WAGON_WIDTH, WAGON_HEIGHT);

            if (addWagonBox != null)
            {
                addWagonBox.color = _canAfford ? Presentation.PuzzleAddWagonBox : new Color(0.18f, 0.55f, 0.28f, 0.5f);
            }

            if (addWagonText != null)
            {
                addWagonText.text = Presentation.PuzzleAddWagonLabel(_nextCost);
                addWagonText.color = _canAfford ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.6f);
            }

            addWagonButton.interactable = _canAfford;
        }
    }
}
