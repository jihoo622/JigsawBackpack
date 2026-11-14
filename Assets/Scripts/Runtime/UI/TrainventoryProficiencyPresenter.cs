using System.Linq;
using UnityEngine;

// 상시 노출되는 숙련 표시(기차 카드 칩, 정보 바 평판)를 갱신한다. 두 View는 선택이며 프리팹마다 한 개씩 둔다.
// 누르면 수첩을 숙련 탭으로 연다. 숫자는 세션의 ProficiencyStanding·Reputation을 그대로 쓴다.
public sealed class TrainventoryProficiencyPresenter : MonoBehaviour
{
    [SerializeField] TrainventoryProficiencyChipsView chipsView;
    [SerializeField] TrainventoryReputationView reputationView;
    PuzzleInventoryService session;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        GameEvents.ProficiencyChanged += OnProficiencyChanged;
        GameEvents.PuzzleRunStarted += OnRunStarted;
        if (chipsView)
        {
            chipsView.Clicked += OnClicked;
        }
        if (reputationView)
        {
            reputationView.Clicked += OnClicked;
        }
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.ProficiencyChanged -= OnProficiencyChanged;
        GameEvents.PuzzleRunStarted -= OnRunStarted;
        if (chipsView)
        {
            chipsView.Clicked -= OnClicked;
        }
        if (reputationView)
        {
            reputationView.Clicked -= OnClicked;
        }
    }

    void OnClicked()
    {
        GameEvents.RaiseNotebookOpenRequested(PuzzleNotebookTab.Proficiency);
    }

    void OnProficiencyChanged(GameEvents.ProficiencyInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnRunStarted(GameEvents.PuzzleRunInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        if (chipsView)
        {
            // 칩 위치가 바뀌지 않도록 태그 기본 순서로 고정한다.
            chipsView.Render(session.ProficiencyTags.Select(session.ProficiencyStanding).ToArray());
        }
        if (reputationView)
        {
            reputationView.Render(session.Reputation);
        }
    }
}
