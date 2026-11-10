using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleItemTooltipView : PanelBase
{
    const float BASE_FONT_SIZE = 18f;
    const float MIN_FONT_SIZE = 18f;
    const float MAX_FONT_SIZE = 24f;
    const float DEFAULT_FOLLOW_SPEED = 25f;

    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform panelRect;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image headerBackground;
    [SerializeField] Image dividerImage;
    [SerializeField] Image bodyBackground;
    [SerializeField] TextMeshProUGUI itemNameText;
    [SerializeField] RectTransform categoryRow;
    [SerializeField] Image[] categoryBadgeImages;
    [SerializeField] TextMeshProUGUI[] categoryBadgeTexts;
    [SerializeField] TextMeshProUGUI actualValueText;
    [SerializeField] TextMeshProUGUI sellRateText;
    [SerializeField] TextMeshProUGUI buyRateText;
    [SerializeField] GameObject synergySection;
    [SerializeField] TextMeshProUGUI synergyHeaderText;
    [SerializeField] RectTransform synergyListRoot;
    [SerializeField] TextMeshProUGUI[] synergySlots;
    [SerializeField] TextMeshProUGUI futureValueText;
    [SerializeField] TextMeshProUGUI freshnessText;
    [SerializeField] TMP_FontAsset fontAsset;
    [SerializeField] float followSpeed = DEFAULT_FOLLOW_SPEED;

    RectTransform canvasRectTransform;
    Vector2 targetCanvasPosition;
    bool needsSnap;
    bool initialized;

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

        if (!initialized)
        {
            ConfigureFontAndAutoSizing();
            ApplyThemeColors();
            initialized = true;
        }
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<PuzzleSynergyBonus> _bonuses, Vector2 _screenPosition, bool _isTrainInventory = true)
    {
        Show(_item, _actualValue, _rate, _bonuses, null, _screenPosition, _isTrainInventory);
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<PuzzleSynergyBonus> _bonuses, decimal? _futureValue, Vector2 _screenPosition,
        bool _isTrainInventory = true, int? _freshness = null)
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

        ConfigureFontAndAutoSizing();
        ApplyThemeColors();

        if (itemNameText)
        {
            itemNameText.text = _item.DisplayName;
        }

        UpdateCategoryBadges(_item);

        if (actualValueText)
        {
            actualValueText.text = Presentation.PuzzleTooltipActualValue(_actualValue);
        }

        if (_isTrainInventory)
        {
            int sellPercent = _rate.SellPercent > 0 ? _rate.SellPercent : 100;
            decimal finalSellPrice = System.Math.Round(_actualValue * (decimal)sellPercent / 100m, System.MidpointRounding.AwayFromZero);
            if (sellRateText)
            {
                sellRateText.text = Presentation.PuzzleTooltipSellRate(sellPercent);
            }
            if (buyRateText)
            {
                buyRateText.text = Presentation.PuzzleTooltipFinalSellPrice(finalSellPrice, _actualValue);
            }
        }
        else
        {
            int buyPercent = _rate.BuyPercent > 0 ? _rate.BuyPercent : 100;
            decimal finalBuyPrice = System.Math.Round(_actualValue * (decimal)buyPercent / 100m, System.MidpointRounding.AwayFromZero);
            if (sellRateText)
            {
                sellRateText.text = Presentation.PuzzleTooltipBuyRate(buyPercent);
            }
            if (buyRateText)
            {
                buyRateText.text = Presentation.PuzzleTooltipFinalBuyPrice(finalBuyPrice, _actualValue);
            }
        }

        bool showSynergy = _bonuses != null;
        if (synergySection)
        {
            synergySection.SetActive(showSynergy);
        }

        if (showSynergy && synergySlots != null && synergySlots.Length > 0)
        {
            if (synergyHeaderText)
            {
                synergyHeaderText.text = Presentation.PuzzleTooltipSynergyHeader();
            }

            if (_bonuses.Count == 0)
            {
                synergySlots[0].text = Presentation.PuzzleTooltipNoSynergy();
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
                    if (i < _bonuses.Count)
                    {
                        var bonus = _bonuses[i];
                        synergySlots[i].text = Presentation.PuzzleTooltipSynergyBonus(bonus.NeighborName, bonus.Percent);
                        synergySlots[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        synergySlots[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        if (futureValueText)
        {
            if (showSynergy && _futureValue.HasValue)
            {
                futureValueText.text = Presentation.PuzzleTooltipFutureValue(_futureValue.Value, _actualValue);
                futureValueText.gameObject.SetActive(true);
            }
            else
            {
                futureValueText.gameObject.SetActive(false);
            }
        }

        if (freshnessText)
        {
            if (_freshness.HasValue)
            {
                freshnessText.text = Presentation.PuzzleTooltipFreshness(_freshness.Value);
                freshnessText.gameObject.SetActive(true);
            }
            else
            {
                freshnessText.gameObject.SetActive(false);
            }
        }

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
                    badgeImage.color = Presentation.PuzzleTagBackgroundColor(tag);
                }
                if (badgeText)
                {
                    badgeText.text = Presentation.PuzzleTagLabel(tag);
                    badgeText.color = Presentation.PuzzleTagTextColor(tag);
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
        if (synergyListRoot)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(synergyListRoot);
        }
        if (synergySection && synergySection.transform is RectTransform synRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(synRect);
        }
        if (actualValueText && actualValueText.transform.parent is RectTransform bodyRect)
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

        float posX = localPoint.x;
        float posY = localPoint.y;

        Rect cRect = canvasRectTransform.rect;

        // 화면 우측을 벗어나면 마우스 좌측으로 플립
        if (posX + width > cRect.xMax)
        {
            posX -= width;
        }
        posX = Mathf.Clamp(posX, cRect.xMin, Mathf.Max(cRect.xMin, cRect.xMax - width));

        // 화면 하단을 벗어나면 마우스 상단으로 플립
        if (posY - height < cRect.yMin)
        {
            posY += height;
        }
        posY = Mathf.Clamp(posY, Mathf.Min(cRect.yMax, cRect.yMin + height), cRect.yMax);

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

        panelRect.anchoredPosition = Vector2.Lerp(panelRect.anchoredPosition, targetCanvasPosition, Time.unscaledDeltaTime * followSpeed);
    }

    void ApplyThemeColors()
    {
        if (headerBackground)
        {
            headerBackground.color = Presentation.PuzzleTooltipHeaderBackground;
        }
        if (bodyBackground)
        {
            bodyBackground.color = Presentation.PuzzleTooltipBodyBackground;
        }
        if (dividerImage)
        {
            dividerImage.color = Presentation.PuzzleTooltipDivider;
        }
        if (itemNameText)
        {
            itemNameText.color = Presentation.PuzzleTooltipText;
        }
        if (synergyHeaderText)
        {
            synergyHeaderText.color = Presentation.PuzzleTooltipText;
        }
    }

    void ConfigureFontAndAutoSizing()
    {
        if (!fontAsset)
        {
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/NotoSansKR-Medium SDF");
            if (!fontAsset)
            {
                fontAsset = TMP_Settings.defaultFontAsset;
            }
        }

        ApplyTextSettings(itemNameText);
        ApplyTextSettings(actualValueText);
        ApplyTextSettings(sellRateText);
        ApplyTextSettings(buyRateText);
        ApplyTextSettings(synergyHeaderText);
        ApplyTextSettings(futureValueText);
        ApplyTextSettings(freshnessText);

        if (categoryBadgeTexts != null)
        {
            foreach (var badgeText in categoryBadgeTexts)
            {
                ApplyTextSettings(badgeText);
            }
        }

        if (synergySlots != null)
        {
            foreach (var slot in synergySlots)
            {
                ApplyTextSettings(slot);
            }
        }
    }

    void ApplyTextSettings(TextMeshProUGUI _text)
    {
        if (!_text)
        {
            return;
        }

        if (fontAsset && _text.font != fontAsset)
        {
            _text.font = fontAsset;
        }
        _text.fontSize = BASE_FONT_SIZE;
        _text.enableAutoSizing = true;
        _text.fontSizeMin = MIN_FONT_SIZE;
        _text.fontSizeMax = MAX_FONT_SIZE;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.raycastTarget = false;
    }
}
