using System;

[Serializable]
public sealed class BasicDataTableData
{
    public string displayName, status, note;
    public BasicDataGroupData[] groups = Array.Empty<BasicDataGroupData>();
}

// 테이블 원본은 Assets/Resources/Tables에만 두고, 이 클래스들은 직렬화 필드만 가진다.
// 조회는 TableManager, 검증·파생 값 계산은 GameTableRules가 담당한다.
// 파일명은 데이터 클래스명과 일치시켜야 한다(TableManager.Load가 그 이름으로 읽는다).

// 플레이 공통 수치와 해금 효과 목록. 예시 필드이므로 프로젝트에서 필드를 더하거나 바꾼다.
[Serializable]
public sealed class GameplayTableData
{
    public string status, note;
    public PlayerData player;
    public UpgradeSequenceData sequence;
    public UpgradeData[] upgrades = Array.Empty<UpgradeData>();
}

// 씬 진행 경로(타이틀 → 게임 → 재시작).
[Serializable]
public sealed class FlowTableData
{
    public string status, note;
    public SceneLinkData[] scenes = Array.Empty<SceneLinkData>();
}

[Serializable]
public sealed class PuzzleTableData
{
    public string status, note;
    public PuzzlePieceData[] pieces = Array.Empty<PuzzlePieceData>();
    public PuzzleBoardData train;
    public string startCity;
    // 지도상 예정 목적지. 실제 상환 장소 판정인 debtCityId와 별개다.
    public string planningTargetCityId;
    public PuzzleCityData[] cities = Array.Empty<PuzzleCityData>();
    public PuzzleRegionData[] regions = Array.Empty<PuzzleRegionData>();
    public PuzzleMarketStockData[] stockProfiles = Array.Empty<PuzzleMarketStockData>();
}

[Serializable]
public sealed class PuzzleSynergyTableData
{
    public string status, note;
    public int activationIntervalHours = -1;
    public PuzzleSynergyRuleData[] rules = Array.Empty<PuzzleSynergyRuleData>();
}

// 마을별 추가 행동(도축 등)의 원본. 도시 id·작업 시간·변환 관계를 이 표만 안다.
[Serializable]
public sealed class PuzzleCityActionTableData
{
    public string status, note;
    public PuzzleCityActionData[] actions = Array.Empty<PuzzleCityActionData>();
}

[Serializable]
public sealed class PuzzleTradeTableData
{
    public string status, note;
    public long startingFunds = -1;
    // 화물칸 증차 시 기본 단위 비용 (예: 50원씩 누적 증가)
    public int wagonAddCostBase = -1;
    public long gameClearCost = -1;
    // 0이면 무기한. 목표 도시가 비어 있으면 상환 장소를 제한하지 않는다.
    public int deadlineHours = -1;
    public string debtCityId;
    public float travelEffectSeconds = -1f;
    public float travelArrivalHoldSeconds = -1f;
}

[Serializable]
public sealed class PuzzleRouteTableData
{
    public string status, note;
    // 기차의 점유 칸 1칸에 더해지는 단가. 0이면 보유량과 무관한 정액 노선이다.
    public int costPerCell = -1;
    public int costPerDistance = -1;
    public PuzzleRouteData[] routes = Array.Empty<PuzzleRouteData>();
}

[Serializable]
public sealed class PuzzleTimeTableData
{
    public string status, note;
    public double pixelsPerHour = -1;
    public int daylightHours = -1;
    public int nightHours = -1;
    public int daylightStartHour = -1;
    public int startHour = -1;
    public long restMinutes = -1;
}
