using System.Collections.Generic;
using System.Linq;

public static class PuzzlePieceEffects
{
    public static bool HasAnyTag(IEnumerable<string> _tags, string _commaSeparated)
    {
        return !string.IsNullOrEmpty(_commaSeparated) &&
            _commaSeparated.Split(',').Any(_tag => _tags.Contains(_tag.Trim()));
    }

    public static bool EligibleTarget(IEnumerable<string> _tags, PuzzleSynergyRuleData _rule, string _wasteTag)
    {
        return !_tags.Contains(_wasteTag) && (string.IsNullOrEmpty(_rule.excludedTargetTag)
            ? _tags.Contains(_rule.targetTag) : !HasAnyTag(_tags, _rule.excludedTargetTag));
    }

    public static bool EligibleSource(IEnumerable<string> _tags, PuzzleSynergyRuleData _rule)
    {
        return _tags.Contains(_rule.neighborTag) && !HasAnyTag(_tags, _rule.excludedNeighborTag);
    }

    public static bool IsTarget(PuzzleItemDefinition _item, PuzzleSynergyRuleData _rule, string _wasteTag)
    {
        return _item.ReceivesSynergy(_rule.id) && EligibleTarget(_item.Tags, _rule, _wasteTag);
    }

    public static bool IsSource(PuzzleItemDefinition _item, PuzzleSynergyRuleData _rule)
    {
        return _item.EmitsSynergy(_rule.id) && EligibleSource(_item.Tags, _rule);
    }
}
