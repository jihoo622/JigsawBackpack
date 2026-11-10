using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PuzzleMapTravelView : PanelBase
{
    [SerializeField] InputActionAsset inputActions;
    [SerializeField] GameObject confirmation;
    [SerializeField] GameObject journey;
    [SerializeField] Text question;
    [SerializeField] Text quote;
    [SerializeField] Text confirmLabel;
    [SerializeField] Text cancelLabel;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Text journeyTitle;
    [SerializeField] Text journeyRoute;
    [SerializeField] Text progressLabel;
    [SerializeField] Image progress;
    [SerializeField] RectTransform train;
    [SerializeField] RectTransform departure;
    [SerializeField] RectTransform destination;

    InputActionAsset actions;
    InputAction cancel;
    GameObject previousSelection;
    bool confirming;
    bool selectionCaptured;

    internal event Action Confirmed;
    internal event Action Canceled;

    protected override void Awake()
    {
        actions = Instantiate(inputActions);
        cancel = actions.FindAction("UI/Cancel", true);
        question.text = Presentation.PUZZLE_TRAVEL_QUESTION;
        confirmLabel.text = Presentation.PUZZLE_TRAVEL_CONFIRM;
        cancelLabel.text = Presentation.PUZZLE_TRAVEL_CANCEL;
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(OnCancel);
        cancel.performed += OnCancelInput;
        cancel.Enable();
    }

    protected override void OnDisable()
    {
        confirmButton.onClick.RemoveListener(OnConfirm);
        cancelButton.onClick.RemoveListener(OnCancel);
        cancel.performed -= OnCancelInput;
        cancel.Disable();
        base.OnDisable();
    }

    void OnDestroy()
    {
        if (actions)
        {
            Destroy(actions);
        }
    }

    internal void ShowConfirmation(string _quote)
    {
        quote.text = _quote;
        confirming = true;
        confirmation.SetActive(true);
        journey.SetActive(false);
        Open();
    }

    internal void BeginJourney(string _route)
    {
        confirming = false;
        confirmation.SetActive(false);
        journey.SetActive(true);
        journeyTitle.text = Presentation.PUZZLE_TRAVEL_IN_PROGRESS;
        journeyRoute.text = _route;
        if (EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        ShowProgress(0f);
    }

    internal void ShowProgress(float _progress)
    {
        float value = Mathf.Clamp01(_progress);
        progress.fillAmount = value;
        train.anchoredPosition = Vector2.Lerp(departure.anchoredPosition, destination.anchoredPosition, value);
        progressLabel.text = Presentation.PuzzleTravelProgress(value);
    }

    internal void ShowArrival()
    {
        ShowProgress(1f);
        journeyTitle.text = Presentation.PUZZLE_TRAVEL_ARRIVED;
    }

    protected override void OnShow()
    {
        if (EventSystem.current)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            selectionCaptured = true;
            EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        }
    }

    protected override void OnHide()
    {
        confirming = false;
        confirmation.SetActive(false);
        journey.SetActive(false);
        progress.fillAmount = 0f;
        if (selectionCaptured && EventSystem.current)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (!selected || selected.transform.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
            }
        }
        selectionCaptured = false;
        previousSelection = null;
    }

    void OnConfirm()
    {
        if (IsOpen && confirming)
        {
            Confirmed?.Invoke();
        }
    }

    void OnCancel()
    {
        if (IsOpen && confirming)
        {
            Canceled?.Invoke();
        }
    }

    void OnCancelInput(InputAction.CallbackContext _context)
    {
        OnCancel();
    }
}
