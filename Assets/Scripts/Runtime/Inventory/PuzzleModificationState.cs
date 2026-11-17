using System.Collections.Generic;

public sealed class PuzzleModificationState
{
    public string CityId { get; }
    public PuzzleCityActionData Action { get; }
    public PuzzleInventory Board { get; }
    public string SelectedModeId { get; internal set; }
    public PuzzleModificationPhase Phase { get; internal set; } = PuzzleModificationPhase.Idle;
    public long ReadyAtTurns { get; internal set; } = -1;
    internal IReadOnlyList<PuzzleDefinitionSwap> PendingSwaps { get; set; }

    internal PuzzleModificationState(string _cityId, PuzzleCityActionData _action, PuzzleInventory _board)
    {
        CityId = _cityId;
        Action = _action;
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
    InsufficientFunds
}
