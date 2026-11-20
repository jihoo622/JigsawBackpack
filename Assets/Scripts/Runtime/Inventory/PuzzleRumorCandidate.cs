using System;
using System.Collections.Generic;
using System.Linq;

// 술집 소문의 종류. 문장이나 태그 유무로 사후 추론하지 않고 후보를 만들 때 확정한다.
public enum PuzzleRumorKind
{
    Specialty,
    RareDemand,
    Action,
    Preference,
    Wagon
}

// 소문 후보에는 대상 마을과 종류별 ID를 보관한다.
internal readonly struct PuzzleRumorCandidate
{
    internal PuzzleRumorKind Kind { get; }
    internal string CityId { get; }
    internal string ItemId { get; }
    internal string Tag { get; }
    internal string ActionId { get; }
    internal string WagonProductId { get; }

    internal PuzzleRumorCandidate(PuzzleRumorKind _kind, string _cityId, string _itemId = null, string _tag = null,
        string _actionId = null, string _wagonProductId = null)
    {
        Kind = _kind;
        CityId = _cityId;
        ItemId = _itemId ?? string.Empty;
        Tag = _tag ?? string.Empty;
        ActionId = _actionId ?? string.Empty;
        WagonProductId = _wagonProductId ?? string.Empty;
    }

    internal static IReadOnlyList<PuzzleRumorCandidate> SortDistinct(IEnumerable<PuzzleRumorCandidate> _candidates)
    {
        return _candidates
            .Distinct()
            .OrderBy(_candidate => _candidate.Kind)
            .ThenBy(_candidate => _candidate.CityId, StringComparer.Ordinal)
            .ThenBy(_candidate => _candidate.ItemId, StringComparer.Ordinal)
            .ThenBy(_candidate => _candidate.Tag, StringComparer.Ordinal)
            .ThenBy(_candidate => _candidate.ActionId, StringComparer.Ordinal)
            .ThenBy(_candidate => _candidate.WagonProductId, StringComparer.Ordinal)
            .ToList();
    }
}
