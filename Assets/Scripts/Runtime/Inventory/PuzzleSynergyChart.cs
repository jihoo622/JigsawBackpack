using System;
using System.Collections.Generic;
using System.Linq;

// 기차 인접 시너지 규칙을 '대상 태그 × 이웃 태그' 상성표로 묶는다. 화면과 제작 도구가 같은 행·열을 쓴다.
// 행 키는 대상 태그이고, 제외 방식 규칙(excludedTargetTag)은 "!" + 제외 태그 목록이다.
// 칸 값은 맞닿은 면 하나당 (배율 − 100)의 합이다.
public sealed class PuzzleSynergyChart
{
    public const string EXCLUDED_PREFIX = "!";
    const int BASE_PERCENT = 100;

    readonly int[,] percents;
    readonly string[,] excludedNeighbors;

    public IReadOnlyList<string> Rows { get; }
    public IReadOnlyList<string> Columns { get; }

    PuzzleSynergyChart(List<string> _rows, List<string> _columns)
    {
        Rows = _rows;
        Columns = _columns;
        percents = new int[_rows.Count, _columns.Count];
        excludedNeighbors = new string[_rows.Count, _columns.Count];
    }

    public int PercentAt(int _row, int _column)
    {
        return percents[_row, _column];
    }

    // 이 칸에서 효과를 주지 않는 이웃 태그(쉼표 구분). 없으면 null.
    public string ExcludedNeighborsAt(int _row, int _column)
    {
        return excludedNeighbors[_row, _column];
    }

    public static bool IsExclusionRow(string _row)
    {
        return _row.StartsWith(EXCLUDED_PREFIX, StringComparison.Ordinal);
    }

    public static IEnumerable<string> ExcludedTags(string _row)
    {
        return _row.Substring(EXCLUDED_PREFIX.Length).Split(',').Select(_tag => _tag.Trim());
    }

    // 기차 화물칸에 적용되는 규칙(scope Train·Both)으로 만든다.
    public static PuzzleSynergyChart ForTrain(TableManager _tables)
    {
        return Build(_tables.SynergiesFor(PuzzleSynergyScope.Train));
    }

    public static PuzzleSynergyChart Build(IEnumerable<PuzzleSynergyRuleData> _rules)
    {
        var rules = _rules.Where(_rule => _rule.spatialRange == nameof(PuzzleSynergyRange.Adjacent)).ToList();
        var rows = rules.Select(RowKey).Distinct(StringComparer.Ordinal)
            .OrderBy(_row => IsExclusionRow(_row) ? 0 : 1).ThenBy(_row => _row, StringComparer.Ordinal).ToList();
        var columns = rules.Select(_rule => _rule.neighborTag).Distinct(StringComparer.Ordinal)
            .OrderBy(_column => _column, StringComparer.Ordinal).ToList();
        var chart = new PuzzleSynergyChart(rows, columns);
        foreach (var rule in rules)
        {
            int row = rows.IndexOf(RowKey(rule));
            int column = columns.IndexOf(rule.neighborTag);
            chart.percents[row, column] += rule.multiplierPercent - BASE_PERCENT;
            if (!string.IsNullOrEmpty(rule.excludedNeighborTag))
            {
                string known = chart.excludedNeighbors[row, column];
                chart.excludedNeighbors[row, column] = known == null ? rule.excludedNeighborTag : known + "," + rule.excludedNeighborTag;
            }
        }
        return chart;
    }

    static string RowKey(PuzzleSynergyRuleData _rule)
    {
        return string.IsNullOrEmpty(_rule.targetTag) ? EXCLUDED_PREFIX + _rule.excludedTargetTag : _rule.targetTag;
    }
}
