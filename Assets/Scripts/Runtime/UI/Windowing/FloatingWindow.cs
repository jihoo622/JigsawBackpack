using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 자유 이동·크기 조절·스냅·영속성을 제공하는 공용 부동 창 컴포넌트.
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class FloatingWindow : MonoBehaviour, IPointerDownHandler
{
    [Serializable]
    struct SavedWindowState
    {
        public float x;
        public float y;
        public float w;
        public float h;
        public bool open;
    }

    const string PREFS_PREFIX = "ui.win.";

    [SerializeField] string windowId;
    [SerializeField] RectTransform titleBar;
    [SerializeField] Button closeButton;
    [SerializeField] Vector2 defaultPosition = new Vector2(120f, 120f);
    [SerializeField] Vector2 defaultSize = new Vector2(480f, 400f);
    [SerializeField] Vector2 minSize = new Vector2(280f, 200f);
    [SerializeField] Vector2 maxSize = new Vector2(1600f, 960f);
    [SerializeField] bool startOpen = true;

    RectTransform rectTransform;
    FloatingWindowManager manager;
    Rect dragStartRect;
    Rect currentRect;
    bool initialized;
    bool hiddenForBackgroundOnly;

    public event Action Resized;

    public string WindowId => string.IsNullOrEmpty(windowId) ? gameObject.name : windowId;
    public bool IsOpen { get; private set; }
    public Rect CurrentRect => currentRect;
    public RectTransform TitleBar => titleBar;

    void Awake()
    {
        InitializeIfNeeded();
    }

    void OnEnable()
    {
        if (closeButton)
        {
            closeButton.onClick.AddListener(Close);
        }
        ResolveManager()?.Register(this);
    }

    void OnDisable()
    {
        if (closeButton)
        {
            closeButton.onClick.RemoveListener(Close);
        }
        if (!hiddenForBackgroundOnly && manager)
        {
            manager.Unregister(this);
        }
    }

    void OnDestroy()
    {
        if (manager)
        {
            manager.Unregister(this);
        }
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        BringToFront();
    }

    public void BringToFront()
    {
        transform.SetAsLastSibling();
    }

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);

    public void SetOpen(bool _open, bool _persist = true)
    {
        InitializeIfNeeded();
        hiddenForBackgroundOnly = false;
        IsOpen = _open;
        if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
        if (_open)
        {
            BringToFront();
        }
        if (_persist)
        {
            SaveState();
        }
    }

    // R18: 배경만 보기 토글 시 열림 상태를 덮어쓰지 않고 오브젝트만 숨기거나 복원한다.
    internal void SetHiddenForBackgroundOnly(bool _hidden)
    {
        InitializeIfNeeded();
        hiddenForBackgroundOnly = _hidden;
        bool active = !_hidden && IsOpen;
        if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
    }

    public void ResetLayout()
    {
        InitializeIfNeeded();
        Vector2 screenSize = GetScreenSize();
        var rect = new Rect(defaultPosition.x, defaultPosition.y, defaultSize.x, defaultSize.y);
        rect = FloatingWindowMath.ClampRect(rect, FloatingWindowMath.TopHudSafeY, screenSize.x, screenSize.y);
        ApplyRect(rect);
        SaveState();
        Resized?.Invoke();
    }

    internal void BeginDrag()
    {
        InitializeIfNeeded();
        BringToFront();
        dragStartRect = currentRect;
    }

    internal void UpdateDrag(Vector2 _cumulativeDeltaTopLeft)
    {
        Vector2 screenSize = GetScreenSize();
        var moved = new Rect(
            dragStartRect.x + _cumulativeDeltaTopLeft.x,
            dragStartRect.y + _cumulativeDeltaTopLeft.y,
            dragStartRect.width,
            dragStartRect.height);
        var others = ResolveManager()?.GetOtherOpenRects(this);
        var snapped = FloatingWindowMath.SnapMoveRect(
            moved, others, FloatingWindowMath.DefaultSnapDistance,
            FloatingWindowMath.TopHudSafeY, screenSize.x, screenSize.y);
        ApplyRect(snapped);
    }

    internal void EndDrag()
    {
        SaveState();
    }

    internal void BeginResize()
    {
        InitializeIfNeeded();
        BringToFront();
        dragStartRect = currentRect;
    }

    internal void UpdateResize(FloatingWindowMath.ResizeEdge _edge, Vector2 _cumulativeDeltaTopLeft)
    {
        Vector2 screenSize = GetScreenSize();
        var others = ResolveManager()?.GetOtherOpenRects(this);
        var resized = FloatingWindowMath.ResizeRect(
            dragStartRect, _edge, _cumulativeDeltaTopLeft, minSize, maxSize, others,
            FloatingWindowMath.DefaultSnapDistance, FloatingWindowMath.TopHudSafeY, screenSize.x, screenSize.y);
        bool sizeChanged = !Mathf.Approximately(resized.width, currentRect.width) ||
                           !Mathf.Approximately(resized.height, currentRect.height);
        ApplyRect(resized);
        if (sizeChanged)
        {
            Resized?.Invoke();
        }
    }

    internal void EndResize()
    {
        SaveState();
        Resized?.Invoke();
    }

    void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;
        rectTransform = (RectTransform)transform;
        ResolveManager()?.Register(this);
        LoadState();
    }

    FloatingWindowManager ResolveManager()
    {
        if (!manager)
        {
            manager = GetComponentInParent<FloatingWindowManager>(true);
        }
        return manager;
    }

    Vector2 GetScreenSize()
    {
        if (!rectTransform)
        {
            rectTransform = (RectTransform)transform;
        }
        return FloatingWindowMath.GetParentReferenceSize(rectTransform);
    }

    void ApplyRect(Rect _rect)
    {
        currentRect = _rect;
        Vector2 screenSize = GetScreenSize();
        FloatingWindowMath.ApplyTopLeftRect(rectTransform, _rect, screenSize.x, screenSize.y);
    }

    void LoadState()
    {
        Vector2 screenSize = GetScreenSize();
        string key = PREFS_PREFIX + WindowId;
        if (PlayerPrefs.HasKey(key))
        {
            try
            {
                var saved = JsonUtility.FromJson<SavedWindowState>(PlayerPrefs.GetString(key));
                if (saved.w > 0f && saved.h > 0f)
                {
                    float minW = Mathf.Max(1f, minSize.x);
                    float minH = Mathf.Max(1f, minSize.y);
                    float maxW = Mathf.Max(minW, maxSize.x);
                    float maxH = Mathf.Max(minH, maxSize.y);
                    float w = Mathf.Clamp(saved.w, minW, maxW);
                    float h = Mathf.Clamp(saved.h, minH, maxH);
                    var clamped = FloatingWindowMath.ClampRect(
                        new Rect(saved.x, saved.y, w, h),
                        FloatingWindowMath.TopHudSafeY,
                        screenSize.x,
                        screenSize.y);
                    ApplyRect(clamped);
                    IsOpen = saved.open;
                    if (!hiddenForBackgroundOnly && gameObject.activeSelf != IsOpen)
                    {
                        gameObject.SetActive(IsOpen);
                    }
                    return;
                }
            }
            catch (Exception)
            {
                // 손상된 저장값은 무시하고 기본 배치로 복구한다.
            }
        }

        var defRect = FloatingWindowMath.ClampRect(
            new Rect(defaultPosition.x, defaultPosition.y, defaultSize.x, defaultSize.y),
            FloatingWindowMath.TopHudSafeY,
            screenSize.x,
            screenSize.y);
        ApplyRect(defRect);
        IsOpen = startOpen;
        if (!hiddenForBackgroundOnly && gameObject.activeSelf != IsOpen)
        {
            gameObject.SetActive(IsOpen);
        }
    }

    void SaveState()
    {
        var state = new SavedWindowState
        {
            x = currentRect.x,
            y = currentRect.y,
            w = currentRect.width,
            h = currentRect.height,
            open = IsOpen
        };
        PlayerPrefs.SetString(PREFS_PREFIX + WindowId, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }
}
