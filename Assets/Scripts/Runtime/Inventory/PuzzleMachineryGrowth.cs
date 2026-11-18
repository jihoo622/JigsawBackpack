using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 기계와 최소 1면 닿은 인접 대상 쌍. 면 수는 점유 칸의 공유 변으로 세며 칸 겹침이 아니다.
// 시간 정산과 UI 표시가 같은 조회를 쓴다.
internal readonly struct PuzzleMachineryContact
{
    internal PuzzlePlacement Machine { get; }
    internal PuzzlePlacement Target { get; }
    internal int ContactFaces { get; }

    internal PuzzleMachineryContact(PuzzlePlacement _machine, PuzzlePlacement _target, int _contactFaces)
    {
        Machine = _machine;
        Target = _target;
        ContactFaces = _contactFaces;
    }
}

// 기계 성장 공통 판정. 기준 면 수와 턴당 성장률은 표 machineryGrowth가 원본이며,
// 집중 강화가 붙은 인스턴스는 강화의 값으로 대체한다.
internal static class PuzzleMachineryGrowth
{
    static readonly Vector2Int[] CONTACT_OFFSETS =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // 기계 인스턴스에 붙은 집중 강화. 없으면 null.
    internal static PuzzleItemUpgradeData FocusUpgrade(PuzzlePlacement _machine)
    {
        if (_machine == null)
        {
            return null;
        }
        foreach (string id in _machine.AppliedUpgradeIds)
        {
            if (TableManager.Instance.TryGetItemUpgrade(id, out var upgrade) &&
                TableManager.TryGetUpgradeEffect(upgrade, out PuzzleUpgradeEffect effect) &&
                effect == PuzzleUpgradeEffect.GrowthFocus)
            {
                return upgrade;
            }
        }
        return null;
    }

    // 기계의 유효 성장 기준. 기계가 아닌 품목이면 false다.
    internal static bool TryGetGrowth(PuzzlePlacement _machine, PuzzleMachineryGrowthData _base,
        out int _minContactCells, out int _growthPercentPerTurn)
    {
        _minContactCells = 0;
        _growthPercentPerTurn = 0;
        if (_machine == null || !_machine.Definition.Tags.Contains(_base.machineryTag))
        {
            return false;
        }
        _minContactCells = _base.minContactCells;
        _growthPercentPerTurn = _base.growthPercentPerTurn;
        var focus = FocusUpgrade(_machine);
        if (focus != null)
        {
            _minContactCells = focus.minContactCells;
            _growthPercentPerTurn = focus.growthPercentPerTurn;
        }
        return true;
    }

    // 같은 객차 안 기계·물건 접촉 쌍. 기준 충족 여부와 무관하게 닿은 면이 1개 이상인 쌍을 모두 담고,
    // 기준 면 수는 호출 측에서 TryGetGrowth와 비교해 판정한다.
    internal static IReadOnlyList<PuzzleMachineryContact> Contacts(IEnumerable<PuzzlePlacement> _placements,
        PuzzleMachineryGrowthData _base)
    {
        var items = _placements.OrderBy(_item => _item.InstanceId).ToList();
        var contacts = new List<PuzzleMachineryContact>();
        foreach (var machine in items)
        {
            if (!machine.Definition.Tags.Contains(_base.machineryTag))
            {
                continue;
            }
            foreach (var target in items)
            {
                if (ReferenceEquals(machine, target))
                {
                    continue;
                }
                int faces = CountFaces(machine, target);
                if (faces > 0)
                {
                    contacts.Add(new PuzzleMachineryContact(machine, target, faces));
                }
            }
        }
        return contacts;
    }

    // 두 배치 사이 공유 변의 수. 변 하나는 기계 칸에서 본 이웃 방향이 대상 칸에 겹칠 때 센다.
    static int CountFaces(PuzzlePlacement _machine, PuzzlePlacement _target)
    {
        var occupied = new HashSet<Vector2Int>(_target.Pose.Cells.Select(_cell => _cell.Position));
        int faces = 0;
        foreach (var cell in _machine.Pose.Cells)
        {
            foreach (var offset in CONTACT_OFFSETS)
            {
                if (occupied.Contains(cell.Position + offset))
                {
                    faces++;
                }
            }
        }
        return faces;
    }
}
