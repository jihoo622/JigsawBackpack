using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PuzzleTrainBarView : MonoBehaviour
{
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
    [SerializeField] bool fitRotatedWagonArtwork;

    [Header("Add Wagon Button")]
    [SerializeField] Button addWagonButton;
    [SerializeField] RectTransform addWagonRect;
    [SerializeField] Image addWagonBox;
    [SerializeField] TextMeshProUGUI addWagonText;

    [Header("Layout Settings")]
    [SerializeField] float wagonWidth = 150f;
    [SerializeField] float wagonHeight = 56f;
    [SerializeField] float wagonSpacing = 50f;
    [SerializeField] float wagonDragHoverScale = 1.08f;
    [SerializeField] float originX = 0f;
    [SerializeField] float originY = 0f;

    int visibleWagonCount = -1;

    public event Action<int> WagonSelected;
    public event Action AddWagonClicked;

    void Awake()
    {
        int maxWagonCount = TableManager.Instance?.PuzzleTrade?.maxWagonCount ?? 0;
        if (maxWagonCount > 0 && (wagonSlots == null || wagonSlots.Length < maxWagonCount))
        {
            throw new InvalidOperationException($"객차 바 슬롯 수({wagonSlots?.Length ?? 0})가 최대 객차 수({maxWagonCount})보다 적습니다.");
        }

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

        int maxWagonCount = TableManager.Instance?.PuzzleTrade?.maxWagonCount ?? 0;
        if (maxWagonCount > 0 && wagonSlots.Length < maxWagonCount)
        {
            throw new InvalidOperationException($"객차 바 슬롯 수({wagonSlots.Length})가 최대 객차 수({maxWagonCount})보다 적습니다.");
        }

        visibleWagonCount = Mathf.Min(_wagonCount, wagonSlots.Length);

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
                if (slot.rectTransform != null)
                {
                    slot.rectTransform.localScale = isDragHovered ? Vector3.one * wagonDragHoverScale : Vector3.one;
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

        bool isMaxWagons = maxWagonCount > 0 && _wagonCount >= maxWagonCount;
        bool canAdd = _canAfford && !isMaxWagons;

        if (addWagonButton != null && addWagonRect != null)
        {
            if (addWagonBox != null)
            {
                addWagonBox.color = canAdd ? Presentation.PuzzleAddWagonBox : Presentation.PuzzleAddWagonBoxDisabled;
            }

            if (addWagonText != null)
            {
                addWagonText.text = Presentation.PuzzleAddWagonLabel(_nextCost);
                addWagonText.color = canAdd ? Presentation.PuzzleUnmodifiedTint : Presentation.PuzzleAddWagonTextDisabled;
            }

            addWagonButton.interactable = canAdd;
        }

        LayoutButtons();
    }

    void OnRectTransformDimensionsChange()
    {
        // 상위 레이아웃 그룹이 첫 갱신 뒤에 너비를 지정할 수 있다.
        LayoutButtons();
    }

    void LayoutButtons()
    {
        if (visibleWagonCount < 0)
        {
            return;
        }

        float available = ((RectTransform)transform).rect.width;
        if (available <= 0f)
        {
            return;
        }

        int count = visibleWagonCount + 1;
        float gap = Mathf.Min(wagonSpacing, available / (count * 2f));
        float width = Mathf.Min(wagonWidth, (available - gap * visibleWagonCount) / count);
        float step = width + gap;
        float start = originX - visibleWagonCount * step * 0.5f;
        float y = originY;
        var size = new Vector2(width, wagonHeight);

        for (int i = 0; i < visibleWagonCount; i++)
        {
            var rect = wagonSlots[i]?.rectTransform;
            if (rect)
            {
                rect.anchoredPosition = new Vector2(start + i * step, y);
                rect.sizeDelta = size;
                if (fitRotatedWagonArtwork && wagonSlots[i].wagonImage && wagonSlots[i].wagonImage.sprite)
                {
                    var image = wagonSlots[i].wagonImage;
                    float aspect = image.sprite.rect.width / image.sprite.rect.height;
                    float height = Mathf.Min(size.x, size.y / aspect);
                    image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
                }
            }
        }

        if (addWagonRect)
        {
            addWagonRect.anchoredPosition = new Vector2(start + visibleWagonCount * step, y);
            addWagonRect.sizeDelta = size;
        }
    }
}
