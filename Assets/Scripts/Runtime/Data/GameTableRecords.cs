using System;

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
    public string[] tags = Array.Empty<string>();
    // 누락된 슬롯 수를 검증에서 거부하기 위한 값이다.
    public int slotCount = -1;
    public PuzzleCellData[] cells = Array.Empty<PuzzleCellData>();
}

// 아이템 기본 가격의 단일 원본. pieceId는 PuzzlePieceData.id를 참조한다.
[Serializable]
public sealed class PuzzleItemPriceData
{
    public string pieceId;
    // 누락된 금액이 유효한 0으로 위장되지 않도록 하는 검증용 값이다.
    public int baseValue = -1;
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
public sealed class PuzzleCityData
{
    public string id, displayName;
    // 대표 생산품의 태그. 넓은 분류(Food)가 아닌 Grain/Fish처럼 생산지를 구분하는 키다.
    public string specialtyTag;
    public bool buyEnabled, sellEnabled;
    // 태그별 규칙이 없을 때 이 도시의 모든 아이템에 적용하는 명시적 기본 비율이다.
    public int buyPercentOfBaseValue = -1, sellPercentOfFinalValue = -1;
    public PuzzleCityTagPriceData[] tagPrices = Array.Empty<PuzzleCityTagPriceData>();
    public PuzzleBoardData market;
    public PuzzleMarketStockData generalStock;
}

[Serializable]
public sealed class PuzzleMarketStockData
{
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

[Serializable]
public sealed class PuzzleSynergyRuleData
{
    public string id, targetTag, excludedTargetTag, neighborTag, excludedNeighborTag, scope;
    public int multiplierPercent;
}

[Serializable]
public sealed class PuzzleCityTagPriceData
{
    public string tag;
    // 도시 안에서 고유해야 하며, 여러 태그가 맞으면 가장 큰 값의 규칙 하나를 쓴다.
    public int priority = -1;
    public int buyPercent = -1, sellPercent = -1;
}

[Serializable]
public sealed class PuzzleDebtPaymentData
{
    public int moveCount;
    public long amount;
}

// 한 방향 노선. fromCityId에서 toCityId로 떠날 때의 비용이고 반대 방향은 별개 행이다.
[Serializable]
public sealed class PuzzleRouteData
{
    public string fromCityId, toCityId;
    public int distance = -1;
    // 누락된 값이 0이라는 정상값으로 위장되지 않도록 하는 검증용 값이다.
    public long cost = -1;
}
