using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PuzzleStationSummaryPresenter : MonoBehaviour
{
    [SerializeField] PuzzleStationSummaryView view;
    PuzzleInventoryService session;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();

        if (view)
        {
            view.RumorClicked += OnRumorClicked;
            view.QuestClicked += OnQuestClicked;
        }

        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.PanelVisibility += OnPanelVisibility;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.PuzzleRunStarted += OnRunStarted;

        Refresh();
    }

    void OnDisable()
    {
        if (view)
        {
            view.RumorClicked -= OnRumorClicked;
            view.QuestClicked -= OnQuestClicked;
        }

        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.PuzzleRunStarted -= OnRunStarted;
    }

    void OnRunStarted(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnRumorClicked()
    {
        GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Rumors);
    }

    void OnQuestClicked()
    {
        GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Quests);
    }

    void OnRumorRecorded(GameEvents.TavernRumorInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnQuestChanged(GameEvents.QuestInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info)
    {
        Refresh();
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (view && _info.panel == Presentation.PUZZLE_MAP_PANEL_NAME)
        {
            view.SetVisible(!_info.open);
        }
    }

    void Refresh()
    {
        if (!view || session == null)
        {
            return;
        }

        RefreshRumors();
        RefreshQuests();
    }

    void RefreshRumors()
    {
        var marketTags = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(session.CurrentCityId) && session.Market != null)
        {
            foreach (var placement in session.Market.Placements)
            {
                if (placement.Definition?.Tags != null)
                {
                    foreach (var tag in placement.Definition.Tags)
                    {
                        marketTags.Add(tag);
                    }
                }
            }
        }

        var allRumors = session.TavernRumors;
        var relatedRumors = new List<PuzzleInventoryService.TavernRumor>();
        var otherRumors = new List<PuzzleInventoryService.TavernRumor>();

        for (int i = allRumors.Count - 1; i >= 0; i--)
        {
            var r = allRumors[i];
            bool isRelated = false;
            if (!string.IsNullOrEmpty(r.Tag) && marketTags.Contains(r.Tag))
            {
                isRelated = true;
            }
            else
            {
                var targetCity = TableManager.Instance.CityPrice(r.TargetCityId);
                if (targetCity?.tagPrices != null)
                {
                    foreach (var rule in targetCity.tagPrices)
                    {
                        if (marketTags.Contains(rule.tag))
                        {
                            isRelated = true;
                            break;
                        }
                    }
                }
            }

            if (isRelated)
            {
                relatedRumors.Add(r);
            }
            else
            {
                otherRumors.Add(r);
            }
        }

        var chosenRumors = new List<string>();
        foreach (var r in relatedRumors)
        {
            if (chosenRumors.Count >= 2)
            {
                break;
            }
            chosenRumors.Add(r.Message);
        }
        foreach (var r in otherRumors)
        {
            if (chosenRumors.Count >= 2)
            {
                break;
            }
            chosenRumors.Add(r.Message);
        }

        int overflow = Math.Max(0, allRumors.Count - chosenRumors.Count);
        view.RenderRumors(chosenRumors, overflow);
    }

    void RefreshQuests()
    {
        var activeQuests = session.Quests
            .Where(_q => _q.State == TrainventoryQuestState.Active)
            .OrderBy(_q => _q.DeadlineMinutes)
            .ToList();

        var chosenQuests = new List<string>();
        for (int i = 0; i < Math.Min(3, activeQuests.Count); i++)
        {
            var q = activeQuests[i];
            string cityName = TableManager.Instance.CityPrice(q.TargetCityId)?.displayName ?? q.TargetCityId;
            long remainingMinutes = Math.Max(0, q.DeadlineMinutes - session.ElapsedTime);
            string timeStr = Presentation.PuzzleMapTime(remainingMinutes);
            string timestamp = Presentation.GameTimestamp(q.DeadlineMinutes);
            chosenQuests.Add($"{cityName} · {timestamp} (남은 {timeStr})");
        }

        int overflow = Math.Max(0, activeQuests.Count - chosenQuests.Count);
        view.RenderQuests(chosenQuests, overflow);
    }
}
