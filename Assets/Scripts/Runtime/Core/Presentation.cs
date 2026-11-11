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
    public static string LayoutText(string _key)
    {
        return BasicData.Text("ui.layout." + _key);
    }

    public static Color SharedColor(string _id)
    {
        return BasicData.Color(_id);
    }

    public static Color PuzzleUnmodifiedTint => BasicData.Color("presentation.PuzzleUnmodifiedTint");
    public static Color PuzzleTextOutline => BasicData.Color("presentation.PuzzleTextOutline");

    public static string TavernTitle(string _cityName)
    {
        return BasicData.Format("presentation.tavern.title", _cityName);
    }

    public static string TavernRumor(string _cityName, string _tag)
    {
        return BasicData.Format("presentation.tavern.rumor", _cityName, PuzzleTagLabel(_tag));
    }

    public static string TavernNoRumor => BasicData.Text("presentation.tavern.noRumor");
    public static string TavernUnreadRumor => BasicData.Text("presentation.tavern.unreadRumor");
    public static string TavernTalk => BasicData.Text("presentation.tavern.talk");

    // 날짜 표기 도입 시 시간 데이터와 분리해 이 표시 경로만 교체한다.
    public static string PuzzleTime(PuzzleTimeManager _time)
    {
        return BasicData.Format("presentation.PuzzleTime.text1", _time.Day, _time.Hour, _time.Minute, (_time.IsNight ? BasicData.Text("presentation.PuzzleTime.text2") : BasicData.Text("presentation.PuzzleTime.text3")));
    }

    public static Color PuzzleTimeAccent => BasicData.Color("presentation.PuzzleTimeAccent.color1");

    public static string PuzzleMapTitle => BasicData.Text("presentation.PuzzleMapTitle");
    public const string PUZZLE_MAP_PANEL_NAME = "PuzzleWorldMap";
    public static string PuzzleMapOpen => BasicData.Text("presentation.PuzzleMapOpen");
    public static string PuzzleMapClose => BasicData.Text("presentation.PuzzleMapClose");
    public static string PuzzleMapCurrentText => BasicData.Text("presentation.PuzzleMapCurrent");
    public static string PuzzleMapTargetText => BasicData.Text("presentation.PuzzleMapTarget");
    public static string PuzzleMapCurrentAndTarget => BasicData.Text("presentation.PuzzleMapCurrentAndTarget");

    internal static string PuzzleMapBadgeText(bool _current, bool _planningTarget)
    {
        if (_current && _planningTarget)
        {
            return PuzzleMapCurrentAndTarget;
        }
        return _current ? PuzzleMapCurrentText : PuzzleMapTargetText;
    }
    public const string PUZZLE_TRAVEL_PANEL_NAME = "MapTravelPanel";
    public static string PuzzleTravelQuestion => BasicData.Text("presentation.PuzzleTravelQuestion");
    public static string PuzzleTravelConfirm => BasicData.Text("presentation.PuzzleTravelConfirm");
    public static string PuzzleTravelCancel => BasicData.Text("presentation.PuzzleTravelCancel");
    public static string PuzzleTravelInProgress => BasicData.Text("presentation.PuzzleTravelInProgress");
    public static string PuzzleTravelArrived => BasicData.Text("presentation.PuzzleTravelArrived");
    public static Color PuzzleTravelDim => BasicData.Color("presentation.PuzzleTravelDim.color1");
    public static Color PuzzleTravelCard => BasicData.Color("presentation.PuzzleTravelCard.color1");

    internal static string PuzzleTravelConfirmation(string _fromName, string _toName, PuzzleTravelResult _quote)
    {
        return BasicData.Format("presentation.PuzzleTravelConfirmation.text1", _fromName, _toName, PuzzleMapTime(_quote.TravelMinutes), _quote.ArrivalClockMinutes / PuzzleTimeManager.MINUTES_PER_HOUR, _quote.ArrivalClockMinutes % PuzzleTimeManager.MINUTES_PER_HOUR, (_quote.ArrivalIsNight ? BasicData.Text("presentation.PuzzleTravelConfirmation.text2") : BasicData.Text("presentation.PuzzleTravelConfirmation.text3")), _quote.Cost, _quote.FundsAfter);
    }

    internal static string PuzzleTravelJourney(string _fromName, string _toName)
    {
        return BasicData.Format("presentation.PuzzleTravelJourney.text1", _fromName, _toName);
    }

    public static string PuzzleTravelProgress(float _progress)
    {
        return BasicData.Format("presentation.PuzzleTravelProgress.text1", Mathf.RoundToInt(_progress * BasicData.Float("presentation.PuzzleTravelProgress.number1")));
    }
    public static string PuzzleMapFit => BasicData.Text("presentation.PuzzleMapFit");
    public static string PuzzleWaitDaylight => BasicData.Text("presentation.PuzzleWaitDaylight");
    public static string StationActionsTitle => BasicData.Text("presentation.StationActionsTitle");
    public static string StationMarket => BasicData.Text("presentation.StationMarket");
    public static string StationMarketClosed => BasicData.Text("presentation.StationMarketClosed");
    public static string StationTavern => BasicData.Text("presentation.StationTavern");
    public static string StationRest => BasicData.Text("presentation.StationRest");
    public static string StationTravel => BasicData.Text("presentation.StationTravel");
    public static string StationBack => BasicData.Text("presentation.StationBack");
    public static string StationRestHint => BasicData.Text("presentation.StationRestHint");
    public static string StationRestDuration(long _minutes)
    {
        return BasicData.Format("presentation.StationRestDuration.text1", PuzzleMapTime(_minutes));
    }
    public static string StationButchery => BasicData.Text("presentation.StationButchery");
    public static string StationButcheryConfirm => BasicData.Text("presentation.StationButcheryConfirm");
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
    public static string PuzzleNightMarket => BasicData.Text("presentation.PuzzleNightMarket");
    public static float PuzzleMarketClosedAlpha => BasicData.Float("presentation.PuzzleMarketClosedAlpha.number1");
    public static string PuzzlePreviewDestination(string _cityName, long? _minutes)
    {
        return _minutes.HasValue ? BasicData.Format("presentation.PuzzlePreviewDestination.text1", _cityName, PuzzleMapTime(_minutes.Value)) : BasicData.Text("presentation.PuzzlePreviewDestination.text2");
    }
    public static string PuzzleMapLocate => BasicData.Text("presentation.PuzzleMapLocate");
    public static string PuzzleMapZoomIn => BasicData.Text("presentation.PuzzleMapZoomIn");
    public static string PuzzleMapZoomOut => BasicData.Text("presentation.PuzzleMapZoomOut");
    public static float PuzzleMapHaloSize => BasicData.Float("presentation.PuzzleMapHaloSize");
    public static int PuzzleMapBadgeFontSize => BasicData.Int("presentation.PuzzleMapBadgeFontSize");
    public static int PuzzleMapTimeFontSize => BasicData.Int("presentation.PuzzleMapTimeFontSize");
    public static int PuzzleMapStationFontSize(float _zoom, bool _emphasized)
    {
        return _emphasized ? BasicData.Int("presentation.PuzzleMapStationFontSize.number1") : _zoom < BasicData.Float("presentation.PuzzleMapStationFontSize.number2") ? BasicData.Int("presentation.PuzzleMapStationFontSize.number3") : BasicData.Int("presentation.PuzzleMapStationFontSize.number4");
    }

    public static float PuzzleMapTrackThickness(float _authoredThickness)
    {
        return _authoredThickness * BasicData.Float("presentation.PuzzleMapTrackThickness.number1");
    }
    public static Color PuzzleMapCurrentHalo => BasicData.Color("presentation.PuzzleMapCurrentHalo.color1");
    public static Color PuzzleMapTargetHalo => PuzzleMapCurrentHalo;
    public static Color PuzzleMapOcean => BasicData.Color("presentation.PuzzleMapOcean.color1");
    public static Color PuzzleMapLand => BasicData.Color("presentation.PuzzleMapLand.color1");
    public static Color PuzzleMapRiver => BasicData.Color("presentation.PuzzleMapRiver.color1");
    public static Color PuzzleMapTerrainBorder => BasicData.Color("presentation.PuzzleMapTerrainBorder.color1");
    public static string PuzzleMapZoom(float _scale)
    {
        return BasicData.Format("presentation.PuzzleMapZoom.text1", _scale * BasicData.Float("presentation.PuzzleMapZoom.number1"));
    }
    public static string PuzzleMapGuideText => BasicData.Text("presentation.PuzzleMapGuide");
    public static string PuzzleMapHint => BasicData.Text("presentation.PuzzleMapHint");
    public static Color PuzzleMapCurrent => BasicData.Color("presentation.PuzzleMapCurrent.color1");
    public static Color PuzzleMapTarget => PuzzleMapCurrent;
    public static Color PuzzleMapVisited => BasicData.Color("presentation.PuzzleMapVisited.color1");
    public static Color PuzzleMapUnknown => BasicData.Color("presentation.PuzzleMapUnknown.color1");
    public static Color PuzzleMapRoute => BasicData.Color("presentation.PuzzleMapRoute.color1");
    public static Color PuzzleMapConnected => BasicData.Color("presentation.PuzzleMapConnected.color1");
    public static Color PuzzleMapConnectedDisabled => BasicData.Color("presentation.PuzzleMapConnectedDisabled.color1");

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
        return BasicData.Format("presentation.PuzzleMapGuide.text1", PuzzleMapGuideText, PuzzleMapTime(_elapsedMinutes));
    }

    internal static string PuzzleMapNodeLabel(string _name, bool _visited, bool _current, bool _connected, bool _canAfford)
    {
        if (_current)
        {
            return _name + BasicData.Text("presentation.PuzzleMapNodeLabel.text1");
        }
        if (_connected)
        {
            return _name + (_canAfford ? BasicData.Text("presentation.PuzzleMapNodeLabel.text2") : BasicData.Text("presentation.PuzzleMapNodeLabel.text3"));
        }
        return _name + (_visited ? BasicData.Text("presentation.PuzzleMapNodeLabel.text4") : BasicData.Text("presentation.PuzzleMapNodeLabel.text5"));
    }

    internal static string PuzzleMapInformation(PuzzleCityData _city, bool _visited, bool _current, bool _connected,
        PuzzleTravelResult _evaluation, PuzzleRegionData _region)
    {
        string heading = BasicData.Format("presentation.PuzzleMapInformation.text1", _city.displayName);
        heading += BasicData.Format("presentation.PuzzleMapInformation.text2", _region.displayName, _region.pricePercent);
        string status;
        if (_current)
        {
            status = BasicData.Format("presentation.PuzzleMapInformation.text3", ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent));
        }
        else if (!_connected)
        {
            status = BasicData.Format("presentation.PuzzleMapInformation.text4", ColorUtility.ToHtmlStringRGB(PuzzleMapUnknown));
        }
        else if (_evaluation.Failure == PuzzleTravelFailure.InsufficientFunds)
        {
            status = BasicData.Format("presentation.PuzzleMapInformation.text5", ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled), PuzzleTravelQuote(_evaluation));
        }
        else if (_evaluation.IsSuccess)
        {
            status = BasicData.Format("presentation.PuzzleMapInformation.text6", ColorUtility.ToHtmlStringRGB(PuzzleMapConnected), PuzzleTravelQuote(_evaluation));
        }
        else
        {
            status = BasicData.Format("presentation.PuzzleMapInformation.text7", ColorUtility.ToHtmlStringRGB(PuzzleMapConnectedDisabled), PuzzleTravelStatus(_evaluation), PuzzleMapTime(_evaluation.TravelMinutes));
        }

        string trade;
        if (!_visited)
        {
            trade = BasicData.Text("presentation.PuzzleMapInformation.text12");
        }
        else
        {
            var rules = _city.tagPrices ?? Array.Empty<PuzzleCityTagPriceData>();
            string common = string.Join(" · ", rules
                .Where(rule => rule != null && rule.buyPercent < BasicData.Int("presentation.if.number1") && rule.sellPercent < BasicData.Int("presentation.if.number2"))
                .OrderBy(rule => rule.tag, StringComparer.Ordinal).Select(rule => PuzzleTagLabel(rule.tag)));
            string rare = string.Join(" · ", rules
                .Where(rule => rule != null && rule.buyPercent > BasicData.Int("presentation.if.number3") && rule.sellPercent > BasicData.Int("presentation.if.number4"))
                .OrderBy(rule => rule.tag, StringComparer.Ordinal).Select(rule => PuzzleTagLabel(rule.tag)));
            string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
            string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent);
            trade = BasicData.Format("presentation.PuzzleMapInformation.text8", (_region.tradeHub ? BasicData.Text("presentation.PuzzleMapInformation.text13") : PuzzleTagLabel(_city.specialtyTag))) +
                BasicData.Format("presentation.PuzzleMapInformation.text9", commonColor, (string.IsNullOrEmpty(common) ? BasicData.Text("presentation.PuzzleMapInformation.text14") : common)) +
                BasicData.Format("presentation.PuzzleMapInformation.text10", rareColor, (string.IsNullOrEmpty(rare) ? BasicData.Text("presentation.PuzzleMapInformation.text15") : rare));
        }

        return BasicData.Format("presentation.PuzzleMapInformation.text11", heading, status, trade);
    }

    static string PuzzleTravelBreakdown(PuzzleTravelResult _result)
    {
        long cargoCost = _result.Cost - _result.BaseCost - _result.DistanceCost;
        return BasicData.Format("presentation.PuzzleTravelBreakdown.text1", _result.BaseCost, _result.DistanceCost, cargoCost);
    }

    static string PuzzleTravelQuote(PuzzleTravelResult _result)
    {
        return BasicData.Format("presentation.PuzzleTravelQuote.text1", _result.Distance, PuzzleMapTime(_result.TravelMinutes), PuzzleTravelCost, _result.Cost, PuzzleTravelBreakdown(_result));
    }

    public static string PuzzleMapRegionLabel(PuzzleRegionData _region)
    {
        return string.IsNullOrEmpty(_region.specialtyTag) ? _region.displayName :
            BasicData.Format("presentation.PuzzleMapRegionLabel.text1", _region.displayName, PuzzleTagLabel(_region.specialtyTag));
    }

    public static string PuzzleMapStationName(string _name, bool _planningTarget)
    {
        return _planningTarget ? _name + " ★" : _name;
    }

    public static string PuzzleMapTime(long _minutes)
    {
        long hours = _minutes / PuzzleTimeManager.MINUTES_PER_HOUR;
        long minutes = _minutes % PuzzleTimeManager.MINUTES_PER_HOUR;
        return minutes == 0 ? BasicData.Format("presentation.PuzzleMapTime.text1", hours) : hours == 0 ? BasicData.Format("presentation.PuzzleMapTime.text2", minutes) : BasicData.Format("presentation.PuzzleMapTime.text3", hours, minutes);
    }

    public static Color PuzzleMapRegionColor(string _regionId)
    {
        return _regionId switch
        {
            "region.mine" => BasicData.Color("presentation.PuzzleMapRegionColor.color1"),
            "region.ranch" => BasicData.Color("presentation.PuzzleMapRegionColor.color2"),
            "region.industry" => BasicData.Color("presentation.PuzzleMapRegionColor.color3"),
            "region.capital" => BasicData.Color("presentation.PuzzleMapRegionColor.color4"),
            "region.coast" => BasicData.Color("presentation.PuzzleMapRegionColor.color5"),
            "region.farm" => BasicData.Color("presentation.PuzzleMapRegionColor.color6"),
            "region.common" => BasicData.Color("presentation.PuzzleMapRegionColor.color7"),
            _ => BasicData.Color("presentation.PuzzleMapRegionColor.color8")
        };
    }

    public static Color PuzzleMapInterchange => BasicData.Color("presentation.PuzzleMapInterchange.color1");

    public static Color PuzzleMapLineColor(string _regionId)
    {
        if (_regionId == "line.regional")
        {
            return PuzzleMapInterchange;
        }
        var color = PuzzleMapRegionColor(_regionId);
        color.a = 1f;
        return color;
    }

    internal static string PuzzleCityTitle(PuzzleCityData _city, PuzzleRegionData _region)
    {
        return BasicData.Format("presentation.PuzzleCityTitle.text1", _city.displayName, _region.displayName);
    }

    public static Color PuzzleItemIcon(float _alpha)
    {
        return BasicData.Color("presentation.PuzzleItemIcon.color1", _alpha);
    }
    public static Color PuzzleSynergyArrow => BasicData.Color("presentation.PuzzleSynergyArrow.color1");
    public static string PuzzleTrainTitleText => BasicData.Text("presentation.PuzzleTrainTitle");
    internal static string PuzzleTrainTitle(int _wagonNumber)
    {
        return BasicData.Format("presentation.PuzzleTrainTitle.text1", _wagonNumber);
    }
    public static string PuzzleAddWagonLabel(long _cost)
    {
        return BasicData.Format("presentation.PuzzleAddWagonLabel.text1", _cost);
    }
    internal static string PuzzleWagonPurchaseStatus(int _wagonNumber, long _cost)
    {
        return BasicData.Format("presentation.PuzzleWagonPurchaseStatus.text1", _cost, _wagonNumber);
    }
    public static Color PuzzleAddWagonBox => BasicData.Color("presentation.PuzzleAddWagonBox.color1");
    public static Color PuzzleWagonSelectedBorder => BasicData.Color("presentation.PuzzleWagonSelectedBorder.color1");
    public static Color PuzzleWagonDragHoverBorder => BasicData.Color("presentation.PuzzleWagonDragHoverBorder.color1");
    public static Color PuzzleWagonNormalBorder => BasicData.Color("presentation.PuzzleWagonNormalBorder.color1");
    public static string PuzzleMarketTitle => BasicData.Text("presentation.PuzzleMarketTitle");
    public static string PuzzleMoveGuide => BasicData.Text("presentation.PuzzleMoveGuide");
    public static string PuzzleTravelCost => BasicData.Text("presentation.PuzzleTravelCost");
    public static string PuzzleTagExpensive => BasicData.Text("presentation.PuzzleTagExpensive");
    public static string PuzzleTagCommon => BasicData.Text("presentation.PuzzleTagCommon");

    static readonly Dictionary<string, string> PUZZLE_TAG_LABELS = new Dictionary<string, string>
    {
        { "Cargo", "presentation.PuzzleTagCommon.text1" },
        { "Wood", "presentation.PuzzleTagCommon.text2" },
        { "Food", "presentation.PuzzleTagCommon.text3" },
        { "Grain", "presentation.PuzzleTagCommon.text4" },
        { "Supply", "presentation.PuzzleTagCommon.text5" },
        { "Water", "presentation.PuzzleTagCommon.text6" },
        { "Machinery", "presentation.PuzzleTagCommon.text7" },
        { "Metal", "presentation.PuzzleTagCommon.text8" },
        { "Fish", "presentation.PuzzleTagCommon.text9" },
        { "Salt", "presentation.PuzzleTagCommon.text10" },
        { "Meat", "presentation.PuzzleTagCommon.text11" },
        { "Animal", "presentation.PuzzleTagCommon.text12" },
        { "Fruit", "presentation.PuzzleTagCommon.text13" },
        { "Poo", "presentation.PuzzleTagCommon.text14" },
        { "Box", "presentation.PuzzleTagCommon.text15" },
        { PuzzleItemDefinition.BABY_TAG, "presentation.PuzzleTagCommon.baby" },

    };

    public static Color PuzzleBackground => BasicData.Color("presentation.PuzzleBackground.color1");
    public static Color PuzzleCell => BasicData.Color("presentation.PuzzleCell.color1");
    public static Color PuzzleHeading => BasicData.Color("presentation.PuzzleHeading.color1");
    public static Color PuzzlePiece => BasicData.Color("presentation.PuzzlePiece.color1");
    public static Color PuzzleGhost => BasicData.Color("presentation.PuzzleGhost.color1");
    public static Color PuzzleMoveTarget => BasicData.Color("presentation.PuzzleMoveTarget.color1");

    public static Color PuzzleTooltipHeaderBackground => BasicData.Color("presentation.PuzzleTooltipHeaderBackground.color1");
    public static Color PuzzleTooltipBodyBackground => BasicData.Color("presentation.PuzzleTooltipBodyBackground.color1");
    public static Color PuzzleTooltipDivider => BasicData.Color("presentation.PuzzleTooltipDivider.color1");
    public static Color PuzzleTooltipText => BasicData.Color("presentation.PuzzleTooltipText.color1");
    public static Color PuzzleTooltipGold => BasicData.Color("presentation.PuzzleTooltipGold.color1");
    public static Color PuzzleTooltipGreen => BasicData.Color("presentation.PuzzleTooltipGreen.color1");
    public static Color PuzzleTooltipRed => BasicData.Color("presentation.PuzzleTooltipRed.color1");

    static string TooltipLabelHtml()
    {
        return ColorUtility.ToHtmlStringRGB(PuzzleTooltipText);
    }

    internal static string PuzzleTooltipActualValueLabel()
    {
        return BasicData.Format("presentation.PuzzleTooltipActualValueLabel.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipActualValueValue(decimal _actualValue)
    {
        return BasicData.Format("presentation.PuzzleTooltipActualValueValue.text1", ColorUtility.ToHtmlStringRGB(PuzzleTooltipGold), _actualValue);
    }

    static Color RateValueColor(int _percent)
    {
        if (_percent > BasicData.Int("presentation.RateValueColor.number1"))
        {
            return PuzzleTooltipGreen;
        }
        if (_percent < BasicData.Int("presentation.RateValueColor.number2"))
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

    internal static string PuzzleTooltipSellRateLabel()
    {
        return BasicData.Format("presentation.PuzzleTooltipSellRateLabel.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipSellRateValue(int _percent)
    {
        return BasicData.Format("presentation.PuzzleTooltipSellRateValue.text1", ColorUtility.ToHtmlStringRGB(RateValueColor(_percent)), _percent);
    }

    internal static string PuzzleTooltipBuyRateLabel()
    {
        return BasicData.Format("presentation.PuzzleTooltipBuyRateLabel.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipBuyRateValue(int _percent)
    {
        return BasicData.Format("presentation.PuzzleTooltipBuyRateValue.text1", ColorUtility.ToHtmlStringRGB(RateValueColor(_percent)), _percent);
    }

    internal static string PuzzleTooltipFinalSellPriceLabel()
    {
        return BasicData.Format("presentation.PuzzleTooltipFinalSellPriceLabel.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFinalSellPriceValue(decimal _price, decimal _actualValue)
    {
        return BasicData.Format("presentation.PuzzleTooltipFinalSellPriceValue.text1", ColorUtility.ToHtmlStringRGB(PriceValueColor(_price, _actualValue)), _price);
    }

    internal static string PuzzleTooltipFinalBuyPriceLabel()
    {
        return BasicData.Format("presentation.PuzzleTooltipFinalBuyPriceLabel.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFinalBuyPriceValue(decimal _price, decimal _actualValue)
    {
        return BasicData.Format("presentation.PuzzleTooltipFinalBuyPriceValue.text1", ColorUtility.ToHtmlStringRGB(PriceValueColor(_price, _actualValue)), _price);
    }

    internal static string PuzzleTooltipEffectHeader()
    {
        return BasicData.Format("presentation.PuzzleTooltipEffectHeader.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipEffectDescription(string _description)
    {
        return BasicData.Format("presentation.PuzzleTooltipEffectDescription.text1", TooltipLabelHtml(), _description);
    }

    internal static string PuzzleTooltipNoEffect()
    {
        return BasicData.Format("presentation.PuzzleTooltipNoEffect.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFutureValueLabel(long _minutes, string _cityName)
    {
        return BasicData.Format("presentation.PuzzleTooltipFutureValueLabel.text1", TooltipLabelHtml(), _cityName, PuzzleMapTime(_minutes));
    }

    internal static string PuzzleTooltipFutureValueValue(decimal _futureValue, decimal _actualValue)
    {
        Color color = _futureValue > _actualValue
            ? PuzzleTooltipGreen
            : (_futureValue < _actualValue ? PuzzleTooltipRed : PuzzleTooltipGold);
        return BasicData.Format("presentation.PuzzleTooltipFutureValueValue.text1", ColorUtility.ToHtmlStringRGB(color), _futureValue);
    }

    internal static string PuzzleTooltipFreshnessLabel()
    {
        return BasicData.Format("presentation.PuzzleTooltipFreshnessLabel.text1", TooltipLabelHtml());
    }

    internal static string PuzzleTooltipFreshnessValue(int _current)
    {
        return BasicData.Format("presentation.PuzzleTooltipFreshnessValue.text1", ColorUtility.ToHtmlStringRGB(PuzzleTooltipGreen), _current);
    }

    internal static string PuzzleValueText(PuzzleValue _value)
    {
        return BasicData.Format("presentation.PuzzleValueText.text1", _value.FinalValue, _value.BaseValue);
    }

    internal static string PuzzleItemDetails(PuzzleItemDefinition _item, PuzzleValue _value, decimal _actualValue,
        PuzzleTradeRate _rate)
    {
        if (_item == null)
        {
            return string.Empty;
        }

        string details =
            BasicData.Format("presentation.PuzzleItemDetails.text1", _item.DisplayName, PuzzleValueText(_value), _actualValue, _item.Description);
        if (_rate.BuyPercent > 0 || _rate.SellPercent > 0)
        {
            details += "\n" + PuzzleTradeRateText(_rate);
        }
        if (_value.SynergyBonuses.Count == 0)
        {
            return BasicData.Format("presentation.PuzzleItemDetails.text2", details);
        }

        var bonuses = _value.SynergyBonuses.Select(bonus =>
            BasicData.Format("presentation.PuzzleItemDetails.text3", bonus.NeighborName, bonus.Percent, bonus.Amount));
        return BasicData.Format("presentation.PuzzleItemDetails.text4", details, string.Join("  ·  ", bonuses));
    }

    internal static string PuzzleTradeRateText(PuzzleTradeRate _rate)
    {
        string rule = string.IsNullOrEmpty(_rate.Tag) ? BasicData.Text("presentation.PuzzleTradeRateText.text2") : _rate.Tag;
        return BasicData.Format("presentation.PuzzleTradeRateText.text1", rule, _rate.BuyPercent, _rate.SellPercent);
    }

    internal static string PuzzleFunds(long _funds)
    {
        return BasicData.Format("presentation.PuzzleFunds.text1", _funds);
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
            if (rule.buyPercent > BasicData.Int("presentation.PuzzleCityTagPreferences.number1") && rule.sellPercent > BasicData.Int("presentation.PuzzleCityTagPreferences.number2"))
            {
                expensive.Add(rule.tag);
            }
            else if (rule.buyPercent < BasicData.Int("presentation.if.number5") && rule.sellPercent < BasicData.Int("presentation.if.number6"))
            {
                common.Add(rule.tag);
            }
        }

        string commonText = string.Join(" · ", common.OrderBy(tag => tag, StringComparer.Ordinal).Select(PuzzleTagLabel));
        string rareText = string.Join(" · ", expensive.OrderBy(tag => tag, StringComparer.Ordinal).Select(PuzzleTagLabel));
        string commonColor = ColorUtility.ToHtmlStringRGB(PuzzleMapVisited);
        string rareColor = ColorUtility.ToHtmlStringRGB(PuzzleMapCurrent);
        return BasicData.Format("presentation.PuzzleCityTagPreferences.text1", commonColor, (string.IsNullOrEmpty(commonText) ? BasicData.Text("presentation.PuzzleCityTagPreferences.text3") : commonText)) +
            BasicData.Format("presentation.PuzzleCityTagPreferences.text2", rareColor, (string.IsNullOrEmpty(rareText) ? BasicData.Text("presentation.PuzzleCityTagPreferences.text4") : rareText));

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
            "wood" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color1"),       // 목재: 팔레트같은 따뜻한 브라운
            "metal" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color2"),     // 금속: 메탈릭 스틸 슬레이트
            "machinery" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color3"),   // 기계: 산업용 앰버/브론즈
            "food" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color4"),       // 식품/음식: 수확 웜 오렌지
            "fish" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color5"),       // 물고기/수산물: 딥 오션 블루
            "water" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color6"),     // 물/식수: 맑은 아쿠아 블루
            "salt" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color7"),     // 소금: 미네랄 실버 슬레이트
            "box" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color8"),
            "supply" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color9"),     // 보급품: 포레스트 그린
            "cargo" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color10"),      // 화물/상자: 가죽/카톤 브라운
            "meat" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color11"),       // 고기: 딥 레드
            "animal" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color12"),    // 가축: 탄/레더 탠
            "fruit" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color13"),      // 과일: 사과 레드
            "poo" => BasicData.Color("presentation.PuzzleTagBackgroundColor.color14"),         // 똥: 다크 브라운
            _ => BasicData.Color("presentation.PuzzleTagBackgroundColor.color15")
        };
    }

    public static Color PuzzleTagTextColor(string _tag)
    {
        // 다크/미디엄 톤 테마 배경 위에서 선명하게 읽히는 흰색
        return BasicData.Color("presentation.PuzzleTagTextColor.color1");
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
            "wood" => BasicData.Int("presentation.tagSort.wood"),
            "machinery" => BasicData.Int("presentation.tagSort.machinery"),
            "metal" => BasicData.Int("presentation.GetTagSortWeight.number1"),
            "fish" => 4,
            "food" => BasicData.Int("presentation.GetTagSortWeight.number2"),
            "meat" => BasicData.Int("presentation.GetTagSortWeight.number3"),
            "fruit" => BasicData.Int("presentation.GetTagSortWeight.number4"),
            "animal" => BasicData.Int("presentation.GetTagSortWeight.number5"),
            "water" => BasicData.Int("presentation.GetTagSortWeight.number6"),
            "salt" => BasicData.Int("presentation.GetTagSortWeight.number7"),
            "box" => BasicData.Int("presentation.GetTagSortWeight.number8"),
            "supply" => BasicData.Int("presentation.GetTagSortWeight.number9"),
            "cargo" => BasicData.Int("presentation.GetTagSortWeight.number10"),
            "poo" => BasicData.Int("presentation.GetTagSortWeight.number11"),
            _ => BasicData.Int("presentation.GetTagSortWeight.number12")
        };
    }

    internal static string PuzzleClearTitle => BasicData.Text("presentation.PuzzleClearTitle");
    internal static string PuzzleGrowthBlocked => BasicData.Text("presentation.PuzzleGrowthBlocked");
    internal static string PuzzleBabyCannotButcher => BasicData.Text("presentation.PuzzleBabyCannotButcher");

    internal static string PuzzleClearButton(long _cost)
    {
        return BasicData.Format("presentation.PuzzleClearButton.text1", _cost);
    }

    internal static string PuzzleClearSummary(long _funds)
    {
        return BasicData.Format("presentation.PuzzleClearSummary.text1", _funds);
    }

    internal static string PuzzleRunStatusText(PuzzleRunStatus _status)
    {
        return _status switch
        {
            PuzzleRunStatus.Won => PuzzleClearTitle,
            PuzzleRunStatus.Lost => BasicData.Text("presentation.PuzzleRunStatusText.text1"),
            _ => string.Empty
        };
    }

    // 이동이 차단된 이유. 비용 부족은 패배가 아니라 상태 유지고, 금액은 노선 기본값과 기차 보유 칸으로 갈린다.
    internal static string PuzzleTravelStatus(PuzzleTravelResult _result)
    {
        return _result.Failure switch
        {
            PuzzleTravelFailure.SameCity => BasicData.Text("presentation.PuzzleTravelStatus.text3"),
            PuzzleTravelFailure.InvalidCity => BasicData.Text("presentation.PuzzleTravelStatus.text4"),
            PuzzleTravelFailure.InsufficientFunds => BasicData.Format("presentation.PuzzleTravelStatus.text1", PuzzleTravelCost, _result.Cost)
                + BasicData.Format("presentation.PuzzleTravelStatus.text2", PuzzleTravelBreakdown(_result)),
            PuzzleTravelFailure.CostOverflow => BasicData.Text("presentation.PuzzleTravelStatus.text5"),
            PuzzleTravelFailure.TimeOverflow => BasicData.Text("presentation.PuzzleTravelStatus.text6"),
            PuzzleTravelFailure.GrowthBlocked => PuzzleGrowthBlocked,
            PuzzleTravelFailure.MissingRoute => BasicData.Text("presentation.PuzzleTravelStatus.text7"),
            PuzzleTravelFailure.NotAvailable => BasicData.Text("presentation.PuzzleTravelStatus.text8"),
            PuzzleTravelFailure.Blocked => BasicData.Text("presentation.PuzzleTravelStatus.text9"),
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

        string direction = _result.Direction == PuzzleTradeDirection.Buy ? BasicData.Text("presentation.PuzzleTradeStatus.text6") : BasicData.Text("presentation.PuzzleTradeStatus.text7");
        string quote = BasicData.Format("presentation.PuzzleTradeStatus.text1", _item.DisplayName, direction);
        if (_result.Placement.Failure != PuzzlePlacementFailure.StaleSource &&
            _result.TradeFailure != PuzzleTradeFailure.PriceOverflow)
        {
            quote += BasicData.Format("presentation.PuzzleTradeStatus.text2", _result.Price);
        }
        if (_result.CanDrop)
        {
            return _committed
                ? BasicData.Format("presentation.PuzzleTradeStatus.text3", _item.DisplayName, direction, _result.Price)
                : BasicData.Format("presentation.PuzzleTradeStatus.text4", _item.DisplayName, direction, _result.Price);
        }

        string reason = _result.TradeFailure switch
        {
            PuzzleTradeFailure.Disabled => BasicData.Text("presentation.PuzzleTradeStatus.text8"),
            PuzzleTradeFailure.MarketClosed => PuzzleNightMarket,
            PuzzleTradeFailure.InsufficientFunds => BasicData.Text("presentation.PuzzleTradeStatus.text9"),
            PuzzleTradeFailure.PriceOverflow => BasicData.Text("presentation.PuzzleTradeStatus.text10"),
            PuzzleTradeFailure.BalanceOverflow => BasicData.Text("presentation.PuzzleTradeStatus.text11"),
            _ => _result.Placement.Failure switch
            {
                PuzzlePlacementFailure.StaleSource => BasicData.Text("presentation.PuzzleTradeStatus.text12"),
                PuzzlePlacementFailure.CellOutsideBoard => BasicData.Text("presentation.PuzzleTradeStatus.text13"),
                PuzzlePlacementFailure.CellOccupied => BasicData.Text("presentation.PuzzleTradeStatus.text14"),
                PuzzlePlacementFailure.NoSpace => BasicData.Text("presentation.PuzzleTradeStatus.text15"),
                _ => BasicData.Text("presentation.PuzzleTradeStatus.text16")
            }
        };
        return BasicData.Format("presentation.PuzzleTradeStatus.text5", quote, reason);
    }

    public static Color PuzzlePieceOutline(float _alpha)
    {
        return BasicData.Color("presentation.PuzzlePieceOutline.color1", _alpha);
    }

    public static Color PuzzleOutline(bool _canPlace)
    {
        return _canPlace ? BasicData.Color("presentation.PuzzleOutline.color1") : BasicData.Color("presentation.PuzzleOutline.color2");
    }

    // 범주 팔레트. 새 범주를 추가하면 여기 색과 Title을 함께 추가한다.
    public static Color Color(EffectCategory _category)
    {
        return _category switch
        {
            EffectCategory.Offense => BasicData.Color("presentation.Color.color1"),
            EffectCategory.Mobility => BasicData.Color("presentation.Color.color2"),
            EffectCategory.Defense => BasicData.Color("presentation.Color.color3"),
            EffectCategory.BaseStat => BasicData.Color("presentation.Color.color4"),
            _ => BasicData.Color("presentation.Color.color5"),
        };
    }

    public static string Title(EffectCategory _category)
    {
        return _category switch
        {
            EffectCategory.Offense => BasicData.Text("presentation.Title.text1"),
            EffectCategory.Mobility => BasicData.Text("presentation.Title.text2"),
            EffectCategory.Defense => BasicData.Text("presentation.Title.text3"),
            EffectCategory.BaseStat => BasicData.Text("presentation.Title.text4"),
            _ => BasicData.Text("presentation.Title.text5"),
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
        return _upgrade == null ? BasicData.Text("presentation.Name.text1") : string.IsNullOrEmpty(_upgrade.displayName) ? _upgrade.id : _upgrade.displayName;
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
    static string ValueTag => BasicData.Text("presentation.ValueTag");
    static string DimTag => BasicData.Text("presentation.DimTag");
    public static string Accent(string _value)
    {
        return BasicData.Format("presentation.Accent.text1", ValueTag, _value);
    }
    public static string Dim(string _value)
    {
        return BasicData.Format("presentation.Dim.text1", DimTag, _value);
    }
    public static string Heading(string _title, Color _color)
    {
        return BasicData.Format("presentation.Heading.text1", Hex(_color), _title);
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
            return Dim(BasicData.Text("presentation.Card.text2"));
        }
        return BasicData.Format("presentation.Card.text1", Heading(Name(_upgrade), Color(_upgrade)), Dim(Title(_upgrade)), Describe(_upgrade));
    }
}
