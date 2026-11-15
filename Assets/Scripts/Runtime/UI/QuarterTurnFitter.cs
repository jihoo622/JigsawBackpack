using UnityEngine;
using UnityEngine.EventSystems;

// 90도 돌린 자식 그림을 이 상자에 꼭 맞춘다. 돌린 RectTransform은 앵커로 늘일 수 없으므로 상자 크기가 바뀔 때 가로·세로를 바꿔 넣는다.
// 상자는 부모 카드에 앵커로 늘여 두고, 자식 Image의 preserveAspect가 종횡비를 지킨다.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class QuarterTurnFitter : UIBehaviour
{
    [SerializeField] RectTransform target;

    DrivenRectTransformTracker tracker;

    protected override void OnEnable()
    {
        base.OnEnable();
        Fit();
    }

    protected override void OnDisable()
    {
        tracker.Clear();
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        Fit();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        Fit();
    }
#endif

    void Fit()
    {
        if (!target || !IsActive())
        {
            return;
        }
        tracker.Clear();
        tracker.Add(this, target, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition |
            DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Pivot);
        Rect box = ((RectTransform)transform).rect;
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = Vector2.zero;
        target.sizeDelta = new Vector2(box.height, box.width);
    }
}
