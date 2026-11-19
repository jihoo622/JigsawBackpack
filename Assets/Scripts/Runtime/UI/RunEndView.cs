using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 런 종료(승리/패배) 전용 결산 모달 뷰. PauseMenu에서 분리되어 배치 후보 토글을 노출하지 않는다.
public sealed class RunEndView : PanelBase
{
    [SerializeField] TextMeshProUGUI headerText;
    [SerializeField] TextMeshProUGUI fundsLabelText;
    [SerializeField] TextMeshProUGUI fundsValueText;
    [SerializeField] TextMeshProUGUI goalLabelText;
    [SerializeField] TextMeshProUGUI goalValueText;
    [SerializeField] TextMeshProUGUI diffLabelText;
    [SerializeField] TextMeshProUGUI diffValueText;
    [SerializeField] TextMeshProUGUI turnsLabelText;
    [SerializeField] TextMeshProUGUI turnsValueText;
    [SerializeField] TextMeshProUGUI questsLabelText;
    [SerializeField] TextMeshProUGUI questsValueText;
    [SerializeField] Button endlessButton;
    [SerializeField] TextMeshProUGUI endlessLabel;
    [SerializeField] Button restartButton;
    [SerializeField] TextMeshProUGUI restartLabel;
    [SerializeField] Button quitButton;
    [SerializeField] TextMeshProUGUI quitLabel;
    [SerializeField] UIPalette palette;

    public GameObject DefaultSelectedObject =>
        endlessButton && endlessButton.gameObject.activeSelf
            ? endlessButton.gameObject
            : (restartButton ? restartButton.gameObject : null);

    public void BindButtons(UnityAction _onEndless, UnityAction _onRestart, UnityAction _onQuit)
    {
        if (endlessButton)
        {
            endlessButton.onClick.RemoveAllListeners();
            if (_onEndless != null)
            {
                endlessButton.onClick.AddListener(_onEndless);
            }
        }
        if (restartButton)
        {
            restartButton.onClick.RemoveAllListeners();
            if (_onRestart != null)
            {
                restartButton.onClick.AddListener(_onRestart);
            }
        }
        if (quitButton)
        {
            quitButton.onClick.RemoveAllListeners();
            if (_onQuit != null)
            {
                quitButton.onClick.AddListener(_onQuit);
            }
        }
    }

    public void ShowResult(
        PuzzleRunStatus _status,
        long _funds,
        long _targetFunds,
        long _elapsedTurns,
        int _completedQuests)
    {
        var source = palette ? palette : Resources.Load<UIPalette>("UI/UIPalette");
        bool won = _status == PuzzleRunStatus.Won;

        if (headerText)
        {
            headerText.text = Presentation.PuzzleRunStatusText(_status);
            if (source)
            {
                headerText.color = won ? source.GoldAccent : source.Negative;
            }
        }

        if (fundsLabelText) fundsLabelText.text = Presentation.RunEndFundsLabel;
        if (fundsValueText) fundsValueText.text = Presentation.RunEndMoney(_funds);

        if (goalLabelText) goalLabelText.text = Presentation.RunEndGoalLabel;
        if (goalValueText) goalValueText.text = Presentation.RunEndMoney(_targetFunds);

        long diff = _funds - _targetFunds;
        if (diffLabelText) diffLabelText.text = Presentation.RunEndDiffLabel;
        if (diffValueText)
        {
            diffValueText.text = Presentation.RunEndSignedMoney(diff);
            if (source)
            {
                diffValueText.color = diff >= 0 ? source.Positive : source.Negative;
            }
        }

        if (turnsLabelText) turnsLabelText.text = Presentation.RunEndTurnsLabel;
        if (turnsValueText) turnsValueText.text = Presentation.RunEndTurns(_elapsedTurns);

        if (questsLabelText) questsLabelText.text = Presentation.RunEndQuestsLabel;
        if (questsValueText) questsValueText.text = Presentation.RunEndQuests(_completedQuests);

        if (endlessLabel) endlessLabel.text = Presentation.PuzzleEndlessContinue;
        if (restartLabel) restartLabel.text = Presentation.RunEndRestart;
        if (quitLabel) quitLabel.text = Presentation.RunEndQuit;

        if (endlessButton)
        {
            endlessButton.gameObject.SetActive(won);
        }

        Open();
    }
}
