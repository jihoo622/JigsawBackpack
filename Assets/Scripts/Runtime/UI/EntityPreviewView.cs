using TMPro;
using UnityEngine;

// 고유명사에 마우스를 올렸을 때 뜨는 미리보기 카드(표시 전용, 입력을 받지 않는다).
// 제목 1줄 + 사전 배치한 줄 4개. 높이는 프리팹의 레이아웃 그룹·크기 맞춤이 정한다.
public sealed class EntityPreviewView : MonoBehaviour
{
    [SerializeField] RectTransform card;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI[] lineLabels;
    // 커서에서 카드 왼쪽 위까지의 거리와 화면 가장자리 여백(프리팹 값이 원본)
    [SerializeField] Vector2 cursorOffset = new Vector2(16f, -16f);
    [SerializeField] float screenMargin = 16f;

    internal void Show(string _title, Color _titleColor, System.Collections.Generic.IReadOnlyList<string> _lines, Vector2 _screenPosition)
    {
        titleLabel.text = _title;
        titleLabel.color = _titleColor;
        for (int i = 0; i < lineLabels.Length; i++)
        {
            bool used = i < _lines.Count;
            lineLabels[i].gameObject.SetActive(used);
            if (used)
            {
                lineLabels[i].text = _lines[i];
                lineLabels[i].color = Presentation.PuzzleHeading;
            }
        }
        card.gameObject.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        Place(_screenPosition);
    }

    internal void Hide()
    {
        card.gameObject.SetActive(false);
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
