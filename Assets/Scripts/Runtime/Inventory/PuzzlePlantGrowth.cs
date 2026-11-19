using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 식물 자연 발아 계산. 후보는 보드 칸으로만 고르고 적용은 퍼즐 인벤토리의 TryExtendPlant가 한다.
// 씨앗만 있을 때는 품목 로컬 북쪽(회전과 함께 돈다) 한 칸이 유일한 후보이고,
// 발아 후에는 씨앗을 제외한 모든 줄기에서 보드 상하좌우로 뻗는다.
internal static class PuzzlePlantGrowth
{
    static readonly Vector2Int NORTH_LOCAL = new Vector2Int(0, 1);
    static readonly Vector2Int[] SIDES =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
    };

    internal static bool IsPlant(PuzzlePlacement _item)
    {
        return _item != null && _item.Definition.SeedCell != null;
    }

    // 이 식물이 지금 성장할 수 있는 빈 칸 후보를 안정 순서(y 오름차순, 그다지 x 오름차순)로 모은다.
    // 경계·막힘·점유·전용 구역은 미리 통과한 칸만 담으므로 적용 시에는 실패 재확인만 남는다.
    internal static List<Vector2Int> Candidates(PuzzlePlacement _plant)
    {
        return ContactCells(_plant)
            .Where(_cell => _plant.Owner.CanGrowInto(_cell, _plant.Definition, _plant.LocalCells.Count))
            .ToList();
    }

    // 자연 발아나 물·거름 접촉으로 성장할 수 있는 칸의 위치 규칙만 적용한 결과. 점유와 구역은 보지 않으며
    // 빈 칸 여부와 무관하게 같은 좌표를 낸다. 순서는 y 오름차순, 그다지 x 오름차순으로 안정적이다.
    internal static List<Vector2Int> ContactCells(PuzzlePlacement _plant)
    {
        var result = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        var rotation = new PuzzleRotation(_plant.LocalCells, _plant.Pose.ClockwiseTurns);
        if (_plant.PlantStemCells.Count == 0)
        {
            // 목재 결합과 같은 로컬 북쪽 규약: (0,1) 칸이 Origin 쪽을 가리키는 방향이 화면 북쪽이다.
            Vector2Int north = rotation.GetBoardOffset(Vector2Int.zero) - rotation.GetBoardOffset(NORTH_LOCAL);
            AddContact(SeedPosition(_plant) + north, seen, result);
        }
        else
        {
            foreach (var cell in _plant.Pose.Cells)
            {
                if (!_plant.PlantStemCells.Contains(cell.OriginalCell))
                {
                    continue;
                }
                foreach (Vector2Int side in SIDES)
                {
                    AddContact(cell.Position + side, seen, result);
                }
            }
        }
        return result
            .OrderBy(_cell => _cell.y)
            .ThenBy(_cell => _cell.x)
            .ToList();
    }

    static void AddContact(Vector2Int _cell, HashSet<Vector2Int> _seen, List<Vector2Int> _result)
    {
        if (_seen.Add(_cell))
        {
            _result.Add(_cell);
        }
    }

    static Vector2Int SeedPosition(PuzzlePlacement _plant)
    {
        Vector2Int seed = _plant.Definition.SeedCell.Value;
        foreach (var cell in _plant.Pose.Cells)
        {
            if (cell.OriginalCell == seed)
            {
                return cell.Position;
            }
        }
        throw new InvalidOperationException("식물 배치에 씨앗 칸이 없습니다.");
    }

    // 발아 칸 선택용 xorshift64. 상태는 0이 아니어야 하고, 행동 분할 시에도 같은 순서로 진행해야 한다.
    internal static int NextIndex(ref ulong _state, int _count)
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;
        return (int)(_state % (ulong)_count);
    }
}
