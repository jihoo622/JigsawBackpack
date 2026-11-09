using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public sealed class PuzzleInventoryInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] InputSystemUIInputModule uiInputModule;

    EventSystem eventSystem;
    InputActionAsset inputActions;
    InputAction rotate;
    InputAction cancel;
    int? pointerId;
    int? hoveredPointerId;
    bool navigationSuppressed;
    bool previousNavigation;

    internal event Action<PointerEventData> Pressed;
    internal event Action<PointerEventData> Dragged;
    internal event Action<PointerEventData> Released;
    internal event Action Rotated;
    internal event Action Canceled;
    internal event Action<Vector2> Hovered;
    internal event Action HoverEnded;

    void OnEnable()
    {
        if (!uiInputModule || !uiInputModule.actionsAsset || !uiInputModule.TryGetComponent(out eventSystem))
        {
            Debug.LogError("PuzzleInventoryInput에 액션 자산이 연결된 InputSystemUIInputModule이 필요합니다.", this);
            enabled = false;
            return;
        }

        inputActions = Instantiate(uiInputModule.actionsAsset);
        rotate = inputActions.FindAction("PuzzleInventory/Rotate");
        cancel = inputActions.FindAction("PuzzleInventory/Cancel");
        if (rotate == null || cancel == null)
        {
            Debug.LogError("PuzzleInventory 액션 맵에 Rotate와 Cancel이 필요합니다.", this);
            enabled = false;
            return;
        }

        rotate.performed += OnRotate;
        cancel.performed += OnCancel;
    }

    void OnDisable()
    {
        ClearHover();
        CancelCapture();
        RestoreNavigation();
        if (rotate != null)
        {
            rotate.performed -= OnRotate;
        }
        if (cancel != null)
        {
            cancel.performed -= OnCancel;
        }
        if (inputActions)
        {
            Destroy(inputActions);
        }

        inputActions = null;
        rotate = null;
        cancel = null;
    }

    void LateUpdate()
    {
        // UI 모듈이 같은 Escape를 처리한 뒤 내비게이션을 복원한다.
        if (!pointerId.HasValue)
        {
            RestoreNavigation();
        }
    }

    void OnApplicationFocus(bool _hasFocus)
    {
        if (!_hasFocus)
        {
            ClearHover();
            CancelCapture();
        }
    }

    void OnApplicationPause(bool _paused)
    {
        if (_paused)
        {
            ClearHover();
            CancelCapture();
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        OnPointerMove(_eventData);
    }

    public void OnPointerMove(PointerEventData _eventData)
    {
        if (!pointerId.HasValue || pointerId == _eventData.pointerId)
        {
            hoveredPointerId = _eventData.pointerId;
            Hovered?.Invoke(_eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (hoveredPointerId == _eventData.pointerId)
        {
            ClearHover();
        }
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (!pointerId.HasValue && _eventData.button == PointerEventData.InputButton.Left)
        {
            Pressed?.Invoke(_eventData);
        }
    }

    public void OnInitializePotentialDrag(PointerEventData _eventData)
    {
        if (pointerId == _eventData.pointerId && _eventData.button == PointerEventData.InputButton.Left)
        {
            _eventData.useDragThreshold = false;
        }
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (pointerId == _eventData.pointerId && _eventData.button == PointerEventData.InputButton.Left)
        {
            Dragged?.Invoke(_eventData);
        }
    }

    public void OnPointerUp(PointerEventData _eventData)
    {
        if (pointerId == _eventData.pointerId && _eventData.button == PointerEventData.InputButton.Left)
        {
            ReleaseCapture();
            Released?.Invoke(_eventData);
        }
    }

    internal void CapturePointer(int _pointerId)
    {
        pointerId = _pointerId;
        if (!navigationSuppressed)
        {
            previousNavigation = eventSystem.sendNavigationEvents;
            eventSystem.sendNavigationEvents = false;
            navigationSuppressed = true;
        }
        rotate.Enable();
        cancel.Enable();
    }

    internal void CancelCapture()
    {
        if (pointerId.HasValue)
        {
            ReleaseCapture();
            Canceled?.Invoke();
        }
    }

    void OnRotate(InputAction.CallbackContext _context)
    {
        Rotated?.Invoke();
    }

    void OnCancel(InputAction.CallbackContext _context)
    {
        CancelCapture();
    }

    void ClearHover()
    {
        if (hoveredPointerId.HasValue)
        {
            hoveredPointerId = null;
            HoverEnded?.Invoke();
        }
    }

    void ReleaseCapture()
    {
        pointerId = null;
        rotate.Disable();
        cancel.Disable();
    }

    void RestoreNavigation()
    {
        if (navigationSuppressed && eventSystem)
        {
            eventSystem.sendNavigationEvents = previousNavigation;
        }
        navigationSuppressed = false;
    }
}
