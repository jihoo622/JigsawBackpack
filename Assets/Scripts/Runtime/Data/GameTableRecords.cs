using System;

// 순수 직렬화 항목. 수치는 JSON에 두고 이 파일에는 필드만 둔다.
// 검증 규칙이나 조회 로직을 여기 넣지 않는다(GameTableRules / TableManager).

[Serializable] public sealed class PlayerData
{
    public float maxHealth;
    public float moveSpeed;
    public int dashCharges;
    public float dashCooldown;
    public float interactRange;
}

// 해금(확장) 효과 한 항목. id로 찾고 nextUpgrade로 다음 해금을 잇는다.
// category는 Presentation이 색·제목의 기준으로 읽는 키이며 EffectCategory 이름과 일치해야 한다.
[Serializable] public sealed class UpgradeData
{
    public string id, nextUpgrade, displayName, description, category;
}

// 해금 사슬의 시작. 배열 순서에 의존하지 않기 위한 명시적 시작 키.
[Serializable] public sealed class UpgradeSequenceData
{
    public string firstUpgrade;
}

// 씬 진행 경로 한 행. 씬 이름이 키이며 배열 순서는 의미하지 않는다.
// nextScene이 비어 있으면 최종 씬, restartScene은 사망·재시작 때 로드하는 씬.
[Serializable] public sealed class SceneLinkData
{
    public string scene, nextScene, restartScene;
}

[Serializable]
public sealed class PuzzlePieceData
{
    public string id, displayName, description, visualKey;
    public int baseValue = -1;
    public string[] tags = Array.Empty<string>();
    // 누락된 슬롯 수를 검증에서 거부하기 위한 값이다.
    public int slotCount = -1;
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
public sealed class PuzzleCityData
{
    public string id, displayName;
    public PuzzleBoardData market;
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
    public string id, targetTag, excludedTargetTag, neighborTag, scope;
    public int multiplierPercent;
}

[Serializable]
public sealed class PuzzleCityPriceData
{
    public string cityId;
    public bool buyEnabled, sellEnabled;
    public int buyPercentOfBaseValue, sellPercentOfFinalValue;
    // 이전 거래 테이블의 판매 비율을 읽기 위한 호환 필드다.
    public int sellPercentOfBaseValue;
}

[Serializable]
public sealed class PuzzleDebtPaymentData
{
    public int moveCount;
    public long amount;
}
