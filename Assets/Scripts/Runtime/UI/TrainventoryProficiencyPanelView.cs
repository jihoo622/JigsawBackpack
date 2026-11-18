using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 수첩 숙련 탭 본문. 위쪽 평판 요약 3줄과 사전 배치한 태그 행 12개(숙련 대상 태그 수만큼 켠다)를 갱신한다.
// 행 순서·숫자는 Presenter가 세션에서 읽어 넘긴다.
public sealed class TrainventoryProficiencyPanelView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI reputationLabel;
    [SerializeField] TextMeshProUGUI nextStageLabel;
    [SerializeField] TextMeshProUGUI hintLabel;
    [SerializeField] TrainventoryProficiencyEntryView[] rows = Array.Empty<TrainventoryProficiencyEntryView>();

    internal void Render(int _reputation, int _maxActiveQuests, bool _hasNextStage, int _nextReputation,
        IReadOnlyList<TrainventoryProficiencyStanding> _standings, string _wagonGates)
    {
        if (!reputationLabel || !nextStageLabel || !hintLabel || rows == null || rows.Length < _standings.Count)
        {
            throw new InvalidOperationException("숙련 탭에 평판 요약 글자와 숙련 대상 태그 수만큼의 행을 연결하세요.");
        }
        reputationLabel.text = Presentation.ProficiencyPanelReputation(_reputation, _maxActiveQuests);
        reputationLabel.color = Presentation.PuzzleHeading;
        nextStageLabel.text = _hasNextStage
            ? Presentation.ProficiencyPanelNextStage(_nextReputation, _nextReputation - _reputation)
            : Presentation.ProficiencyPanelMaxStage;
        // 증차 상한·화차 해금 게이트를 같은 줄 뒤에 이어 쓴다. 남은 게이트가 없으면 평판 단계 문구만 남는다.
        if (!string.IsNullOrEmpty(_wagonGates))
        {
            nextStageLabel.text += Presentation.ProficiencyNoticeSeparator + _wagonGates;
        }
        nextStageLabel.color = Presentation.PuzzleHeading;
        hintLabel.text = Presentation.ProficiencyPanelHint;
        hintLabel.color = Presentation.ProficiencyDimText;
        for (int i = 0; i < rows.Length; i++)
        {
            bool used = i < _standings.Count;
            rows[i].gameObject.SetActive(used);
            if (used)
            {
                rows[i].Render(_standings[i]);
            }
        }
    }
}
