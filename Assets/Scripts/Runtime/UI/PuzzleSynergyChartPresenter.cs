using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 시너지 표에서 상성표를 만들어 View에 넣는다. 테이블만 읽으므로 세션 상태와 무관하다.
// 표시는 수첩의 상성표 탭이 GameEvents로 요청한다(수첩이 넓게 펼쳐지고 그 본문 영역에 이 패널이 보인다).
public sealed class PuzzleSynergyChartPresenter : MonoBehaviour
{
    [SerializeField] PuzzleSynergyChartView view;

    PuzzleSynergyChart chart;
    int[,] cellFootnotes;
    string[] pieceIds;
    string[] baseItemLines;
    string defaultItemTags;

    void OnEnable()
    {
        var tables = TableManager.Instance.RequireValid();
        GameEvents.SynergyChartShowRequested += OnShowRequested;
        GameEvents.SynergyHighlightRequested += OnSynergyHighlightRequested;
        GameEvents.PanelVisibility += OnPanelVisibility;
        if (view)
        {
            view.Hidden += OnViewHidden;
        }

        chart = PuzzleSynergyChart.ForTrain(tables);
        if (!view.MatchesShape(chart.Rows.Count, chart.Columns.Count))
        {
            throw new InvalidOperationException("상성표 칸 수가 시너지 규칙과 다릅니다. Tools/MyJG/Apply Synergy Chart를 다시 실행하세요.");
        }

        // 제외 조건은 칸에 각주 번호를 달고 설명 영역에 모은다.
        var footnotes = new List<string>();
        cellFootnotes = new int[chart.Rows.Count, chart.Columns.Count];
        for (int row = 0; row < chart.Rows.Count; row++)
        {
            for (int column = 0; column < chart.Columns.Count; column++)
            {
                int percent = chart.PercentAt(row, column);
                string excluded = chart.ExcludedNeighborsAt(row, column);
                if (percent != 0 && excluded != null)
                {
                    footnotes.Add(excluded);
                    cellFootnotes[row, column] = footnotes.Count;
                }
            }
        }

        var pieces = tables.Pieces;
        pieceIds = new string[pieces.Count];
        baseItemLines = new string[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            pieceIds[i] = pieces[i].id;
            baseItemLines[i] = Presentation.SynergyChartItemLine(pieces[i].displayName, pieces[i].tags);
        }
        defaultItemTags = string.Join("\n", baseItemLines);

        view.RenderFrame(
            Presentation.SynergyChartTitle,
            Presentation.SynergyChartToggle,
            Presentation.SynergyChartClose,
            Presentation.SynergyChartCorner,
            Presentation.SynergyChartNotes(footnotes, tables.Pieces, tables.PuzzleSynergy.activationIntervalTurns),
            Presentation.SynergyChartItemTitle,
            defaultItemTags);

        ApplyHighlight(null, null);
    }

    void OnDisable()
    {
        GameEvents.SynergyChartShowRequested -= OnShowRequested;
        GameEvents.SynergyHighlightRequested -= OnSynergyHighlightRequested;
        GameEvents.PanelVisibility -= OnPanelVisibility;
        if (view)
        {
            view.Hidden -= OnViewHidden;
        }
    }

    void OnShowRequested(GameEvents.SynergyChartShowInfo _info)
    {
        if (_info.sceneHandle == gameObject.scene.handle)
        {
            view.SetOpen(_info.show);
        }
    }

    void OnSynergyHighlightRequested(GameEvents.SynergyHighlightInfo _info)
    {
        if (_info.sceneHandle != gameObject.scene.handle)
        {
            return;
        }

        ApplyHighlight(_info.tags, _info.itemId);
    }

    void OnPanelVisibility(GameEvents.PanelInfo _info)
    {
        if (view && _info.panel == view.PanelName && !_info.open)
        {
            ClearHighlight();
        }
    }

    void OnViewHidden()
    {
        ClearHighlight();
    }

    void ClearHighlight()
    {
        if (chart != null)
        {
            ApplyHighlight(null, null);
        }
    }

    void ApplyHighlight(IReadOnlyList<string> _tags, string _itemId)
    {
        if (chart == null || !view)
        {
            return;
        }

        HashSet<string> tagSet = null;
        if (_tags != null && _tags.Count > 0)
        {
            tagSet = new HashSet<string>(_tags.Where(_t => !string.IsNullOrEmpty(_t)), StringComparer.Ordinal);
        }
        bool hasTags = tagSet != null && tagSet.Count > 0;

        for (int row = 0; row < chart.Rows.Count; row++)
        {
            string key = chart.Rows[row];
            Color bg = Presentation.SynergyChartTagBackground(key);
            if (!PuzzleSynergyChart.IsExclusionRow(key) && hasTags && tagSet.Contains(key))
            {
                bg = BlendHighlight(bg);
            }
            view.RenderRowHeader(row, Presentation.SynergyChartRowLabel(key), bg, Presentation.PuzzleTagTextColor(key));
        }

        for (int column = 0; column < chart.Columns.Count; column++)
        {
            string tag = chart.Columns[column];
            Color bg = Presentation.PuzzleTagBackgroundColor(tag);
            if (hasTags && tagSet.Contains(tag))
            {
                bg = BlendHighlight(bg);
            }
            view.RenderColumnHeader(column, Presentation.PuzzleTagLabel(tag), bg, Presentation.PuzzleTagTextColor(tag));
        }

        for (int row = 0; row < chart.Rows.Count; row++)
        {
            string rowKey = chart.Rows[row];
            bool rowMatch = !PuzzleSynergyChart.IsExclusionRow(rowKey) && hasTags && tagSet.Contains(rowKey);

            for (int column = 0; column < chart.Columns.Count; column++)
            {
                string colTag = chart.Columns[column];
                bool colMatch = hasTags && tagSet.Contains(colTag);

                int percent = chart.PercentAt(row, column);
                int footnote = cellFootnotes != null ? cellFootnotes[row, column] : 0;
                Color bg = Presentation.SynergyChartCellBackground(percent);
                if (rowMatch || colMatch)
                {
                    bg = BlendHighlight(bg);
                }
                view.RenderCell(row, column, Presentation.SynergyChartCell(percent, footnote), bg, Presentation.PuzzleTagTextColor(null));
            }
        }

        if (string.IsNullOrEmpty(_itemId) || pieceIds == null || baseItemLines == null)
        {
            view.RenderItemTags(defaultItemTags);
        }
        else
        {
            string hoverHex = ColorUtility.ToHtmlStringRGB(Presentation.EntityLinkHover);
            var lines = new string[baseItemLines.Length];
            for (int i = 0; i < pieceIds.Length; i++)
            {
                string line = baseItemLines[i];
                if (string.Equals(pieceIds[i], _itemId, StringComparison.Ordinal))
                {
                    line = $"<b><color=#{hoverHex}>{line}</color></b>";
                }
                lines[i] = line;
            }
            view.RenderItemTags(string.Join("\n", lines));
        }
    }

    static Color BlendHighlight(Color _base)
    {
        Color highlight = Presentation.EntityLinkHighlight;
        return Color.Lerp(_base, highlight, highlight.a);
    }
}
