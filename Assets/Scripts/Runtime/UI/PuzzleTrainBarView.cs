using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleTrainBarView : MonoBehaviour
{
    static float WagonWidth => BasicData.Float("ui.PuzzleTrainBarView.WagonWidth");
    static float WagonHeight => BasicData.Float("ui.PuzzleTrainBarView.WagonHeight");
    static float WagonSpacing => BasicData.Float("ui.PuzzleTrainBarView.WagonSpacing");
    static float WagonDragHoverScale => BasicData.Float("ui.PuzzleTrainBarView.WagonDragHoverScale");

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

    float OriginX => BasicData.Float("ui.PuzzleTrainBarView.OriginX");
    float OriginY => BasicData.Float("ui.PuzzleTrainBarView.OriginY");

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

    public bool TryGetWagonAtScreenPosition(Vector2 _screenPosition, out int _wagonIndex)
    {
        _wagonIndex = -1;
        if (wagonSlots == null)
        {
            return false;
        }

        var canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        for (int i = 0; i < wagonSlots.Length; i++)
        {
            var slot = wagonSlots[i];
            if (slot?.root != null && slot.root.activeInHierarchy && slot.rectTransform != null &&
                RectTransformUtility.RectangleContainsScreenPoint(slot.rectTransform, _screenPosition, eventCamera))
            {
                _wagonIndex = i;
                return true;
            }
        }

        return false;
    }

    public void Refresh(int _wagonCount, int _selectedIndex, long _nextCost, bool _canAfford, int? _dragHoveredIndex = null)
    {
        if (wagonSlots == null)
        {
            return;
        }

        int visibleCount = Mathf.Min(_wagonCount, wagonSlots.Length);
        int count = visibleCount + 1;
        float available = ((RectTransform)transform).rect.width;
        float gap = Mathf.Min(WagonSpacing, available / (count * 2f));
        float width = Mathf.Min(WagonWidth, (available - gap * (count - 1)) / count);
        float step = width + gap;
        float start = OriginX - (count - 1) * step * 0.5f;

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
                bool isSelected = i == _selectedIndex;
                bool isDragHovered = _dragHoveredIndex == i;
                float x = start + i * step;
                if (slot.rectTransform != null)
                {
                    slot.rectTransform.anchoredPosition = new Vector2(x, OriginY);
                    slot.rectTransform.sizeDelta = new Vector2(width, WagonHeight);
                    slot.rectTransform.localScale = isDragHovered ? Vector3.one * WagonDragHoverScale : Vector3.one;
                }

                if (slot.numberText != null)
                {
                    slot.numberText.text = (i + 1).ToString();
                }

                if (slot.highlightBorder != null)
                {
                    slot.highlightBorder.enabled = isSelected || isDragHovered;
                    slot.highlightBorder.color = isDragHovered
                        ? Presentation.PuzzleWagonDragHoverBorder
                        : Presentation.PuzzleWagonSelectedBorder;
                }
            }
        }

        if (addWagonButton != null && addWagonRect != null)
        {
            addWagonRect.anchoredPosition = new Vector2(start + visibleCount * step, OriginY);
            addWagonRect.sizeDelta = new Vector2(width, WagonHeight);
            if (addWagonBox != null)
            {
                addWagonBox.color = _canAfford ? Presentation.PuzzleAddWagonBox : Presentation.SharedColor("ui.PuzzleTrainBarView.Vector2.color1");
            }

            if (addWagonText != null)
            {
                addWagonText.text = Presentation.PuzzleAddWagonLabel(_nextCost);
                addWagonText.color = _canAfford ? Presentation.PuzzleUnmodifiedTint : Presentation.SharedColor("ui.PuzzleTrainBarView.Color.color1");
            }

            addWagonButton.interactable = _canAfford;
        }
    }
}
