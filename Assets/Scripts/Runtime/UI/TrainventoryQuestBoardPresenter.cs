using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 술집 의뢰 게시판의 표시 갱신과 수락 요청. 술집 화면이 열려 있는 동안만 활성화된다.
public sealed class TrainventoryQuestBoardPresenter : MonoBehaviour
{
    [SerializeField] TrainventoryQuestBoardView view;
    PuzzleInventoryService session;
    // 마지막 수락 시도의 실패 사유. 다른 마을에 도착하면 지운다.
    string acceptMessage;
    int messageVisit = -1;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        session.Initialize();
        view.OfferAccepted += OnOfferAccepted;
        GameEvents.QuestChanged += OnQuestChanged;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        Refresh();
    }

    void OnDisable()
    {
        view.OfferAccepted -= OnOfferAccepted;
        GameEvents.QuestChanged -= OnQuestChanged;
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
        Refresh();
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
        view.Render(Presentation.QuestBoardTitle,
            Presentation.QuestStatus(session.ActiveQuestCount, session.MaxActiveQuests, session.Reputation),
            contents.ToArray(), message);
    }
}
