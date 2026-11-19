using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// FloatingWindowCanvas에 부착해 모든 부동 창의 스냅 대상, 일괄 초기화, 배경만 보기, Esc 닫기를 관리한다.
[DisallowMultipleComponent]
public sealed class FloatingWindowManager : MonoBehaviour
{
    readonly List<FloatingWindow> windows = new List<FloatingWindow>();
    readonly List<FloatingWindow> hiddenWindows = new List<FloatingWindow>();
    readonly List<Rect> scratchRects = new List<Rect>();

    bool backgroundOnly;

    public bool IsBackgroundOnly => backgroundOnly;

    Canvas rootCanvas;

    void Awake()
    {
        DiscoverChildWindows();
        var canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas ? canvas.rootCanvas : null;
    }

    // 창을 누르면(제목줄·버튼·보드 어디든) 그 창을 맨 앞으로 올린다. 마지막으로 누른 창이 가장 위에 겹친다.
    // 창 안의 버튼·보드가 누름 이벤트를 먼저 받아 창의 OnPointerDown이 불리지 않는 경우가 있어, 누른 순간 포인터 아래의 가장 위 창을 직접 찾는다.
    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
        {
            return;
        }
        Vector2 screenPos = pointer.position.ReadValue();
        Camera cam = rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        FloatingWindow top = null;
        int topIndex = -1;
        for (int i = 0; i < windows.Count; i++)
        {
            var win = windows[i];
            if (!win || !win.IsOpen || !win.gameObject.activeInHierarchy)
            {
                continue;
            }
            int index = win.transform.GetSiblingIndex();
            if (index > topIndex && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)win.transform, screenPos, cam))
            {
                top = win;
                topIndex = index;
            }
        }
        if (top && topIndex != top.transform.parent.childCount - 1)
        {
            top.BringToFront();
        }
    }

    public void Register(FloatingWindow _window)
    {
        if (_window && !windows.Contains(_window))
        {
            windows.Add(_window);
        }
    }

    public void Unregister(FloatingWindow _window)
    {
        windows.Remove(_window);
    }

    public IReadOnlyList<Rect> GetOtherOpenRects(FloatingWindow _exclude)
    {
        scratchRects.Clear();
        for (int i = 0; i < windows.Count; i++)
        {
            var win = windows[i];
            if (!win || win == _exclude || !win.IsOpen || !win.gameObject.activeInHierarchy)
            {
                continue;
            }
            scratchRects.Add(win.CurrentRect);
        }
        return scratchRects;
    }

    public void ResetAllLayouts()
    {
        DiscoverChildWindows();
        if (backgroundOnly)
        {
            ToggleBackgroundOnly();
        }
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i])
            {
                windows[i].ResetLayout();
            }
        }
    }

    // R18: 열린 창만 일시 숨기고 열림 상태를 기억한 뒤 다시 호출하면 복원한다.
    public void ToggleBackgroundOnly()
    {
        DiscoverChildWindows();
        backgroundOnly = !backgroundOnly;
        if (backgroundOnly)
        {
            hiddenWindows.Clear();
            for (int i = 0; i < windows.Count; i++)
            {
                var win = windows[i];
                if (win && win.IsOpen)
                {
                    hiddenWindows.Add(win);
                }
            }
            for (int i = 0; i < hiddenWindows.Count; i++)
            {
                if (hiddenWindows[i])
                {
                    hiddenWindows[i].SetHiddenForBackgroundOnly(true);
                }
            }
        }
        else
        {
            for (int i = 0; i < hiddenWindows.Count; i++)
            {
                if (hiddenWindows[i])
                {
                    hiddenWindows[i].SetHiddenForBackgroundOnly(false);
                }
            }
            hiddenWindows.Clear();
        }
    }

    // 가장 위(SiblingIndex 최대)에 있는 열린 창을 닫는다. 열린 창이 없으면 false.
    public bool TryConsumeCancel()
    {
        if (backgroundOnly)
        {
            return false;
        }

        FloatingWindow topMost = null;
        int topIndex = -1;
        for (int i = 0; i < windows.Count; i++)
        {
            var win = windows[i];
            if (!win || !win.IsOpen || !win.gameObject.activeInHierarchy || win.CloseBlocked)
            {
                continue;
            }
            int sibling = win.transform.GetSiblingIndex();
            if (sibling > topIndex)
            {
                topIndex = sibling;
                topMost = win;
            }
        }

        if (!topMost)
        {
            return false;
        }

        topMost.Close();
        return true;
    }

    void DiscoverChildWindows()
    {
        var found = GetComponentsInChildren<FloatingWindow>(true);
        for (int i = 0; i < found.Length; i++)
        {
            Register(found[i]);
        }
    }
}
