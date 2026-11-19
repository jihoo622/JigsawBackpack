using System.Collections.Generic;
using UnityEngine;

// 부동 창 시스템의 순수 좌표·스냅·클램프 수학 유틸리티(1920×1080 좌상단 원점 기준).
public static class FloatingWindowMath
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;
    public const float TopHudSafeY = 76f;
    public const float DefaultSnapDistance = 12f;

    // 화면 영역(1920×1080)과 상단 HUD 안전선(y >= 76) 안으로 사각형을 제한한다.
    public static Rect ClampRect(
        Rect _rect,
        float _minY = TopHudSafeY,
        float _screenW = ReferenceWidth,
        float _screenH = ReferenceHeight)
    {
        float w = Mathf.Clamp(_rect.width, 1f, _screenW);
        float h = Mathf.Clamp(_rect.height, 1f, Mathf.Max(1f, _screenH - _minY));
        float x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, _screenW - w));
        float y = Mathf.Clamp(_rect.y, _minY, Mathf.Max(_minY, _screenH - h));
        return new Rect(x, y, w, h);
    }

    // 이동 중인 창을 화면 가장자리 및 다른 열린 창의 외곽선에 12px 이내로 자석 스냅한다.
    public static Rect SnapMoveRect(
        Rect _rect,
        IReadOnlyList<Rect> _others,
        float _snapDistance = DefaultSnapDistance,
        float _minY = TopHudSafeY,
        float _screenW = ReferenceWidth,
        float _screenH = ReferenceHeight)
    {
        float bestDx = 0f;
        float bestAbsDx = _snapDistance + 0.001f;

        TrySnapAxis(_rect.xMin, 0f, ref bestDx, ref bestAbsDx);
        TrySnapAxis(_rect.xMax, _screenW, ref bestDx, ref bestAbsDx);
        if (_others != null)
        {
            for (int i = 0; i < _others.Count; i++)
            {
                Rect o = _others[i];
                TrySnapAxis(_rect.xMin, o.xMin, ref bestDx, ref bestAbsDx);
                TrySnapAxis(_rect.xMin, o.xMax, ref bestDx, ref bestAbsDx);
                TrySnapAxis(_rect.xMax, o.xMin, ref bestDx, ref bestAbsDx);
                TrySnapAxis(_rect.xMax, o.xMax, ref bestDx, ref bestAbsDx);
            }
        }

        float bestDy = 0f;
        float bestAbsDy = _snapDistance + 0.001f;

        TrySnapAxis(_rect.yMin, _minY, ref bestDy, ref bestAbsDy);
        TrySnapAxis(_rect.yMax, _screenH, ref bestDy, ref bestAbsDy);
        if (_others != null)
        {
            for (int i = 0; i < _others.Count; i++)
            {
                Rect o = _others[i];
                TrySnapAxis(_rect.yMin, o.yMin, ref bestDy, ref bestAbsDy);
                TrySnapAxis(_rect.yMin, o.yMax, ref bestDy, ref bestAbsDy);
                TrySnapAxis(_rect.yMax, o.yMin, ref bestDy, ref bestAbsDy);
                TrySnapAxis(_rect.yMax, o.yMax, ref bestDy, ref bestAbsDy);
            }
        }

        var moved = new Rect(_rect.x + bestDx, _rect.y + bestDy, _rect.width, _rect.height);
        return ClampRect(moved, _minY, _screenW, _screenH);
    }

    // RectTransform을 1920×1080 좌상단 원점 사각형으로 변환한다.
    public static Rect ToTopLeftRect(
        RectTransform _rect,
        float _screenW = ReferenceWidth,
        float _screenH = ReferenceHeight)
    {
        Vector2 parentSize = GetParentReferenceSize(_rect, _screenW, _screenH);
        Vector2 bl = new Vector2(_rect.anchorMin.x * parentSize.x, _rect.anchorMin.y * parentSize.y) + _rect.offsetMin;
        Vector2 tr = new Vector2(_rect.anchorMax.x * parentSize.x, _rect.anchorMax.y * parentSize.y) + _rect.offsetMax;
        float w = tr.x - bl.x;
        float h = tr.y - bl.y;
        float x = bl.x;
        float y = parentSize.y - tr.y;
        return new Rect(x, y, w, h);
    }

    // 1920×1080 좌상단 원점 사각형을 RectTransform의 앵커/피벗에 맞게 적용한다.
    public static void ApplyTopLeftRect(
        RectTransform _rect,
        Rect _topLeftRect,
        float _screenW = ReferenceWidth,
        float _screenH = ReferenceHeight)
    {
        Vector2 parentSize = GetParentReferenceSize(_rect, _screenW, _screenH);
        Vector2 bl = new Vector2(_topLeftRect.xMin, parentSize.y - _topLeftRect.yMax);
        Vector2 tr = new Vector2(_topLeftRect.xMax, parentSize.y - _topLeftRect.yMin);
        _rect.offsetMin = bl - new Vector2(_rect.anchorMin.x * parentSize.x, _rect.anchorMin.y * parentSize.y);
        _rect.offsetMax = tr - new Vector2(_rect.anchorMax.x * parentSize.x, _rect.anchorMax.y * parentSize.y);
    }

    public static Vector2 GetParentReferenceSize(
        RectTransform _rect,
        float _fallbackW = ReferenceWidth,
        float _fallbackH = ReferenceHeight)
    {
        if (_rect && _rect.parent is RectTransform parentRt &&
            parentRt.rect.width > 0f && parentRt.rect.height > 0f)
        {
            return parentRt.rect.size;
        }
        return new Vector2(_fallbackW, _fallbackH);
    }

    static void TrySnapAxis(float _source, float _target, ref float _bestDelta, ref float _bestAbs)
    {
        float delta = _target - _source;
        float abs = Mathf.Abs(delta);
        if (abs < _bestAbs)
        {
            _bestAbs = abs;
            _bestDelta = delta;
        }
    }
}
