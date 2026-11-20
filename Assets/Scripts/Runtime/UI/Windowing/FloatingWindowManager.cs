using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// FloatingWindowCanvas에 부착해 모든 부동 창의 스냅 대상, 일괄 초기화, 배경만 보기, Esc 닫기를 관리한다.
[DisallowMultipleComponent]
public sealed class FloatingWindowManager : MonoBehaviour
{
    readonly List<FloatingWindow> windows = new List<FloatingWindow>();
    readonly List<FloatingWindow> hiddenWindows = new List<FloatingWindow>();
    readonly List<Rect> scratchRects = new List<Rect>();
    readonly List<RaycastResult> raycastHits = new List<RaycastResult>();
    PointerEventData pointerData;

    bool backgroundOnly;

    public bool IsBackgroundOnly => backgroundOnly;

    void Awake()
    {
        DiscoverChildWindows();
    }

    // 창을 누르면(제목줄·버튼·보드 어디든) 그 창을 맨 앞으로 올린다. 마지막으로 누른 창이 가장 위에 겹친다.
    // 창 안의 버튼·보드가 누름 이벤트를 먼저 받아 창의 OnPointerDown이 불리지 않는 경우가 있어, 누른 순간 포인터 아래의 가장 위 UI를 직접 찾는다.
    // 창 영역이 아니라 실제로 가장 위에 그려진 UI를 기준으로 한다. 수첩 지도 크게 보기 레이어처럼 창 위를 덮은 화면을 누르면 그 아래 창을 올리지 않는다.
    void Update()
    {
        var pointer = Pointer.current;
        var eventSystem = EventSystem.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame || !eventSystem)
        {
            return;
        }
        pointerData ??= new PointerEventData(eventSystem);
        pointerData.Reset();
        pointerData.position = pointer.position.ReadValue();
        raycastHits.Clear();
        eventSystem.RaycastAll(pointerData, raycastHits);
        if (raycastHits.Count == 0 || !raycastHits[0].gameObject)
        {
            return;
        }
        var top = raycastHits[0].gameObject.GetComponentInParent<FloatingWindow>();
        if (top && windows.Contains(top) && top.IsOpen &&
            top.transform.GetSiblingIndex() != top.transform.parent.childCount - 1)
        {
            top.BringToFront();
        }
    }

    // 열린 창을 위(마지막으로 누르거나 연 창)에서 아래 순서로. 창을 누르면 맨 앞으로 오므로 형제 순서가 곧 포커스 순서다.
    readonly List<FloatingWindow> focusOrderBuffer = new List<FloatingWindow>();

    internal IReadOnlyList<FloatingWindow> OpenWindowsTopFirst()
    {
        focusOrderBuffer.Clear();
        for (int i = 0; i < windows.Count; i++)
        {
            var win = windows[i];
            if (win && win.IsOpen && win.gameObject.activeInHierarchy)
            {
                focusOrderBuffer.Add(win);
            }
        }
        focusOrderBuffer.Sort((_a, _b) => _b.transform.GetSiblingIndex().CompareTo(_a.transform.GetSiblingIndex()));
        return focusOrderBuffer;
    }

    // 화면 좌표 아래에서 가장 위에 그려진 열린 창(형제 순서가 뒤일수록 위). 없으면 null.
    // 화물 끌어 놓기는 이 창 안의 보드에만 들어가야 한다(겹친 창 뒤의 보드로 들어가지 않게).
    internal FloatingWindow TopWindowAt(Vector2 _screenPosition)
    {
        var canvas = GetComponentInParent<Canvas>();
        var root = canvas ? canvas.rootCanvas : null;
        Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
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
            if (index > topIndex &&
                RectTransformUtility.RectangleContainsScreenPoint((RectTransform)win.transform, _screenPosition, cam))
            {
                top = win;
                topIndex = index;
            }
        }
        return top;
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
