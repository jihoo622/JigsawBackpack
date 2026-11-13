using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 시너지 표에서 상성표를 만들어 View에 넣는다. 테이블만 읽으므로 세션 상태와 무관하다.
public sealed class PuzzleSynergyChartPresenter : MonoBehaviour
{
    [SerializeField] PuzzleSynergyChartView view;

    void OnEnable()
    {
        var tables = TableManager.Instance.RequireValid();
        var chart = PuzzleSynergyChart.ForTrain(tables);
        if (!view.MatchesShape(chart.Rows.Count, chart.Columns.Count))
        {
            throw new InvalidOperationException("상성표 칸 수가 시너지 규칙과 다릅니다. Tools/MyJG/Apply Synergy Chart를 다시 실행하세요.");
        }

        for (int row = 0; row < chart.Rows.Count; row++)
        {
            string key = chart.Rows[row];
            view.RenderRowHeader(row, Presentation.SynergyChartRowLabel(key), Presentation.SynergyChartTagBackground(key),
                Presentation.PuzzleTagTextColor(key));
        }
        for (int column = 0; column < chart.Columns.Count; column++)
        {
            string tag = chart.Columns[column];
            view.RenderColumnHeader(column, Presentation.PuzzleTagLabel(tag), Presentation.PuzzleTagBackgroundColor(tag),
                Presentation.PuzzleTagTextColor(tag));
        }

        // 제외 조건은 칸에 각주 번호를 달고 설명 영역에 모은다.
        var footnotes = new List<string>();
        for (int row = 0; row < chart.Rows.Count; row++)
        {
            for (int column = 0; column < chart.Columns.Count; column++)
            {
                int percent = chart.PercentAt(row, column);
                string excluded = chart.ExcludedNeighborsAt(row, column);
                int footnote = 0;
                if (percent != 0 && excluded != null)
                {
                    footnotes.Add(excluded);
                    footnote = footnotes.Count;
                }
                view.RenderCell(row, column, Presentation.SynergyChartCell(percent, footnote),
                    Presentation.SynergyChartCellBackground(percent), Presentation.PuzzleTagTextColor(null));
            }
        }

        var items = tables.Pieces.Select(_piece => Presentation.SynergyChartItemLine(_piece.displayName, _piece.tags));
        view.RenderFrame(Presentation.SynergyChartTitle, Presentation.SynergyChartToggle, Presentation.SynergyChartClose,
            Presentation.SynergyChartCorner,
            Presentation.SynergyChartNotes(footnotes, tables.PuzzleSynergy.freshness.refrigerationReductionPercent, tables.PuzzleSynergy.activationIntervalHours),
            Presentation.SynergyChartItemTitle, string.Join("\n", items));
    }
}
