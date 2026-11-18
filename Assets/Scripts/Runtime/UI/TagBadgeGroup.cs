using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배지 목록을 컨테이너에 배치하는 범용 컨트롤러.
/// 프리팹 풀링과 정적 슬롯 배열, 태그 ID 자동 해석과 커스텀 배지 입력, 빈 목록 fallback 문구를 모두 지원한다.
/// </summary>
public sealed class TagBadgeGroup : MonoBehaviour
{
    [SerializeField] TagBadgeView badgePrefab;
    [SerializeField] TagBadgeView[] slots;
    [SerializeField] TMP_Text emptyLabel;
    [SerializeField] string emptyText = "없음";
    [SerializeField] float badgeBlend = 0.25f;
    [SerializeField] float fontSizeOverride;
    [SerializeField] bool overrideBadgePadding;
    [SerializeField] RectOffset badgePadding;
    [SerializeField] bool hideWhenEmpty;
    [SerializeField] int maxVisibleCount;

    readonly List<TagBadgeView> pooledBadges = new List<TagBadgeView>();
    readonly List<TagBadgeEntry> entryScratch = new List<TagBadgeEntry>();

    public void ShowTags(IEnumerable<string> _rawTags, bool _prioritize = true)
    {
        entryScratch.Clear();
        if (_rawTags != null)
        {
            IEnumerable<string> ordered = _prioritize ? Presentation.PrioritizeTags(_rawTags) : _rawTags;
            foreach (string tag in ordered)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    entryScratch.Add(TagBadgeEntry.FromTag(tag));
                }
            }
        }
        ShowEntries(entryScratch);
    }

    public void ShowEntries(IReadOnlyList<TagBadgeEntry> _entries)
    {
        int totalCount = _entries?.Count ?? 0;
        if (maxVisibleCount > 0 && totalCount > maxVisibleCount)
        {
            totalCount = maxVisibleCount;
        }
        int staticCount = slots?.Length ?? 0;
        if (!badgePrefab && totalCount > staticCount)
        {
            totalCount = staticCount;
        }

        if (hideWhenEmpty && totalCount == 0)
        {
            Clear();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        HideAllPoolItems();
        RectOffset paddingOverride = overrideBadgePadding && badgePadding != null ? badgePadding : null;

        for (int i = 0; i < totalCount; i++)
        {
            TagBadgeView view = AcquireBadge(i, staticCount);
            if (!view)
            {
                continue;
            }
            view.Show(_entries[i], badgeBlend, fontSizeOverride, paddingOverride);
            view.transform.SetSiblingIndex(i);
        }

        for (int i = totalCount; i < staticCount; i++)
        {
            if (slots[i])
            {
                slots[i].Hide();
            }
        }

        if (emptyLabel)
        {
            bool showEmpty = totalCount == 0;
            if (showEmpty && !string.IsNullOrEmpty(emptyText))
            {
                emptyLabel.text = emptyText;
            }
            emptyLabel.gameObject.SetActive(showEmpty);
            if (showEmpty)
            {
                emptyLabel.transform.SetAsLastSibling();
            }
        }

        RebuildLayout();
    }

    public void Clear()
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i])
                {
                    slots[i].Hide();
                }
            }
        }
        HideAllPoolItems();
        if (emptyLabel)
        {
            emptyLabel.gameObject.SetActive(false);
        }
    }

    public void RebuildLayout()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                RebuildBadgeRect(slots[i]);
            }
        }
        for (int i = 0; i < pooledBadges.Count; i++)
        {
            RebuildBadgeRect(pooledBadges[i]);
        }

        if (transform is RectTransform rectTransform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    TagBadgeView AcquireBadge(int _index, int _staticCount)
    {
        if (_index < _staticCount)
        {
            return slots[_index];
        }
        int poolIndex = _index - _staticCount;
        while (pooledBadges.Count <= poolIndex)
        {
            if (!badgePrefab)
            {
                return null;
            }
            TagBadgeView created = Instantiate(badgePrefab, transform);
            created.gameObject.SetActive(false);
            pooledBadges.Add(created);
        }
        return pooledBadges[poolIndex];
    }

    void HideAllPoolItems()
    {
        for (int i = 0; i < pooledBadges.Count; i++)
        {
            if (pooledBadges[i])
            {
                pooledBadges[i].Hide();
            }
        }
    }

    static void RebuildBadgeRect(TagBadgeView _badge)
    {
        if (_badge && _badge.gameObject.activeSelf && _badge.transform is RectTransform badgeRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(badgeRect);
        }
    }
}
