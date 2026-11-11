# 테이블 계약

데이터 원본은 `Assets/Resources/Tables/<데이터 클래스명>.json`이다. 게임 규칙은 [게임 기획서](<../게임 기획서.md>), 기획 미정 사항은 기획서 10장, 개발 기반 제약은 [BACKLOG](BACKLOG.md)를 본다. 전체 실행 목록은 JSON에서 읽고, 기획서에는 검토에 필요한 지역·연결과 임시값을 요약한다. 문서의 표를 별도 실행 원본으로 사용하지 않는다.

## 구조와 편집

| 계층 | 위치 | 책임 |
|---|---|---|
| 원본 | `Assets/Resources/Tables/*.json` | 수치·ID·참조·설명 |
| DTO | `Runtime/Data/GameTables.cs`, `GameTableRecords.cs` | 직렬화 필드와 누락 판별값 |
| 로더·조회 | `Runtime/Data/TableManager.cs` | 공통 로딩·ID 인덱스·조회·검증 상태 |
| 규칙 | `Runtime/Data/GameTableRules.cs` | 항목별 오류 수집·참조 무결성·파생 계산 |

- 새 테이블은 JSON과 `[Serializable]` DTO를 만들고 `TableManager`의 공통 `Load<T>(ref loaded)` 경로 및 `GameTableRules` 검증에 등록한다. 개별 로더나 경로를 만들지 않는다.
- `status`·`note`는 운영 메모다. 숫자 필드의 `-1` 등 누락 판별값은 게임 기본값이 아니다. 밸런스 값을 코드·씬·UI에 중복 저장하지 않는다.
- 오류는 항목별로 모아 `IsValid`로 제공한다. 데이터를 사용하는 초기화는 `RequireValid()`로 차단한다. 모르는 필수 키는 기본값 대신 예외를 내고 선택 조회에만 `TryGet*`를 쓴다.
- 키는 순서 무관 ID다. 배열 순서를 이동 경로·우선순위로 사용하지 않고 노출 목록은 결정적으로 정렬한다.
- JSON 저장 시 `TableEditor`가 캐시를 무효화한다. 수동 갱신은 `Tools/MyJG/Tables/Reload Tables`이며 플레이 중에는 실행하지 않는다. 에디터는 디스크 원본을 우선 읽는다.

## 데이터 소유권

| 테이블 | 소유하는 데이터 |
|---|---|
| `BasicDataTableData` (기초데이터) | 공통 표시 문구·서식·색·UI 조정값·제작 치수·규칙 참조 ID·레이어 번호. [편집 계약](BASIC_DATA.md) |
| `PuzzleTableData` | 화물 모양·복수 태그·표기 키, 시작 기차, 지역·생산 태그·물가, 도시·시장·특산품·재고·도시 배율 |
| `PuzzleItemPriceTableData` | 화물 종류별 기본 가치 |
| `PuzzleSynergyTableData` | 인접 시너지 조건·범위·배율 |
| `PuzzleTradeTableData` | 시작 자금·공통 구매 배율 설정·증차 비용·클리어 비용·이동 연출 시간 |
| `PuzzleTimeTableData` | 고정 폴라인의 픽셀/시간 비율·시작 및 일출 시각·일출/야간 길이·휴식 분수 |
| `PuzzleRouteTableData` | 방향별 연결·고정 폴라인 좌표·기본 비용·거리·거리 및 적재 단가 |
| `PuzzleTurnTableData` | 비활성 상태인 빚 일정 |
| `FlowTableData` | 씬 이름·다음 씬·재시작 씬 |
| `GameplayTableData` | 템플릿의 player·sequence·upgrades 샘플 |

## 화물·보드·시장

- `pieces` 항목의 `id`, `displayName`, `description`, `tags`, `visualKey`, `slotCount`, `cells`가 정의 원본이다. 태그는 대소문자를 구분하고 한 아이템 안에서 중복되지 않아야 한다.
- `slotCount`는 `cells` 길이와 같고 슬롯은 상하좌우로 연결되어야 한다. 현재 화물의 네 방향 상태는 모두 `Flat`이다. 셀 배열 순서에는 의미가 없다.
- 화물 로컬 좌표는 외접 사각형 왼쪽 위에서 오른쪽·아래로 증가한다. 보드 좌표는 왼쪽 아래에서 오른쪽·위로 증가한다. 시작 배치의 `x`·`y`는 회전 후 외접 사각형의 왼쪽 위 기준점이다. 높이 2칸 화물은 y가 1 이상이어야 한다.
- `clockwiseTurns`는 시계 방향 90도 회전 횟수 0~3이다. 시작 배치 ID는 기차와 모든 시장에서 고유해야 하며 정의 참조·경계·점유 겹침을 검증한다.
- `train`과 각 도시의 `market`이 `width`·`height`·`startingPlacements`를 소유한다. `startCity`는 등록된 도시 ID를 참조한다. 도시에는 고유 ID·표시 이름·양의 기본 거래 배율·거래 허용 설정이 필요하다.
- `visualKey`는 `PuzzleInventoryUI.itemIcons`의 스프라이트 키다. 새 표시 키를 쓰면 프리팹 참조도 함께 추가한다. View는 초기화 시 모든 정의의 참조와 사전 배치 용량을 확인한다.
- 각 도시는 등록된 `regionId`를 참조한다. 지역은 고유 ID·표시 이름·양의 `pricePercent`, 대표 `specialtyTag`·`productionTags`를 가진다. `tradeHub` 지역은 생산 특산품 없이 집산품을 사용한다.
- 생산 도시의 `specialtyTag`는 대표 생산품이다. 고정 특산품이 이 태그를 가져야 하고 해당 태그의 구매·판매 배율이 모두 100 미만이어야 한다. 같은 대표 특산품의 직결을 허용하되 근거리 공급·수요 일치 연결은 금지한다. 일반 물품이나 공통 분류 태그의 중복을 금지하는 규칙은 아니다.
- `generalStock`와 `regionalStock` 각각의 `pieceIds`는 중복 없는 후보 목록, `slots`는 예약 영역이다. 두 그룹의 모든 후보가 공통 예약 영역에 들어가야 하고 영역은 고정 배치 및 두 그룹 전체에서 겹치면 안 된다. 생산 지역의 `regionalStock` 후보는 해당 지역 대표 특산품 태그를 가져야 한다. 각 후보는 모든 예약 영역에 회전 없이 들어가야 한다. 생성 시 슬롯마다 후보를 중복 허용해 추첨한다. `market.startingPlacements`에는 고정 특산품을 둔다.
- 정의는 공유하고 실제 화물 인스턴스는 씬 로컬 세션이 소유한다. 보드 간 이동 시 인스턴스 ID와 실제 가치를 보존하며 시장 재생성 때는 새 ID를 발급한다.

## 가치·거래

- `PuzzleItemPriceTableData.prices`의 `pieceId`는 등록된 화물을 참조하고 `baseValue`는 0 이상이다. 모든 화물에 가격이 정확히 하나씩 있어야 한다. 누락·중복·미등록 참조·음수 가격은 오류다.
- 도시의 `tagPrices`는 등록된 `tag`, 고유 `priority`, 양의 `buyPercent`·`sellPercent`를 가진다. 일치 태그 중 최고 우선순위 하나를 사용하고 없으면 `buyPercentOfBaseValue`·`sellPercentOfFinalValue`를 쓴다.
- 현재 거래가는 구매 시 기본 가치, 판매 시 인스턴스의 `ActualValue`에 선택된 도시 배율을 곱한다. 도시 배율과 지역의 `pricePercent`를 중간 반올림 없이 곱한 최종 금액을 `MidpointRounding.AwayFromZero`로 정수화한다. 범위 초과는 거래 실패로 처리한다. 미리보기와 확정 모두 `EvaluateDrop` 경로를 쓰고 확정 직전에 다시 평가한다.
- `startingFunds`는 0 이상, `purchaseMarkupPercent`, `wagonAddCostBase`, `gameClearCost`는 양수다. **공통 구매 배율은 저장·검증만 하며 현재 가격 계산에는 사용되지 않는다.** 설정 존재를 구현 완료로 해석하지 않는다.
- 시너지 규칙은 고유 `id`, 등록된 `neighborTag`, `scope`(`Train`, `Market`, `Both`), 양의 `multiplierPercent`, 비어 있지 않은 `description`과 대상 조건을 가진다. `targetTag` 또는 `excludedTargetTag` 중 하나로 대상을 정한다. `description`은 아이템 팝업에 표시할 시너지 설명이며, `descriptionTag`를 지정하면 그 태그를 가진 아이템의 팝업에만 표시한다(미지정 시 주체 조건을 따른다).
- 같은 이웃과 여러 면이 접해도 한 번 평가한다. 규칙 ID 순서로 기본 가치 기준 증가분을 합산한 뒤 감소 배율을 곱한다. 인접 정보 갱신과 실제 가치 누적을 분리하며 가치의 누적 경계 정책은 게임 기획서 10장을 따른다.

## 노선·턴

- `routes`는 `(fromCityId, toCityId)`를 키로 하는 방향 그래프다. 자기 자신·중복 방향·미등록 도시 참조를 금지한다. `cost`는 0 이상, `distance`는 양수다. 현재 표는 레일 생성 seed 20265146의 `projectMapData.rails`가 마을 ID로 준 양방향 연결 43개(마을 30)이며 시작 도시에서 모든 도시에 도달할 수 있어야 한다. 같은 이름이 다른 `city.*`에 붙을 수 있어 표시 이름과 ID는 따로 관리한다.
- `TableManager.TryGetRoute`·`HasRoute`는 개별 연결을 조회하고 `RoutesFrom(cityId)`는 도착 ID 오름차순 목록을 제공한다. 노출 정렬은 연결 관계가 아니다. 동일 방향의 복수 간선은 지원하지 않는다.
- 반대 방향이 있으면 거리와 `highSpeed`가 같아야 한다. 방향별 기본요금은 달라도 된다. 같은 지역의 근거리에서는 보장 재고의 복수 태그와 거래 우선순위를 사용해 어느 방향으로든 공급·수요가 맞물리는 연결을 금지한다.
- `distance`는 비용용 양수 게임 거리다. 시간은 `points`의 고정 폴라인 길이 ÷ `PuzzleTimeTableData.pixelsPerHour`를 분 단위로 올림한다. `points`는 유한한 x,y 쌍이 두 점 이상이며 왕복은 역순이다. 시간 가중치와 기본값 폴백은 제거했다. K=28은 잠정치다.
- 경과 시간은 `PuzzleInventoryService.Time` 하나가 소유한다. `ElapsedTime`은 `Time.ElapsedMinutes` 조회 별칭이다. 이동당 `AdvanceMinutes`를 한 번 호출하며 보유 시간을 먼저 갱신한 후 이벤트를 발행한다. 일반 휴식은 `PuzzleTimeTableData.restMinutes`, 아침 대기는 다음 일출까지의 분수를 사용한다. 휴식은 이동 시너지 가치·요금·이동 횟수·재입고를 변경하지 않는다.
- `planningTargetCityId`는 등록된 도시 ID를 가리키는 선택적 지도 표식이다. 현재 `city.capital.finance`(금융마을)이며 실제 상환 장소 설정인 `PuzzleTradeTableData.debtCityId`와 분리한다. `debtCityId`도 같은 도시를 가리켜 금융마을에서만 상환할 수 있다.
- `PuzzleTradeTableData.travelEffectSeconds`·`travelArrivalHoldSeconds`는 UI 이동 연출·도착 표시의 실제 초 단위 시간이며 유한한 양수여야 한다. 게임 내 소요 시간·기한과 분리한다.
- 비용은 `cost + distance × costPerDistance + TotalTrainOccupiedCells × costPerCell`이다. 화물 가치 변화·보유 시간·견적·표시는 같은 소요 분을 사용하며, 가치 배수는 소요 분/60이다.
- `highSpeed`는 이전 철도망 호환 필드이며 현 초안은 모두 false다. 노선도는 지역별 색·일반 길 회색·역 표식과 시간·일수 숫자를 사용한다.
- 이동은 `EvaluateTravel(toCityId)`·`Travel(toCityId)`로 평가·확정한다. 자금 부족·연결 없음·종료 상태 등 실패 시 이동 상태를 보존한다.
- 시장·노드·연결선은 프리팹에 사전 배치하므로 도시·노선을 수정하면 관련 프리팹·씬 참조도 함께 맞춘다. 표시·조작 계약은 게임 기획서를 따른다.
- `PuzzleTurnTableData`의 `debtInterval`·각 `moveCount`·`amount`는 양수다. 일정은 중복 없이 주기마다 존재해야 하며 `DebtSchedule`은 이동 횟수 순으로 제공한다. 옛 이동횟수 기반 빚 실행 코드는 비활성이다. 최신 시간 기한·조기 상환은 거래 테이블의 deadlineHours·debtCityId·gameClearCost를 사용한다. 현재 deadlineHours는 336이고 `debtCityId`는 `city.capital.finance`로 상환 장소가 제한된다.

## 씬·분석 설정

- `FlowTableData.scenes`의 씬 이름이 진행 경로 키다. 현재 표는 Preload·Title·Game이지만 빌드 활성 씬은 Game 하나이므로 검증에서 불일치를 보고한다. 이를 문서상 완료로 표기하지 않는다.
- `Assets/Resources/LogData.json`은 분석 운영 설정이며 도메인 테이블이 아니다. `TelemetryLog`가 직접 읽고 `TableManager`에 등록하지 않는다. 기록·전송 계약은 [TELEMETRY](TELEMETRY.md), 이벤트 필드는 [PLAY_LOG](PLAY_LOG.md)를 따른다.
