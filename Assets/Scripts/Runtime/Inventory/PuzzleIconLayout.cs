using System;
using UnityEngine;

public readonly struct PuzzleIconLayout
{
    public Vector2 Center { get; }
    public Vector2 Size { get; }

    PuzzleIconLayout(Vector2 _center, Vector2 _size)
    {
        Center = _center;
        Size = _size;
    }

    // Center는 보드 좌표(+y 위), Size는 회전 전 이미지 사각형의 칸 단위 크기다.
    public static PuzzleIconLayout Calculate(Vector2Int _boardMin, Vector2Int _boardMax,
        Vector2Int _originalMin, Vector2Int _originalMax, Vector2 _size, Vector2 _offset, int _clockwiseTurns)
    {
        Vector2 center = ((Vector2)_boardMin + _boardMax) * 0.5f + RotateOffset(_offset, _clockwiseTurns);
        Vector2 size = _size.x > 0f && _size.y > 0f
            ? _size : (Vector2)(_originalMax - _originalMin + Vector2Int.one);
        return new PuzzleIconLayout(center, size);
    }

    public static Vector2 RotateOffset(Vector2 _offset, int _clockwiseTurns)
    {
        return _clockwiseTurns switch
        {
            0 => _offset,
            1 => new Vector2(_offset.y, -_offset.x),
            2 => -_offset,
            3 => new Vector2(-_offset.y, _offset.x),
            _ => throw new ArgumentOutOfRangeException(nameof(_clockwiseTurns))
        };
    }
}
