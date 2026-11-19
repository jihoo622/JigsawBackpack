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
    // 시작 전용 화차. 생략한 기존 데이터는 train을 시작 화차로 사용한다.
    public PuzzleBoardData startingTrain;
    public string startCity;
    // 지도에 ★로 표시하는 예정 목적지. 비우면 표시하지 않는다. 규칙에는 쓰지 않는다.
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
    // 규칙이 적용될 때 대상·발생원 중 숙련 대상 태그의 레벨 합에 곱해 multiplierPercent에 더하는 보강 계수(%p).
    public float proficiencyComboPercentPerLevel;
    public PuzzleSynergyRuleData[] rules = Array.Empty<PuzzleSynergyRuleData>();
    public PuzzleAnimalSynergyData animals;
    public PuzzleFreshnessData freshness;
}

// 같은 객차에서 인접한 품목 두 개를 정해진 시간 후 하나로 바꾸는 공용 조합 규칙. 진행 상태는 세션이 가진다.
[Serializable]
public sealed class PuzzleCombinationTableData
{
    public string status, note;
    public PuzzleCombinationRuleData[] rules = Array.Empty<PuzzleCombinationRuleData>();
}

// 특산 소문 후보에서 제외할 품목.
[Serializable]
public sealed class PuzzleRumorTableData
{
    public string status, note;
    // 시작 마을의 첫 대화에서 제공하는 선호 소문 한 건. 생략하면 희귀 수요·특산·행동에서 선택한다.
    public PuzzleRumorPreferenceData firstPreference;
    // 소문에서 빼는 품목 ID. 특산 후보만 막고 품목의 시장 유통은 그대로 둔다.
    public string[] excludedItemIds = Array.Empty<string>();
    // 소문 재생 주기(게임 시간). 마을 소문을 들은 지 이 시간이 지나면 같은 마을 소문을 다시 들을 수 있다.
    public long refreshHours = -1;
}

// 작업(WorksData)의 원본. 입력·작업 시간·산출·비용을 이 표만 안다.
[Serializable]
public sealed class TrainventoryWorkTableData
{
    public string status, note;
    public TrainventoryWorkData[] works = Array.Empty<TrainventoryWorkData>();
}

// 화차 가공의 원본. 화차 상품(PuzzleTradeTableData.wagonProducts[].processId)이 가공 하나를 가진다.
[Serializable]
public sealed class TrainventoryWagonProcessTableData
{
    public string status, note;
    public TrainventoryWagonProcessData[] processes = Array.Empty<TrainventoryWagonProcessData>();
}

// 작업장의 원본. 작업장 종류·보드·처리할 작업·수수료 계수를 이 표만 안다. 역 배치는 PuzzleTableData.cities[].workshopIds다.
[Serializable]
public sealed class TrainventoryWorkshopTableData
{
    public string status, note;
    public TrainventoryWorkshopData[] workshops = Array.Empty<TrainventoryWorkshopData>();
}

[Serializable]
public sealed class PuzzleTradeTableData
{
    public string status, note;
    public long startingFunds = -1;
    // 화물칸 기본 증차 상한. 평판 1당 1칸씩 더해지고 maxWagonCountCap을 넘지 않는다.
    public int maxWagonCount = -1;
    // 평판으로 늘어나는 증차 상한의 최댓값. 객차 바 슬롯 수 이하여야 한다.
    public int maxWagonCountCap = -1;
    public int wagonRefrigerationCost = -1;
    // 시작 화차의 가치. 판매가는 다른 화차와 같이 가치 × wagonSalePercent / 100이다.
    public long startingWagonValue = -1;
    // 화차 판매가 비율(%). 상품은 price, 시작 화차는 startingWagonValue에 곱하고 내림한다.
    public int wagonSalePercent = -1;
    // 모든 마을에 기본 진열하는 일반 화차 상품 ID.
    public string defaultWagonProductId;
    // 마을마다 회차 시작에 추첨하는 일반 특수 화차 수(중복 없이 진열).
    public int generalSpecialWagonsPerCity = 1;
    public PuzzleWagonProductData[] wagonProducts = Array.Empty<PuzzleWagonProductData>();
    // 목표 잔액(점수 기준). 영업 기간이 끝나 결산할 때 잔액이 이 값 이상이면 승리한다.
    public long targetFunds = -1;
    // 영업 기간(게임 시간). 0이면 무기한이다.
    public int deadlineHours = -1;
    public float travelEffectSeconds = -1f;
    public float travelArrivalHoldSeconds = -1f;
}

[Serializable]
public sealed class PuzzleWagonProductData
{
    public string id, displayName;
    public long price = -1;
    // 화차 효과 설명. 상세·툴팁에 그대로 보인다.
    public string description;
    public string modificationId;
    // 지역 특색 상품은 해당 지역의 마을 한 곳에서 회차 동안 판매한다.
    public string saleRegionId;
    // 일반 특수 상품은 모든 지역의 무작위 진열 후보가 된다.
    public bool randomSale;
    // 이 태그를 가진 화물은 운임의 적재 칸 수에 넣지 않는다.
    public string[] freightExemptTags = Array.Empty<string>();
    // 화차 가공(TrainventoryWagonProcessTableData.processes의 id). 비어 있으면 가공하지 않는다.
    public string processId;
    // 이 화차의 화물칸 보드(크기·막힌 칸). 화차마다 모양이 달라 적재 공간이 다르다. 시작 배치는 쓰지 않는다.
    public PuzzleBoardData board;
}

[Serializable]
public sealed class PuzzleRouteTableData
{
    public string status, note;
    // 맵 제작 도구의 새 노선/일괄 적용 기준. 실행 요금은 각 routes[].cost를 읽는다.
    public int defaultBaseCost = -1;
    public double pixelsPerDistance = -1;
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
    // 하루 24시간 중 낮 시간. 낮 시작(startHour)부터 이만큼이 낮이고 나머지가 밤(시장 진입 불가)이다.
    public int daylightHours = -1;
    // 낮이 시작하는 시각(0~23시). 게임 시작 시각이자 날짜 변경 시각이다.
    public int startHour = -1;
    // 이동 1회가 쓰는 턴. 노선 거리와 무관하게 모든 노선이 같다.
    public int travelTurns = -1;
    // 휴식 화면을 열 때 고른 기본 휴식 턴. 휴식은 1턴부터 하루(4턴)까지 고를 수 있다.
    public int restTurns = -1;
    // 시장 재입고 주기(턴). 경과 0시간부터 이 턴 수의 경계를 지날 때마다 모든 마을 시장을 새로 채운다.
    public int restockTurns = -1;
}
