using System.Collections.Generic;
using UnityEngine;

public sealed class RailRouteDraft : ScriptableObject
{
    public string railId;
    public List<Vector2> points = new List<Vector2>();
    public Vector2 timeLabel;
}
