using System.Collections.Generic;
using UnityEngine;

// 행선지 렌즈(L0–L3) 연결. 화물을 잡는 순간의 거래 견적, 가리킨 자리와 상위 후보의 다음 정산 Δ, 이웃·계획 목적지의 공개 정보를
// 기차 열의 보드 아래 패널에 고정해 보여 준다(E1). 기차 화물이나 시장 화물을 잡을 때 항상 연다(F5). 표시 전용이며 확정은 기존 Drop 경로를 거친다.
// 계획: Docs/Research/ROUTE_LENS_IMPLEMENTATION_PLAN.md §5
public sealed partial class PuzzleInventoryPresenter
{
    // 누름·회전 때의 후보 계산이 이보다 오래 걸리면 이번 판에서는 상위 후보 계산을 끈다(계획 §5 L2 성능 예산).
    const double LENS_RANKING_BUDGET_MS = 4.0;

    [SerializeField] PuzzleRouteLensView routeLens;

    PuzzleLensTab lensTab = PuzzleLensTab.Current;
    PuzzleItemDefinition lensDefinition;
    decimal lensValue;
    // 시장에서 사는 화물은 아직 기차에 없으므로 운임 계산에 그 칸 수를 더한다.
    bool lensItemOnTrain;
    long lensFundsBefore;
    PuzzleTradeDirection lensDirection;
    string lensTitle;
    string lensTrade;
    string lensSpotText;
    string lensRankingText;
    bool lensRankingTooSlow;
    PuzzleInventory lastLensSpotDestination;
    Vector2Int lastLensSpotCell = new Vector2Int(int.MinValue, int.MinValue);
    int lastLensSpotRotation = -1;
    PuzzlePlacementDelta cachedLensSpot;
    readonly List<(PuzzlePose Pose, Vector2Int TargetCell, string Label)> lensMarkers = new();
    readonly List<decimal> lensTopDeltas = new();

    PuzzleRouteLensView RouteLens => routeLens;
    // 렌즈는 기차 열에 있으므로 기차 보드가 보이고 이동 중이 아닐 때 연다.
    bool LensAvailable => ActiveTrainView && ActiveTrainView.gameObject.activeInHierarchy && !TrainOperationsBlocked &&
        session != null && session.RunStatus == PuzzleRunStatus.InProgress;
    bool LensVisible => (RouteLens && RouteLens.IsShowing) || (RelayoutSettings.Enabled && trainWindowView && trainWindowView.gameObject.activeInHierarchy);

    void SubscribeLens()
    {
        if (RouteLens)
        {
            RouteLens.TabSelected += OnLensTabSelected;
        }
        PuzzleLensSettings.Changed += OnLensSettingsChanged;
    }

    void UnsubscribeLens()
    {
        if (RouteLens)
        {
            RouteLens.TabSelected -= OnLensTabSelected;
        }
        PuzzleLensSettings.Changed -= OnLensSettingsChanged;
    }

    // L0: 잡는 순간의 거래 문맥을 고정한다. 앞선 거래 결과(D7)는 여기서만 교체된다.
    void BeginLens(PuzzlePlacement _placement)
    {
        ResetLensSpot();
        UpdateLensRanking();
        bool fromMarket = session.Market != null && ReferenceEquals(_placement.Owner, session.Market);
        bool fromTrain = session.IsTrainWagon(_placement.Owner);
        if (!LensAvailable || (!fromMarket && !fromTrain))
        {
            HideLensPanel();
            return;
        }

        // 기차 화물은 시장이 열려 있을 때만 판매 견적을 보인다. 아니면 기차 안 재배치다(F5).
        bool marketOpen = !MarketOperationsBlocked && marketView && marketView.gameObject.activeInHierarchy;
        lensDirection = fromMarket ? PuzzleTradeDirection.Buy
            : marketOpen ? PuzzleTradeDirection.Sell : PuzzleTradeDirection.None;
        lensDefinition = _placement.Definition;
        lensValue = _placement.FreshnessAdjustedValue;
        lensItemOnTrain = !fromMarket;
        lensFundsBefore = session.Funds;
        long? quote = lensDirection != PuzzleTradeDirection.None &&
            session.TryQuoteTrade(_placement, lensDirection, out long price) ? price : (long?)null;
        lensTitle = Presentation.RouteLensTitle(lensDefinition);
        lensTrade = Presentation.RouteLensTrade(lensDirection, quote, lensFundsBefore, null, _committed: false);
        if (RouteLens)
        {
            RouteLens.Show(lensTitle, lensTrade, JoinLensLines(lensSpotText, lensRankingText), LensDestinationText(),
                Presentation.RouteLensFootnote(lensTab), lensTab, _tabsInteractable: false);
        }
        trainWindowView?.SetLensSummary(lensTrade, lensSpotText);
    }

    // L2: 현재 화차·현재 회전의 상위 후보. 누름·회전·화차 전환 때만 계산한다.
    void UpdateLensRanking()
    {
        lensMarkers.Clear();
        lensTopDeltas.Clear();
        lensRankingText = string.Empty;
        if (ActiveTrainView)
        {
            ActiveTrainView.HidePlacementCandidates();
        }
        if (drag == null || session?.Train == null || !PuzzleLensSettings.ShowCandidates || lensRankingTooSlow)
        {
            return;
        }

        var ranking = session.RankPlacements(drag.OriginalPlacement, session.Train, drag.GrabbedCell, drag.ClockwiseTurns);
        if (ranking.ElapsedMilliseconds > LENS_RANKING_BUDGET_MS)
        {
            lensRankingTooSlow = true;
            Debug.LogWarning($"배치 후보 계산이 {ranking.ElapsedMilliseconds:0.0}ms 걸려 이번 판에서는 상위 후보 표시를 끕니다.", this);
        }

        for (int i = 0; i < ranking.Top.Count; i++)
        {
            var top = ranking.Top[i];
            lensTopDeltas.Add(top.SettlementDelta);
            lensMarkers.Add((top.Pose, top.TargetCell, Presentation.RouteLensCandidateBadge(i + 1, top.SettlementDelta)));
        }
        lensRankingText = Presentation.RouteLensRanking(lensTopDeltas);
        if (ActiveTrainView && ActiveTrainView.gameObject.activeInHierarchy)
        {
            ActiveTrainView.ShowPlacementCandidates(lensMarkers);
        }
    }

    // L1: 포인터가 가리킨 자리 하나의 정산 Δ. 기존 시너지 칩과 같은 (보드·칸·회전) 키로 캐시한다.
    void UpdateLensSpot(PuzzleInventory _destination, Vector2Int _targetCell, bool _canDrop)
    {
        if (!LensVisible || drag == null)
        {
            return;
        }

        string spot = null;
        if (_destination != null && session.IsTrainWagon(_destination))
        {
            if (!_canDrop)
            {
                spot = Presentation.RouteLensPlacementSpot(null, 0);
            }
            else
            {
                if (_destination != lastLensSpotDestination || _targetCell != lastLensSpotCell ||
                    drag.ClockwiseTurns != lastLensSpotRotation)
                {
                    lastLensSpotDestination = _destination;
                    lastLensSpotCell = _targetCell;
                    lastLensSpotRotation = drag.ClockwiseTurns;
                    cachedLensSpot = session.PreviewPlacementDelta(drag.OriginalPlacement, _destination, drag.GrabbedCell,
                        _targetCell, drag.ClockwiseTurns);
                }
                spot = Presentation.RouteLensPlacementSpot(cachedLensSpot?.SettlementDelta, cachedLensSpot?.Affected.Count ?? 0);
            }
        }
        lensSpotText = spot;
        if (RouteLens)
        {
            RouteLens.SetPlacement(JoinLensLines(lensSpotText, lensRankingText));
        }
        trainWindowView?.SetLensSummary(lensTrade, lensSpotText);
    }

    // 거래 직후 결과를 남긴다(D7: 다음 화물을 누를 때까지 유지). 실패·취소면 잡기 전 문맥을 그대로 둔다.
    void FinishLens(PuzzleDropResult _result)
    {
        lensMarkers.Clear();
        if (!LensVisible)
        {
            return;
        }

        string placement = JoinLensLines(lensSpotText, lensRankingText);
        if (_result.CanDrop)
        {
            long? price = _result.Direction == PuzzleTradeDirection.None ? (long?)null : _result.Price;
            lensTrade = Presentation.RouteLensTrade(_result.Direction, price, lensFundsBefore, null, _committed: true);
            // 이제 기차에 실렸거나 팔렸으므로 운임에 따로 더할 칸이 없다.
            lensItemOnTrain = true;
            placement = lensSpotText ?? string.Empty;
        }
        if (RouteLens)
        {
            RouteLens.Show(lensTitle, lensTrade, placement, LensDestinationText(), Presentation.RouteLensFootnote(lensTab),
                lensTab, _tabsInteractable: true);
        }
        trainWindowView?.SetLensSummary(lensTrade, lensSpotText);
    }

    void HideLens()
    {
        ResetLensSpot();
        lensMarkers.Clear();
        if (ActiveTrainView)
        {
            ActiveTrainView.HidePlacementCandidates();
        }
        HideLensPanel();
    }

    void HideLensPanel()
    {
        if (RouteLens)
        {
            RouteLens.Hide();
        }
        trainWindowView?.SetLensSummary(string.Empty, string.Empty);
    }

    void ResetLensSpot()
    {
        lensSpotText = null;
        cachedLensSpot = null;
        lastLensSpotDestination = null;
        lastLensSpotCell = new Vector2Int(int.MinValue, int.MinValue);
        lastLensSpotRotation = -1;
    }

    string LensDestinationText()
    {
        if (lensTab == PuzzleLensTab.Current || lensDefinition == null)
        {
            return string.Empty;
        }
        return Presentation.RouteLensDestinations(session.LensDestinations(lensDefinition, lensValue, lensItemOnTrain), lensTab);
    }

    // 드래그 중에는 탭을 잠근다(계획 §6). 거래 결과를 보는 동안에만 바꿀 수 있다.
    void OnLensTabSelected(PuzzleLensTab _tab)
    {
        if (drag != null || !LensVisible)
        {
            return;
        }
        lensTab = _tab;
        RouteLens.SetDestination(LensDestinationText(), Presentation.RouteLensFootnote(lensTab), lensTab);
    }

    void OnLensSettingsChanged()
    {
        if (drag != null)
        {
            UpdateLensRanking();
        }
        else
        {
            lensRankingText = string.Empty;
            if (ActiveTrainView)
            {
                ActiveTrainView.HidePlacementCandidates();
            }
        }
        if (RouteLens && RouteLens.IsShowing)
        {
            RouteLens.SetPlacement(JoinLensLines(lensSpotText, lensRankingText));
        }
    }

    static string JoinLensLines(string _first, string _second)
    {
        if (string.IsNullOrEmpty(_first))
        {
            return _second ?? string.Empty;
        }
        return string.IsNullOrEmpty(_second) ? _first : _first + "\n" + _second;
    }
}
