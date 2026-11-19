using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 자유 이동·스냅·영속성을 제공하는 공용 부동 창 컴포넌트(고정 크기).
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class FloatingWindow : MonoBehaviour, IPointerDownHandler
{
    [Serializable]
    struct SavedWindowState
    {
        public float x;
        public float y;
        public bool open;
    }

    const string PREFS_PREFIX = "ui.win.";

    [SerializeField] string windowId;
    [SerializeField] RectTransform titleBar;
    [SerializeField] Button closeButton;
    [SerializeField] Vector2 defaultPosition = new Vector2(120f, 120f);
    [SerializeField] Vector2 defaultSize = new Vector2(480f, 400f);
    [SerializeField] bool startOpen = true;

    RectTransform rectTransform;
    FloatingWindowManager manager;
    Rect dragStartRect;
    Rect currentRect;
    // 사용자가 정한(또는 기본) 위치. 화면 크기에 맞춰 가둔 값(currentRect)과 따로 둔다.
    // 첫 배치 때는 캔버스 크기 조정이 아직 안 돼 부모 크기가 실제 픽셀 크기로 잡힐 수 있으므로, 부모 크기가 바뀌면 이 값으로 다시 가둔다.
    Rect desiredRect;
    Vector2 placedForParentSize;
    bool dragging;
    bool initialized;
    bool hiddenForBackgroundOnly;

    public string WindowId => string.IsNullOrEmpty(windowId) ? gameObject.name : windowId;
    public bool IsOpen { get; private set; }
    public bool CloseBlocked { get; set; }
    public Rect CurrentRect => currentRect;
    public RectTransform TitleBar => titleBar;

    public event Action Focused;
    public event Action<bool> OpenChanged;

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
        Focused?.Invoke();
    }

    void PlaceAt(Rect _desired)
    {
        desiredRect = _desired;
        Vector2 screenSize = GetScreenSize();
        placedForParentSize = screenSize;
        ApplyRect(FloatingWindowMath.ClampRect(_desired, FloatingWindowMath.TopHudSafeY, screenSize.x, screenSize.y));
    }

    void LateUpdate()
    {
        if (!initialized || dragging)
        {
            return;
        }
        if (GetScreenSize() != placedForParentSize)
        {
            PlaceAt(desiredRect);
        }
    }

    public void SetPosition(Vector2 _topLeftPos, bool _persist = true)
    {
        InitializeIfNeeded();
        PlaceAt(new Rect(_topLeftPos.x, _topLeftPos.y, currentRect.width, currentRect.height));
        if (_persist)
        {
            SaveState();
        }
    }

    public void Open() => SetOpen(true);

    public void Close()
    {
        if (CloseBlocked)
        {
            return;
        }
        SetOpen(false);
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (!_open && CloseBlocked)
        {
            return;
        }
        InitializeIfNeeded();
        hiddenForBackgroundOnly = false;
        bool changed = IsOpen != _open;
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
        if (changed)
        {
            OpenChanged?.Invoke(_open);
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
        PlaceAt(new Rect(defaultPosition.x, defaultPosition.y, defaultSize.x, defaultSize.y));
        SaveState();
    }

    internal void BeginDrag()
    {
        InitializeIfNeeded();
        BringToFront();
        dragStartRect = currentRect;
        dragging = true;
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
        dragging = false;
        desiredRect = currentRect;
        placedForParentSize = GetScreenSize();
        SaveState();
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
                PlaceAt(new Rect(saved.x, saved.y, defaultSize.x, defaultSize.y));
                IsOpen = saved.open;
                if (!hiddenForBackgroundOnly && gameObject.activeSelf != IsOpen)
                {
                    gameObject.SetActive(IsOpen);
                }
                return;
            }
            catch (Exception)
            {
                // 손상된 저장값은 무시하고 기본 배치로 복구한다.
            }
        }

        PlaceAt(new Rect(defaultPosition.x, defaultPosition.y, defaultSize.x, defaultSize.y));
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
            open = IsOpen
        };
        PlayerPrefs.SetString(PREFS_PREFIX + WindowId, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }
}
