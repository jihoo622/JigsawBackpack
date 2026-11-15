/// <summary>툴팁 일반 효과에 덧붙이는 진행도. 값은 분 단위 원본이며 표시 서식은 Presentation이 만든다.</summary>
public readonly struct PuzzleProgressDisplay
{
    public long ElapsedMinutes { get; }
    public long TotalMinutes { get; }
    public bool SpaceBlocked { get; }

    public PuzzleProgressDisplay(long _elapsedMinutes, long _totalMinutes, bool _spaceBlocked)
    {
        ElapsedMinutes = _elapsedMinutes;
        TotalMinutes = _totalMinutes;
        SpaceBlocked = _spaceBlocked;
    }
}

/// <summary>툴팁 일반 효과 한 행. 설명은 이미 서식화된 문자열이고 진행도는 선택 사항이다.</summary>
public readonly struct PuzzleEffectDisplay
{
    public string Description { get; }
    public PuzzleProgressDisplay? Progress { get; }

    public PuzzleEffectDisplay(string _description, PuzzleProgressDisplay? _progress = null)
    {
        Description = _description;
        Progress = _progress;
    }
}

/// <summary>툴팁 효과 한 행. 왼쪽 설명과 오른쪽 끝 수치(없으면 빈 문자열)로 나뉜다.</summary>
public readonly struct PuzzleTooltipEffectEntry
{
    public string Description { get; }
    public string Value { get; }

    public PuzzleTooltipEffectEntry(string _description, string _value)
    {
        Description = _description;
        Value = _value;
    }
}
