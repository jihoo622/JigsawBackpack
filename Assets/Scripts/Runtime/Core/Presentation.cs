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

internal enum PuzzleCityTradeGrade
{
    Common,
    Expensive,
    Rare,
    Waste
}

internal readonly struct PuzzleCityTradePreferencesData
{
    public readonly bool IsValid;
    public readonly IReadOnlyList<string> CommonTags;
    public readonly IReadOnlyList<string> ExpensiveTags;
    public readonly IReadOnlyList<string> RareTags;
    public readonly IReadOnlyList<string> WasteTags;
    public readonly bool HasWastePurchase;
    public readonly int BuyPercentOfBaseValue;
    public readonly int SellPercentOfFinalValue;

    public PuzzleCityTradePreferencesData(
        IReadOnlyList<string> _commonTags,
        IReadOnlyList<string> _expensiveTags,
        IReadOnlyList<string> _rareTags,
        IReadOnlyList<string> _wasteTags,
        bool _hasWastePurchase,
        int _buyPercentOfBaseValue,
        int _sellPercentOfFinalValue)
    {
        IsValid = true;
        CommonTags = _commonTags ?? Array.Empty<string>();
        ExpensiveTags = _expensiveTags ?? Array.Empty<string>();
        RareTags = _rareTags ?? Array.Empty<string>();
        WasteTags = _wasteTags ?? Array.Empty<string>();
        HasWastePurchase = _hasWastePurchase;
        BuyPercentOfBaseValue = _buyPercentOfBaseValue;
        SellPercentOfFinalValue = _sellPercentOfFinalValue;
    }
}

public static partial class Presentation
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

    internal static string TavernRumor(PuzzleRumorCandidate _candidate)
    {
        var tables = TableManager.Instance;
        string city = CityLink(_candidate.CityId);
        return _candidate.Kind switch
        {
            PuzzleRumorKind.Specialty => BasicData.Format("presentation.station.specialtyRumor", city,
                ItemLink(_candidate.ItemId, tables.Piece(_candidate.ItemId).displayName)),
            PuzzleRumorKind.RareDemand or PuzzleRumorKind.Preference => TavernDemandRumor(_candidate, city),
            PuzzleRumorKind.Action => TavernActionRumor(city, tables.WorkshopById(_candidate.ActionId)),
            _ => throw new ArgumentOutOfRangeException(nameof(_candidate), _candidate.Kind, "지원하지 않는 소문 종류입니다.")
        };
    }

    static string TavernDemandRumor(PuzzleRumorCandidate _candidate, string _city)
    {
        var tables = TableManager.Instance;
        var firstPreference = tables.PuzzleRumor.firstPreference;
        bool isPreference = _candidate.Kind == PuzzleRumorKind.Preference && firstPreference != null &&
            string.Equals(_candidate.CityId, firstPreference.cityId, StringComparison.Ordinal) &&
            string.Equals(_candidate.Tag, firstPreference.tag, StringComparison.Ordinal) &&
            tables.TryGetCityTagPrice(_candidate.CityId, _candidate.Tag, out var rule) &&
            TableManager.SellGradeKindOf(rule) == PuzzleSellGradeKind.Expensive;
        if (isPreference)
        {
            return BasicData.Format("presentation.station.preferenceRumor", _city, TagLink(_candidate.Tag),
                HasFinalConsonant(PuzzleTagLabel(_candidate.Tag)) ? "을" : "를");
        }
        return BasicData.Format("presentation.station.rareRumor", _city, TagLink(_candidate.Tag));
    }

    // 저장된 수요 소문도 종류·마을·태그로 문구를 만들어 현재 표시 원본을 사용한다.
    internal static string TavernRumorMessage(PuzzleInventoryService.TavernRumor _rumor)
    {
        if (_rumor.Kind == PuzzleRumorKind.RareDemand || _rumor.Kind == PuzzleRumorKind.Preference)
        {
            return TavernRumor(new PuzzleRumorCandidate(_rumor.Kind, _rumor.TargetCityId, _rumor.ItemId,
                _rumor.Tag, _rumor.ActionId));
        }
        return _rumor.Message;
    }

    static string TavernActionRumor(string _city, TrainventoryWorkshopData _cityAction)
    {
        string action = _cityAction.type == nameof(TrainventoryWorkshopType.Refining)
            ? BasicData.Text("presentation.station.refiningRumorAction")
            : TavernActionName(_cityAction.title.TrimEnd().Normalize());
        return BasicData.Format("presentation.station.actionRumor", _city, action, HasFinalConsonant(action) ? "이" : "가");
    }

    static bool HasFinalConsonant(string _word)
    {
        const int HANGUL_FIRST_SYLLABLE = 0xAC00;
        const int HANGUL_LAST_SYLLABLE = 0xD7A3;
        const int FINAL_CONSONANT_COUNT = 28;
        if (string.IsNullOrEmpty(_word))
        {
            return false;
        }

        char last = _word[^1];
        return last >= HANGUL_FIRST_SYLLABLE && last <= HANGUL_LAST_SYLLABLE &&
            (last - HANGUL_FIRST_SYLLABLE) % FINAL_CONSONANT_COUNT != 0;
    }

    static string TavernActionName(string _title)
    {
        const string BUTTON_SUFFIX = "하기";
        if (_title.Length > BUTTON_SUFFIX.Length && _title.EndsWith(BUTTON_SUFFIX, StringComparison.Ordinal))
        {
            return _title.Substring(0, _title.Length - BUTTON_SUFFIX.Length);
        }

        return _title;
    }

    public static string RumorLogTitle => BasicData.Text("presentation.rumorLog.title");
    public static string RumorLogCollapse => BasicData.Text("presentation.rumorLog.collapse");
    public static string RumorLogEmpty => BasicData.Text("presentation.rumorLog.empty");

    public static string RumorLogCount(int _count)
    {
        return BasicData.Format("presentation.rumorLog.count", _count);
    }

    // 하단 '정보' 버튼은 가로 이름, 오른쪽 가장자리 손잡이는 세로로 쓴 이름을 쓴다.
    // 수첩을 마지막으로 연 뒤 생긴 변화 수는 둘 다 작은 배지로 붙인다.
    public static string RumorLogCollapsed => BasicData.Text("presentation.rumorLog.collapsed");
    public static string RumorLogHandle => BasicData.Text("presentation.rumorLog.handle");
    public static string RumorLogSynergyTab => BasicData.Text("presentation.rumorLog.synergyTab");

    public static string RumorLogBadge(int _count)
    {
        return BasicData.Format("presentation.rumorLog.badge", _count);
    }

    public static Color RumorLogBadgeColor => BasicData.Color("presentation.rumorLog.badge");
    public static Color RumorLogBadgeText => BasicData.Color("presentation.rumorLog.badgeText");

    public static string RumorLogRumorTab => BasicData.Text("presentation.rumorLog.rumorTab");
    public static string RumorLogQuestTab => BasicData.Text("presentation.rumorLog.questTab");

    // 수첩 탭 배경·글자색. 선택한 탭은 밝은 배경과 화물칸 선택색 밑줄로 구분한다.
    public static Color RumorLogTabNormal => BasicData.Color("presentation.rumorLog.tabNormal");
    public static Color RumorLogTabNormalText => BasicData.Color("presentation.rumorLog.tabNormalText");
    public static Color RumorLogTabSelected => BasicData.Color("presentation.rumorLog.tabSelected");
    public static Color RumorLogTabSelectedText => BasicData.Color("presentation.rumorLog.tabSelectedText");

    // 누적 경과 시간을 게임 날짜(1부터)·시각으로 표기한다. 소문·의뢰 기록이 같은 표기를 쓴다.
    // 경과 시간을 "D일 오후"처럼 날짜와 턴 시간대로 표기한다.
    internal static string GameTimestamp(long _elapsedHours)
    {
        int hour = (int)((TableManager.Instance.PuzzleTime.startHour + _elapsedHours % PuzzleTimeManager.HOURS_PER_DAY)
            % PuzzleTimeManager.HOURS_PER_DAY);
        return BasicData.Format("presentation.time.timestamp", _elapsedHours / PuzzleTimeManager.HOURS_PER_DAY + 1,
            TurnSlotName(hour / PuzzleTimeManager.HOURS_PER_TURN));
    }

    // 턴 시간대 이름(0 새벽, 1 오전, 2 오후, 3 저녁).
    internal static string TurnSlotName(int _slot)
    {
        int slot = (_slot % PuzzleTimeManager.TURNS_PER_DAY + PuzzleTimeManager.TURNS_PER_DAY) % PuzzleTimeManager.TURNS_PER_DAY;
        return BasicData.Text("presentation.time.slot" + slot);
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
            string timestamp = GameTimestamp(rumor.RecordedHours);
            entries.Add(BasicData.Format("presentation.rumorLog.entry", timestamp,
                CityLink(rumor.SourceCityId), TavernRumorMessage(rumor)));
        }
        return string.Join(BasicData.Text("presentation.rumorLog.separator"), entries);
    }

    #region E3 고정 UI·요약·상세 표시

    // 시간 칸 정산 표시. 남은 시간은 턴(TurnSpan), 예상치는 열차 전체 시너지 합(정산 계산 재사용 값)을 넣는다.
    public static string GaugeInline(long _remainingHours, decimal _estimate)
    {
        return BasicData.Format("presentation.gauge.inline", TurnSpan(_remainingHours), _estimate);
    }

    // 다음 정산이 아닌 정산 시각 눈금(다음 정산은 PuzzleTimeAccent).
    public static Color GaugeTick => BasicData.Color("presentation.gauge.tick");

    // 소문·의뢰 요약 띠 머리표와 의뢰 빈 상태. 소문 빈 상태는 presentation.station.unreadRumor,
    // 넘치는 항목은 presentation.quest.moreOffers("외 {0}건")를 재사용한다. 띠 전체를 누르면 수첩의 해당 탭이 열린다.
    public static string SummaryRumorTitle => BasicData.Text("presentation.summary.rumorTitle");
    public static string SummaryQuestTitle => BasicData.Text("presentation.summary.questTitle");
    public static string SummaryNoQuest => BasicData.Text("presentation.summary.noQuest");

    // 행동 목록 6번째 카드. 시설이 없는 역의 비활성 카드 문구.
    public static string StationNoFacility => BasicData.Text("presentation.station.noFacility");

    // 정보 바 남은 기한 카드. D-n은 남은 시간을 일 단위로 올림한 값이다.
    public static string TimeDeadlineTitle => BasicData.Text("presentation.time.deadlineTitle");

    public static string TimeDeadlineValue(long _remainingHours)
    {
        long days = (_remainingHours + PuzzleTimeManager.HOURS_PER_DAY - 1) / PuzzleTimeManager.HOURS_PER_DAY;
        return BasicData.Format("presentation.time.deadlineValue", days, TurnSpan(_remainingHours));
    }

    // 남은 기한이 3일 이하면 경고색으로 표시한다(UI_LAYOUT 2절 남은 기한 카드 규칙).
    public static bool TimeDeadlineWarning(long _remainingHours)
    {
        return _remainingHours <= 3 * PuzzleTimeManager.HOURS_PER_DAY;
    }

    // 구매 전에는 화차의 외곽 규격과 파손 칸 생성 여부를 표시한다.
    public static string WagonSizeLabel(int _width, int _height)
    {
        return BasicData.Format("presentation.train.wagonSizeLabel", _width, _height);
    }

    internal static string WagonCellsChip(int _cells)
    {
        return BasicData.Format("presentation.train.wagonCellsChip", _cells);
    }

    public static string WagonFormationDelta(int _ownedDelta)
    {
        return BasicData.Format("presentation.train.wagonFormationDelta", _ownedDelta);
    }

    // 화차 상세 패널 제목과 화차를 고르기 전 안내. 화차 상점은 선택 없음으로 시작한다.
    public static string WagonDetailTitle => BasicData.Text("presentation.train.wagonDetailTitle");
    public static string WagonDetailEmpty => BasicData.Text("presentation.train.wagonDetailEmpty");

    // 화물칸 바 오른쪽 고정 지도 버튼. 닫힘 라벨은 presentation.map.open("지도")을 재사용한다.
    public static string MapToggleClose => BasicData.Text("presentation.map.closeMap");
    public static string MapToggleHint => BasicData.Text("presentation.map.openHint");

    // 지도 노드 소문 배지. 의뢰 배지는 QuestMapBadge(QuestMapMarker 색), 소문 배지 강조색은 PuzzleMapCurrent를 재사용한다.
    public static string MapRumorBadge => BasicData.Text("presentation.map.rumorBadge");

    #region 고유명사 하이퍼링크
    // 링크 ID는 "<접두어>:<테이블 ID>". 만들기(EntityLink)와 읽기(TryParseEntityLink)는 여기 한 곳만 쓴다.
    public static Color EntityLinkColor => BasicData.Color("presentation.link.color");
    public static Color EntityLinkHover => BasicData.Color("presentation.link.hover");
    public static Color EntityLinkHighlight => BasicData.Color("presentation.link.highlight");

    // 고유명사 링크 글자색. 역·지역은 그 지역 노선 색, 나머지는 공통 링크 색.
    public static Color EntityLinkTextColor(EntityLinkKind _kind, string _id)
    {
        return TryGetEntityRegionColor(_kind, _id, out var region) ? region : EntityLinkColor;
    }

    // 마우스를 올린 고유명사 강조 색. 역·지역은 지역 색을 밝게(presentation.link.regionHoverLighten), 나머지는 공통 강조 색.
    public static Color EntityLinkHoverColor(EntityLinkKind _kind, string _id)
    {
        return TryGetEntityRegionColor(_kind, _id, out var region)
            ? UnityEngine.Color.Lerp(region, UnityEngine.Color.white, BasicData.Float("presentation.link.regionHoverLighten"))
            : EntityLinkHover;
    }

    static bool TryGetEntityRegionColor(EntityLinkKind _kind, string _id, out Color _color)
    {
        var tables = TableManager.Instance;
        string regionId = null;
        if (_kind == EntityLinkKind.City && tables.HasCity(_id))
        {
            regionId = tables.CityPrice(_id).regionId;
        }
        else if (_kind == EntityLinkKind.Region && tables.TryGetRegion(_id, out _))
        {
            regionId = _id;
        }
        _color = regionId == null ? default : PuzzleMapLineColor(regionId);
        return regionId != null;
    }

    // 미리보기 카드 제목 색. 도시·지역은 마을권 색(링크 글자색과 동일), 나머지는 기본 툴팁 텍스트 색.
    public static Color EntityPreviewTitleColor(EntityLinkKind _kind, string _id)
    {
        return _kind == EntityLinkKind.City || _kind == EntityLinkKind.Region
            ? EntityLinkTextColor(_kind, _id)
            : PuzzleTooltipText;
    }

    static string EntityLinkPrefix(EntityLinkKind _kind)
    {
        switch (_kind)
        {
            case EntityLinkKind.City: return "city";
            case EntityLinkKind.Item: return "item";
            case EntityLinkKind.Tag: return "tag";
            case EntityLinkKind.Quest: return "quest";
            default: return "region";
        }
    }

    // _text가 비었거나 ID가 없으면 링크 없이 글자만 돌려준다.
    public static string EntityLink(EntityLinkKind _kind, string _id, string _text)
    {
        if (string.IsNullOrEmpty(_id) || string.IsNullOrEmpty(_text))
        {
            return _text ?? string.Empty;
        }
        return $"<link=\"{EntityLinkPrefix(_kind)}:{_id}\"><color=#{ColorUtility.ToHtmlStringRGB(EntityLinkTextColor(_kind, _id))}>{_text}</color></link>";
    }

    public static bool TryParseEntityLink(string _linkId, out EntityLinkKind _kind, out string _id)
    {
        _kind = EntityLinkKind.City;
        _id = null;
        if (string.IsNullOrEmpty(_linkId))
        {
            return false;
        }
        int colon = _linkId.IndexOf(':');
        if (colon <= 0 || colon == _linkId.Length - 1)
        {
            return false;
        }
        string prefix = _linkId.Substring(0, colon);
        _id = _linkId.Substring(colon + 1);
        foreach (EntityLinkKind kind in System.Enum.GetValues(typeof(EntityLinkKind)))
        {
            if (EntityLinkPrefix(kind) == prefix)
            {
                _kind = kind;
                return true;
            }
        }
        _id = null;
        return false;
    }

    public static string CityLink(string _cityId)
    {
        var tables = TableManager.Instance;
        string name = tables.HasCity(_cityId) ? tables.CityPrice(_cityId).displayName : _cityId;
        return EntityLink(EntityLinkKind.City, _cityId, name);
    }

    public static string TagLink(string _tag)
    {
        return EntityLink(EntityLinkKind.Tag, _tag, PuzzleTagLabel(_tag));
    }

    public static string ItemLink(string _pieceId, string _displayName)
    {
        return EntityLink(EntityLinkKind.Item, _pieceId, _displayName);
    }

    public static string RegionLink(PuzzleRegionData _region)
    {
        return _region == null ? string.Empty : EntityLink(EntityLinkKind.Region, _region.id, _region.displayName);
    }

    // 링크 없이 이름만(우클릭 메뉴 '이전' 항목 등).
    internal static string EntityName(EntityLinkKind _kind, string _id, PuzzleInventoryService _session)
    {
        var tables = TableManager.Instance;
        switch (_kind)
        {
            case EntityLinkKind.City:
                return tables.HasCity(_id) ? tables.CityPrice(_id).displayName : _id;
            case EntityLinkKind.Region:
                return tables.TryGetRegion(_id, out var region) ? region.displayName : _id;
            case EntityLinkKind.Tag:
                return PuzzleTagLabel(_id);
            case EntityLinkKind.Item:
                var piece = FindPiece(_id);
                return piece != null ? piece.displayName : _id;
            default:
                var quest = FindQuest(_session, _id);
                return quest != null ? BasicData.Format("presentation.link.previewQuestRoute", QuestCargoPlain(quest.PieceId), QuestCityPlain(quest.TargetCityId)) : _id;
        }
    }

    static PuzzlePieceData FindPiece(string _id)
    {
        foreach (var piece in TableManager.Instance.Pieces)
        {
            if (piece.id == _id)
            {
                return piece;
            }
        }
        return null;
    }

    static TrainventoryQuest FindQuest(PuzzleInventoryService _session, string _id)
    {
        if (_session == null || !int.TryParse(_id, out int questId))
        {
            return null;
        }
        foreach (var quest in _session.Quests)
        {
            if (quest.Id == questId)
            {
                return quest;
            }
        }
        return null;
    }

    static string QuestCargoPlain(string _pieceId)
    {
        var piece = FindPiece(_pieceId);
        return piece != null ? piece.displayName : _pieceId;
    }

    static string QuestCityPlain(string _cityId)
    {
        return TableManager.Instance.HasCity(_cityId) ? TableManager.Instance.CityPrice(_cityId).displayName : _cityId;
    }

    public static string EntityLinkActionLabel(EntityLinkAction _action)
    {
        switch (_action)
        {
            case EntityLinkAction.Map: return BasicData.Text("presentation.link.actionMap");
            case EntityLinkAction.Rumors: return BasicData.Text("presentation.link.actionRumors");
            case EntityLinkAction.Proficiency: return BasicData.Text("presentation.link.actionProficiency");
            case EntityLinkAction.Quests: return BasicData.Text("presentation.link.actionQuests");
            default: return BasicData.Text("presentation.link.actionSynergy");
        }
    }

    public static string EntityLinkBackLabel(string _name)
    {
        return BasicData.Format("presentation.link.actionBack", _name);
    }

    // 마우스를 올렸을 때 보이는 미리보기 카드 내용(제목 + 상단 표 행 + 본문 줄). 링크 없이 글자만 쓴다(카드는 입력을 받지 않는다).
    internal static void EntityPreview(EntityLinkKind _kind, string _id, PuzzleInventoryService _session,
        out string _title, List<(string label, string value)> _statRows, List<string> _lines,
        bool _includeCurrentLine = true)
    {
        _statRows?.Clear();
        _lines.Clear();
        _title = EntityName(_kind, _id, _session);
        var tables = TableManager.Instance;
        switch (_kind)
        {
            case EntityLinkKind.City:
                PreviewCity(_id, _session, _statRows, _lines, _includeCurrentLine);
                break;
            case EntityLinkKind.Region:
                if (tables.TryGetRegion(_id, out var region))
                {
                    int cityCount = tables.Cities.Count(_city => _city.regionId == region.id);
                    _lines.Add(BasicData.Format("presentation.link.previewRegionCities", cityCount));
                    if (region.specialtyTags != null && region.specialtyTags.Length > 0)
                    {
                        _lines.Add(BasicData.Format("presentation.link.previewSpecialty", string.Join(" · ", region.specialtyTags.Select(PuzzleTagLabel))));
                    }
                }
                break;
            case EntityLinkKind.Item:
                // 품목은 아이템 정보 팝업(ItemInfoPopup)이 맡는다.
                break;
            case EntityLinkKind.Tag:
                PreviewTag(_id, _lines);
                break;
            default:
                PreviewQuest(_id, _session, _lines);
                break;
        }
    }

    internal static bool TryGetCityRegionRow(string _cityId, out (string label, string value) _row)
    {
        var tables = TableManager.Instance;
        if (tables.HasCity(_cityId))
        {
            var city = tables.CityPrice(_cityId);
            if (tables.TryGetRegion(city.regionId, out var region))
            {
                string hex = ColorUtility.ToHtmlStringRGB(PuzzleMapLineColor(region.id));
                _row = ("지역", $"<color=#{hex}>{region.displayName}</color>");
                return true;
            }
        }
        _row = default;
        return false;
    }

    static void PreviewCity(string _cityId, PuzzleInventoryService _session,
        List<(string label, string value)> _statRows, List<string> _lines, bool _includeCurrentLine = true)
    {
        var tables = TableManager.Instance;
        if (!tables.HasCity(_cityId))
        {
            return;
        }
        if (TryGetCityRegionRow(_cityId, out var regionRow))
        {
            if (_statRows != null)
            {
                _statRows.Add(regionRow);
            }
            else
            {
                _lines.Add(BasicData.Format("presentation.link.previewRegion", regionRow.value));
            }
        }
        bool visited = _session != null && _session.HasVisitedCity(_cityId);
        if (_session != null)
        {
            if (_cityId == _session.CurrentCityId)
            {
                if (_includeCurrentLine)
                {
                    _lines.Add(BasicData.Text("presentation.link.previewCurrent"));
                }
            }
            else
            {
                var travel = _session.EvaluateTravel(_cityId);
                if (travel.Failure == PuzzleTravelFailure.None || travel.Failure == PuzzleTravelFailure.InsufficientFunds)
                {
                    _lines.Add(BasicData.Format("presentation.link.previewTravel", TurnSpan(travel.TravelHours), travel.Cost));
                }
                _lines.Add(BasicData.Text(visited ? "presentation.link.previewVisited" : "presentation.link.previewUnvisited"));
            }
        }
    }

    static void PreviewTag(string _tag, List<string> _lines)
    {
        var chart = PuzzleSynergyChart.ForTrain(TableManager.Instance);
        int row = -1;
        for (int i = 0; i < chart.Rows.Count; i++)
        {
            if (chart.Rows[i] == _tag)
            {
                row = i;
                break;
            }
        }
        int added = 0;
        if (row >= 0)
        {
            for (int column = 0; column < chart.Columns.Count && added < 4; column++)
            {
                int percent = chart.PercentAt(row, column);
                if (percent == 0)
                {
                    continue;
                }
                _lines.Add(BasicData.Format("presentation.link.previewSynergy", PuzzleTagLabel(chart.Columns[column]),
                    percent > 0 ? "+" + percent : percent.ToString()));
                added++;
            }
        }
        if (added == 0)
        {
            _lines.Add(BasicData.Text("presentation.link.previewNoSynergy"));
        }
    }

    static void PreviewQuest(string _id, PuzzleInventoryService _session, List<string> _lines)
    {
        var quest = FindQuest(_session, _id);
        if (quest == null)
        {
            return;
        }
        long remaining = Math.Max(0, quest.DeadlineHours - _session.ElapsedTime);
        _lines.Add(BasicData.Format("presentation.link.previewQuestDeadline", TurnSpan(remaining)));
        _lines.Add(BasicData.Format("presentation.link.previewQuestReward", quest.Reward));
    }
    #endregion

    #region 아이템 정보 팝업
    // 품목 링크에 마우스를 올렸을 때의 정보. 화물 종류 자체의 값만 쓴다(칸에 놓인 화물의 현재 가치·신선도는 화물 툴팁이 맡는다).
    internal static PuzzlePieceData ItemInfoPiece(string _pieceId)
    {
        return FindPiece(_pieceId);
    }

    public static Color ItemInfoLabelColor => PuzzleTooltipText;

    internal static void ItemInfoStats(PuzzlePieceData _piece, List<string> _labels, List<string> _values)
    {
        _labels.Clear();
        _values.Clear();
        _labels.Add(BasicData.Text("presentation.itemInfo.value"));
        _values.Add(BasicData.Format("presentation.itemInfo.valueFormat", _piece.baseValue));
        _labels.Add(BasicData.Text("presentation.itemInfo.size"));
        _values.Add(BasicData.Format("presentation.itemInfo.sizeFormat", _piece.cells?.Length ?? 0));
        if (_piece.shelfLifeHours > 0)
        {
            _labels.Add(BasicData.Text("presentation.itemInfo.shelfLife"));
            _values.Add(BasicData.Format("presentation.itemInfo.shelfLifeFormat", PuzzleTimeManager.TurnsOf(_piece.shelfLifeHours)));
        }
    }

    // 이 품목이 기차 화물칸에서 받는 인접 상성(맞닿은 면 하나당). 품목 태그가 대상인 행과, 제외 조건에 걸리지 않는 '!' 행을 합친다.
    internal static void ItemInfoSynergies(PuzzlePieceData _piece, List<string> _lines)
    {
        _lines.Clear();
        int[] totals = ComputeTrainSynergyTotals(_piece, out var chart);
        string positive = ColorUtility.ToHtmlStringRGB(BasicData.Color("presentation.itemInfo.positive"));
        string negative = ColorUtility.ToHtmlStringRGB(BasicData.Color("presentation.itemInfo.negative"));
        foreach (int column in OrderedSynergyColumns(totals, chart))
        {
            int percent = totals[column];
            _lines.Add(BasicData.Format("presentation.itemInfo.synergyLine", PuzzleTagLabel(chart.Columns[column]),
                percent > 0 ? positive : negative, percent > 0 ? "+" + percent : percent.ToString()));
        }
    }

    static int[] ComputeTrainSynergyTotals(PuzzlePieceData _piece, out PuzzleSynergyChart _chart)
    {
        var tags = _piece.tags ?? Array.Empty<string>();
        _chart = PuzzleSynergyChart.ForTrain(TableManager.Instance);
        var totals = new int[_chart.Columns.Count];
        for (int row = 0; row < _chart.Rows.Count; row++)
        {
            string key = _chart.Rows[row];
            bool applies = PuzzleSynergyChart.IsExclusionRow(key)
                ? !PuzzleSynergyChart.ExcludedTags(key).Any(tags.Contains)
                : tags.Contains(key);
            if (!applies)
            {
                continue;
            }
            for (int column = 0; column < _chart.Columns.Count; column++)
            {
                totals[column] += _chart.PercentAt(row, column);
            }
        }
        return totals;
    }

    static IEnumerable<int> OrderedSynergyColumns(int[] _totals, PuzzleSynergyChart _chart)
    {
        return Enumerable.Range(0, _totals.Length)
            .Where(_column => _totals[_column] != 0)
            .OrderByDescending(_column => _totals[_column])
            .ThenBy(_column => _chart.Columns[_column], StringComparer.Ordinal);
    }

    public static string ItemInfoSynergyHeader => BasicData.Text("presentation.itemInfo.synergyHeader");
    public static string ItemInfoNoSynergy => BasicData.Text("presentation.itemInfo.noSynergy");

    public static string ItemInfoMore(int _count)
    {
        return BasicData.Format("presentation.itemInfo.more", _count);
    }
    #endregion

    // 지도 역 이름 뒤 어두운 판(지형 위 가독성).
    public static Color MapLabelPlate => BasicData.Color("presentation.map.labelPlate");

    // 술집 의뢰 상세 패널 줄. 기한 표기는 rumorLog와 같은 GameTimestamp를 쓴다.
    public static string QuestDetailDestination(string _cityName, long _travelTurns)
    {
        return BasicData.Format("presentation.quest.detailDestination", _cityName,
            TurnSpan(PuzzleTimeManager.HoursOf(_travelTurns)));
    }

    public static string QuestDetailDeadline(long _deadlineHours)
    {
        return BasicData.Format("presentation.quest.detailDeadline", GameTimestamp(_deadlineHours));
    }

    public static string QuestDetailReward(long _reward, string _proficiencyTag, long _proficiencyXp)
    {
        return BasicData.Format("presentation.quest.detailReward", _reward, PuzzleTagLabel(_proficiencyTag), _proficiencyXp);
    }

    public static string QuestDetailDeposit(long _deposit)
    {
        return BasicData.Format("presentation.quest.detailDeposit", _deposit);
    }

    public static string QuestDetailCargo(string _cargoName, int _cellCount)
    {
        return BasicData.Format("presentation.quest.detailCargo", _cargoName, _cellCount);
    }

    // 상세 패널 제목과 의뢰를 고르기 전 안내. 게시판은 선택 없음으로 시작한다.
    public static string QuestDetailTitle => BasicData.Text("presentation.quest.detailTitle");
    public static string QuestDetailEmpty => BasicData.Text("presentation.quest.detailEmpty");

    #endregion

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
        return ItemLink(_pieceId, TableManager.Instance.Piece(_pieceId).displayName);
    }

    internal static string QuestCityName(string _cityId)
    {
        return CityLink(_cityId);
    }

    // 실패 시 받는 불이익. 보증금은 금액을, 지역 거래 불이익은 의뢰를 준 지역 이름을 함께 표시한다.
    static string QuestPenaltyText(string _penaltyId, long _deposit, string _sourceCityId)
    {
        var tables = TableManager.Instance;
        var penalty = tables.QuestPenalty(_penaltyId);
        return penalty.type == nameof(TrainventoryQuestPenaltyType.Deposit)
            ? BasicData.Format("presentation.quest.depositPenalty", penalty.displayName, _deposit)
            : BasicData.Format("presentation.quest.regionPenalty", penalty.displayName,
                RegionLink(tables.Region(tables.CityPrice(_sourceCityId).regionId)));
    }

    internal static string QuestOffer(TrainventoryQuestOffer _offer)
    {
        return BasicData.Format("presentation.quest.offer", QuestCargoName(_offer.PieceId), QuestCityName(_offer.TargetCityId),
            TurnSpan(PuzzleTimeManager.HoursOf(_offer.TravelTurns)), TurnSpan(_offer.DeadlineHours), _offer.Reward,
            QuestPenaltyText(_offer.PenaltyId, _offer.Deposit, _offer.SourceCityId), PuzzleTagLabel(_offer.ProficiencyTag),
            _offer.ProficiencyXp);
    }

    // 개편 술집 창 의뢰 정보 카드: 제목 · 네 칸(최단 이동 · 기한 · 보수 · 숙련) · 실패 조건.
    internal static string QuestCardTitle(TrainventoryQuestOffer _offer) =>
        BasicData.Format("presentation.quest.cardTitle", QuestCargoName(_offer.PieceId), QuestCityName(_offer.TargetCityId));
    internal static string QuestCardTravelLabel => BasicData.Text("presentation.quest.cardTravelLabel");
    internal static string QuestCardDeadlineLabel => BasicData.Text("presentation.quest.cardDeadlineLabel");
    internal static string QuestCardRewardLabel => BasicData.Text("presentation.quest.cardRewardLabel");
    internal static string QuestCardProficiencyLabel => BasicData.Text("presentation.quest.cardProficiencyLabel");
    internal static string QuestCardTravel(TrainventoryQuestOffer _offer) => TurnSpan(PuzzleTimeManager.HoursOf(_offer.TravelTurns));
    internal static string QuestCardDeadline(TrainventoryQuestOffer _offer) => TurnSpan(_offer.DeadlineHours);
    internal static string QuestCardReward(TrainventoryQuestOffer _offer) =>
        BasicData.Format("presentation.quest.cardRewardValue", _offer.Reward);
    internal static string QuestCardProficiency(TrainventoryQuestOffer _offer) =>
        BasicData.Format("presentation.quest.cardProficiencyValue", PuzzleTagLabel(_offer.ProficiencyTag), _offer.ProficiencyXp);
    internal static string QuestCardPenalty(TrainventoryQuestOffer _offer) =>
        BasicData.Format("presentation.quest.cardPenalty", QuestPenaltyText(_offer.PenaltyId, _offer.Deposit, _offer.SourceCityId));

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
        return BasicData.Format("presentation.quest.accepted", cargo, GameTimestamp(_info.deadlineHours),
            QuestCityName(_info.targetCityId));
    }

    public static Color QuestMapMarker => BasicData.Color("presentation.quest.mapMarker");

    static IEnumerable<TrainventoryQuest> ActiveQuestsTo(PuzzleInventoryService _session, string _cityId)
    {
        return _session.Quests.Where(_quest => _quest.State == TrainventoryQuestState.Active &&
            string.Equals(_quest.TargetCityId, _cityId, StringComparison.Ordinal));
    }

    // 지도 역 이름 옆 의뢰 표식. 이 역이 목적지인 진행 중 의뢰가 없으면 null이다.
    // 의뢰가 여럿이면 건수와 가장 급한 의뢰의 남은 시간을 보인다.
    internal static string QuestMapBadge(PuzzleInventoryService _session, string _cityId)
    {
        var quests = ActiveQuestsTo(_session, _cityId).ToList();
        if (quests.Count == 0)
        {
            return null;
        }
        string remaining = TurnSpan(Math.Max(0, quests.Min(_quest => _quest.DeadlineHours) - _session.ElapsedTime));
        return quests.Count == 1
            ? BasicData.Format("presentation.quest.mapBadge", remaining)
            : BasicData.Format("presentation.quest.mapBadgeMany", quests.Count, remaining);
    }

    internal static string QuestMapStation(string _stationName, string _badge)
    {
        return string.IsNullOrEmpty(_badge) ? _stationName
            : BasicData.Format("presentation.quest.mapStation", _stationName, _badge, ColorUtility.ToHtmlStringRGB(QuestMapMarker));
    }

    // 지도 역 정보 끝에 붙이는 진행 중 의뢰 목록. 없으면 빈 문자열이다.
    internal static string QuestMapInformation(PuzzleInventoryService _session, string _cityId)
    {
        return string.Join("\n", ActiveQuestsTo(_session, _cityId)
            .OrderBy(_quest => _quest.DeadlineHours)
            .Select(_quest => BasicData.Format("presentation.quest.mapInformation", QuestCargoName(_quest.PieceId),
                TurnSpan(Math.Max(0, _quest.DeadlineHours - _session.ElapsedTime)), _quest.Reward)));
    }

    internal static string BuildTownHeaderStatus(string _cityId, PuzzleInventoryService _session)
    {
        if (_session == null || string.IsNullOrEmpty(_cityId))
        {
            return null;
        }
        bool isCurrent = string.Equals(_cityId, _session.CurrentCityId, StringComparison.Ordinal);
        string questBadge = QuestMapBadge(_session, _cityId);
        string questText = string.IsNullOrEmpty(questBadge)
            ? null
            : $"<color=#{ColorUtility.ToHtmlStringRGB(QuestMapMarker)}>{questBadge}</color>";
        if (isCurrent && !string.IsNullOrEmpty(questText))
        {
            return $"{BasicData.Text("presentation.link.previewCurrent")} · {questText}";
        }
        if (isCurrent)
        {
            return BasicData.Text("presentation.link.previewCurrent");
        }
        return questText;
    }

    // 숙련 레벨이 오를 때 하단 상태 줄 끝에 붙이는 알림.
    internal static string ProficiencyLevelUp(PuzzleInventoryService _session, GameEvents.ProficiencyInfo _info)
    {
        return BasicData.Format("presentation.proficiency.levelUp", TagLink(_info.tag), _info.level,
            ProficiencyPercent(_session.ProficiencyBonusPercent(_info.level)));
    }

    public static string ProficiencyNoticeSeparator => BasicData.Text("presentation.proficiency.noticeSeparator");

    static string ProficiencyPercent(decimal _percent)
    {
        return _percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    // 수첩 숙련 탭·정보 바 평판. 숫자는 세션(테이블 원본 계산)이 넘기고 여기서는 문구·색만 정한다.
    public static string RumorLogProficiencyTab => BasicData.Text("presentation.rumorLog.proficiencyTab");

    public static string InfoBarReputation(int _reputation)
    {
        return BasicData.Format("presentation.proficiency.infoBar", _reputation);
    }

    public static string ProficiencyPanelReputation(int _reputation, int _maxActiveQuests)
    {
        return BasicData.Format("presentation.proficiency.panelReputation", _reputation, _maxActiveQuests);
    }

    public static string ProficiencyPanelNextStage(int _nextReputation, int _remaining)
    {
        return BasicData.Format("presentation.proficiency.panelNextStage", _nextReputation, _remaining);
    }

    // 평판으로 늘어나는 증차 상한. 값은 세션·테이블이 계산해 넘기고 여기서는 문구만 붙인다.
    public static string ProficiencyPanelWagonLimit(int _maxWagonCount, int _cap)
    {
        return BasicData.Format("presentation.proficiency.panelWagonLimit", _maxWagonCount, _cap);
    }

    public static string ProficiencyPanelMaxStage => BasicData.Text("presentation.proficiency.panelMaxStage");
    public static string ProficiencyPanelHint => BasicData.Text("presentation.proficiency.panelHint");

    public static string ProficiencyRowLevel(int _level)
    {
        return BasicData.Format("presentation.proficiency.rowLevel", _level);
    }

    public static string ProficiencyChipLevel(int _level)
    {
        return BasicData.Format("presentation.proficiency.chipLevel", _level);
    }

    public static string ProficiencyRowXp(long _xp, long _nextXp)
    {
        return BasicData.Format("presentation.proficiency.rowXp", _xp, _nextXp);
    }

    // 거래 이득은 소수 첫째 자리까지 표기한다(+x.x%).
    public static string ProficiencyRowBonus(decimal _percent)
    {
        return BasicData.Format("presentation.proficiency.rowBonus",
            _percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    }

    public static Color ProficiencyRowBackground => BasicData.Color("presentation.proficiency.rowBackground");
    public static Color ProficiencyBarTrack => BasicData.Color("presentation.proficiency.barTrack");
    public static Color ProficiencyBarFill => BasicData.Color("presentation.proficiency.barFill");
    // 경험치가 0인 태그 행의 흐린 글자색.
    public static Color ProficiencyDimText => BasicData.Color("presentation.proficiency.dimText");

    // 수첩 의뢰 탭 본문. 평판·진행 수, 지역 불이익, 진행 중 의뢰, 끝난 의뢰(최근 순) 순서다. 숙련 목록은 숙련 탭에 있다.
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
                entries.Add(BasicData.Format("presentation.quest.logRegionPenalty", RegionLink(region),
                    GameTimestamp(penalty.UntilHours), penalty.BuyPercentDelta, penalty.SellPercentDelta));
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
                QuestCityName(quest.TargetCityId), GameTimestamp(quest.DeadlineHours), quest.Reward,
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

    // 수첩 상성표 탭의 빌드 요약. 열차 화물의 태그 분포와 성립 중인 콤보를 평가 결과(SynergyBonuses)에서 읽고,
    // 규칙 id와 숙련 보강치는 같은 판정(IsTarget·IsSource)과 세션 조회자로 라벨만 붙인다.
    internal static string TrainBuildSummary(PuzzleInventoryService _session)
    {
        var tables = TableManager.Instance;
        var entries = new List<string>
        {
            BasicData.Text("presentation.buildSummary.title")
        };
        var tagCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int cargoCount = 0;
        foreach (var wagon in _session.TrainWagons)
        {
            foreach (var placement in wagon.Placements)
            {
                cargoCount++;
                foreach (string tag in placement.Definition.Tags)
                {
                    tagCounts.TryGetValue(tag, out int count);
                    tagCounts[tag] = count + 1;
                }
            }
        }
        if (cargoCount == 0)
        {
            entries.Add(BasicData.Text("presentation.buildSummary.empty"));
            return string.Join(BasicData.Text("presentation.buildSummary.separator"), entries);
        }
        string distribution = string.Join("  ", tagCounts
            .OrderByDescending(_entry => _entry.Value)
            .ThenBy(_entry => _entry.Key, StringComparer.Ordinal)
            .Take(5)
            .Select(_entry => $"{PuzzleTagLabel(_entry.Key)}×{_entry.Value}"));
        entries.Add(BasicData.Format("presentation.buildSummary.tagHeader", distribution));

        var rules = tables.SynergiesFor(PuzzleSynergyScope.Train);
        string wasteTag = tables.PuzzleSynergy.animals.wasteTag;
        foreach (var wagon in _session.TrainWagons)
        {
            entries.Add(BasicData.Format("presentation.buildSummary.wagonHeader", wagon.WagonNumber));
            var neighbors = wagon.Placements.ToDictionary(_placement => _placement.InstanceId);
            var lines = new List<string>();
            foreach (var placement in wagon.Placements)
            {
                // 화살표 표시(RenderSynergies)가 읽는 같은 접촉 데이터를 이웃 단위로 합쳐 쓴다.
                foreach (var bonus in PuzzleSynergyBonus.GroupByNeighbor(wagon.ValueOf(placement.InstanceId).SynergyBonuses)
                    .OrderByDescending(_bonus => _bonus.Percent)
                    .ThenBy(_bonus => _bonus.NeighborName, StringComparer.Ordinal))
                {
                    if (!neighbors.TryGetValue(bonus.NeighborId, out var neighbor))
                    {
                        continue;
                    }
                    string facesText = bonus.Faces > 1
                        ? BasicData.Format("presentation.buildSummary.comboFaces", bonus.Faces)
                        : string.Empty;
                    var labels = new List<string>
                    {
                        BasicData.Format("presentation.buildSummary.comboPercent", PercentText(bonus.Percent), facesText)
                    };
                    var matching = rules.Where(_rule => PuzzlePieceEffects.IsTarget(placement.Definition, _rule, wasteTag) &&
                        PuzzlePieceEffects.IsSource(neighbor.Definition, _rule)).ToArray();
                    if (matching.Length > 0)
                    {
                        labels.Add(BasicData.Format("presentation.buildSummary.ruleLabel",
                            string.Join(", ", matching.Select(_rule => _rule.id))));
                        decimal bonusPercent = matching.Sum(_rule => _session.SynergyComboBonusPercent(_rule));
                        if (bonusPercent > 0m)
                        {
                            labels.Add(BasicData.Format("presentation.buildSummary.comboBonus", PercentText(bonusPercent)));
                        }
                    }
                    lines.Add(BasicData.Format("presentation.buildSummary.comboLine", placement.Definition.DisplayName,
                        neighbor.Definition.DisplayName, string.Join(" · ", labels)));
                }
            }
            if (lines.Count == 0)
            {
                lines.Add(BasicData.Text("presentation.buildSummary.noCombo"));
            }
            entries.AddRange(lines);
        }
        return string.Join(BasicData.Text("presentation.buildSummary.separator"), entries);
    }

    // 콤보 백분율 표기. "+20", "-10"처럼 부호를 붙이고 소수점은 필요한 자리만 남긴다.
    static string PercentText(decimal _percent)
    {
        return _percent.ToString("+0.##;-0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion

    public static string TavernNoRumor => BasicData.Text("presentation.station.noRumor");
    public static string TavernUnreadRumor => BasicData.Text("presentation.station.unreadRumor");
    public static string TavernTalk => BasicData.Text("presentation.station.talk");

    // 행선지 렌즈 문구(계획 §5). 금액·퍼센트 서식은 기초데이터 템플릿이 맡고, 여기서는 부호와 목록 조립만 한다.
    internal static string RouteLensTab(PuzzleLensTab _tab)
    {
        return _tab switch
        {
            PuzzleLensTab.Visited => BasicData.Text("presentation.inventory.routeLensTabVisited"),
            PuzzleLensTab.Rumor => BasicData.Text("presentation.inventory.routeLensTabRumor"),
            _ => BasicData.Text("presentation.inventory.routeLensTabCurrent"),
        };
    }

    internal static string RouteLensTitle(PuzzleItemDefinition _item)
    {
        if (_item == null)
        {
            return string.Empty;
        }
        string tags = string.Join(" · ", _item.Tags.Select(PuzzleTagLabel));
        return string.IsNullOrEmpty(tags) ? _item.DisplayName
            : BasicData.Format("presentation.inventory.routeLensTitleTags", _item.DisplayName, tags);
    }

    // 판매 견적이 음수면 잔액이 줄어든다. 방향별 부호를 뒤집지 않고 잔액 산식을 그대로 쓴다.
    internal static string RouteLensTrade(PuzzleTradeDirection _direction, long? _quote, long _fundsBefore, string _failure, bool _committed)
    {
        string body;
        if (_direction == PuzzleTradeDirection.None)
        {
            body = BasicData.Text("presentation.inventory.routeLensTradeNone");
        }
        else if (_quote is long quote)
        {
            bool buying = _direction == PuzzleTradeDirection.Buy;
            decimal after = buying ? (decimal)_fundsBefore - quote : (decimal)_fundsBefore + quote;
            body = BasicData.Format(buying ? "presentation.inventory.routeLensTradeBuy" : "presentation.inventory.routeLensTradeSell",
                quote, _fundsBefore, after);
        }
        else
        {
            body = BasicData.Format("presentation.inventory.routeLensTradeNoQuote", _fundsBefore);
        }

        if (_committed)
        {
            body = BasicData.Format("presentation.inventory.routeLensCommitted", body);
        }
        if (!string.IsNullOrEmpty(_failure))
        {
            body += "\n<color=#" + ColorUtility.ToHtmlStringRGB(PuzzleTooltipRed) + ">" + _failure + "</color>";
        }
        return body;
    }

    internal static string RouteLensPlacementSpot(decimal? _delta, int _affectedCount)
    {
        return _delta is decimal delta
            ? BasicData.Format("presentation.inventory.routeLensSpot", RouteLensSignedAmount(delta), _affectedCount)
            : BasicData.Text("presentation.inventory.routeLensSpotBlocked");
    }

    internal static string RouteLensRanking(IReadOnlyList<decimal> _top)
    {
        if (_top == null || _top.Count == 0)
        {
            return BasicData.Text("presentation.inventory.routeLensRankingEmpty");
        }

        var badges = new List<string>(_top.Count);
        for (int i = 0; i < _top.Count; i++)
        {
            badges.Add(RouteLensCandidateBadge(i + 1, _top[i]));
        }
        return BasicData.Format("presentation.inventory.routeLensRanking", string.Join(" · ", badges));
    }

    internal static string RouteLensCandidateBadge(int _rank, decimal _delta)
    {
        // 원문자 ①(U+2460)부터 잇는다. 20위를 넘으면 숫자로 쓴다.
        string mark = _rank >= 1 && _rank <= 20 ? char.ConvertFromUtf32(0x2460 + _rank - 1) : _rank + ".";
        return mark + " " + RouteLensSignedAmount(_delta);
    }

    // 현재 탭 목록은 비운다. 현재 도시 견적은 TradeStatus가 맡는다.
    internal static string RouteLensDestinations(IReadOnlyList<PuzzleLensDestination> _list, PuzzleLensTab _tab)
    {
        if (_tab == PuzzleLensTab.Current)
        {
            return string.Empty;
        }

        var level = _tab == PuzzleLensTab.Visited ? PuzzleLensLevel.Visited : PuzzleLensLevel.Rumor;
        var lines = new List<string>();
        if (_list != null)
        {
            foreach (var destination in _list)
            {
                if (destination.Level != level)
                {
                    continue;
                }
                lines.Add(level == PuzzleLensLevel.Visited
                    ? RouteLensVisitedLine(destination)
                    : RouteLensRumorLine(destination));
            }
        }

        if (lines.Count == 0)
        {
            return BasicData.Text(level == PuzzleLensLevel.Visited
                ? "presentation.inventory.routeLensNoVisited"
                : "presentation.inventory.routeLensNoRumor");
        }
        return string.Join("\n", lines);
    }

    static string RouteLensVisitedLine(PuzzleLensDestination _destination)
    {
        var parts = CreateRouteDestinationParts(_destination);
        if (_destination.SellPercent is int sell)
        {
            parts.Add(BasicData.Format("presentation.inventory.routeLensVisitedSell", sell));
        }
        if (_destination.RegionPercent is int region)
        {
            parts.Add(BasicData.Format("presentation.inventory.routeLensVisitedRegion", region));
        }
        if (_destination.Quote is long quote)
        {
            parts.Add(BasicData.Format("presentation.inventory.routeLensVisitedQuote", quote));
        }
        AppendRouteCommonParts(parts, _destination);
        return string.Join(" · ", parts);
    }

    static string RouteLensRumorLine(PuzzleLensDestination _destination)
    {
        var parts = CreateRouteDestinationParts(_destination);
        parts.Add(string.IsNullOrEmpty(_destination.RumorMessage)
            ? BasicData.Text("presentation.inventory.routeLensRumorFallback")
            : _destination.RumorMessage);
        if (_destination.RumorHoursAgo is long hours)
        {
            parts.Add(BasicData.Format("presentation.inventory.routeLensRumorHours", hours));
        }
        if (_destination.RumorStale)
        {
            parts.Add(BasicData.Text("presentation.inventory.routeLensRumorStale"));
        }
        AppendRouteCommonParts(parts, _destination);
        return string.Join(" · ", parts);
    }

    static List<string> CreateRouteDestinationParts(PuzzleLensDestination _destination)
    {
        var parts = new List<string>();
        if (_destination.Planned)
        {
            parts.Add(BasicData.Text("presentation.inventory.routeLensPlanned"));
        }
        parts.Add(_destination.CityName);
        return parts;
    }

    static void AppendRouteCommonParts(List<string> _parts, PuzzleLensDestination _destination)
    {
        if (_destination.Fare is long fare)
        {
            _parts.Add(BasicData.Format("presentation.inventory.routeLensFare", fare));
        }
        _parts.Add(BasicData.Format("presentation.inventory.routeLensTravelTurns", _destination.TravelTurns));
        if (_destination.Planned)
        {
            _parts.Add(BasicData.Format("presentation.inventory.routeLensLegs", _destination.Legs));
        }
    }

    internal static string RouteLensFootnote(PuzzleLensTab _tab)
    {
        return _tab switch
        {
            PuzzleLensTab.Visited => BasicData.Text("presentation.inventory.routeLensFootnoteVisited"),
            PuzzleLensTab.Rumor => BasicData.Text("presentation.inventory.routeLensFootnoteRumor"),
            _ => BasicData.Text("presentation.inventory.routeLensFootnoteCurrent"),
        };
    }

    internal static string LensCandidatesToggle(bool _on)
    {
        return BasicData.Text(_on ? "presentation.inventory.routeLensCandidatesOn" : "presentation.inventory.routeLensCandidatesOff");
    }

    // 델타 표기는 시너지 칩과 같이 +와 U+2212 마이너스를 쓴다.
    static string RouteLensSignedAmount(decimal _value)
    {
        if (_value == 0m)
        {
            return "0";
        }
        return (_value < 0m ? "−" : "+") + Math.Abs(_value).ToString("#,0.##");
    }

    // 시계: 날짜·턴 시간대·낮밤·턴 번호.
    public static string PuzzleTime(PuzzleTimeManager _time)
    {
        return BasicData.Format("presentation.time.clock", _time.Day + 1, TurnSlotName(_time.TurnSlot),
            _time.IsNight ? BasicData.Text("presentation.time.clockText2") : BasicData.Text("presentation.time.clockText3"),
            _time.TurnNumber);
    }

    public static Color PuzzleTimeAccent => BasicData.Color("presentation.time.accent");
    public static Color TimeDayBar => BasicData.Color("presentation.time.dayBar");
    public static Color TimeNightBar => BasicData.Color("presentation.time.nightBar");
    // 전장의 안개 고지도 색(채도 낮은 양피지). 물·땅·높은 땅·가장자리 그을림. 고지도 이미지 생성 도구가 쓴다.
    public static Color PuzzleMapOldWater => BasicData.Color("presentation.map.oldWater");
    public static Color PuzzleMapOldLand => BasicData.Color("presentation.map.oldLand");
    public static Color PuzzleMapOldHigh => BasicData.Color("presentation.map.oldHigh");
    public static Color PuzzleMapOldBurn => BasicData.Color("presentation.map.oldBurn");
    // 노선 외곽선 색. 고지도·지형 위에서 노선을 또렷하게 한다.
    public static Color PuzzleMapTrackOutline => BasicData.Color("presentation.map.trackOutline");
    // 아는 노선 중 아직 달려 보지 않은 노선의 색(짙은 회색).
    public static Color PuzzleMapUntraveledTrack => BasicData.Color("presentation.map.untraveledTrack");
    // 경로 계획에서 아직 모르는 경유역의 표시 이름.
    public static string RouteUnknownStation => BasicData.Text("presentation.route.unknownStation");

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
    // 화차 상점 하위 화면 열림 여부는 StationActionsView가 이 이름으로 PanelVisibility를 올린다.
    public const string PUZZLE_WAGON_SHOP_PANEL_NAME = "WagonShopContent";
    public static string PuzzleTravelQuestion => BasicData.Text("presentation.inventory.travelQuestion");
    public static string PuzzleTravelConfirm => BasicData.Text("presentation.inventory.travelConfirm");
    public static string PuzzleTravelCancel => BasicData.Text("presentation.inventory.travelCancel");
    public static string PuzzleTravelInProgress => BasicData.Text("presentation.inventory.travelInProgress");
    public static string PuzzleTravelArrived => BasicData.Text("presentation.inventory.travelArrived");
    public static Color PuzzleTravelDim => BasicData.Color("presentation.inventory.travelDim");
    public static Color PuzzleTravelCard => BasicData.Color("presentation.inventory.travelCard");

    internal static string PuzzleTravelConfirmation(string _fromName, string _toName, PuzzleTravelResult _quote)
    {
        return BasicData.Format("presentation.inventory.travelConfirmation", _fromName, _toName, TurnSpan(_quote.TravelHours),
            TurnSlotName(_quote.ArrivalHourOfDay / PuzzleTimeManager.HOURS_PER_TURN), (_quote.ArrivalIsNight ? BasicData.Text("presentation.inventory.travelConfirmationText2") : BasicData.Text("presentation.inventory.travelConfirmationText3")), _quote.Cost, _quote.FundsAfter);
    }

    // 이동 연출의 경로 줄: "카르포트 → 테라그라드 · 운임 12$ · 1턴".
    internal static string PuzzleTravelJourney(string _fromName, string _toName, long _cost, long _hours)
    {
        return BasicData.Format("presentation.inventory.travelJourney", _fromName, _toName, _cost, TurnSpan(_hours));
    }

    // 이동 연출의 시계: "3일 오전 08:20". 분은 10분 단위로 내린다.
    internal static string PuzzleTravelClock(double _elapsedHours)
    {
        long hour = (long)Math.Floor(_elapsedHours);
        int minute = (int)Math.Floor((_elapsedHours - hour) * 6) * 10;
        int clock = (int)((TableManager.Instance.PuzzleTime.startHour + hour % PuzzleTimeManager.HOURS_PER_DAY)
            % PuzzleTimeManager.HOURS_PER_DAY);
        return BasicData.Format("presentation.inventory.travelClock", hour / PuzzleTimeManager.HOURS_PER_DAY + 1,
            TurnSlotName(clock / PuzzleTimeManager.HOURS_PER_TURN), clock, Math.Min(50, minute));
    }
    public static string PuzzleMapFit => BasicData.Text("presentation.map.fit");
    public static string PuzzleWaitDaylight => BasicData.Text("presentation.inventory.waitDaylight");
    public static string StationActionsTitle => BasicData.Text("presentation.station.actionsTitle");
    public static string TitleGameName => BasicData.Text("presentation.title.gameName");
    public static string TitleStart => BasicData.Text("presentation.title.start");
    public static string TitleQuit => BasicData.Text("presentation.title.quit");
    internal static string StationWagonShop => BasicData.Text("presentation.station.wagonShop");
    internal static string StationWagonShopPage(int _page, int _pageCount) =>
        BasicData.Format("presentation.station.wagonShopPage", _page, _pageCount);
    // 개편 역사 창 화차 카드: 이름(크게) · 보드 크기 · 가격.
    internal static string WagonProductCard(PuzzleWagonProductData _product)
    {
        var board = TableManager.Instance.RequireValid().WagonBoardOf(_product);
        return BasicData.Format("presentation.station.wagonProductCard", _product.displayName, board.width, board.height, _product.price);
    }

    public static string WagonProductDragHint => BasicData.Text("presentation.station.wagonProductDragHint");

    internal static string WagonProductLabel(PuzzleWagonProductData _product)
    {
        var board = TableManager.Instance.RequireValid().WagonBoardOf(_product);
        return BasicData.Format("presentation.station.wagonProduct", _product.displayName, board.width, board.height, _product.price);
    }

    // 화차 보드에서 화물을 실을 수 있는 칸 수.
    internal static int WagonUsableCells(PuzzleWagonProductData _product)
    {
        var board = TableManager.Instance.RequireValid().WagonBoardOf(_product);
        return board.width * board.height - (board.blockedCells?.Length ?? 0);
    }

    internal static string WagonSaleAmount(long _price, bool _showPlus = false)
    {
        string color = ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen);
        string sign = _showPlus && _price > 0 ? "+" : string.Empty;
        return $"<color=#{color}>{sign}{_price:N0} $</color>";
    }

    internal static string WagonSaleNotice(string _reason)
    {
        string color = ColorUtility.ToHtmlStringRGB(PuzzleTooltipRed);
        return $"<color=#{color}>{BasicData.Text("rules.wagon." + _reason)}</color>";
    }
    public static string StationMarket => BasicData.Text("presentation.station.market");
    public static string StationMarketClosed => BasicData.Text("presentation.station.marketClosed");
    public static string StationTavern => BasicData.Text("presentation.station.tavern");
    public static string StationRest => BasicData.Text("presentation.station.rest");
    public static string StationTravel => BasicData.Text("presentation.station.travel");
    public static string StationBack => BasicData.Text("presentation.station.back");
    public static string StationRestHint => BasicData.Format("presentation.station.restHint",
        TurnSpan((long)TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours));
    // 휴식 확인 버튼: "저녁까지 휴식 · 1턴".
    public static string StationRestUntil(int _slot, int _turns)
    {
        return BasicData.Format("presentation.station.restUntil", TurnSlotName(_slot),
            TurnSpan(PuzzleTimeManager.HoursOf(_turns)));
    }
    public static string StationRestEarlier => BasicData.Text("presentation.station.restEarlier");
    public static string StationRestLater => BasicData.Text("presentation.station.restLater");
    public static string StationButchery => BasicData.Text("presentation.StationButchery");
    internal static string StationActionDescription(TrainventoryWorkshopData _action) =>
        _action == null ? string.Empty : _action.description;
    public static string StationWorkWaitQuestion => BasicData.Text("presentation.StationWorkWaitQuestion");
    public static string StationWorkWaitLater => BasicData.Text("presentation.StationWorkWaitLater");
    public static string StationWorkWaitNow => BasicData.Text("presentation.StationWorkWaitNow");
    public static string StationButcheryConfirm => BasicData.Text("presentation.StationButcheryConfirm");
    // 확인 버튼 라벨에 표 행동 비용을 붙인다. 비용을 모르면 기본 라벨을 쓴다.
    // 맡기기 전 확인 버튼: 수수료 합 · 작업 시간.
    public static string StationButcheryConfirmCost(long _cost, long _hours) =>
        BasicData.Format("presentation.StationButcheryConfirmCost", _cost, TurnSpan(_hours));
    public static string PuzzleButcheryInsufficientFunds(long _cost) =>
        BasicData.Format("rules.butchery.insufficientFunds", _cost);
    public static string PuzzleButcheryTitle => BasicData.Text("presentation.PuzzleButcheryTitle");
    // 도축대 상태 문구의 단일 출처. 버튼·보드·hint가 같은 문구를 읽는다.
    public static string StationButcheryStatus(PuzzleButcheryPhase _phase, long _remainingHours)
    {
        return _phase switch
        {
            PuzzleButcheryPhase.Working => BasicData.Format("presentation.StationButcheryCountdown.text1", TurnSpan(_remainingHours)),
            PuzzleButcheryPhase.Ready => BasicData.Text("presentation.StationButcheryReady"),
            _ => BasicData.Text("presentation.StationButcheryIdleHint")
        };
    }
    public static string StationRefining => BasicData.Text("presentation.StationRefining");
    public static string StationRefiningConfirm => BasicData.Text("presentation.StationRefiningConfirm");
    // 확인 버튼 라벨에 표 행동 비용을 붙인다. 비용을 모르면 기본 라벨을 쓴다.
    public static string StationRefiningConfirmCost(long _cost, long _hours) =>
        BasicData.Format("presentation.StationRefiningConfirmCost", _cost, TurnSpan(_hours));
    public static string PuzzleRefiningInsufficientFunds(long _cost) =>
        BasicData.Format("rules.refining.insufficientFunds", _cost);
    public static string PuzzleRefiningTitle => BasicData.Text("presentation.PuzzleRefiningTitle");
    // 정제대 상태 문구의 단일 출처. 버튼·보드·hint가 같은 문구를 읽는다.
    public static string StationRefiningStatus(PuzzleRefiningPhase _phase, long _remainingHours)
    {
        return _phase switch
        {
            PuzzleRefiningPhase.Working => BasicData.Format("presentation.StationRefiningCountdown.text1", TurnSpan(_remainingHours)),
            PuzzleRefiningPhase.Ready => BasicData.Text("presentation.StationRefiningReady"),
            _ => BasicData.Text("presentation.StationRefiningIdleHint")
        };
    }
    public static string StationModification => BasicData.Text("presentation.StationModification");
    public static string StationModificationConfirm => BasicData.Text("presentation.StationModificationConfirm");
    // 확인 버튼 라벨에 선택 모드의 비용을 붙인다. 모드를 고르지 않았으면 기본 라벨을 쓴다.
    public static string StationModificationConfirmCost(long _cost, long _hours) =>
        BasicData.Format("presentation.StationModificationConfirmCost", _cost, TurnSpan(_hours));
    public static string PuzzleModificationInsufficientFunds(long _cost) =>
        BasicData.Format("rules.modification.insufficientFunds", _cost);
    public static string PuzzleModificationTitle => BasicData.Text("presentation.PuzzleModificationTitle");
    public static string PuzzleModificationStatus(PuzzleInventoryService _session)
    {
        if (_session == null || !_session.HasModification())
        {
            return BasicData.Text("rules.modification.unavailable");
        }
        switch (_session.ModificationPhase())
        {
            case PuzzleModificationPhase.Working:
                return BasicData.Format("presentation.StationModificationCountdown",
                    TurnSpan(_session.ModificationRemainingHours()));
            case PuzzleModificationPhase.Ready:
                return BasicData.Text("presentation.StationModificationCollect");
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
            PuzzleModificationFailure.Deadline => BasicData.Text("rules.workshop.deadline"),
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
    // 상환 목표는 현재 위치(금색)와 다른 산호색으로 구분한다.
    public static Color PuzzleMapTargetHalo => BasicData.Color("presentation.map.targetMarkerHalo");
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
    public static Color PuzzleMapTarget => BasicData.Color("presentation.map.targetMarker");
    public static Color PuzzleMapVisited => BasicData.Color("presentation.map.visited");
    public static Color PuzzleMapRare => BasicData.Color("presentation.map.rare");
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

    internal static string PuzzleMapGuide(long _elapsedHours)
    {
        return BasicData.Format("presentation.map.guide2", PuzzleMapGuideText, TurnSpan(_elapsedHours));
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

    static (string heading, string status) BuildPuzzleMapCityHeadingParts(PuzzleCityData _city, bool _visited, bool _current, bool _connected,
        PuzzleTravelResult _evaluation, PuzzleRegionData _region)
    {
        string heading = BasicData.Format("presentation.map.information", _city.displayName);
        heading += _visited
            ? BasicData.Format("presentation.map.informationText2", RegionLink(_region), _city.buyPercentOfBaseValue, _city.sellPercentOfFinalValue)
            : BasicData.Format("presentation.map.regionOnly", RegionLink(_region));
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
            status = BasicData.Format("presentation.map.informationText7", ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled), PuzzleTravelStatus(_evaluation), TurnSpan(_evaluation.TravelHours));
        }
        return (heading, status);
    }

    internal static string PuzzleMapCityHeading(PuzzleCityData _city, bool _visited, bool _current, bool _connected,
        PuzzleTravelResult _evaluation, PuzzleRegionData _region)
    {
        var (heading, status) = BuildPuzzleMapCityHeadingParts(_city, _visited, _current, _connected, _evaluation, _region);
        return BasicData.Format("presentation.map.informationText11", heading, status, string.Empty).TrimEnd('\n');
    }

    internal static string PuzzleMapInformation(PuzzleCityData _city, bool _visited, bool _current, bool _connected,
        PuzzleTravelResult _evaluation, PuzzleRegionData _region, IReadOnlyList<PuzzleInventoryService.TavernRumor> _rumors)
    {
        var (heading, status) = BuildPuzzleMapCityHeadingParts(_city, _visited, _current, _connected, _evaluation, _region);

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
            var buckets = ClassifyTagRulesBySellGrade(_city);
            string common = JoinTags(buckets.common);
            string expensive = JoinTags(buckets.expensive);
            string rare = JoinTags(buckets.rare);
            string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
            string expensiveColor = ColorUtility.ToHtmlStringRGB(TradeExpensiveColor);
            string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapRare);
            trade = BasicData.Format("presentation.map.informationText9", commonColor, (string.IsNullOrEmpty(common) ? BasicData.Text("presentation.map.informationText14") : common)) +
                BasicData.Format("presentation.map.informationText10", expensiveColor, (string.IsNullOrEmpty(expensive) ? BasicData.Text("presentation.map.informationText15") : expensive)) +
                BasicData.Format("presentation.map.informationText16", rareColor, (string.IsNullOrEmpty(rare) ? BasicData.Text("presentation.map.informationText15") : rare), RareSellPercent()) +
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
        return BasicData.Format("presentation.inventory.travelQuote", _result.Distance, TurnSpan(_result.TravelHours), PuzzleTravelCost, _result.Cost, PuzzleTravelBreakdown(_result));
    }

    public static string PuzzleMapRegionLabel(PuzzleRegionData _region)
    {
        var tags = _region.specialtyTags ?? Array.Empty<string>();
        if (tags.Length == 0)
        {
            return _region.displayName;
        }

        string specialties = string.Join(" + ", tags.OrderBy(_tag => _tag, StringComparer.Ordinal).Select(PuzzleTagLabel));
        return BasicData.Format("presentation.map.regionLabel", _region.displayName, specialties);
    }

    public static string PuzzleMapStationName(string _name, bool _planningTarget, bool _uncertain = false)
    {
        string name = _planningTarget ? _name + " ★" : _name;
        return _uncertain ? BasicData.Format("presentation.map.rumorStation", name,
            ColorUtility.ToHtmlStringRGB(PuzzleMapTarget)) : name;
    }

    // 기간·남은 시간 표기("N턴"). 게임 시간을 턴으로 올림해 쓴다. 지도·견적·툴팁·의뢰·작업장이 같은 표기를 쓴다.
    public static string TurnSpan(long _hours)
    {
        return BasicData.Format("presentation.map.time", PuzzleTimeManager.TurnsOf(_hours));
    }

    // 지도 노선 라벨(에디터 배치 도구용): 기본 운임(기본요금 + 거리 요금). 적재 요금은 화물에 따라 달라 빼고 표시한다.
    public static string PuzzleMapRouteFare(PuzzleRouteData _route, PuzzleRouteTableData _rules)
    {
        return BasicData.Format("presentation.map.routeFare", _route.cost + (long)_route.distance * _rules.costPerDistance);
    }

    // 게임 중 지도 노선 라벨: 지금 출발하면 실제로 차감되는 운임(기본요금 + 거리 요금 + 기차 적재 칸 × 칸당 요금).
    // 이동 처리(EvaluateTravel)와 같은 계산식을 쓴다.
    public static string PuzzleMapRouteFare(PuzzleRouteData _route, PuzzleRouteTableData _rules, int _trainCells)
    {
        return PuzzleInventoryService.TryTravelCost(_route.cost, _route.distance, _rules.costPerDistance,
                _trainCells, _rules.costPerCell, out long cost, out _)
            ? BasicData.Format("presentation.map.routeFare", cost)
            : PuzzleMapRouteFare(_route, _rules);
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
        return BasicData.Format("presentation.trade.cityTitle", CityLink(_city.id), RegionLink(_region));
    }

    public static Color PuzzleItemIcon(float _alpha)
    {
        return BasicData.Color("presentation.inventory.itemIcon", _alpha);
    }
    public static Color PuzzleSynergyArrow => BasicData.Color("presentation.inventory.synergyArrow");
    internal static Color PuzzleRefrigerationArrow => BasicData.Color("presentation.inventory.refrigerationArrow");
    public static string PuzzleTrainTitleText => BasicData.Text("presentation.train.trainTitle");
    internal static string PuzzleTrainTitle(PuzzleInventory _wagon)
    {
        return string.IsNullOrEmpty(_wagon.DisplayName)
            ? BasicData.Format("presentation.train.trainTitle2", _wagon.WagonNumber)
            : _wagon.DisplayName;
    }
    public static string PuzzleAddWagonLabel(long _cost)
    {
        return BasicData.Format("presentation.train.addWagonLabel", _cost);
    }
    // 전용 구역 칸 색. 구역이 받는 첫 태그의 색을 기본 칸 색에 섞는다.
    const float ZONE_TINT = 0.45f;

    internal static Color PuzzleZoneCellColor(PuzzleBoardZone _zone)
    {
        string tag = _zone.AllowedTags.Count > 0 ? _zone.AllowedTags[0] : null;
        return UnityEngine.Color.Lerp(PuzzleCell, PuzzleTagBackgroundColor(tag), ZONE_TINT);
    }

    internal static string WagonProductEffect(PuzzleWagonProductData _product)
    {
        return BasicData.Format("presentation.wagonProcess.effect", _product.description);
    }

    // 화차 가공 진행 한 줄. 묶음이 없으면 재료 대기, 자리가 없으면 산출 대기, 진행 중이면 경과/필요 턴.
    internal static string WagonProcessStatus(TrainventoryWagonProcessData _process, PuzzleWagonProcessState _state)
    {
        if (_state == null)
        {
            return BasicData.Format("presentation.wagonProcess.idle", _process.displayName);
        }
        if (_state.WaitingForSpace)
        {
            return BasicData.Format("presentation.wagonProcess.noSpace", _process.displayName);
        }
        return BasicData.Format("presentation.wagonProcess.progress", _process.displayName,
            PuzzleTimeManager.TurnsOf(_state.ElapsedHours), PuzzleTimeManager.TurnsOf(_state.RequiredHours));
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
    public static Color PuzzleOriginalSlotOutline => BasicData.Color("presentation.inventory.originalSlotOutline");
    public static Color PuzzleDragBeam => BasicData.Color("presentation.inventory.dragBeam");
    public static Color PuzzleSynergyChipBackground => BasicData.Color("presentation.inventory.synergyChipBackground");

    public static Color PuzzleTooltipHeaderBackground => BasicData.Color("presentation.tooltip.headerBackground");
    public static Color PuzzleTooltipBodyBackground => BasicData.Color("presentation.tooltip.bodyBackground");
    public static Color PuzzleTooltipDivider => BasicData.Color("presentation.tooltip.divider");
    public static Color PuzzleTooltipText => BasicData.Color("presentation.tooltip.text");
    // 기본 텍스트와 중립 색을 동일 색으로 통합한다.
    public static Color PuzzleTooltipGold => PuzzleTooltipText;
    public static Color PuzzleTooltipGreen => BasicData.Color("presentation.tooltip.green");
    public static Color PuzzleTooltipRed => BasicData.Color("presentation.tooltip.red");

    static string TooltipLabelHtml()
    {
        return ColorUtility.ToHtmlStringRGB(PuzzleTooltipText);
    }

    public static string PuzzleTooltipBaseValueLabel => BasicData.Text("presentation.tooltip.baseValueLabel");
    internal static string PuzzleTooltipPriceUnavailable => BasicData.Text("presentation.tooltip.priceUnavailable");
    internal static string PuzzleMarketNoSettlement => BasicData.Text("presentation.tooltip.marketNoSettlement");

    // \uAE30\uBCF8 \uAC00\uCE58\uB294 \uBB38\uAD6C\uC758 \uACE0\uC815 \uC704\uCE58(<pos>)\uC5D0 \uB193\uC774\uBBC0\uB85C \uD604\uC7AC \uAC00\uCE58 \uC790\uB9BF\uC218\uAC00 \uBC14\uB00C\uC5B4\uB3C4 \uC6C0\uC9C1\uC774\uC9C0 \uC54A\uB294\uB2E4.
    internal static string PuzzleBoardActualValue(decimal _actualValue, long _baseValue)
    {
        return BasicData.Format("presentation.inventory.boardActualValue", $"{_actualValue:#,0.##}$", _baseValue);
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

    static readonly Regex EFFECT_PERCENTAGE_REGEX = new(@"[+−-]\d+(?:[.,]\d+)?%");

    internal static PuzzleTooltipEffectEntry PuzzleRefrigerationEffect(int _reductionPercent)
    {
        return PuzzleTooltipSynergyEffect(PuzzlePreservationDescription(_reductionPercent, BasicData.Text));
    }

    public static string PuzzlePreservationDescription(int _reductionPercent, Func<string, string> _text)
    {
        return string.Format(_text("presentation.tooltip.refrigerationEffect"), _reductionPercent);
    }

    internal static PuzzleTooltipEffectEntry PuzzleAppliedPreservationEffect(int _reductionPercent)
    {
        string raw = BasicData.Format("presentation.tooltip.appliedPreservationEffect", _reductionPercent);
        (string label, string value) = SplitEffectPercentage(raw);
        return new PuzzleTooltipEffectEntry(
            BasicData.Format("presentation.tooltip.activeEffectLabel", TooltipLabelHtml(), label),
            FormatEffectPercentage(value));
    }

    internal static string PuzzleAnimalBreedingEffect(string _description, long _hours, string _offspringName, int _count, string _parentName)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            TurnSpan(_hours), _offspringName, _count, _parentName));
    }

    internal static string PuzzleAnimalGrowthEffect(string _description, long _hours, string _adultName, long _blockedHours)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            TurnSpan(_hours), _adultName, TurnSpan(_blockedHours)));
    }

    internal static string PuzzleAnimalWasteEffect(string _description, int _intervalHours, string _wasteName, int _count)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            TurnSpan((long)_intervalHours), _wasteName, _count));
    }

    static string PuzzleAnimalFeedEffect(string _description, int _intervalHours, int _reductionHours)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            TurnSpan((long)_intervalHours),
            TurnSpan((long)_reductionHours)));
    }

    internal static string PuzzleCombinationEffect(string _description, long _hours, string _resultName)
    {
        return PuzzleTooltipEffectDescription(string.Format(_description,
            TurnSpan(_hours), _resultName));
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

    internal static PuzzleTooltipEffectEntry PuzzleTooltipSynergyEffect(string _description)
    {
        (string label, string value) = SplitEffectPercentage(_description);
        string formattedLabel = BasicData.Format("presentation.tooltip.effectDescription",
            TooltipLabelHtml(), ShrinkParentheticalNotes(label));
        return new PuzzleTooltipEffectEntry(formattedLabel, FormatEffectPercentage(value));
    }

    internal static string PuzzleTooltipEffectDescription(string _description)
    {
        string highlighted = ShrinkParentheticalNotes(HighlightEffectPercentages(_description));
        return BasicData.Format("presentation.tooltip.effectDescription", TooltipLabelHtml(), highlighted);
    }

    static (string Label, string Value) SplitEffectPercentage(string _text)
    {
        if (string.IsNullOrEmpty(_text))
        {
            return (string.Empty, string.Empty);
        }

        Match match = EFFECT_PERCENTAGE_REGEX.Match(_text);
        if (!match.Success)
        {
            return (_text, string.Empty);
        }

        string prefix = _text[..match.Index].TrimEnd(' ');
        string suffix = _text[(match.Index + match.Length)..];
        if (prefix.Length == 0)
        {
            suffix = suffix.TrimStart(' ');
        }
        return (prefix + suffix, match.Value);
    }

    static string FormatEffectPercentage(string _percentage)
    {
        if (string.IsNullOrEmpty(_percentage))
        {
            return string.Empty;
        }

        Color color = _percentage[0] == '+' ? PuzzleTooltipGreen : PuzzleTooltipRed;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{_percentage}</color>";
    }

    static string ShrinkParentheticalNotes(string _text)
    {
        return Regex.Replace(_text, @"\([^)]*\)", match => $"<size=70%>{match.Value}</size>");
    }

    static string HighlightEffectPercentages(string _description)
    {
        return EFFECT_PERCENTAGE_REGEX.Replace(_description, match => FormatEffectPercentage(match.Value));
    }

    // 일반 효과 한 행. 진행도가 있으면 설명 바로 아래에 녹색으로 놓고, 공간을 막혀 있으면 같은 줄에 빨간 표시를 덧붙인다.
    internal static string PuzzleTooltipGeneralEffect(PuzzleEffectDisplay _effect)
    {
        if (_effect.Progress is not PuzzleProgressDisplay progress)
        {
            return _effect.Description;
        }

        string line = BasicData.Format("presentation.tooltip.effectProgress",
            ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), TurnSpan(progress.ElapsedHours),
            TurnSpan(progress.TotalHours));
        if (progress.SpaceBlocked)
        {
            line += " " + BasicData.Format("presentation.tooltip.effectSpaceBlocked",
                ColorUtility.ToHtmlStringRGB(PuzzleTooltipRed));
        }
        return _effect.Description + "\n" + line;
    }

    // 화물·품목 링크 툴팁 공통: 일반 효과와 가능 인접 효과를 기존 시너지 규칙(PuzzlePieceEffects) 기준으로 수집한다.
    internal static (IReadOnlyList<string> General, IReadOnlyList<PuzzleTooltipEffectEntry> Synergies)
        PuzzleTooltipPossibleEffects(PuzzleItemDefinition _item, PuzzleSynergyScope _scope,
            PuzzlePlacement _placement = null, PuzzleInventoryService _session = null)
    {
        if (_item == null)
        {
            return default;
        }

        bool isTrain = _scope == PuzzleSynergyScope.Train;
        var general = new List<PuzzleEffectDisplay>();
        var synergies = new List<(bool isTarget, bool isPenalty, PuzzleTooltipEffectEntry entry)>();
        var tables = TableManager.Instance;
        int preservation = PuzzleRefrigeration.SourceReductionPercent(_item, tables.PuzzleSynergy.freshness);
        if (preservation > 0)
        {
            synergies.Add((false, false, PuzzleRefrigerationEffect(preservation)));
        }

        var animals = tables.PuzzleSynergy.animals;
        if (_item.Tags.Contains(animals.animalTag))
        {
            if (!_item.IsBaby && tables.TryGetAnimalBreeding(_item.Id, out var breeding))
            {
                var offspring = tables.Piece(breeding.offspringPieceId);
                long breedingHours = (long)animals.breedingHours;
                // 실제 선택된 번식 쌍에 속한 동물에만 진행도를 표시한다.
                PuzzleProgressDisplay? breedingProgress = null;
                if (isTrain && _placement != null && _session != null
                    && _session.TryGetBreedingProgress(_placement, out long pairedHours, out bool pairedBlocked))
                {
                    breedingProgress = new PuzzleProgressDisplay(pairedHours, breedingHours, pairedBlocked);
                }
                general.Add(new PuzzleEffectDisplay(PuzzleAnimalBreedingEffect(breeding.description,
                    breedingHours, offspring.displayName, breeding.offspringCount, _item.DisplayName), breedingProgress));
                general.Add(new PuzzleEffectDisplay(PuzzleAnimalFeedEffect(animals.breedingFeedDescription,
                    animals.feedIntervalHours, animals.breedingFeedReductionHours)));
            }
            if (_item.GrowthHours > 0)
            {
                var adult = tables.Piece(_item.AdultPieceId);
                // 성장 시계는 객차의 소유 물건에만 있다. 시장·도축·개조 보드에는 진행도를 보여주지 않는다.
                PuzzleProgressDisplay? growthProgress = isTrain && _placement != null && _placement.GrowthElapsedHours >= 0
                    ? new PuzzleProgressDisplay(_placement.GrowthElapsedHours, _item.GrowthHours,
                        _placement.GrowthSpaceBlocked)
                    : null;
                general.Add(new PuzzleEffectDisplay(PuzzleAnimalGrowthEffect(_item.GrowthDescription,
                    _item.GrowthHours, adult.displayName, _item.GrowthBlockedHours), growthProgress));
                general.Add(new PuzzleEffectDisplay(PuzzleAnimalFeedEffect(animals.growthFeedDescription,
                    animals.feedIntervalHours, animals.growthFeedReductionHours)));
            }

            var waste = tables.Piece(animals.wastePieceId);
            // 배설은 달력 경계 기반으로 흐르므로 진행도를 붙이지 않는다.
            general.Add(new PuzzleEffectDisplay(PuzzleAnimalWasteEffect(animals.wasteDescription,
                animals.wasteIntervalHours, waste.displayName, animals.wasteCountPerAnimal)));
        }

        // 인접 조합은 입력으로 등록된 품목 양쪽에 같은 규칙 설명을 보인다. 진행도는 객차에서 실제로
        // 맺힌 쌍의 설명에만 붙이고, 결과 품목은 입력 목록에 없으므로 설명이 붙지 않는다.
        foreach (var combination in tables.Combinations)
        {
            bool isInput = string.Equals(combination.sourcePieceId, _item.Id, StringComparison.Ordinal) ||
                string.Equals(combination.partnerPieceId, _item.Id, StringComparison.Ordinal);
            if (!isInput)
            {
                continue;
            }

            var product = tables.Piece(combination.resultPieceId);
            long combinationHours = (long)combination.requiredHours;
            PuzzleProgressDisplay? combinationProgress = null;
            if (isTrain && _placement != null && _session != null
                && _session.TryGetCombinationProgress(_placement, out var activeCombination,
                    out long combinationElapsed) && ReferenceEquals(activeCombination, combination))
            {
                combinationProgress = new PuzzleProgressDisplay(combinationElapsed, combinationHours, false);
            }

            general.Add(new PuzzleEffectDisplay(PuzzleCombinationEffect(combination.description,
                combinationHours, product.displayName), combinationProgress));
        }


        foreach (var rule in tables.SynergiesFor(_scope))
        {
            bool isPenalty = rule.multiplierPercent < 100;
            if (PuzzlePieceEffects.IsSource(_item, rule))
            {
                synergies.Add((false, isPenalty, PuzzleTooltipSynergyEffect(rule.sourceDescription)));
            }
            if (PuzzlePieceEffects.IsTarget(_item, rule, animals.wasteTag))
            {
                synergies.Add((true, isPenalty, PuzzleTooltipSynergyEffect(rule.targetDescription)));
            }
        }

        var orderedSynergies = synergies
            .OrderBy(_entry => _entry.isTarget)
            .ThenBy(_entry => _entry.isPenalty)
            .Select(_entry => _entry.entry)
            .ToList();
        return (general.Select(_effect => PuzzleTooltipGeneralEffect(_effect)).ToList(), orderedSynergies);
    }

    internal static string PuzzleTooltipNoEffect()
    {
        return BasicData.Format("presentation.tooltip.noEffect", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFutureValueLabel(long _hours, string _cityName, long _activations)
    {
        return BasicData.Format("presentation.tooltip.futureValueLabel", TooltipLabelHtml(), _cityName, TurnSpan(_hours), _activations);
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

    internal static string PuzzleTooltipShelfLifeValue(long _hours, int _freshness)
    {
        string color = ColorUtility.ToHtmlStringRGB(PuzzleFreshnessColor(_freshness));
        if (_hours == long.MaxValue)
        {
            return BasicData.Format("presentation.tooltip.shelfLifePaused", color);
        }
        return BasicData.Format("presentation.tooltip.shelfLifeValue", color, PuzzleTimeManager.TurnsOf(_hours));
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

    // 태그 행을 판매 등급의 단일 기준으로 흔함/귀함/희귀로 나눈다. 등급이 없거나 일반(100%)이면 어느 목록에도 넣지 않는다.
    static (List<string> common, List<string> expensive, List<string> rare) ClassifyTagRulesBySellGrade(PuzzleCityData _city)
    {
        var common = new List<string>();
        var expensive = new List<string>();
        var rare = new List<string>();
        foreach (var rule in _city?.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>())
        {
            if (rule == null)
            {
                continue;
            }
            switch (TableManager.SellGradeKindOf(rule))
            {
                case PuzzleSellGradeKind.Common:
                    common.Add(rule.tag);
                    break;
                case PuzzleSellGradeKind.Expensive:
                    expensive.Add(rule.tag);
                    break;
                case PuzzleSellGradeKind.Rare:
                    rare.Add(rule.tag);
                    break;
            }
        }
        return (common, expensive, rare);
    }

    static string JoinTags(IEnumerable<string> _tags)
    {
        return string.Join(" · ", _tags.OrderBy(_tag => _tag, StringComparer.Ordinal).Select(PuzzleTagLabel));
    }

    // 도시의 태그 규칙을 판매 등급 기준으로 구조화해 반환한다. 지도/상점의 배지 뷰가 공용으로 사용한다.
    internal static PuzzleCityTradePreferencesData GetCityTradePreferences(PuzzleCityData _city)
    {
        if (_city == null)
        {
            return default;
        }
        var buckets = ClassifyTagRulesBySellGrade(_city);
        bool hasWaste = string.Equals(_city.regionId, "region.farm", StringComparison.Ordinal);
        IReadOnlyList<string> wasteTags = hasWaste ? new[] { "Waste" } : Array.Empty<string>();
        return new PuzzleCityTradePreferencesData(
            PrioritizeTags(buckets.common),
            PrioritizeTags(buckets.expensive),
            PrioritizeTags(buckets.rare),
            wasteTags,
            hasWaste,
            _city.buyPercentOfBaseValue,
            _city.sellPercentOfFinalValue);
    }

    public static Color TradeExpensiveColor => BasicData.Color("presentation.trade.expensiveResourceColor");

    internal static void CityTradeGradeInfo(PuzzleCityTradeGrade _grade, bool _withLink,
        out string _formattedLabel, out string _linkId, out Color _color, bool _bold = true)
    {
        string rawTitle;
        switch (_grade)
        {
            case PuzzleCityTradeGrade.Common:
                rawTitle = BasicData.Text("presentation.trade.commonResourceTitle");
                _linkId = "common";
                _color = PuzzleMapVisited;
                break;
            case PuzzleCityTradeGrade.Expensive:
                rawTitle = BasicData.Text("presentation.trade.expensiveResourceTitle");
                _linkId = "expensive";
                _color = TradeExpensiveColor;
                break;
            case PuzzleCityTradeGrade.Rare:
                rawTitle = BasicData.Format("presentation.trade.rareResourceTitle", RareSellPercent());
                _linkId = "rare";
                _color = PuzzleMapRare;
                break;
            case PuzzleCityTradeGrade.Waste:
                rawTitle = BasicData.Text("presentation.trade.wastePurchaseTitle");
                _linkId = "waste";
                _color = PuzzleTooltipGreen;
                break;
            default:
                rawTitle = string.Empty;
                _linkId = string.Empty;
                _color = UnityEngine.Color.white;
                break;
        }

        string content = _withLink && !string.IsNullOrEmpty(_linkId)
            ? $"<link=\"{_linkId}\">{rawTitle}</link>"
            : rawTitle;
        string inner = _bold ? $"<b>{content}</b>" : content;
        _formattedLabel = $"<color=#{ColorUtility.ToHtmlStringRGB(_color)}>{inner}</color>";
    }

    internal static string CityTradeWasteSuffix => "(+10$)";

    const string REGION_PRICE_LINK_PREFIX = "regionPrice:";

    internal static string CityTradeRegionPriceText(int _buyPercent, int _sellPercent, bool _withLink = false, bool _bold = true)
    {
        bool samePercent = _buyPercent == _sellPercent;
        string rawText = samePercent
            ? BasicData.Format("presentation.trade.regionPriceTitle", _buyPercent)
            : BasicData.Format("presentation.trade.regionPrice", _buyPercent, _sellPercent).TrimStart('\n');

        string content = _withLink
            ? $"<link=\"{REGION_PRICE_LINK_PREFIX}{_buyPercent}:{_sellPercent}\">{rawText}</link>"
            : rawText;

        string inner = _bold ? $"<b>{content}</b>" : content;
        if (_buyPercent != 100 || _sellPercent != 100)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipGold)}>{inner}</color>";
        }
        return inner;
    }

    // 도시의 태그 규칙을 판매 등급 기준으로 흔함/귀함/희귀로 묶는다. 임의 배율이나 일반(100%)은 표시하지 않는다.
    internal static string PuzzleCityTagPreferences(PuzzleCityData _city)
    {
        var buckets = ClassifyTagRulesBySellGrade(_city);
        string commonText = JoinTags(buckets.common);
        string expensiveText = JoinTags(buckets.expensive);
        string rareText = JoinTags(buckets.rare);
        string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
        string expensiveColor = ColorUtility.ToHtmlStringRGB(TradeExpensiveColor);
        string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapRare);
        return BasicData.Format("presentation.trade.cityTagPreferences", commonColor, (string.IsNullOrEmpty(commonText) ? BasicData.Text("presentation.trade.cityTagPreferencesText3") : commonText)) +
            BasicData.Format("presentation.trade.cityTagPreferencesText2", expensiveColor, (string.IsNullOrEmpty(expensiveText) ? BasicData.Text("presentation.trade.cityTagPreferencesText4") : expensiveText)) +
            BasicData.Format("presentation.trade.cityTagPreferencesText5", rareColor, (string.IsNullOrEmpty(rareText) ? BasicData.Text("presentation.trade.cityTagPreferencesText4") : rareText), RareSellPercent()) +
            FarmWastePurchaseLine("presentation.trade.wastePurchase", _city?.regionId) +
            BasicData.Format("presentation.trade.regionPrice", _city.buyPercentOfBaseValue, _city.sellPercentOfFinalValue);

    }

    static string FarmWastePurchaseLine(string _textKey, string _regionId)
    {
        if (!string.Equals(_regionId, "region.farm", StringComparison.Ordinal))
        {
            return string.Empty;
        }
        return BasicData.Format(_textKey, ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), PuzzleTagLabel("Waste"));
    }

    internal static void BuildTownTooltipRumorLines(string _cityId, PuzzleInventoryService _session,
        List<string> _lines, out string _header)
    {
        _lines.Clear();
        _header = null;
        if (_session?.TavernRumors == null || (_session.HasVisitedCity(_cityId)))
        {
            return;
        }
        foreach (var rumor in _session.TavernRumors)
        {
            if (!string.Equals(rumor.TargetCityId, _cityId, StringComparison.Ordinal))
            {
                continue;
            }
            _header ??= SummaryRumorTitle;
            string plain = Regex.Replace(TavernRumorMessage(rumor), @"</?link(?:=[^>]*)?>", string.Empty);
            _lines.Add($"• {plain}");
        }
    }

    internal static bool TryGetTradeKeywordTooltip(string _linkId, out string _title, out string _description)
    {
        if (!string.IsNullOrEmpty(_linkId) &&
            _linkId.StartsWith(REGION_PRICE_LINK_PREFIX, StringComparison.Ordinal))
        {
            string[] parts = _linkId.Substring(REGION_PRICE_LINK_PREFIX.Length).Split(':');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out int buyPercent) &&
                int.TryParse(parts[1], out int sellPercent))
            {
                _title = buyPercent == sellPercent
                    ? BasicData.Format("presentation.trade.regionPriceTitle", buyPercent)
                    : BasicData.Format("presentation.trade.regionPrice", buyPercent, sellPercent).TrimStart('\n');
                _description = FormatKeywordTooltipDescription(BasicData.Text("presentation.trade.regionPriceTooltip"));
                return true;
            }
        }

        switch (_linkId)
        {
            case "common":
                _title = BasicData.Text("presentation.trade.commonResourceTitle");
                _description = FormatKeywordTooltipDescription(BasicData.Text("presentation.trade.commonResourceTooltip"));
                return true;
            case "expensive":
                _title = BasicData.Text("presentation.trade.expensiveResourceTitle");
                _description = FormatKeywordTooltipDescription(BasicData.Text("presentation.trade.expensiveResourceTooltip"));
                return true;
            case "rare":
                _title = BasicData.Format("presentation.trade.rareResourceTitle", RareSellPercent());
                _description = FormatKeywordTooltipDescription(BasicData.Format("presentation.trade.rareResourceTooltip", RareSellPercent() - PuzzleInventoryValuation.BASE_PERCENT));
                return true;
            case "waste":
                _title = BasicData.Text("presentation.trade.wastePurchaseTitle");
                _description = FormatKeywordTooltipDescription(BasicData.Text("presentation.trade.wastePurchaseTooltip"));
                return true;
            default:
                _title = null;
                _description = null;
                return false;
        }
    }

    static int RareSellPercent()
    {
        var tables = TableManager.Instance.RequireValid();
        if (!tables.TryGetSellGrade(TableManager.SELL_GRADE_RARE, out var grade))
        {
            throw new InvalidOperationException("희귀 판매 등급이 필요합니다.");
        }
        return grade.sellPercent;
    }

    static string FormatKeywordTooltipDescription(string _description)
    {
        if (string.IsNullOrEmpty(_description))
        {
            return string.Empty;
        }

        string highlighted = Regex.Replace(_description, @"[+−-]\d+(?:[.,]\d+)?([%$])", match =>
        {
            Color color = match.Groups[1].Value == "$" ? PuzzleTooltipGreen : PuzzleTooltipGold;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{match.Value}</color>";
        });
        highlighted = ShrinkParentheticalNotes(highlighted);
        return $"<color=#{TooltipLabelHtml()}>{highlighted}</color>";
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
            BasicData.Format("presentation.synergyChart.rules", TurnSpan(_intervalHours)),
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
            "waste" => BasicData.Color("presentation.tags.wasteBackground"),
            "ore" => BasicData.Color("presentation.tags.oreBackground"),
            "grain" => BasicData.Color("presentation.tags.grainBackground"),         // 폐기물: 다크 브라운
            _ => BasicData.Color("presentation.tags.puzzleTagBackgroundColorColor")
        };
    }

    public static Color PuzzleTagTextColor(string _tag)
    {
        if (TableManager.Instance.Puzzle.tagPresentations.Any(_entry => _entry.id == _tag))
        {
            return PuzzleTagTextOnBackground(PuzzleTagBackgroundColor(_tag));
        }
        return IsQuestTag(_tag) || IsFreshTag(_tag) ? PuzzleTooltipBodyBackground : PuzzleTooltipText;
    }

    public static Color PuzzleTagTextOnBackground(Color _background)
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

    static bool IsQuestTag(string _tag)
    {
        return string.Equals(_tag, "Quest", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsFreshTag(string _tag)
    {
        return string.Equals(_tag, "Fresh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_tag, "Refrigerator", StringComparison.OrdinalIgnoreCase);
    }

    public static Color PuzzleTooltipTagTextColor(string _tag)
    {
        return PuzzleTagTextColor(_tag);
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

    internal static Color32 PuzzleObjectiveBackground => new Color32(22, 27, 43, 255);
    internal static Color32 PuzzleObjectiveBackdrop => new Color32(0, 0, 0, 140);
    internal static Color32 PuzzleObjectiveSurface => new Color32(28, 33, 42, 255);
    internal static string PuzzleObjectiveConfirm => BasicData.Text("presentation.inventory.travelConfirm");

    // 시작 안내: 목표 잔액과 영업 기간(일).
    internal static string PuzzleObjectiveHint(long _deadlineHours)
    {
        return BasicData.Format("presentation.inventory.objectiveHint", _deadlineHours / PuzzleTimeManager.HOURS_PER_DAY);
    }

    // 결산 버튼. 목표 잔액을 함께 보인다.
    public static string PuzzleClearButton(long _targetFunds)
    {
        return BasicData.Format("presentation.inventory.clearButton", _targetFunds);
    }

    internal static string PuzzleEndlessContinue => BasicData.Text("presentation.inventory.endlessContinue");
    internal static string PuzzleEndlessLabel => BasicData.Text("presentation.inventory.endlessLabel");
    internal static string ResetWindowLayouts => BasicData.Text("presentation.inventory.resetWindowLayouts");
    internal static string RunEndFundsLabel => BasicData.Text("presentation.inventory.runEndFundsLabel");
    internal static string RunEndGoalLabel => BasicData.Text("presentation.inventory.runEndGoalLabel");
    internal static string RunEndDiffLabel => BasicData.Text("presentation.inventory.runEndDiffLabel");
    internal static string RunEndTurnsLabel => BasicData.Text("presentation.inventory.runEndTurnsLabel");
    internal static string RunEndQuestsLabel => BasicData.Text("presentation.inventory.runEndQuestsLabel");
    internal static string RunEndRestart => BasicData.Text("presentation.inventory.runEndRestart");
    internal static string RunEndQuit => BasicData.Text("presentation.inventory.runEndQuit");
    internal static string TopHudPause => BasicData.Text("presentation.inventory.topHudPause");
    internal static string QuickToolbarNotebook => BasicData.Text("presentation.inventory.qtNotebook");
    internal static string QuickToolbarMap => BasicData.Text("presentation.inventory.qtMap");
    internal static string QuickToolbarBackgroundOnly => BasicData.Text("presentation.inventory.qtBackgroundOnly");
    internal static string QuickToolbarShortcutHint => BasicData.Text("presentation.inventory.qtShortcutHint");
    internal static string BuildingMarket => BasicData.Text("presentation.inventory.bldgMarket");
    internal static string BuildingTavern => BasicData.Text("presentation.inventory.bldgTavern");
    internal static string BuildingRest => BasicData.Text("presentation.inventory.bldgRest");
    internal static string BuildingStation => BasicData.Text("presentation.inventory.bldgStation");
    internal static string BuildingWorkshop => BasicData.Text("presentation.inventory.bldgWorkshop");
    internal static string BuildingCityHall => BasicData.Text("presentation.inventory.bldgCityHall");
    internal static string BuildingCityHallPlaceholder => BasicData.Text("presentation.inventory.bldgCityHallPlaceholder");
    internal static string BuildingWorkshopEmpty => BasicData.Text("presentation.inventory.bldgWorkshopEmpty");
    // 마을 칠판의 짧은 공터 표시. BasicData 키를 추가하면 계약 재생성이 필요해 상수로 둔다.
    internal static string BuildingWorkshopNoneLabel => BasicData.Text("presentation.inventory.bldgWorkshopNoneLabel");
    internal static string TrainWindowLensIdle => BasicData.Text("presentation.inventory.trainWindowLensIdle");
    internal static string NotebookTabMap => BasicData.Text("presentation.inventory.notebookTabMap");
    internal static string NotebookTitle => BasicData.Text("presentation.inventory.notebookTitle");

    internal static string NotebookMapPinVisited(int _sellPercent, long? _quote, long? _fare)
    {
        if (_quote.HasValue)
        {
            return _fare.HasValue
                ? BasicData.Format("presentation.inventory.notebookMapPinVisited", _sellPercent, _quote.Value, _fare.Value)
                : BasicData.Format("presentation.inventory.notebookMapPinVisitedNoFare", _sellPercent, _quote.Value);
        }
        return _fare.HasValue
            ? BasicData.Format("presentation.inventory.notebookMapPinNoQuote", _sellPercent, _fare.Value)
            : BasicData.Format("presentation.inventory.notebookMapPinNoQuoteNoFare", _sellPercent);
    }

    internal static string NotebookMapPinOverflow(int _count)
    {
        return BasicData.Format("presentation.inventory.notebookMapPinOverflow", _count);
    }

    internal static string NotebookMapPinRumor(string _rumorMessage, long? _fare)
    {
        string text = string.IsNullOrEmpty(_rumorMessage) ? BasicData.Text("presentation.inventory.routeLensRumorFallback") : _rumorMessage;
        return _fare.HasValue
            ? BasicData.Format("presentation.inventory.notebookMapPinRumor", text, _fare.Value)
            : text;
    }

    internal static string RailWagonLabel(int _number, int _usedCells, int _totalCells, long _cargoValue)
    {
        return BasicData.Format("presentation.inventory.railWagonLabel", _number, _usedCells, _totalCells, _cargoValue);
    }

    internal static string TrainWindowTitle(PuzzleInventory _wagon)
    {
        string wagonName = !string.IsNullOrEmpty(_wagon.WagonProduct?.displayName)
            ? _wagon.WagonProduct.displayName
            : (!string.IsNullOrEmpty(_wagon.DisplayName)
                ? _wagon.DisplayName
                : BasicData.Text("presentation.inventory.trainWindowDefaultName"));
        return BasicData.Format("presentation.inventory.trainWindowTitle",
            _wagon.WagonNumber, wagonName, _wagon.OccupiedCells, _wagon.UsableCellCount);
    }

    internal static string PlatformSignTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.platformSignTitle", _cityName);

    public static string MarketWindowTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.marketWindowTitle", _cityName);
    public static string MarketWindowHint => BasicData.Text("presentation.inventory.marketWindowHint");
    public static string MarketWindowRestock => BasicData.Text("presentation.inventory.marketWindowRestock");
    public static string MarketItemPrice(long _price) =>
        BasicData.Format("presentation.inventory.marketItemPrice", _price);
    public static string LensWindowTitle => BasicData.Text("presentation.inventory.lensWindowTitle");
    public static string QuickToolbarLens => BasicData.Text("presentation.inventory.quickToolbarLens");
    public static string TrainWindowLensButton => BasicData.Text("presentation.inventory.trainWindowLensButton");
    public static string LensWindowEmpty => BasicData.Text("presentation.inventory.lensWindowEmpty");
    public static string QuestDropNotQuestCargo => BasicData.Text("presentation.inventory.questDropNotQuestCargo");
    public static string QuestDropWrongCity(string _cityName) =>
        BasicData.Format("presentation.inventory.questDropWrongCity", _cityName);
    public static string QuestCompleteLabel => BasicData.Text("presentation.inventory.questCompleteLabel");
    public static string QuestDeliverableOffer(string _cargoName, long _reward) =>
        BasicData.Format("presentation.inventory.questDeliverableOffer", _cargoName, _reward);
    public static string TavernWindowTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.tavernWindowTitle", _cityName);
    public static string TavernWindowRumorButton => BasicData.Text("presentation.inventory.tavernWindowRumorButton");
    public static string TavernWindowRumorEmpty => BasicData.Text("presentation.inventory.tavernWindowRumorEmpty");
    public static string TavernWindowBoardHint => BasicData.Text("presentation.inventory.tavernWindowBoardHint");
    public static string RestWindowTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.restWindowTitle", _cityName);
    public static string RestWindowHint => BasicData.Text("presentation.inventory.restWindowHint");
    public static string StationHallWindowTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.stationHallWindowTitle", _cityName);
    public static string StationHallSaleDrop => BasicData.Text("presentation.inventory.stationHallSaleDrop");
    public static string StationHallDepartButton => BasicData.Text("presentation.inventory.stationHallDepartButton");
    public static string WorkshopWindowTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.workshopWindowTitle", _cityName);
    public static string WorkshopWindowEmptyLot => BasicData.Text("presentation.inventory.workshopWindowEmptyLot");
    public static string CityHallWindowTitle(string _cityName) =>
        BasicData.Format("presentation.inventory.cityHallWindowTitle", _cityName);
    public static string CityHallIndustryFormat(string _industry) =>
        BasicData.Format("presentation.inventory.cityHallIndustryFormat", _industry);
    public static string CityHallReputationFormat(int _rep, int _maxQuests) =>
        BasicData.Format("presentation.inventory.cityHallReputationFormat", _rep, _maxQuests);
    public static string CityHallGoalFormat(long _funds, long _target, long _remainingTurns) =>
        BasicData.Format("presentation.inventory.cityHallGoalFormat", _funds, _target, _remainingTurns);
    public static string TravelBannerDepart(string _dest, long _fare, long _turns) =>
        BasicData.Format("presentation.inventory.travelBannerDepart", _dest, _fare, _turns);
    public static string TravelBannerConfirm => BasicData.Text("presentation.inventory.travelBannerConfirm");
    public static string TravelBannerCancel => BasicData.Text("presentation.inventory.travelBannerCancel");
    public static string TravelBannerInProgress(string _from, string _to) =>
        BasicData.Format("presentation.inventory.travelBannerInProgress", _from, _to);
    public static string BldgStatusMarketOpen(int _pricePercent, int _prefCount) =>
        BasicData.Format("presentation.inventory.bldgStatusMarketOpen", _pricePercent, _prefCount);
    public static string BldgStatusMarketClosed =>
        BasicData.Text("presentation.inventory.bldgStatusMarketClosed");
    public static string BldgStatusTavernOffers(int _offerCount) =>
        BasicData.Format("presentation.inventory.bldgStatusTavernOffers", _offerCount);
    public static string BldgStatusTavernDeliverable(int _deliverableCount) =>
        BasicData.Format("presentation.inventory.bldgStatusTavernDeliverable", _deliverableCount);
    public static string BldgStatusRest =>
        BasicData.Text("presentation.inventory.bldgStatusRest");
    public static string BldgStatusStation(int _wagonCount, int _maxWagons) =>
        BasicData.Format("presentation.inventory.bldgStatusStation", _wagonCount, _maxWagons);
    public static string BldgStatusWorkshop(string _title, long _fee) =>
        BasicData.Format("presentation.inventory.bldgStatusWorkshop", _title, _fee);
    public static string BldgStatusCityHall(int _reputation, int _goalPercent) =>
        BasicData.Format("presentation.inventory.bldgStatusCityHall", _reputation, _goalPercent);

    internal static string RelayoutToggle(bool _enabled)
    {
        return BasicData.Text(_enabled ? "presentation.inventory.relayoutToggleOn" : "presentation.inventory.relayoutToggleOff");
    }

    internal static string RunEndMoney(long _amount)
    {
        return BasicData.Format("presentation.inventory.runEndValueMoney", _amount);
    }

    internal static string RunEndSignedMoney(long _amount)
    {
        return BasicData.Format("presentation.inventory.runEndValueSignedMoney", _amount);
    }

    internal static string RunEndTurns(long _turns)
    {
        return BasicData.Format("presentation.inventory.runEndValueTurns", _turns);
    }

    internal static string RunEndQuests(int _count)
    {
        return BasicData.Format("presentation.inventory.runEndValueQuests", _count);
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
            PuzzleTravelFailure.TimeUp => BasicData.Text("presentation.inventory.travelTimeUp"),
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

        string direction = _result.Direction == PuzzleTradeDirection.Buy ? BasicData.Text("presentation.trade.tradeStatusText6")
            : _result.PurchaseCancel ? BasicData.Text("presentation.trade.purchaseCancel")
            : BasicData.Text("presentation.trade.tradeStatusText7");
        string quote = BasicData.Format("presentation.trade.tradeStatus", ItemLink(_item.Id, _item.DisplayName), direction);
        if (_result.Placement.Failure != PuzzlePlacementFailure.StaleSource &&
            _result.TradeFailure != PuzzleTradeFailure.PriceOverflow)
        {
            quote += BasicData.Format("presentation.trade.tradeStatusText2", _result.Price);
        }
        if (_result.CanDrop)
        {
            return _committed
                ? BasicData.Format("presentation.trade.tradeStatusText3", ItemLink(_item.Id, _item.DisplayName), direction, _result.Price)
                : BasicData.Format("presentation.trade.tradeStatusText4", ItemLink(_item.Id, _item.DisplayName), direction, _result.Price);
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
                PuzzlePlacementFailure.ZoneRestricted => BasicData.Text("presentation.trade.zoneRestricted"),
                PuzzlePlacementFailure.NoSpace => BasicData.Text("presentation.trade.tradeStatusText15"),
                _ => BasicData.Text("presentation.trade.tradeStatusText16")
            }
        };
        return BasicData.Format("presentation.trade.tradeStatusText5", quote, reason);
    }

    // 효과 품목 전용 테두리. 냉장(신선 보존 효과) 품목은 하늘색, 배설물(Waste) 품목은 갈색이며 나머지는 기본 테두리다.
    // 2026-11-19: 효과 표시 기준을 정리하는 동안 아이템 테두리 색을 기본 색으로 통일하기 위해 호출을 주석 처리했다.
    // 효과별 테두리 색을 다시 사용할 수 있도록 원본 메서드는 보존한다.
    internal static Color? PuzzlePieceAccentOutline(PuzzleItemDefinition _item, float _alpha)
    {
        if (_item == null)
        {
            return null;
        }
        if (_item.HasPreservationEffect)
        {
            return BasicData.Color("presentation.inventory.refrigerationOutline", _alpha);
        }
        string wasteTag = TableManager.Instance.PuzzleSynergy?.animals?.wasteTag;
        if (PuzzlePieceEffects.HasAnyTag(_item.Tags, wasteTag))
        {
            return BasicData.Color("presentation.inventory.wasteOutline", _alpha);
        }
        return null;
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
