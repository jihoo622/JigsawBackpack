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
    public PuzzleTagPresentationData[] tagPresentations = Array.Empty<PuzzleTagPresentationData>();
    public PuzzleBoardData train;
    public string startCity;
    // 지도상 예정 목적지. 실제 상환 장소 판정인 debtCityId와 별개다.
    public string planningTargetCityId;
    public PuzzleCityData[] cities = Array.Empty<PuzzleCityData>();
    public PuzzleRegionData[] regions = Array.Empty<PuzzleRegionData>();
    public PuzzleMarketStockData[] stockProfiles = Array.Empty<PuzzleMarketStockData>();
    // 판매 등급별 배율의 단일 원본. 도시 태그 행의 sellGradeId가 이 목록의 id를 참조한다.
    public PuzzleSellGradeData[] sellGrades = Array.Empty<PuzzleSellGradeData>();
}

[Serializable]
public sealed class PuzzleSynergyTableData
{
    public string status, note;
    public int activationIntervalHours = -1;
    public PuzzleSynergyRuleData[] rules = Array.Empty<PuzzleSynergyRuleData>();
    public PuzzleAnimalSynergyData animals;
    public PuzzleFreshnessData freshness;
}

// 술집 소문의 종류 선택 확률. 후보와 문장은 코드가 만들고 이 표는 두 종류를 고르는 비율만 원본으로 둔다.
[Serializable]
public sealed class PuzzleRumorTableData
{
    public string status, note;
    public int specialtyPercent = -1;
    public int rarePercent = -1;
    // 소문에서 빼는 품목 ID. 특산 후보만 막고 품목의 시장 유통은 그대로 둔다.
    public string[] excludedItemIds = Array.Empty<string>();
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
    // 화물칸 최대 증차 상한 (8칸)
    public int maxWagonCount = -1;
    public int wagonRefrigerationCost = -1;
    public PuzzleWagonProductData[] wagonProducts = Array.Empty<PuzzleWagonProductData>();
    public long gameClearCost = -1;
    // 0이면 무기한. 목표 도시가 비어 있으면 상환 장소를 제한하지 않는다.
    public int deadlineHours = -1;
    public string debtCityId;
    public float travelEffectSeconds = -1f;
    public float travelArrivalHoldSeconds = -1f;
}

[Serializable]
public sealed class PuzzleWagonProductData
{
    public string id, displayName;
    public long price = -1;
    public string modificationId;
}

[Serializable]
public sealed class PuzzleRouteTableData
{
    public string status, note;
    // 맵 제작 도구의 새 노선/일괄 적용 기준. 실행 요금은 각 routes[].cost를 읽는다.
    public int defaultBaseCost = -1;
    public double fareDistancePixelsPerUnit = -1;
    // 기차의 점유 칸 1칸에 더해지는 단가. 0이면 보유량과 무관한 정액 노선이다.
    public int costPerCell = -1;
    public int costPerDistance = -1;
    public PuzzleRouteData[] routes = Array.Empty<PuzzleRouteData>();
}

// 술집 의뢰. 의뢰 생성 규칙·보상·패널티·평판 단계의 원본이다. 진행 상태는 세션이 가진다.
// 평판은 별도로 쌓지 않고 거래 숙련 레벨의 합으로 계산한다(TrainventoryProficiencyTableData).
[Serializable]
public sealed class TrainventoryQuestTableData
{
    public string status, note;
    // 의뢰 화물에만 붙는 전용 태그. 의뢰 화물의 판별과 표시에 쓴다.
    public string cargoTag;
    // 마을 방문 한 번에 술집이 내놓는 의뢰 수.
    public int offersPerTavern = -1;
    // 완료 시 의뢰 화물의 생산 태그에 주는 숙련 경험치. 의뢰 보상 금액 대비 백분율이다.
    public int proficiencyXpPercentOfReward = -1;
    public TrainventoryQuestReputationData[] reputationLevels = Array.Empty<TrainventoryQuestReputationData>();
    public TrainventoryQuestPenaltyData[] penalties = Array.Empty<TrainventoryQuestPenaltyData>();
    public TrainventoryCourierQuestData[] courierQuests = Array.Empty<TrainventoryCourierQuestData>();
}

// 태그별 거래 숙련도. 숙련 대상 태그는 PuzzleTableData.regions[].productionTags의 합집합이다.
// 경험치·레벨 진행은 세션이 가지고, 여기에는 경험치 획득률·레벨 기준·거래 이득 수치만 둔다.
[Serializable]
public sealed class TrainventoryProficiencyTableData
{
    public string status, note;
    // 다른 마을에서 실어 온 화물을 팔 때 판매 금액 대비 얻는 경험치 백분율.
    public int xpPercentOfSalePrice = -1;
    // 1레벨부터 차례로 필요한 누적 경험치. 오름차순이다.
    public long[] levelThresholds = Array.Empty<long>();
    // 마지막 기준을 넘은 뒤 1레벨마다 더 필요한 경험치. 레벨 상한은 없다.
    public long extraLevelXp = -1;
    // 1레벨당 최종 구매가 할인·최종 판매가 할증 비율(%).
    public float bonusPercentPerLevel = -1;
}

[Serializable]
public sealed class PuzzleTimeTableData
{
    public string status, note;
    public double pixelsPerHour = -1;
    // 노선 소요 시간의 올림 단위(분). 60이면 25시간 44분 → 26시간.
    public int travelRoundingMinutes = -1;
    public int daylightHours = -1;
    public int nightHours = -1;
    public int daylightStartHour = -1;
    public int startHour = -1;
    public long restMinutes = -1;
}
