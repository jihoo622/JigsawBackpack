using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Relayout
{
    public sealed class FloatingWindowMathTests
    {
        [Test]
        public void ClampRect_EnforcesParentBoundsAndTopReserve76()
        {
            // Above top reserve (y < 76) and outside left edge (x < 0)
            var topLeft = new Rect(-50f, 10f, 400f, 300f);
            var clampedTopLeft = FloatingWindowMath.ClampRect(topLeft);
            Assert.AreEqual(0f, clampedTopLeft.x, 0.001f);
            Assert.AreEqual(76f, clampedTopLeft.y, 0.001f);
            Assert.AreEqual(400f, clampedTopLeft.width, 0.001f);
            Assert.AreEqual(300f, clampedTopLeft.height, 0.001f);

            // Outside right and bottom edges of 1920x1080
            var bottomRight = new Rect(1800f, 950f, 400f, 300f);
            var clampedBottomRight = FloatingWindowMath.ClampRect(bottomRight);
            Assert.AreEqual(1520f, clampedBottomRight.x, 0.001f);
            Assert.AreEqual(780f, clampedBottomRight.y, 0.001f);

            // Oversized rect clamped to parent minus top reserve
            var oversized = new Rect(0f, 0f, 2500f, 1500f);
            var clampedOversized = FloatingWindowMath.ClampRect(oversized);
            Assert.AreEqual(1920f, clampedOversized.width, 0.001f);
            Assert.AreEqual(1080f - 76f, clampedOversized.height, 0.001f);
            Assert.AreEqual(76f, clampedOversized.y, 0.001f);
        }

        [Test]
        public void SnapMoveRect_SnapsWithin12PxToEdgesAndOtherWindows()
        {
            // Snap to left edge (x=8 <= 12) and top reserve (y=86, delta=-10 <= 12)
            var nearTopLeft = new Rect(8f, 86f, 400f, 300f);
            var snappedEdges = FloatingWindowMath.SnapMoveRect(nearTopLeft, null);
            Assert.AreEqual(0f, snappedEdges.x, 0.001f);
            Assert.AreEqual(76f, snappedEdges.y, 0.001f);

            // Snap to right edge and bottom edge
            var nearBottomRight = new Rect(1920f - 400f - 11f, 1080f - 300f - 12f, 400f, 300f);
            var snappedBR = FloatingWindowMath.SnapMoveRect(nearBottomRight, null);
            Assert.AreEqual(1920f - 400f, snappedBR.x, 0.001f);
            Assert.AreEqual(1080f - 300f, snappedBR.y, 0.001f);

            // Outside 12px threshold: should not snap
            var outsideThreshold = new Rect(13f, 89f, 400f, 300f);
            var notSnapped = FloatingWindowMath.SnapMoveRect(outsideThreshold, null);
            Assert.AreEqual(13f, notSnapped.x, 0.001f);
            Assert.AreEqual(89f, notSnapped.y, 0.001f);

            // Snap to another window's right edge (xMax=500) and top edge (yMin=200)
            var others = new List<Rect> { new Rect(100f, 200f, 400f, 300f) };
            var nearOther = new Rect(509f, 210f, 300f, 250f);
            var snappedOther = FloatingWindowMath.SnapMoveRect(nearOther, others);
            Assert.AreEqual(500f, snappedOther.x, 0.001f);
            Assert.AreEqual(200f, snappedOther.y, 0.001f);
        }

        [Test]
        public void LargerThan1920x1080Parent_UsesParentRectSizeForClampSnapAndRoundTrip()
        {
            const float bigW = 2560f;
            const float bigH = 1440f;

            // Clamp & Snap allow coordinates beyond 1920x1080 when parent is 2560x1440
            var rectInBigParent = new Rect(2150f, 1130f, 400f, 300f);
            var clamped = FloatingWindowMath.ClampRect(rectInBigParent, FloatingWindowMath.TopHudSafeY, bigW, bigH);
            Assert.AreEqual(2150f, clamped.x, 0.001f);
            Assert.AreEqual(1130f, clamped.y, 0.001f);

            var snapped = FloatingWindowMath.SnapMoveRect(rectInBigParent, null, FloatingWindowMath.DefaultSnapDistance,
                FloatingWindowMath.TopHudSafeY, bigW, bigH);
            Assert.AreEqual(bigW - 400f, snapped.x, 0.001f);
            Assert.AreEqual(bigH - 300f, snapped.y, 0.001f);

            // Verify GetParentReferenceSize, ApplyTopLeftRect, ToTopLeftRect with an actual RectTransform > 1920x1080
            var parentGo = new GameObject("BigParent", typeof(RectTransform));
            var childGo = new GameObject("ChildWin", typeof(RectTransform));
            try
            {
                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.sizeDelta = new Vector2(bigW, bigH);
                parentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bigW);
                parentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bigH);

                var childRt = childGo.GetComponent<RectTransform>();
                childRt.SetParent(parentRt, false);
                childRt.anchorMin = Vector2.zero;
                childRt.anchorMax = Vector2.zero;

                Vector2 refSize = FloatingWindowMath.GetParentReferenceSize(childRt);
                Assert.AreEqual(bigW, refSize.x, 0.001f);
                Assert.AreEqual(bigH, refSize.y, 0.001f);

                var target = new Rect(2100f, 1000f, 440f, 430f);
                FloatingWindowMath.ApplyTopLeftRect(childRt, target);
                var roundTrip = FloatingWindowMath.ToTopLeftRect(childRt);
                Assert.AreEqual(target.x, roundTrip.x, 0.01f);
                Assert.AreEqual(target.y, roundTrip.y, 0.01f);
                Assert.AreEqual(target.width, roundTrip.width, 0.01f);
                Assert.AreEqual(target.height, roundTrip.height, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(childGo);
                Object.DestroyImmediate(parentGo);
            }
        }
    }
}
