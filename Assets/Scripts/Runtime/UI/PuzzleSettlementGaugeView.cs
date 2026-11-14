using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 다음 정산 게이지의 표시만 담당한다. 값은 PuzzleSettlementGaugePresenter가 시간·정산 계산에서 넣는다.
// 6칸 막대는 프리팹에 미리 배치한 Image를 켜고 끄는 방식이다(런타임 생성 금지).
public sealed class PuzzleSettlementGaugeView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI timeLabel;
    [SerializeField] TextMeshProUGUI estimateLabel;
    [SerializeField] Image[] cells;
    [SerializeField] Image background;
    [SerializeField] Image border;

    internal int CellCount => cells == null ? 0 : cells.Length;

    internal void Initialize()
    {
        if (!timeLabel || !estimateLabel || cells == null || cells.Length == 0 ||
            System.Array.Exists(cells, _cell => _cell == null))
        {
            throw new InvalidOperationException("정산 게이지 View에 남은 시간·예상치 라벨과 미리 배치한 칸 Image 참조가 필요합니다.");
        }

        if (titleLabel)
        {
            titleLabel.text = Presentation.GaugeTitle;
            titleLabel.color = Presentation.PuzzleHeading;
        }
        timeLabel.color = Presentation.PuzzleHeading;
        estimateLabel.color = Presentation.PuzzleHeading;
        if (background)
        {
            background.color = Presentation.PuzzleBackground;
        }
        if (border)
        {
            border.color = Presentation.PuzzleWagonNormalBorder;
        }
        foreach (var cell in cells)
        {
            cell.color = Presentation.PuzzleWagonNormalBorder;
        }
    }

    internal void Render(string _timeText, decimal _estimate, int _filledCells)
    {
        timeLabel.text = _timeText;
        estimateLabel.text = Presentation.GaugeEstimate(_estimate);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].color = i < _filledCells ? Presentation.PuzzleTimeAccent : Presentation.PuzzleWagonNormalBorder;
        }
    }

    internal void Clear(string _timeText)
    {
        timeLabel.text = _timeText;
        estimateLabel.text = string.Empty;
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].color = Presentation.PuzzleWagonNormalBorder;
        }
    }
}
