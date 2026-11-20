using TMPro;
using UnityEngine;

// S6: 시청 부동 창(읽기 전용). 도시 물가/선호·풍부·희귀·산업, 평판, 결산 목표 진행을 표시한다.
[DisallowMultipleComponent]
public sealed class CityHallWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI priceIndexText;
    [SerializeField] CityTradePreferencesView tradePreferencesView;
    [SerializeField] TextMeshProUGUI industryText;
    [SerializeField] TextMeshProUGUI reputationText;
    [SerializeField] TextMeshProUGUI settlementText;

    PuzzleInventoryService session;

    public FloatingWindow FloatingWindow => floatingWindow;
    public bool IsOpen => floatingWindow && floatingWindow.IsOpen;

    void OnEnable()
    {
        session = SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        GameEvents.CityArrived += OnCityArrived;
        GameEvents.FundsChanged += OnFundsChanged;
        GameEvents.TimeAdvanced += OnTimeAdvanced;
        GameEvents.QuestChanged += OnQuestChanged;
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.CityArrived -= OnCityArrived;
        GameEvents.FundsChanged -= OnFundsChanged;
        GameEvents.TimeAdvanced -= OnTimeAdvanced;
        GameEvents.QuestChanged -= OnQuestChanged;
    }

    public void SetOpen(bool _open, bool _persist = true)
    {
        if (floatingWindow)
        {
            floatingWindow.SetOpen(_open, _persist);
        }
        else if (gameObject.activeSelf != _open)
        {
            gameObject.SetActive(_open);
        }
        if (_open)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        session ??= SceneServices.For<PuzzleInventoryService>(gameObject.scene);
        if (session == null || session.CityData == null)
        {
            return;
        }
        var city = session.CityData;
        if (titleText)
        {
            titleText.text = Presentation.CityHallWindowTitle(city.displayName);
        }
        if (priceIndexText)
        {
            string regionText = city.buyPercentOfBaseValue > 0 ? $"{city.buyPercentOfBaseValue}%" : "-";
            string sellText = city.sellPercentOfFinalValue > 0 ? $"{city.sellPercentOfFinalValue}%" : "-";
            priceIndexText.text = $"{regionText} / {sellText}";
        }
        if (tradePreferencesView)
        {
            tradePreferencesView.Show(Presentation.GetCityTradePreferences(city));
        }
        if (industryText)
        {
            string workshopName = session.HasButchery()
                ? (session.ButcheryAction()?.title ?? string.Empty)
                : session.HasModification()
                    ? (session.ModificationAction()?.title ?? string.Empty)
                    : session.HasRefining()
                        ? (session.RefiningAction()?.title ?? string.Empty)
                        : Presentation.BuildingWorkshopEmpty;
            industryText.text = Presentation.CityHallIndustryFormat(workshopName);
        }
        if (reputationText)
        {
            reputationText.text = Presentation.CityHallReputationFormat(
                session.Reputation, session.MaxActiveQuests);
        }
        if (settlementText && session.Time != null)
        {
            settlementText.text = Presentation.CityHallGoalFormat(
                session.Funds, session.TargetFunds, session.Time.RemainingTurns);
        }
    }

    void OnCityArrived(GameEvents.CityArrivalInfo _info) => Refresh();
    void OnFundsChanged(GameEvents.FundsInfo _info) => Refresh();
    void OnTimeAdvanced(GameEvents.TimeAdvanceInfo _info) => Refresh();
    void OnQuestChanged(GameEvents.QuestInfo _info) => Refresh();
}
