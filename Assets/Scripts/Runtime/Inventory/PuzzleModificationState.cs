using System.Collections.Generic;

public sealed class PuzzleModificationState
{
    public string CityId { get; }
    public TrainventoryWorkshopData Workshop { get; }
    public PuzzleInventory Board { get; }
    public string SelectedModeId { get; internal set; }
    public PuzzleModificationPhase Phase { get; internal set; } = PuzzleModificationPhase.Idle;
    public long ReadyAtHours { get; internal set; } = -1;
    internal IReadOnlyList<PuzzleDefinitionSwap> PendingSwaps { get; set; }

    internal PuzzleModificationState(string _cityId, TrainventoryWorkshopData _workshop, PuzzleInventory _board)
    {
        CityId = _cityId;
        Workshop = _workshop;
        Board = _board;
        SelectedModeId = null;
    }
}

public enum PuzzleModificationPhase
{
    Idle,
    Working,
    Ready
}

public enum PuzzleModificationFailure
{
    None,
    Blocked,
    Unavailable,
    NoMode,
    Empty,
    InvalidItem,
    NoConversion,
    AlreadyModified,
    InvalidResult,
    InsufficientFunds,
    Deadline
}
