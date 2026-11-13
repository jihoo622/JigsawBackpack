using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public static class ItemEditorEffectMigration
{
    public static IReadOnlyList<string> Migrate(JObject _puzzle, JObject _synergy)
    {
        var before = (JObject)_puzzle.DeepClone();
        var candidate = (JObject)_puzzle.DeepClone();
        var data = _synergy.ToObject<PuzzleSynergyTableData>();
        foreach (var piece in candidate["pieces"].OfType<JObject>())
        {
            bool missingRules = IsMissing(piece["synergyEffects"]);
            bool missingPreservation = IsMissing(piece["preservationEffect"]);
            if (missingRules != missingPreservation)
            {
                throw new InvalidOperationException((string)piece["id"] + ": 효과 계약이 부분 누락되어 자동 이행할 수 없습니다.");
            }
            if (!missingRules)
            {
                continue;
            }
            var tags = piece["tags"].Values<string>().ToArray();
            var effects = new JArray();
            foreach (var rule in data.rules.OrderBy(_rule => _rule.id, StringComparer.Ordinal))
            {
                bool target = PuzzlePieceEffects.EligibleTarget(tags, rule, data.animals.wasteTag);
                bool source = PuzzlePieceEffects.EligibleSource(tags, rule);
                if (target || source)
                {
                    effects.Add(new JObject
                    {
                        ["ruleId"] = rule.id,
                        ["target"] = target,
                        ["source"] = source
                    });
                }
            }
            bool enabled = tags.Contains(data.freshness.refrigerationTag);
            int percent = LegacyPreservation(tags, data.freshness);
            piece["synergyEffects"] = effects;
            piece["preservationEffect"] = new JObject
            {
                ["enabled"] = enabled,
                ["reductionPercent"] = percent
            };
        }
        var report = Compare(before, candidate, _synergy);
        _puzzle["pieces"] = candidate["pieces"].DeepClone();
        return report;
    }

    public static bool IsMissing(JToken _token)
    {
        return _token == null || _token.Type == JTokenType.Null;
    }

    // 이행 경계의 구 판정은 런타임의 새 선택 판정과 분리한다. 대상 면역·제외 태그와
    // 소금 분기를 포함해 보존 여부를 대조하며, 이 함수는 런타임 폴백으로 사용하지 않는다.
    static bool LegacyTarget(string[] _tags, PuzzleSynergyRuleData _rule, string _wasteTag)
    {
        if (_tags.Contains(_wasteTag))
        {
            return false;
        }
        return string.IsNullOrEmpty(_rule.excludedTargetTag) ? _tags.Contains(_rule.targetTag)
            : !_rule.excludedTargetTag.Split(',').Any(_tag => _tags.Contains(_tag.Trim()));
    }

    static bool LegacySource(string[] _tags, PuzzleSynergyRuleData _rule)
    {
        return _tags.Contains(_rule.neighborTag) && (string.IsNullOrEmpty(_rule.excludedNeighborTag) ||
            !_rule.excludedNeighborTag.Split(',').Any(_tag => _tags.Contains(_tag.Trim())));
    }

    static int LegacyPreservation(string[] _tags, PuzzleFreshnessData _freshness)
    {
        if (!_tags.Contains(_freshness.refrigerationTag))
        {
            return 0;
        }
        int percent = _tags.Contains("Salt") ? _freshness.saltPreservationReductionPercent : _freshness.refrigerationReductionPercent;
        if (percent < 0 || percent > 100)
        {
            throw new InvalidOperationException("기존 신선 효과의 이행 원본 수치가 유효하지 않습니다.");
        }
        return percent;
    }

    static bool Selected(PuzzlePieceData _piece, PuzzleSynergyRuleData _rule, bool _target)
    {
        return _piece.synergyEffects.Any(_effect => _effect.ruleId == _rule.id && (_target ? _effect.target : _effect.source));
    }

    public static IReadOnlyList<string> Compare(JObject _before, JObject _candidate, JObject _synergy)
    {
        var oldPieces = _before["pieces"].OfType<JObject>().ToDictionary(_piece => (string)_piece["id"], StringComparer.Ordinal);
        var pieces = _candidate["pieces"].ToObject<PuzzlePieceData[]>();
        var rules = _synergy.ToObject<PuzzleSynergyTableData>();
        if (pieces.Length != oldPieces.Count || !new HashSet<string>(oldPieces.Keys, StringComparer.Ordinal)
            .SetEquals(pieces.Select(_piece => _piece.id)))
        {
            throw new InvalidOperationException("효과 이행에서 품목을 추가·삭제하거나 ID를 바꿀 수 없습니다.");
        }
        var beforeOther = (JObject)_before.DeepClone();
        var afterOther = (JObject)_candidate.DeepClone();
        beforeOther.Remove("pieces");
        afterOther.Remove("pieces");
        if (!JToken.DeepEquals(beforeOther, afterOther))
        {
            throw new InvalidOperationException("효과 이행에서 품목 외의 데이터를 바꿀 수 없습니다.");
        }
        var migrated = pieces.Where(_piece => oldPieces.TryGetValue(_piece.id, out var old) &&
            IsMissing(old["synergyEffects"]) && IsMissing(old["preservationEffect"])).ToArray();
        var migratedIds = new HashSet<string>(migrated.Select(_piece => _piece.id), StringComparer.Ordinal);
        var originals = oldPieces.ToDictionary(_pair => _pair.Key, _pair => _pair.Value.ToObject<PuzzlePieceData>(), StringComparer.Ordinal);
        foreach (var piece in pieces.Where(_piece => !migratedIds.Contains(_piece.id)))
        {
            if (!JToken.DeepEquals(oldPieces[piece.id], _candidate["pieces"].OfType<JObject>().Single(_piece => (string)_piece["id"] == piece.id)))
            {
                throw new InvalidOperationException(piece.id + ": 이미 명시된 효과는 다시 이행하거나 덮어쓰지 않습니다.");
            }
        }
        var report = new List<string>();
        foreach (var piece in migrated)
        {
            var original = oldPieces[piece.id];
            var current = _candidate["pieces"].OfType<JObject>().Single(_piece => (string)_piece["id"] == piece.id);
            var withoutEffects = (JObject)current.DeepClone();
            withoutEffects.Remove("synergyEffects");
            withoutEffects.Remove("preservationEffect");
            var comparable = (JObject)original.DeepClone();
            comparable.Remove("synergyEffects");
            comparable.Remove("preservationEffect");
            if (!JToken.DeepEquals(comparable, withoutEffects))
            {
                throw new InvalidOperationException(piece.id + ": 이행 과정에서 효과 이외의 정의가 바뀌었습니다.");
            }
            var errors = new List<string>();
            new GameTableRules(errors).ValidatePieceEffects(piece, rules.rules, rules.freshness, rules.animals.wasteTag, piece.id);
            if (errors.Count != 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
            foreach (var rule in rules.rules)
            {
                if (LegacyTarget(piece.tags, rule, rules.animals.wasteTag) !=
                    (Selected(piece, rule, true) && PuzzlePieceEffects.EligibleTarget(piece.tags, rule, rules.animals.wasteTag)) ||
                    LegacySource(piece.tags, rule) != (Selected(piece, rule, false) && PuzzlePieceEffects.EligibleSource(piece.tags, rule)))
                {
                    throw new InvalidOperationException(piece.id + ": 이행 전후 규칙 역할이 다릅니다: " + rule.id);
                }
            }
            int oldReduction = LegacyPreservation(piece.tags, rules.freshness);
            if (piece.preservationEffect.reductionPercent != oldReduction)
            {
                throw new InvalidOperationException(piece.id + ": 이행 전후 신선 감소율이 다릅니다.");
            }
            report.Add(piece.id + ": 받는 규칙 " + piece.synergyEffects.Count(_effect => _effect.target) +
                ", 주는 규칙 " + piece.synergyEffects.Count(_effect => _effect.source) + ", 신선 감소율 " + oldReduction + "%");
        }
        long combinations = 0;
        foreach (var target in pieces)
        {
            foreach (var source in pieces)
            {
                foreach (var rule in rules.rules)
                {
                    var oldTarget = originals[target.id];
                    var oldSource = originals[source.id];
                    bool targetBefore = LegacyTarget(oldTarget.tags, rule, rules.animals.wasteTag) &&
                        (migratedIds.Contains(target.id) || Selected(oldTarget, rule, true));
                    bool sourceBefore = LegacySource(oldSource.tags, rule) &&
                        (migratedIds.Contains(source.id) || Selected(oldSource, rule, false));
                    bool oldMatch = targetBefore && sourceBefore;
                    bool newMatch = Selected(target, rule, true) && Selected(source, rule, false) &&
                        PuzzlePieceEffects.EligibleTarget(target.tags, rule, rules.animals.wasteTag) &&
                        PuzzlePieceEffects.EligibleSource(source.tags, rule);
                    decimal oldPercent = oldMatch ? rule.multiplierPercent - 100m : 0m;
                    decimal newPercent = newMatch ? rule.multiplierPercent - 100m : 0m;
                    if (oldPercent != newPercent)
                    {
                        throw new InvalidOperationException("이행 전후 품목 쌍의 효과 수치가 다릅니다: " + target.id + "/" + source.id + "/" + rule.id);
                    }
                    combinations++;
                }
            }
        }
        if (migrated.Length != 0)
        {
            report.Insert(0, "명시적 효과 이행 " + migrated.Length + "품목 · " + combinations +
                "개 품목 쌍/규칙 대조. scope·spatialRange·배율·정산 주기는 공통 원본을 유지합니다.");
        }
        return report;
    }
}
