using System;

// 객차에 설치되는 개조 설정이다. 시간 진행 미리보기에서도 같은 설정을 읽으므로 설치 후 값은 바뀌지 않아야 한다.
internal abstract class PuzzleWagonModification
{
    internal string Id { get; }

    protected PuzzleWagonModification(string _id)
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            throw new ArgumentException("객차 개조 ID가 비어 있습니다.", nameof(_id));
        }
        Id = _id;
    }

    internal virtual bool CanInstallOn(PuzzleInventory _wagon)
    {
        return true;
    }
}

internal sealed class PuzzleWagonRefrigeration : PuzzleWagonModification, IPuzzlePreservationEffect
{
    internal const string ModificationId = "refrigeration";
    readonly int reductionPercent;

    internal PuzzleWagonRefrigeration(int _reductionPercent) : base(ModificationId)
    {
        reductionPercent = _reductionPercent;
    }

    public int ReductionPercentFor(PuzzleInventory _wagon, PuzzlePlacement _item) => reductionPercent;
}
