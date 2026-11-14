using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PuzzleStationSummaryView : MonoBehaviour
{
    [SerializeField] GameObject rumorRoot;
    [SerializeField] GameObject questRoot;
    [SerializeField] TextMeshProUGUI rumorTitleLabel;
    [SerializeField] TextMeshProUGUI questTitleLabel;
    [SerializeField] Button rumorViewAllButton;
    [SerializeField] Button questViewAllButton;
    [SerializeField] TextMeshProUGUI[] rumorLines;
    [SerializeField] TextMeshProUGUI[] questLines;

    internal event Action RumorViewAllClicked;
    internal event Action QuestViewAllClicked;

    void Awake()
    {
        if (rumorTitleLabel)
        {
            rumorTitleLabel.text = Presentation.SummaryRumorTitle;
        }
        if (questTitleLabel)
        {
            questTitleLabel.text = Presentation.SummaryQuestTitle;
        }
    }

    void OnEnable()
    {
        if (rumorViewAllButton)
        {
            rumorViewAllButton.onClick.AddListener(OnRumorViewAll);
        }
        if (questViewAllButton)
        {
            questViewAllButton.onClick.AddListener(OnQuestViewAll);
        }
    }

    void OnDisable()
    {
        if (rumorViewAllButton)
        {
            rumorViewAllButton.onClick.RemoveListener(OnRumorViewAll);
        }
        if (questViewAllButton)
        {
            questViewAllButton.onClick.RemoveListener(OnQuestViewAll);
        }
    }

    void OnRumorViewAll()
    {
        RumorViewAllClicked?.Invoke();
    }

    void OnQuestViewAll()
    {
        QuestViewAllClicked?.Invoke();
    }

    public void SetVisible(bool _visible)
    {
        if (rumorRoot)
        {
            rumorRoot.SetActive(_visible);
        }
        if (questRoot)
        {
            questRoot.SetActive(_visible);
        }
    }

    public void RenderRumors(IReadOnlyList<string> _lines, int _overflowCount)
    {
        if (rumorLines == null || rumorLines.Length == 0)
        {
            return;
        }

        if (_lines == null || _lines.Count == 0)
        {
            rumorLines[0].gameObject.SetActive(true);
            rumorLines[0].text = Presentation.TavernUnreadRumor;
            for (int i = 1; i < rumorLines.Length; i++)
            {
                rumorLines[i].gameObject.SetActive(false);
            }
            return;
        }

        for (int i = 0; i < rumorLines.Length; i++)
        {
            if (i < _lines.Count)
            {
                rumorLines[i].gameObject.SetActive(true);
                if (i == rumorLines.Length - 1 && _overflowCount > 0)
                {
                    rumorLines[i].text = $"{_lines[i]}  <color=#8E95A5>({Presentation.QuestMoreOffers(_overflowCount)})</color>";
                }
                else
                {
                    rumorLines[i].text = _lines[i];
                }
            }
            else if (i == _lines.Count && _overflowCount > 0)
            {
                rumorLines[i].gameObject.SetActive(true);
                rumorLines[i].text = Presentation.QuestMoreOffers(_overflowCount);
                _overflowCount = 0;
            }
            else
            {
                rumorLines[i].gameObject.SetActive(false);
            }
        }
    }

    public void RenderQuests(IReadOnlyList<string> _lines, int _overflowCount)
    {
        if (questLines == null || questLines.Length == 0)
        {
            return;
        }

        if (_lines == null || _lines.Count == 0)
        {
            questLines[0].gameObject.SetActive(true);
            questLines[0].text = Presentation.SummaryNoQuest;
            for (int i = 1; i < questLines.Length; i++)
            {
                questLines[i].gameObject.SetActive(false);
            }
            return;
        }

        for (int i = 0; i < questLines.Length; i++)
        {
            if (i < _lines.Count)
            {
                questLines[i].gameObject.SetActive(true);
                if (i == questLines.Length - 1 && _overflowCount > 0)
                {
                    questLines[i].text = $"{_lines[i]}  <color=#8E95A5>({Presentation.QuestMoreOffers(_overflowCount)})</color>";
                }
                else
                {
                    questLines[i].text = _lines[i];
                }
            }
            else if (i == _lines.Count && _overflowCount > 0)
            {
                questLines[i].gameObject.SetActive(true);
                questLines[i].text = Presentation.QuestMoreOffers(_overflowCount);
                _overflowCount = 0;
            }
            else
            {
                questLines[i].gameObject.SetActive(false);
            }
        }
    }
}
