using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// S3/S5: 기차 화차 부동 창(510, 544, 목업 concept-3b). 제목, 렌즈 버튼, 가치/다음 정산 요약, 동적 보드(7×5, 56px),
// 우측 열의 시너지 요약(화차 자신의 다음 정산 Δ)과 조작 안내, 2줄 렌즈 요약(R14).
[DisallowMultipleComponent]
public sealed class TrainWindowView : MonoBehaviour
{
    [SerializeField] FloatingWindow floatingWindow;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI nextSettlementText;
    [SerializeField] TextMeshProUGUI synergyLabelText;
    [SerializeField] TextMeshProUGUI synergyNumberText;
    [SerializeField] TextMeshProUGUI controlsHintText;
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
        if (synergyLabelText)
        {
            synergyLabelText.text = BasicData.Text("presentation.inventory.synergySettlementNotice");
        }
        if (controlsHintText)
        {
            // 기존 이동 안내 한 줄(4칸 공백 구분)을 조작 안내 박스에 맞게 줄바꿈한다.
            controlsHintText.text = Presentation.PuzzleMoveGuide.Replace("    ", "\n");
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
        // 우측 시너지 요약은 이 화차 한 칸의 다음 정산 Δ(Σ 최종가치-기본가치)를 보여준다.
        if (synergyNumberText)
        {
            synergyNumberText.text = _wagon != null
                ? string.Format(CultureInfo.InvariantCulture, "{0:+#,0.##;-#,0.##;0}$", _wagon.SettlementGain)
                : string.Empty;
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
