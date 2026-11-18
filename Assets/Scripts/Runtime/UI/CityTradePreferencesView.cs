using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도시의 교역 선호도(흔한 자원·귀한 자원·희귀 자원·특수 매입)를 라벨 + TagBadgeGroup으로 렌더링한다.
/// 지도(2x2)와 상점(세로 목록 + 툴팁 링크)이 동일한 데이터를 받아 공용으로 사용한다.
/// </summary>
public sealed class CityTradePreferencesView : MonoBehaviour
{
    [Serializable]
    struct GradeSlot
    {
        public PuzzleCityTradeGrade grade;
        public GameObject rowRoot;
        public TMP_Text titleLabel;
        public TagBadgeGroup badgeGroup;
        public TMP_Text suffixLabel;
        public bool hideRowWhenEmpty;
    }

    [SerializeField] GradeSlot[] grades;
    [SerializeField] TMP_Text regionPriceLabel;
    [SerializeField] bool useKeywordLinks;
    [SerializeField] bool alignTitleColumn;

    internal void Show(PuzzleCityTradePreferencesData _data)
    {
        if (!_data.IsValid)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        if (grades != null)
        {
            for (int i = 0; i < grades.Length; i++)
            {
                ApplyGradeSlot(ref grades[i], _data);
            }
            SyncTitleColumnWidths();
        }

        if (regionPriceLabel)
        {
            regionPriceLabel.text = Presentation.CityTradeRegionPriceText(
                _data.BuyPercentOfBaseValue, _data.SellPercentOfFinalValue, useKeywordLinks);
            regionPriceLabel.raycastTarget = useKeywordLinks;
            regionPriceLabel.ForceMeshUpdate();
            regionPriceLabel.gameObject.SetActive(true);
        }

        RebuildLayout();
    }

    internal void Hide()
    {
        if (grades != null)
        {
            for (int i = 0; i < grades.Length; i++)
            {
                grades[i].badgeGroup?.Clear();
            }
        }
        gameObject.SetActive(false);
    }

    internal bool TryGetHoveredKeywordLink(Vector2 _screenPosition, Camera _eventCamera, out string _linkId)
    {
        _linkId = null;
        if (!useKeywordLinks || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (grades != null)
        {
            for (int i = 0; i < grades.Length; i++)
            {
                ref readonly GradeSlot slot = ref grades[i];
                if (slot.rowRoot && !slot.rowRoot.activeInHierarchy)
                {
                    continue;
                }
                if (TryGetHoveredTmpLink(slot.titleLabel, _screenPosition, _eventCamera, out _linkId))
                {
                    return true;
                }
            }
        }

        return TryGetHoveredTmpLink(regionPriceLabel, _screenPosition, _eventCamera, out _linkId);
    }

    void ApplyGradeSlot(ref GradeSlot _slot, PuzzleCityTradePreferencesData _data)
    {
        IReadOnlyList<string> tags = ResolveGradeTags(_slot.grade, _data, out bool gradeEnabled);
        bool visible = gradeEnabled && (!_slot.hideRowWhenEmpty || tags.Count > 0);
        if (_slot.rowRoot)
        {
            _slot.rowRoot.SetActive(visible);
        }
        if (!visible)
        {
            _slot.badgeGroup?.Clear();
            if (_slot.suffixLabel)
            {
                _slot.suffixLabel.gameObject.SetActive(false);
            }
            return;
        }

        if (_slot.titleLabel)
        {
            Presentation.CityTradeGradeInfo(_slot.grade, useKeywordLinks, out string formattedTitle, out _, out _);
            _slot.titleLabel.text = formattedTitle;
            _slot.titleLabel.raycastTarget = useKeywordLinks;
            _slot.titleLabel.ForceMeshUpdate();
        }

        _slot.badgeGroup?.ShowTags(tags, false);

        if (_slot.suffixLabel)
        {
            bool showSuffix = _slot.grade == PuzzleCityTradeGrade.Waste && _data.HasWastePurchase;
            if (showSuffix)
            {
                _slot.suffixLabel.text = Presentation.CityTradeWasteSuffix;
            }
            _slot.suffixLabel.gameObject.SetActive(showSuffix);
        }

        if (_slot.rowRoot && _slot.rowRoot.transform is RectTransform rowRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
        }
    }

    void SyncTitleColumnWidths()
    {
        if (grades == null)
        {
            return;
        }

        float maxWidth = 0f;
        if (alignTitleColumn)
        {
            for (int i = 0; i < grades.Length; i++)
            {
                ref readonly GradeSlot slot = ref grades[i];
                if ((slot.rowRoot && !slot.rowRoot.activeSelf) || !slot.titleLabel)
                {
                    continue;
                }
                float width = slot.titleLabel.preferredWidth;
                if (width > maxWidth)
                {
                    maxWidth = width;
                }
            }
        }

        float targetWidth = alignTitleColumn && maxWidth > 0f ? Mathf.Ceil(maxWidth) : -1f;
        for (int i = 0; i < grades.Length; i++)
        {
            TMP_Text title = grades[i].titleLabel;
            if (!title)
            {
                continue;
            }
            LayoutElement layout = title.GetComponent<LayoutElement>();
            if (targetWidth <= 0f)
            {
                if (layout)
                {
                    layout.minWidth = -1f;
                    layout.preferredWidth = -1f;
                }
                continue;
            }
            if (!layout)
            {
                layout = title.gameObject.AddComponent<LayoutElement>();
            }
            layout.minWidth = targetWidth;
            layout.preferredWidth = targetWidth;
        }
    }

    void RebuildLayout()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (grades != null)
        {
            for (int i = 0; i < grades.Length; i++)
            {
                grades[i].badgeGroup?.RebuildLayout();
                if (grades[i].rowRoot && grades[i].rowRoot.activeSelf && grades[i].rowRoot.transform is RectTransform rowRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
                }
            }
            for (int i = 0; i < grades.Length; i++)
            {
                if (grades[i].rowRoot && grades[i].rowRoot.transform.parent is RectTransform parentRect && parentRect != transform)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }
            }
        }

        if (transform is RectTransform rootRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }
    }

    static IReadOnlyList<string> ResolveGradeTags(PuzzleCityTradeGrade _grade, PuzzleCityTradePreferencesData _data, out bool _enabled)
    {
        switch (_grade)
        {
            case PuzzleCityTradeGrade.Common:
                _enabled = true;
                return _data.CommonTags;
            case PuzzleCityTradeGrade.Expensive:
                _enabled = true;
                return _data.ExpensiveTags;
            case PuzzleCityTradeGrade.Rare:
                _enabled = true;
                return _data.RareTags;
            case PuzzleCityTradeGrade.Waste:
                _enabled = _data.HasWastePurchase;
                return _data.WasteTags;
            default:
                _enabled = false;
                return Array.Empty<string>();
        }
    }

    internal static bool TryGetHoveredTmpLink(TMP_Text _label, Vector2 _screenPosition, Camera _eventCamera, out string _linkId)
    {
        _linkId = null;
        if (!_label || !_label.gameObject.activeInHierarchy)
        {
            return false;
        }
        if (_label.havePropertiesChanged)
        {
            _label.ForceMeshUpdate();
        }
        if (_label.textInfo == null || _label.textInfo.linkCount == 0)
        {
            return false;
        }
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_label, _screenPosition, _eventCamera);
        if (linkIndex < 0 || linkIndex >= _label.textInfo.linkInfo.Length)
        {
            return false;
        }
        _linkId = _label.textInfo.linkInfo[linkIndex].GetLinkID();
        return !string.IsNullOrEmpty(_linkId);
    }
}
