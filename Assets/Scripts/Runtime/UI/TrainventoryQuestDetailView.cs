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
    [SerializeField] GameObject miniGridRoot;
    [SerializeField] Image[] miniGridCells = Array.Empty<Image>();

    static readonly Color miniGridFilledColor = new Color(0.23f, 0.51f, 0.96f, 1f); // #3B82F6
    static readonly Color miniGridEmptyColor = new Color(0.12f, 0.15f, 0.20f, 0.5f);

    void Awake()
    {
        if (titleLabel)
        {
            titleLabel.text = "선택한 의뢰 상세";
        }
    }

    internal void Bind(TrainventoryQuestOffer _offer, PuzzleInventoryService _session)
    {
        if (_offer == null || _session == null)
        {
            if (destinationLabel) destinationLabel.text = string.Empty;
            if (deadlineLabel) deadlineLabel.text = string.Empty;
            if (rewardLabel) rewardLabel.text = string.Empty;
            if (depositLabel) depositLabel.gameObject.SetActive(false);
            if (cargoLabel) cargoLabel.text = string.Empty;
            if (miniGridRoot) miniGridRoot.SetActive(false);
            return;
        }

        string cityName = TableManager.Instance.CityPrice(_offer.TargetCityId)?.displayName ?? _offer.TargetCityId;
        if (destinationLabel)
        {
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

        var def = _session.ItemDefinitions?.FirstOrDefault(d => string.Equals(d.Id, _offer.PieceId, StringComparison.Ordinal));
        int cellCount = def != null ? def.Cells.Count : 0;
        string cargoName = def != null ? def.DisplayName : _offer.PieceId;
        if (cargoLabel)
        {
            cargoLabel.text = Presentation.QuestDetailCargo(cargoName, cellCount);
        }

        UpdateMiniGrid(def);
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
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
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
