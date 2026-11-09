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
    public const string PUZZLE_TRAIN_TITLE = "Train Inventory";
    public const string PUZZLE_MARKET_TITLE = "Market Inventory";
    public const string PUZZLE_MOVE_GUIDE = "드래그로 이동    R로 회전    Esc로 취소";
    public const string PUZZLE_PREVIOUS_CITY = "Previous City";
    public const string PUZZLE_NEXT_CITY = "Next City";
    public const string PUZZLE_TRADE_GUIDE = "시장 → 기차 구매   ·   기차 → 시장 판매";

    public static Color PuzzleBackground => new(0.10f, 0.13f, 0.17f);
    public static Color PuzzleCell => new(0.17f, 0.21f, 0.26f);
    public static Color PuzzleHeading => new(0.92f, 0.94f, 0.97f);
    public static Color PuzzlePiece => UnityEngine.Color.white;
    public static Color PuzzleGhost => new(1f, 1f, 1f, 0.42f);
    public static Color PuzzleMoveTarget => new(0.53f, 0.79f, 1f);

    internal static string PuzzleValueText(PuzzleValue _value)
    {
        return $"기본 가치 {_value.BaseValue:N0}  →  최종 가치 {_value.FinalValue:#,0.##}";
    }

    internal static string PuzzleItemDetails(PuzzleItemDefinition _item, PuzzleValue _value)
    {
        if (_item == null)
        {
            return string.Empty;
        }

        string details = $"{_item.DisplayName}    {PuzzleValueText(_value)}\n{_item.Description}";
        if (_value.SynergyBonuses.Count == 0)
        {
            return $"{details}\n인접 시너지 없음";
        }

        var bonuses = _value.SynergyBonuses.Select(bonus =>
            $"{bonus.NeighborName} {bonus.Percent:+#,0.##;-#,0.##;0}% ({bonus.Amount:+#,0.##;-#,0.##;0})");
        return $"{details}\n인접 시너지\n{string.Join("  ·  ", bonuses)}";
    }

    internal static string PuzzleFunds(long _funds)
    {
        return $"보유 자금 {_funds:N0}";
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
