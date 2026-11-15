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
    struct EffectRow
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
    [SerializeField] EffectRow[] activeEffectRows;
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
    [SerializeField] EffectRow[] synergyRows;
    [SerializeField] TextMeshProUGUI futureValueLabelText;
    [SerializeField] TextMeshProUGUI futureValueValueText;
    [SerializeField] TextMeshProUGUI freshnessLabelText;
    [SerializeField] TextMeshProUGUI freshnessValueText;
    [SerializeField] TextMeshProUGUI shelfLifeLabelText;
    [SerializeField] TextMeshProUGUI shelfLifeValueText;

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
    Color defaultTitleColor;
    bool hasDefaultTitleColor;
    bool pointerFlipped;
    bool needsSnap;

    protected override void Awake()
    {
        base.Awake();
        EnsureInitialized();
    }

    void EnsureInitialized()
    {
        if (!canvas || canvas.gameObject == gameObject)
        {
            canvas = transform.parent ? transform.parent.GetComponentInParent<Canvas>(true) : null;
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
        if (itemNameText && !hasDefaultTitleColor)
        {
            defaultTitleColor = itemNameText.color;
            hasDefaultTitleColor = true;
        }
    }

    void BeginShow(string _title, Color? _titleColor = null)
    {
        EnsureInitialized();
        if (synergySection)
        {
            synergySection.SetActive(false);
        }
        needsSnap = true;
        SetOpen(true);

        if (panelRect)
        {
            panelRect.SetAsLastSibling();
        }

        if (itemNameText)
        {
            itemNameText.text = _title ?? string.Empty;
            if (_titleColor.HasValue)
            {
                itemNameText.color = _titleColor.Value;
            }
            else if (hasDefaultTitleColor)
            {
                itemNameText.color = defaultTitleColor;
            }
        }
    }

    void HideAllStatRows()
    {
        SetRowActive(baseValueLabelText, baseValueValueText, false);
        SetRowActive(actualValueLabelText, actualValueValueText, false);
        SetRowActive(sellRateLabelText, sellRateValueText, false);
        SetRowActive(buyRateLabelText, buyRateValueText, false);
        SetRowActive(futureValueLabelText, futureValueValueText, false);
        SetRowActive(freshnessLabelText, freshnessValueText, false);
        SetRowActive(shelfLifeLabelText, shelfLifeValueText, false);
    }

    void SetPlainRow(TextMeshProUGUI _label, TextMeshProUGUI _value, string _labelText, string _valueText)
    {
        SetRow(_label, _value, _labelText, _valueText);
    }

    void PopulatePlainEffectSection(IReadOnlyList<string> _lines, bool _showHeader = false, string _headerText = null)
    {
        if (!generalEffectSection)
        {
            return;
        }

        bool hasLines = _lines != null && _lines.Count > 0;
        generalEffectSection.SetActive(hasLines);
        SetSectionBottomRuleActive(generalEffectSection, false);
        if (!hasLines)
        {
            return;
        }

        if (generalEffectHeaderText)
        {
            generalEffectHeaderText.gameObject.SetActive(_showHeader && !string.IsNullOrEmpty(_headerText));
            if (_showHeader && !string.IsNullOrEmpty(_headerText))
            {
                generalEffectHeaderText.color = Presentation.PuzzleTooltipText;
                generalEffectHeaderText.text = _headerText;
            }
        }

        if (generalEffectSlots == null)
        {
            return;
        }

        for (int i = 0; i < generalEffectSlots.Length; i++)
        {
            var slot = generalEffectSlots[i];
            if (!slot)
            {
                continue;
            }

            bool used = i < _lines.Count;
            slot.gameObject.SetActive(used);
            if (used)
            {
                slot.color = Presentation.PuzzleTooltipText;
                slot.text = i == generalEffectSlots.Length - 1 && _lines.Count > generalEffectSlots.Length
                    ? string.Join("\n", _lines.Skip(i))
                    : _lines[i];
            }
        }
    }

    internal void Show(PuzzleItemDefinition _item, decimal _actualValue, PuzzleTradeRate _rate,
        IReadOnlyList<string> _generalEffects, IReadOnlyList<PuzzleTooltipEffectEntry> _synergyEffects, decimal? _futureValue, Vector2 _screenPosition,
        bool _isTrainInventory, int? _freshness, long? _minutesUntilNextFreshnessBand, long? _previewMinutes, string _previewCityName,
        long? _previewActivations, long? _tradePrice, IReadOnlyList<PuzzleSynergyBonus> _activeEffects, int _appliedPreservation,
        bool _isActionBoard = false)
    {
        if (_item == null)
        {
            Close();
            return;
        }

        BeginShow(_item.DisplayName);

        if (activeEffectsSection)
        {
            activeEffectsSection.gameObject.SetActive(true);
        }

        UpdateCategoryBadges(_item);

        SetRow(baseValueLabelText, baseValueValueText,
            Presentation.PuzzleTooltipBaseValueLabel, Presentation.PuzzleTooltipActualValueValue(_item.BaseValue));
        UpdateActiveEffects(_isTrainInventory, _isActionBoard, _activeEffects, _appliedPreservation);
        SetRow(actualValueLabelText, actualValueValueText,
            Presentation.PuzzleTooltipActualValueLabel(), Presentation.PuzzleTooltipActualValueValue(_actualValue));

        if (_isTrainInventory || _isActionBoard)
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
        // UpdateSynergySection(_synergyEffects);

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
        ShowEntityInfo(_title, null, string.IsNullOrEmpty(_description) ? null : new[] { _description }, _screenPosition);
    }

    /// <summary>
    /// 엔티티 링크(지역·마을·태그·의뢰) 및 안내 문구를 기존 툴팁 카드 레이아웃으로 표시한다.
    /// </summary>
    internal void ShowEntityInfo(string _title, Color? _titleColor, IReadOnlyList<string> _lines,
        Vector2 _screenPosition, IReadOnlyList<string> _tags = null,
        IReadOnlyList<(string label, string value)> _statRows = null)
    {
        BeginShow(_title, _titleColor);
        UpdateCategoryBadges(_tags);
        if (_statRows != null && _statRows.Count > 0)
        {
            ApplyPlainStatRows(_statRows);
        }
        else
        {
            HideAllStatRows();
        }
        if (activeEffectsSection)
        {
            activeEffectsSection.gameObject.SetActive(false);
        }
        PopulatePlainEffectSection(_lines);
        RebuildLayouts();
        SetPointerPosition(_screenPosition);
    }

    /// <summary>
    /// 의뢰·소문의 화물 품목 링크 정보를 기존 아이템 툴팁 레이아웃(태그 배지, 기본 능력치, 일반 효과)으로 표시한다.
    /// </summary>
    internal void ShowPieceInfo(PuzzlePieceData _piece, IReadOnlyList<string> _statLabels,
        IReadOnlyList<string> _statValues, IReadOnlyList<string> _generalEffects,
        IReadOnlyList<PuzzleTooltipEffectEntry> _synergies, Vector2 _screenPosition)
    {
        if (_piece == null)
        {
            Close();
            return;
        }

        BeginShow(_piece.displayName);
        UpdateCategoryBadges(_piece.tags);

        int statCount = Mathf.Min(_statLabels?.Count ?? 0, _statValues?.Count ?? 0);
        var statRows = new List<(string label, string value)>(statCount);
        for (int i = 0; i < statCount; i++)
        {
            statRows.Add((_statLabels[i], _statValues[i]));
        }
        ApplyPlainStatRows(statRows);

        if (activeEffectsSection)
        {
            activeEffectsSection.gameObject.SetActive(false);
        }

        UpdateEffectSection(generalEffectSection, generalEffectHeaderText, generalEffectSlots, _generalEffects,
            Presentation.PuzzleTooltipGeneralEffectHeader(), _showEmpty: (_synergies?.Count ?? 0) == 0);
        // UpdateSynergySection(_synergies);
        RebuildLayouts();
        SetPointerPosition(_screenPosition);
    }

    /// <summary>
    /// 세계 지도의 마을 정보와 이동 견적을 기존 툴팁 레이아웃으로 표시한다.
    /// </summary>
    internal void ShowTravelPreview(string _title, Color _titleColor,
        IReadOnlyList<(string label, string value)> _statRows, IReadOnlyList<string> _infoLines, Vector2 _screenPosition)
    {
        ShowEntityInfo(_title, _titleColor, _infoLines, _screenPosition, _statRows: _statRows);
    }

    void ApplyPlainStatRows(IReadOnlyList<(string label, string value)> _rows)
    {
        SetPlainStatSlot(baseValueLabelText, baseValueValueText, _rows, 0);
        SetPlainStatSlot(actualValueLabelText, actualValueValueText, _rows, 1);
        SetPlainStatSlot(sellRateLabelText, sellRateValueText, _rows, 2);
        SetPlainStatSlot(buyRateLabelText, buyRateValueText, _rows, 3);
        SetRowActive(futureValueLabelText, futureValueValueText, false);
        SetRowActive(freshnessLabelText, freshnessValueText, false);
        SetRowActive(shelfLifeLabelText, shelfLifeValueText, false);
    }

    void SetPlainStatSlot(TextMeshProUGUI _label, TextMeshProUGUI _value,
        IReadOnlyList<(string label, string value)> _rows, int _index)
    {
        if (_rows != null && _index < _rows.Count)
        {
            SetPlainRow(_label, _value, _rows[_index].label, _rows[_index].value);
        }
        else
        {
            SetRowActive(_label, _value, false);
        }
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

    static bool PrepareEffectSection(GameObject _section, TextMeshProUGUI _header, string _title, int _effectCount, bool _showEmpty)
    {
        if (!_section)
        {
            return false;
        }

        bool visible = _effectCount > 0 || _showEmpty;
        _section.SetActive(visible);
        if (!visible)
        {
            return false;
        }

        SetSectionBottomRuleActive(_section, true);
        if (_header)
        {
            _header.gameObject.SetActive(true);
            _header.color = Presentation.PuzzleTooltipText;
            _header.text = _title;
        }
        return true;
    }

    static void UpdateEffectSection(GameObject _section, TextMeshProUGUI _header, TextMeshProUGUI[] _slots,
        IReadOnlyList<string> _effects, string _title, bool _showEmpty)
    {
        int effectCount = _effects?.Count ?? 0;
        if (!PrepareEffectSection(_section, _header, _title, effectCount, _showEmpty) || _slots == null)
        {
            return;
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

    void UpdateSynergySection(IReadOnlyList<PuzzleTooltipEffectEntry> _effects,
        string _header = null, string _emptyText = null)
    {
        int effectCount = _effects?.Count ?? 0;
        bool showEmpty = !string.IsNullOrEmpty(_emptyText);
        string header = string.IsNullOrEmpty(_header) ? Presentation.PuzzleTooltipValueSynergyHeader() : _header;
        if (!PrepareEffectSection(synergySection, synergyHeaderText, header, effectCount, showEmpty) || synergyRows == null)
        {
            return;
        }

        if (effectCount == 0)
        {
            for (int i = 0; i < synergyRows.Length; i++)
            {
                EffectRow row = synergyRows[i];
                if (i == 0)
                {
                    SetPlainRow(row.labelText, row.valueText, _emptyText, string.Empty);
                }
                else
                {
                    SetRowActive(row.labelText, row.valueText, false);
                }
            }
            return;
        }

        for (int i = 0; i < synergyRows.Length; i++)
        {
            EffectRow row = synergyRows[i];
            if (i < effectCount)
            {
                if (i == synergyRows.Length - 1 && effectCount > synergyRows.Length)
                {
                    string combined = string.Join("\n", _effects.Skip(i).Select(JoinEffectEntry));
                    SetPlainRow(row.labelText, row.valueText, combined, string.Empty);
                }
                else
                {
                    SetPlainRow(row.labelText, row.valueText, _effects[i].Description, _effects[i].Value);
                }
            }
            else
            {
                SetRowActive(row.labelText, row.valueText, false);
            }
        }
    }

    static string JoinEffectEntry(PuzzleTooltipEffectEntry _entry)
    {
        return string.IsNullOrEmpty(_entry.Value) ? _entry.Description : $"{_entry.Description} {_entry.Value}";
    }

    void UpdateCategoryBadges(PuzzleItemDefinition _item)
    {
        UpdateCategoryBadges(_item != null ? _item.Tags : null);
    }

    void UpdateCategoryBadges(IReadOnlyList<string> _rawTags)
    {
        if (!categoryRow)
        {
            return;
        }

        var tags = _rawTags != null && _rawTags.Count > 0 ? Presentation.PrioritizeTags(_rawTags) : System.Array.Empty<string>();
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

    void UpdateActiveEffects(bool _isTrainInventory, bool _isActionBoard, IReadOnlyList<PuzzleSynergyBonus> _activeEffects,
        int _appliedPreservation)
    {
        int valueCount = _isTrainInventory ? _activeEffects?.Count ?? 0 : 0;
        bool hasPreservation = _isTrainInventory && _appliedPreservation > 0;
        int activeCount = valueCount + (hasPreservation ? 1 : 0);
        PuzzleTooltipEffectEntry preservation = hasPreservation
            ? Presentation.PuzzleAppliedPreservationEffect(_appliedPreservation)
            : default;
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
                string preservationLine = hasPreservation ? JoinEffectEntry(preservation) : null;
                activeEffectsMessageText.text = activeCount > 0
                    ? string.Join("\n", new[] { valueSummary, preservationLine }.Where(_line => !string.IsNullOrEmpty(_line)))
                    : _isTrainInventory || _isActionBoard ? Presentation.PuzzleNoActiveEffects : Presentation.PuzzleMarketNoSettlement;
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
            EffectRow row = activeEffectRows[i];
            bool active = i < activeCount;
            if (i < valueCount)
            {
                SetRow(row.labelText, row.valueText,
                    Presentation.PuzzleTooltipActiveEffectLabel(_activeEffects[i]),
                    Presentation.PuzzleTooltipActiveEffectValue(_activeEffects[i]));
            }
            else if (active)
            {
                SetRow(row.labelText, row.valueText, preservation.Description, preservation.Value);
            }
            else
            {
                SetRowActive(row.labelText, row.valueText, false);
            }
        }
    }

    void SetRow(TextMeshProUGUI _label, TextMeshProUGUI _value, string _labelText, string _valueText)
    {
        SetRowActive(_label, _value, true);
        if (_label)
        {
            _label.color = Presentation.PuzzleTooltipText;
            _label.text = _labelText;
        }
        if (_value)
        {
            _value.color = Presentation.PuzzleTooltipText;
            _value.text = _valueText;
            _value.gameObject.SetActive(!string.IsNullOrEmpty(_valueText));
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
