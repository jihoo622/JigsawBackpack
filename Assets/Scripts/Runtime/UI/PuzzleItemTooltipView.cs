using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleItemTooltipView : PanelBase
{
    static float DefaultFollowSpeed => BasicData.Float("ui.PuzzleItemTooltipView.defaultFollowSpeed");

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
    float FollowSpeed => DefaultFollowSpeed;

    RectTransform canvasRectTransform;
    Vector2 targetCanvasPosition;
    bool pointerFlipped;
    bool needsSnap;
    readonly List<ActiveEffectRow> activeRowPool = new();

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
            if (!canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (activeRowPool.Count == 0 && activeEffectRows != null)
        {
            for (int i = 0; i < activeEffectRows.Length; i++)
            {
                if (activeEffectRows[i].labelText || activeEffectRows[i].valueText)
                {
                    activeRowPool.Add(activeEffectRows[i]);
                }
            }
        }
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<string> _effects, decimal? _futureValue, Vector2 _screenPosition,
        bool _isTrainInventory, int? _freshness, long? _remainingShelfLifeMinutes, long? _previewMinutes, string _previewCityName,
        long? _previewActivations, long? _tradePrice, IReadOnlyList<PuzzleSynergyBonus> _activeEffects)
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

        SetRow(baseValueLabelText, baseValueValueText,
            Presentation.PuzzleTooltipBaseValueLabel, Presentation.PuzzleTooltipActualValueValue(_item.BaseValue));
        UpdateActiveEffects(_isTrainInventory, _activeEffects);
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
                ? Presentation.PuzzleTooltipFinalBuyPriceValue(_tradePrice.Value, _item.BaseValue)
                : Presentation.PuzzleTooltipPriceUnavailable;
            SetRow(sellRateLabelText, sellRateValueText,
                Presentation.PuzzleTooltipBuyRateLabel(), Presentation.PuzzleTooltipBuyRateValue(buyPercent));
            SetRow(buyRateLabelText, buyRateValueText,
                Presentation.PuzzleTooltipFinalBuyPriceLabel(), price);
        }

        if (synergySection)
        {
            synergySection.SetActive(true);
        }

        if (synergyHeaderText)
        {
            synergyHeaderText.text = Presentation.PuzzleTooltipEffectHeader();
        }

        if (synergySlots != null && synergySlots.Length > 0)
        {
            int effectCount = _effects?.Count ?? 0;
            if (effectCount == 0)
            {
                synergySlots[0].text = Presentation.PuzzleTooltipNoEffect();
                synergySlots[0].gameObject.SetActive(true);
                for (int i = 1; i < synergySlots.Length; i++)
                {
                    synergySlots[i].gameObject.SetActive(false);
                }
            }
            else
            {
                for (int i = 0; i < synergySlots.Length; i++)
                {
                    if (i < effectCount)
                    {
                        synergySlots[i].text = Presentation.PuzzleTooltipEffectDescription(_effects[i]);
                        synergySlots[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        synergySlots[i].gameObject.SetActive(false);
                    }
                }
            }
        }

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

        if (_remainingShelfLifeMinutes is long remaining && _freshness is int freshness)
        {
            SetRow(shelfLifeLabelText, shelfLifeValueText,
                Presentation.PuzzleTooltipShelfLifeLabel(), Presentation.PuzzleTooltipShelfLifeValue(remaining, freshness));
        }
        SetRowActive(shelfLifeLabelText, shelfLifeValueText, _remainingShelfLifeMinutes.HasValue && _freshness.HasValue);

        RebuildLayouts();
        SetPointerPosition(_screenPosition);
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
                    badgeImage.color = Color.Lerp(Presentation.PuzzleTagBackgroundColor(tag),
                        Presentation.PuzzleTooltipBodyBackground, BasicData.Float("ui.tooltip.badgeBlend"));
                }
                if (badgeText)
                {
                    badgeText.text = Presentation.PuzzleTagLabel(tag);
                    badgeText.color = Presentation.PuzzleTooltipText;
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
        if (activeEffectsListRoot && activeEffectsListRoot.gameObject.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeEffectsListRoot);
        }
        if (activeEffectsSection)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeEffectsSection);
        }
        if (synergyListRoot)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(synergyListRoot);
        }
        if (synergySection && synergySection.transform is RectTransform synRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(synRect);
        }
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

        float offset = BasicData.Float("ui.layout.tooltipPointerOffset");
        float posX = localPoint.x + offset;
        float pointerTopInset = BasicData.Float("ui.tooltip.pointerTopInset");
        float posY = localPoint.y + pointerTopInset;

        Rect cRect = canvasRectTransform.rect;
        float margin = BasicData.Float("ui.layout.tooltipScreenMargin");
        cRect = Rect.MinMaxRect(cRect.xMin + margin, cRect.yMin + margin, cRect.xMax - margin, cRect.yMax - margin);

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
            float edgeInset = BasicData.Float("ui.tooltip.pointerEdgeInset");
            pointer.anchoredPosition = new Vector2(0f,
                Mathf.Clamp(localPoint.y - posY, -height + edgeInset, -edgeInset));
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

    void UpdateActiveEffects(bool _isTrainInventory, IReadOnlyList<PuzzleSynergyBonus> _activeEffects)
    {
        int activeCount = _isTrainInventory && _activeEffects != null ? _activeEffects.Count : 0;
        if (activeEffectsText)
        {
            activeEffectsText.text = activeCount > 0
                ? Presentation.PuzzleTooltipActiveEffectsHeader()
                : Presentation.PuzzleTooltipActiveEffectsTitle;
        }
        if (activeEffectsMessageText)
        {
            activeEffectsMessageText.gameObject.SetActive(activeCount == 0);
            if (activeCount == 0)
            {
                activeEffectsMessageText.color = Presentation.PuzzleTooltipText;
                activeEffectsMessageText.text = _isTrainInventory
                    ? Presentation.PuzzleNoActiveEffects
                    : Presentation.PuzzleMarketNoSettlement;
            }
        }

        if (!activeEffectsListRoot)
        {
            return;
        }

        while (activeRowPool.Count < activeCount && activeRowPool.Count > 0)
        {
            ActiveEffectRow templateRow = activeRowPool[0];
            Transform templateTransform = templateRow.labelText
                ? templateRow.labelText.transform.parent
                : templateRow.valueText ? templateRow.valueText.transform.parent : null;
            if (!templateTransform)
            {
                break;
            }
            GameObject clone = Instantiate(templateTransform.gameObject, activeEffectsListRoot);
            clone.name = $"Row_{activeRowPool.Count}";
            TextMeshProUGUI[] texts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI label = clone.transform.Find("Label")?.GetComponent<TextMeshProUGUI>() ?? (texts.Length > 0 ? texts[0] : null);
            TextMeshProUGUI value = clone.transform.Find("Value")?.GetComponent<TextMeshProUGUI>() ?? (texts.Length > 1 ? texts[1] : null);
            activeRowPool.Add(new ActiveEffectRow
            {
                labelText = label,
                valueText = value
            });
        }

        for (int i = 0; i < activeRowPool.Count; i++)
        {
            ActiveEffectRow row = activeRowPool[i];
            bool active = i < activeCount;
            if (active)
            {
                SetRow(row.labelText, row.valueText,
                    Presentation.PuzzleTooltipActiveEffectLabel(_activeEffects[i]),
                    Presentation.PuzzleTooltipActiveEffectValue(_activeEffects[i]));
            }
            SetRowActive(row.labelText, row.valueText, active);
        }
        activeEffectsListRoot.gameObject.SetActive(activeCount > 0);
    }

    void SetRow(TextMeshProUGUI _label, TextMeshProUGUI _value, string _labelText, string _valueText)
    {
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
