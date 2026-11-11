using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PuzzleItemTooltipView : PanelBase
{
    static float BaseFontSize => BasicData.Float("ui.PuzzleItemTooltipView.BaseFontSize");
    static float MinFontSize => BasicData.Float("ui.PuzzleItemTooltipView.MinFontSize");
    static float MaxFontSize => BasicData.Float("ui.PuzzleItemTooltipView.MaxFontSize");
    static float DefaultFollowSpeed => BasicData.Float("ui.PuzzleItemTooltipView.DefaultFollowSpeed");

    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform panelRect;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image headerBackground;
    [SerializeField] Image dividerImage;
    [SerializeField] Image bodyBackground;
    [SerializeField] RectTransform pointer;
    [SerializeField] TextMeshProUGUI itemNameText;
    [SerializeField] RectTransform categoryRow;
    [SerializeField] Image[] categoryBadgeImages;
    [SerializeField] TextMeshProUGUI[] categoryBadgeTexts;
    [SerializeField] RectTransform bodyRect;
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
    [SerializeField] TMP_FontAsset fontAsset;
    float FollowSpeed => DefaultFollowSpeed;

    RectTransform canvasRectTransform;
    Vector2 targetCanvasPosition;
    bool pointerFlipped;
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
            BuildPanelDecoration();
            ConfigureFontAndAutoSizing();
            ApplyThemeColors();
            initialized = true;
        }
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<string> _effects, Vector2 _screenPosition, bool _isTrainInventory = true)
    {
        Show(_item, _actualValue, _rate, _effects, null, _screenPosition, _isTrainInventory);
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<string> _effects, decimal? _futureValue, Vector2 _screenPosition,
        bool _isTrainInventory = true, int? _freshness = null, long? _previewMinutes = null, string _previewCityName = null,
        long? _previewActivations = null)
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

        SetRow(actualValueLabelText, actualValueValueText,
            Presentation.PuzzleTooltipActualValueLabel(), Presentation.PuzzleTooltipActualValueValue(_actualValue));

        if (_isTrainInventory)
        {
            int sellPercent = _rate.SellPercent;
            decimal finalSellPrice = _rate.HasRareValue ? _rate.RareValue
                : System.Math.Round(_actualValue * (decimal)sellPercent / PuzzleInventoryValuation.BASE_PERCENT, System.MidpointRounding.AwayFromZero);
            SetRow(sellRateLabelText, sellRateValueText,
                Presentation.PuzzleTooltipSellRateLabel(), Presentation.PuzzleTooltipSellRateValue(sellPercent));
            SetRow(buyRateLabelText, buyRateValueText,
                Presentation.PuzzleTooltipFinalSellPriceLabel(), Presentation.PuzzleTooltipFinalSellPriceValue(finalSellPrice, _actualValue));
        }
        else
        {
            int buyPercent = _rate.BuyPercent;
            decimal finalBuyPrice = _rate.HasRareValue ? _rate.RareValue
                : System.Math.Round(_actualValue * (decimal)buyPercent / PuzzleInventoryValuation.BASE_PERCENT, System.MidpointRounding.AwayFromZero);
            SetRow(sellRateLabelText, sellRateValueText,
                Presentation.PuzzleTooltipBuyRateLabel(), Presentation.PuzzleTooltipBuyRateValue(buyPercent));
            SetRow(buyRateLabelText, buyRateValueText,
                Presentation.PuzzleTooltipFinalBuyPriceLabel(), Presentation.PuzzleTooltipFinalBuyPriceValue(finalBuyPrice, _actualValue));
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
                        Presentation.PuzzleTooltipBodyBackground, 0.35f);
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
        const float pointerTopInset = 32f;
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
            pointer.anchoredPosition = new Vector2(0f,
                Mathf.Clamp(localPoint.y - posY, -height + 20f, -20f));
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

    void BuildPanelDecoration()
    {
        Color frameColor = new Color32(191, 174, 147, 255);
        Sprite roundedSprite = null;
        foreach (var badge in categoryBadgeImages ?? System.Array.Empty<Image>())
        {
            if (badge && badge.sprite)
            {
                roundedSprite = badge.sprite;
                break;
            }
        }

        if (bodyBackground)
        {
            // The root image forms the frame, with a matching inset surface over it.
            var frame = bodyBackground;
            frame.sprite = roundedSprite;
            frame.type = Image.Type.Sliced;
            // The badge sprite uses smaller borders so it does not dictate the badge height.
            float panelBorderScale = roundedSprite ? 64f / roundedSprite.pixelsPerUnit : 1f;
            frame.pixelsPerUnitMultiplier = panelBorderScale;
            frame.color = frameColor;
            var shadow = frame.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(3f, -5f);

            bodyBackground = CreateDecoration("Panel Surface", panelRect, Presentation.PuzzleTooltipBodyBackground);
            bodyBackground.sprite = roundedSprite;
            bodyBackground.type = Image.Type.Sliced;
            bodyBackground.pixelsPerUnitMultiplier = panelBorderScale;
            Stretch(bodyBackground.rectTransform, 2f);
            bodyBackground.transform.SetAsFirstSibling();

            if (pointer)
            {
                pointer.SetSiblingIndex(1);
            }
        }

        if (panelRect.TryGetComponent<VerticalLayoutGroup>(out var panelLayout))
        {
            panelLayout.padding = new RectOffset(16, 16, 12, 14);
        }
        if (headerBackground && headerBackground.TryGetComponent<VerticalLayoutGroup>(out var headerLayout))
        {
            headerLayout.padding = new RectOffset(0, 0, 0, 10);
            headerLayout.spacing = 6f;
            if (headerBackground.TryGetComponent<ContentSizeFitter>(out var headerFitter))
            {
                headerFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }
        if (bodyRect && bodyRect.TryGetComponent<VerticalLayoutGroup>(out var bodyLayout))
        {
            bodyLayout.padding = new RectOffset(0, 0, 6, 0);
            bodyLayout.spacing = 10f;
            // Decorate rows in their scene order. Hidden rows also hide their rules.
            foreach (Transform row in bodyRect)
            {
                if (synergySection && row == synergySection.transform)
                {
                    continue;
                }
                var rule = CreateDecoration("Bottom Rule", row, Presentation.PuzzleTooltipDivider);
                var rect = rule.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.right;
                rect.sizeDelta = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, -5f);
            }
        }
    }

    static Image CreateDecoration(string _name, Transform _parent, Color _color)
    {
        var decoration = new GameObject(_name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        decoration.transform.SetParent(_parent, false);
        decoration.GetComponent<LayoutElement>().ignoreLayout = true;
        var image = decoration.GetComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform _rect, float _inset)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.one * _inset;
        _rect.offsetMax = -Vector2.one * _inset;
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
        ApplyBodyTextSettings(actualValueLabelText);
        ApplyBodyTextSettings(actualValueValueText);
        ApplyBodyTextSettings(sellRateLabelText);
        ApplyBodyTextSettings(sellRateValueText);
        ApplyBodyTextSettings(buyRateLabelText);
        ApplyBodyTextSettings(buyRateValueText);
        ApplyBodyTextSettings(synergyHeaderText);
        ApplyBodyTextSettings(futureValueLabelText);
        if (futureValueLabelText)
        {
            futureValueLabelText.textWrappingMode = TextWrappingModes.Normal;
        }
        ApplyBodyTextSettings(futureValueValueText);
        ApplyBodyTextSettings(freshnessLabelText);
        ApplyBodyTextSettings(freshnessValueText);
        if (itemNameText)
        {
            itemNameText.enableAutoSizing = false;
            itemNameText.fontSize = BaseFontSize * 1.45f;
            itemNameText.fontStyle = FontStyles.Bold;
        }
        if (synergyHeaderText)
        {
            synergyHeaderText.fontStyle = FontStyles.Bold;
        }

        if (categoryBadgeTexts != null)
        {
            foreach (var badgeText in categoryBadgeTexts)
            {
                ApplyTextSettings(badgeText);
                if (badgeText)
                {
                    badgeText.enableAutoSizing = false;
                    badgeText.fontSize = BaseFontSize * 0.85f;
                    badgeText.fontStyle = FontStyles.Bold;
                }
            }
        }

        if (synergySlots != null)
        {
            foreach (var slot in synergySlots)
            {
                ApplyBodyTextSettings(slot);
                if (slot)
                {
                    slot.textWrappingMode = TextWrappingModes.Normal;
                }
            }
        }
    }

    void ApplyBodyTextSettings(TextMeshProUGUI _text)
    {
        if (!_text)
        {
            return;
        }

        if (fontAsset && _text.font != fontAsset)
        {
            _text.font = fontAsset;
        }
        _text.fontSize = BaseFontSize;
        _text.enableAutoSizing = false;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.raycastTarget = false;
    }

    void SetRow(TextMeshProUGUI _label, TextMeshProUGUI _value, string _labelText, string _valueText)
    {
        if (_label)
        {
            _label.text = _labelText;
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (_value)
        {
            _value.text = _valueText;
            _value.alignment = TextAlignmentOptions.MidlineRight;
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
        _text.fontSize = BaseFontSize;
        _text.enableAutoSizing = true;
        _text.fontSizeMin = MinFontSize;
        _text.fontSizeMax = MaxFontSize;
        _text.textWrappingMode = categoryBadgeTexts != null && System.Array.IndexOf(categoryBadgeTexts, _text) >= 0
            ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        _text.raycastTarget = false;
    }
}
