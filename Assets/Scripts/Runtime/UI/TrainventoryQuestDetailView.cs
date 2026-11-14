using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TrainventoryQuestDetailView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI destinationLabel;
    [SerializeField] TextMeshProUGUI deadlineLabel;
    [SerializeField] TextMeshProUGUI rewardLabel;
    [SerializeField] TextMeshProUGUI depositLabel;
    [SerializeField] TextMeshProUGUI cargoLabel;
    [SerializeField] TextMeshProUGUI emptyLabel;
    [SerializeField] GameObject miniGridRoot;
    [SerializeField] Image[] miniGridCells = Array.Empty<Image>();

    static readonly Color miniGridFilledColor = new Color(0.23f, 0.51f, 0.96f, 1f); // #3B82F6
    static readonly Color miniGridEmptyColor = new Color(0.12f, 0.15f, 0.20f, 0.5f);

    void Awake()
    {
        if (titleLabel)
        {
            titleLabel.text = Presentation.QuestDetailTitle;
        }
        if (emptyLabel)
        {
            emptyLabel.text = Presentation.QuestDetailEmpty;
        }
    }

    // 의뢰가 없으면(선택 전) 상세 줄을 모두 숨기고 고르기 안내만 보인다.
    internal void Bind(TrainventoryQuestOffer _offer, PuzzleInventoryService _session)
    {
        bool hasOffer = _offer != null && _session != null;
        SetLineVisible(destinationLabel, hasOffer);
        SetLineVisible(deadlineLabel, hasOffer);
        SetLineVisible(rewardLabel, hasOffer);
        SetLineVisible(cargoLabel, hasOffer);
        SetLineVisible(emptyLabel, !hasOffer);
        if (!hasOffer)
        {
            SetLineVisible(depositLabel, false);
            if (miniGridRoot)
            {
                miniGridRoot.SetActive(false);
            }
            return;
        }

        string cityName = Presentation.QuestCityName(_offer.TargetCityId);
        if (destinationLabel)
        {
            destinationLabel.richText = true;
            destinationLabel.text = Presentation.QuestDetailDestination(cityName, _offer.TravelHours * PuzzleTimeManager.MINUTES_PER_HOUR);
        }
        if (deadlineLabel)
        {
            long deadlineMinutes = _session.ElapsedTime + _offer.DeadlineHours * PuzzleTimeManager.MINUTES_PER_HOUR;
            deadlineLabel.text = Presentation.QuestDetailDeadline(deadlineMinutes);
        }
        if (rewardLabel)
        {
            rewardLabel.text = Presentation.QuestDetailReward(_offer.Reward);
        }
        if (depositLabel)
        {
            bool hasDeposit = _offer.Deposit > 0;
            depositLabel.gameObject.SetActive(hasDeposit);
            if (hasDeposit)
            {
                depositLabel.text = Presentation.QuestDetailDeposit(_offer.Deposit);
            }
        }

        var def = _session.ItemDefinitions?.FirstOrDefault(_definition => string.Equals(_definition.Id, _offer.PieceId, StringComparison.Ordinal));
        int cellCount = def != null ? def.Cells.Count : 0;
        string cargoName = def != null ? def.DisplayName : _offer.PieceId;
        if (cargoLabel)
        {
            cargoLabel.text = Presentation.QuestDetailCargo(cargoName, cellCount);
        }

        UpdateMiniGrid(def);
    }

    static void SetLineVisible(TextMeshProUGUI _label, bool _visible)
    {
        if (_label)
        {
            _label.gameObject.SetActive(_visible);
        }
    }

    void UpdateMiniGrid(PuzzleItemDefinition _item)
    {
        if (!miniGridRoot)
        {
            return;
        }

        if (_item == null || _item.Cells == null || _item.Cells.Count == 0 || miniGridCells == null || miniGridCells.Length != 25)
        {
            miniGridRoot.SetActive(false);
            return;
        }

        miniGridRoot.SetActive(true);

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        for (int i = 0; i < _item.Cells.Count; i++)
        {
            var cell = _item.Cells[i];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        int offsetX = Mathf.Max(0, (5 - width) / 2);
        int offsetY = Mathf.Max(0, (5 - height) / 2);

        bool[] occupied = new bool[25];
        for (int i = 0; i < _item.Cells.Count; i++)
        {
            int col = offsetX + (_item.Cells[i].x - minX);
            int row = offsetY + (_item.Cells[i].y - minY);
            if (col >= 0 && col < 5 && row >= 0 && row < 5)
            {
                occupied[row * 5 + col] = true;
            }
        }

        for (int i = 0; i < 25; i++)
        {
            if (miniGridCells[i] != null)
            {
                miniGridCells[i].color = occupied[i] ? miniGridFilledColor : miniGridEmptyColor;
            }
        }
    }
}
