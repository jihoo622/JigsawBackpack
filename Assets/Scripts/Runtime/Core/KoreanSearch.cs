using System;
using System.Collections.Generic;
using System.Text;

// 전역 검색 확장 지점:
// 1. 검색 규칙 및 정렬은 KoreanSearch 하나를 공유한다.
// 2. 전역 검색으로 확장할 때는 SearchEntry 공급원만 늘린다 (품목·태그·의뢰 등).
//    - 공급원은 화면별로 두고 검색 규칙(KoreanSearch)에 특정 화면이나 특정 엔티티 의존성을 넣지 않는다.
// 3. 검색 결과 이동은 고유명사 링크와 동일한 (EntityLinkKind, id) 쌍을 사용하므로 EntityLinkNavigation.Navigate로 그대로 전달할 수 있다.
// 4. 시세 정보를 미리 알려 주지 않는 원칙 때문에 품목 검색은 이름과 태그로만 찾고 '어디서 비싸게 파는지'로는 찾지 않는다.
// 5. 전역 검색 단축키(예: Ctrl+F)는 InputSystem_Actions의 UI 맵에 액션으로 추가하여 사용한다 (코드에 하드코딩 금지).

public readonly struct SearchEntry
{
    public readonly EntityLinkKind kind;
    public readonly string id;
    public readonly string name;

    public SearchEntry(EntityLinkKind _kind, string _id, string _name)
    {
        kind = _kind;
        id = _id;
        name = _name;
    }
}

public static class KoreanSearch
{
    public const int SCORE_EXACT = 3;
    public const int SCORE_PREFIX = 2;
    public const int SCORE_CONTAINS = 1;
    public const int SCORE_MISMATCH = -1;

    const string CHOSUNG = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    const int HANGUL_BASE = 0xAC00;
    const int HANGUL_LIMIT = 0xD7A3;
    const int HANGUL_CHOSUNG_DIVISOR = 588; // 21 * 28

    /// <summary>
    /// 한글 음절은 초성으로 변환하고, 그 외 문자는 소문자 그대로 유지하며, 공백은 제거한다.
    /// </summary>
    public static string Initials(string _text)
    {
        if (string.IsNullOrEmpty(_text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(_text.Length);
        for (int i = 0; i < _text.Length; i++)
        {
            char c = _text[i];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c >= HANGUL_BASE && c <= HANGUL_LIMIT)
            {
                int chosungIndex = (c - HANGUL_BASE) / HANGUL_CHOSUNG_DIVISOR;
                sb.Append(CHOSUNG[chosungIndex]);
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 공백 무시·대소문자 무시로 검색 점수를 산출한다.
    /// 정확 일치(3) > 앞부분 일치(2) > 포함(1), 불일치는 -1.
    /// 질의가 초성 자모(ㄱ~ㅎ, 쌍자음 포함)로만 이루어진 경우 이름의 초성열과 같은 규칙으로 비교한다.
    /// </summary>
    public static int Score(string _name, string _query)
    {
        if (string.IsNullOrEmpty(_name) || string.IsNullOrEmpty(_query))
        {
            return SCORE_MISMATCH;
        }

        string normalizedQuery = Normalize(_query);
        if (normalizedQuery.Length == 0)
        {
            return SCORE_MISMATCH;
        }

        bool isChosung = IsChosungOnly(normalizedQuery);
        string target = isChosung ? Initials(_name) : Normalize(_name);

        if (target.Length == 0)
        {
            return SCORE_MISMATCH;
        }

        if (string.Equals(target, normalizedQuery, StringComparison.Ordinal))
        {
            return SCORE_EXACT;
        }
        if (target.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return SCORE_PREFIX;
        }
        if (target.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return SCORE_CONTAINS;
        }
        return SCORE_MISMATCH;
    }

    /// <summary>
    /// 항목 목록을 질의로 검색하여 점수 내림차순, 점수가 같으면 ID Ordinal 순으로 정렬해 반환한다.
    /// </summary>
    public static List<SearchEntry> Search(IEnumerable<SearchEntry> _entries, string _query, int _limit, out int _total)
    {
        _total = 0;
        if (_entries == null || string.IsNullOrWhiteSpace(_query))
        {
            return new List<SearchEntry>();
        }

        var matches = new List<(SearchEntry entry, int score)>();
        foreach (var entry in _entries)
        {
            int score = Score(entry.name, _query);
            if (score >= 0)
            {
                matches.Add((entry, score));
            }
        }

        _total = matches.Count;
        if (matches.Count == 0 || _limit <= 0)
        {
            return new List<SearchEntry>();
        }

        matches.Sort((_a, _b) =>
        {
            int scoreCompare = _b.score.CompareTo(_a.score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }
            return string.Compare(_a.entry.id, _b.entry.id, StringComparison.Ordinal);
        });

        int count = Math.Min(_limit, matches.Count);
        var result = new List<SearchEntry>(count);
        for (int i = 0; i < count; i++)
        {
            result.Add(matches[i].entry);
        }
        return result;
    }

    static string Normalize(string _text)
    {
        if (string.IsNullOrEmpty(_text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(_text.Length);
        for (int i = 0; i < _text.Length; i++)
        {
            char c = _text[i];
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    static bool IsChosungOnly(string _text)
    {
        if (string.IsNullOrEmpty(_text))
        {
            return false;
        }

        for (int i = 0; i < _text.Length; i++)
        {
            if (CHOSUNG.IndexOf(_text[i]) < 0)
            {
                return false;
            }
        }
        return true;
    }
}
