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
        // 열린 창은 아래(먼저 연 창)부터 배치 규칙으로 다시 놓는다. 이미 다시 놓은 창만 피한다.
        var placed = new List<Rect>();
        var open = new List<FloatingWindow>(OpenWindowsTopFirst());
        for (int i = open.Count - 1; i >= 0; i--)
        {
            open[i].PlaceAutomatically(placed);
            placed.Add(open[i].CurrentRect);
        }
    }

    // ---- 창 배치 규칙(H10) ----
    // 개인 창(화차·렌즈·수첩)은 왼쪽 플랫폼 위, 마을 창(시장·술집·여관·역사·생산시설·시청)은 오른쪽 마을 위에 뜬다.
    // 가운데 선로(x 549~640)는 가리지 않는다. 좌표는 1920x1080 기준 좌상단 원점(FloatingWindowMath와 같음).
    // 1) 기본 위치가 비어 있으면 거기. 기본 위치는 마을 창이면 마을 영역 오른쪽 위(폭이 달라도 오른쪽 끝을 맞춤),
    //    개인 창이면 빌더가 정한 defaultPosition(화차 창 슬롯·렌즈·수첩).
    // 2) 아니면 자기 영역에서 다른 열린 창과 겹치지 않는 첫 자리(24px 간격으로 위에서 아래로, 개인은 왼쪽부터·마을은 오른쪽부터).
    //    개인 창은 왼쪽 영역에 자리가 없으면 마을 영역도 찾는다(화차 창이 여러 개일 때, R29).
    // 3) 그래도 없으면 기본 위치에서 24px씩 계단으로 비켜(마을은 왼쪽 아래, 개인은 오른쪽 아래) 다른 창과 왼쪽 위 모서리가 겹치지 않는 자리.
    // 사용자가 끌어 옮긴 창(FloatingWindow.UserPlaced)은 이 규칙을 쓰지 않고 그 자리를 지킨다.
    static readonly Rect PersonalArea = new Rect(16f, 128f, 524f, 936f);
    static readonly Rect TownArea = new Rect(653f, 134f, 1247f, 925f);
    const float PLACEMENT_STEP = 24f;

    static bool IsTownWindow(FloatingWindow _window)
    {
        switch (_window.WindowId)
        {
            case "market":
            case "tavern":
            case "rest":
            case "station_hall":
            case "workshop":
            case "city_hall":
                return true;
            default:
                return false;
        }
    }

    internal Vector2 FindOpenPosition(FloatingWindow _window, IReadOnlyList<Rect> _occupied)
    {
        var occupied = _occupied ?? GetOtherOpenRects(_window);
        Vector2 size = _window.DefaultSize;
        bool town = IsTownWindow(_window);
        var preferred = town
            ? new Rect(TownArea.xMax - size.x, TownArea.yMin, size.x, size.y)
            : new Rect(_window.DefaultPosition, size);
        if (!Overlaps(preferred, occupied))
        {
            return preferred.position;
        }
        if (TryFindFreeSpot(town ? TownArea : PersonalArea, size, occupied, town, out var spot) ||
            (!town && TryFindFreeSpot(TownArea, size, occupied, true, out spot)))
        {
            return spot;
        }
        for (int k = 1; k <= 40; k++)
        {
            // 마을 창은 오른쪽 끝에 붙어 있으므로 왼쪽 아래로, 개인 창은 오른쪽 아래로 비켜 계단을 만든다.
            float dx = town ? -k * PLACEMENT_STEP : k * PLACEMENT_STEP;
            var candidate = new Vector2(
                Mathf.Clamp(preferred.x + dx, 0f, FloatingWindowMath.ReferenceWidth - size.x),
                Mathf.Min(preferred.y + k * PLACEMENT_STEP, FloatingWindowMath.ReferenceHeight - size.y));
            bool sameCorner = false;
            foreach (var rect in occupied)
            {
                if (Mathf.Abs(rect.x - candidate.x) < PLACEMENT_STEP * 0.5f && Mathf.Abs(rect.y - candidate.y) < PLACEMENT_STEP * 0.5f)
                {
                    sameCorner = true;
                    break;
                }
            }
            if (!sameCorner)
            {
                return candidate;
            }
        }
        return preferred.position;
    }

    static bool TryFindFreeSpot(Rect _area, Vector2 _size, IReadOnlyList<Rect> _occupied, bool _fromRight, out Vector2 _spot)
    {
        _spot = default;
        if (_size.x > _area.width || _size.y > _area.height)
        {
            return false;
        }
        for (float y = _area.yMin; y + _size.y <= _area.yMax + 0.5f; y += PLACEMENT_STEP)
        {
            int columns = Mathf.FloorToInt((_area.width - _size.x) / PLACEMENT_STEP);
            for (int c = 0; c <= columns; c++)
            {
                float x = _fromRight ? _area.xMax - _size.x - c * PLACEMENT_STEP : _area.xMin + c * PLACEMENT_STEP;
                var candidate = new Rect(x, y, _size.x, _size.y);
                if (!Overlaps(candidate, _occupied))
                {
                    _spot = candidate.position;
                    return true;
                }
            }
        }
        return false;
    }

    static bool Overlaps(Rect _rect, IReadOnlyList<Rect> _others)
    {
        for (int i = 0; i < _others.Count; i++)
        {
            if (_rect.Overlaps(_others[i]))
            {
                return true;
            }
        }
        return false;
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
