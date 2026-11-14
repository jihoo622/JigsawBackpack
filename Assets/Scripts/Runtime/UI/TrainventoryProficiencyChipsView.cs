using System;
using System.Collections.Generic;
using UnityEngine;

// 기차 화물 카드의 숙련 칩 6열×2행(사전 배치 12개, 숙련 대상 태그 수만큼 켠다).
// 칩 순서는 태그 기본 순서로 고정하며 어느 칩을 눌러도 Clicked를 올린다.
public sealed class TrainventoryProficiencyChipsView : MonoBehaviour
{
    [SerializeField] TrainventoryProficiencyEntryView[] chips = Array.Empty<TrainventoryProficiencyEntryView>();

    internal event Action Clicked;

    void OnEnable()
    {
        foreach (var chip in chips)
        {
            if (chip)
            {
                chip.Clicked += OnChipClicked;
            }
        }
    }

    void OnDisable()
    {
        foreach (var chip in chips)
        {
            if (chip)
            {
                chip.Clicked -= OnChipClicked;
            }
        }
    }

    void OnChipClicked()
    {
        Clicked?.Invoke();
    }

    internal void Render(IReadOnlyList<TrainventoryProficiencyStanding> _standings)
    {
        if (chips == null || chips.Length < _standings.Count)
        {
            throw new InvalidOperationException("숙련 칩을 숙련 대상 태그 수만큼 연결하세요.");
        }
        for (int i = 0; i < chips.Length; i++)
        {
            bool used = i < _standings.Count;
            chips[i].gameObject.SetActive(used);
            if (used)
            {
                chips[i].Render(_standings[i]);
            }
        }
    }
}
