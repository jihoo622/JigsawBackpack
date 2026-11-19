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
    readonly List<TrainventoryQuest> deliverableSlots = new();

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
        GameEvents.PuzzleActionCommitted += OnPuzzleActionCommitted;
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
        GameEvents.PuzzleActionCommitted -= OnPuzzleActionCommitted;
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

    // 화물 재배치·판매·증차처럼 기차가 바뀌는 행동이 성공하면 수락 가능 여부(빈 칸)를 다시 판정한다.
    // 앞선 수락 실패 문구는 이제 맞지 않을 수 있으므로 지운다. 실패한 행동은 기차를 바꾸지 않아 무시한다.
    void OnPuzzleActionCommitted(GameEvents.PuzzleActionInfo _info)
    {
        if (_info.session != session || !string.IsNullOrEmpty(_info.failure))
        {
            return;
        }
        acceptMessage = null;
        Refresh();
    }

    void OnOfferAccepted(int _index)
    {
        if (_index >= 0 && _index < deliverableSlots.Count)
        {
            var deliverable = deliverableSlots[_index];
            if (!session.TryCompleteQuest(deliverable, out string fail))
            {
                acceptMessage = fail;
                messageVisit = session.CityMoveCount;
            }
            else
            {
                acceptMessage = null;
                view.SetSelectedIndex(-1);
            }
            Refresh();
            return;
        }
        int offerIdx = _index - deliverableSlots.Count;
        var offers = session.QuestOffers;
        if (offerIdx < 0 || offerIdx >= offers.Count)
        {
            return;
        }
        var offer = offers[offerIdx];
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
        int offerIdx = _index - deliverableSlots.Count;
        var offers = session.QuestOffers;
        if (offerIdx >= 0 && offerIdx < offers.Count)
        {
            detailView?.Bind(offers[offerIdx], session);
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
        deliverableSlots.Clear();
        var contents = new List<TrainventoryQuestBoardView.OfferContent>();
        foreach (var q in session.Quests)
        {
            if (contents.Count >= view.SlotCount) break;
            if (q.State == TrainventoryQuestState.Active &&
                string.Equals(q.TargetCityId, session.CurrentCityId, System.StringComparison.Ordinal))
            {
                deliverableSlots.Add(q);
                var def = session.ItemDefinitions?.FirstOrDefault(_d => _d.Id == q.PieceId);
                string cargoName = def != null ? def.DisplayName : q.PieceId;
                contents.Add(new TrainventoryQuestBoardView.OfferContent(
                    Presentation.QuestDeliverableOffer(cargoName, q.Reward),
                    Presentation.QuestCompleteLabel, true));
            }
        }

        var offers = session.QuestOffers;
        int offerRoom = Mathf.Max(0, view.SlotCount - contents.Count);
        var shown = offers.Take(offerRoom).ToArray();
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
        if (message == null && offers.Count == 0 && deliverableSlots.Count == 0)
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

        int selectedOfferIdx = view.SelectedIndex - deliverableSlots.Count;
        if (selectedOfferIdx >= 0 && selectedOfferIdx < shown.Length)
        {
            detailView?.Bind(shown[selectedOfferIdx], session);
        }
        else
        {
            detailView?.Bind(null, session);
        }
    }
}
