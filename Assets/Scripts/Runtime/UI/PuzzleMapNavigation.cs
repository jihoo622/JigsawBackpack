using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The viewport owns navigation; map objects remain authored in the prefab.
public sealed class PuzzleMapNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    const float MIN_ZOOM = 0.35f;
    const float MAX_ZOOM = 2.5f;
    const float ZOOM_STEP = 1.2f;

    [SerializeField] InputActionAsset inputActions;
    [SerializeField] RectTransform content;
    [SerializeField] RectTransform viewport;
    [SerializeField] Button zoomIn;
    [SerializeField] Button zoomOut;
    [SerializeField] Button fit;
    [SerializeField] Button locate;
    [SerializeField] Text zoomLabel;

    InputActionAsset actions;
    InputAction zoom;
    InputAction point;
    RectTransform current;
    float scale = 1f;
    bool dragging;

    internal bool IsDragging => dragging;

    void Awake()
    {
        actions = Instantiate(inputActions);
        zoom = actions.FindAction("Map/Zoom", true);
        point = actions.FindAction("Map/Point", true);
    }

    void OnEnable()
    {
        zoom.performed += OnZoom;
        zoom.actionMap.Enable();
        zoomIn.onClick.AddListener(ZoomIn);
        zoomOut.onClick.AddListener(ZoomOut);
        fit.onClick.AddListener(Fit);
        locate.onClick.AddListener(FocusCurrent);
    }

    void OnDisable()
    {
        zoom.performed -= OnZoom;
        zoom.actionMap.Disable();
        zoomIn.onClick.RemoveListener(ZoomIn);
        zoomOut.onClick.RemoveListener(ZoomOut);
        fit.onClick.RemoveListener(Fit);
        locate.onClick.RemoveListener(FocusCurrent);
        dragging = false;
    }

    void OnDestroy()
    {
        if (actions)
        {
            Destroy(actions);
        }
    }

    internal void SetCurrent(RectTransform _current)
    {
        current = _current;
    }

    internal void Open()
    {
        Fit();
    }

    Camera EventCamera()
    {
        var canvas = GetComponentInParent<Canvas>();
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    void OnZoom(InputAction.CallbackContext _context)
    {
        var position = point.ReadValue<Vector2>();
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, position, EventCamera()))
        {
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, position, EventCamera(), out var local);
        float value = _context.ReadValue<float>();
        if (Mathf.Abs(value) > 0f)
        {
            SetZoom(scale * (value > 0f ? ZOOM_STEP : 1f / ZOOM_STEP), local);
        }
    }

    void ZoomIn()
    {
        SetZoom(scale * ZOOM_STEP, Vector2.zero);
    }

    void ZoomOut()
    {
        SetZoom(scale / ZOOM_STEP, Vector2.zero);
    }

    void Fit()
    {
        float fitScale = Mathf.Min(viewport.rect.width / content.rect.width, viewport.rect.height / content.rect.height);
        SetZoom(fitScale, Vector2.zero);
        content.anchoredPosition = Vector2.zero;
        Clamp();
    }

    void FocusCurrent()
    {
        if (current)
        {
            SetZoom(Mathf.Max(scale, 1.2f), Vector2.zero);
            content.anchoredPosition = -(Vector2)current.localPosition * scale;
            Clamp();
        }
    }

    void SetZoom(float _scale, Vector2 _anchor)
    {
        float next = Mathf.Clamp(_scale, MIN_ZOOM, MAX_ZOOM);
        content.anchoredPosition = _anchor - (_anchor - content.anchoredPosition) * (next / scale);
        scale = next;
        content.localScale = Vector3.one * scale;
        zoomLabel.text = Presentation.PuzzleMapZoom(scale);
        zoomIn.interactable = scale < MAX_ZOOM;
        zoomOut.interactable = scale > MIN_ZOOM;
        Clamp();
    }

    void Clamp()
    {
        Vector2 range = Vector2.Max(Vector2.zero, (content.rect.size * scale - viewport.rect.size) * 0.5f);
        Vector2 position = content.anchoredPosition;
        content.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -range.x, range.x), Mathf.Clamp(position.y, -range.y, range.y));
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        dragging = _eventData.button == PointerEventData.InputButton.Left;
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (!dragging)
        {
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, _eventData.position, _eventData.pressEventCamera, out var next);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, _eventData.position - _eventData.delta, _eventData.pressEventCamera, out var previous);
        content.anchoredPosition += next - previous;
        Clamp();
    }

    public void OnEndDrag(PointerEventData _eventData)
    {
        dragging = false;
    }
}
