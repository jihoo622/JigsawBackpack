using System;
using System.Collections.Generic;
using UnityEngine;

// S3b: 다중 기차 화차 창(H18, R25, R29) 관리. 최대 ui.maxTrainWindows개까지 동시 오픈, LRU 교체, 슬롯 배치, 열림 상태 영속성.
[DisallowMultipleComponent]
public sealed class TrainWindowManager : MonoBehaviour
{
    const string OPEN_WAGONS_KEY = "ui.train.openWagons";
    const float CASCADE_OFFSET = 24f;
    const float SLOT_OCCUPIED_TOLERANCE = 24f;
    const float DEFAULT_WINDOW_WIDTH = 510f;
    const float DEFAULT_WINDOW_HEIGHT = 544f;

    // 목업 concept-3b 배치: 1번째 창은 왼쪽 하단 (26, 516)에 정확히 놓는다.
    // 2번째 이후는 마을 상단(x 653부터, y 134)에 가로로 타일링하고, 슬롯이 모두 차면 24px 계단식으로 이어진다.
    // 1920 폭에는 510px 창이 2열까지만 들어가므로 마을 상단 슬롯은 2개만 둔다.
    static readonly Vector2[] FirstOpenSlots =
    {
        new Vector2(26f, 516f),
        new Vector2(653f, 134f),
        new Vector2(1173f, 134f),
    };

    [SerializeField] TrainWindowView[] windows;

    readonly List<TrainWindowView> openSortBuffer = new();
    readonly HashSet<PuzzleInventory> seenWagonBuffer = new();
    long[] focusOrder;
    long focusCounter;
    int lastOpenedIndex = -1;
    bool restoredOpenWagons;
    // 런 시작 직후 첫 복원이 실패하면 창 계층이 준비될 때까지 다음 프레임에 다시 시도한다.
    PuzzleInventoryService pendingRestoreSession;
    string restoredRunId = string.Empty;
    bool suppressOpenChanged;
    Canvas rootCanvas;

    public IReadOnlyList<TrainWindowView> Windows => windows;
    public int MaxWindowCount
    {
        get
        {
            int limit = BasicData.Int("ui.maxTrainWindows");
            if (limit <= 0) limit = 16;
            return windows != null ? Mathf.Clamp(limit, 1, windows.Length) : 0;
        }
    }

    Camera EventCamera
    {
        get
        {
            if (!rootCanvas)
            {
                var c = GetComponentInParent<Canvas>();
                rootCanvas = c ? c.rootCanvas : null;
            }
            return rootCanvas && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        }
    }

    public event Action<int> WagonFocused;
    public event Action OpenWindowsChanged;
    public event Action LensToggleRequested;

    void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c ? c.rootCanvas : null;
        EnsureFocusArray();
        if (windows != null)
        {
            for (int i = 0; i < windows.Length; i++)
            {
                int idx = i;
                if (windows[i] && windows[i].FloatingWindow)
                {
                    windows[i].FloatingWindow.Focused += () => OnWindowFocused(idx);
                    windows[i].FloatingWindow.OpenChanged += _open => OnWindowOpenChanged(idx, _open);
                }
                if (windows[i])
                {
                    windows[i].LensRequested += OnWindowLensRequested;
                }
            }
        }
    }

    void OnWindowLensRequested()
    {
        LensToggleRequested?.Invoke();
    }

    void EnsureFocusArray()
    {
        int len = windows != null ? windows.Length : 0;
        if (focusOrder == null || focusOrder.Length != len)
        {
            focusOrder = new long[len];
        }
    }

    void OnWindowFocused(int _windowIndex)
    {
        EnsureFocusArray();
        if (_windowIndex < 0 || _windowIndex >= focusOrder.Length)
        {
            return;
        }
        focusOrder[_windowIndex] = ++focusCounter;
        var win = windows[_windowIndex];
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        int wagonIdx = win ? IndexOfWagon(session, win.BoundWagon) : -1;
        if (wagonIdx >= 0)
        {
            win.BoundWagonIndex = wagonIdx;
            WagonFocused?.Invoke(wagonIdx);
        }
    }

    void OnWindowOpenChanged(int _windowIndex, bool _open)
    {
        if (suppressOpenChanged || windows == null || _windowIndex < 0 || _windowIndex >= windows.Length)
        {
            return;
        }
        var win = windows[_windowIndex];
        if (!_open && win)
        {
            win.BoundWagon = null;
            win.BoundWagonIndex = -1;
            var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
            SaveOpenWagons(session);
            OpenWindowsChanged?.Invoke();
        }
    }

    public static Vector2 GetDefaultSlotPosition(int _slotIndex)
    {
        if (_slotIndex >= 0 && _slotIndex < FirstOpenSlots.Length)
        {
            return FirstOpenSlots[_slotIndex];
        }
        int extra = Mathf.Max(0, _slotIndex - FirstOpenSlots.Length + 1);
        Vector2 anchor = FirstOpenSlots[FirstOpenSlots.Length - 1];
        return new Vector2(anchor.x + extra * CASCADE_OFFSET, anchor.y + extra * CASCADE_OFFSET);
    }

    public TrainWindowView OpenOrFocusWagon(int _wagonIndex, PuzzleInventoryService _session, bool _persist = true,
        PuzzleInventory _protectedSource = null, PuzzleInventoryView _protectedTarget = null)
    {
        if (_session == null || _wagonIndex < 0 || _wagonIndex >= _session.WagonCount)
        {
            return null;
        }
        return OpenOrFocusWagon(_session.TrainWagons[_wagonIndex], _session, _persist, _protectedSource, _protectedTarget);
    }

    public TrainWindowView OpenOrFocusWagon(PuzzleInventory _wagon, PuzzleInventoryService _session, bool _persist = true,
        PuzzleInventory _protectedSource = null, PuzzleInventoryView _protectedTarget = null)
    {
        if (windows == null || windows.Length == 0 || _session == null || _wagon == null)
        {
            return null;
        }
        int wagonIndex = IndexOfWagon(_session, _wagon);
        if (wagonIndex < 0)
        {
            return null;
        }

        EnsureFocusArray();
        int cap = MaxWindowCount;

        // 1) 이미 해당 화차를 표시 중인 열린 창이 있으면 최상위로 올린다(화차당 최대 1창).
        for (int i = 0; i < cap; i++)
        {
            var win = windows[i];
            if (win && win.FloatingWindow && win.FloatingWindow.IsOpen && ReferenceEquals(win.BoundWagon, _wagon))
            {
                win.BoundWagonIndex = wagonIndex;
                win.FloatingWindow.BringToFront();
                focusOrder[i] = ++focusCounter;
                return win;
            }
        }

        // 2) 닫혀 있는 여유 창이 있으면 R29 슬롯(또는 24px 계단) 위치로 연다.
        for (int i = 0; i < cap; i++)
        {
            var win = windows[i];
            if (win && win.FloatingWindow && !win.FloatingWindow.IsOpen)
            {
                Vector2 targetPos = ComputeNextWindowPosition(win);
                BindAndOpen(i, _wagon, wagonIndex, targetPos, _session, _persist);
                return win;
            }
        }

        // 3) 상한에 도달했으면 드래그 출발/대상 창을 제외한 가장 오래 안 만진(LRU) 창을 전환한다.
        int lruIndex = -1;
        long minOrder = long.MaxValue;
        for (int i = 0; i < cap; i++)
        {
            var candidate = windows[i];
            if (!candidate || !candidate.FloatingWindow)
            {
                continue;
            }
            if ((_protectedSource != null && ReferenceEquals(candidate.BoundWagon, _protectedSource)) ||
                (_protectedTarget != null && candidate.InventoryView == _protectedTarget))
            {
                continue;
            }
            if (focusOrder[i] < minOrder)
            {
                minOrder = focusOrder[i];
                lruIndex = i;
            }
        }

        if (lruIndex < 0)
        {
            // 보호 대상 창 외에 교체 가능한 창이 없으면 보류한다.
            return null;
        }

        var lruWin = windows[lruIndex];
        if (lruWin && lruWin.FloatingWindow)
        {
            lruWin.BoundWagon = _wagon;
            lruWin.BoundWagonIndex = wagonIndex;
            BindWindowToWagon(lruWin, _wagon, _session);
            lruWin.FloatingWindow.BringToFront();
            focusOrder[lruIndex] = ++focusCounter;
            lastOpenedIndex = lruIndex;
            if (_persist) SaveOpenWagons(_session);
            OpenWindowsChanged?.Invoke();
            return lruWin;
        }

        return null;
    }

    Vector2 ComputeNextWindowPosition(TrainWindowView _targetWindow)
    {
        int cap = MaxWindowCount;
        // 1~8번째 빈 슬롯 중 이미 열린 창이 점유하지 않은 첫 슬롯을 선택한다.
        for (int s = 0; s < FirstOpenSlots.Length; s++)
        {
            Vector2 slot = FirstOpenSlots[s];
            bool occupied = false;
            for (int i = 0; i < cap; i++)
            {
                var w = windows[i];
                if (!w || !w.FloatingWindow || !w.FloatingWindow.IsOpen)
                {
                    continue;
                }
                var r = w.FloatingWindow.CurrentRect;
                if (Mathf.Abs(r.x - slot.x) <= SLOT_OCCUPIED_TOLERANCE &&
                    Mathf.Abs(r.y - slot.y) <= SLOT_OCCUPIED_TOLERANCE)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
            {
                return slot;
            }
        }

        // 8개 슬롯이 모두 차 있으면 직전 창에서 +24px 계단식 오프셋을 적용한다.
        float winW = DEFAULT_WINDOW_WIDTH;
        float winH = DEFAULT_WINDOW_HEIGHT;
        if (_targetWindow && _targetWindow.FloatingWindow && _targetWindow.FloatingWindow.CurrentRect.width > 1f)
        {
            winW = _targetWindow.FloatingWindow.CurrentRect.width;
            winH = _targetWindow.FloatingWindow.CurrentRect.height;
        }
        if (lastOpenedIndex >= 0 && lastOpenedIndex < windows.Length)
        {
            var prev = windows[lastOpenedIndex];
            if (prev && prev.FloatingWindow && prev.FloatingWindow.IsOpen)
            {
                var r = prev.FloatingWindow.CurrentRect;
                float nextX = Mathf.Min(r.x + CASCADE_OFFSET, FloatingWindowMath.ReferenceWidth - winW);
                float nextY = Mathf.Min(r.y + CASCADE_OFFSET, FloatingWindowMath.ReferenceHeight - winH);
                return new Vector2(nextX, nextY);
            }
        }
        return FirstOpenSlots[0];
    }

    void BindAndOpen(int _windowIndex, PuzzleInventory _wagon, int _wagonIndex, Vector2 _pos,
        PuzzleInventoryService _session, bool _persist)
    {
        var win = windows[_windowIndex];
        win.BoundWagon = _wagon;
        win.BoundWagonIndex = _wagonIndex;
        suppressOpenChanged = true;
        try
        {
            win.FloatingWindow.SetOpen(true, _persist);
            win.FloatingWindow.SetPosition(_pos, _persist);
        }
        finally
        {
            suppressOpenChanged = false;
        }
        BindWindowToWagon(win, _wagon, _session);
        focusOrder[_windowIndex] = ++focusCounter;
        lastOpenedIndex = _windowIndex;
        if (_persist) SaveOpenWagons(_session);
        OpenWindowsChanged?.Invoke();
    }

    void LateUpdate()
    {
        if (pendingRestoreSession != null && !restoredOpenWagons)
        {
            RestoreOrEnsureDefault(pendingRestoreSession);
        }
    }

    public void RestoreOrEnsureDefault(PuzzleInventoryService _session)
    {
        if (_session == null || windows == null || windows.Length == 0)
        {
            return;
        }
        if (!restoredOpenWagons || !string.Equals(restoredRunId, _session.RunId, StringComparison.Ordinal))
        {
            restoredRunId = _session.RunId ?? string.Empty;
            RestoreOpenWagons(_session);
            if (!HasAnyOpenWindow && _session.WagonCount > 0)
            {
                OpenOrFocusWagon(Mathf.Clamp(_session.CurrentWagonIndex, 0, _session.WagonCount - 1), _session, false);
            }
            // 런 시작 직후에는 창 계층이 아직 켜지지 않아 열기가 실패할 수 있다. 실제로 창이 열렸을 때만 복원을 끝낸 것으로 본다.
            restoredOpenWagons = HasAnyOpenWindow;
            pendingRestoreSession = restoredOpenWagons ? null : _session;
        }

        int cap = MaxWindowCount;
        seenWagonBuffer.Clear();
        for (int i = 0; i < cap; i++)
        {
            var win = windows[i];
            if (!win || !win.FloatingWindow || !win.FloatingWindow.IsOpen)
            {
                continue;
            }
            int idx = IndexOfWagon(_session, win.BoundWagon);
            if (idx < 0 || !seenWagonBuffer.Add(win.BoundWagon))
            {
                CloseWindowSilently(win);
                continue;
            }
            win.BoundWagonIndex = idx;
        }
    }

    internal void RefreshAllOpenWindows(PuzzleInventoryService _session, long _remainingHours, decimal _synergyDelta,
        int? _hiddenInstanceId, int? _highlightedInstanceId,
        Func<PuzzleInventory, IReadOnlyList<PuzzleRefrigerationContact>> _contactsProvider)
    {
        if (windows == null || _session == null)
        {
            return;
        }
        bool closedAny = false;
        seenWagonBuffer.Clear();
        int cap = MaxWindowCount;
        for (int i = 0; i < cap; i++)
        {
            var win = windows[i];
            if (!win || !win.FloatingWindow || !win.FloatingWindow.IsOpen)
            {
                continue;
            }
            int idx = IndexOfWagon(_session, win.BoundWagon);
            if (idx < 0 || !seenWagonBuffer.Add(win.BoundWagon))
            {
                CloseWindowSilently(win);
                closedAny = true;
                continue;
            }
            win.BoundWagonIndex = idx;
            var wagon = win.BoundWagon;
            BindWindowToWagon(win, wagon, _session, _remainingHours);
            if (win.InventoryView)
            {
                win.InventoryView.RenderPlacements(wagon, _hiddenInstanceId, _highlightedInstanceId,
                    _contactsProvider != null ? _contactsProvider(wagon) : null);
            }
        }
        if (closedAny)
        {
            SaveOpenWagons(_session);
            OpenWindowsChanged?.Invoke();
        }
    }

    public void SavePersistence(PuzzleInventoryService _session)
    {
        SaveOpenWagons(_session);
    }

    void CloseWindowSilently(TrainWindowView _win)
    {
        if (!_win)
        {
            return;
        }
        _win.BoundWagon = null;
        _win.BoundWagonIndex = -1;
        if (_win.FloatingWindow)
        {
            suppressOpenChanged = true;
            try
            {
                _win.FloatingWindow.SetOpen(false, false);
            }
            finally
            {
                suppressOpenChanged = false;
            }
        }
    }

    static int IndexOfWagon(PuzzleInventoryService _session, PuzzleInventory _wagon)
    {
        if (_session == null || _wagon == null)
        {
            return -1;
        }
        var wagons = _session.TrainWagons;
        for (int i = 0; i < wagons.Count; i++)
        {
            if (ReferenceEquals(wagons[i], _wagon))
            {
                return i;
            }
        }
        return -1;
    }

    static void BindWindowToWagon(TrainWindowView _win, PuzzleInventory _wagon, PuzzleInventoryService _session,
        long _remainingHours = 0)
    {
        if (!_win || _wagon == null || !_win.InventoryView)
        {
            return;
        }
        _win.InventoryView.SetTitle(Presentation.PuzzleTrainTitle(_wagon));
        _win.InventoryView.SetBoard(_wagon);
        if (_session != null)
        {
            _win.SetHeader(_wagon, _remainingHours, _session.TrainSynergyDelta);
        }
    }

    public bool TryGetWindowAtScreenPosition(Vector2 _screenPos, Camera _eventCamera,
        out TrainWindowView _win, out PuzzleInventory _inventory, out Vector2Int _cell)
    {
        _win = null;
        _inventory = null;
        _cell = default;
        if (windows == null)
        {
            return false;
        }

        openSortBuffer.Clear();
        int cap = MaxWindowCount;
        for (int i = 0; i < cap; i++)
        {
            var w = windows[i];
            if (w && w.FloatingWindow && w.FloatingWindow.IsOpen && w.gameObject.activeInHierarchy)
            {
                openSortBuffer.Add(w);
            }
        }
        openSortBuffer.Sort((_a, _b) => _b.transform.GetSiblingIndex().CompareTo(_a.transform.GetSiblingIndex()));

        Camera cam = _eventCamera ? _eventCamera : EventCamera;
        var session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        for (int i = 0; i < openSortBuffer.Count; i++)
        {
            var w = openSortBuffer[i];
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)w.transform, _screenPos, cam))
            {
                continue;
            }
            if (w.InventoryView && w.InventoryView.isActiveAndEnabled &&
                w.InventoryView.TryGetBoardCell(_screenPos, out _cell))
            {
                _win = w;
                if (session != null && IndexOfWagon(session, w.BoundWagon) >= 0)
                {
                    _inventory = w.BoundWagon;
                }
                return _inventory != null;
            }
            // 앞쪽 창 영역 안이면 뒤쪽 창으로 통과하지 않는다.
            return false;
        }
        return false;
    }

    public TrainWindowView GetViewForInventory(PuzzleInventory _inventory, PuzzleInventoryService _session)
    {
        if (windows == null || _inventory == null || _session == null)
        {
            return null;
        }
        int cap = MaxWindowCount;
        for (int i = 0; i < cap; i++)
        {
            var w = windows[i];
            if (!w || !w.FloatingWindow || !w.FloatingWindow.IsOpen)
            {
                continue;
            }
            if (ReferenceEquals(w.BoundWagon, _inventory) && IndexOfWagon(_session, _inventory) >= 0)
            {
                return w;
            }
        }
        return null;
    }

    public bool HasAnyOpenWindow
    {
        get
        {
            if (windows == null) return false;
            int cap = MaxWindowCount;
            for (int i = 0; i < cap; i++)
            {
                if (windows[i] && windows[i].FloatingWindow && windows[i].FloatingWindow.IsOpen &&
                    windows[i].gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public void ClearAllDragVisuals()
    {
        if (windows == null) return;
        for (int i = 0; i < windows.Length; i++)
        {
            var iv = windows[i] ? windows[i].InventoryView : null;
            if (!iv) continue;
            iv.ClearDrag();
            iv.HidePlacementCandidates();
        }
    }

    public void HideAllPlacementCandidates()
    {
        if (windows == null) return;
        for (int i = 0; i < windows.Length; i++)
        {
            windows[i]?.InventoryView?.HidePlacementCandidates();
        }
    }

    public void SetLensSummary(string _tradeLine, string _spotLine)
    {
        if (windows == null) return;
        int cap = MaxWindowCount;
        for (int i = 0; i < cap; i++)
        {
            if (windows[i] && windows[i].FloatingWindow && windows[i].FloatingWindow.IsOpen)
            {
                windows[i].SetLensSummary(_tradeLine, _spotLine);
            }
        }
    }

    public void ClearOpenWagonsPersistence()
    {
        PlayerPrefs.DeleteKey(OPEN_WAGONS_KEY);
        PlayerPrefs.Save();
    }

    void SaveOpenWagons(PuzzleInventoryService _session)
    {
        if (windows == null || _session == null || string.IsNullOrEmpty(_session.RunId))
        {
            return;
        }
        var pairs = new List<string>();
        int cap = MaxWindowCount;
        for (int i = 0; i < cap; i++)
        {
            var w = windows[i];
            if (w && w.FloatingWindow && w.FloatingWindow.IsOpen && w.BoundWagon != null &&
                IndexOfWagon(_session, w.BoundWagon) >= 0)
            {
                pairs.Add($"{i}:{w.BoundWagon.WagonNumber}");
            }
        }
        PlayerPrefs.SetString(OPEN_WAGONS_KEY, $"{_session.RunId}|{string.Join(",", pairs)}");
        PlayerPrefs.Save();
    }

    void RestoreOpenWagons(PuzzleInventoryService _session)
    {
        if (!PlayerPrefs.HasKey(OPEN_WAGONS_KEY) || windows == null || _session == null ||
            string.IsNullOrEmpty(_session.RunId))
        {
            return;
        }
        string raw = PlayerPrefs.GetString(OPEN_WAGONS_KEY, string.Empty);
        int sep = raw.IndexOf('|');
        if (sep <= 0)
        {
            return;
        }
        string savedRunId = raw.Substring(0, sep);
        if (!string.Equals(savedRunId, _session.RunId, StringComparison.Ordinal))
        {
            // 다른 런의 열림 상태는 무시한다.
            return;
        }
        string payload = raw.Substring(sep + 1);
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        int cap = MaxWindowCount;
        seenWagonBuffer.Clear();
        var parts = payload.Split(',');
        for (int p = 0; p < parts.Length; p++)
        {
            var kv = parts[p].Split(':');
            if (kv.Length != 2 || !int.TryParse(kv[0], out int winIdx) || !int.TryParse(kv[1], out int wagonNumber))
            {
                continue;
            }
            if (winIdx < 0 || winIdx >= cap)
            {
                continue;
            }
            PuzzleInventory targetWagon = null;
            int targetIdx = -1;
            var wagons = _session.TrainWagons;
            for (int w = 0; w < wagons.Count; w++)
            {
                if (wagons[w].WagonNumber == wagonNumber)
                {
                    targetWagon = wagons[w];
                    targetIdx = w;
                    break;
                }
            }
            if (targetWagon == null || !seenWagonBuffer.Add(targetWagon))
            {
                continue;
            }
            var win = windows[winIdx];
            if (win && win.FloatingWindow)
            {
                win.BoundWagon = targetWagon;
                win.BoundWagonIndex = targetIdx;
                suppressOpenChanged = true;
                try
                {
                    win.FloatingWindow.SetOpen(true, false);
                }
                finally
                {
                    suppressOpenChanged = false;
                }
                BindWindowToWagon(win, targetWagon, _session);
                focusOrder[winIdx] = ++focusCounter;
                lastOpenedIndex = winIdx;
            }
        }
    }
}
