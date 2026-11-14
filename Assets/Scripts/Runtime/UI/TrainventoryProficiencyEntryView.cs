using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 태그 하나의 숙련 표시(수첩 숙련 탭 행, 기차 카드 칩). 프리팹에 사전 배치한 글자·막대만 갱신한다.
// 막대는 채움 RectTransform의 anchorMax.x로 현재 레벨 구간 진행도를 보인다. 경험치가 0인 태그는 흐린 글자색이다.
// 경험치 숫자·거래 이득 칸과 버튼은 선택이다(칩에는 경험치 숫자·이득 칸이 없다).
public sealed class TrainventoryProficiencyEntryView : MonoBehaviour
{
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI tagLabel;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] TextMeshProUGUI xpLabel;
    [SerializeField] TextMeshProUGUI bonusLabel;
    [SerializeField] Image barTrack;
    [SerializeField] Image barFill;
    [SerializeField] Button button;
    // 좁은 칩은 레벨을 숫자만 표기한다(행은 'Lv n').
    [SerializeField] bool compactLevel;

    internal event Action Clicked;

    void OnEnable()
    {
        if (button)
        {
            button.onClick.AddListener(OnClicked);
        }
    }

    void OnDisable()
    {
        if (button)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }

    void OnClicked()
    {
        Clicked?.Invoke();
    }

    internal void Render(TrainventoryProficiencyStanding _standing)
    {
        if (!background || !tagLabel || !levelLabel || !barTrack || !barFill)
        {
            throw new InvalidOperationException("숙련 표시에 배경·태그·레벨·막대 참조를 연결하세요.");
        }
        Color text = _standing.HasXp ? Presentation.PuzzleHeading : Presentation.ProficiencyDimText;
        background.color = Presentation.ProficiencyRowBackground;
        tagLabel.text = Presentation.PuzzleTagLabel(_standing.Tag);
        tagLabel.color = text;
        levelLabel.text = compactLevel
            ? Presentation.ProficiencyChipLevel(_standing.Level)
            : Presentation.ProficiencyRowLevel(_standing.Level);
        levelLabel.color = text;
        if (xpLabel)
        {
            xpLabel.text = Presentation.ProficiencyRowXp(_standing.Xp, _standing.NextXp);
            xpLabel.color = text;
        }
        if (bonusLabel)
        {
            bonusLabel.text = Presentation.ProficiencyRowBonus(_standing.BonusPercent);
            bonusLabel.color = _standing.Level > 0 ? Presentation.PuzzleTooltipGreen : Presentation.ProficiencyDimText;
        }
        barTrack.color = Presentation.ProficiencyBarTrack;
        barFill.color = _standing.HasXp ? Presentation.ProficiencyBarFill : Presentation.ProficiencyDimText;
        var fill = barFill.rectTransform;
        fill.anchorMax = new Vector2(_standing.Progress, fill.anchorMax.y);
        barFill.enabled = _standing.Progress > 0f;
    }
}
