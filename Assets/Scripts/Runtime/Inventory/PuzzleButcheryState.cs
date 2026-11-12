using System.Collections.Generic;

// 마을 도축대의 씬 로컬 실행 상태. 정의·시간·변환 관계는 테이블이 소유하고, 여기에는 보드 인스턴스와 진행 상황만 둔다.
public sealed class PuzzleButcheryState
{
    public string CityId { get; }
    public PuzzleCityActionData Action { get; }
    public PuzzleInventory Board { get; }
    public PuzzleButcheryPhase Phase { get; internal set; }
    // 완성 시점(누적 경과 분). Idle이면 -1.
    public long ReadyAtMinutes { get; internal set; }
    // 완료 때 보드에 올릴 산출물. 공간 부족 등으로 남으면 다음 판정 때 다시 배치한다.
    internal List<string> PendingOutputs { get; } = new List<string>();
    // 이번 도축으로 확정된 산출물 총개수. 완료 사건에 그대로 쓴다.
    internal int OutputCount { get; set; }

    internal PuzzleButcheryState(string _cityId, PuzzleCityActionData _action, PuzzleInventory _board)
    {
        CityId = _cityId;
        Action = _action;
        Board = _board;
        Phase = PuzzleButcheryPhase.Idle;
        ReadyAtMinutes = -1;
    }
}

public enum PuzzleButcheryPhase
{
    /// <summary>보드가 비어 있고 새로 맡길 수 있는 상태.</summary>
    Idle,
    /// <summary>동물을 맡기고 작업이 진행 중인 상태. 반입·반출을 막는다.</summary>
    Working,
    /// <summary>산출물이 보드에 등장해 회수만 가능한 상태.</summary>
    Ready
}
