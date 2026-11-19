using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배지 하나에 표시할 데이터. 태그 ID를 넘기면 Presentation에서 라벨·색상을 자동 해석하고,
/// 직접 색상과 텍스트를 지정하면 범용 배지로 렌더링한다.
/// </summary>
public readonly struct TagBadgeEntry
{
    public readonly bool IsTagId;
    public readonly string TagId;
    public readonly string CustomText;
    public readonly Color BackgroundColor;
    public readonly Color? TextColor;

    TagBadgeEntry(
        bool _isTagId,
        string _tagId,
        string _customText,
        Color _backgroundColor,
        Color? _textColor)
    {
        IsTagId = _isTagId;
        TagId = _tagId;
        CustomText = _customText;
        BackgroundColor = _backgroundColor;
        TextColor = _textColor;
    }

    public static TagBadgeEntry FromTag(string _tagId)
    {
        return new TagBadgeEntry(true, _tagId, null, default, null);
    }

    public static TagBadgeEntry Custom(string _text, Color _backgroundColor, Color? _textColor = null)
    {
        return new TagBadgeEntry(false, null, _text, _backgroundColor, _textColor);
    }
}

/// <summary>
/// 둥근 사각 배경과 텍스트 라벨로 이루어진 단일 UI 배지 컴포넌트.
/// </summary>
public sealed class TagBadgeView : MonoBehaviour
{
    [SerializeField] Image plate;
    [SerializeField] TMP_Text label;
    [SerializeField] HorizontalLayoutGroup layoutGroup;
    [SerializeField] float fontSizeOverride;

    float baseFontSize = -1f;
    RectOffset defaultPadding;

    void Awake()
    {
        EnsureComponents();
    }

    public void ShowTag(string _tagId, float _fontSizeOverride = 0f, RectOffset _paddingOverride = null)
    {
        Show(TagBadgeEntry.FromTag(_tagId), _fontSizeOverride, _paddingOverride);
    }

    public void Show(string _text, Color _backgroundColor, Color? _textColor = null, float _fontSizeOverride = 0f, RectOffset _paddingOverride = null)
    {
        Show(TagBadgeEntry.Custom(_text, _backgroundColor, _textColor), _fontSizeOverride, _paddingOverride);
    }

    public void Show(TagBadgeEntry _entry, float _fontSizeOverride = 0f, RectOffset _paddingOverride = null)
    {
        if (!EnsureComponents())
        {
            return;
        }

        string text;
        Color backgroundColor;
        Color textColor;

        if (_entry.IsTagId)
        {
            if (string.IsNullOrEmpty(_entry.TagId))
            {
                Hide();
                return;
            }
            text = Presentation.PuzzleTagLabel(_entry.TagId);
            backgroundColor = Presentation.PuzzleTagBackgroundColor(_entry.TagId);
            textColor = Presentation.PuzzleTooltipTagTextColor(_entry.TagId);
        }
        else
        {
            text = _entry.CustomText ?? string.Empty;
            backgroundColor = _entry.BackgroundColor;
            textColor = _entry.TextColor ?? Presentation.PuzzleTagTextOnBackground(backgroundColor);
        }

        plate.color = backgroundColor;
        label.text = text;
        label.color = textColor;

        float effectiveFontSize = _fontSizeOverride > 0f
            ? _fontSizeOverride
            : (fontSizeOverride > 0f ? fontSizeOverride : baseFontSize);
        if (effectiveFontSize > 0f)
        {
            label.fontSize = effectiveFontSize;
        }

        ApplyPadding(_paddingOverride ?? defaultPadding);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void ApplyPadding(RectOffset _targetPadding)
    {
        if (!layoutGroup || _targetPadding == null)
        {
            return;
        }
        RectOffset padding = layoutGroup.padding;
        if (padding.left == _targetPadding.left &&
            padding.right == _targetPadding.right &&
            padding.top == _targetPadding.top &&
            padding.bottom == _targetPadding.bottom)
        {
            return;
        }
        padding.left = _targetPadding.left;
        padding.right = _targetPadding.right;
        padding.top = _targetPadding.top;
        padding.bottom = _targetPadding.bottom;
        if (layoutGroup.transform is RectTransform layoutRect)
        {
            LayoutRebuilder.MarkLayoutForRebuild(layoutRect);
        }
    }

    bool EnsureComponents()
    {
        if (!plate)
        {
            plate = GetComponent<Image>();
        }
        if (!label)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
        if (!layoutGroup)
        {
            layoutGroup = GetComponent<HorizontalLayoutGroup>();
        }
        if (layoutGroup && defaultPadding == null)
        {
            RectOffset padding = layoutGroup.padding;
            defaultPadding = new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
        }
        if (label && baseFontSize < 0f)
        {
            baseFontSize = label.fontSize;
        }
        return plate && label;
    }
}
