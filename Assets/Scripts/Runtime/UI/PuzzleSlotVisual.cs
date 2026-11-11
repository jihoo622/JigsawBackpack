using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PuzzleSlotVisual : MonoBehaviour
{
    internal const int DIRECTION_COUNT = 4;

    // 북쪽부터 시계방향으로 배치한 이미지다.
    [SerializeField] Image[] fillDirections;
    [SerializeField] Image[] outlineDirections;
    [SerializeField] Image itemIcon;

    Sprite[] fillSprites;
    Sprite outlineSprite;

    RectTransform Rect => (RectTransform)transform;

    internal bool IsConfigured(Sprite _reference)
    {
        return Rect.rect.size == Vector2.one * _reference.pixelsPerUnit &&
            HasDirections(fillDirections, _reference) && HasDirections(outlineDirections, _reference) &&
            itemIcon && itemIcon.rectTransform.parent == transform && !itemIcon.raycastTarget;
    }

    bool HasDirections(Image[] _images, Sprite _reference)
    {
        return _images != null && _images.Length == DIRECTION_COUNT && _images.Distinct().Count() == DIRECTION_COUNT &&
            _images.All(image => image && image.rectTransform.parent == transform &&
                image.rectTransform.rect.size == _reference.rect.size);
    }

    internal void ConfigureSprites(Sprite _outlineSprite, Sprite[] _fillSprites = null)
    {
        outlineSprite = _outlineSprite;
        fillSprites = _fillSprites;
    }

    internal void Show(Vector2 _center, float _cellSize, Color _color, int _hiddenEdges, Sprite _icon,
        Vector2 _iconOffset, Vector2 _iconSize, int _clockwiseTurns)
    {
        Rect.localPosition = _center;
        Rect.localScale = Vector3.one * (_cellSize / Rect.rect.width);

        bool hasFill = fillSprites != null;
        var outlineColor = hasFill ? Presentation.PuzzlePieceOutline(_color.a) : _color;
        for (int direction = 0; direction < DIRECTION_COUNT; direction++)
        {
            var fill = fillDirections[direction];
            fill.enabled = hasFill;
            if (hasFill)
            {
                fill.sprite = fillSprites[direction];
                fill.color = _color;
            }

            var outline = outlineDirections[direction];
            outline.enabled = (_hiddenEdges & (1 << direction)) == 0;
            if (outline.enabled)
            {
                outline.sprite = outlineSprite;
                outline.color = outlineColor;
            }
        }

        itemIcon.sprite = _icon;
        itemIcon.enabled = hasFill && _icon;
        itemIcon.preserveAspect = true;
        itemIcon.color = Presentation.PuzzleItemIcon(_color.a);
        itemIcon.rectTransform.localPosition = _iconOffset * Rect.rect.width;
        itemIcon.rectTransform.sizeDelta = _iconSize * Rect.rect.width;
        itemIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f * _clockwiseTurns);
        gameObject.SetActive(true);
    }

    internal void Hide()
    {
        gameObject.SetActive(false);
    }
}
