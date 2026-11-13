using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>냉장이 실제로 적용되는 접촉 관계 하나. 방향은 냉장원에서 신선도 대상으로 향한다.</summary>
internal readonly struct PuzzleRefrigerationContact
{
    internal PuzzlePlacement Source { get; }
    internal PuzzlePlacement Target { get; }
    internal (Vector2Int Source, Vector2Int Target) Cells { get; }

    internal PuzzleRefrigerationContact(PuzzlePlacement _source, PuzzlePlacement _target,
        (Vector2Int Source, Vector2Int Target) _cells)
    {
        Source = _source;
        Target = _target;
        Cells = _cells;
    }
}

/// <summary>
/// 냉장 적용 판정의 원본. 시간 경과 계산과 화살표 표시가 같은 조건·같은 접촉을 쓰도록 여기에만 둔다.
/// 조건은 같은 보드, 서로 다른 물건, 냉장 태그, 신선도가 남은 대상, 상하좌우 접촉이다.
/// </summary>
internal static class PuzzleRefrigeration
{
    static readonly Vector2Int[] CONTACT_OFFSETS =
    {
        Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
    };

    internal static int AdjacentReductionPercent(IEnumerable<PuzzlePlacement> _sources, PuzzlePlacement _item,
        string _sourceTag, int _reductionPercent, ICollection<PuzzleRefrigerationContact> _contacts = null)
    {
        if (_item.RemainingShelfLifeMinutes is not > 0)
        {
            return 0;
        }

        int remainingPercent = PuzzleInventoryValuation.BASE_PERCENT;
        foreach (var source in _sources)
        {
            // 소모율이 이미 0이면 뒤의 냉장원은 이번 정산에 기여하지 못한다. 기여한 관계만 표시한다.
            if (remainingPercent <= 0)
            {
                break;
            }

            if (source == _item || !source.Definition.Tags.Contains(_sourceTag))
            {
                continue;
            }

            if (!TryGetContact(source, _item, out var cells))
            {
                continue;
            }

            remainingPercent -= _reductionPercent;
            _contacts?.Add(new PuzzleRefrigerationContact(source, _item, cells));
        }

        return PuzzleInventoryValuation.BASE_PERCENT - System.Math.Max(0, remainingPercent);
    }

    // 보드 전체의 냉장 접촉 관계. 목록 순서는 이동 이력으로 달라질 수 있어 인스턴스 ID 순서로 결정적으로 남긴다.
    internal static IReadOnlyList<PuzzleRefrigerationContact> Contacts(IEnumerable<PuzzlePlacement> _placements,
        PuzzleFreshnessData _freshness)
    {
        var ordered = _placements.OrderBy(_placement => _placement.InstanceId).ToList();
        var contacts = new List<PuzzleRefrigerationContact>();
        foreach (var item in ordered)
        {
            AdjacentReductionPercent(ordered, item, _freshness.refrigerationTag, _freshness.refrigerationReductionPercent, contacts);
        }

        return contacts;
    }

    /// <summary>상하좌우 접촉 여부와 표시에 쓸 접촉 칸 쌍. 두 물건의 순서가 바뀌어도 접촉 판정은 같다.</summary>
    internal static bool TryGetContact(PuzzlePlacement _source, PuzzlePlacement _target,
        out (Vector2Int Source, Vector2Int Target) _cells)
    {
        foreach (var cell in _source.Pose.Cells)
        {
            foreach (var offset in CONTACT_OFFSETS)
            {
                foreach (var other in _target.Pose.Cells)
                {
                    if (other.Position == cell.Position + offset)
                    {
                        _cells = (cell.Position, other.Position);
                        return true;
                    }
                }
            }
        }

        _cells = default;
        return false;
    }
}
