using UnityEngine;

// 세 원본을 하나의 Undo 단위로 보관한다. 저장 전에는 파일과 프리팹을 건드리지 않는다.
public sealed class WorldMapDraft : ScriptableObject
{
    public string layout;
    public string puzzle;
    public string routes;
}
