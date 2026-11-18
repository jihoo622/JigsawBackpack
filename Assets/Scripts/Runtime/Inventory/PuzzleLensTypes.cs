using System;
using System.Collections.Generic;
using UnityEngine;

// 행선지 렌즈(L안)의 표시 전용 읽기 결과. 거래 권한이나 확정 근거로 쓰지 않는다. 확정은 기존 ApplyDrop 재평가를 거친다.
// 설계: Docs/Research/ROUTE_LENS_IMPLEMENTATION_PLAN.md §4

// 배치 영향을 받는 화물 하나의 정산 1회 증가분(FinalValue − BaseValue) 전후.
internal readonly struct PuzzleSettlementChange
{
    internal int InstanceId { get; }
    internal string DisplayName { get; }
    internal decimal Before { get; }
    internal decimal After { get; }
    internal decimal Delta => After - Before;

    internal PuzzleSettlementChange(int _instanceId, string _displayName, decimal _before, decimal _after)
    {
        InstanceId = _instanceId;
        DisplayName = _displayName;
        Before = _before;
        After = _after;
    }
}

// 한 자리에 놓았을 때의 다음 정산(다음 턴) 가치 변화. SettlementDelta는 목적 화차(와 기차 안 다른 화차에서 빠지는 경우
// 출발 화차)의 Σ(FinalValue − BaseValue) 변화로, HUD 정산 예상(TrainSynergyDelta)의 변화량과 같다. 행선지 가격·운임은 포함하지 않는다.
internal sealed class PuzzlePlacementDelta
{
    internal PuzzlePose Pose { get; }
    // 잡은 칸이 놓이는 목표 칸. 2·3순위 배지를 이 칸에 둔다.
    internal Vector2Int TargetCell { get; }
    internal decimal SettlementDelta { get; }
    // 옮겨지는 화물 자신을 제외한, 값이 바뀐 이웃만 담는다.
    internal IReadOnlyList<PuzzleSettlementChange> Affected { get; }

    internal PuzzlePlacementDelta(PuzzlePose _pose, Vector2Int _targetCell, decimal _settlementDelta,
        IReadOnlyList<PuzzleSettlementChange> _affected)
    {
        Pose = _pose;
        TargetCell = _targetCell;
        SettlementDelta = _settlementDelta;
        Affected = _affected ?? Array.Empty<PuzzleSettlementChange>();
    }
}

// 현재 화차·현재 회전에서 유효한 자리의 정산 Δ 순위. 동률은 위→아래, 왼→오른 순서.
internal sealed class PuzzlePlacementRanking
{
    internal PuzzleInventory Destination { get; }
    internal int ClockwiseTurns { get; }
    internal IReadOnlyList<PuzzlePlacementDelta> Top { get; }
    internal int ValidCount { get; }
    internal double ElapsedMilliseconds { get; }

    internal PuzzlePlacementRanking(PuzzleInventory _destination, int _clockwiseTurns,
        IReadOnlyList<PuzzlePlacementDelta> _top, int _validCount, double _elapsedMilliseconds)
    {
        Destination = _destination;
        ClockwiseTurns = _clockwiseTurns;
        Top = _top ?? Array.Empty<PuzzlePlacementDelta>();
        ValidCount = _validCount;
        ElapsedMilliseconds = _elapsedMilliseconds;
    }
}

internal enum PuzzleLensLevel
{
    Current,
    Visited,
    Rumor,
    Unknown
}

// 렌즈 탭 순서. View의 탭 버튼 인덱스와 같다.
internal enum PuzzleLensTab
{
    Current = 0,
    Visited = 1,
    Rumor = 2
}

// 행선지 한 곳의 공개 정보. 수준별로 채우는 필드가 다르다(계획 §7).
//   Visited: SellPercent·RegionPercent·Quote(현재 가치 기준 공식 견적)
//   Rumor:   RumorMessage·RumorHoursAgo·RumorStale (견적·등급 수치 없음)
//   Unknown: 운임·이동 턴만
// Planned는 지도에서 경로를 계획한 목적지다(직결이 아니어도 포함). 공개 수준은 위 규칙을 그대로 따른다.
internal readonly struct PuzzleLensDestination
{
    internal string CityId { get; }
    internal string CityName { get; }
    internal PuzzleLensLevel Level { get; }
    internal int? SellPercent { get; }
    internal int? RegionPercent { get; }
    internal long? Quote { get; }
    internal string RumorMessage { get; }
    internal long? RumorHoursAgo { get; }
    internal bool RumorStale { get; }
    // 구간 수. 직결 이웃은 1, 계획 목적지는 남은 구간 수.
    internal int Legs { get; }
    // 도착까지 쓰는 턴(구간 × travelTurns).
    internal long TravelTurns { get; }
    // 이 화물을 실은 상태의 운임(운임 칸 기준, 목재·기계 등 운임 면제 태그 제외). 계산할 수 없으면 null.
    internal long? Fare { get; }
    internal bool Planned { get; }

    internal PuzzleLensDestination(string _cityId, string _cityName, PuzzleLensLevel _level, int? _sellPercent,
        int? _regionPercent, long? _quote, string _rumorMessage, long? _rumorHoursAgo, bool _rumorStale,
        int _legs, long _travelTurns, long? _fare, bool _planned)
    {
        CityId = _cityId;
        CityName = _cityName;
        Level = _level;
        SellPercent = _sellPercent;
        RegionPercent = _regionPercent;
        Quote = _quote;
        RumorMessage = _rumorMessage;
        RumorHoursAgo = _rumorHoursAgo;
        RumorStale = _rumorStale;
        Legs = _legs;
        TravelTurns = _travelTurns;
        Fare = _fare;
        Planned = _planned;
    }
}

// 배치 후보 표시 설정(D4: 기본 켜짐, Esc 일시정지 메뉴에서 끄기). 기기 단위 PlayerPrefs에 저장한다.
internal static class PuzzleLensSettings
{
    const string SHOW_CANDIDATES_KEY = "routeLens.showCandidates";

    internal static event Action Changed;

    internal static bool ShowCandidates
    {
        get => PlayerPrefs.GetInt(SHOW_CANDIDATES_KEY, 1) != 0;
        set
        {
            if (ShowCandidates == value)
            {
                return;
            }
            PlayerPrefs.SetInt(SHOW_CANDIDATES_KEY, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
