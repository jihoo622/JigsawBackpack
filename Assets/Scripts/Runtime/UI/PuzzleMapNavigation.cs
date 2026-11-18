using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine;

// The viewport owns navigation; map objects remain authored in the prefab.
public sealed class PuzzleMapNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Zoom Settings")]
    [SerializeField] float minZoom = 0.35f;
    [SerializeField] float maxZoom = 2.5f;
    [SerializeField] float zoomStep = 1.2f;
    [SerializeField] float focusCurrent = 1.2f;
    [SerializeField] float zoomDisplayMultiplier = 100f;

    [SerializeField] InputActionAsset inputActions;
    [SerializeField] RectTransform content;
    [SerializeField] RectTransform viewport;
    [SerializeField] Button zoomIn;
    [SerializeField] Button zoomOut;
    [SerializeField] Button fit;
    [SerializeField] Button locate;
    [SerializeField] TMPro.TextMeshProUGUI zoomLabel;
    [SerializeField] PuzzleMapView view;

    public float MinZoom => minZoom;
    public float MaxZoom => maxZoom;
    public float ZoomStep => zoomStep;
    public float FocusCurrentZoom => focusCurrent;
    public float ZoomDisplayMultiplier => zoomDisplayMultiplier;

    void OnValidate()
    {
        if (minZoom <= 0f)
        {
            minZoom = 0.01f;
        }
        if (maxZoom < minZoom)
        {
            maxZoom = minZoom;
        }
        if (zoomStep <= 1f)
        {
            zoomStep = 1.01f;
        }
        if (focusCurrent <= 0f)
        {
            focusCurrent = minZoom;
        }
    }

    InputActionAsset actions;
    InputAction zoom;
    InputAction point;
    RectTransform current;
    float scale = 1f;
    bool dragging;
    bool hasOpened;
    readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

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

    internal void RefreshLayout()
    {
        view.UpdateMapLayout(scale, viewport);
    }

    void OnRectTransformDimensionsChange()
    {
        if (hasOpened && isActiveAndEnabled && content && viewport && view)
        {
            Clamp();
        }
    }

    internal void Open()
    {
        if (!hasOpened)
        {
            Fit();
            hasOpened = true;
        }
        else
        {
            SetZoom(scale, Vector2.zero);
        }
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
        if (EventSystem.current)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            pointerHits.Clear();
            EventSystem.current.RaycastAll(pointer, pointerHits);
            if (pointerHits.Count > 0 && !pointerHits[0].gameObject.transform.IsChildOf(view.transform))
            {
                return;
            }
        }
        float value = _context.ReadValue<float>();
        if (Mathf.Abs(value) > 0f)
        {
            ZoomAt(scale * (value > 0f ? ZoomStep : 1f / ZoomStep), position);
        }
    }

    // 버튼 확대·축소는 뷰포트 가운데를 기준으로 한다.
    void ZoomIn()
    {
        ZoomAt(scale * ZoomStep, ViewportCenterOnScreen());
    }

    void ZoomOut()
    {
        ZoomAt(scale / ZoomStep, ViewportCenterOnScreen());
    }

    Vector2 ViewportCenterOnScreen()
    {
        return RectTransformUtility.WorldToScreenPoint(EventCamera(), viewport.TransformPoint(viewport.rect.center));
    }

    // 화면 위치 _screen 아래에 있던 지도 지점이 확대·축소 뒤에도 같은 화면 위치에 남도록 지도를 옮긴다.
    // 뷰포트·지도의 앵커·피벗 배치와 무관하게 화면 좌표로 보정한다. 지도 끝에서는 Clamp가 위치를 다시 제한한다.
    void ZoomAt(float _scale, Vector2 _screen)
    {
        var camera = EventCamera();
        var parent = content.parent as RectTransform;
        if (!parent || !RectTransformUtility.ScreenPointToLocalPointInRectangle(content, _screen, camera, out var pinned))
        {
            SetZoom(_scale, Vector2.zero);
            return;
        }
        ApplyScale(Mathf.Clamp(_scale, MinZoom, MaxZoom));
        Vector2 moved = RectTransformUtility.WorldToScreenPoint(camera, content.TransformPoint(pinned));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, _screen, camera, out var target);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, moved, camera, out var current);
        content.anchoredPosition += target - current;
        Clamp();
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
            SetZoom(Mathf.Max(scale, focusCurrent), Vector2.zero);
            content.anchoredPosition = -(Vector2)current.localPosition * scale;
            Clamp();
        }
    }

    internal void FocusCity(string _cityId)
    {
        if (string.IsNullOrEmpty(_cityId) || !view || view.Nodes == null)
        {
            return;
        }
        var node = System.Array.Find(view.Nodes, _node => _node && string.Equals(_node.CityId, _cityId, System.StringComparison.Ordinal));
        if (!node)
        {
            return;
        }
        var target = node.transform as RectTransform;
        if (!target)
        {
            return;
        }
        SetZoom(Mathf.Max(scale, focusCurrent), Vector2.zero);
        content.anchoredPosition = -(Vector2)target.localPosition * scale;
        Clamp();
    }

    internal void FocusRegion(string _regionId)
    {
        if (string.IsNullOrEmpty(_regionId))
        {
            return;
        }
        if (!TableManager.Instance.TryGetRegion(_regionId, out _))
        {
            return;
        }

        Fit();

        if (!view || view.Nodes == null)
        {
            return;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        int count = 0;

        foreach (var node in view.Nodes)
        {
            if (!node || string.IsNullOrEmpty(node.CityId))
            {
                continue;
            }
            if (!TableManager.Instance.HasCity(node.CityId))
            {
                continue;
            }
            if (!string.Equals(TableManager.Instance.CityPrice(node.CityId).regionId, _regionId, System.StringComparison.Ordinal))
            {
                continue;
            }

            var rt = node.transform as RectTransform;
            if (!rt)
            {
                continue;
            }

            Vector2 pos = (Vector2)rt.localPosition;
            min = Vector2.Min(min, pos);
            max = Vector2.Max(max, pos);
            count++;
        }

        if (count > 0)
        {
            Vector2 center = (min + max) * 0.5f;
            content.anchoredPosition = -center * scale;
            Clamp();
        }
    }

    // 위치를 바로 다시 잡는 호출(전체 보기·현재 위치·역 이동)용. 기준점 보정은 ZoomAt이 한다.
    void SetZoom(float _scale, Vector2 _anchor)
    {
        float next = Mathf.Clamp(_scale, MinZoom, MaxZoom);
        content.anchoredPosition = _anchor - (_anchor - content.anchoredPosition) * (next / scale);
        ApplyScale(next);
        Clamp();
    }

    void ApplyScale(float _scale)
    {
        scale = _scale;
        content.localScale = Vector3.one * scale;
        zoomLabel.text = Presentation.PuzzleMapZoom(scale * zoomDisplayMultiplier);
        zoomIn.interactable = scale < MaxZoom;
        zoomOut.interactable = scale > MinZoom;
    }

    void Clamp()
    {
        Vector2 range = Vector2.Max(Vector2.zero, (content.rect.size * scale - viewport.rect.size) * 0.5f);
        Vector2 position = content.anchoredPosition;
        content.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -range.x, range.x), Mathf.Clamp(position.y, -range.y, range.y));
        RefreshLayout();
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
