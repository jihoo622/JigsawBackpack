using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 사전 배치한 상성표 칸에 문구와 색만 넣는다. 칸 구성은 Tools/MyJG/Apply Synergy Chart가 만든다.
public sealed class PuzzleSynergyChartView : PanelBase
{
    [SerializeField] Button toggleButton;
    [SerializeField] Button closeButton;
    [SerializeField] TextMeshProUGUI toggleLabel;
    [SerializeField] TextMeshProUGUI closeLabel;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI cornerLabel;
    [SerializeField] Image[] rowHeaderImages;
    [SerializeField] TextMeshProUGUI[] rowHeaderTexts;
    [SerializeField] Image[] columnHeaderImages;
    [SerializeField] TextMeshProUGUI[] columnHeaderTexts;
    // 행 우선 순서(행 × 열 수).
    [SerializeField] Image[] cellImages;
    [SerializeField] TextMeshProUGUI[] cellTexts;
    [SerializeField] TextMeshProUGUI notesText;
    [SerializeField] TextMeshProUGUI itemTitleText;
    [SerializeField] TextMeshProUGUI itemTagsText;

    internal int RowCapacity => rowHeaderTexts == null ? 0 : rowHeaderTexts.Length;
    internal int ColumnCapacity => columnHeaderTexts == null ? 0 : columnHeaderTexts.Length;

    protected override void OnEnable()
    {
        base.OnEnable();
        toggleButton.onClick.AddListener(ToggleChart);
        closeButton.onClick.AddListener(Close);
    }

    protected override void OnDisable()
    {
        toggleButton.onClick.RemoveListener(ToggleChart);
        closeButton.onClick.RemoveListener(Close);
        base.OnDisable();
    }

    void ToggleChart()
    {
        Toggle();
    }

    internal void RenderFrame(string _title, string _toggle, string _close, string _corner, string _notes, string _itemTitle, string _itemTags)
    {
        if (!toggleButton || !closeButton || !toggleLabel || !closeLabel || !titleLabel || !cornerLabel ||
            !notesText || !itemTitleText || !itemTagsText)
        {
            throw new InvalidOperationException("상성표 패널의 버튼·제목·설명 참조를 연결하세요(Tools/MyJG/Apply Synergy Chart).");
        }
        titleLabel.text = _title;
        toggleLabel.text = _toggle;
        closeLabel.text = _close;
        cornerLabel.text = _corner;
        notesText.text = _notes;
        itemTitleText.text = _itemTitle;
        itemTagsText.text = _itemTags;
    }

    internal void RenderRowHeader(int _row, string _text, Color _background, Color _textColor)
    {
        SetCell(rowHeaderImages[_row], rowHeaderTexts[_row], _text, _background, _textColor);
    }

    internal void RenderColumnHeader(int _column, string _text, Color _background, Color _textColor)
    {
        SetCell(columnHeaderImages[_column], columnHeaderTexts[_column], _text, _background, _textColor);
    }

    internal void RenderCell(int _row, int _column, string _text, Color _background, Color _textColor)
    {
        int index = _row * ColumnCapacity + _column;
        SetCell(cellImages[index], cellTexts[index], _text, _background, _textColor);
    }

    internal bool MatchesShape(int _rows, int _columns)
    {
        return RowCapacity == _rows && ColumnCapacity == _columns &&
            rowHeaderImages != null && rowHeaderImages.Length == _rows &&
            columnHeaderImages != null && columnHeaderImages.Length == _columns &&
            cellImages != null && cellImages.Length == _rows * _columns &&
            cellTexts != null && cellTexts.Length == _rows * _columns;
    }

    static void SetCell(Image _image, TextMeshProUGUI _text, string _value, Color _background, Color _textColor)
    {
        _image.color = _background;
        _text.text = _value;
        _text.color = _textColor;
    }
}
