using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 품목 링크에 마우스를 올렸을 때 뜨는 아이템 정보 팝업(표시 전용, 입력을 받지 않는다).
// 이름·태그 칩·설명·수치 줄·상성 줄은 프리팹에 미리 배치한 슬롯을 켜고 끈다. 높이는 레이아웃 그룹·크기 맞춤이 정한다.
public sealed class ItemInfoPopupView : MonoBehaviour
{
    [SerializeField] RectTransform card;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] GameObject tagRow;
    [SerializeField] Image[] tagPlates;
    [SerializeField] TextMeshProUGUI[] tagLabels;
    [SerializeField] TextMeshProUGUI descriptionLabel;
    [SerializeField] TextMeshProUGUI[] statLabels;
    [SerializeField] TextMeshProUGUI[] statValues;
    [SerializeField] TextMeshProUGUI synergyHeader;
    [SerializeField] TextMeshProUGUI[] synergyLines;
    [SerializeField, Range(0f, 1f)] float tagBlend = 0.35f;
    // 커서에서 팝업 왼쪽 위까지의 거리와 화면 가장자리 여백(프리팹 값이 원본)
    [SerializeField] Vector2 cursorOffset = new Vector2(16f, -16f);
    [SerializeField] float screenMargin = 16f;

    internal void Show(PuzzlePieceData _piece, IReadOnlyList<string> _statNames, IReadOnlyList<string> _statTexts,
        IReadOnlyList<string> _synergies, Vector2 _screenPosition)
    {
        titleLabel.text = _piece.displayName;
        titleLabel.color = Presentation.PuzzleHeading;
        ShowTags(Presentation.PrioritizeTags(_piece.tags ?? System.Array.Empty<string>()));
        bool hasDescription = !string.IsNullOrEmpty(_piece.description);
        descriptionLabel.gameObject.SetActive(hasDescription);
        if (hasDescription)
        {
            descriptionLabel.text = _piece.description;
            descriptionLabel.color = Presentation.PuzzleTooltipText;
        }
        for (int i = 0; i < statLabels.Length; i++)
        {
            bool used = i < _statNames.Count;
            statLabels[i].transform.parent.gameObject.SetActive(used);
            if (used)
            {
                statLabels[i].text = _statNames[i];
                statLabels[i].color = Presentation.ItemInfoLabelColor;
                statValues[i].text = _statTexts[i];
                statValues[i].color = Presentation.PuzzleHeading;
            }
        }
        synergyHeader.text = Presentation.ItemInfoSynergyHeader;
        synergyHeader.color = Presentation.ItemInfoLabelColor;
        ShowSynergies(_synergies);
        card.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        Place(_screenPosition);
    }

    internal void Hide()
    {
        card.gameObject.SetActive(false);
    }

    void ShowTags(IReadOnlyList<string> _tags)
    {
        tagRow.SetActive(_tags.Count > 0);
        for (int i = 0; i < tagPlates.Length; i++)
        {
            bool used = i < _tags.Count;
            tagPlates[i].gameObject.SetActive(used);
            if (used)
            {
                tagPlates[i].color = Presentation.PuzzleTooltipTagBackground(_tags[i], tagBlend);
                tagLabels[i].text = Presentation.PuzzleTagLabel(_tags[i]);
                tagLabels[i].color = Presentation.PuzzleTooltipTagTextColor(_tags[i], tagBlend);
            }
        }
    }

    // 슬롯보다 많으면 마지막 슬롯에 "외 N개"를 쓴다. 하나도 없으면 첫 슬롯에 '없음' 문구.
    void ShowSynergies(IReadOnlyList<string> _synergies)
    {
        bool overflow = _synergies.Count > synergyLines.Length;
        for (int i = 0; i < synergyLines.Length; i++)
        {
            var line = synergyLines[i];
            if (_synergies.Count == 0)
            {
                line.gameObject.SetActive(i == 0);
                line.text = Presentation.ItemInfoNoSynergy;
                line.color = Presentation.ItemInfoLabelColor;
                continue;
            }
            bool used = i < _synergies.Count;
            line.gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }
            bool last = i == synergyLines.Length - 1;
            line.color = Presentation.PuzzleHeading;
            line.text = overflow && last ? Presentation.ItemInfoMore(_synergies.Count - i) : _synergies[i];
        }
    }

    // 커서 오른쪽 아래에 두되 화면 밖으로 나가면 반대쪽으로 붙인다.
    void Place(Vector2 _screenPosition)
    {
        var parent = (RectTransform)card.parent;
        var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _screenPosition, eventCamera, out var local);
        Rect bounds = parent.rect;
        Vector2 size = card.rect.size;
        float x = local.x + cursorOffset.x;
        float y = local.y + cursorOffset.y;
        if (x + size.x > bounds.xMax - screenMargin)
        {
            x = local.x - cursorOffset.x - size.x;
        }
        if (y - size.y < bounds.yMin + screenMargin)
        {
            y = local.y - cursorOffset.y + size.y;
        }
        x = Mathf.Clamp(x, bounds.xMin + screenMargin, bounds.xMax - screenMargin - size.x);
        y = Mathf.Clamp(y, bounds.yMin + screenMargin + size.y, bounds.yMax - screenMargin);
        card.pivot = new Vector2(0f, 1f);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(x, y);
    }
}
