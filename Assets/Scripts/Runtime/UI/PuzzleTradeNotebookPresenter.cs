using System.Linq;
using UnityEngine;

// 새 소식 수(unseenCount)는 수첩이 접혀 있는 동안 기록된 소문, 의뢰 변화(수락·완료·실패), 숙련 레벨 상승 건수다.
// 수첩을 열면 0으로 돌아간다. 씬 로컬 인스턴스 필드이므로 정적 초기화가 필요 없다.
public sealed class PuzzleTradeNotebookPresenter : MonoBehaviour
{
    [SerializeField] PuzzleTradeNotebookView view;
    [SerializeField] bool embeddedInNotebook;
    [SerializeField] NotebookWindowView notebookWindow;
    PuzzleInventoryService session;
    PuzzleNotebookTab tab = PuzzleNotebookTab.Rumors;
    int unseenCount;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        GameEvents.TavernRumorRecorded += OnRumorRecorded;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.ProficiencyChanged += OnProficiencyChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.NotebookOpenRequested += OnNotebookOpenRequested;
        GameEvents.PanelVisibility += OnPanelVisibility;
        view.TabSelected += OnTabSelected;
        view.Shown += OnShown;
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.TavernRumorRecorded -= OnRumorRecorded;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.ProficiencyChanged -= OnProficiencyChanged;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.NotebookOpenRequested -= OnNotebookOpenRequested;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        view.TabSelected -= OnTabSelected;
        view.Shown -= OnShown;
    }

    public bool TryConsumeCancel()
    {
        return view && view.TryConsumeCancel();
    }

    void OnNotebookOpenRequested(PuzzleNotebookTab _tab)
    {
        tab = _tab;
        Refresh();
        if (embeddedInNotebook && notebookWindow)
        {
            var winTab = _tab switch
            {
                PuzzleNotebookTab.Quests => NotebookWindowView.NotebookWindowTab.Quests,
                PuzzleNotebookTab.Proficiency => NotebookWindowView.NotebookWindowTab.Proficiency,
                PuzzleNotebookTab.Synergy => NotebookWindowView.NotebookWindowTab.Synergy,
                _ => NotebookWindowView.NotebookWindowTab.Rumors
            };
            notebookWindow.OpenTab(winTab);
            return;
        }
        view.SetOpen(true);
        SyncSynergyChart();
    }

    internal void MarkShown() => OnShown();

    void OnShown()
    {
        unseenCount = 0;
        Refresh();
        SyncSynergyChart();
    }

    // 수첩이 접히면 상성표도 숨긴다. 상성표가 따로 닫히면(Esc) 접지 않고 그 탭 본문의 빌드 요약이 남는다.
    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (_info.open)
        {
            return;
        }
        if (_info.panel == view.PanelName)
        {
            GameEvents.RaiseSynergyChartShowRequested(gameObject.scene.handle, false);
        }
    }

    void SyncSynergyChart()
    {
        if (embeddedInNotebook)
        {
            return;
        }
        GameEvents.RaiseSynergyChartShowRequested(gameObject.scene.handle, view.IsOpen && tab == PuzzleNotebookTab.Synergy);
    }

    void OnRumorRecorded(GameEvents.TavernRumorInfo _info)
    {
        if (_info.session == session)
        {
            CountUnseen();
            Refresh();
        }
    }

    void OnQuestChanged(GameEvents.QuestInfo _info)
    {
        if (_info.session == session)
        {
            CountUnseen();
            Refresh();
        }
    }

    // 숙련 레벨이 오른 경우만 새 소식으로 센다(경험치만 늘면 세지 않는다).
    void OnProficiencyChanged(GameEvents.ProficiencyInfo _info)
    {
        if (_info.session == session)
        {
            if (_info.LeveledUp)
            {
                CountUnseen();
            }
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
        SyncSynergyChart();
    }

    // 펼쳐 둔 수첩에서 바로 보이는 변화는 새 소식으로 세지 않는다.
    void CountUnseen()
    {
        if (!view.IsOpen)
        {
            unseenCount++;
        }
    }

    void Refresh()
    {
        string text = tab switch
        {
            PuzzleNotebookTab.Rumors => Presentation.RumorLogEntries(session.TavernRumors),
            PuzzleNotebookTab.Quests => Presentation.QuestLogEntries(session),
            PuzzleNotebookTab.Synergy => Presentation.TrainBuildSummary(session),
            _ => string.Empty
        };
        view.Render(tab, text, session.TavernRumors.Count, session.Quests.Count, unseenCount);
        if (tab == PuzzleNotebookTab.Proficiency)
        {
            bool hasNext = session.TryGetNextQuestReputationLevel(out int nextReputation, out _);
            view.RenderProficiency(session.Reputation, session.MaxActiveQuests, hasNext, nextReputation,
                session.RankedProficiencyTags.Select(session.ProficiencyStanding).ToArray(), WagonGateLine());
        }
    }

    // 평판으로 늘어나는 지금 증차 상한과 최댓값 한 줄.
    string WagonGateLine()
    {
        return Presentation.ProficiencyPanelWagonLimit(session.MaxWagonCount, TableManager.Instance.MaxWagonCountCeiling);
    }
}
