using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 색과 설명 문자열의 단일 출처.
// 선택지 카드·정보 패널·슬롯 핍·장식 오브젝트가 모두 여기를 읽고, 개별 UI가 자기 색과 문구를 가지지 않는다.
// 설명 원문은 테이블의 description이 원본이고, 이 파일은 정렬·강조 같은 표기만 덧붙인다.
public enum EffectCategory
{
    Offense, Mobility, Defense, Utility, BaseStat
}

public static class Presentation
{
    // 날짜 표기 도입 시 시간 데이터와 분리해 이 표시 경로만 교체한다.
    internal static string PuzzleTime(PuzzleTimeManager _time)
    {
        return $"Day {_time.Day} · {_time.Hour:00}:00";
    }

    public static Color PuzzleTimeAccent => new(1f, 0.82f, 0.42f);

    public const string PUZZLE_MAP_TITLE = "교역 노선도";
    public const string PUZZLE_MAP_PANEL_NAME = "PuzzleWorldMap";
    public const string PUZZLE_MAP_OPEN = "지도";
    public const string PUZZLE_MAP_CLOSE = "닫기";
    public const string PUZZLE_MAP_GUIDE = "연결된 마을을 클릭해 이동합니다. 선이 교차해도 마을이 없으면 환승할 수 없습니다.";
    public const string PUZZLE_MAP_HINT = "마을을 가리켜 거리·비용과 수집한 교역 정보를 확인하세요.";
    public static Color PuzzleMapCurrent => new(1f, 0.78f, 0.30f);
    public static Color PuzzleMapVisited => new(0.34f, 0.86f, 0.72f);
    public static Color PuzzleMapUnknown => new(0.40f, 0.46f, 0.55f);
    public static Color PuzzleMapRoute => new(0.30f, 0.68f, 0.82f);
    public static Color PuzzleMapConnected => new(0.39f, 0.91f, 0.67f);
    public static Color PuzzleMapConnectedDisabled => new(0.85f, 0.45f, 0.45f);

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

    internal static string PuzzleMapGuide()
    {
        return $"{PUZZLE_MAP_GUIDE}\n굵은 선: 고속선 · 가는 선: 일반선 · 시간 가중치 1 = {PuzzleTimeManager.HOURS_PER_TRAVEL_WEIGHT}시간 · 마을을 가리켜 거리·비용 확인";
    }

    internal static string PuzzleMapNodeLabel(string _name, bool _visited, bool _current, bool _connected, bool _canAfford)
    {
        if (_current)
        {
            return _name + "\n현재 위치";
        }
        if (_connected)
        {
            return _name + (_canAfford ? "\n이동 가능" : "\n자금 부족");
        }
        return _name + (_visited ? "\n방문 완료" : "\n미방문");
    }

    internal static string PuzzleMapInformation(PuzzleCityData _city, bool _visited, bool _current, bool _connected,
        PuzzleTravelResult _evaluation, long _travelHours, bool _highSpeed)
    {
        string heading = $"<size=28><b>{_city.displayName}</b></size>";
        if (_connected)
        {
            heading += _highSpeed ? " <size=18>[고속선]</size>" : " <size=18>[일반선]</size>";
        }
        string status;
        if (_current)
        {
            status = $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent)}><b>현재 체류 중인 마을</b></color>";
        }
        else if (!_connected)
        {
            status = $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleMapUnknown)}><b>직접 연결된 철로 없음 (이동 불가)</b></color>";
        }
        else if (_evaluation.Failure == PuzzleTravelFailure.InsufficientFunds)
        {
            status = $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled)}><b>자금 부족</b> · {PuzzleTravelQuote(_evaluation, _travelHours)}</color>";
        }
        else if (_evaluation.IsSuccess)
        {
            status = $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleMapConnected)}><b>이동 가능</b> · {PuzzleTravelQuote(_evaluation, _travelHours)}</color>";
        }
        else
        {
            status = $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled)}>{PuzzleTravelStatus(_evaluation)} · 소요: {_travelHours}시간</color>";
        }

        string trade;
        if (!_visited)
        {
            trade = "<size=20>미방문 · 정보 미수집</size>\n\n직접 방문하면 교역 정보가 기록됩니다.";
        }
        else
        {
            var rules = _city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>();
            string common = string.Join(" · ", rules
                .Where(rule => rule != null && rule.buyPercent < 100 && rule.sellPercent < 100)
                .OrderBy(rule => rule.tag, StringComparer.Ordinal).Select(rule => PuzzleTagLabel(rule.tag)));
            string rare = string.Join(" · ", rules
                .Where(rule => rule != null && rule.buyPercent > 100 && rule.sellPercent > 100)
                .OrderBy(rule => rule.tag, StringComparer.Ordinal).Select(rule => PuzzleTagLabel(rule.tag)));
            string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
            string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent);
            trade = $"<size=20>특산품: {PuzzleTagLabel(_city.specialtyTag)} · 방문 완료</size>\n" +
                $"<color=#{commonColor}><b>흔한 자원</b></color>   {(string.IsNullOrEmpty(common) ? "없음" : common)}\n" +
                $"<color=#{rareColor}><b>귀한 자원</b></color>   {(string.IsNullOrEmpty(rare) ? "없음" : rare)}";
        }

        return $"{heading}  {status}\n{trade}";
    }

    static string PuzzleTravelBreakdown(PuzzleTravelResult _result)
    {
        long cargoCost = _result.Cost - _result.BaseCost - _result.DistanceCost;
        return $"기본 {_result.BaseCost:N0} + 거리 {_result.DistanceCost:N0} + 적재 {cargoCost:N0}";
    }

    static string PuzzleTravelQuote(PuzzleTravelResult _result, long _travelHours)
    {
        return $"거리 {_result.Distance} · 소요 {_travelHours}시간 · {PUZZLE_TRAVEL_COST} {_result.Cost:N0} ({PuzzleTravelBreakdown(_result)})";
    }

    public static Color PuzzleItemIcon(float _alpha)
    {
        return new Color(1f, 1f, 1f, _alpha);
    }
    public static Color PuzzleSynergyArrow => UnityEngine.Color.white;
    public const string PUZZLE_TRAIN_TITLE = "Train Inventory";
    internal static string PuzzleTrainTitle(int _wagonNumber)
    {
        return $"기차 화물 ({_wagonNumber}번 칸)";
    }
    internal static string PuzzleAddWagonLabel(long _cost)
    {
        return $"기차 화물칸 추가\n${_cost}";
    }
    public static Color PuzzleAddWagonBox => new Color32(40, 167, 69, 255);
    public static Color PuzzleWagonSelectedBorder => new Color32(255, 215, 0, 255);
    public static Color PuzzleWagonNormalBorder => new Color32(90, 95, 105, 255);
    public const string PUZZLE_MARKET_TITLE = "Market Inventory";
    public const string PUZZLE_MOVE_GUIDE = "드래그로 이동    R로 회전    Esc로 취소";
    public const string PUZZLE_PREVIOUS_CITY = "Previous City";
    public const string PUZZLE_NEXT_CITY = "Next City";
    public const string PUZZLE_TRAVEL_COST = "비용";
    public const string PUZZLE_TRADE_GUIDE = "시장 → 기차 구매   ·   기차 → 시장 판매   ·   Ctrl + 클릭 즉시 이동";
    public const string PUZZLE_TAG_EXPENSIVE = "귀함";
    public const string PUZZLE_TAG_COMMON = "흔함";

    static readonly Dictionary<string, string> PUZZLE_TAG_LABELS = new Dictionary<string, string>
    {
        { "Cargo", "상자" },
        { "Wood", "목재" },
        { "Food", "음식" },
        { "Grain", "곡물" },
        { "Supply", "보급품" },
        { "Water", "물" },
        { "Machinery", "기계" },
        { "Metal", "금속" },
        { "Fish", "물고기" },
        { "Salt", "소금" },
        { "Meat", "고기" },
        { "Animal", "가축" },
        { "Fruit", "과일" },
    };

    public static Color PuzzleBackground => new(0.10f, 0.13f, 0.17f);
    public static Color PuzzleCell => new(0.17f, 0.21f, 0.26f);
    public static Color PuzzleHeading => new(0.92f, 0.94f, 0.97f);
    public static Color PuzzlePiece => UnityEngine.Color.white;
    public static Color PuzzleGhost => new(1f, 1f, 1f, 0.42f);
    public static Color PuzzleMoveTarget => new(0.53f, 0.79f, 1f);

    public static Color PuzzleTooltipHeaderBackground => new Color32(176, 180, 184, 255);
    public static Color PuzzleTooltipBodyBackground => new Color32(214, 216, 218, 255);
    public static Color PuzzleTooltipDivider => new Color32(30, 30, 30, 255);
    public static Color PuzzleTooltipText => new Color32(30, 30, 30, 255);
    public static Color PuzzleTooltipGold => new Color32(159, 163, 72, 255);
    public static Color PuzzleTooltipGreen => new Color32(31, 158, 61, 255);
    public static Color PuzzleTooltipRed => new Color32(192, 57, 43, 255);

    internal static string PuzzleTooltipActualValue(decimal _actualValue)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>실제 가치 : </color><color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipGold)}>$ {_actualValue:#,0.##}</color>";
    }

    static Color RateValueColor(int _percent)
    {
        if (_percent > 100)
        {
            return PuzzleTooltipGreen;
        }
        if (_percent < 100)
        {
            return PuzzleTooltipRed;
        }
        return PuzzleTooltipText;
    }

    static Color PriceValueColor(decimal _price, decimal _actualValue)
    {
        if (_price > _actualValue)
        {
            return PuzzleTooltipGreen;
        }
        if (_price < _actualValue)
        {
            return PuzzleTooltipRed;
        }
        return PuzzleTooltipText;
    }

    internal static string PuzzleTooltipSellRate(int _percent)
    {
        Color color = RateValueColor(_percent);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>판매 배율 : </color><color=#{ColorUtility.ToHtmlStringRGB(color)}>{_percent}%</color>";
    }

    internal static string PuzzleTooltipBuyRate(int _percent)
    {
        Color color = RateValueColor(_percent);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>구매 배율 : </color><color=#{ColorUtility.ToHtmlStringRGB(color)}>{_percent}%</color>";
    }

    internal static string PuzzleTooltipFinalSellPrice(decimal _price, decimal _actualValue)
    {
        Color color = PriceValueColor(_price, _actualValue);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>판매 금액 : </color><color=#{ColorUtility.ToHtmlStringRGB(color)}>$ {_price:#,0.##}</color>";
    }

    internal static string PuzzleTooltipFinalBuyPrice(decimal _price, decimal _actualValue)
    {
        Color color = PriceValueColor(_price, _actualValue);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>구매 금액 : </color><color=#{ColorUtility.ToHtmlStringRGB(color)}>$ {_price:#,0.##}</color>";
    }

    internal static string PuzzleTooltipSynergyHeader()
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>적용 중인 시너지 목록</color>";
    }

    internal static string PuzzleTooltipSynergyBonus(string _neighborName, decimal _percent)
    {
        string sign = _percent >= 0 ? "+" : string.Empty;
        Color color = _percent >= 0 ? PuzzleTooltipGreen : PuzzleTooltipRed;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>• {_neighborName} : </color><color=#{ColorUtility.ToHtmlStringRGB(color)}>{sign}{_percent:#,0.##}%</color>";
    }

    internal static string PuzzleTooltipNoSynergy()
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>• 적용 중인 시너지 없음</color>";
    }

    internal static string PuzzleTooltipFutureValue(decimal _futureValue, decimal _actualValue)
    {
        Color color = _futureValue > _actualValue
            ? PuzzleTooltipGreen
            : (_futureValue < _actualValue ? PuzzleTooltipRed : PuzzleTooltipGold);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>미래 가치 : </color><color=#{ColorUtility.ToHtmlStringRGB(color)}>$ {_futureValue:#,0.##}</color>";
    }

    internal static string PuzzleTooltipFreshness(int _current)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>신선도 : </color><color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen)}>{_current}</color>";
    }

    internal static string PuzzleValueText(PuzzleValue _value)
    {
        return $"기본 가치 {_value.BaseValue:N0}  →  최종 가치 {_value.FinalValue:#,0.##}";
    }

    internal static string PuzzleItemDetails(PuzzleItemDefinition _item, PuzzleValue _value, decimal _actualValue,
        PuzzleTradeRate _rate)
    {
        if (_item == null)
        {
            return string.Empty;
        }

        string details =
            $"{_item.DisplayName}    {PuzzleValueText(_value)}    실제 가치 {_actualValue:#,0.##}\n{_item.Description}";
        if (_rate.BuyPercent > 0 || _rate.SellPercent > 0)
        {
            details += "\n" + PuzzleTradeRateText(_rate);
        }
        if (_value.SynergyBonuses.Count == 0)
        {
            return $"{details}\n인접 시너지 없음";
        }

        var bonuses = _value.SynergyBonuses.Select(bonus =>
            $"{bonus.NeighborName} {bonus.Percent:+#,0.##;-#,0.##;0}% ({bonus.Amount:+#,0.##;-#,0.##;0})");
        return $"{details}\n인접 시너지\n{string.Join("  ·  ", bonuses)}";
    }

    internal static string PuzzleTradeRateText(PuzzleTradeRate _rate)
    {
        string rule = string.IsNullOrEmpty(_rate.Tag) ? "기본" : _rate.Tag;
        return $"현재 도시 배율 · {rule} 구매 {_rate.BuyPercent}% · 판매 {_rate.SellPercent}%";
    }

    internal static string PuzzleFunds(long _funds)
    {
        return $"보유 자금 {_funds:N0}";
    }

    // 도시 이동 버튼 문구. 목적지 노선의 비용을 같은 버튼에 붙여 별도 표시 요소를 두지 않는다.
    internal static string PuzzleCityButton(string _label, long? _cost)
    {
        return _cost is long cost ? $"{_label}    {PUZZLE_TRAVEL_COST} {cost:N0}" : _label;
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
            if (rule.buyPercent > 100 && rule.sellPercent > 100)
            {
                expensive.Add(rule.tag);
            }
            else if (rule.buyPercent < 100 && rule.sellPercent < 100)
            {
                common.Add(rule.tag);
            }
        }

        var parts = new List<string>();
        if (expensive.Count > 0)
        {
            parts.Add($"{PUZZLE_TAG_EXPENSIVE} {string.Join(", ", expensive.OrderBy(tag => tag, StringComparer.Ordinal).Select(PuzzleTagLabel))}");
        }
        if (common.Count > 0)
        {
            parts.Add($"{PUZZLE_TAG_COMMON} {string.Join(", ", common.OrderBy(tag => tag, StringComparer.Ordinal).Select(PuzzleTagLabel))}");
        }
        return string.Join("    ", parts);
    }

    public static string PuzzleTagLabel(string _tag)
    {
        if (string.IsNullOrEmpty(_tag))
        {
            return string.Empty;
        }
        return PUZZLE_TAG_LABELS.TryGetValue(_tag, out string label) ? label : _tag;
    }

    public static Color PuzzleTagBackgroundColor(string _tag)
    {
        return (_tag ?? string.Empty).ToLowerInvariant() switch
        {
            "wood" => new Color32(141, 90, 48, 255),       // 목재: 팔레트같은 따뜻한 브라운
            "metal" => new Color32(84, 110, 122, 255),     // 금속: 메탈릭 스틸 슬레이트
            "machinery" => new Color32(180, 83, 9, 255),   // 기계: 산업용 앰버/브론즈
            "food" => new Color32(217, 119, 6, 255),       // 식품/음식: 수확 웜 오렌지
            "fish" => new Color32(2, 132, 199, 255),       // 물고기/수산물: 딥 오션 블루
            "water" => new Color32(14, 165, 233, 255),     // 물/식수: 맑은 아쿠아 블루
            "salt" => new Color32(100, 116, 139, 255),     // 소금: 미네랄 실버 슬레이트
            "supply" => new Color32(22, 163, 74, 255),     // 보급품: 포레스트 그린
            "cargo" => new Color32(141, 110, 99, 255),      // 화물/상자: 가죽/카톤 브라운
            "meat" => new Color32(159, 45, 58, 255),       // 고기: 딥 레드
            "animal" => new Color32(160, 120, 74, 255),    // 가축: 탄/레더 탠
            "fruit" => new Color32(196, 63, 63, 255),      // 과일: 사과 레드
            _ => new Color32(71, 85, 105, 255)
        };
    }

    public static Color PuzzleTagTextColor(string _tag)
    {
        // 다크/미디엄 톤 테마 배경 위에서 선명하게 읽히는 흰색
        return new Color32(255, 255, 255, 255);
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
            "wood" => 1,
            "machinery" => 2,
            "metal" => 3,
            "fish" => 4,
            "food" => 5,
            "meat" => 5,
            "fruit" => 5,
            "animal" => 8,
            "water" => 6,
            "salt" => 7,
            "supply" => 20,
            "cargo" => 21,
            _ => 10
        };
    }

    internal const string PUZZLE_CLEAR_TITLE = "게임 완료";

    internal static string PuzzleClearButton(long _cost)
    {
        return $"게임 클리어  {_cost:N0}$";
    }

    internal static string PuzzleClearSummary(long _funds)
    {
        return $"남은 자금 {_funds:N0}$";
    }

    internal static string PuzzleRunStatusText(PuzzleRunStatus _status)
    {
        return _status switch
        {
            PuzzleRunStatus.Won => PUZZLE_CLEAR_TITLE,
            PuzzleRunStatus.Lost => "빚을 갚지 못했습니다. 패배",
            _ => PUZZLE_TRADE_GUIDE
        };
    }

    // 이동이 차단된 이유. 비용 부족은 패배가 아니라 상태 유지고, 금액은 노선 기본값과 기차 보유 칸으로 갈린다.
    internal static string PuzzleTravelStatus(PuzzleTravelResult _result)
    {
        return _result.Failure switch
        {
            PuzzleTravelFailure.SameCity => "현재 체류 중인 마을입니다",
            PuzzleTravelFailure.InvalidCity => "유효하지 않은 마을입니다",
            PuzzleTravelFailure.InsufficientFunds => $"{PUZZLE_TRAVEL_COST} {_result.Cost:N0}"
                + $" ({PuzzleTravelBreakdown(_result)})에 필요한 자금이 부족합니다",
            PuzzleTravelFailure.CostOverflow => "이동 비용이 거래 한도를 초과합니다",
            PuzzleTravelFailure.MissingRoute => "두 도시를 잇는 직결 철로가 없습니다",
            PuzzleTravelFailure.NotAvailable => "이동할 수 있는 도시가 없습니다",
            PuzzleTravelFailure.Blocked => "게임이 종료되었습니다",
            _ => PUZZLE_TRADE_GUIDE
        };
    }

    internal static string PuzzleTradeStatus(PuzzleDropResult _result, PuzzleItemDefinition _item, bool _committed)
    {
        if (_result.Direction == PuzzleTradeDirection.None)
        {
            return PUZZLE_TRADE_GUIDE;
        }

        string direction = _result.Direction == PuzzleTradeDirection.Buy ? "구매" : "판매";
        string quote = $"{_item.DisplayName} · {direction}";
        if (_result.Placement.Failure != PuzzlePlacementFailure.StaleSource &&
            _result.TradeFailure != PuzzleTradeFailure.PriceOverflow)
        {
            quote += $" {_result.Price:N0}";
        }
        if (_result.AppliedPercent > 0)
        {
            quote += _result.AppliedTag == null
                ? $" · 기본 {_result.AppliedPercent}%"
                : $" · {_result.AppliedTag} {_result.AppliedPercent}%";
        }
        if (_result.CanDrop)
        {
            return _committed ? $"{quote} 완료" : quote;
        }

        string reason = _result.TradeFailure switch
        {
            PuzzleTradeFailure.Disabled => "이 도시에서는 허용되지 않습니다",
            PuzzleTradeFailure.InsufficientFunds => "자금이 부족합니다",
            PuzzleTradeFailure.PriceOverflow => "판매가가 거래 한도를 초과합니다",
            PuzzleTradeFailure.BalanceOverflow => "보유 자금 한도를 초과합니다",
            _ => _result.Placement.Failure switch
            {
                PuzzlePlacementFailure.StaleSource => "원래 화물 배치가 변경됐습니다",
                PuzzlePlacementFailure.CellOutsideBoard => "화물이 보드 경계를 넘습니다",
                PuzzlePlacementFailure.CellOccupied => "다른 화물과 겹칩니다",
                PuzzlePlacementFailure.NoSpace => "상대 보드에 화물을 놓을 공간이 없습니다",
                _ => "이 위치에 배치할 수 없습니다"
            }
        };
        return $"{quote} · {reason}";
    }

    public static ColorBlock PuzzleCityButtonColors => new()
    {
        normalColor = PuzzleBackground,
        highlightedColor = new Color(0.23f, 0.30f, 0.38f),
        pressedColor = PuzzleBackground,
        selectedColor = PuzzleCell,
        disabledColor = new Color(0.10f, 0.13f, 0.17f, 0.50f),
        colorMultiplier = 1f,
        fadeDuration = ColorBlock.defaultColorBlock.fadeDuration
    };

    public static Color PuzzlePieceOutline(float _alpha)
    {
        return new Color(0f, 0f, 0f, _alpha);
    }

    public static Color PuzzleOutline(bool _canPlace)
    {
        return _canPlace ? new Color(0.39f, 0.91f, 0.67f) : new Color(1f, 0.43f, 0.39f);
    }

    // 범주 팔레트. 새 범주를 추가하면 여기 색과 Title을 함께 추가한다.
    public static Color Color(EffectCategory _category)
    {
        return _category switch
        {
            EffectCategory.Offense => new Color(0.90f, 0.33f, 0.29f),
            EffectCategory.Mobility => new Color(0.33f, 0.58f, 0.95f),
            EffectCategory.Defense => new Color(0.35f, 0.76f, 0.44f),
            EffectCategory.BaseStat => new Color(0.96f, 0.78f, 0.27f),
            _ => new Color(0.72f, 0.74f, 0.80f),
        };
    }

    public static string Title(EffectCategory _category)
    {
        return _category switch
        {
            EffectCategory.Offense => "공격",
            EffectCategory.Mobility => "이동",
            EffectCategory.Defense => "방어",
            EffectCategory.BaseStat => "기본 능력치",
            _ => "유틸",
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
        return _upgrade == null ? "비어 있음" : string.IsNullOrEmpty(_upgrade.displayName) ? _upgrade.id : _upgrade.displayName;
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
    const string VALUE_TAG = "#FFE08A";
    const string DIM_TAG = "#AEB6C2";
    public static string Accent(string _value)
    {
        return $"<color={VALUE_TAG}>{_value}</color>";
    }
    public static string Dim(string _value)
    {
        return $"<color={DIM_TAG}>{_value}</color>";
    }
    public static string Heading(string _title, Color _color)
    {
        return $"<size=135%><b><color={Hex(_color)}>{_title}</color></b></size>";
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
            return Dim("유효하지 않은 항목");
        }
        return $"{Heading(Name(_upgrade), Color(_upgrade))}\n{Dim(Title(_upgrade))}\n{Describe(_upgrade)}";
    }
}
