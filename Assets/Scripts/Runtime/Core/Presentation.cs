using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public static string LayoutText(string _key)
    {
        return BasicData.Text("ui.layout." + _key);
    }

    public static Color SharedColor(string _id)
    {
        return BasicData.Color(_id);
    }

    public static Color PuzzleUnmodifiedTint => BasicData.Color("presentation.inventory.unmodifiedTint");
    public static Color PuzzleTextOutline => BasicData.Color("presentation.inventory.textOutline");

    public static string TavernTitle(string _cityName)
    {
        return BasicData.Format("presentation.station.title", _cityName);
    }

    public static string TavernRumor(string _cityName, string _tag)
    {
        return BasicData.Format("presentation.station.rumor", _cityName, PuzzleTagLabel(_tag));
    }

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
    public static string PuzzleNightMarket => BasicData.Text("presentation.inventory.nightMarket");
    public static float PuzzleMarketClosedAlpha => BasicData.Float("presentation.inventory.stationRestDurationAbovePercent");
    public static string PuzzleMapLocate => BasicData.Text("presentation.map.locate");
    public static string PuzzleMapZoomIn => BasicData.Text("presentation.map.zoomIn");
    public static string PuzzleMapZoomOut => BasicData.Text("presentation.map.zoomOut");
    public static float PuzzleMapHaloSize => BasicData.Float("presentation.map.haloSize");
    public static int PuzzleMapBadgeFontSize => BasicData.Int("presentation.map.badgeFontSize");
    public static int PuzzleMapTimeFontSize => BasicData.Int("presentation.map.timeFontSize");
    public static int PuzzleMapStationFontSize(float _zoom, bool _emphasized)
    {
        return _emphasized ? BasicData.Int("presentation.map.puzzleMapStationFontSize") : _zoom < BasicData.Float("presentation.map.puzzleMapStationFontSizeBelowPercent") ? BasicData.Int("presentation.map.puzzleMapStationFontSizeBelowPercent2") : BasicData.Int("presentation.map.puzzleMapStationFontSizeBelowPercent3");
    }

    public static float PuzzleMapTrackThickness(float _authoredThickness)
    {
        return _authoredThickness * BasicData.Float("presentation.map.puzzleMapTrackThickness");
    }
    public static Color PuzzleMapCurrentHalo => BasicData.Color("presentation.map.currentHalo");
    public static Color PuzzleMapTargetHalo => PuzzleMapCurrentHalo;
    public static Color PuzzleMapOcean => BasicData.Color("presentation.map.ocean");
    public static Color PuzzleMapLand => BasicData.Color("presentation.map.land");
    public static Color PuzzleMapRiver => BasicData.Color("presentation.map.river");
    public static Color PuzzleMapTerrainBorder => BasicData.Color("presentation.map.terrainBorder");
    public static string PuzzleMapZoom(float _scale)
    {
        return BasicData.Format("presentation.map.zoom", _scale * BasicData.Float("presentation.map.puzzleMapZoom"));
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
        PuzzleTravelResult _evaluation, PuzzleRegionData _region)
    {
        string heading = BasicData.Format("presentation.map.information", _city.displayName);
        heading += _visited
            ? BasicData.Format("presentation.map.informationText2", _region.displayName, _region.pricePercent)
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
            trade = BasicData.Text("presentation.map.informationText12");
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
                BasicData.Format("presentation.map.informationText10", rareColor, (string.IsNullOrEmpty(rare) ? BasicData.Text("presentation.map.informationText15") : rare));
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

    public static string PuzzleMapStationName(string _name, bool _planningTarget)
    {
        return _planningTarget ? _name + " ★" : _name;
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
    public static string PuzzleTrainTitleText => BasicData.Text("presentation.train.trainTitle");
    internal static string PuzzleTrainTitle(int _wagonNumber)
    {
        return BasicData.Format("presentation.train.trainTitle2", _wagonNumber);
    }
    public static string PuzzleAddWagonLabel(long _cost)
    {
        return BasicData.Format("presentation.train.addWagonLabel", _cost);
    }
    internal static string PuzzleWagonPurchaseStatus(int _wagonNumber, long _cost)
    {
        return BasicData.Format("presentation.train.wagonPurchaseStatus", _cost, _wagonNumber);
    }
    public static Color PuzzleAddWagonBox => BasicData.Color("presentation.train.addWagonBox");
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
        { "Fish", "presentation.tags.fishLabel" },
        { "Salt", "presentation.tags.saltLabel" },
        { "Meat", "presentation.tags.meatLabel" },
        { "Animal", "presentation.tags.animalLabel" },
        { "Fruit", "presentation.tags.fruitLabel" },
        { "Poo", "presentation.tags.pooLabel" },
        { "Box", "presentation.tags.boxLabel" },

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
        string amount = $"${_actualValue:#,0.##}".PadRight(7, '\u00A0');
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
        return BasicData.Format("presentation.tooltip.activeEffectLabel", TooltipLabelHtml(), _bonus.NeighborName);
    }

    internal static string PuzzleTooltipActiveEffectValue(PuzzleSynergyBonus _bonus)
    {
        return BasicData.Format("presentation.tooltip.activeEffectValue",
            ColorUtility.ToHtmlStringRGB(ComparisonColor(_bonus.Amount, 0m, PuzzleTooltipText)), _bonus.Amount);
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

    internal static string PuzzleTooltipEffectHeader()
    {
        return BasicData.Format("presentation.tooltip.effectHeader", TooltipLabelHtml(), TableManager.Instance.RequireValid().PuzzleSynergy.activationIntervalHours);
    }

    internal static string PuzzleFreshnessText(int _percent)
    {
        return BasicData.Format("presentation.freshness.percent", _percent);
    }

    internal static Color PuzzleFreshnessOutline => BasicData.Color("presentation.freshness.outline");

    internal static Color PuzzleFreshnessColor(int _percent)
    {
        if (_percent == 0)
        {
            return BasicData.Color("presentation.freshness.empty");
        }
        if (_percent <= BasicData.Int("ui.freshness.lowThreshold"))
        {
            return BasicData.Color("presentation.freshness.low");
        }
        if (_percent <= BasicData.Int("ui.freshness.mediumThreshold"))
        {
            return BasicData.Color("presentation.freshness.medium");
        }
        return BasicData.Color("presentation.freshness.high");
    }

    internal static string PuzzleTooltipEffectDescription(string _description)
    {
        string highlighted = Regex.Replace(_description, @"[+−-]\d+(?:[.,]\d+)?%", match =>
        {
            Color color = match.Value[0] == '+' ? PuzzleTooltipGreen : PuzzleTooltipRed;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{match.Value}</b></color>";
        });
        highlighted = Regex.Replace(highlighted, @"\([^)]*\)", match => $"<size=70%>{match.Value}</size>");
        return BasicData.Format("presentation.tooltip.effectDescription", TooltipLabelHtml(), highlighted);
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
        return BasicData.Format("presentation.tooltip.freshnessValue", ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), _current);
    }

    internal static string PuzzleTooltipShelfLifeLabel()
    {
        return BasicData.Format("presentation.tooltip.shelfLifeLabel", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipShelfLifeValue(int _hours)
    {
        return BasicData.Format("presentation.tooltip.shelfLifeValue", ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), _hours);
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

        var bonuses = _value.SynergyBonuses.Select(bonus =>
            BasicData.Format("presentation.inventory.itemDetailsText3", bonus.NeighborName, bonus.Percent, bonus.Amount));
        return BasicData.Format("presentation.inventory.itemDetailsText4", details, string.Join("  ·  ", bonuses));
    }

    internal static string PuzzleTradeRateText(PuzzleTradeRate _rate)
    {
        string rule = string.IsNullOrEmpty(_rate.Tag) ? BasicData.Text("presentation.trade.tradeRateTextText2") : _rate.Tag;
        return BasicData.Format("presentation.trade.tradeRateText", rule, _rate.BuyPercent, _rate.SellPercent);
    }

    internal static string PuzzleFunds(long _funds)
    {
        return BasicData.Format("presentation.inventory.funds", _funds);
    }

    // 도시의 태그 규칙을 구매·판매 비율 기준으로 귀함/흔함으로 묶는다.
    // 두 비율이 100을 사이에 두고 갈리거나 한쪽이 100이면 어느 쪽도 아닌 것으로 보고 표시하지 않는다.
    internal static string PuzzleCityTagPreferences(PuzzleCityData _city, PuzzleRegionData _region)
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
            BasicData.Format("presentation.trade.regionPrice", _region.pricePercent);

    }

    public static string PuzzleTagLabel(string _tag)
    {
        if (string.IsNullOrEmpty(_tag))
        {
            return string.Empty;
        }
        return PUZZLE_TAG_LABELS.TryGetValue(_tag, out string label) ? BasicData.Text(label) : _tag;
    }

    public static Color PuzzleTagBackgroundColor(string _tag)
    {
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
            "cargo" => BasicData.Color("presentation.tags.cargoBackground"),      // 화물/상자: 가죽/카톤 브라운
            "meat" => BasicData.Color("presentation.tags.meatBackground"),       // 고기: 딥 레드
            "animal" => BasicData.Color("presentation.tags.animalBackground"),    // 가축: 탄/레더 탠
            "fruit" => BasicData.Color("presentation.tags.fruitBackground"),      // 과일: 사과 레드
            "poo" => BasicData.Color("presentation.tags.pooBackground"),         // 똥: 다크 브라운
            _ => BasicData.Color("presentation.tags.puzzleTagBackgroundColorColor")
        };
    }

    public static Color PuzzleTagTextColor(string _tag)
    {
        // 다크/미디엄 톤 테마 배경 위에서 선명하게 읽히는 흰색
        return BasicData.Color("presentation.tags.puzzleTagTextColor");
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
            "poo" => BasicData.Int("presentation.tags.pooPriority"),
            _ => BasicData.Int("presentation.tags.defaultPriority")
        };
    }

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
            PuzzleTravelFailure.TimeOverflow => BasicData.Text("presentation.inventory.travelStatusText6"),
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
