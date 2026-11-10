using UnityEngine;

internal sealed class PuzzleInventoryDrag
{
    readonly PuzzlePlacement original;
    readonly Vector2Int grabbedCell;
    int clockwiseTurns;

    public PuzzleInventory Source => original.Owner;
    public PuzzleItemDefinition Definition => original.Definition;
    public int InstanceId => original.InstanceId;

    public PuzzleInventoryDrag(PuzzlePlacement _placement, PuzzleCell _cell)
    {
        original = _placement;
        grabbedCell = _cell.OriginalCell;
        clockwiseTurns = _placement.Pose.ClockwiseTurns;
    }

    public void RotateClockwise()
    {
        clockwiseTurns = (clockwiseTurns + 1) % PuzzleRotation.TURN_COUNT;
    }

    public PuzzleDropResult Evaluate(PuzzleInventoryService _session, PuzzleInventory _destination, Vector2Int _targetCell)
    {
        return _session.EvaluateDrop(original, _destination, grabbedCell, _targetCell, clockwiseTurns);
    }

    public PuzzlePose GetGhostPose()
    {
        return Source.GetDragPose(Definition, grabbedCell, clockwiseTurns);
    }

    public PuzzleDropResult DropOnBoard(PuzzleInventoryService _session, PuzzleInventory _destination, Vector2Int _targetCell)
    {
        return _session.Drop(original, _destination, grabbedCell, _targetCell, clockwiseTurns);
    }

    public PuzzleDropResult QuickDropToWagon(PuzzleInventoryService _session, int _wagonIndex)
    {
        return _session.QuickTransferToWagon(original, _wagonIndex, clockwiseTurns);
    }
}
