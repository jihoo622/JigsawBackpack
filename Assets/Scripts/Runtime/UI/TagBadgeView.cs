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
    public readonly float BlendOverride;

    TagBadgeEntry(
        bool _isTagId,
        string _tagId,
        string _customText,
        Color _backgroundColor,
        Color? _textColor,
        float _blendOverride)
    {
        IsTagId = _isTagId;
        TagId = _tagId;
        CustomText = _customText;
        BackgroundColor = _backgroundColor;
        TextColor = _textColor;
        BlendOverride = _blendOverride;
    }

    public static TagBadgeEntry FromTag(string _tagId, float _blendOverride = -1f)
    {
        return new TagBadgeEntry(true, _tagId, null, default, null, _blendOverride);
    }

    public static TagBadgeEntry Custom(string _text, Color _backgroundColor, Color? _textColor = null)
    {
        return new TagBadgeEntry(false, null, _text, _backgroundColor, _textColor, -1f);
    }
}

/// <summary>
/// 둥근 사각 배경과 텍스트 라벨로 이루어진 단일 UI 배지 컴포넌트.
/// </summary>
public sealed class TagBadgeView : MonoBehaviour
{
    [SerializeField] Image plate;
    [SerializeField] TMP_Text label;
    [SerializeField] float defaultBlend = 0.25f;
    [SerializeField] float fontSizeOverride;

    float baseFontSize = -1f;

    void Awake()
    {
        EnsureComponents();
    }

    public void ShowTag(string _tagId, float _blend = -1f, float _fontSizeOverride = 0f)
    {
        Show(TagBadgeEntry.FromTag(_tagId, _blend), -1f, _fontSizeOverride);
    }

    public void Show(string _text, Color _backgroundColor, Color? _textColor = null, float _fontSizeOverride = 0f)
    {
        Show(TagBadgeEntry.Custom(_text, _backgroundColor, _textColor), -1f, _fontSizeOverride);
    }

    public void Show(TagBadgeEntry _entry, float _groupBlend = -1f, float _fontSizeOverride = 0f)
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
            float blend = _entry.BlendOverride >= 0f
                ? _entry.BlendOverride
                : (_groupBlend >= 0f ? _groupBlend : defaultBlend);
            text = Presentation.PuzzleTagLabel(_entry.TagId);
            backgroundColor = Presentation.PuzzleTooltipTagBackground(_entry.TagId, blend);
            textColor = Presentation.PuzzleTooltipTagTextColor(_entry.TagId, blend);
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

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
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
        if (label && baseFontSize < 0f)
        {
            baseFontSize = label.fontSize;
        }
        return plate && label;
    }
}
