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
- `TableManager.ValidateSourceIssues`는 같은 `GameTableRules` 검증의 `GameTableIssue` 경로·설명과 관련 품목·필드를 돌려준다. `ValidateSources`의 문자열 목록도 이 결과에서 만든다. Item Editor는 시작 원본과 후보를 ID 기준으로 대조해 기존/추가 오류를 구분하고 속성 이동에 연결하며, 기존 오류가 있어도 유효하지 않은 후보는 저장하지 않는다.
- 키는 순서 무관 ID다. 배열 순서를 이동 경로·우선순위로 사용하지 않고 노출 목록은 결정적으로 정렬한다.
- JSON 저장 시 `TableEditor`가 캐시를 무효화한다. 수동 갱신은 `Tools/MyJG/Tables/Reload Tables`이며 플레이 중에는 실행하지 않는다. 에디터는 디스크 원본을 우선 읽는다.

## 데이터 소유권

| 테이블 | 소유하는 데이터 |
|---|---|
| `BasicDataTableData` (기초데이터) | 공통 표시 문구·서식·색·UI 조정값·제작 치수·규칙 참조 ID·레이어 번호. [편집 계약](BASIC_DATA.md) |
| `PuzzleTableData` | 화물 모양·복수 태그·표기 키, 시작 기차, 지역·생산 태그·물가, 도시·시장·특산품·재고·도시 배율 |
| `PuzzleCityActionTableData` | 마을 역의 추가 행동·진입 도시·작업 보드·소요 분·입력/산출 태그·화물 변환 |
| `PuzzleSynergyTableData` | 시너지 조건·공간 범위·배율·적용 주기, 동물 배설·번식 설정 |
| `PuzzleTradeTableData` | 시작 자금·증차 비용·최대 객차 수(maxWagonCount)·클리어 비용·상환 조건·이동 연출 시간 |
| `PuzzleTimeTableData` | 고정 폴라인의 픽셀/시간 비율·시작 및 일출 시각·일출/야간 길이·휴식 분수 |
| `PuzzleRouteTableData` | 방향별 연결·고정 폴라인 좌표·기본 비용·거리·거리 및 적재 단가 |
| `FlowTableData` | 씬 이름·다음 씬·재시작 씬 |
| `GameplayTableData` | 템플릿의 player·sequence·upgrades 샘플 |

현재 도메인 JSON은 8개다. `GameplayTableData`는 UpgradeManager와 템플릿 SmokeCheck 참조가 있어 유지한다.

## 화물·보드·시장

- 품목 정의·Sprite·성장과 번식 행은 `Tools/MyJG/Item Editor`에서 초안으로 편집한다. 품목표와 번식표의 변경 파일을 함께 검토·백업·복구하며, 전체 표 검증·외부 변경·미저장 씬/프리팹 검사와 아이콘 반영을 거친다. 참조 현황은 원본 테이블 이름·행 ID·필드 경로를 표시한다. 신규 품목은 시장 제외 상태이며 기존 ID는 읽기 전용이다([사용법](ITEM_EDITOR.md)).
- 저장 후 캐시를 갱신해 유효성을 확인하고, 품목·번식 JSON 전체와 모든 관련 `PuzzleInventoryView`의 키·Sprite를 검토 초안에 대조한다. 사용하지 않는 기존 이미지 키도 View별로 보존한다. 대조 실패는 같은 파일·메타 복구 경로로 처리한다. 실제 실행 확인 항목은 사용 문서에 둔다.
- `pieces` 항목의 `id`, `displayName`, `description`, `tags`, `visualKey`, `baseValue`, `slotCount`, `cells`가 정의 원본이다. 태그는 대소문자를 구분하고 한 아이템 안에서 중복되지 않아야 한다.
- 새 태그의 표시 정의는 선택적인 `tagPresentations[]`의 `id`, 한글 `displayName`, 0~1 RGBA(`r/g/b/a`)에 둔다. 생략된 목록은 빈 목록으로 읽으며 기존 기본 태그는 `Presentation`의 기초데이터 매핑을 사용한다. 새 정의는 기본 태그와 이름이 겹치거나 대소문자만 다른 중복 ID·공백/쉼표 ID를 가질 수 없다. 에디터는 새 태그의 표시 정의를 요구하고 기존 데이터의 미등록 태그 표기는 보존한다.
- `durability`는 모든 품목에서 1~기초데이터 상한의 값이다. 신선도 표시는 기초데이터의 `rules.items.durabilityTag` 품목에 적용된다. `shelfLifeHours`는 0 이상의 시간이며 0은 시간에 따른 유통기한 소모 없음이다. 비적용 품목의 기존 값도 보존한다. `baseValue`는 음수를 허용하며 -1은 누락 판정값으로 거부한다.
- `excludeFromMarket: true` 품목은 `stockProfiles` 후보와 시장 고정 재고에 둘 수 없고 검증이 막는다. 다만 플레이어가 판매한 품목은 그 도시 시장이 다시 만들어지기(재방문) 전까지 시장 보드에 남아 다시 살 수 있다. 미출시 품목은 정의와 아이콘만 두고 이 플래그와 재고 후보에서 제외된 상태로 유지한다.
- `slotCount`는 `cells` 길이와 같고 슬롯은 상하좌우로 연결되어야 한다. 현재 화물의 네 방향 상태는 모두 `Flat`이다. 셀 배열 순서에는 의미가 없다.
- Item Editor의 격자 편집은 셀 수로 `slotCount`를 갱신하며 `GameTableRules.ValidatePieceCells`로 런타임과 같은 오류를 표시한다. 회전 미리보기는 저장하지 않는다. 이미지의 `iconWidth/Height`는 회전 전 칸 단위 크기이고 둘 다 양수일 때만 사용하며, 나머지는 원본 모양의 외접 사각형을 쓴다. `iconOffsetX/Y`는 그 중심에서 오른쪽·위 방향 오프셋이다. `PuzzleIconLayout`이 게임과 에디터의 중심·회전·크기 계산을 공유한다.
- 화물 로컬 좌표는 외접 사각형 왼쪽 위에서 오른쪽·아래로 증가한다. 보드 좌표는 왼쪽 아래에서 오른쪽·위로 증가한다. 시작 배치의 `x`·`y`는 회전 후 외접 사각형의 왼쪽 위 기준점이다. 높이 2칸 화물은 y가 1 이상이어야 한다.
- `clockwiseTurns`는 시계 방향 90도 회전 횟수 0~3이다. 시작 배치 ID는 기차와 모든 시장에서 고유해야 하며 정의 참조·경계·점유 겹침을 검증한다.
- `train`과 각 도시의 `market`이 `width`·`height`·`startingPlacements`를 소유한다. `startCity`는 등록된 도시 ID를 참조한다. 도시에는 고유 ID·표시 이름·양의 기본 거래 배율·거래 허용 설정이 필요하다.
- `visualKey`는 `PuzzleInventoryUI.itemIcons`의 스프라이트 키다. 새 표시 키를 쓰면 프리팹 참조도 함께 추가한다. View는 초기화 시 모든 정의의 참조와 사전 배치 용량을 확인한다.
- 각 도시는 등록된 `regionId`를 참조한다. 지역은 고유 ID·표시 이름·양의 `pricePercent`, 대표 `specialtyTag`·`productionTags`를 가진다. `tradeHub` 지역은 생산 특산품 없이 집산품을 사용한다.
- 생산 도시의 `specialtyTag`는 대표 생산품이다. 고정 특산품이 이 태그를 가져야 하고 해당 태그의 구매·판매 배율이 모두 100 미만이어야 한다. 같은 대표 특산품의 직결을 허용하되 근거리 공급·수요 일치 연결은 금지한다. 일반 물품이나 공통 분류 태그의 중복을 금지하는 규칙은 아니다.
- 도시의 `generalStockId`·`regionalStockId`는 `stockProfiles`의 고유 ID를 참조한다. 현재 일반 재고 1개·생산 지역 5개·집산 재고 1개로 7개 프로필을 공유한다. 일반·집산 후보에는 한 칸 사과도 포함한다. 프로필의 `pieceIds`는 중복 없는 후보, `slots`는 예약 영역이다. 후보는 모든 슬롯에 회전 없이 들어가야 하며 고정 배치·두 프로필의 영역이 겹치면 안 된다. 생산 지역 후보는 대표 특산품 태그를 가져야 한다. 슬롯 ID는 프로필 내부에서 고유하며 실제 배치 ID는 `도시 ID.슬롯 ID`다. 슬롯 순서를 결정적으로 정렬하고 중복 허용 추첨한다. 고정 특산품은 `market.startingPlacements`에 둔다.
- 정의는 공유하고 실제 화물 인스턴스는 씬 로컬 세션이 소유한다. 보드 간 이동 시 인스턴스 ID와 실제 가치를 보존하며 시장 재생성 때는 새 ID를 발급한다.

## 가치·거래

- 기본 가치는 `pieces[].baseValue`에 통합했다. 기존 음수 가치도 유지하며 누락 센티널 `-1`은 오류다. 별도 가격 표와 중복 참조는 두지 않는다.
- 도시의 `tagPrices`는 등록된 `tag`, 고유 `priority`, 양의 `buyPercent`·`sellPercent`를 가진다. 거래 배율은 `buyPercentOfBaseValue`·`sellPercentOfFinalValue`에 일치한 모든 태그 규칙의 (배율 − 100)을 더한 값이며 하한 없이 0 이하도 허용한다. 개별 규칙의 배율은 양수여야 한다. `priority`는 적용 태그 표기 순서다. 수도권 할증은 지역 `pricePercent`(현재 모두 100)가 아니라 도시 기본 배율로 표현한다.
- 거래 모델과 툴팁 견적은 `PuzzleInventoryService.TryQuoteTrade`를 공유하며 기획에 없는 고정 단가 예외는 두지 않는다.
- 현재 거래가는 구매·판매 모두 인스턴스의 신선도 반영 실제 가치(`FreshnessAdjustedValue` = `ActualValue` × 기초데이터 `rules.freshness.*` 구간 배율)에 선택된 도시 배율을 곱한다. 새 재고는 `ActualValue`가 기본 가치와 같다. 도시 배율과 지역의 `pricePercent`를 중간 반올림 없이 곱한 최종 금액을 `MidpointRounding.AwayFromZero`로 정수화한다. 범위 초과는 거래 실패로 처리한다. 미리보기와 확정 모두 `EvaluateDrop` 경로를 쓰고 확정 직전에 다시 평가한다.
- `startingFunds`는 0 이상, `wagonAddCostBase`·`gameClearCost`는 양수다. 가격 계산에 사용하지 않던 공통 구매 할증 필드는 제거했다. 현재 구매/판매 배율은 도시와 지역 표에서 읽는다.
- 시너지 규칙은 고유 `id`, 등록된 `neighborTag`, 보드 종류 `scope`(`Train`, `Market`, `Both`), 공간 범위 `spatialRange`(`Adjacent`, `Board`), 양의 `multiplierPercent`, 비어 있지 않은 `description`·`targetDescription`·`sourceDescription`과 대상 조건을 가진다. `targetTag` 또는 `excludedTargetTag` 중 하나로 대상을 정한다. `descriptionTag`는 이전 작성 데이터를 보존하는 메타데이터이며, 툴팁은 선택 역할에 맞는 설명을 읽는다. `Adjacent`는 상하좌우 접촉 쌍, `Board`는 같은 보드의 모든 화물 쌍을 평가한다. 인접 효과의 화살표는 접촉 칸에 표시하고, 보드 효과는 상세 수치에 표시한다.
- `pieces[].synergyEffects`는 `{ruleId,target,source}` 목록이다. 빈 배열은 가치 효과 없음이고 누락·null·중복 ID·없는 규칙·양쪽 모두 false·태그 조건과 맞지 않는 역할은 오류다. 계산은 대상의 `target`과 이웃의 `source`를 모두 요구하고 상대 태그·제외 조건·범위·면별 합산·배설물 면역도 유지한다. 가능 효과 설명과 접촉 화살표는 같은 선택 판정을 사용한다.
- `pieces[].preservationEffect`는 `{enabled,reductionPercent}`이며 모든 품목에 필수다. 감소율은 0~100%, 비활성은 0%이며 활성과 공통 냉장 태그가 일치해야 한다. 시간 진행·냉장 접촉·툴팁은 이 값만 읽는다. 기존 소금 10%·냉장 프레임 50%를 포함해 21개 품목을 명시적 값으로 이행했다.
- `ItemEditorEffectMigration`은 기존 태그 판정을 동결한 비교 경로로 역할별 적격 여부와 모든 품목 쌍/규칙의 변화율·품목별 신선 감소율을 대조한다. 이행은 초안에서만 수행하고 검토·Undo·기존 저장 트랜잭션을 거친다. 이미 명시한 선택을 덮어쓰거나 런타임에서 태그 폴백으로 복구하지 않는다.
- `animals`에 배설물 품목·태그, 배설 주기(일)·동물당 수량, 번식 시간(시간)과 `breeding`을 둔다. 번식 행은 같은 품목의 성체 쌍에 대해 `parentPieceId`, `offspringPieceId`, 양의 `offspringCount`를 지정한다. 검증은 `Baby` 태그가 없는 동물 품목마다 고유한 부모 행을 요구하고, `Baby` 태그의 부모 등록을 오류로 처리한다.
- Item Editor는 `animals.breeding`만 편집하고 번식·배설 공통 설정과 가치 규칙·배율은 읽기 전용으로 둔다. 마을 재고·도축·개조표는 참조만 표시한다. 새 DTO 필드는 고급 속성에 JSON 값으로 노출하고 원본의 알 수 없는 필드는 읽기 전용으로 보존한다.
- `TableManager.TryGetAnimalBreeding`은 부모 품목 ID로 조회한다. 현재 `piece.cow` 한 쌍은 `piece.babyCow` 한 마리를 낳는다. 번식 시간은 `animals.breedingHours`, 송아지의 성장 시간과 차단 구간은 `PuzzleTableData.pieces`의 `growthHours`·`growthBlockedHours`가 소유한다.
- `animals.completionHoldHours`는 번식·출산·배설이 끝나기 직전에 기다리는 공통 시간(시간)이다. 산출물을 놓을 자리가 없으면 진행도를 `주기 − completionHoldHours`에서 멈추고, 자리가 돌아오면 그 시간만큼 더 흘러야 완료한다. 검증은 양수이고 `breedingHours`·`wasteIntervalDays × 24`보다 짧아야 함을 요구한다. 성장은 품목의 `growthBlockedHours`를 같은 방식의 대기·재시도 구간으로 쓴다(진행 정지 지점 = `growthHours − growthBlockedHours`). 대기에 새로 들어간 개체·쌍에는 가치 변동 알림과 독립된 미확인 공간 부족 알림을 남긴다. 옛 `presentation.PuzzleGrowthBlocked` 문구는 이동 실패와 함께 제거했다.
- 동물 툴팁 원문은 `breeding[].description`(소요 시간, 자식 이름, 수량), `animals.wasteDescription`(주기 일수, 배설물 이름, 수량), `pieces[].growthDescription`(소요 시간, 성체 이름, 대기 시간)에 둔다. 괄호 안의 순서대로 `{0}`~`{2}`를 사용한다. 설명 누락과 잘못된 인자·품목 참조는 표 오류이며 시간 표기와 색은 `Presentation`이 처리한다. 설명 아래 진행도는 `presentation.tooltip.effectProgress`, 공간 부족 표시는 `presentation.tooltip.effectSpaceBlocked`가 원본이고 값은 표가 아니라 세션의 현재 상태에서 읽는다.
- `freshness`는 신선도 반영 주기(시간)·냉장 태그·객차 냉장 감소율의 공통 원본이다. `refrigerationReductionPercent`·`saltPreservationReductionPercent`는 이전 품목의 명시적 이행 입력으로만 남기며 런타임 품목 수치의 폴백이 아니다. 인접 발생원의 `preservationEffect.reductionPercent`를 인스턴스별로 한 번씩 합산하고 100%로 제한한다. 기존 객차 개조와의 최댓값 적용을 유지한다. 음식의 시작 유통기한은 품목의 `shelfLifeHours`이며, 남은 유통기한이 0이면 배설물 품목으로 교체한다.
- 인접 시너지는 맞닿은 면(경계)마다 한 번씩 평가하므로 같은 이웃과 여러 면이 닿으면 면 수만큼 더해진다. 냉장 프레임의 유통기한 감소는 프레임 인스턴스당 한 번이다. `excludedTargetTag`·`excludedNeighborTag`는 쉼표로 여러 태그를 지정할 수 있다(똥 패널티는 `Waste,Animal` 제외). 증가·감소를 구분하지 않고 모든 규칙의 (배율 − 100)을 합산해 기본 가치에 곱하며 하한 없이 음수 배율도 허용한다. 인접 정보 갱신과 실제 가치 누적을 분리하며 가치의 누적 경계 정책은 게임 기획서 10장을 따른다.

## 노선·턴

- `routes`는 `(fromCityId, toCityId)`를 키로 하는 방향 그래프다. 자기 자신·중복 방향·미등록 도시 참조를 금지한다. `cost`는 0 이상, `distance`는 양수다. 현재 표는 레일 생성 seed 20265146의 `projectMapData.rails` 연결에 프로젝트 보완 노선을 더한 양방향 연결 50개(마을 31)이며 시작 도시에서 모든 도시에 도달할 수 있어야 한다. 역 ID는 `city.<역 이름 로마자>`로 지역과 무관하다.
- `TableManager.TryGetRoute`·`HasRoute`는 개별 연결을 조회하고 `RoutesFrom(cityId)`는 도착 ID 오름차순 목록을 제공한다. 노출 정렬은 연결 관계가 아니다. 동일 방향의 복수 간선은 지원하지 않는다.
- 반대 방향이 있으면 거리와 `highSpeed`가 같아야 한다. 방향별 기본요금은 달라도 된다. 같은 지역의 근거리에서는 보장 재고의 복수 태그와 거래 우선순위를 사용해 어느 방향으로든 공급·수요가 맞물리는 연결을 금지한다.
- `distance`는 비용용 양수 게임 거리다. 시간은 `points`의 고정 폴라인 길이 ÷ `PuzzleTimeTableData.pixelsPerHour`를 분 단위로 올림한다. `points`는 유한한 x,y 쌍이 두 점 이상이며 왕복은 역순이다. 시간 가중치와 기본값 폴백은 제거했다. K=28은 잠정치다.
- 경과 시간은 `PuzzleInventoryService.Time` 하나가 소유한다. 이동·휴식·아침 대기는 배설·식품 만료·성장·출산 경계별로 보유 시간과 가치를 계산한 뒤 한 번 확정하고 `TimeAdvanced`를 발행한다. 진행 시뮬레이션은 객차 사본과 번식·배설 상태 사본에서 돌고 확정 시에만 본래 상태에 반영하므로 미리보기가 원본을 바꾸지 않는다. 배설은 동물 인스턴스별 진행 상태를 갖고 첫 발생을 이후 처음 맞는 달력 경계에 맞추며, 공간이 부족한 동물만 완료 직전에서 대기한다. 출산도 산출물 전체가 들어갈 때만 주기를 넘긴다. 성장 진행도는 `OwnedMinutes`와 분리한 성장 시계로 계산하고, 성체 자리가 없으면 그 품목의 성장 시계만 `growthHours − growthBlockedHours`에서 멈춘다. 공간 부족은 더 이상 이동·휴식의 실패 사유가 아니다. 같은 시각에는 정산·배설·식품 만료·성장·출산 순서로 처리한다.
- `planningTargetCityId`는 등록된 도시 ID를 가리키는 선택적 지도 표식이다. 현재 `city.finance`(금융마을)이며 실제 상환 장소 설정인 `PuzzleTradeTableData.debtCityId`와 분리한다. `debtCityId`도 같은 도시를 가리켜 금융마을에서만 상환할 수 있다.
- `PuzzleTradeTableData.maxWagonCount`는 최대 객차 수 상한(8칸)이며 1 이상의 정수여야 한다. `PuzzleInventoryService`가 상한 도달 시 증차를 거부하고, UI 객차 바 슬롯 수가 이 값보다 적으면 예외를 발생시킨다.
- `PuzzleTradeTableData.travelEffectSeconds`·`travelArrivalHoldSeconds`는 UI 이동 연출·도착 표시의 실제 초 단위 시간이며 유한한 양수여야 한다. 게임 내 소요 시간·기한과 분리한다.
- 비용은 `cost + distance × costPerDistance + TotalTrainOccupiedCells × costPerCell`이다. 보유 시간·견적·표시는 같은 소요 분을 사용한다. 가치 배수는 `floor(행동 후 경과 분 / 주기 분) − floor(행동 전 경과 분 / 주기 분)`이다. `PuzzleSynergyTableData.activationIntervalHours`는 양의 정수이며 현재 6이다. 현재 배율의 증감량을 정산 1회당 한 번 적용한다.
- `highSpeed`는 이전 철도망 호환 필드이며 현 초안은 모두 false다. 노선도는 지역별 색·일반 길 회색·역 표식과 시간·일수 숫자를 사용한다.
- 이동은 `EvaluateTravel(toCityId)`·`Travel(toCityId)`로 평가·확정한다. 자금 부족·연결 없음·종료 상태 등 실패 시 이동 상태를 보존한다.
- 시장·노드·연결선은 프리팹에 사전 배치하므로 도시·노선을 수정하면 관련 프리팹·씬 참조도 함께 맞춘다. 표시·조작 계약은 게임 기획서를 따른다.
- 옛 이동 횟수 기반 빚 일정 DTO·JSON·조회·비활성 코드는 제거했다. 상환은 거래 표의 `deadlineHours`·`debtCityId`·`gameClearCost`를 사용한다. 현재 기한은 336시간이며 금융마을에서 상환한다.

## 마을별 행동

- `actions` 행은 고유 `id`, 알려진 `type`(`Butchery`·`Modification`·`Refining`), 비어 있지 않은 `title`·`description`, `Butchery`·`Refining`의 양의 `workMinutes`와 `Modification`의 0, 빈 `board.startingPlacements`와 양수 `width`·`height`, 등록된 `inputTag`·`outputTag` 또는 `modes`를 가진다.
- `cityIds`는 등록된 도시 참조이며 같은 도시에서 같은 `type`의 행동은 하나만 쓸 수 있다. `TableManager.CityActionsOf(cityId)`는 action id 오름차순 목록을 주고 `TryGetCityAction(cityId, type, out row)`으로 하나를 조회한다. 전체 목록은 `CityActions` 속성(id 오름차순)이다. `modes`의 `id`·`displayName`·`description`·태그·`conversions`도 참조 무결성을 검증한다.
- `conversions`의 `inputPieceId`는 `inputTag`를, `outputPieceId`는 `outputTag`를 가진 등록된 화물이어야 하고 `inputPieceId`는 고유하다. 도축·개조는 `outputCount` 양수를 쓰고 정제는 `minOutputCount`(양수)·`maxOutputCount`(최솟값 이상)를 쓰며 두 표기를 섞지 않는다. 산출 품목을 정제 입력으로 등록하면 오류다. 현재 `action.butchery`는 `city.darpol`(다르폴)에서 `piece.cow` 1개를 `piece.beef` 2개로 360분간 작업하고, `action.modification`은 `city.erna`의 `mode.refrigeration`으로 `piece.uShape` 1개를 `piece.refrigeratedFrame` 1개로 즉시 바꾼다. `action.refining`은 `city.riobruk`(리오브루크)에서 `piece.oreCross`→`piece.metalIngot` 4~6개, `piece.oreL`→`piece.metalIngot` 2~4개를 360분간 작업하고 시간·비용은 미확정 제안값이다.
- 개조는 `InstanceId`, 회전 후 `Pose`, `ActualValue`, `OwnedMinutes`, `Durability`와 유통기한 진행을 보존한다. `BaseValue`와 로컬 점유 셀을 같게 유지해 변환으로 이후 시너지 기준액이 바뀌지 않게 한다. 냉장 프레임의 `Refrigerator` 태그는 `PuzzleSynergyTableData.freshness`에서 참조한다.
- 도축대·개조대·정제대 버튼·View는 Game 씬의 `Assets/Art/UI/GameplayLayout.prefab`에 연결되어 있으며 배치는 프리팹에서 수정한다. 문구 원본은 `BasicDataTableData`이며 상태·사유는 `rules.butchery.*`·`rules.modification.*`·`rules.refining.*`, 버튼·보드는 `presentation.StationButchery*`·`presentation.PuzzleButcheryTitle`·`presentation.StationModification*`·`presentation.PuzzleModification*`·`presentation.StationRefining*`·`presentation.PuzzleRefiningTitle`을 쓴다.

## 씬·분석 설정

- `FlowTableData.scenes`의 씬 이름이 진행 경로 키다. 현재 표는 Preload·Title·Game이지만 빌드 활성 씬은 Game 하나이므로 검증에서 불일치를 보고한다. 이를 문서상 완료로 표기하지 않는다.
- `Assets/Resources/LogData.json`은 분석 운영 설정이며 도메인 테이블이 아니다. `TelemetryLog`가 직접 읽고 `TableManager`에 등록하지 않는다. 기록·전송 계약은 [TELEMETRY](TELEMETRY.md), 이벤트 필드는 [PLAY_LOG](PLAY_LOG.md)를 따른다.
