using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PuzzleWorkWaitView : PanelBase, IPointerDownHandler, IPointerMoveHandler
{
    [SerializeField] Image backdrop;
    [SerializeField] Image card;
    [SerializeField] TextMeshProUGUI question;
    [SerializeField] TextMeshProUGUI laterLabel;
    [SerializeField] TextMeshProUGUI waitLabel;
    [SerializeField] Button laterButton;
    [SerializeField] Button waitButton;

    Action<bool> choice;
    GameObject previousSelection;

    protected override void Awake()
    {
        backdrop.color = Presentation.PuzzleTravelDim;
        card.color = Presentation.PuzzleTravelCard;
        question.text = Presentation.StationWorkWaitQuestion;
        laterLabel.text = Presentation.StationWorkWaitLater;
        waitLabel.text = Presentation.StationWorkWaitNow;
        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        laterButton.onClick.AddListener(ChooseLater);
        waitButton.onClick.AddListener(ChooseWait);
    }

    protected override void OnDisable()
    {
        laterButton.onClick.RemoveListener(ChooseLater);
        waitButton.onClick.RemoveListener(ChooseWait);
        base.OnDisable();
    }

    internal void Ask(Action<bool> _choice)
    {
        if (isActiveAndEnabled && !IsOpen)
        {
            choice = _choice;
            Open();
        }
    }

    internal bool TryChooseLater()
    {
        if (!IsOpen)
        {
            return false;
        }

        ChooseLater();
        return true;
    }

    protected override void OnShow()
    {
        if (EventSystem.current)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(laterButton.gameObject);
        }
    }

    protected override void OnHide()
    {
        choice = null;
        if (EventSystem.current)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected && selected.transform.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy
                    ? previousSelection
                    : null);
            }
        }
        previousSelection = null;
    }

    public void OnPointerDown(PointerEventData _eventData)
    {
        if (IsOpen && EventSystem.current && !EventSystem.current.currentSelectedGameObject)
        {
            laterButton.Select();
        }
        _eventData.Use();
    }

    public void OnPointerMove(PointerEventData _eventData)
    {
        _eventData.Use();
    }

    void ChooseLater()
    {
        Choose(false);
    }

    void ChooseWait()
    {
        Choose(true);
    }

    void Choose(bool _wait)
    {
        if (!IsOpen)
        {
            return;
        }

        var callback = choice;
        Close();
        callback?.Invoke(_wait);
    }
}
