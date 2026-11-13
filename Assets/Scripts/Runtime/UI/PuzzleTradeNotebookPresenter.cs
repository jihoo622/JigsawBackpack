using UnityEngine;

public sealed class PuzzleTradeNotebookPresenter : MonoBehaviour
{
    [SerializeField] PuzzleTradeNotebookView view;
    PuzzleInventoryService session;
    PuzzleNotebookTab tab = PuzzleNotebookTab.Rumors;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.ProficiencyChanged += OnProficiencyChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        view.TabSelected += OnTabSelected;
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.ProficiencyChanged -= OnProficiencyChanged;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        view.TabSelected -= OnTabSelected;
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

    void OnProficiencyChanged(GameEvents.ProficiencyInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    // 지역 거래 불이익의 만료 표시를 시간 진행에 맞춘다.
    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnTabSelected(PuzzleNotebookTab _tab)
    {
        tab = _tab;
        Refresh();
    }

    void Refresh()
    {
        string text = tab == PuzzleNotebookTab.Rumors
            ? Presentation.RumorLogEntries(session.TavernRumors)
            : Presentation.QuestLogEntries(session);
        view.Render(tab, text, session.TavernRumors.Count, session.Quests.Count);
    }
}
