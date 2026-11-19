using System.Collections.Generic;
using UnityEngine;

// FloatingWindowCanvas에 부착해 모든 부동 창의 스냅 대상, 일괄 초기화, 배경만 보기, Esc 닫기를 관리한다.
[DisallowMultipleComponent]
public sealed class FloatingWindowManager : MonoBehaviour
{
    readonly List<FloatingWindow> windows = new List<FloatingWindow>();
    readonly List<FloatingWindow> hiddenWindows = new List<FloatingWindow>();
    readonly List<Rect> scratchRects = new List<Rect>();

    bool backgroundOnly;

    public bool IsBackgroundOnly => backgroundOnly;

    void Awake()
    {
        DiscoverChildWindows();
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
            if (!win || !win.IsOpen || !win.gameObject.activeInHierarchy)
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
