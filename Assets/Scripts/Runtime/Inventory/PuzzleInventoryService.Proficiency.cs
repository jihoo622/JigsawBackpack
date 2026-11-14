using System;
using System.Collections.Generic;
using System.Linq;

// 태그별 거래 숙련도. 생산 태그별 경험치와 시장에서 산 화물의 구입 마을을 세션 상태로 가진다.
// 경험치 획득률·레벨 기준·거래 이득 수치는 TrainventoryProficiencyTableData가 원본이다.
public sealed partial class PuzzleInventoryService
{
    readonly Dictionary<string, long> proficiencyXp = new Dictionary<string, long>(StringComparer.Ordinal);
    // 화물을 얻은 마을·방문과 구입가. 인스턴스 ID는 이동·성장·만료 변환에도 유지된다.
    // 기록이 없는 화물(번식 등 이동 중 생긴 것)은 어느 마을에서도 다른 마을 화물로 본다.
    readonly Dictionary<int, TrainventoryTradeOrigin> tradeOrigins = new Dictionary<int, TrainventoryTradeOrigin>();

    // 평판은 모든 숙련 레벨의 합이다. 따로 쌓거나 깎지 않는다.
    internal int Reputation => tables.ProficiencyTags.Sum(ProficiencyLevel);

    void ResetProficiency()
    {
        proficiencyXp.Clear();
        tradeOrigins.Clear();
    }

    internal long ProficiencyXp(string _tag)
    {
        return proficiencyXp.TryGetValue(_tag, out long xp) ? xp : 0;
    }

    internal int ProficiencyLevel(string _tag)
    {
        return LevelForXp(ProficiencyXp(_tag));
    }

    // 숙련 대상 태그(지역 생산 태그) 기본 순서. id 오름차순이다.
    internal IReadOnlyList<string> ProficiencyTags => tables.ProficiencyTags;

    // 태그 하나의 숙련 표시값. 레벨 구간·다음 경계·이득은 테이블 기준 계산을 그대로 쓴다.
    internal TrainventoryProficiencyStanding ProficiencyStanding(string _tag)
    {
        long xp = ProficiencyXp(_tag);
        int level = LevelForXp(xp);
        return new TrainventoryProficiencyStanding(_tag, level, xp, ProficiencyLevelStartXp(level), NextProficiencyXp(level),
            ProficiencyBonusPercent(level));
    }

    // 숙련 대상 태그 전체를 레벨 내림차순 → 경험치 내림차순 → 태그 기본 순서(id 오름차순)로 돌려준다.
    // 경험치가 0인 태그도 포함한다(수첩 숙련 탭 표시용).
    internal IReadOnlyList<string> RankedProficiencyTags => tables.ProficiencyTags
        .Select((_tag, _index) => (tag: _tag, index: _index))
        .OrderByDescending(_entry => ProficiencyLevel(_entry.tag))
        .ThenByDescending(_entry => ProficiencyXp(_entry.tag))
        .ThenBy(_entry => _entry.index)
        .Select(_entry => _entry.tag)
        .ToArray();

    int LevelForXp(long _xp)
    {
        var proficiency = tables.Proficiency;
        var thresholds = proficiency.levelThresholds;
        int level = 0;
        while (level < thresholds.Length && _xp >= thresholds[level])
        {
            level++;
        }
        if (level < thresholds.Length)
        {
            return level;
        }
        long extra = (_xp - thresholds[thresholds.Length - 1]) / proficiency.extraLevelXp;
        return (int)Math.Min(int.MaxValue, level + extra);
    }

    // 지정 레벨에서 다음 레벨이 되는 누적 경험치. 마지막 기준 이후는 extraLevelXp씩 늘어난다.
    internal long NextProficiencyXp(int _level)
    {
        var proficiency = tables.Proficiency;
        var thresholds = proficiency.levelThresholds;
        if (_level < thresholds.Length)
        {
            return thresholds[_level];
        }
        decimal next = thresholds[thresholds.Length - 1] + (decimal)(_level - thresholds.Length + 1) * proficiency.extraLevelXp;
        return next > long.MaxValue ? long.MaxValue : (long)next;
    }

    // 지정 레벨이 시작되는 누적 경험치. 0레벨은 0부터 시작한다.
    internal long ProficiencyLevelStartXp(int _level)
    {
        return _level <= 0 ? 0 : NextProficiencyXp(_level - 1);
    }

    internal decimal ProficiencyBonusPercent(int _level)
    {
        return (decimal)tables.Proficiency.bonusPercentPerLevel * _level;
    }

    // 화물에 붙은 숙련 대상(생산) 태그. id 오름차순이다.
    IReadOnlyList<string> ProficiencyTagsOf(PuzzleItemDefinition _definition)
    {
        return _definition.Tags
            .Where(_tag => tables.ProficiencyTags.Contains(_tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(_tag => _tag, StringComparer.Ordinal)
            .ToArray();
    }

    // 거래 이득에 쓰는 레벨. 생산 태그가 여럿이면 가장 높은 레벨 하나만 쓴다. 의뢰 화물도 일반 화물과 같다.
    internal int TradeProficiencyLevel(PuzzleItemDefinition _definition)
    {
        if (_definition == null)
        {
            return 0;
        }
        return ProficiencyTagsOf(_definition).Select(ProficiencyLevel).DefaultIfEmpty(0).Max();
    }

    // 같은 방문 중 산 마을에 되파는 것은 구매 취소다. 품목이 그대로면 낸 금액을 그대로 돌려받는다.
    internal bool IsPurchaseCancel(PuzzlePlacement _item, out long _refund)
    {
        _refund = 0;
        if (_item == null || !tradeOrigins.TryGetValue(_item.InstanceId, out var origin) || !origin.Purchased ||
            origin.Visit != CityMoveCount ||
            !string.Equals(origin.CityId, CurrentCityId, StringComparison.Ordinal) ||
            !string.Equals(origin.PieceId, _item.Definition.Id, StringComparison.Ordinal))
        {
            return false;
        }
        _refund = origin.Paid;
        return true;
    }

    // 도시 배율까지 반영한 최종 거래가에 숙련 이득을 적용한다. 구매 취소는 낸 금액이 곧 판매가다.
    bool TryApplyProficiency(PuzzlePlacement _item, PuzzleTradeDirection _direction, ref long _price)
    {
        if (_direction == PuzzleTradeDirection.Sell && IsPurchaseCancel(_item, out long refund))
        {
            _price = refund;
            return true;
        }
        if (_price <= 0)
        {
            return true;
        }

        int level = TradeProficiencyLevel(_item.Definition);
        if (level == 0)
        {
            return true;
        }
        decimal percent = ProficiencyBonusPercent(level);
        decimal factor = _direction == PuzzleTradeDirection.Buy
            ? Math.Max(0m, 100m - percent) / 100m
            : (100m + percent) / 100m;
        decimal adjusted = Math.Round(_price * factor, MidpointRounding.AwayFromZero);
        if (adjusted > long.MaxValue)
        {
            return false;
        }
        _price = (long)adjusted;
        return true;
    }

    // 시장에서 산 화물의 구입 마을·방문·품목과 낸 금액을 기록한다.
    void RecordPurchase(PuzzlePlacement _item, long _paid)
    {
        tradeOrigins[_item.InstanceId] = new TrainventoryTradeOrigin(CurrentCityId, CityMoveCount, _item.Definition.Id,
            _paid, true);
    }

    // 작업대(도축·개조·정제) 산출물과 의뢰 화물은 그 마을에서 얻은 화물로 본다. 이미 기록이 있으면 유지한다.
    void RecordLocalOrigin(int _instanceId)
    {
        if (!tradeOrigins.ContainsKey(_instanceId))
        {
            tradeOrigins.Add(_instanceId, new TrainventoryTradeOrigin(CurrentCityId, CityMoveCount, null, 0, false));
        }
    }

    // 판매 경험치. 다른 마을에서 얻은 화물(기록이 없거나 다른 마을 출신)만 판매 금액에 비례해 받는다.
    // 얻은 마을에서 팔면(구매 취소 포함) 경험치가 없다.
    void GrantSaleProficiency(PuzzlePlacement _item, long _price)
    {
        bool local = tradeOrigins.TryGetValue(_item.InstanceId, out var origin) &&
            string.Equals(origin.CityId, CurrentCityId, StringComparison.Ordinal);
        tradeOrigins.Remove(_item.InstanceId);
        if (local || _price <= 0)
        {
            return;
        }
        long xp = (long)Math.Min(long.MaxValue, (decimal)_price * tables.Proficiency.xpPercentOfSalePrice / 100m);
        GrantProficiency(ProficiencyTagsOf(_item.Definition), xp, TrainventoryProficiencySource.Sale, _item.InstanceId, 0);
    }

    // 의뢰 완료 경험치. 의뢰 보상 금액에 비례해 의뢰 화물의 생산 태그에 준다.
    // 화물에 생산 태그가 없으면 의뢰 틀의 fallbackProficiencyTags에 준다.
    void GrantQuestProficiency(TrainventoryQuest _quest)
    {
        long xp = (long)Math.Min(long.MaxValue, (decimal)_quest.Reward * tables.Quest.proficiencyXpPercentOfReward / 100m);
        var tags = ProficiencyTagsOf(definitions[_quest.PieceId]);
        if (tags.Count == 0)
        {
            var template = tables.CourierQuests.FirstOrDefault(_template =>
                string.Equals(_template.id, _quest.TemplateId, StringComparison.Ordinal));
            tags = (template?.fallbackProficiencyTags ?? Array.Empty<string>())
                .Where(_tag => tables.ProficiencyTags.Contains(_tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(_tag => _tag, StringComparer.Ordinal)
                .ToArray();
        }
        GrantProficiency(tags, xp, TrainventoryProficiencySource.Quest, _quest.CargoInstanceId, _quest.Id);
    }

    // 경험치를 태그 수로 나눠 준다. 나머지는 id 오름차순 앞 태그부터 1씩 더한다.
    void GrantProficiency(IReadOnlyList<string> _tags, long _xp, TrainventoryProficiencySource _source, int _instanceId,
        int _questId)
    {
        if (_tags.Count == 0 || _xp <= 0)
        {
            return;
        }
        long share = _xp / _tags.Count;
        long remainder = _xp % _tags.Count;
        for (int i = 0; i < _tags.Count; i++)
        {
            long gained = share + (i < remainder ? 1 : 0);
            if (gained <= 0)
            {
                continue;
            }
            string tag = _tags[i];
            long before = ProficiencyXp(tag);
            int previousLevel = LevelForXp(before);
            long after = before > long.MaxValue - gained ? long.MaxValue : before + gained;
            proficiencyXp[tag] = after;
            GameEvents.RaiseProficiencyChanged(new GameEvents.ProficiencyInfo(this, tag, _source.ToString(), gained, after,
                previousLevel, LevelForXp(after), _instanceId, _questId, ElapsedTime));
        }
    }
}

// 수첩 숙련 탭·기차 카드 칩이 함께 쓰는 태그별 숙련 표시값.
internal readonly struct TrainventoryProficiencyStanding
{
    internal string Tag { get; }
    internal int Level { get; }
    internal long Xp { get; }
    // 현재 레벨이 시작된 누적 경험치와 다음 레벨 경계.
    internal long LevelStartXp { get; }
    internal long NextXp { get; }
    internal decimal BonusPercent { get; }

    internal TrainventoryProficiencyStanding(string _tag, int _level, long _xp, long _levelStartXp, long _nextXp,
        decimal _bonusPercent)
    {
        Tag = _tag;
        Level = _level;
        Xp = _xp;
        LevelStartXp = _levelStartXp;
        NextXp = _nextXp;
        BonusPercent = _bonusPercent;
    }

    internal bool HasXp => Xp > 0;

    // 현재 레벨 구간 진행도(0~1).
    internal float Progress
    {
        get
        {
            long span = NextXp - LevelStartXp;
            if (span <= 0)
            {
                return 1f;
            }
            return Math.Max(0f, Math.Min(1f, (float)((double)(Xp - LevelStartXp) / span)));
        }
    }
}

internal enum TrainventoryProficiencySource
{
    Sale,
    Quest
}

internal readonly struct TrainventoryTradeOrigin
{
    internal string CityId { get; }
    // 얻은 방문(CityMoveCount). 같은 방문 중일 때만 구매 취소가 된다.
    internal int Visit { get; }
    // 산 품목. 만료로 다른 품목(똥 등)이 되면 구매 취소가 아니다. 구입이 아니면 비어 있다.
    internal string PieceId { get; }
    internal long Paid { get; }
    internal bool Purchased { get; }

    internal TrainventoryTradeOrigin(string _cityId, int _visit, string _pieceId, long _paid, bool _purchased)
    {
        CityId = _cityId;
        Visit = _visit;
        PieceId = _pieceId;
        Paid = _paid;
        Purchased = _purchased;
    }
}
