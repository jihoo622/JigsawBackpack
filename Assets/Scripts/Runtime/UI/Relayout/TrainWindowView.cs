using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S3/S5: 기차 화차 부동 창(440, 430). 제목, 렌즈 버튼, 가치/다음 정산 요약, 숙련 칩, 동적 보드, 2줄 렌즈 요약(R14).
[DisallowMultipleComponent]
public sealed class TrainWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI nextSettlementText;
    [SerializeField] TextMeshProUGUI lensTradeLine;
    [SerializeField] TextMeshProUGUI lensSpotLine;
    [SerializeField] PuzzleInventoryView inventoryView;
    [SerializeField] Button lensButton;
    [SerializeField] TextMeshProUGUI lensLabel;

    public FloatingWindow FloatingWindow => floatingWindow;
    public PuzzleInventoryView InventoryView => inventoryView;
    public PuzzleInventory BoundWagon { get; set; }
    public int BoundWagonIndex { get; set; } = -1;

    public event Action LensRequested;

    void Awake()
    {
        SetLensSummary(string.Empty, string.Empty);
    }

    void OnEnable()
    {
        if (lensLabel)
        {
            lensLabel.text = Presentation.TrainWindowLensButton;
        }
        if (lensButton)
        {
            lensButton.onClick.AddListener(OnLensClicked);
        }
    }

    void OnDisable()
    {
        if (lensButton)
        {
            lensButton.onClick.RemoveListener(OnLensClicked);
        }
    }

    void OnLensClicked()
    {
        LensRequested?.Invoke();
    }

    public void SetHeader(PuzzleInventory _wagon, long _remainingSettlementHours, decimal _synergyDelta)
    {
        if (titleText && _wagon != null)
        {
            titleText.text = Presentation.TrainWindowTitle(_wagon);
        }
        if (nextSettlementText)
        {
            nextSettlementText.text = Presentation.GaugeInline(_remainingSettlementHours, _synergyDelta);
        }
    }

    // R14: 잡은 화물의 거래 견적·잔액 변화(1줄)와 가리킨 자리의 다음 정산 Δ(2줄).
    public void SetLensSummary(string _tradeLine, string _spotLine)
    {
        bool hasTrade = !string.IsNullOrEmpty(_tradeLine);
        bool hasSpot = !string.IsNullOrEmpty(_spotLine);
        if (lensTradeLine)
        {
            lensTradeLine.richText = true;
            lensTradeLine.text = hasTrade ? _tradeLine : Presentation.TrainWindowLensIdle;
        }
        if (lensSpotLine)
        {
            lensSpotLine.richText = true;
            lensSpotLine.text = hasSpot ? _spotLine : string.Empty;
        }
    }
}
