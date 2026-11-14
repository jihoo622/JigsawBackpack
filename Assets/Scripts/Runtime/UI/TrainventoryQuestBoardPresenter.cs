using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 술집 의뢰 게시판의 표시 갱신과 수락 요청. 술집 화면이 열려 있는 동안만 활성화된다.
public sealed class TrainventoryQuestBoardPresenter : MonoBehaviour
{
    [SerializeField] TrainventoryQuestBoardView view;
    [SerializeField] TrainventoryQuestDetailView detailView;
    PuzzleInventoryService session;
    // 마지막 수락 시도의 실패 사유. 다른 마을에 도착하면 지운다.
    string acceptMessage;
    int messageVisit = -1;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        view.OfferAccepted += OnOfferAccepted;
        view.OfferSelected += OnOfferSelected;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.ProficiencyChanged += OnProficiencyChanged;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        Refresh();
    }

    void OnDisable()
    {
        view.OfferAccepted -= OnOfferAccepted;
        view.OfferSelected -= OnOfferSelected;
        GameEvents.QuestChanged -= OnQuestChanged;
        GameEvents.ProficiencyChanged -= OnProficiencyChanged;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
    }

    void OnQuestChanged(GameEvents.QuestInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    // 평판은 숙련 레벨 합이므로 판매로 레벨이 오르면 동시 진행 수 표시도 바뀐다.
    void OnProficiencyChanged(GameEvents.ProficiencyInfo _info)
    {
        if (_info.session == session && _info.LeveledUp)
        {
            Refresh();
        }
    }

    void OnFundsChanged(GameEvents.FundsInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info)
    {
        if (_info.session == session)
        {
            Refresh();
        }
    }

    void OnOfferAccepted(int _index)
    {
        var offers = session.QuestOffers;
        if (_index < 0 || _index >= offers.Count)
        {
            return;
        }
        var offer = offers[_index];
        var failure = session.TryAcceptQuest(offer);
        acceptMessage = failure == TrainventoryQuestAcceptFailure.None
            ? null
            : Presentation.QuestAcceptFailure(failure, offer, session.MaxActiveQuests);
        messageVisit = session.CityMoveCount;
        // 수락하면 게시판 목록이 당겨지므로 선택을 해제해 다른 의뢰의 상세가 남지 않게 한다.
        if (failure == TrainventoryQuestAcceptFailure.None)
        {
            view.SetSelectedIndex(-1);
        }
        Refresh();
    }

    void OnOfferSelected(int _index)
    {
        var offers = session.QuestOffers;
        if (_index >= 0 && _index < offers.Count)
        {
            detailView?.Bind(offers[_index], session);
        }
        else
        {
            detailView?.Bind(null, session);
        }
    }

    void Refresh()
    {
        if (messageVisit != session.CityMoveCount)
        {
            acceptMessage = null;
        }
        var offers = session.QuestOffers;
        var shown = offers.Take(view.SlotCount).ToArray();
        var contents = new List<TrainventoryQuestBoardView.OfferContent>();
        string blocked = null;
        foreach (var offer in shown)
        {
            var failure = session.EvaluateQuestAccept(offer);
            if (failure != TrainventoryQuestAcceptFailure.None && blocked == null)
            {
                blocked = Presentation.QuestAcceptFailure(failure, offer, session.MaxActiveQuests);
            }
            contents.Add(new TrainventoryQuestBoardView.OfferContent(Presentation.QuestOffer(offer),
                Presentation.QuestAcceptLabel(offer), failure == TrainventoryQuestAcceptFailure.None));
        }

        // 안내 문구 우선순위: 수락 실패 사유 → 의뢰 없음 → 수락할 수 없는 이유 → 슬롯 밖 의뢰 수.
        string message = acceptMessage;
        if (message == null && offers.Count == 0)
        {
            message = Presentation.QuestNoOffers;
        }
        message ??= blocked;
        if (message == null && offers.Count > shown.Length)
        {
            message = Presentation.QuestMoreOffers(offers.Count - shown.Length);
        }

        // 선택은 사용자가 슬롯을 눌렀을 때만 생긴다. 범위를 벗어나면 Render가 선택 없음으로 되돌린다.
        view.Render(Presentation.QuestBoardTitle,
            Presentation.QuestStatus(session.ActiveQuestCount, session.MaxActiveQuests, session.Reputation),
            contents.ToArray(), message);

        int selectedIndex = view.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < shown.Length)
        {
            detailView?.Bind(shown[selectedIndex], session);
        }
        else
        {
            detailView?.Bind(null, session);
        }
    }
}
