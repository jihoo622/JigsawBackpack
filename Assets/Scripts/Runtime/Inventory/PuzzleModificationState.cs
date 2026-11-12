public sealed class PuzzleModificationState
{
    public string CityId { get; }
    public PuzzleCityActionData Action { get; }
    public PuzzleInventory Board { get; }
    public string SelectedModeId { get; internal set; }

    internal PuzzleModificationState(string _cityId, PuzzleCityActionData _action, PuzzleInventory _board)
    {
        CityId = _cityId;
        Action = _action;
        Board = _board;
        SelectedModeId = null;
    }
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
    InvalidResult
}
