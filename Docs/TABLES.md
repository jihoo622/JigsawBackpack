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
  - `pieces`의 각 아이템 정의는 고유한 `id`, `displayName`, `description`, 0 이상의 정수 `baseValue`, `tags`, `visualKey`, `slotCount`, `cells`를 가진다. 태그는 대소문자를 구분하며 한 아이템에서 중복될 수 없다. 현재 기본 가치와 태그는 개발용 샘플이다.
  - `visualKey`는 `PuzzleInventoryUI` 프리팹의 `itemIcons` 목록에 있는 스프라이트 키다. View 초기화 시 모든 아이템 정의의 참조를 확인한다. 등록된 키를 사용해 정의와 시작 배치를 추가하면 코드 변경 없이 표시할 수 있다.
  - `slotCount`는 `cells` 길이와 일치하고 슬롯은 상하좌우로 연결되어야 한다. 화물 로컬 좌표는 외접 사각형의 왼쪽 위 기준으로 오른쪽과 아래로 증가한다. `(0,0)`은 비어 있어도 된다. `north`, `east`, `south`, `west`는 모두 `Flat`이다. 셀 배열 순서는 배치나 아이콘 기준에 영향을 주지 않는다.
  - 시작 배치에는 기차와 모든 시장에서 고유한 `id`와 화물 종류를 가리키는 `pieceId`가 필요하다. `clockwiseTurns`는 시계방향 90도 회전 횟수로 0~3이다. `x`·`y`는 회전 후 외접 사각형의 왼쪽 위 기준점이며, 보드 좌표는 왼쪽 아래에서 오른쪽과 위로 증가한다. 회전한 슬롯 전체가 해당 보드 안에 있어야 하며 같은 보드의 배치끼리는 같은 칸을 점유할 수 없다.
  - `TableManager`는 아이템 정의와 각 보드의 시작 배치를 ID 오름차순으로 제공한다. 씬 로컬 `PuzzleInventoryService`는 `RequireValid()` 이후 종류별 읽기 전용 정의를 만들고, 모든 보드에서 공통 순번으로 인스턴스 ID를 발급한다. 각 배치가 공유 정의와 개별 소속·좌표·회전을 가지며, 시장 배치 변경은 Game 씬의 세션이 유지되는 동안 보존한다.
- `PuzzleSynergyTableData`는 인접 시너지 규칙을 정의한다. 규칙마다 고유 `id`, 등록된 `neighborTag`, `scope`(`Train`, `Market`, `Both`), 양의 정수 `multiplierPercent`를 둔다. 대상 조건은 해당 태그를 요구하는 `targetTag` 또는 해당 태그를 가진 화물만 제외하는 `excludedTargetTag` 중 하나를 지정한다.
  - 같은 보드에서 상하좌우로 맞닿은 두 인스턴스는 접촉 면이 여러 개여도 한 쌍으로 판정한다. 규칙은 대상 방향별로 한 번씩 적용하며, 대상과 이웃 태그가 양방향으로 일치하면 양쪽이 각각 보너스를 받는다.
  - 규칙은 ID 오름차순으로 평가한다. 100%를 넘는 배율의 초과분은 기본 가치 기준으로 합산한다. 120% 규칙 두 개가 적용되면 기본 가치의 140%가 된다. 그 뒤 100% 미만인 배율을 각각 곱한다. 80% 배율의 이웃 두 개는 증가 반영 가치의 64%를 남긴다. 상세 정보는 이웃별 증가와 감소를 표시한다.
- `PuzzleTradeTableData`는 0 이상의 정수 `startingFunds`와 도시별 `cityPrices`를 정의한다. `cityId`는 `PuzzleTableData.cities`를 참조하며 도시마다 하나의 가격 항목이 필요하다.
  - `buyEnabled`·`sellEnabled`가 도시별 거래 가능 여부를 정한다. 구매가는 아이템 기본 가치에 `buyPercentOfBaseValue`를 적용한다. 판매가는 이동 직전 기차에서의 시너지 반영 최종 가치에 현재 도시의 `sellPercentOfFinalValue`를 적용한다. 두 가격 모두 가까운 정수로 반올림하고 정확히 절반이면 올림한다. 배율은 양의 정수다.
  - 이전 테이블에 `sellPercentOfFinalValue`가 없으면 `sellPercentOfBaseValue`에 적힌 기존 비율을 최종 가치에 적용한다.
  - 시장에서 기차로 옮기면 구매, 기차에서 현재 시장으로 옮기면 판매다. 같은 보드 안의 배치 변경은 무료다. 거래가 확정되면 씬 로컬 세션에서 소속과 자금을 함께 갱신한다.
- `PuzzleTurnTableData`는 양의 정수 `debtInterval`과 차감 일정 `debtPayments`를 정의한다. 각 행의 양의 정수 `moveCount`는 고유하며 `amount`는 양의 정수다. 마지막 차감 시점은 주기와 행 수의 곱이다. 첫 주기부터 마지막 차감까지 모든 시점이 한 번씩 있어야 하며, `TableManager.DebtSchedule`은 이동 횟수 오름차순으로 제공한다.
  - 씬 로컬 `PuzzleInventoryService`가 자금·이동 횟수·납부 횟수·진행 상태를 보유한다. 자금 시작값은 `PuzzleTradeTableData.startingFunds`에서 읽는다. 현재는 시작 상태와 다음 차감 조회를 제공하며, 도시 이동과 차감 확정은 턴 계획의 3단계에서 연결한다.

## 테이블이 아닌 JSON
`Assets/Resources/LogData.json`은 분석 로그(JSONL)의 운영 설정(`enabled`·`bufferLines`·`retentionDays`)이다. 게임 규칙 수치가 아니라 기록 계통의 스위치라 따로 두며, `TableManager`가 아니라 `TelemetryLog`가 읽는다. 자세한 설명은 `Docs/TELEMETRY.md`.
