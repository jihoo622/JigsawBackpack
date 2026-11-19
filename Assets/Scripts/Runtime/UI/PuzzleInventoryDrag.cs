using System.Collections.Generic;
using UnityEngine;

internal sealed class PuzzleInventoryDrag
{
    readonly PuzzlePlacement original;
    readonly Vector2Int grabbedCell;
    int clockwiseTurns;

    public PuzzleInventory Source => original.Owner;
    public PuzzleItemDefinition Definition => original.Definition;
    public int? FreshnessPercent => original.FreshnessPercent;
    public int InstanceId => original.InstanceId;
    public PuzzlePose OriginalPose => original?.Pose;
    public PuzzlePlacement OriginalPlacement => original;
    public int ClockwiseTurns => clockwiseTurns;

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

    // 묶음 회원이 잡은 조각의 고스트 포즈(_anchorPose)와 강체로 함께 움직이는 표시용 포즈. 잡은 조각 제외.
    public List<(PuzzlePose Pose, PuzzleItemDefinition Item, int? Freshness, PuzzlePlacement Placement)> GetBundleGhostExtras(
        PuzzlePose _anchorPose)
    {
        var extras = new List<(PuzzlePose Pose, PuzzleItemDefinition Item, int? Freshness, PuzzlePlacement Placement)>();
        foreach (var member in Source.GetBundleGhostPoses(original, grabbedCell, _anchorPose))
        {
            if (ReferenceEquals(member.Member, original))
            {
                continue;
            }
            extras.Add((member.Pose, member.Member.Definition, member.Member.FreshnessPercent, member.Member));
        }
        return extras;
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
