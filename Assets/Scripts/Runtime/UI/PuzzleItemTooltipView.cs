using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleItemTooltipView : PanelBase
{
    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform panelRect;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform pointer;
    [SerializeField] TextMeshProUGUI itemNameText;
    [SerializeField] RectTransform categoryRow;
    [SerializeField] Image[] categoryBadgeImages;
    [SerializeField] TextMeshProUGUI[] categoryBadgeTexts;
    [System.Serializable]
    struct ActiveEffectRow
    {
        public TextMeshProUGUI labelText;
        public TextMeshProUGUI valueText;
    }

    [SerializeField] RectTransform bodyRect;
    [SerializeField] TextMeshProUGUI baseValueLabelText;
    [SerializeField] TextMeshProUGUI baseValueValueText;
    [SerializeField] RectTransform activeEffectsSection;
    [SerializeField] TextMeshProUGUI activeEffectsText;
    [SerializeField] TextMeshProUGUI activeEffectsMessageText;
    [SerializeField] RectTransform activeEffectsListRoot;
    [SerializeField] ActiveEffectRow[] activeEffectRows;
    [SerializeField] TextMeshProUGUI actualValueLabelText;
    [SerializeField] TextMeshProUGUI actualValueValueText;
    [SerializeField] TextMeshProUGUI sellRateLabelText;
    [SerializeField] TextMeshProUGUI sellRateValueText;
    [SerializeField] TextMeshProUGUI buyRateLabelText;
    [SerializeField] TextMeshProUGUI buyRateValueText;
    [SerializeField] GameObject generalEffectSection;
    [SerializeField] TextMeshProUGUI generalEffectHeaderText;
    [SerializeField] RectTransform generalEffectListRoot;
    [SerializeField] TextMeshProUGUI[] generalEffectSlots;
    [SerializeField] GameObject synergySection;
    [SerializeField] TextMeshProUGUI synergyHeaderText;
    [SerializeField] RectTransform synergyListRoot;
    [SerializeField] TextMeshProUGUI[] synergySlots;
    [SerializeField] TextMeshProUGUI futureValueLabelText;
    [SerializeField] TextMeshProUGUI futureValueValueText;
    [SerializeField] TextMeshProUGUI freshnessLabelText;
    [SerializeField] TextMeshProUGUI freshnessValueText;
    [SerializeField] TextMeshProUGUI shelfLifeLabelText;
    [SerializeField] TextMeshProUGUI shelfLifeValueText;

    [Header("Mini Shape Grid")]
    [SerializeField] GameObject miniGridRoot;
    [SerializeField] Image[] miniGridCells;
    [SerializeField] Color miniGridFilledColor = new Color(0.961f, 0.875f, 0.753f, 1f);
    [SerializeField] Color miniGridEmptyColor = new Color(0.17f, 0.21f, 0.26f, 0.8f);

    [Header("Layout & Movement")]
    [SerializeField] float defaultFollowSpeed = 25f;
    [SerializeField, Range(0f, 1f)] float badgeBlend = 0.35f;
    [SerializeField] float pointerOffset = 24f;
    [SerializeField] float pointerTopInset = 32f;
    [SerializeField] float pointerEdgeInset = 20f;
    [SerializeField] float screenMargin = 20f;
    [SerializeField] float screenBottomMargin = 228f;

    float FollowSpeed => defaultFollowSpeed;

    void OnValidate()
    {
        badgeBlend = Mathf.Clamp01(badgeBlend);
    }

    RectTransform canvasRectTransform;
    Vector2 targetCanvasPosition;
    bool pointerFlipped;
    bool needsSnap;

    protected override void Awake()
    {
        base.Awake();
        EnsureInitialized();
    }

    void EnsureInitialized()
    {
        if (!canvas)
        {
            canvas = GetComponentInParent<Canvas>(true);
        }
        if (canvas && !canvasRectTransform)
        {
            canvasRectTransform = canvas.transform as RectTransform;
        }
        if (!panelRect)
        {
            panelRect = transform as RectTransform;
        }
        if (!canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (canvasGroup)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<string> _generalEffects, IReadOnlyList<string> _synergyEffects, decimal? _futureValue, Vector2 _screenPosition,
        bool _isTrainInventory, int? _freshness, long? _minutesUntilNextFreshnessBand, long? _previewMinutes, string _previewCityName,
        long? _previewActivations, long? _tradePrice, IReadOnlyList<PuzzleSynergyBonus> _activeEffects, int _appliedPreservation,
        int _clockwiseTurns = 0)
    {
        if (_item == null)
        {
            Close();
            return;
        }

        EnsureInitialized();
        needsSnap = true;
        SetOpen(true);

        if (panelRect)
        {
            panelRect.SetAsLastSibling();
        }

        if (itemNameText)
        {
            itemNameText.text = _item.DisplayName;
        }

        UpdateCategoryBadges(_item);
        UpdateMiniGrid(_item, _clockwiseTurns);

        SetRow(baseValueLabelText, baseValueValueText,
            Presentation.PuzzleTooltipBaseValueLabel, Presentation.PuzzleTooltipActualValueValue(_item.BaseValue));
        UpdateActiveEffects(_isTrainInventory, _activeEffects, _appliedPreservation);
        SetRow(actualValueLabelText, actualValueValueText,
            Presentation.PuzzleTooltipActualValueLabel(), Presentation.PuzzleTooltipActualValueValue(_actualValue));

        if (_isTrainInventory)
        {
            int sellPercent = _rate.SellPercent;
            string price = _tradePrice.HasValue
                ? Presentation.PuzzleTooltipFinalSellPriceValue(_tradePrice.Value, _actualValue)
                : Presentation.PuzzleTooltipPriceUnavailable;
            SetRow(sellRateLabelText, sellRateValueText,
                Presentation.PuzzleTooltipSellRateLabel(), Presentation.PuzzleTooltipSellRateValue(sellPercent));
            SetRow(buyRateLabelText, buyRateValueText,
                Presentation.PuzzleTooltipFinalSellPriceLabel(), price);
        }
        else
        {
            int buyPercent = _rate.BuyPercent;
            string price = _tradePrice.HasValue
                ? Presentation.PuzzleTooltipFinalBuyPriceValue(_tradePrice.Value, _actualValue)
                : Presentation.PuzzleTooltipPriceUnavailable;
            SetRow(sellRateLabelText, sellRateValueText,
                Presentation.PuzzleTooltipBuyRateLabel(), Presentation.PuzzleTooltipBuyRateValue(buyPercent));
            SetRow(buyRateLabelText, buyRateValueText,
                Presentation.PuzzleTooltipFinalBuyPriceLabel(), price);
        }

        UpdateEffectSection(generalEffectSection, generalEffectHeaderText, generalEffectSlots, _generalEffects,
            Presentation.PuzzleTooltipGeneralEffectHeader(), _showEmpty: (_synergyEffects?.Count ?? 0) == 0);
        UpdateEffectSection(synergySection, synergyHeaderText, synergySlots, _synergyEffects,
            Presentation.PuzzleTooltipValueSynergyHeader(), _showEmpty: false);

        if (futureValueLabelText || futureValueValueText)
        {
            if (_isTrainInventory && _futureValue.HasValue && _previewMinutes.HasValue && _previewActivations.HasValue)
            {
                SetRow(futureValueLabelText, futureValueValueText,
                    Presentation.PuzzleTooltipFutureValueLabel(_previewMinutes.Value, _previewCityName, _previewActivations.Value),
                    Presentation.PuzzleTooltipFutureValueValue(_futureValue.Value, _actualValue));
                SetRowActive(futureValueLabelText, futureValueValueText, true);
            }
            else
            {
                SetRowActive(futureValueLabelText, futureValueValueText, false);
            }
        }

        if (freshnessLabelText || freshnessValueText)
        {
            if (_freshness.HasValue)
            {
                SetRow(freshnessLabelText, freshnessValueText,
                    Presentation.PuzzleTooltipFreshnessLabel(), Presentation.PuzzleTooltipFreshnessValue(_freshness.Value));
                SetRowActive(freshnessLabelText, freshnessValueText, true);
            }
            else
            {
                SetRowActive(freshnessLabelText, freshnessValueText, false);
            }
        }

        if (_minutesUntilNextFreshnessBand is long remaining && _freshness is int freshness)
        {
            SetRow(shelfLifeLabelText, shelfLifeValueText,
                Presentation.PuzzleTooltipShelfLifeLabel(), Presentation.PuzzleTooltipShelfLifeValue(remaining, freshness));
        }
        SetRowActive(shelfLifeLabelText, shelfLifeValueText, _minutesUntilNextFreshnessBand.HasValue && _freshness.HasValue);

        RebuildLayouts();
        SetPointerPosition(_screenPosition);
    }

    internal void ShowKeyword(string _title, string _description, Vector2 _screenPosition)
    {
        EnsureInitialized();
        needsSnap = true;
        SetOpen(true);

        if (panelRect)
        {
            panelRect.SetAsLastSibling();
        }

        if (itemNameText)
        {
            itemNameText.text = _title ?? string.Empty;
        }

        if (categoryRow)
        {
            categoryRow.gameObject.SetActive(false);
        }

        UpdateMiniGrid(null, 0);

        SetRowActive(baseValueLabelText, baseValueValueText, false);
        if (activeEffectsSection)
        {
            activeEffectsSection.gameObject.SetActive(false);
        }
        SetRowActive(actualValueLabelText, actualValueValueText, false);
        SetRowActive(sellRateLabelText, sellRateValueText, false);
        SetRowActive(buyRateLabelText, buyRateValueText, false);
        if (synergySection)
        {
            synergySection.SetActive(false);
        }
        SetRowActive(futureValueLabelText, futureValueValueText, false);
        SetRowActive(freshnessLabelText, freshnessValueText, false);
        SetRowActive(shelfLifeLabelText, shelfLifeValueText, false);

        if (generalEffectSection)
        {
            bool hasDescription = !string.IsNullOrEmpty(_description);
            generalEffectSection.SetActive(hasDescription);
            SetSectionBottomRuleActive(generalEffectSection, false);
            if (hasDescription)
            {
                if (generalEffectHeaderText)
                {
                    generalEffectHeaderText.gameObject.SetActive(false);
                }
                if (generalEffectSlots != null)
                {
                    for (int i = 0; i < generalEffectSlots.Length; i++)
                    {
                        var slot = generalEffectSlots[i];
                        if (!slot)
                        {
                            continue;
                        }
                        bool used = i == 0;
                        slot.gameObject.SetActive(used);
                        if (used)
                        {
                            slot.text = _description;
                        }
                    }
                }
            }
        }

        RebuildLayouts();
        SetPointerPosition(_screenPosition);
    }

    static void SetSectionBottomRuleActive(GameObject _section, bool _active)
    {
        if (!_section)
        {
            return;
        }

        var bottomRule = _section.transform.Find("Bottom Rule");
        if (bottomRule)
        {
            bottomRule.gameObject.SetActive(_active);
        }
    }

    static void UpdateEffectSection(GameObject _section, TextMeshProUGUI _header, TextMeshProUGUI[] _slots,
        IReadOnlyList<string> _effects, string _title, bool _showEmpty)
    {
        if (!_section)
        {
            return;
        }

        int effectCount = _effects?.Count ?? 0;
        bool visible = effectCount > 0 || _showEmpty;
        _section.SetActive(visible);
        if (!visible)
        {
            return;
        }

        SetSectionBottomRuleActive(_section, true);
        if (_header)
        {
            _header.gameObject.SetActive(true);
            _header.text = _title;
        }
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            bool used = i < effectCount || (effectCount == 0 && i == 0);
            slot.gameObject.SetActive(used);
            if (used)
            {
                slot.text = effectCount == 0
                    ? Presentation.PuzzleTooltipNoEffect()
                    : i == _slots.Length - 1 && effectCount > _slots.Length
                        ? string.Join("\n", _effects.Skip(i))
                        : _effects[i];
            }
        }
    }

    void UpdateCategoryBadges(PuzzleItemDefinition _item)
    {
        if (!categoryRow)
        {
            return;
        }

        var tags = _item != null ? Presentation.PrioritizeTags(_item.Tags) : System.Array.Empty<string>();
        bool hasBadges = tags != null && tags.Count > 0 && categoryBadgeImages != null && categoryBadgeImages.Length > 0;
        categoryRow.gameObject.SetActive(hasBadges);

        if (!hasBadges)
        {
            return;
        }

        for (int i = 0; i < categoryBadgeImages.Length; i++)
        {
            var badgeImage = categoryBadgeImages[i];
            var badgeText = categoryBadgeTexts != null && i < categoryBadgeTexts.Length ? categoryBadgeTexts[i] : null;

            if (i < tags.Count)
            {
                string tag = tags[i];
                if (badgeImage)
                {
                    badgeImage.gameObject.SetActive(true);
                    badgeImage.color = Presentation.PuzzleTooltipTagBackground(tag, badgeBlend);
                }
                if (badgeText)
                {
                    badgeText.text = Presentation.PuzzleTagLabel(tag);
                    badgeText.color = Presentation.PuzzleTooltipTagTextColor(tag, badgeBlend);
                }
            }
            else
            {
                if (badgeImage)
                {
                    badgeImage.gameObject.SetActive(false);
                }
            }
        }
    }

    void UpdateMiniGrid(PuzzleItemDefinition _item, int _clockwiseTurns)
    {
        if (!miniGridRoot)
        {
            return;
        }

        if (_item == null || _item.Cells == null || _item.Cells.Count == 0 || miniGridCells == null || miniGridCells.Length != 25)
        {
            miniGridRoot.SetActive(false);
            return;
        }

        miniGridRoot.SetActive(true);

        int turns = ((_clockwiseTurns % 4) + 4) % 4;

        var rotatedCells = new Vector2Int[_item.Cells.Count];
        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        for (int i = 0; i < _item.Cells.Count; i++)
        {
            var cell = _item.Cells[i];
            var rotated = turns switch
            {
                0 => cell,
                1 => new Vector2Int(-cell.y, cell.x),
                2 => new Vector2Int(-cell.x, -cell.y),
                3 => new Vector2Int(cell.y, -cell.x),
                _ => cell
            };
            rotatedCells[i] = rotated;
            if (rotated.x < minX) minX = rotated.x;
            if (rotated.x > maxX) maxX = rotated.x;
            if (rotated.y < minY) minY = rotated.y;
            if (rotated.y > maxY) maxY = rotated.y;
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        int offsetX = Mathf.Max(0, (5 - width) / 2);
        int offsetY = Mathf.Max(0, (5 - height) / 2);

        bool[] occupied = new bool[25];
        for (int i = 0; i < rotatedCells.Length; i++)
        {
            int col = offsetX + (rotatedCells[i].x - minX);
            int row = offsetY + (rotatedCells[i].y - minY);
            if (col >= 0 && col < 5 && row >= 0 && row < 5)
            {
                occupied[row * 5 + col] = true;
            }
        }

        Color filledColor = miniGridFilledColor;
        Color emptyColor = miniGridEmptyColor;

        for (int i = 0; i < 25; i++)
        {
            if (miniGridCells[i] != null)
            {
                miniGridCells[i].color = occupied[i] ? filledColor : emptyColor;
            }
        }
    }

    void RebuildLayouts()
    {
        Canvas.ForceUpdateCanvases();
        if (categoryRow && categoryRow.gameObject.activeSelf)
        {
            if (categoryBadgeImages != null)
            {
                foreach (var badge in categoryBadgeImages)
                {
                    if (badge && badge.gameObject.activeSelf)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(badge.rectTransform);
                    }
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(categoryRow);
        }
        if (miniGridRoot && miniGridRoot.activeSelf)
        {
            if (miniGridRoot.transform is RectTransform miniGridRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(miniGridRect);
            }
        }
        if (activeEffectsListRoot && activeEffectsListRoot.gameObject.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeEffectsListRoot);
        }
        if (activeEffectsSection)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeEffectsSection);
        }
        RebuildEffectLayout(generalEffectSection, generalEffectListRoot);
        RebuildEffectLayout(synergySection, synergyListRoot);
        if (bodyRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);
        }
        if (itemNameText && itemNameText.transform.parent is RectTransform headerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(headerRect);
        }
        if (panelRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }
    }

    static void RebuildEffectLayout(GameObject _section, RectTransform _list)
    {
        if (_section && _section.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_list);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_section.transform);
        }
    }

    public void SetPointerPosition(Vector2 _screenPosition)
    {
        EnsureInitialized();
        if (!canvasRectTransform || !panelRect)
        {
            return;
        }

        Camera cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, _screenPosition, cam, out Vector2 localPoint))
        {
            return;
        }

        float width = panelRect.rect.width;
        float height = panelRect.rect.height;

        float offset = pointerOffset;
        float posX = localPoint.x + offset;
        float posY = localPoint.y + pointerTopInset;

        Rect cRect = canvasRectTransform.rect;
        float margin = screenMargin;
        float bottomMargin = Mathf.Max(screenMargin, screenBottomMargin);
        cRect = Rect.MinMaxRect(cRect.xMin + margin, cRect.yMin + bottomMargin, cRect.xMax - margin, cRect.yMax - margin);

        // 화면 우측을 벗어나면 마우스 좌측으로 플립
        bool flipped = posX + width > cRect.xMax;
        if (flipped)
        {
            posX = localPoint.x - offset - width;
        }
        posX = Mathf.Clamp(posX, cRect.xMin, Mathf.Max(cRect.xMin, cRect.xMax - width));

        posY = Mathf.Clamp(posY, Mathf.Min(cRect.yMax, cRect.yMin + height), cRect.yMax);

        if (pointer)
        {
            needsSnap |= pointerFlipped != flipped;
            pointerFlipped = flipped;
            float edge = flipped ? 1f : 0f;
            pointer.anchorMin = pointer.anchorMax = new Vector2(edge, 1f);
            pointer.localScale = new Vector3(flipped ? -1f : 1f, 1f, 1f);
            pointer.anchoredPosition = new Vector2(0f,
                Mathf.Clamp(localPoint.y - posY, -height + pointerEdgeInset, -pointerEdgeInset));
        }

        targetCanvasPosition = new Vector2(posX, posY);

        if (needsSnap)
        {
            panelRect.anchoredPosition = targetCanvasPosition;
            needsSnap = false;
        }
    }

    void Update()
    {
        if (!IsOpen || !panelRect)
        {
            return;
        }

        panelRect.anchoredPosition = Vector2.Lerp(panelRect.anchoredPosition, targetCanvasPosition, Time.unscaledDeltaTime * FollowSpeed);
    }

    void UpdateActiveEffects(bool _isTrainInventory, IReadOnlyList<PuzzleSynergyBonus> _activeEffects, int _appliedPreservation)
    {
        int valueCount = _isTrainInventory ? _activeEffects?.Count ?? 0 : 0;
        bool hasPreservation = _isTrainInventory && _appliedPreservation > 0;
        int activeCount = valueCount + (hasPreservation ? 1 : 0);
        string preservation = hasPreservation ? Presentation.PuzzleAppliedPreservationEffect(_appliedPreservation) : null;
        int rowCount = activeEffectRows?.Length ?? 0;
        bool useRows = activeEffectsListRoot && activeCount > 0 && activeCount <= rowCount;
        if (activeEffectsText)
        {
            activeEffectsText.text = valueCount > 0 && !hasPreservation
                ? Presentation.PuzzleTooltipActiveEffectsHeader()
                : Presentation.PuzzleTooltipActiveEffectsTitle;
        }
        if (activeEffectsMessageText)
        {
            activeEffectsMessageText.gameObject.SetActive(!useRows);
            if (!useRows)
            {
                activeEffectsMessageText.color = Presentation.PuzzleTooltipText;
                string valueSummary = valueCount > 0 ? Presentation.PuzzleTooltipActiveEffectsSummary(_activeEffects) : null;
                activeEffectsMessageText.text = activeCount > 0
                    ? string.Join("\n", new[] { valueSummary, preservation }.Where(_line => !string.IsNullOrEmpty(_line)))
                    : _isTrainInventory ? Presentation.PuzzleNoActiveEffects : Presentation.PuzzleMarketNoSettlement;
            }
        }

        if (activeEffectsListRoot)
        {
            activeEffectsListRoot.gameObject.SetActive(useRows);
        }

        if (!useRows)
        {
            return;
        }

        for (int i = 0; i < rowCount; i++)
        {
            ActiveEffectRow row = activeEffectRows[i];
            bool active = i < activeCount;
            if (i < valueCount)
            {
                SetRow(row.labelText, row.valueText,
                    Presentation.PuzzleTooltipActiveEffectLabel(_activeEffects[i]),
                    Presentation.PuzzleTooltipActiveEffectValue(_activeEffects[i]));
            }
            else if (active)
            {
                SetRow(row.labelText, row.valueText, preservation, string.Empty);
            }
            SetRowActive(row.labelText, row.valueText, active);
        }
    }

    void SetRow(TextMeshProUGUI _label, TextMeshProUGUI _value, string _labelText, string _valueText)
    {
        SetRowActive(_label, _value, true);
        if (_label)
        {
            _label.text = _labelText;
        }
        if (_value)
        {
            _value.text = _valueText;
        }
    }

    void SetRowActive(TextMeshProUGUI _label, TextMeshProUGUI _value, bool _active)
    {
        Transform row = _label ? _label.transform.parent : _value ? _value.transform.parent : null;
        if (!row)
        {
            return;
        }

        row.gameObject.SetActive(_active);
        if (_active)
        {
            if (_label)
            {
                _label.gameObject.SetActive(true);
            }
            if (_value)
            {
                _value.gameObject.SetActive(true);
            }
        }
    }

}
