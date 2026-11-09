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
    public static Color PuzzleItemIcon(float _alpha)
    {
        return new Color(1f, 1f, 1f, _alpha);
    }
    public static Color PuzzleSynergyArrow => UnityEngine.Color.white;
    public const string PUZZLE_TRAIN_TITLE = "Train Inventory";
    public const string PUZZLE_MARKET_TITLE = "Market Inventory";
    public const string PUZZLE_MOVE_GUIDE = "드래그로 이동    R로 회전    Esc로 취소";
    public const string PUZZLE_PREVIOUS_CITY = "Previous City";
    public const string PUZZLE_NEXT_CITY = "Next City";
    public const string PUZZLE_TRAVEL_COST = "비용";
    public const string PUZZLE_TRADE_GUIDE = "시장 → 기차 구매   ·   기차 → 시장 판매";
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

    internal static string PuzzleTooltipSellRate(int _percent)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>판매 배율 : </color><color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen)}>{_percent}%</color>";
    }

    internal static string PuzzleTooltipBuyRate(int _percent)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipText)}>구매 배율 : </color><color=#{ColorUtility.ToHtmlStringRGB(PuzzleTooltipRed)}>{_percent}%</color>";
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
            "cargo" => new Color32(141, 110, 99, 255),     // 화물/상자: 가죽/카톤 브라운
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
            "water" => 6,
            "salt" => 7,
            "supply" => 20,
            "cargo" => 21,
            _ => 10
        };
    }

    internal static string PuzzleRunStatusText(PuzzleRunStatus _status)
    {
        return _status switch
        {
            PuzzleRunStatus.Won => "빚을 모두 갚았습니다. 승리!",
            PuzzleRunStatus.Lost => "빚을 갚지 못했습니다. 패배",
            _ => PUZZLE_TRADE_GUIDE
        };
    }

    // 이동이 차단된 이유. 비용 부족은 패배가 아니라 상태 유지다.
    internal static string PuzzleTravelStatus(PuzzleTravelResult _result)
    {
        return _result.Failure switch
        {
            PuzzleTravelFailure.InsufficientFunds => $"{PUZZLE_TRAVEL_COST} {_result.Cost:N0}이(가) 부족합니다",
            PuzzleTravelFailure.MissingRoute => "두 도시를 잇는 노선이 없습니다",
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
