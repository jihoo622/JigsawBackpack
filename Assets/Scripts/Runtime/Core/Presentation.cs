using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using UnityEngine.UI;
using UnityEngine;

// 색과 설명 문자열의 단일 출처.
// 선택지 카드·정보 패널·슬롯 핍·장식 오브젝트가 모두 여기를 읽고, 개별 UI가 자기 색과 문구를 가지지 않는다.
// 설명 원문은 테이블의 description이 원본이고, 이 파일은 정렬·강조 같은 표기만 덧붙인다.
public enum EffectCategory
{
    Offense, Mobility, Defense, Utility, BaseStat
}

public static class Presentation
{
    public static Color SharedColor(string _id)
    {
        return BasicData.Color(_id);
    }

    public static Color PuzzleUnmodifiedTint => BasicData.Color("presentation.inventory.unmodifiedTint");
    public static Color PuzzleTextOutline => BasicData.Color("presentation.inventory.textOutline");
    public static Color PuzzleSynergyNotice => BasicData.Color("presentation.inventory.synergyNotice");

    public static string TavernTitle(string _cityName)
    {
        return BasicData.Format("presentation.station.title", _cityName);
    }

    public static string TavernRumor(string _cityName, string _tag)
    {
        return BasicData.Format("presentation.station.rumor", _cityName, PuzzleTagLabel(_tag));
    }

    public static string RumorLogTitle => BasicData.Text("presentation.rumorLog.title");
    public static string RumorLogCollapse => BasicData.Text("presentation.rumorLog.collapse");
    public static string RumorLogEmpty => BasicData.Text("presentation.rumorLog.empty");

    public static string RumorLogCount(int _count)
    {
        return BasicData.Format("presentation.rumorLog.count", _count);
    }

    public static string RumorLogCollapsed(int _rumorCount, int _questCount)
    {
        return BasicData.Format("presentation.rumorLog.collapsed", _rumorCount, _questCount);
    }

    public static string RumorLogRumorTab => BasicData.Text("presentation.rumorLog.rumorTab");
    public static string RumorLogQuestTab => BasicData.Text("presentation.rumorLog.questTab");

    // 누적 경과 분을 게임 날짜·시각으로 표기한다. 소문·의뢰 기록이 같은 표기를 쓴다.
    internal static string GameTimestamp(long _elapsedMinutes)
    {
        var tables = TableManager.Instance;
        long clock = _elapsedMinutes % PuzzleTimeManager.MINUTES_PER_DAY +
            tables.PuzzleTime.startHour * PuzzleTimeManager.MINUTES_PER_HOUR;
        long day = _elapsedMinutes / PuzzleTimeManager.MINUTES_PER_DAY + clock / PuzzleTimeManager.MINUTES_PER_DAY;
        int minuteOfDay = (int)(clock % PuzzleTimeManager.MINUTES_PER_DAY);
        return BasicData.Format("presentation.rumorLog.time", day,
            minuteOfDay / PuzzleTimeManager.MINUTES_PER_HOUR, minuteOfDay % PuzzleTimeManager.MINUTES_PER_HOUR);
    }

    internal static string RumorLogEntries(IReadOnlyList<PuzzleInventoryService.TavernRumor> _rumors)
    {
        if (_rumors.Count == 0)
        {
            return RumorLogEmpty;
        }
        var entries = new List<string>();
        var tables = TableManager.Instance;
        foreach (var rumor in _rumors)
        {
            string timestamp = GameTimestamp(rumor.RecordedMinutes);
            entries.Add(BasicData.Format("presentation.rumorLog.entry", timestamp,
                tables.CityPrice(rumor.SourceCityId).displayName, rumor.Message));
        }
        return string.Join(BasicData.Text("presentation.rumorLog.separator"), entries);
    }

    #region 술집 의뢰

    public static string QuestBoardTitle => BasicData.Text("presentation.quest.boardTitle");
    public static string QuestNoOffers => BasicData.Text("presentation.quest.noOffers");

    public static string QuestStatus(int _active, int _max, int _reputation)
    {
        return BasicData.Format("presentation.quest.status", _active, _max, _reputation);
    }

    public static string QuestMoreOffers(int _count)
    {
        return BasicData.Format("presentation.quest.moreOffers", _count);
    }

    static string QuestCargoName(string _pieceId)
    {
        return TableManager.Instance.Piece(_pieceId).displayName;
    }

    static string QuestCityName(string _cityId)
    {
        return TableManager.Instance.CityPrice(_cityId).displayName;
    }

    // 실패 시 받는 불이익. 보증금은 금액을, 지역 거래 불이익은 의뢰를 준 지역 이름을 함께 표시한다.
    static string QuestPenaltyText(string _penaltyId, long _deposit, string _sourceCityId)
    {
        var tables = TableManager.Instance;
        var penalty = tables.QuestPenalty(_penaltyId);
        return penalty.type == nameof(TrainventoryQuestPenaltyType.Deposit)
            ? BasicData.Format("presentation.quest.depositPenalty", penalty.displayName, _deposit)
            : BasicData.Format("presentation.quest.regionPenalty", penalty.displayName,
                tables.Region(tables.CityPrice(_sourceCityId).regionId).displayName);
    }

    internal static string QuestOffer(TrainventoryQuestOffer _offer)
    {
        return BasicData.Format("presentation.quest.offer", QuestCargoName(_offer.PieceId), QuestCityName(_offer.TargetCityId),
            _offer.TravelHours, _offer.DeadlineHours, _offer.Reward,
            QuestPenaltyText(_offer.PenaltyId, _offer.Deposit, _offer.SourceCityId));
    }

    internal static string QuestAcceptLabel(TrainventoryQuestOffer _offer)
    {
        return _offer.Deposit > 0
            ? BasicData.Format("presentation.quest.acceptWithDeposit", _offer.Deposit)
            : BasicData.Text("presentation.quest.accept");
    }

    internal static string QuestAcceptFailure(TrainventoryQuestAcceptFailure _failure, TrainventoryQuestOffer _offer, int _maxActive)
    {
        return _failure switch
        {
            TrainventoryQuestAcceptFailure.RunFinished => BasicData.Text("presentation.quest.failRunFinished"),
            TrainventoryQuestAcceptFailure.ActiveLimit => BasicData.Format("presentation.quest.failActiveLimit", _maxActive),
            TrainventoryQuestAcceptFailure.InsufficientFunds => BasicData.Format("presentation.quest.failInsufficientFunds", _offer.Deposit),
            TrainventoryQuestAcceptFailure.NoSpace => BasicData.Text("presentation.quest.failNoSpace"),
            _ => BasicData.Text("presentation.quest.failUnavailable")
        };
    }

    // 상태 변화 직후 하단 거래 상태 줄에 띄우는 한 줄 결과.
    internal static string QuestChanged(GameEvents.QuestInfo _info)
    {
        string cargo = QuestCargoName(_info.pieceId);
        if (_info.state == nameof(TrainventoryQuestState.Completed))
        {
            return BasicData.Format("presentation.quest.completed", cargo, _info.payout);
        }
        if (_info.state == nameof(TrainventoryQuestState.Failed))
        {
            return _info.failure == nameof(TrainventoryQuestFailure.Overdue)
                ? BasicData.Format("presentation.quest.failedOverdue", cargo)
                : BasicData.Format("presentation.quest.failedCargoLost", cargo);
        }
        return BasicData.Format("presentation.quest.accepted", cargo, GameTimestamp(_info.deadlineMinutes),
            QuestCityName(_info.targetCityId));
    }

    // 수첩 의뢰 탭 본문. 평판·진행 수, 지역 불이익, 진행 중 의뢰, 끝난 의뢰(최근 순) 순서다.
    internal static string QuestLogEntries(PuzzleInventoryService _session)
    {
        var tables = TableManager.Instance;
        var entries = new List<string>
        {
            BasicData.Format("presentation.quest.logHeader", _session.Reputation, _session.ActiveQuestCount, _session.MaxActiveQuests)
        };
        foreach (var region in tables.Puzzle.regions.OrderBy(_region => _region.id, StringComparer.Ordinal))
        {
            if (_session.TryGetRegionPenalty(region.id, out var penalty))
            {
                entries.Add(BasicData.Format("presentation.quest.logRegionPenalty", region.displayName,
                    GameTimestamp(penalty.UntilMinutes), penalty.BuyPercentDelta, penalty.SellPercentDelta));
            }
        }
        var quests = _session.Quests;
        if (quests.Count == 0)
        {
            entries.Add(BasicData.Text("presentation.quest.logEmpty"));
        }
        foreach (var quest in quests.Where(_quest => _quest.State == TrainventoryQuestState.Active))
        {
            entries.Add(BasicData.Format("presentation.quest.logActive", QuestCargoName(quest.PieceId),
                QuestCityName(quest.TargetCityId), GameTimestamp(quest.DeadlineMinutes), quest.Reward,
                QuestPenaltyText(quest.PenaltyId, quest.Deposit, quest.SourceCityId)));
        }
        foreach (var quest in quests.Where(_quest => _quest.State != TrainventoryQuestState.Active).Reverse())
        {
            entries.Add(quest.State == TrainventoryQuestState.Completed
                ? BasicData.Format("presentation.quest.logCompleted", QuestCargoName(quest.PieceId),
                    QuestCityName(quest.TargetCityId), quest.Payout)
                : BasicData.Format("presentation.quest.logFailed", QuestCargoName(quest.PieceId),
                    QuestCityName(quest.TargetCityId), quest.Failure == TrainventoryQuestFailure.Overdue
                        ? BasicData.Text("presentation.quest.failureOverdue")
                        : BasicData.Text("presentation.quest.failureCargoLost")));
        }
        return string.Join(BasicData.Text("presentation.quest.separator"), entries);
    }

    #endregion

    public static string TavernNoRumor => BasicData.Text("presentation.station.noRumor");
    public static string TavernUnreadRumor => BasicData.Text("presentation.station.unreadRumor");
    public static string TavernTalk => BasicData.Text("presentation.station.talk");

    // 날짜 표기 도입 시 시간 데이터와 분리해 이 표시 경로만 교체한다.
    public static string PuzzleTime(PuzzleTimeManager _time)
    {
        return BasicData.Format("presentation.time.clock", _time.Day, _time.Hour, _time.Minute, (_time.IsNight ? BasicData.Text("presentation.time.clockText2") : BasicData.Text("presentation.time.clockText3")));
    }

    public static Color PuzzleTimeAccent => BasicData.Color("presentation.time.accent");

    public static string PuzzleMapTitle => BasicData.Text("presentation.map.title");
    public const string PUZZLE_MAP_PANEL_NAME = "PuzzleWorldMap";
    public static string PuzzleMapOpen => BasicData.Text("presentation.map.open");
    public static string PuzzleMapClose => BasicData.Text("presentation.map.close");
    public static string PuzzleMapCurrentText => BasicData.Text("presentation.map.current");
    public static string PuzzleMapTargetText => BasicData.Text("presentation.map.target");
    public static string PuzzleMapCurrentAndTarget => BasicData.Text("presentation.map.currentAndTarget");

    internal static string PuzzleMapBadgeText(bool _current, bool _planningTarget)
    {
        if (_current && _planningTarget)
        {
            return PuzzleMapCurrentAndTarget;
        }
        return _current ? PuzzleMapCurrentText : PuzzleMapTargetText;
    }
    public const string PUZZLE_TRAVEL_PANEL_NAME = "MapTravelPanel";
    public static string PuzzleTravelQuestion => BasicData.Text("presentation.inventory.travelQuestion");
    public static string PuzzleTravelConfirm => BasicData.Text("presentation.inventory.travelConfirm");
    public static string PuzzleTravelCancel => BasicData.Text("presentation.inventory.travelCancel");
    public static string PuzzleTravelInProgress => BasicData.Text("presentation.inventory.travelInProgress");
    public static string PuzzleTravelArrived => BasicData.Text("presentation.inventory.travelArrived");
    public static Color PuzzleTravelDim => BasicData.Color("presentation.inventory.travelDim");
    public static Color PuzzleTravelCard => BasicData.Color("presentation.inventory.travelCard");

    internal static string PuzzleTravelConfirmation(string _fromName, string _toName, PuzzleTravelResult _quote)
    {
        return BasicData.Format("presentation.inventory.travelConfirmation", _fromName, _toName, PuzzleMapTime(_quote.TravelMinutes), _quote.ArrivalClockMinutes / PuzzleTimeManager.MINUTES_PER_HOUR, _quote.ArrivalClockMinutes % PuzzleTimeManager.MINUTES_PER_HOUR, (_quote.ArrivalIsNight ? BasicData.Text("presentation.inventory.travelConfirmationText2") : BasicData.Text("presentation.inventory.travelConfirmationText3")), _quote.Cost, _quote.FundsAfter);
    }

    internal static string PuzzleTravelJourney(string _fromName, string _toName)
    {
        return BasicData.Format("presentation.inventory.travelJourney", _fromName, _toName);
    }

    public static string PuzzleTravelProgress(float _progress)
    {
        return BasicData.Format("presentation.inventory.travelProgress", Mathf.RoundToInt(_progress * BasicData.Float("presentation.inventory.puzzleTravelProgress")));
    }
    public static string PuzzleMapFit => BasicData.Text("presentation.map.fit");
    public static string PuzzleWaitDaylight => BasicData.Text("presentation.inventory.waitDaylight");
    public static string StationActionsTitle => BasicData.Text("presentation.station.actionsTitle");
    internal static string StationWagonShop => BasicData.Text("presentation.station.wagonShop");
    internal static string StationWagonShopPage(int _page, int _pageCount) =>
        BasicData.Format("presentation.station.wagonShopPage", _page, _pageCount);
    internal static string WagonProductLabel(string _name, long _price) =>
        BasicData.Format("presentation.station.wagonProduct", _name, _price);
    public static string StationMarket => BasicData.Text("presentation.station.market");
    public static string StationMarketClosed => BasicData.Text("presentation.station.marketClosed");
    public static string StationTavern => BasicData.Text("presentation.station.tavern");
    public static string StationRest => BasicData.Text("presentation.station.rest");
    public static string StationTravel => BasicData.Text("presentation.station.travel");
    public static string StationBack => BasicData.Text("presentation.station.back");
    public static string StationRestHint => BasicData.Format("presentation.station.restHint", TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours);
    public static string StationRestDuration(long _minutes)
    {
        return BasicData.Format("presentation.station.restDuration", PuzzleMapTime(_minutes));
    }
    public static string StationButchery => BasicData.Text("presentation.StationButchery");
    public static string StationButcheryConfirm => BasicData.Text("presentation.StationButcheryConfirm");
    // 확인 버튼 라벨에 표 행동 비용을 붙인다. 비용을 모르면 기본 라벨을 쓴다.
    public static string StationButcheryConfirmCost(long _cost) =>
        BasicData.Format("presentation.StationButcheryConfirmCost", _cost);
    public static string PuzzleButcheryInsufficientFunds(long _cost) =>
        BasicData.Format("rules.butchery.insufficientFunds", _cost);
    public static string PuzzleButcheryTitle => BasicData.Text("presentation.PuzzleButcheryTitle");
    // 도축대 상태 문구의 단일 출처. 버튼·보드·hint가 같은 문구를 읽는다.
    public static string StationButcheryStatus(PuzzleButcheryPhase _phase, long _remainingMinutes)
    {
        return _phase switch
        {
            PuzzleButcheryPhase.Working => BasicData.Format("presentation.StationButcheryCountdown.text1", PuzzleMapTime(_remainingMinutes)),
            PuzzleButcheryPhase.Ready => BasicData.Text("presentation.StationButcheryReady"),
            _ => BasicData.Text("presentation.StationButcheryIdleHint")
        };
    }
    public static string StationRefining => BasicData.Text("presentation.StationRefining");
    public static string StationRefiningConfirm => BasicData.Text("presentation.StationRefiningConfirm");
    // 확인 버튼 라벨에 표 행동 비용을 붙인다. 비용을 모르면 기본 라벨을 쓴다.
    public static string StationRefiningConfirmCost(long _cost) =>
        BasicData.Format("presentation.StationRefiningConfirmCost", _cost);
    public static string PuzzleRefiningInsufficientFunds(long _cost) =>
        BasicData.Format("rules.refining.insufficientFunds", _cost);
    public static string PuzzleRefiningTitle => BasicData.Text("presentation.PuzzleRefiningTitle");
    // 정제대 상태 문구의 단일 출처. 버튼·보드·hint가 같은 문구를 읽는다.
    public static string StationRefiningStatus(PuzzleRefiningPhase _phase, long _remainingMinutes)
    {
        return _phase switch
        {
            PuzzleRefiningPhase.Working => BasicData.Format("presentation.StationRefiningCountdown.text1", PuzzleMapTime(_remainingMinutes)),
            PuzzleRefiningPhase.Ready => BasicData.Text("presentation.StationRefiningReady"),
            _ => BasicData.Text("presentation.StationRefiningIdleHint")
        };
    }
    public static string StationModification => BasicData.Text("presentation.StationModification");
    public static string StationModificationConfirm => BasicData.Text("presentation.StationModificationConfirm");
    // 확인 버튼 라벨에 선택 모드의 비용을 붙인다. 모드를 고르지 않았으면 기본 라벨을 쓴다.
    public static string StationModificationConfirmCost(long _cost) =>
        BasicData.Format("presentation.StationModificationConfirmCost", _cost);
    public static string PuzzleModificationInsufficientFunds(long _cost) =>
        BasicData.Format("rules.modification.insufficientFunds", _cost);
    public static string PuzzleModificationTitle => BasicData.Text("presentation.PuzzleModificationTitle");
    public static string PuzzleModificationStatus(PuzzleInventoryService _session)
    {
        if (_session == null || !_session.HasModification())
        {
            return BasicData.Text("rules.modification.unavailable");
        }
        bool selected = _session.TryGetSelectedModificationMode(out var mode);
        string modeName = selected ? mode.displayName : FirstModificationModeName(_session);
        if (!selected)
        {
            return BasicData.Format("presentation.PuzzleModificationIdle", modeName);
        }
        return _session.ModificationConfirmable()
            ? BasicData.Format("presentation.PuzzleModificationReady", modeName)
            : BasicData.Format("presentation.PuzzleModificationSelected", modeName);
    }
    public static string PuzzleModificationReason(PuzzleModificationFailure _failure, PuzzleInventoryService _session)
    {
        return _failure switch
        {
            PuzzleModificationFailure.Blocked => BasicData.Text("rules.modification.blocked"),
            PuzzleModificationFailure.Unavailable => BasicData.Text("rules.modification.unavailable"),
            PuzzleModificationFailure.NoMode => BasicData.Text("rules.modification.noMode"),
            PuzzleModificationFailure.Empty => BasicData.Text("rules.modification.empty"),
            PuzzleModificationFailure.InvalidItem => BasicData.Text("rules.modification.invalidItem"),
            PuzzleModificationFailure.AlreadyModified => BasicData.Text("rules.modification.alreadyModified"),
            PuzzleModificationFailure.NoConversion => BasicData.Text("rules.modification.noConversion"),
            PuzzleModificationFailure.InsufficientFunds when _session.TryGetSelectedModificationMode(out var mode)
                => PuzzleModificationInsufficientFunds(mode.cost),
            _ => BasicData.Text("rules.modification.invalidResult")
        };
    }
    public static string PuzzleModificationReturnItems() => BasicData.Text("rules.modification.returnItems");
    static string FirstModificationModeName(PuzzleInventoryService _session)
    {
        var modes = _session.ModificationModes();
        return modes.Count > 0 ? modes[0].displayName : string.Empty;
    }
    public static string PuzzleNightMarket => BasicData.Text("presentation.inventory.nightMarket");
    public static float PuzzleMarketClosedAlpha => BasicData.Float("presentation.inventory.stationRestDurationAbovePercent");
    public static string PuzzleMapLocate => BasicData.Text("presentation.map.locate");
    public static string PuzzleMapZoomIn => BasicData.Text("presentation.map.zoomIn");
    public static string PuzzleMapZoomOut => BasicData.Text("presentation.map.zoomOut");
    public static Color PuzzleMapCurrentHalo => BasicData.Color("presentation.map.currentHalo");
    public static Color PuzzleMapTargetHalo => PuzzleMapCurrentHalo;
    public static Color PuzzleMapOcean => BasicData.Color("presentation.map.ocean");
    public static Color PuzzleMapRiver => BasicData.Color("presentation.map.river");
    public static Color PuzzleMapTerrainShallow => BasicData.Color("presentation.map.terrainShallow");
    public static Color PuzzleMapTerrainDeep => BasicData.Color("presentation.map.terrainDeep");
    public static Color PuzzleMapTerrainPlain => BasicData.Color("presentation.map.terrainPlain");
    public static Color PuzzleMapTerrainGrass => BasicData.Color("presentation.map.terrainGrass");
    public static Color PuzzleMapTerrainHill => BasicData.Color("presentation.map.terrainHill");
    public static Color PuzzleMapTerrainMountain => BasicData.Color("presentation.map.terrainMountain");
    public static Color PuzzleMapTerrainPeak => BasicData.Color("presentation.map.terrainPeak");
    public static Color PuzzleMapTerrainBorder => BasicData.Color("presentation.map.terrainBorder");
    public static string PuzzleMapZoom(float _percent)
    {
        return BasicData.Format("presentation.map.zoom", _percent);
    }
    public static string PuzzleMapGuideText => BasicData.Text("presentation.map.guide");
    public static string PuzzleMapHint => BasicData.Text("presentation.map.hint");
    public static Color PuzzleMapCurrent => BasicData.Color("presentation.map.current");
    public static Color PuzzleMapTarget => PuzzleMapCurrent;
    public static Color PuzzleMapVisited => BasicData.Color("presentation.map.visited");
    public static Color PuzzleMapUnknown => BasicData.Color("presentation.map.unknown");
    public static Color PuzzleMapRoute => BasicData.Color("presentation.map.route");
    public static Color PuzzleMapConnected => BasicData.Color("presentation.map.connected");
    public static Color PuzzleMapConnectedDisabled => BasicData.Color("presentation.map.connectedDisabled");

    public static Color PuzzleMapNodeMarkerColor(bool _current, bool _visited, bool _connected, bool _canAfford)
    {
        if (_current)
        {
            return PuzzleMapCurrent;
        }
        if (_connected)
        {
            return _canAfford ? PuzzleMapConnected : PuzzleMapConnectedDisabled;
        }
        return _visited ? PuzzleMapVisited : PuzzleMapUnknown;
    }

    internal static string PuzzleMapGuide(long _elapsedMinutes)
    {
        return BasicData.Format("presentation.map.guide2", PuzzleMapGuideText, PuzzleMapTime(_elapsedMinutes));
    }

    internal static string PuzzleMapNodeLabel(string _name, bool _visited, bool _current, bool _connected, bool _canAfford)
    {
        if (_current)
        {
            return _name + BasicData.Text("presentation.map.nodeLabel");
        }
        if (_connected)
        {
            return _name + (_canAfford ? BasicData.Text("presentation.map.nodeLabelText2") : BasicData.Text("presentation.map.nodeLabelText3"));
        }
        return _name + (_visited ? BasicData.Text("presentation.map.nodeLabelText4") : BasicData.Text("presentation.map.nodeLabelText5"));
    }

    internal static string PuzzleMapInformation(PuzzleCityData _city, bool _visited, bool _current, bool _connected,
        PuzzleTravelResult _evaluation, PuzzleRegionData _region, IReadOnlyList<PuzzleInventoryService.TavernRumor> _rumors)
    {
        string heading = BasicData.Format("presentation.map.information", _city.displayName);
        heading += _visited
            ? BasicData.Format("presentation.map.informationText2", _region.displayName, _city.buyPercentOfBaseValue, _city.sellPercentOfFinalValue)
            : BasicData.Format("presentation.map.regionOnly", _region.displayName);
        string status;
        if (_current)
        {
            status = BasicData.Format("presentation.map.informationText3", ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent));
        }
        else if (!_connected)
        {
            status = BasicData.Format("presentation.map.informationText4", ColorUtility.ToHtmlStringRGB(PuzzleMapUnknown));
        }
        else if (_evaluation.Failure == PuzzleTravelFailure.InsufficientFunds)
        {
            status = BasicData.Format("presentation.map.informationText5", ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled), PuzzleTravelQuote(_evaluation));
        }
        else if (_evaluation.IsSuccess)
        {
            status = BasicData.Format("presentation.map.informationText6", ColorUtility.ToHtmlStringRGB(PuzzleMapConnected), PuzzleTravelQuote(_evaluation));
        }
        else
        {
            status = BasicData.Format("presentation.map.informationText7", ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled), PuzzleTravelStatus(_evaluation), PuzzleMapTime(_evaluation.TravelMinutes));
        }

        string trade;
        if (!_visited)
        {
            var rumors = _rumors.Where(_rumor => string.Equals(_rumor.TargetCityId, _city.id, StringComparison.Ordinal)).ToArray();
            trade = rumors.Length == 0
                ? BasicData.Text("presentation.map.informationText12")
                : BasicData.Format("presentation.map.rumorInformation", RumorLogEntries(rumors));
        }
        else
        {
            var rules = _city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>();
            string common = string.Join(" · ", rules
                .Where(rule => rule != null && rule.buyPercent < BasicData.Int("presentation.inventory.puzzleMapNodeLabelAbovePercent") && rule.sellPercent < BasicData.Int("presentation.inventory.puzzleMapNodeLabelAbovePercent2"))
                .OrderBy(rule => rule.tag, StringComparer.Ordinal).Select(rule => PuzzleTagLabel(rule.tag)));
            string rare = string.Join(" · ", rules
                .Where(rule => rule != null && rule.buyPercent > BasicData.Int("presentation.inventory.puzzleMapNodeLabelAbovePercent3") && rule.sellPercent > BasicData.Int("presentation.inventory.puzzleMapNodeLabelAbovePercent4"))
                .OrderBy(rule => rule.tag, StringComparer.Ordinal).Select(rule => PuzzleTagLabel(rule.tag)));
            string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
            string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent);
            trade = BasicData.Format("presentation.map.informationText8", (_region.tradeHub ? BasicData.Text("presentation.map.informationText13") : PuzzleTagLabel(_city.specialtyTag))) +
                BasicData.Format("presentation.map.informationText9", commonColor, (string.IsNullOrEmpty(common) ? BasicData.Text("presentation.map.informationText14") : common)) +
                BasicData.Format("presentation.map.informationText10", rareColor, (string.IsNullOrEmpty(rare) ? BasicData.Text("presentation.map.informationText15") : rare)) +
                FarmWastePurchaseLine("presentation.map.wastePurchase", _region.id);
        }

        return BasicData.Format("presentation.map.informationText11", heading, status, trade);
    }

    static string PuzzleTravelBreakdown(PuzzleTravelResult _result)
    {
        long cargoCost = _result.Cost - _result.BaseCost - _result.DistanceCost;
        return BasicData.Format("presentation.inventory.travelBreakdown", _result.BaseCost, _result.DistanceCost, cargoCost);
    }

    static string PuzzleTravelQuote(PuzzleTravelResult _result)
    {
        return BasicData.Format("presentation.inventory.travelQuote", _result.Distance, PuzzleMapTime(_result.TravelMinutes), PuzzleTravelCost, _result.Cost, PuzzleTravelBreakdown(_result));
    }

    public static string PuzzleMapRegionLabel(PuzzleRegionData _region)
    {
        return string.IsNullOrEmpty(_region.specialtyTag) ? _region.displayName :
            BasicData.Format("presentation.map.regionLabel", _region.displayName, PuzzleTagLabel(_region.specialtyTag));
    }

    public static string PuzzleMapStationName(string _name, bool _planningTarget, bool _uncertain = false)
    {
        string name = _planningTarget ? _name + " ★" : _name;
        return _uncertain ? BasicData.Format("presentation.map.rumorStation", name,
            ColorUtility.ToHtmlStringRGB(PuzzleMapTarget)) : name;
    }

    public static string PuzzleMapTime(long _minutes)
    {
        long hours = _minutes / PuzzleTimeManager.MINUTES_PER_HOUR;
        long minutes = _minutes % PuzzleTimeManager.MINUTES_PER_HOUR;
        return minutes == 0 ? BasicData.Format("presentation.map.time", hours) : hours == 0 ? BasicData.Format("presentation.map.timeText2", minutes) : BasicData.Format("presentation.map.timeText3", hours, minutes);
    }

    public static Color PuzzleMapRegionColor(string _regionId)
    {
        return _regionId switch
        {
            "region.mine" => BasicData.Color("presentation.map.region.mine"),
            "region.ranch" => BasicData.Color("presentation.map.region.ranch"),
            "region.industry" => BasicData.Color("presentation.map.region.industry"),
            "region.capital" => BasicData.Color("presentation.map.region.capital"),
            "region.coast" => BasicData.Color("presentation.map.region.coast"),
            "region.farm" => BasicData.Color("presentation.map.region.farm"),
            "region.common" => BasicData.Color("presentation.map.region.common"),
            _ => BasicData.Color("presentation.map.regionColorColor7")
        };
    }

    public static Color PuzzleMapInterchange => BasicData.Color("presentation.map.interchange");

    public static Color PuzzleMapLineColor(string _regionId)
    {
        if (_regionId == "line.regional")
        {
            return PuzzleMapInterchange;
        }
        if (_regionId == "region.common")
        {
            return BasicData.Color("presentation.map.neutralLine");
        }
        var color = PuzzleMapRegionColor(_regionId);
        color.a = 1f;
        return color;
    }

    internal static string PuzzleCityTitle(PuzzleCityData _city, PuzzleRegionData _region)
    {
        return BasicData.Format("presentation.trade.cityTitle", _city.displayName, _region.displayName);
    }

    public static Color PuzzleItemIcon(float _alpha)
    {
        return BasicData.Color("presentation.inventory.itemIcon", _alpha);
    }
    public static Color PuzzleSynergyArrow => BasicData.Color("presentation.inventory.synergyArrow");
    internal static Color PuzzleRefrigerationArrow => BasicData.Color("presentation.inventory.refrigerationArrow");
    public static string PuzzleTrainTitleText => BasicData.Text("presentation.train.trainTitle");
    internal static string PuzzleTrainTitle(int _wagonNumber)
    {
        return BasicData.Format("presentation.train.trainTitle2", _wagonNumber);
    }
    public static string PuzzleAddWagonLabel(long _cost)
    {
        return BasicData.Format("presentation.train.addWagonLabel", _cost);
    }
    internal static string PuzzleWagonRefrigerationInstalled => BasicData.Text("presentation.train.refrigerationInstalled");

    internal static string PuzzleWagonRefrigerationLabel(long _cost)
    {
        return BasicData.Format("presentation.train.refrigerationButton", _cost);
    }

    internal static string PuzzleWagonPurchaseStatus(int _wagonNumber, long _cost)
    {
        return BasicData.Format("presentation.train.wagonPurchaseStatus", _cost, _wagonNumber);
    }
    public static Color PuzzleAddWagonBox => BasicData.Color("presentation.train.addWagonBox");
    public static Color PuzzleAddWagonBoxDisabled => BasicData.Color("presentation.train.addWagonBoxDisabled");
    public static Color PuzzleAddWagonTextDisabled => BasicData.Color("presentation.train.addWagonTextDisabled");
    public static Color PuzzleWagonSelectedBorder => BasicData.Color("presentation.train.wagonSelectedBorder");
    public static Color PuzzleWagonDragHoverBorder => BasicData.Color("presentation.train.wagonDragHoverBorder");
    public static Color PuzzleWagonNormalBorder => BasicData.Color("presentation.train.wagonNormalBorder");
    public static string PuzzleMarketTitle => BasicData.Text("presentation.inventory.marketTitle");
    public static string PuzzleMoveGuide => BasicData.Text("presentation.inventory.moveGuide");
    public static string PuzzleTravelCost => BasicData.Text("presentation.inventory.travelCost");
    public static string PuzzleTagExpensive => BasicData.Text("presentation.inventory.tagExpensive");
    public static string PuzzleTagCommon => BasicData.Text("presentation.tags.puzzleTagCommon");

    static readonly Dictionary<string, string> PUZZLE_TAG_LABELS = new Dictionary<string, string>
    {
        { "Cargo", "presentation.tags.cargoLabel" },
        { "Wood", "presentation.tags.woodLabel" },
        { "Food", "presentation.tags.foodLabel" },
        { "Grain", "presentation.tags.grainLabel" },
        { "Supply", "presentation.tags.supplyLabel" },
        { "Water", "presentation.tags.waterLabel" },
        { "Machinery", "presentation.tags.machineryLabel" },
        { "Metal", "presentation.tags.metalLabel" },
        { "Ore", "presentation.tags.oreLabel" },
        { "Fish", "presentation.tags.fishLabel" },
        { "Salt", "presentation.tags.saltLabel" },
        { "Meat", "presentation.tags.meatLabel" },
        { "Animal", "presentation.tags.animalLabel" },
        { "Fruit", "presentation.tags.fruitLabel" },
        { "Waste", "presentation.tags.wasteLabel" },
        { "Box", "presentation.tags.boxLabel" },
        { "Fresh", "presentation.tags.refrigeratorLabel" },
        { "Refrigerator", "presentation.tags.refrigeratorLabel" },
        { "Quest", "presentation.tags.questLabel" },
        { PuzzleItemDefinition.BABY_TAG, "presentation.PuzzleTagCommon.baby" },

    };

    public static Color PuzzleBackground => BasicData.Color("presentation.inventory.background");
    public static Color PuzzleCell => BasicData.Color("presentation.inventory.cell");
    public static Color PuzzleHeading => BasicData.Color("presentation.inventory.heading");
    public static Color PuzzlePiece => BasicData.Color("presentation.inventory.piece");
    public static Color PuzzleGhost => BasicData.Color("presentation.inventory.ghost");
    public static Color PuzzleMoveTarget => BasicData.Color("presentation.inventory.moveTarget");

    public static Color PuzzleTooltipHeaderBackground => BasicData.Color("presentation.tooltip.headerBackground");
    public static Color PuzzleTooltipBodyBackground => BasicData.Color("presentation.tooltip.bodyBackground");
    public static Color PuzzleTooltipDivider => BasicData.Color("presentation.tooltip.divider");
    public static Color PuzzleTooltipText => BasicData.Color("presentation.tooltip.text");
    public static Color PuzzleTooltipGold => BasicData.Color("presentation.tooltip.gold");
    public static Color PuzzleTooltipGreen => BasicData.Color("presentation.tooltip.green");
    public static Color PuzzleTooltipRed => BasicData.Color("presentation.tooltip.red");

    static string TooltipLabelHtml()
    {
        return ColorUtility.ToHtmlStringRGB(PuzzleTooltipText);
    }

    public static string PuzzleTooltipBaseValueLabel => BasicData.Text("presentation.tooltip.baseValueLabel");
    internal static string PuzzleTooltipPriceUnavailable => BasicData.Text("presentation.tooltip.priceUnavailable");
    internal static string PuzzleMarketNoSettlement => BasicData.Text("presentation.tooltip.marketNoSettlement");

    internal static string PuzzleBoardActualValue(decimal _actualValue, long _baseValue)
    {
        string amount = $"{_actualValue:#,0.##}$".PadRight(7, '\u00A0');
        return BasicData.Format("presentation.inventory.boardActualValue", amount, _baseValue);
    }

    public static string PuzzleNoActiveEffects => BasicData.Text("presentation.tooltip.activeEffectsNone");
    public static string PuzzleTooltipActiveEffectsTitle => BasicData.Text("presentation.tooltip.activeEffectsTitle");

    static Color ComparisonColor(decimal _value, decimal _baseline, Color _neutralColor, bool _invert = false)
    {
        return ComparisonColor(_value, _baseline, _baseline, _neutralColor, _invert);
    }

    static Color ComparisonColor(decimal _value, decimal _lowThreshold, decimal _highThreshold, Color _neutralColor, bool _invert = false)
    {
        if (_value > _highThreshold)
        {
            return _invert ? PuzzleTooltipRed : PuzzleTooltipGreen;
        }
        if (_value < _lowThreshold)
        {
            return _invert ? PuzzleTooltipGreen : PuzzleTooltipRed;
        }
        return _neutralColor;
    }

    public static string PuzzleTooltipActiveEffectsHeader()
    {
        return BasicData.Text("presentation.tooltip.activeEffectsHeader");
    }

    internal static string PuzzleTooltipActiveEffectLabel(PuzzleSynergyBonus _bonus)
    {
        return BasicData.Format("presentation.tooltip.activeEffectLabel", TooltipLabelHtml(), SynergyNeighborName(_bonus));
    }

    // 여러 면이 닿은 이웃은 면 수를 붙인다.
    static string SynergyNeighborName(PuzzleSynergyBonus _bonus)
    {
        return _bonus.Faces > 1
            ? BasicData.Format("presentation.tooltip.activeEffectFaces", _bonus.NeighborName, _bonus.Faces)
            : _bonus.NeighborName;
    }

    internal static string PuzzleTooltipActiveEffectValue(PuzzleSynergyBonus _bonus)
    {
        return BasicData.Format("presentation.tooltip.activeEffectValue",
            ColorUtility.ToHtmlStringRGB(ComparisonColor(_bonus.Amount, 0m, PuzzleTooltipText)), _bonus.Amount);
    }

    internal static string PuzzleTooltipActiveEffectsSummary(IReadOnlyList<PuzzleSynergyBonus> _bonuses)
    {
        return string.Join("\n", _bonuses.Select(_bonus =>
            $"{PuzzleTooltipActiveEffectLabel(_bonus)} {PuzzleTooltipActiveEffectValue(_bonus)}"));
    }

    internal static string PuzzleTooltipActualValueLabel()
    {
        return BasicData.Format("presentation.tooltip.actualValueLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipActualValueValue(decimal _actualValue)
    {
        return BasicData.Format("presentation.tooltip.actualValueValue", ColorUtility.ToHtmlStringRGB(PuzzleTooltipGold), _actualValue);
    }

    static Color RateValueColor(int _percent, bool _isBuying)
    {
        return ComparisonColor(
            _percent,
            BasicData.Int("presentation.tooltip.rateValueColorBelowPercent"),
            BasicData.Int("presentation.tooltip.rateValueColorAbovePercent"),
            PuzzleTooltipGold,
            _isBuying);
    }

    static Color PriceValueColor(decimal _price, decimal _actualValue, bool _isBuying)
    {
        return ComparisonColor(_price, _actualValue, PuzzleTooltipGold, _isBuying);
    }

    internal static string PuzzleTooltipSellRateLabel()
    {
        return BasicData.Format("presentation.tooltip.sellRateLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipSellRateValue(int _percent)
    {
        return BasicData.Format("presentation.tooltip.sellRateValue", ColorUtility.ToHtmlStringRGB(RateValueColor(_percent, _isBuying: false)), _percent);
    }

    internal static string PuzzleTooltipBuyRateLabel()
    {
        return BasicData.Format("presentation.tooltip.buyRateLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipBuyRateValue(int _percent)
    {
        return BasicData.Format("presentation.tooltip.buyRateValue", ColorUtility.ToHtmlStringRGB(RateValueColor(_percent, _isBuying: true)), _percent);
    }

    internal static string PuzzleTooltipFinalSellPriceLabel()
    {
        return BasicData.Format("presentation.tooltip.finalSellPriceLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFinalSellPriceValue(decimal _price, decimal _actualValue)
    {
        return BasicData.Format("presentation.tooltip.finalSellPriceValue", ColorUtility.ToHtmlStringRGB(PriceValueColor(_price, _actualValue, _isBuying: false)), _price);
    }

    internal static string PuzzleTooltipFinalBuyPriceLabel()
    {
        return BasicData.Format("presentation.tooltip.finalBuyPriceLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFinalBuyPriceValue(decimal _price, decimal _actualValue)
    {
        return BasicData.Format("presentation.tooltip.finalBuyPriceValue", ColorUtility.ToHtmlStringRGB(PriceValueColor(_price, _actualValue, _isBuying: true)), _price);
    }

    public static string PuzzleTooltipGeneralEffectHeader()
    {
        return BasicData.Format("presentation.tooltip.generalEffectHeader", TooltipLabelHtml());
    }

    public static string PuzzleTooltipValueSynergyHeader()
    {
        return BasicData.Format("presentation.tooltip.effectHeader", TooltipLabelHtml());
    }

    internal static string PuzzleRefrigerationEffect(int _reductionPercent)
    {
        return PuzzleTooltipEffectDescription(PuzzlePreservationDescription(_reductionPercent, BasicData.Text));
    }

    public static string PuzzlePreservationDescription(int _reductionPercent, Func<string, string> _text)
    {
        return string.Format(_text("presentation.tooltip.refrigerationEffect"), _reductionPercent);
    }

    internal static string PuzzleAppliedPreservationEffect(int _reductionPercent)
    {
        string description = BasicData.Format("presentation.tooltip.appliedPreservationEffect", _reductionPercent);
        return BasicData.Format("presentation.tooltip.activeEffectLabel", TooltipLabelHtml(), HighlightEffectPercentages(description));
    }

    internal static string PuzzleAnimalBreedingEffect(string _description, long _minutes, string _offspringName, int _count)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            PuzzleMapTime(_minutes), _offspringName, _count));
    }

    internal static string PuzzleAnimalGrowthEffect(string _description, long _minutes, string _adultName, long _blockedMinutes)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            PuzzleMapTime(_minutes), _adultName, PuzzleMapTime(_blockedMinutes)));
    }

    internal static string PuzzleAnimalWasteEffect(string _description, int _intervalDays, string _wasteName, int _count)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            _intervalDays, _wasteName, _count));
    }

    internal static string PuzzleFreshnessDot => BasicData.Text("presentation.freshness.dot");

    internal static Color PuzzleFreshnessOutline => BasicData.Color("presentation.freshness.outline");

    internal static Color PuzzleFreshnessColor(int _percent)
    {
        return PuzzlePlacement.FreshnessBand(_percent) switch
        {
            PuzzleFreshnessBand.Low => BasicData.Color("presentation.freshness.low"),
            PuzzleFreshnessBand.Medium => BasicData.Color("presentation.freshness.medium"),
            PuzzleFreshnessBand.High => BasicData.Color("presentation.freshness.high"),
            _ => BasicData.Color("presentation.freshness.empty")
        };
    }

    internal static string PuzzleTooltipEffectDescription(string _description)
    {
        string highlighted = HighlightEffectPercentages(_description);
        highlighted = Regex.Replace(highlighted, @"\([^)]*\)", match => $"<size=70%>{match.Value}</size>");
        return BasicData.Format("presentation.tooltip.effectDescription", TooltipLabelHtml(), highlighted);
    }

    static string HighlightEffectPercentages(string _description)
    {
        return Regex.Replace(_description, @"[+−-]\d+(?:[.,]\d+)?%", match =>
        {
            Color color = match.Value[0] == '+' ? PuzzleTooltipGreen : PuzzleTooltipRed;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{match.Value}</b></color>";
        });
    }

    // 일반 효과 한 행. 진행도가 있으면 설명 바로 아래에 녹색으로 놓고, 공간을 막혀 있으면 같은 줄에 빨간 표시를 덧붙인다.
    internal static string PuzzleTooltipGeneralEffect(PuzzleEffectDisplay _effect)
    {
        if (_effect.Progress is not PuzzleProgressDisplay progress)
        {
            return _effect.Description;
        }

        string line = BasicData.Format("presentation.tooltip.effectProgress",
            ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), PuzzleMapTime(progress.ElapsedMinutes),
            PuzzleMapTime(progress.TotalMinutes));
        if (progress.SpaceBlocked)
        {
            line += " " + BasicData.Format("presentation.tooltip.effectSpaceBlocked",
                ColorUtility.ToHtmlStringRGB(PuzzleTooltipRed));
        }
        return _effect.Description + "\n" + line;
    }

    internal static string PuzzleTooltipNoEffect()
    {
        return BasicData.Format("presentation.tooltip.noEffect", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFutureValueLabel(long _minutes, string _cityName, long _activations)
    {
        return BasicData.Format("presentation.tooltip.futureValueLabel", TooltipLabelHtml(), _cityName, PuzzleMapTime(_minutes), _activations);
    }

    internal static string PuzzleTooltipFutureValueValue(decimal _futureValue, decimal _actualValue)
    {
        Color color = ComparisonColor(_futureValue, _actualValue, PuzzleTooltipGold);
        return BasicData.Format("presentation.tooltip.futureValueValue", ColorUtility.ToHtmlStringRGB(color), _futureValue);
    }

    internal static string PuzzleTooltipFreshnessLabel()
    {
        return BasicData.Format("presentation.tooltip.freshnessLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFreshnessValue(int _current)
    {
        return BasicData.Format("presentation.tooltip.freshnessValue",
            ColorUtility.ToHtmlStringRGB(PuzzleFreshnessColor(_current)), PuzzleFreshnessDot);
    }

    internal static string PuzzleTooltipShelfLifeLabel()
    {
        return BasicData.Format("presentation.tooltip.shelfLifeLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipShelfLifeValue(long _minutes, int _freshness)
    {
        string color = ColorUtility.ToHtmlStringRGB(PuzzleFreshnessColor(_freshness));
        if (_minutes == long.MaxValue)
        {
            return BasicData.Format("presentation.tooltip.shelfLifePaused", color);
        }
        long hours = _minutes / PuzzleTimeManager.MINUTES_PER_HOUR;
        if (_minutes % PuzzleTimeManager.MINUTES_PER_HOUR > 0)
        {
            hours++;
        }
        return BasicData.Format("presentation.tooltip.shelfLifeValue", color,
            hours / PuzzleTimeManager.HOURS_PER_DAY, hours % PuzzleTimeManager.HOURS_PER_DAY);
    }

    internal static string PuzzleValueText(PuzzleValue _value)
    {
        return BasicData.Format("presentation.inventory.valueText", _value.FinalValue, _value.BaseValue);
    }

    internal static string PuzzleItemDetails(PuzzleItemDefinition _item, PuzzleValue _value, decimal _actualValue,
        PuzzleTradeRate _rate)
    {
        if (_item == null)
        {
            return string.Empty;
        }

        string details =
            BasicData.Format("presentation.inventory.itemDetails", _item.DisplayName, PuzzleValueText(_value), _actualValue, _item.Description);
        if (_rate.BuyPercent > 0 || _rate.SellPercent > 0)
        {
            details += "\n" + PuzzleTradeRateText(_rate);
        }
        if (_value.SynergyBonuses.Count == 0)
        {
            return BasicData.Format("presentation.inventory.itemDetailsText2", details);
        }

        var bonuses = PuzzleSynergyBonus.GroupByNeighbor(_value.SynergyBonuses).Select(bonus =>
            BasicData.Format("presentation.inventory.itemDetailsText3", SynergyNeighborName(bonus), bonus.Percent, bonus.Amount));
        return BasicData.Format("presentation.inventory.itemDetailsText4", details, string.Join("  ·  ", bonuses));
    }

    internal static string PuzzleTradeRateText(PuzzleTradeRate _rate)
    {
        string rule = string.IsNullOrEmpty(_rate.Tag) ? BasicData.Text("presentation.trade.tradeRateTextText2")
            : string.Join(" + ", _rate.Tag.Split(PuzzleTradeRate.TAG_SEPARATOR).Select(PuzzleTagLabel));
        return BasicData.Format("presentation.trade.tradeRateText", rule, _rate.BuyPercent, _rate.SellPercent);
    }

    internal static string PuzzleFunds(long _funds)
    {
        return BasicData.Format("presentation.inventory.funds", _funds);
    }

    // 도시의 태그 규칙을 구매·판매 비율 기준으로 귀함/흔함으로 묶는다.
    // 두 비율이 100을 사이에 두고 갈리거나 한쪽이 100이면 어느 쪽도 아닌 것으로 보고 표시하지 않는다.
    internal static string PuzzleCityTagPreferences(PuzzleCityData _city)
    {
        var expensive = new List<string>();
        var common = new List<string>();
        foreach (var rule in _city?.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>())
        {
            if (rule == null)
            {
                continue;
            }
            if (rule.buyPercent > BasicData.Int("presentation.trade.puzzleCityTagPreferencesAbovePercent") && rule.sellPercent > BasicData.Int("presentation.trade.puzzleCityTagPreferencesAbovePercent2"))
            {
                expensive.Add(rule.tag);
            }
            else if (rule.buyPercent < BasicData.Int("presentation.inventory.puzzleCityTagPreferencesBelowPercent") && rule.sellPercent < BasicData.Int("presentation.inventory.puzzleCityTagPreferencesBelowPercent2"))
            {
                common.Add(rule.tag);
            }
        }

        string commonText = string.Join(" · ", common.OrderBy(tag => tag, StringComparer.Ordinal).Select(PuzzleTagLabel));
        string rareText = string.Join(" · ", expensive.OrderBy(tag => tag, StringComparer.Ordinal).Select(PuzzleTagLabel));
        string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
        string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent);
        return BasicData.Format("presentation.trade.cityTagPreferences", commonColor, (string.IsNullOrEmpty(commonText) ? BasicData.Text("presentation.trade.cityTagPreferencesText3") : commonText)) +
            BasicData.Format("presentation.trade.cityTagPreferencesText2", rareColor, (string.IsNullOrEmpty(rareText) ? BasicData.Text("presentation.trade.cityTagPreferencesText4") : rareText)) +
            BasicData.Format("presentation.trade.regionPrice", _city.buyPercentOfBaseValue, _city.sellPercentOfFinalValue) +
            FarmWastePurchaseLine("presentation.trade.wastePurchase", _city?.regionId);

    }

    static string FarmWastePurchaseLine(string _textKey, string _regionId)
    {
        if (!string.Equals(_regionId, "region.farm", StringComparison.Ordinal))
        {
            return string.Empty;
        }
        return BasicData.Format(_textKey, ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), PuzzleTagLabel("Waste"));
    }

    public static string PuzzleTagLabel(string _tag)
    {
        return PuzzleTagLabel(_tag, BasicData.Text, TableManager.Instance.Puzzle.tagPresentations);
    }

    public static string PuzzleTagLabel(string _tag, Func<string, string> _text,
        IReadOnlyList<PuzzleTagPresentationData> _customTags = null)
    {
        if (string.IsNullOrEmpty(_tag))
        {
            return string.Empty;
        }
        var custom = _customTags?.FirstOrDefault(_entry => _entry != null && _entry.id == _tag);
        return custom != null ? custom.displayName :
            PUZZLE_TAG_LABELS.TryGetValue(_tag, out string label) ? _text(label) : _tag;
    }

    public static bool HasBuiltInPuzzleTag(string _tag)
    {
        return PUZZLE_TAG_LABELS.Keys.Any(_key => string.Equals(_key, _tag, StringComparison.OrdinalIgnoreCase));
    }

    #region 시너지 상성표

    public static string SynergyChartTitle => BasicData.Text("presentation.synergyChart.title");
    public static string SynergyChartToggle => BasicData.Text("presentation.synergyChart.toggle");
    public static string SynergyChartClose => BasicData.Text("presentation.synergyChart.close");
    public static string SynergyChartCorner => BasicData.Text("presentation.synergyChart.corner");
    public static string SynergyChartItemTitle => BasicData.Text("presentation.synergyChart.itemTitle");
    public static Color SynergyChartBackdrop => BasicData.Color("presentation.synergyChart.backdrop");

    // 대상 태그 행 이름. 제외 방식 행은 "모든 화물(… 제외)"로 표기한다.
    public static string SynergyChartRowLabel(string _row)
    {
        if (!PuzzleSynergyChart.IsExclusionRow(_row))
        {
            return PuzzleTagLabel(_row);
        }
        return BasicData.Format("presentation.synergyChart.allTargets",
            string.Join("·", PuzzleSynergyChart.ExcludedTags(_row).Select(PuzzleTagLabel)));
    }

    public static Color SynergyChartTagBackground(string _row)
    {
        return PuzzleTagBackgroundColor(PuzzleSynergyChart.IsExclusionRow(_row) ? null : _row);
    }

    public static string SynergyChartCell(int _percent, int _footnote)
    {
        if (_percent == 0)
        {
            return string.Empty;
        }
        string mark = _footnote > 0 ? BasicData.Format("presentation.synergyChart.footnoteMark", _footnote) : string.Empty;
        return BasicData.Format("presentation.synergyChart.cell", _percent, mark);
    }

    public static Color SynergyChartCellBackground(int _percent)
    {
        return BasicData.Color(_percent > 0 ? "presentation.synergyChart.positive"
            : _percent < 0 ? "presentation.synergyChart.negative" : "presentation.synergyChart.empty");
    }

    // 계산 규칙, 품목별 신선 감소율, 제외 이웃 태그 각주를 모은다.
    public static string SynergyChartNotes(IReadOnlyList<string> _footnotes, IReadOnlyList<PuzzlePieceData> _pieces, int _intervalHours)
    {
        var lines = new List<string>
        {
            BasicData.Format("presentation.synergyChart.rules", _intervalHours),
            BasicData.Format("presentation.synergyChart.refrigeration", string.Join(" · ", _pieces
                .Where(_piece => _piece.preservationEffect.enabled)
                .OrderBy(_piece => _piece.id, StringComparer.Ordinal)
                .Select(_piece => _piece.displayName + " " + _piece.preservationEffect.reductionPercent + "%")))
        };
        for (int i = 0; i < _footnotes.Count; i++)
        {
            string tags = string.Join("·", _footnotes[i].Split(',').Select(_tag => PuzzleTagLabel(_tag.Trim())));
            lines.Add(BasicData.Format("presentation.synergyChart.footnote", i + 1, tags));
        }
        return string.Join("\n", lines);
    }

    // 화물 이름과 태그 배지(태그 색 강조).
    public static string SynergyChartItemLine(string _name, IReadOnlyList<string> _tags)
    {
        var badges = (_tags ?? Array.Empty<string>()).Select(_tag => BasicData.Format("presentation.synergyChart.itemTag",
            ColorUtility.ToHtmlStringRGB(PuzzleTagBackgroundColor(_tag)), PuzzleTagLabel(_tag)));
        return BasicData.Format("presentation.synergyChart.itemLine", _name, string.Join(" ", badges));
    }

    #endregion

    public static Color PuzzleTagBackgroundColor(string _tag)
    {
        var custom = TableManager.Instance.Puzzle.tagPresentations.FirstOrDefault(_entry => _entry.id == _tag);
        if (custom != null)
        {
            return new Color(custom.r, custom.g, custom.b, custom.a);
        }
        return (_tag ?? string.Empty).ToLowerInvariant() switch
        {
            "wood" => BasicData.Color("presentation.tags.woodBackground"),       // 목재: 팔레트같은 따뜻한 브라운
            "metal" => BasicData.Color("presentation.tags.metalBackground"),     // 금속: 메탈릭 스틸 슬레이트
            "machinery" => BasicData.Color("presentation.tags.machineryBackground"),   // 기계: 산업용 앰버/브론즈
            "food" => BasicData.Color("presentation.tags.foodBackground"),       // 식품/음식: 수확 웜 오렌지
            "fish" => BasicData.Color("presentation.tags.fishBackground"),       // 물고기/수산물: 딥 오션 블루
            "water" => BasicData.Color("presentation.tags.waterBackground"),     // 물/식수: 맑은 아쿠아 블루
            "salt" => BasicData.Color("presentation.tags.saltBackground"),     // 소금: 미네랄 실버 슬레이트
            "box" => BasicData.Color("presentation.tags.boxBackground"),
            "supply" => BasicData.Color("presentation.tags.supplyBackground"),     // 보급품: 포레스트 그린
            "fresh" or "refrigerator" => BasicData.Color("presentation.tags.freshBackground"),
            "cargo" => BasicData.Color("presentation.tags.cargoBackground"),      // 화물/상자: 가죽/카톤 브라운
            "meat" => BasicData.Color("presentation.tags.meatBackground"),       // 고기: 딥 레드
            "animal" => BasicData.Color("presentation.tags.animalBackground"),    // 가축: 탄/레더 탠
            "fruit" => BasicData.Color("presentation.tags.fruitBackground"),      // 과일: 사과 레드
            "quest" => BasicData.Color("presentation.tags.questBackground"),
            "waste" => BasicData.Color("presentation.tags.wasteBackground"),         // 폐기물: 다크 브라운
            _ => BasicData.Color("presentation.tags.puzzleTagBackgroundColorColor")
        };
    }

    public static Color PuzzleTagTextColor(string _tag)
    {
        if (TableManager.Instance.Puzzle.tagPresentations.Any(_entry => _entry.id == _tag))
        {
            return PuzzleTagTextOnBackground(PuzzleTagBackgroundColor(_tag));
        }
        return IsFreshTag(_tag) ? BasicData.Color("presentation.tags.freshTextColor")
            : BasicData.Color("presentation.tags.puzzleTagTextColor");
    }

    static Color PuzzleTagTextOnBackground(Color _background)
    {
        Color light = BasicData.Color("presentation.tags.puzzleTagTextColor");
        Color dark = BasicData.Color("presentation.tags.freshTextColor");
        return PuzzleTagContrast(_background, light) >= PuzzleTagContrast(_background, dark) ? light : dark;
    }

    static float PuzzleTagContrast(Color _background, Color _text)
    {
        Color background = _background.linear;
        Color text = _text.linear;
        float backgroundLuminance = background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f;
        float textLuminance = text.r * 0.2126f + text.g * 0.7152f + text.b * 0.0722f;
        return (Mathf.Max(backgroundLuminance, textLuminance) + 0.05f) /
            (Mathf.Min(backgroundLuminance, textLuminance) + 0.05f);
    }

    static bool IsFreshTag(string _tag)
    {
        return string.Equals(_tag, "Fresh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_tag, "Refrigerator", StringComparison.OrdinalIgnoreCase);
    }

    internal static Color PuzzleTooltipTagBackground(string _tag, float _blend)
    {
        Color background = PuzzleTagBackgroundColor(_tag);
        return IsFreshTag(_tag) ? background
            : UnityEngine.Color.Lerp(background, PuzzleTooltipBodyBackground, _blend);
    }

    internal static Color PuzzleTooltipTagTextColor(string _tag, float _blend)
    {
        if (TableManager.Instance.Puzzle.tagPresentations.Any(_entry => _entry.id == _tag))
        {
            return PuzzleTagTextOnBackground(PuzzleTooltipTagBackground(_tag, _blend));
        }
        return IsFreshTag(_tag) ? PuzzleTagTextColor(_tag) : PuzzleTooltipText;
    }

    public static IReadOnlyList<string> PrioritizeTags(IEnumerable<string> _tags)
    {
        if (_tags == null)
        {
            return Array.Empty<string>();
        }

        return _tags.OrderBy(tag => GetTagSortWeight(tag)).ThenBy(tag => tag, StringComparer.Ordinal).ToArray();
    }

    static int GetTagSortWeight(string _tag)
    {
        return (_tag ?? string.Empty).ToLowerInvariant() switch
        {
            "wood" => BasicData.Int("presentation.tags.woodPriority"),
            "machinery" => BasicData.Int("presentation.tags.machineryPriority"),
            "metal" => BasicData.Int("presentation.tags.metalPriority"),
            "fish" => BasicData.Int("presentation.tags.fishPriority"),
            "food" => BasicData.Int("presentation.tags.foodPriority"),
            "meat" => BasicData.Int("presentation.tags.meatPriority"),
            "fruit" => BasicData.Int("presentation.tags.fruitPriority"),
            "animal" => BasicData.Int("presentation.tags.animalPriority"),
            "water" => BasicData.Int("presentation.tags.waterPriority"),
            "salt" => BasicData.Int("presentation.tags.saltPriority"),
            "box" => BasicData.Int("presentation.tags.boxPriority"),
            "supply" => BasicData.Int("presentation.tags.supplyPriority"),
            "cargo" => BasicData.Int("presentation.tags.cargoPriority"),
            "waste" => BasicData.Int("presentation.tags.wastePriority"),
            _ => BasicData.Int("presentation.tags.defaultPriority")
        };
    }

    // 시간 진행이 계산 범위를 넘어 진행 자체가 실패한 경우의 안내.
    internal static string PuzzleTimeOverflow => BasicData.Text("presentation.inventory.travelStatusText6");
    internal static string PuzzleBabyCannotButcher => BasicData.Text("presentation.PuzzleBabyCannotButcher");

    internal static string PuzzleLossSummary(long _funds)
    {
        return BasicData.Format("presentation.inventory.lossSummary", _funds);
    }

    internal static string PuzzleClearTitle => BasicData.Text("presentation.inventory.clearTitle");

    public static string PuzzleClearButton(long _cost)
    {
        return BasicData.Format("presentation.inventory.clearButton", _cost);
    }

    internal static string PuzzleClearSummary(long _funds)
    {
        return BasicData.Format("presentation.inventory.clearSummary", _funds);
    }

    internal static string PuzzleRunStatusText(PuzzleRunStatus _status)
    {
        return _status switch
        {
            PuzzleRunStatus.Won => PuzzleClearTitle,
            PuzzleRunStatus.Lost => BasicData.Text("presentation.inventory.runStatusText"),
            _ => string.Empty
        };
    }

    // 이동이 차단된 이유. 비용 부족은 패배가 아니라 상태 유지고, 금액은 노선 기본값과 기차 보유 칸으로 갈린다.
    internal static string PuzzleTravelStatus(PuzzleTravelResult _result)
    {
        return _result.Failure switch
        {
            PuzzleTravelFailure.SameCity => BasicData.Text("presentation.inventory.travelStatusText3"),
            PuzzleTravelFailure.InvalidCity => BasicData.Text("presentation.inventory.travelStatusText4"),
            PuzzleTravelFailure.InsufficientFunds => BasicData.Format("presentation.inventory.travelStatus", PuzzleTravelCost, _result.Cost)
                + BasicData.Format("presentation.inventory.travelStatusText2", PuzzleTravelBreakdown(_result)),
            PuzzleTravelFailure.CostOverflow => BasicData.Text("presentation.inventory.travelStatusText5"),
            PuzzleTravelFailure.TimeOverflow => PuzzleTimeOverflow,
            PuzzleTravelFailure.MissingRoute => BasicData.Text("presentation.inventory.travelStatusText7"),
            PuzzleTravelFailure.NotAvailable => BasicData.Text("presentation.inventory.travelStatusText8"),
            PuzzleTravelFailure.Blocked => BasicData.Text("presentation.inventory.travelStatusText9"),
            _ => string.Empty
        };
    }

    internal static string PuzzleTradeStatus(PuzzleDropResult _result, PuzzleItemDefinition _item, bool _committed)
    {
        if (_result.TradeFailure == PuzzleTradeFailure.MarketClosed)
        {
            return PuzzleNightMarket;
        }
        if (_result.Placement.Failure == PuzzlePlacementFailure.BabyCannotButcher)
        {
            return PuzzleBabyCannotButcher;
        }
        if (_result.Direction == PuzzleTradeDirection.None)
        {
            return string.Empty;
        }

        string direction = _result.Direction == PuzzleTradeDirection.Buy ? BasicData.Text("presentation.trade.tradeStatusText6") : BasicData.Text("presentation.trade.tradeStatusText7");
        string quote = BasicData.Format("presentation.trade.tradeStatus", _item.DisplayName, direction);
        if (_result.Placement.Failure != PuzzlePlacementFailure.StaleSource &&
            _result.TradeFailure != PuzzleTradeFailure.PriceOverflow)
        {
            quote += BasicData.Format("presentation.trade.tradeStatusText2", _result.Price);
        }
        if (_result.CanDrop)
        {
            return _committed
                ? BasicData.Format("presentation.trade.tradeStatusText3", _item.DisplayName, direction, _result.Price)
                : BasicData.Format("presentation.trade.tradeStatusText4", _item.DisplayName, direction, _result.Price);
        }

        string reason = _result.TradeFailure switch
        {
            PuzzleTradeFailure.Disabled => BasicData.Text("presentation.trade.tradeStatusText8"),
            PuzzleTradeFailure.MarketClosed => PuzzleNightMarket,
            PuzzleTradeFailure.InsufficientFunds => BasicData.Text("presentation.trade.tradeStatusText9"),
            PuzzleTradeFailure.PriceOverflow => BasicData.Text("presentation.trade.tradeStatusText10"),
            PuzzleTradeFailure.BalanceOverflow => BasicData.Text("presentation.trade.tradeStatusText11"),
            _ => _result.Placement.Failure switch
            {
                PuzzlePlacementFailure.StaleSource => BasicData.Text("presentation.trade.tradeStatusText12"),
                PuzzlePlacementFailure.CellOutsideBoard => BasicData.Text("presentation.trade.tradeStatusText13"),
                PuzzlePlacementFailure.CellOccupied => BasicData.Text("presentation.trade.tradeStatusText14"),
                PuzzlePlacementFailure.NoSpace => BasicData.Text("presentation.trade.tradeStatusText15"),
                _ => BasicData.Text("presentation.trade.tradeStatusText16")
            }
        };
        return BasicData.Format("presentation.trade.tradeStatusText5", quote, reason);
    }

    public static Color PuzzlePieceOutline(float _alpha)
    {
        return BasicData.Color("presentation.inventory.pieceOutline", _alpha);
    }

    public static Color PuzzleOutline(bool _canPlace)
    {
        return _canPlace ? BasicData.Color("presentation.inventory.outline") : BasicData.Color("presentation.inventory.outlineColor");
    }

    // 범주 팔레트. 새 범주를 추가하면 여기 색과 Title을 함께 추가한다.
    public static Color Color(EffectCategory _category)
    {
        return _category switch
        {
            EffectCategory.Offense => BasicData.Color("presentation.inventory.color"),
            EffectCategory.Mobility => BasicData.Color("presentation.inventory.colorColor"),
            EffectCategory.Defense => BasicData.Color("presentation.inventory.colorColor2"),
            EffectCategory.BaseStat => BasicData.Color("presentation.inventory.colorColor3"),
            _ => BasicData.Color("presentation.inventory.colorColor4"),
        };
    }

    public static string Title(EffectCategory _category)
    {
        return _category switch
        {
            EffectCategory.Offense => BasicData.Text("presentation.inventory.title"),
            EffectCategory.Mobility => BasicData.Text("presentation.inventory.titleText2"),
            EffectCategory.Defense => BasicData.Text("presentation.inventory.titleText3"),
            EffectCategory.BaseStat => BasicData.Text("presentation.inventory.titleText4"),
            _ => BasicData.Text("presentation.inventory.titleText5"),
        };
    }

    public static EffectCategory Category(UpgradeData _upgrade)
    {
        return GameTableRules.Category(_upgrade == null ? null : _upgrade.category);
    }
    public static Color Color(UpgradeData _upgrade)
    {
        return Color(Category(_upgrade));
    }
    public static string Title(UpgradeData _upgrade)
    {
        return Title(Category(_upgrade));
    }

    public static string Name(UpgradeData _upgrade)
    {
        return _upgrade == null ? BasicData.Text("presentation.inventory.name") : string.IsNullOrEmpty(_upgrade.displayName) ? _upgrade.id : _upgrade.displayName;
    }

    // 설명은 테이블 원본. 비어 있으면 GameTableRules가 테이블 오류로 보고하므로 여기서 폴백 문구를 두지 않는다.
    public static string Describe(UpgradeData _upgrade)
    {
        return _upgrade == null ? string.Empty : _upgrade.description ?? string.Empty;
    }

    // 같은 색에서 채도·명도만 낮춰 '기존 보유/약화' 표현을 만든다(팔레트를 새로 만들지 않는다).
    public static Color Shade(Color _color, float _saturation, float _value)
    {
        UnityEngine.Color.RGBToHSV(_color, out float hue, out float sat, out float val);
        return UnityEngine.Color.HSVToRGB(hue, sat * _saturation, val * _value);
    }

    // ── 리치 텍스트 표기 ──
    // 값 강조·Dim·제목 서식을 카드와 패널이 같이 쓴다. 서식 문자열을 개별 UI에 복사하지 않는다.
    static string ValueTag => BasicData.Text("presentation.inventory.valueTag");
    static string DimTag => BasicData.Text("presentation.inventory.dimTag");
    public static string Accent(string _value)
    {
        return BasicData.Format("presentation.inventory.accent", ValueTag, _value);
    }
    public static string Dim(string _value)
    {
        return BasicData.Format("presentation.inventory.dim", DimTag, _value);
    }
    public static string Heading(string _title, Color _color)
    {
        return BasicData.Format("presentation.inventory.heading", Hex(_color), _title);
    }
    public static string Hex(Color _color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(_color);
    }

    // 테이블 항목 하나의 카드 본문(이름·범주·설명). 카드와 목록이 같은 출력을 쓴다.
    public static string Card(UpgradeData _upgrade)
    {
        if (_upgrade == null)
        {
            return Dim(BasicData.Text("presentation.inventory.cardText2"));
        }
        return BasicData.Format("presentation.inventory.card", Heading(Name(_upgrade), Color(_upgrade)), Dim(Title(_upgrade)), Describe(_upgrade));
    }
}
