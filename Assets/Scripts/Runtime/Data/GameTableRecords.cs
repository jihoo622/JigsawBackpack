using System;

[Serializable]
public sealed class BasicDataGroupData
{
    public string id;
    public BasicNumberData[] numbers = Array.Empty<BasicNumberData>();
    public BasicTextData[] texts = Array.Empty<BasicTextData>();
    public BasicColorData[] colors = Array.Empty<BasicColorData>();
}

[Serializable]
public sealed class BasicNumberData
{
    public string id;
    public double value = double.NaN;
    public bool whole;
    public bool positive;
}

[Serializable]
public sealed class BasicTextData
{
    public string id, value;
    public int arguments;
}

[Serializable]
public sealed class BasicColorData
{
    public string id;
    public float r = -1f, g = -1f, b = -1f, a = -1f;
}

// 순수 직렬화 항목. 수치는 JSON에 두고 이 파일에는 필드만 둔다.
// 검증 규칙이나 조회 로직을 여기 넣지 않는다(GameTableRules / TableManager).

[Serializable]
public sealed class PlayerData
{
    public float maxHealth;
    public float moveSpeed;
    public int dashCharges;
    public float dashCooldown;
    public float interactRange;
}

// 해금(확장) 효과 한 항목. id로 찾고 nextUpgrade로 다음 해금을 잇는다.
// category는 Presentation이 색·제목의 기준으로 읽는 키이며 EffectCategory 이름과 일치해야 한다.
[Serializable]
public sealed class UpgradeData
{
    public string id, nextUpgrade, displayName, description, category;
}

// 해금 사슬의 시작. 배열 순서에 의존하지 않기 위한 명시적 시작 키.
[Serializable]
public sealed class UpgradeSequenceData
{
    public string firstUpgrade;
}

// 씬 진행 경로 한 행. 씬 이름이 키이며 배열 순서는 의미하지 않는다.
// nextScene이 비어 있으면 최종 씬, restartScene은 사망·재시작 때 로드하는 씬.
[Serializable]
public sealed class SceneLinkData
{
    public string scene, nextScene, restartScene;
}

[Serializable]
public sealed class PuzzlePieceData
{
    public string id, displayName, description, visualKey;
    public int baseValue = -1;
    // 아이콘 표시 크기(칸 단위). 0 이하면 외접 사각형 크기를 쓴다. 삐져나오는 이미지를 조정한다.
    public float iconWidth = -1, iconHeight = -1;
    // 아이콘 중심을 외접 사각형 중심에서 칸 단위로 어긋나게 옮긴다.
    public float iconOffsetX, iconOffsetY;
    public string[] tags = Array.Empty<string>();
    // 누락된 슬롯 수를 검증에서 거부하기 위한 값이다.
    public int slotCount = -1;
    // 내구도 시작값. 누락이 정상값으로 위장되지 않도록 검증용 값으로 둔다.
    public int durability = -1;
    public bool excludeFromMarket;
    // 개조 커버리지 검사에서 명시적으로 제외하는 기계 품목. 예외는 반드시 이 플래그로 선언한다.
    public bool exemptFromModification;
    public string adultPieceId, growthDescription;
    public int growthHours, growthBlockedHours;
    public int shelfLifeHours;
    public PuzzleCellData[] cells = Array.Empty<PuzzleCellData>();
}

[Serializable]
public sealed class PuzzleCellData
{
    // 필드 누락을 유효한 0과 구분하기 위한 검증용 값이다.
    public int x = -1;
    public int y = -1;
    public string north, east, south, west;
}

[Serializable]
public sealed class PuzzleRegionData
{
    public string id, displayName, specialtyTag;
    public int mapLabelPriority = -1;
    public string[] productionTags = Array.Empty<string>();
    public int pricePercent = -1;
    public bool tradeHub;
}

[Serializable]
public sealed class PuzzleCityData
{
    public string id, displayName, regionId;
    public int mapLabelImportance = -1;
    // 대표 생산품의 태그. 넓은 분류(Food)가 아닌 Grain/Fish처럼 생산지를 구분하는 키다.
    public string specialtyTag;
    public bool buyEnabled, sellEnabled;
    // 태그별 규칙이 없을 때 이 도시의 모든 아이템에 적용하는 명시적 기본 비율이다.
    public int buyPercentOfBaseValue = -1, sellPercentOfFinalValue = -1;
    public PuzzleCityTagPriceData[] tagPrices = Array.Empty<PuzzleCityTagPriceData>();
    public PuzzleBoardData market;
    public string generalStockId, regionalStockId;
}

[Serializable]
public sealed class PuzzleMarketStockData
{
    public string id;
    public string[] pieceIds = Array.Empty<string>();
    public PuzzleMarketStockSlotData[] slots = Array.Empty<PuzzleMarketStockSlotData>();
}

[Serializable]
public sealed class PuzzleMarketStockSlotData
{
    public string id;
    public int x = -1, y = -1;
    public int width, height;
}

[Serializable]
public sealed class PuzzleBoardData
{
    public int width, height;
    public PuzzleStartingPlacementData[] startingPlacements = Array.Empty<PuzzleStartingPlacementData>();
}

[Serializable]
public sealed class PuzzleStartingPlacementData
{
    public string id, pieceId;
    // 누락을 검증에서 거부하기 위한 값이다.
    public int x = -1;
    public int y = -1;
    public int clockwiseTurns = -1;
}

internal enum PuzzleSynergyRange
{
    Adjacent,
    Board
}

[Serializable]
public sealed class PuzzleSynergyRuleData
{
    public string id, targetTag, excludedTargetTag, neighborTag, excludedNeighborTag, scope;
    public string spatialRange;
    public int multiplierPercent;
    // 주체 태그(targetTag) 아이템의 팝업에 표시할 시너지 설명.
    public string description;
    // description을 팝업에 표시할 태그. 비우면 주체 조건(targetTag)을 따른다.
    public string descriptionTag;
}

[Serializable]
public sealed class PuzzleFreshnessData
{
    public int settlementIntervalHours;
    public string refrigerationTag;
    public int refrigerationReductionPercent = -1;
    public int wagonRefrigerationReductionPercent = -1;
    public int saltPreservationReductionPercent = -1;
}

[Serializable]
public sealed class PuzzleAnimalSynergyData
{
    public string animalTag, wasteTag, wastePieceId, wasteDescription;
    public int wasteIntervalDays, wasteCountPerAnimal, breedingHours;
    // 출산·배설이 끝나기 직전 산출물을 놓을 자리가 없을 때 대기하는 시간. 동물 규칙 전체가 공유하는 값이다.
    public int completionHoldHours;
    public PuzzleAnimalBreedingData[] breeding = Array.Empty<PuzzleAnimalBreedingData>();
}

[Serializable]
public sealed class PuzzleAnimalBreedingData
{
    public string parentPieceId, offspringPieceId, description;
    public int offspringCount;
}

[Serializable]
public sealed class PuzzleCityTagPriceData
{
    public string tag;
    // 도시 안에서 고유해야 하며, 여러 태그가 맞으면 가장 큰 값의 규칙 하나를 쓴다.
    public int priority = -1;
    public int buyPercent = -1, sellPercent = -1;
}

// 마을 역에서 추가 동작으로 진입하는 행동의 종류. 테이블의 type 문자열과 이름이 같아야 한다.
public enum PuzzleCityActionType
{
    Butchery,
    Modification,
    Refining
}

// 도시별 추가 행동. cityIds에 지정한 마을의 역에서만 버튼이 보인다.
[Serializable]
public sealed class PuzzleCityActionData
{
    public string id, type, title, description, inputTag, outputTag;
    public string[] cityIds = Array.Empty<string>();
    public PuzzleBoardData board;
    // 확인 버튼을 누른 시점부터 완성에 걸리는 분. 도축·정제 등 시간 기반 행동이 쓰고 개조는 0이다. 누락이 0으로 위장되지 않도록 검증용 값으로 둔다.
    public long workMinutes = -1;
    // 도축·정제 확인 1회당 차감하는 비용. 개조는 모드별 cost를 쓰므로 이 값을 쓰지 않는다. 누락과 무료(0)를 구별하기 위한 검증용 값.
    public long cost = -1;
    public PuzzleActionConversionData[] conversions = Array.Empty<PuzzleActionConversionData>();
    // 개조(Modification) 전용 모드 목록. 도축은 모드 대신 행동 자체의 inputTag·conversions을 쓴다.
    public PuzzleActionModeData[] modes = Array.Empty<PuzzleActionModeData>();
}

// 행동의 입력 아이템 하나를 산출물로 바꾸는 대응. inputPieceId 기준 고유하다.
// 도축·개조는 outputCount를 쓰고 정제는 minOutputCount~maxOutputCount 무작위 범위를 쓴다.
[Serializable]
public sealed class PuzzleActionConversionData
{
    public string inputPieceId, outputPieceId;
    public int outputCount = -1;
    // 정제 전용. 입력 하나의 산출 개수 하한·상한이며 범위의 양 끝을 포함한다. 누락과 0을 구별하기 위한 검증용 값.
    public int minOutputCount = -1, maxOutputCount = -1;
}

// 개조 행동의 선택 모드. 입력 태그의 화물을 결과 태그가 붙은 품목으로 즉시 바꾼다.
// 정의 교체 방식이므로 변환은 1:1이며 모양·칸 수·기본 가치는 입력과 결과가 같다.
[Serializable]
public sealed class PuzzleActionModeData
{
    public string id, displayName, description, inputTag, outputTag;
    // 개조 확인 1회당 차감하는 비용. 개조 행동 자체의 cost 대신 모드별 값이 원본이다. 누락과 무료(0)를 구별하기 위한 검증용 값.
    public long cost = -1;
    public PuzzleActionConversionData[] conversions = Array.Empty<PuzzleActionConversionData>();
}

// 한 방향 노선. fromCityId에서 toCityId로 떠날 때의 비용이고 반대 방향은 별개 행이다.
[Serializable]
public sealed class PuzzleRouteData
{
    public string fromCityId, toCityId;
    public int distance = -1;
    // 고정 지도 좌표의 폴라인. x,y 쌍이며 화면 줌·요금용 distance와 무관하다.
    public float[] points = Array.Empty<float>();
    public bool highSpeed;
    // 누락된 값이 0이라는 정상값으로 위장되지 않도록 하는 검증용 값이다.
    public long cost = -1;
}
