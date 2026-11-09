# 테이블 규칙

수치의 원본은 `Assets/Resources/Tables`에만 있다. 코드·씬·UI에 사본을 만들지 않는다.

## 4계층

| 계층 | 파일 | 책임 |
|---|---|---|
| 원본 | `Resources/Tables/<DTO명>.json` | 사람이 편집하는숫자값. 파일명은 데이터 클래스명과 일치 |
| DTO | `Runtime/Data/GameTables.cs`, `GameTableRecords.cs` | `[Serializable]` 필드만. 로직·기본값·계산 없음 |
| 로더·조회 | `Runtime/Data/TableManager.cs` | 지연 로드, ID 인덱스, `IsValid`, `RequireValid`, `Reload` |
| 규칙 | `Runtime/Data/GameTableRules.cs` | 항목별 검증, 참조 무결성, 파생 값 계산 |

## 추가 순서
1. JSON 파일을 만들고 `status`, `note`를 첫 필드로 둔다(운영 상태·메모).
2. `GameTables.cs`에 `[Serializable] public sealed class XxxTableData`를 정의하고, 배열 필드는 `Array.Empty<T>()`로 초기화한다.
3. `TableManager`에 프로퍼티와 `Xxx = Load<XxxTableData>(ref loaded);` 한 줄, 필요한 조회 메서드를 더한다.
4. `GameTableRules.Validate`에 목록 non-empty·키 참조·범위 검사를 더한다. 숫자 유한성과 누락 중첩 객체는 `Inspect`가 자동으로 커버한다.
5. 같은 값을 코드·씬에 복사하지 않는다. UI는 해당 테이블을 읽어 표시한다.

## 검증 원칙
- 오류는 항목별로 전부 모은다(첫 오류에서 멈추지 않는다). 메시지는 `테이블 오류 [경로]: 규칙` 형식.
- 잘못된 값으로 조용히 동작하지 않게, 데이터를 쓰는 초기화는 `TableManager.Instance.RequireValid()`로 차단한다.
- 모르는 키 조회는 기본값을 주지 않고 예외(`KeyNotFoundException`)로 막는다. 선택 조회만 `TryGet*`를 쓴다.
- 키는 id·씬 이름·자산 GUID처럼 순서 무관 값. 배열 순서에 의미를 두지 않고, 노출 목록은 결정적으로 정렬한다.

## 편집 도구 동작
- JSON 저장 시 `Editor/Tables/TableEditor.cs`가 `TableManager.Reload()`로 캐시를 무너뜨린다(추가 임포트·씬 수정 없음 → 도메인 리로드 안전).
- `Tools/MyJG/Tables/Reload Tables`는 편집 중 수동 갱신용. 플레이 중에는 실행 중 상태를 재구성하지 못해 막는다.
- 에디터에서는 `Resources.Load` 대신 디스크 원본을 우선 읽어, 저장 직후 값으로 검증한다.

## 현재 테이블
- `GameplayTableData` — 템플릿의 `player` 수치와 `sequence`/`upgrades` 샘플.
- `FlowTableData` — Preload → Title → Game 진행 경로(`scene`, `nextScene`, `restartScene`).
- `PuzzleTableData`는 공용 화물 모양(`pieces`), 기차 보드(`train`), 도시 목록(`cities`)을 정의한다. 각 도시에는 고유한 `id`, 화면 이름인 `displayName`, 시장 보드(`market`)가 필요하다. 1단계의 루트 `market`은 시작 도시 항목의 `market`으로 옮겼다. 각 보드의 크기와 시작 배치는 `width`·`height`와 `startingPlacements`에 둔다.
  - `startCity`는 도시 ID를 참조한다. 이동 순서는 시작 도시 우선, 나머지는 ID 오름차순이며 이전·다음 버튼은 이 순서를 양방향으로 순환한다.
  - `pieces`의 각 아이템 정의는 고유한 `id`, `displayName`, `description`, `tags`, `visualKey`, `slotCount`, `cells`를 가진다. 태그는 대소문자를 구분하며 한 아이템에서 중복될 수 없다. 현재 태그는 개발용 샘플이다.
  - `visualKey`는 `PuzzleInventoryUI` 프리팹의 `itemIcons` 목록에 있는 스프라이트 키다. View 초기화 시 모든 아이템 정의의 참조를 확인한다. 등록된 키를 사용해 정의와 시작 배치를 추가하면 코드 변경 없이 표시할 수 있다.
  - `slotCount`는 `cells` 길이와 일치하고 슬롯은 상하좌우로 연결되어야 한다. 화물 로컬 좌표는 외접 사각형의 왼쪽 위 기준으로 오른쪽과 아래로 증가한다. `(0,0)`은 비어 있어도 된다. `north`, `east`, `south`, `west`는 모두 `Flat`이다. 셀 배열 순서는 배치나 아이콘 기준에 영향을 주지 않는다.
  - 시작 배치에는 기차와 모든 시장에서 고유한 `id`와 화물 종류를 가리키는 `pieceId`가 필요하다. `clockwiseTurns`는 시계방향 90도 회전 횟수로 0~3이다. `x`·`y`는 회전 후 외접 사각형의 왼쪽 위 기준점이며, 보드 좌표는 왼쪽 아래에서 오른쪽과 위로 증가한다. 회전한 슬롯 전체가 해당 보드 안에 있어야 하며 같은 보드의 배치끼리는 같은 칸을 점유할 수 없다.
  - `TableManager`는 아이템 정의와 각 보드의 시작 배치를 ID 오름차순으로 제공한다. 씬 로컬 `PuzzleInventoryService`는 `RequireValid()` 이후 종류별 읽기 전용 정의를 만들고, 모든 보드에서 공통 순번으로 인스턴스 ID를 발급한다. 각 배치가 공유 정의와 개별 소속·좌표·회전을 가지며, **도시를 떠나면 그 도시의 시장은 `PuzzleInventoryService.CreateMarket`이 만들어 준 시작 배치 상태로 되돌아간다.** 되돌릴 때 새 인스턴스 ID를 발급해 과거 ID는 재사용하지 않고, 이미 기차로 옮긴 화물은 되돌아오지 않는다. 목적지 시장은 초기화하지 않는다.
- `PuzzleItemPriceTableData`는 아이템 기본 가격의 단일 원본이다. `prices`의 각 행은 `PuzzleTableData.pieces`의 `id`를 참조하는 `pieceId`와 0 이상의 정수 `baseValue`를 가진다. 모든 아이템에 가격이 정확히 하나씩 있어야 하며, ID·가격의 누락·중복·빈 값, 미등록 아이템 참조, 음수 금액은 테이블 오류다. 가격이 없는 아이템이나 정의 없는 가격 항목이 있으면 `RequireValid()` 초기화가 차단된다. `TableManager.ItemPrice`는 등록된 `pieceId`의 가격을 반환하고 모르는 ID에는 기본값을 주지 않고 예외를 낸다.
- `PuzzleSynergyTableData`는 인접 시너지 규칙을 정의한다. 규칙마다 고유 `id`, 등록된 `neighborTag`, `scope`(`Train`, `Market`, `Both`), 양의 정수 `multiplierPercent`를 둔다. 대상 조건은 해당 태그를 요구하는 `targetTag` 또는 해당 태그를 가진 화물만 제외하는 `excludedTargetTag` 중 하나를 지정한다.
  - 같은 보드에서 상하좌우로 맞닿은 두 인스턴스는 접촉 면이 여러 개여도 한 쌍으로 판정한다. 규칙은 대상 방향별로 한 번씩 적용하며, 대상과 이웃 태그가 양방향으로 일치하면 양쪽이 각각 보너스를 받는다.
  - 규칙은 ID 오름차순으로 평가한다. 100%를 넘는 배율의 초과분은 기본 가치 기준으로 합산한다. 120% 규칙 두 개가 적용되면 기본 가치의 140%가 된다. 그 뒤 100% 미만인 배율을 각각 곱한다. 80% 배율의 이웃 두 개는 증가 반영 가치의 64%를 남긴다. 상세 정보는 이웃별 증가와 감소를 표시한다.
  - 인접 관계는 배치·회전·거래 때 다시 계산하지만 인스턴스의 실제 가치를 즉시 바꾸지 않는다. 도시 이동 한 번이 확정될 때 기차 인스턴스마다 기존 계산 결과에서 기본 가치를 뺀 변화량을 실제 가치에 누적한다. 같은 이웃 인스턴스가 여러 면으로 닿아도 한 번만 계산하며, 실제 가치는 보드 이동과 도시 재방문에도 유지된다. 반올림·최솟값·상한은 아직 정하지 않았다.
- `PuzzleTradeTableData`는 0 이상의 정수 `startingFunds`만 정의한다. 도시별 거래 설정은 `PuzzleTableData.cities`의 각 도시 항목이 가진다.
- `PuzzleRouteTableData`는 도시 간 노선과 이동 비용의 원본이다. `routes`의 각 행은 `PuzzleTableData.cities`의 `id`를 참조하는 `fromCityId`·`toCityId`와 0 이상의 정수 `cost`를 가진다. 방향이 다르면 별개 노선이며 같은 방향의 중복 행, 자기 자신으로의 노선, 미등록 도시 참조, 음수 또는 누락된 비용은 테이블 오류다. 순환 이동(시작 도시 우선, ID 오름차순)에서 맞닿는 두 도시의 `from→to`와 `to→from`은 둘 다 있어야 하며 없으면 테이블 오류다. `TableManager.TryGetRoute`·`HasRoute`가 출발·도착 도시로 조회하고 모르는 키에는 기본값을 주지 않는다.
  - 이동 비용은 출발 직전에 차감한다. 현재 자금이 비용보다 적으면 이동이 차단되고 도시·턴·시장·기차 가치·자금이 모두 그대로 남는다(패배가 아니다). 빚은 도착 후에 차감하므로 같은 이동에서 비용이 먼저, 빚이 나중이다. 이전·다음 버튼 문구에는 `Presentation.PuzzleCityButton`이 붙인 목적지 노선 비용이 표시되고, 노선이 없거나 게임이 종료되면 비용 없이 이름만 표시한다. 처리 순서와 계약은 `Docs/노선 비용과 시장 초기화 구현 계획.md`.
- 각 도시의 `buyEnabled`·`sellEnabled`가 거래 가능 여부를 정한다. `buyPercentOfBaseValue`·`sellPercentOfFinalValue`는 태그 규칙이 없는 아이템에 적용하는 명시적 기본 비율이며 둘 다 양의 정수다.
  - `tagPrices`의 각 행은 `tag`, 고유한 `priority`, 양의 정수 `buyPercent`·`sellPercent`를 가진다. 태그는 아이템 정의의 태그를 대소문자까지 같게 참조하고 도시 안에서 중복될 수 없다. 같은 도시에서 `priority`도 중복될 수 없다.
  - 여러 태그 규칙이 맞으면 가장 큰 `priority` 하나를 적용하고, 맞는 규칙이 없으면 도시의 기본 비율을 적용한다. 배열 순서는 결과에 영향을 주지 않는다. `TableManager.TryGetCityTagPrice`가 도시·태그로 규칙을 조회하고 `CityPrice`가 도시 설정을 반환한다.
  - 구매가는 아이템 기본 가치에 구매 비율을, 판매가는 이동 직전 기차에서의 시너지 반영 최종 가치에 판매 비율을 적용한다. 두 가격 모두 가까운 정수로 반올림하고 정확히 절반이면 올림한다. 미리보기와 확정 거래는 같은 `EvaluateDrop` 가격 경로를 쓰며, 화면에는 적용된 태그·비율이 함께 표시된다. 화물 상세에도 현재 도시에서 선택된 태그와 구매·판매 비율을 표시한다.
  - 시장 보드 위에는 현재 도시의 `tagPrices`를 구매·판매 비율 기준으로 요약한다. 두 비율 모두 100 초과인 태그는 `귀함`, 둘 다 100 미만인 태그는 `흔함`으로 표시하고, 100을 사이에 두고 갈리거나 한쪽이 100이면 표시하지 않는다. 시장 보드의 종합 가치 텍스트는 쓰지 않는다.
  - 시장에서 기차로 옮기면 구매, 기차에서 현재 시장으로 옮기면 판매다. 같은 보드 안의 배치 변경은 무료다. 거래가 확정되면 씬 로컬 세션에서 소속과 자금을 함께 갱신한다.
- `PuzzleTurnTableData`는 양의 정수 `debtInterval`과 차감 일정 `debtPayments`를 정의한다. 각 행의 양의 정수 `moveCount`는 고유하며 `amount`는 양의 정수다. 마지막 차감 시점은 주기와 행 수의 곱이다. 첫 주기부터 마지막 차감까지 모든 시점이 한 번씩 있어야 하며, `TableManager.DebtSchedule`은 이동 횟수 오름차순으로 제공한다.
  - 씬 로컬 `PuzzleInventoryService`가 자금·이동 횟수·납부 횟수·진행 상태를 보유한다. 자금 시작값은 `PuzzleTradeTableData.startingFunds`에서 읽는다. 유효한 도시 이동 한 번이 턴 하나를 확정하고, 차감 시점마다 빚을 도착 후에 한 번 차감한다(노선 비용은 그보다 앞선 출발 직전에 차감한다). 차감 전 자금이 부족하면 도착·턴·가치 변화를 반영한 뒤 패배하며, 마지막 빚을 전액 갚으면 잔액이 0이어도 승리한다. 종료 후 이동·거래·회전은 차단된다.

## 테이블이 아닌 JSON
`Assets/Resources/LogData.json`은 분석 로그(JSONL)의 운영 설정(`enabled`·`bufferLines`·`retentionDays`)이다. 게임 규칙 수치가 아니라 기록 계통의 스위치라 따로 두며, `TableManager`가 아니라 `TelemetryLog`가 읽는다. 자세한 설명은 `Docs/TELEMETRY.md`.
