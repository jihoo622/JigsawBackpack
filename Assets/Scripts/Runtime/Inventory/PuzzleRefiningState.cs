using System.Collections.Generic;

// 마을 정제대의 씬 로컬 실행 상태. 정의·시간·변환 관계는 테이블이 소유하고, 여기에는 보드 인스턴스와 진행 상황만 둔다.
public sealed class PuzzleRefiningState
{
    public string CityId { get; }
    public PuzzleCityActionData Action { get; }
    public PuzzleInventory Board { get; }
    public PuzzleRefiningPhase Phase { get; internal set; }
    // 완성 시점(누적 경과 분). Idle이면 -1.
    public long ReadyAtMinutes { get; internal set; }
    // 확인 시점에 수량이 확정된 산출물. 완료 때 보드에 올리고, 공간이 없어 남으면 도시 상태에 보존해 다시 배치한다.
    internal List<string> PendingOutputs { get; } = new List<string>();
    // 이번 정제로 확정된 산출물 총개수.
    internal int OutputCount { get; set; }

    internal PuzzleRefiningState(string _cityId, PuzzleCityActionData _action, PuzzleInventory _board)
    {
        CityId = _cityId;
        Action = _action;
        Board = _board;
        Phase = PuzzleRefiningPhase.Idle;
        ReadyAtMinutes = -1;
    }
}

public enum PuzzleRefiningPhase
{
    /// <summary>보드가 비어 있고 새로 맡길 수 있는 상태.</summary>
    Idle,
    /// <summary>광석을 맡기고 수량이 확정된 채 작업이 진행 중인 상태. 반입·반출을 막는다.</summary>
    Working,
    /// <summary>산출물이 보드에 등장해 회수만 가능한 상태.</summary>
    Ready
}
