using System;
using System.Collections.Generic;
using System.Linq;

// 술집 소문의 종류. 문장이나 태그 유무로 사후 추론하지 않고 후보를 만들 때 확정한다.
public enum PuzzleRumorKind
{
    Specialty,
    RareDemand
}

// 소문 후보 하나. 대상 마을과 품목·태그를 문자열이 아닌 ID로만 보유한다.
internal readonly struct PuzzleRumorCandidate
{
    internal PuzzleRumorKind Kind { get; }
    internal string CityId { get; }
    // 특산 후보가 언급하는 품목. 희귀 후보는 빈 문자열이다.
    internal string ItemId { get; }
    // 희귀 후보가 언급하는 태그. 특산 후보는 대상 마을이 내건 특산 태그다.
    internal string Tag { get; }

    internal PuzzleRumorCandidate(PuzzleRumorKind _kind, string _cityId, string _itemId, string _tag)
    {
        Kind = _kind;
        CityId = _cityId;
        ItemId = _itemId ?? string.Empty;
        Tag = _tag ?? string.Empty;
    }

    // 테이블 배열 순서에 의존하지 않는다. 종류·마을·품목·태그 순으로 정렬하고 같은 후보는 하나만 남긴다.
    internal static IReadOnlyList<PuzzleRumorCandidate> SortDistinct(IEnumerable<PuzzleRumorCandidate> _candidates)
    {
        var ordered = _candidates
            .OrderBy(_candidate => _candidate.Kind)
            .ThenBy(_candidate => _candidate.CityId, StringComparer.Ordinal)
            .ThenBy(_candidate => _candidate.ItemId, StringComparer.Ordinal)
            .ThenBy(_candidate => _candidate.Tag, StringComparer.Ordinal)
            .ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<PuzzleRumorCandidate>(ordered.Count);
        foreach (var candidate in ordered)
        {
            string key = candidate.Kind + "|" + candidate.CityId + "|" + candidate.ItemId + "|" + candidate.Tag;
            if (seen.Add(key))
            {
                distinct.Add(candidate);
            }
        }

        return distinct;
    }
}
