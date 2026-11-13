using UnityEngine;

// 레이아웃·역·노선·지형 원본과 역 ID를 참조하는 마을 행동·거래 테이블을 하나의 Undo 단위로 보관한다.
// 저장 전에는 파일과 프리팹을 건드리지 않는다.
public sealed class WorldMapDraft : ScriptableObject
{
    public string layout;
    public string puzzle;
    public string routes;
    public string geography;
    public string cityActions;
    public string trade;
}
