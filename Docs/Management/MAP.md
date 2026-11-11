# 맵 데이터와 화면 구성

맵(교역 지도) 관련 사실의 위치를 한 곳에 모은 참조 문서다. 다른 세션이 기획서를 읽지 않아도 **어디서 무엇을 고쳐야 하는지** 알 수 있게 저장소 좌표만 적는다. 게임 규칙·의도는 [게임 기획서](<../게임 기획서.md>) 3장·7장, 테이블 일반 계약은 [TABLES](TABLES.md), 시간 구현 계약은 [게임 시간 시스템](<게임 시간 시스템.md>)이 원본이다.

## 1. 맵을 구성하는 저장소

맵은 **테이블 3종 + 지도 프리팹 + 지형 배경 이미지 + 코드 상수**로 구성한다.

| 저장소 | 경로 | 맵에서 담당하는 것 |
|---|---|---|
| 노드 테이블 | `Assets/Resources/Tables/PuzzleTableData.json` | 마을 30·지역 7·`startCity`·`planningTargetCityId`, 각 마을의 시장 격자와 재고 |
| 노선 테이블 | `Assets/Resources/Tables/PuzzleRouteTableData.json` | 방향 노선 86항목(= 왕복 43철도)의 `travelTimeWeight`·`distance`·`cost`·`highSpeed`, `costPerDistance`·`costPerCell` |
| 시간·판정 테이블 | `Assets/Resources/Tables/PuzzleTradeTableData.json` | `deadlineHours` 336(지도 경로 설계의 시간 총량)·`debtCityId` |
| 지도 프리팹 | `Assets/Art/PuzzleWorldMap.prefab`<br>guid `ab2441b70b1c4490988e4eb2ca06784a` | 화면 좌표·선분·라벨 등 모든 시각 요소, 그리고 Presenter/View/NodeView 컴포넌트 자체 |
| 코드 상수 | `Assets/Scripts/Runtime/Core/PuzzleTimeManager.cs` | `HOURS_PER_DAY = 24` · `HOURS_PER_TRAVEL_WEIGHT = 8` · `DEFAULT_DEADLINE_HOURS = 14 * 24` |

- 맵 UI 스크립트(PuzzleMapPresenter 1·PuzzleMapView 1·PuzzleMapNavigation 1·PuzzleMapTravelView 1·PuzzleMapNodeView 30)는 **전부 프리팹 안에 자기 완결로 들어 있다.** `Assets/Scenes/Game.unity`는 프리팹 인스턴스 하나를 사용한다.
- `Game.unity`의 인스턴스는 루트 RectTransform(fileID 700011)에 프로퍼티 오버라이드 11개(pivot·anchor·sizeDelta·localScale)를 저장한다. 노드·선분·라벨 좌표는 전부 프리팹에 있고 씬 오버라이드는 없다.
- 배경 이미지의 지리 원본은 `rail_map_geography_seed_20265146.json`이며, `Game.unity`는 지도 프리팹 인스턴스를 사용한다.
- 현재 맵은 생성 데이터 `ProjectWorldData.json`(seed 20265146)의 `projectMapData`를 값 그대로 옮긴 판이다. 표에 들어갈 값은 `targetTables`가 지정한 3개 테이블에 넣고, 표시 좌표와 노선 폴리라인은 [rail_map_layout_seed_20265146.json](rail_map_layout_seed_20265146.json)에 따로 저장한다. 원본 `ProjectWorldData.json`(1.5MB, 지형 배열 포함)은 저장소에 넣지 않고 위 파일만 보관한다. 지역 구분·태그 경제·시장 격자는 생성 데이터 대신 기존 테이블 값을 유지한다.
- 프리팹에도 노드 좌표·노선 선분·시간 라벨·환승 표시를 적용해 저장했다. `Tools/MyJG/Apply Rail Map Layout (seed 20265146)`으로 같은 레이아웃을 다시 적용할 수 있다(§4).

## 2. 노드 — `PuzzleTableData.json`

- `startCity` = `city.industry.ironworks`(에른아 공업역), `planningTargetCityId` = `city.capital.finance`(금융마을), `debtCityId` = `city.capital.finance`. 세 값 모두 생성 데이터가 준 값이다.
- `planningTargetCityId`는 **지도 표식(★)**이고 실제 상환 장소 판정은 `PuzzleTradeTableData.debtCityId`다. 지금은 금융마을이라 **그 도시에서 상환해야 클리어**된다(`PuzzleInventoryService.CanClearGame`).
- 마을은 전부 시장 7×6·고정 배치 3·지역 재고 슬롯 1·일반 재고 슬롯 2로 동일하다. 표의 배율은 `tagPrices`가 없을 때 쓰는 기본값(구매/판매)이다.

### 지역 7

| id | 표시 이름 | 대표 태그 | 생산 태그 | 물가 | `tradeHub` |
|---|---|---|---|---:|---|
| `region.mine` | 광산 지역 | Salt | Metal, Salt | 100% | — |
| `region.ranch` | 목축 지역 | Animal | Meat, Animal | 100% | — |
| `region.industry` | 공업 지역 | Machinery | Machinery | 100% | — |
| `region.capital` | 수도권 | — | — | 125% | O |
| `region.coast` | 해안 지역 | Fish | Fish, Water | 100% | — |
| `region.farm` | 농업 지역 | Fruit | Grain, Fruit, Wood | 100% | — |
| `region.common` | 일반 마을권 | — | — | 100% | O |

### 마을 30

`displayName`은 생성 원본의 역 이름이고 **이름과 ID는 따로 관리**한다. 이번 판에서 ID 대응이 바뀌어 같은 이름이 다른 `city.*`에 붙었다(라센턴은 `city.mine.iron`가 아니라 `city.mine.dig`). `tagPrices`가 6개인 26개 마을(생산 지역 21 + 수도권 5)은 태그 규칙을 쓰고, 중립 경유 마을 4개는 0개라 기본 배율 100/90만 쓴다. 지역 열은 `region.` 접두사를 생략했다.

| id | 이름 | 지역 | 특산 태그 | 배율 | `tagPrices` | 생성 역·`sourceId` | 타일 (x,y) | 차수 |
|---|---|---|---|---|---:|---|---|---:|
| `city.industry.assembly` | 포르베르 | industry | Machinery | 100/90 | 6 | 포르베르 `S10` | 22, 29 | 1 |
| `city.industry.engine` | 노르노바 | industry | Machinery | 100/90 | 6 | 노르노바 `S11` | 19, 62 | 2 |
| `city.industry.ironworks` | 에른아 공업역 | industry | Machinery | 100/90 | 6 | 에른아 공업역 `S01` | 13, 47 | 5 |
| `city.industry.logistics` | 헤일포드 | industry | Machinery | 100/90 | 6 | 헤일포드 `S12` | 4, 37 | 2 |
| `city.industry.meatlover` | 카르포트 | industry | Machinery | 100/90 | 6 | 카르포트 `S13` | 40, 45 | 2 |
| `city.mine.cave` | 카르카 | mine | Salt | 100/90 | 6 | 카르카 `S16` | 16, 5 | 1 |
| `city.mine.dig` | 라센턴 | mine | Metal | 100/90 | 6 | 라센턴 `S05` | 40, 14 | 4 |
| `city.mine.iron` | 리오브루크 | mine | Metal | 100/90 | 6 | 리오브루크 `S14` | 47, 5 | 1 |
| `city.mine.salt` | 다르안 | mine | Salt | 100/90 | 6 | 다르안 `S15` | 52, 34 | 2 |
| `city.coast.fish` | 테라티아 | coast | Fish | 100/90 | 6 | 테라티아 `S17` | 105, 19 | 2 |
| `city.coast.port` | 피오아 항 | coast | Fish | 100/90 | 6 | 피오아 항 `S03` | 101, 36 | 5 |
| `city.coast.sea` | 발렌리에 | coast | Water | 100/90 | 6 | 발렌리에 `S18` | 104, 63 | 1 |
| `city.coast.spring` | 테라카 | coast | Water | 100/90 | 6 | 테라카 `S19` | 88, 20 | 1 |
| `city.farm.field` | 다르트 | farm | Grain | 100/90 | 6 | 다르트 `S20` | 73, 77 | 1 |
| `city.farm.grain` | 이브트 | farm | Grain | 100/90 | 6 | 이브트 `S04` | 65, 60 | 6 |
| `city.farm.lumber` | 베르스카 | farm | Wood | 100/90 | 6 | 베르스카 `S21` | 50, 43 | 5 |
| `city.farm.orchard` | 테라폴 | farm | Fruit | 100/90 | 6 | 테라폴 `S08` | 54, 53 | 4 |
| `city.ranch.butcher` | 헤일하임 | ranch | Meat | 100/90 | 6 | 헤일하임 `S22` | 46, 49 | 5 |
| `city.ranch.highland` | 다르폴 | ranch | Animal | 100/90 | 6 | 다르폴 `S06` | 41, 68 | 4 |
| `city.ranch.jerky` | 오스그라드 | ranch | Meat | 100/90 | 6 | 오스그라드 `S23` | 49, 77 | 1 |
| `city.ranch.pass` | 라센폴 | ranch | Animal | 100/90 | 6 | 라센폴 `S24` | 28, 54 | 2 |
| `city.capital.central` | 아이포트 중앙 | capital | — | 100/90 | 6 | 아이포트 중앙 `S02` | 82, 43 | 4 |
| `city.capital.finance` | 금융마을 | capital | — | 100/90 | 6 | 금융마을 `S07` | 89, 52 | 3 |
| `city.capital.merchant` | 세르그라드 | capital | — | 100/90 | 6 | 세르그라드 `S25` | 65, 31 | 1 |
| `city.capital.outskirts` | 디아안 | capital | — | 100/90 | 6 | 디아안 `S26` | 92, 42 | 4 |
| `city.capital.wholesale` | 트렌포드 | capital | — | 100/90 | 6 | 트렌포드 `S09` | 77, 31 | 3 |
| `city.common.crossing` | 테라그라드 | common | — | 100/90 | 0 | 테라그라드 `S27` | 28, 41 | 2 |
| `city.common.rest` | 오스베르 | common | — | 100/90 | 0 | 오스베르 `S29` | 59, 42 | 4 |
| `city.common.roadside` | 메르트 | common | — | 100/90 | 0 | 메르트 `S30` | 74, 57 | 3 |
| `city.common.wayfarer` | 세르아 | common | — | 100/90 | 0 | 세르아 `S28` | 43, 57 | 5 |

## 3. 노선 — `PuzzleRouteTableData.json`

생성 원본의 철도 43개(방향 86항목)를 값 그대로 옮겼다. `projectMapData.rails`가 마을 ID로 직접 준 값이며 역→마을 추측은 쓰지 않는다. `travelTimeWeight`와 `distance`는 원본에서 이미 분리된 두 필드다(km 약 190당 1 / km 약 80당 1).

| 필드 | 원본 | 현재 값 |
|---|---|---|
| `travelTimeWeight` | `travelTimeWeight` | 1×39 · 2×3 · 4×1 |
| `distance` | `distance` | 1×2 · 2×21 · 3×12 · 4×4 · 5×1 · 6×2 · 9×1 (레일 기준 1×39 · 2×3 · 4×1) |
| `cost` | `railFareBase` | 전 항목 6 |
| `highSpeed` | 없음 | 전 항목 false. 시간 계산에 참여하지 않음 |
| `costPerDistance` / `costPerCell` | `ratePerDistanceUnit` / `ratePerOccupiedGridCell` | 1 / 1 |

- 소요 시간 = `가중치 × 8시간` → 8시간(39레일) · 16시간(3레일) · 32시간(1레일).
- 이동 요금 = `cost + distance × costPerDistance + 기차 점유 칸 수 × costPerCell`. 레일 평균 기본+거리 요금 8.84.
- 지역 내 22 · 지역 간 21. 막다른 역(차수 1) 8개, 차수 최대는 이브트 6.

### 전 레일 목록 (43)

표의 방향 86항목은 아래 43레일을 왕복 2항목으로 펼친 것이다. `sourceId` 숫자 순서가 프리팹의 `Track_<n>`·`Time_<n>` 번호(`railIndex` = 0~42)와 같다.

| 출발 | 도착 | 가중치 | 시간 | 거리 | 기본요금 | 구분 · `sourceId` · 원본 용도 |
|---|---|---:|---:|---:|---:|---|
| 에른아 공업역 `city.industry.ironworks` | 라센턴 `city.mine.dig` | 2 | 16h | 6 | 6 | 지역 간 · `R01` · 교역 간선 |
| 라센턴 `city.mine.dig` | 피오아 항 `city.coast.port` | 4 | 32h | 9 | 6 | 지역 간 · `R02` · 교역 간선 |
| 피오아 항 `city.coast.port` | 이브트 `city.farm.grain` | 2 | 16h | 6 | 6 | 지역 간 · `R03` · 교역 간선 |
| 이브트 `city.farm.grain` | 아이포트 중앙 `city.capital.central` | 1 | 8h | 4 | 6 | 지역 간 · `R04` · 교역 간선 |
| 아이포트 중앙 `city.capital.central` | 금융마을 `city.capital.finance` | 1 | 8h | 2 | 6 | 지역 내 · `R05` · 교역 간선 |
| 에른아 공업역 `city.industry.ironworks` | 테라그라드 `city.common.crossing` | 1 | 8h | 3 | 6 | 지역 간 · `R06` · 통과 간선 |
| 테라그라드 `city.common.crossing` | 세르아 `city.common.wayfarer` | 1 | 8h | 3 | 6 | 지역 내 · `R07` · 통과 간선 |
| 세르아 `city.common.wayfarer` | 오스베르 `city.common.rest` | 1 | 8h | 3 | 6 | 지역 내 · `R08` · 통과 간선 |
| 오스베르 `city.common.rest` | 메르트 `city.common.roadside` | 1 | 8h | 3 | 6 | 지역 내 · `R09` · 권장 교역 |
| 메르트 `city.common.roadside` | 금융마을 `city.capital.finance` | 1 | 8h | 3 | 6 | 지역 간 · `R10` · 권장 교역 |
| 다르폴 `city.ranch.highland` | 에른아 공업역 `city.industry.ironworks` | 2 | 16h | 5 | 6 | 지역 간 · `R11` · 권장 교역 |
| 다르폴 `city.ranch.highland` | 이브트 `city.farm.grain` | 1 | 8h | 4 | 6 | 지역 간 · `R12` · 권장 교역 |
| 아이포트 중앙 `city.capital.central` | 트렌포드 `city.capital.wholesale` | 1 | 8h | 2 | 6 | 지역 내 · `R13` · 최소신장 |
| 이브트 `city.farm.grain` | 테라폴 `city.farm.orchard` | 1 | 8h | 2 | 6 | 지역 내 · `R14` · 권장 교역 |
| 아이포트 중앙 `city.capital.central` | 디아안 `city.capital.outskirts` | 1 | 8h | 2 | 6 | 지역 내 · `R15` · 최소신장 |
| 테라폴 `city.farm.orchard` | 베르스카 `city.farm.lumber` | 1 | 8h | 2 | 6 | 지역 내 · `R16` · 최소신장 |
| 라센턴 `city.mine.dig` | 리오브루크 `city.mine.iron` | 1 | 8h | 2 | 6 | 지역 내 · `R17` · 최소신장 |
| 트렌포드 `city.capital.wholesale` | 세르그라드 `city.capital.merchant` | 1 | 8h | 2 | 6 | 지역 내 · `R18` · 최소신장 |
| 다르폴 `city.ranch.highland` | 오스그라드 `city.ranch.jerky` | 1 | 8h | 2 | 6 | 지역 내 · `R19` · 최소신장 |
| 에른아 공업역 `city.industry.ironworks` | 헤일포드 `city.industry.logistics` | 1 | 8h | 2 | 6 | 지역 내 · `R20` · 최소신장 |
| 피오아 항 `city.coast.port` | 테라티아 `city.coast.fish` | 1 | 8h | 3 | 6 | 지역 내 · `R21` · 최소신장 |
| 테라티아 `city.coast.fish` | 테라카 `city.coast.spring` | 1 | 8h | 3 | 6 | 지역 내 · `R22` · 최소신장 |
| 에른아 공업역 `city.industry.ironworks` | 노르노바 `city.industry.engine` | 1 | 8h | 3 | 6 | 지역 내 · `R23` · 최소신장 |
| 베르스카 `city.farm.lumber` | 헤일하임 `city.ranch.butcher` | 1 | 8h | 1 | 6 | 지역 간 · `R24` · 최소신장 |
| 카르포트 `city.industry.meatlover` | 헤일하임 `city.ranch.butcher` | 1 | 8h | 1 | 6 | 지역 간 · `R25` · 권장 교역 |
| 이브트 `city.farm.grain` | 다르트 `city.farm.field` | 1 | 8h | 3 | 6 | 지역 내 · `R26` · 최소신장 |
| 헤일하임 `city.ranch.butcher` | 라센폴 `city.ranch.pass` | 1 | 8h | 3 | 6 | 지역 내 · `R27` · 최소신장 |
| 다르안 `city.mine.salt` | 베르스카 `city.farm.lumber` | 1 | 8h | 2 | 6 | 지역 간 · `R28` · 권장 교역 |
| 포르베르 `city.industry.assembly` | 헤일포드 `city.industry.logistics` | 1 | 8h | 3 | 6 | 지역 내 · `R29` · 최소신장 |
| 피오아 항 `city.coast.port` | 발렌리에 `city.coast.sea` | 1 | 8h | 4 | 6 | 지역 내 · `R30` · 최소신장 |
| 라센턴 `city.mine.dig` | 카르카 `city.mine.cave` | 1 | 8h | 4 | 6 | 지역 내 · `R31` · 최소신장 |
| 테라폴 `city.farm.orchard` | 헤일하임 `city.ranch.butcher` | 1 | 8h | 2 | 6 | 지역 간 · `R32` · 권장 교역 |
| 트렌포드 `city.capital.wholesale` | 디아안 `city.capital.outskirts` | 1 | 8h | 3 | 6 | 지역 내 · `R33` · 허브 |
| 금융마을 `city.capital.finance` | 디아안 `city.capital.outskirts` | 1 | 8h | 2 | 6 | 지역 내 · `R34` · 경제 보조 |
| 카르포트 `city.industry.meatlover` | 베르스카 `city.farm.lumber` | 1 | 8h | 2 | 6 | 지역 간 · `R35` · 권장 교역 |
| 이브트 `city.farm.grain` | 메르트 `city.common.roadside` | 1 | 8h | 2 | 6 | 지역 간 · `R36` · 경제 보조 |
| 헤일하임 `city.ranch.butcher` | 세르아 `city.common.wayfarer` | 1 | 8h | 2 | 6 | 지역 간 · `R37` · 경제 보조 |
| 노르노바 `city.industry.engine` | 라센폴 `city.ranch.pass` | 1 | 8h | 2 | 6 | 지역 간 · `R38` · 경제 보조 |
| 베르스카 `city.farm.lumber` | 오스베르 `city.common.rest` | 1 | 8h | 2 | 6 | 지역 간 · `R39` · 경제 보조 |
| 피오아 항 `city.coast.port` | 디아안 `city.capital.outskirts` | 1 | 8h | 2 | 6 | 지역 간 · `R40` · 경제 보조 |
| 다르폴 `city.ranch.highland` | 세르아 `city.common.wayfarer` | 1 | 8h | 2 | 6 | 지역 간 · `R41` · 경제 보조 |
| 테라폴 `city.farm.orchard` | 세르아 `city.common.wayfarer` | 1 | 8h | 2 | 6 | 지역 간 · `R42` · 경제 보조 |
| 다르안 `city.mine.salt` | 오스베르 `city.common.rest` | 1 | 8h | 2 | 6 | 지역 간 · `R43` · 권장 교역 |

### 경로 참고 값 (재계산 결과)

- 에른아 공업역 → 금융마을 최단 = **5 가중치 = 40시간(1일 16시간)**. 기한 336시간 대비 8.4배 여유.
- 기한은 가중치 42 단위와 같다. 평균 레일 가중치 1.14이므로 이론상 약 37회 이동에 해당한다.
- 시작역에서 전 마을 도달 최대는 7 가중치(56시간, 포르베르).
- 상환 장소가 금융마을로 고정돼(`debtCityId`) 있어 공업 지역 출발점별 최소 소요가 그대로 플레이 하한이 된다.

| 공업 마을 | 금융마을까지 최단 |
|---|---:|
| 노르노바 | 48h |
| 에른아 공업역 | 40h |
| 카르포트 | 32h |
| 포르베르 | 56h |
| 헤일포드 | 48h |
- 원본 권장 동선은 11역 88시간(4일차 16:00 도착, 목표 구간 72~96시간): 에른아 공업역 → 다르폴 → 이브트 → 테라폴 → 헤일하임 → 카르포트 → 베르스카 → 다르안 → 오스베르 → 메르트 → 금융마을.
- 레일 차수는 이브트 6이 최대이고 차수 1이 8개다. 기존 맵은 차수 최소 2였다.

## 4. 프리팹 — `Assets/Art/PuzzleWorldMap.prefab`

`PuzzleMapView : PanelBase`(`startOpen: 0`)가 루트에 있고, 아래 배열은 **모두 Inspector 수동 바인딩**이다. 키는 cityId·regionId 문자열이라 배열 순서에 의미는 없지만, **누락이나 오탈자는 검증되지 않는다.**

| 배열 | 개수 | 내용 |
|---|---|---|
| `nodes` | 30 | `PuzzleMapNodeView` (`cityId` 30종과 1:1, 누락·여분 0건) |
| `routeTimes` | 43 | `fromCityId`/`toCityId` + `Text`. 레이아웃 도구가 레일 43개와 1:1로 다시 묶는다 |
| `regions` | 7 | `regionId` + 범례 라벨·스와치(Image) |
| `lines` | 8그룹 / 85세그먼트 | `regionId`별 노선 색 그룹. 키는 `region.*` 7개와 **특수 키 `line.regional`**(지역 간 회색선). 갱신 후 지역 간 42 · 수도권 11 · common/farm/industry/coast 각 6 · mine/ranch 각 4 |
| `routeLines` | 85 | 사용 중인 세그먼트의 평탄한 목록(Awake에서 색 미적용, 참조용) |

- 노드 30개의 `m_AnchoredPosition`은 프리팹 저장 값이며, 레이아웃 도구를 돌리면 생성 데이터의 `position`(MapPanel 1920×1080·중심 기준)으로 바뀐다. 범위는 x -728.02~550.77, y -365~355.

오브젝트 네이밍:

- `Village_<cityId>` 24개, `Station_<cityId>` 6개. Station은 `city.capital.finance`(금융마을)·`city.common.*` 4개·`city.industry.logistics`(헤일포드)로, 허브 성격을 그림으로 구분한 것이다. 이름은 바뀌어도 오브젝트 이름은 ID를 쓴다.
- `Track_<n>_<m>` = 레일 `n`(0~42)의 `m`번째 구간(1부터). 옛 맵은 43레일을 76구간으로 그렸고, 새 폴리라인은 85구간이다(42레일 × 2 + 1레일 × 1).
- 노드 하위에 `Label`·`Marker`·`StationRing`·`StationFace`·`CurrentHalo`·`CurrentBadge`가 각각 30개 있다. 지도 버튼들도 별도 `Label`을 사용한다.
- 계층: `PuzzleWorldMap`(루트·`PuzzleMapView` + Canvas) → `OpenMap` 버튼과 `MapPanel`. `PanelBase.root`는 `MapPanel`(fileID 700040)을 가리킨다.
- `MapPanel`의 고정 영역은 `Title`·`Guide`·`CloseMap`·`CityInformation`·`SubwayMapLabels`·`MapControls`이고, 지도 내용은 `MapViewport/MapContent` 아래에 있다.
- `SubwayMapTracks` 자식 100 = 활성 `Track_*` 85 + 이전 배치의 비활성 구간 15. `TimePlate_*` 43·`Time_*` 43은 지도 내용과 함께 확대한다. `SubwayMapLabels`에는 고정 범례인 `LineSwatch_*` 7·`LineName_*` 7만 남긴다.

### `PuzzleMapNodeView` 필드

| 필드 | 개수·분포 |
|---|---|
| `cityId` | 30 (테이블 도시 ID와 정확히 일치) |
| `interchange` | 도구 실행 후 true 15 · false 15 (차수 ≥3과 정확히 일치) |

색 분기만 쓰는 시각 플래그이고 테이블에는 없는 프리팹 전용 값이다. 현재 프리팹에도 생성 데이터의 환승 표시가 저장되어 있다.

### 지도 그림을 갱신하는 도구

배경·확대/축소·마진 구성:

- `Assets/Art/PuzzleMapTerrain.png`는 외부 지형의 육지·바다·고도·강·경제 지역 경계를 그린 배경이다. `rail_map_geography_seed_20265146.json`에 필요한 원본 배열을 보관하고, `Tools/MyJG/Generate Map Terrain Image`로 재생성한다. 국가 경계와 경제 지역 경계는 서로 다른 정보이며, 현재 배경에는 경제 지역 경계만 표시한다.
- `MapPresentationTool`은 기존 프리팹을 편집해 `MapViewport`(RectMask2D) 아래 `MapContent`에 배경·노드·선분·시간 라벨을 함께 배치한다. 배경 크기는 1570×820이며 타일 중심과 역 좌표를 일치시킨다.
- `PuzzleMapNavigation`은 복제한 `InputSystem_Actions`의 `Map/Zoom`·`Map/Point`를 사용한다. 휠은 포인터 위치 기준 35~250% 확대·축소, UI 드래그는 지도 이동이다. 가장자리를 제한하고, 전체 보기와 현재 위치 버튼을 연결한다. 패널을 닫으면 입력 구독을 해제한다.
- 지형과 역 좌표는 줌에 따라 변하지만 노드 전체·시간 라벨·시간 배경은 역배율로 보정해 화면상 크기를 유지한다. 노선 길이는 확대하고 굵기는 역배율로 보정한다. 이름은 전체 보기에서 18, 상세에서 22, 현재 역은 24 크기다. 시간은 18, 현재 위치 배지는 16 크기다. 시각 크기의 원본은 `Presentation`이다.
- 확대·축소·드래그·호버·도착 때 `PuzzleMapView.UpdateMapLayout`이 라벨을 재배치한다. 이름은 현재 역·가리킨 역·목표 역·직접 연결 역·환승역·나머지 역 순으로 처리하고, ID로 동률을 정렬한다. 마커를 피하는 방향을 선택하고 자리가 없으면 이름만 생략한다. 현재 역과 가리킨 역의 이름은 우선 보존한다. 시간은 85% 이상에서 남은 공간에 배치하고, 겹치는 시간 라벨은 숨긴다. 생략된 도시 정보·이동 시간은 역을 가리켜 하단 정보에서 확인한다.
- 제목·범례·도시 정보·버튼은 확대 대상 밖에 고정한다. 좌우 여백은 48px, 지도 영역은 위아래 228px를 비운다. 현재 역은 금색 링·배지·굵은 글씨로 강조한다.
- `Tools/MyJG/Apply Map Presentation`으로 UI 연결을 재적용한다. 배경 이미지를 처음 생성한 경우 에디터가 새 파일을 읽은 뒤 적용한다. 이 작업에서 플레이·입력 실험·화면 캡처는 수행하지 않았다.

`Tools/MyJG/Apply Rail Map Layout (seed 20265146)` — `Assets/Scripts/Editor/Build/RailMapLayoutTool.cs`. 레이아웃 파일 [rail_map_layout_seed_20265146.json](rail_map_layout_seed_20265146.json)을 읽어 **프리팹만** 고쳐 저장한다(씬·테이블은 건드리지 않는다).

| 작업 | 내용 |
|---|---|
| 노드 | `cityId`로 노드를 찾아 `anchoredPosition` = 레이아웃 `x`·`y`, `interchange` = 차수 ≥3(15개 true) |
| 노선 | 레일 폴리라인을 구간별로 나눠 `Track_<railIndex>_<leg>`의 위치·회전·길이를 배치. 부족한 구간은 기존 선분을 복제하고 남는 구간은 `SetActive(false)` |
| 굵기 | 교역·통과 간선 7px, 보조선 5px |
| 시간 라벨 | `routeTimes[i]`의 출발·도착을 레일 `i`로 다시 묶고 `label` 참조를 `Time_<railIndex>`의 Text로 고정, `Time_*`·`TimePlate_*`를 `labelPosition`으로 이동 |
| 노선 색 그룹 | `lines[].segments`를 레일의 `regionId`(지역을 넘으면 `line.regional`)로 재구성 |
| 보정 | 라벨이 노드와 60px 이내로 겹치면(18개) 노드에서 바깥쪽으로 34px 밀어 배치 |
| 경고 | 노드 좌표와 폴리라인 끝이 2px 이상 어긋나거나 레이아웃에 없는 `cityId`가 나오면 로그에 남긴다 |

- 이번 적용에서 선분 24개를 추가하고 이전 구간 15개를 비활성화해 활성 85구간을 저장했다. 검증 메뉴와 플레이는 실행하지 않았다.
- 노드 2개(`city.farm.field` 145.6,-365 / `city.ranch.jerky` -158.27,-365)는 y=-365로 하단 `CityInformation`(중심 y=-453·높이 140)과 근접한다. 겹치면 두 노드의 y만 위로 조정한다.
- `Track_`를 참조하는 코드는 여전히 0건이다. 도구가 프리팹에 그림을 심고, 실행 시점의 인접·시간·색은 전부 표 조회 결과다.


### 코드에서 읽는 경로

- `PuzzleMapView.Awake` — `regions` 라벨·색, `lines` 세그먼트 색, `routeTimes` 라벨 텍스트(`Presentation.PuzzleMapTime(PuzzleTimeManager.TravelHours(route.travelTimeWeight))`)를 채운다. `TryGetRoute`가 실패하면 **라벨을 갱신하지 않고 조용히 넘어간다.**
- `PuzzleMapPresenter` — `SceneServices.For<PuzzleInventoryService>`로 세션을 얻고, 노드 Hover/Click을 구독한다. Click → `EvaluateTravel` → 확인·취소 모달 → 확인 시 재판정 → 이동 연출 → `Travel` 1회 → 도착 표시 → 지도 Close 순서다. 실패는 지도 `information`에 표시한다.
- `PuzzleMapTravelView : PanelBase`는 `MapPanel/MapTravelPanel` 아래에 사전 배치한 확인 카드·이동 카드·기차·진행 막대를 제어한다. 기본 선택은 취소이며 `UI/Cancel`도 취소다. 이동 중에는 확인·취소·지도 조작을 막는다. 패널이 닫히거나 비활성화되면 Presenter의 대기 코루틴을 중단한다. 이동 확정 전 중단은 도시·시간·자금을 바꾸지 않는다.
- `Tools/MyJG/Apply Map Travel UI`(`MapTravelSetupTool`)로 모달·연출·Presenter 참조를 다시 저장한다. 연출 실제 초 단위 시간은 `PuzzleTradeTableData.travelEffectSeconds`·`travelArrivalHoldSeconds`, 문구·색은 `Presentation`이 원본이다. 실제 게임 시간·요금 계산은 기존 `PuzzleInventoryService.Travel`만 수행한다.
- 노드 상태는 `HasRoute(현재, target)` + `EvaluateTravel` + `session.HasVisitedCity` + `planningTargetCityId` 비교로 계산한다. **화면 좌표와 선분은 프리팹에 있고, 인접 관계·시간·비용·방문 여부는 전부 테이블과 세션 조회 결과다.** 프리팹에 인접 목록이 중복 저장되지는 않는다.
- `GameEvents.CityArrived`를 받아 도착 후 다시 갱신한다.
- 노선 라벨은 `PuzzleMapTime`이 24의 배수면 "N일"로 나머지는 "N시간"으로 표기한다. 현재 가중치는 8시간 · 16시간 · 32시간으로 표시된다.

## 5. 표시 규칙 — `Presentation.cs`

| 항목 | 값·위치 |
|---|---|
| 패널 이름 | `PUZZLE_MAP_PANEL_NAME = "PuzzleWorldMap"` |
| 지역 색 | `PuzzleMapLineColor(regionId)` → `PuzzleMapRegionColor`의 7색(알파 30 → 255), 미등록 ID는 `Color.clear` |
| 지역 간 선 | `regionId == "line.regional"`일 때 `PuzzleMapInterchange`(228,233,239). 프리팹 `lines`의 특수 키와 짝을 이룬다 |
| 상태 색 | 현재(255,199,77) · 방문(87,219,184) · 미탐색(102,117,140) · 이동가능(99,232,171) · 자금부족(217,115,115) |
| 안내문 | `PuzzleMapGuide`가 `"근거리 {HOURS_PER_TRAVEL_WEIGHT}시간 · 숫자: 소요 시간 · ★: 상환 예정지 · 경과 N일 M시간"` |
| 미래 가치 | `PuzzleTooltipFutureValueLabel`이 `"{HOURS_PER_TRAVEL_WEIGHT}시간 후 가치"` |

**`HOURS_PER_TRAVEL_WEIGHT`를 바꾸면 이 문자열 2곳이 자동으로 따라간다. 대신 "8시간"이라는 단어를 하드코딩한 기획서·문서와 어긋난다.**

## 6. 시간 변환과 코드 상수

| 상수 | 위치 | 값 |
|---|---|---|
| `HOURS_PER_DAY` | `PuzzleTimeManager` | 24 |
| `HOURS_PER_TRAVEL_WEIGHT` | `PuzzleTimeManager` | 8 (**테이블 아닌 코드 `const`**) |
| `DEFAULT_DEADLINE_HOURS` | `PuzzleTimeManager` | 336 (14일) |
| `deadlineHours` | `PuzzleTradeTableData.json` | 336 |

- 변환은 `PuzzleTimeManager.TravelHours(weight)` 단일 경로다. 가중치 1 미만은 예외, 결과는 `checked`다.
- 당일 시각은 0시·8시·16시 3칸만 지난다(전 가중치가 8의 배수). Day는 `ElapsedHours / 24`.
- 조회 API: `Time.ElapsedHours`·`Day`·`Hour`·`RemainingHours`·`HasDeadline`·`ArrivalAt(h)`·`CanArriveBeforeDeadline(h)`.
- 진행 API: `session.AdvanceTravelTime(weight)`(외부 이동 시스템용), `session.AdvanceHours(h)`(대기·작업용). `Travel(toCityId)`은 내부에서 한 번 진행하므로 추가 호출하면 중복이다.
- **가치 변동은 가중치 단위, 보유 시간은 실제 hour 단위다.** `wagon.ApplyTurnValueChange(route.travelTimeWeight)` vs `placement.ApplyElapsedHours(실제 시간)`. 기준 배율을 바꾸면 두 축이 어긋난다.
- 노선 조회: `TableManager.TryGetRoute` / `HasRoute` / `RoutesFrom(cityId)`(도착 ID 오름차순, 노출 정렬일 뿐 연결 관계 아님). 동일 방향 복수 간선은 미지원.

## 7. 검증이 잡는 것과 못 잡는 것

`GameTableRules.ValidateRoutes`·`ValidateCities`가 잡는 것:

- 미등록 도시 참조, 자기 자신 연결, 방향 중복, `cost < 0`, `distance <= 0`, `travelTimeWeight < 1`
- 반대 방향 항목의 `distance`·`highSpeed` 대칭
- 같은 지역 근거리 직결에서 보장 재고의 공급·수요가 어느 한 방향이라도 맞물리는 연결
- `startCity`에서 전 도시 도달 가능성 (BFS)

잡지 못하는 것:

- **`travelTimeWeight`의 왕복 대칭** — `distance`·`highSpeed`와 달리 검사문이 없다. 지금은 86/86 일치하지만 수동 리밸런싱 시 왕복 시간이 달라져도 조용하다.
- 프리팹 바인딩 누락·오탈자 (`nodes`의 cityId, `routeTimes`의 쌍, `lines`의 regionId). 레이아웃 도구는 잘못된 `cityId`·`regionId`를 로그로 남기지만, 도구를 실행하지 않은 상태는 아무도 잡지 못한다.
- `Track_<n>`과 노선의 시각적 대응. **코드는 `Track_`를 참조하지 않는다.** 레이아웃 도구를 돌려 `railIndex`(sourceId 숫자 순서) 기준으로 다시 그리기 전까지 그림과 표는 다른 세상에 산다.
- `highSpeed`는 필드와 왕복 대칭 검사만 있고 시간·요금 계산에 미사용이다(전 항목 false).
- 시간과 거리의 값 중복 여부. 지금 표는 두 값을 다르게 쓰지만(km 환산 기준이 다름), 검사는 값의 범위와 왕복 대칭만 본다.
- `Game.unity`의 루트 RectTransform scale 0 오버라이드 11개(§1).

## 8. 편집 체크리스트

| 변경 | 손댈 곳 |
|---|---|
| 마을 추가 | `PuzzleTableData.cities` + `regions` 참조 유지 + `PuzzleRouteTableData.routes`(왕복 2항목) + 레이아웃 JSON의 `nodes`·`rails` + 레이아웃 도구 실행(노드 오브젝트는 미리 있어야 함) |
| 노선 추가·삭제 | `routes` 왕복 2항목 + 레이아웃 JSON의 `rails` + `Tools/MyJG/Apply Rail Map Layout` 실행(선분·라벨·색 그룹 재생성) |
| 거리만 바꾸기 | `routes.distance` (요금에만 영향. 시간은 그대로) |
| 시간만 바꾸기 | `routes.travelTimeWeight` (시간·**가치 변동 배수**에 영향. 요금 그대로) |
| 기준 배율 8시간 변경 | `PuzzleTimeManager.HOURS_PER_TRAVEL_WEIGHT` + `PuzzleTradeTableData.deadlineHours`(난이도 연동) + 기획서 3장 표·6장 수식·7장 수치표. 8시간 문자열은 `Presentation`이 상수를 읽으므로 자동 |
| 기한 변경 | `PuzzleTradeTableData.deadlineHours`(시간 단위) + `PuzzleTimeManager.DEFAULT_DEADLINE_HOURS`는 생성자 기본값임을 기억 |
| 지도 색·표기 | `Presentation` 한 곳. 개별 View·프리팹에 색을 두지 않는다 |

- 테이블 JSON을 저장하면 `TableEditor`가 캐시를 무너뜨린다. 수동 갱신은 `Tools/MyJG/Tables/Reload Tables`(플레이 중 제외).
- 프리팹·씬을 건드린 뒤에는 `Tools/MyJG/Validate Project`로 컴파일 경고와 표-씬 참조를 확인한다.
- 맵 UI는 실행 시 프로시저럴 생성을 하지 않는다. 노드·선·라벨을 코드에서 만들지 않고, 에디터 도구가 프리팹에 결과를 저장하는 방식만 쓴다.

## 9. 미결

- `highSpeed`를 시간 단축으로 쓸지, 삭제할지. 지금은 죽은 필드다.
- 막다른 역(차수 1) 8개를 용인할지. 왕복 2회의 시간 비용이 드는 구조는 이번 판에서 처음이다.
- `HOURS_PER_TRAVEL_WEIGHT`를 코드 상수에서 테이블 필드로 옮길지. AGENTS의 "밸런스 수치는 테이블이 원본"을 현재는 지키지 않는다.
- `travelTimeWeight` 왕복 대칭 검증(§7)을 추가할지.
- 상환 장소를 금융마을로 고정한 값(`debtCityId`)을 유지할지. 도착 후 상환까지의 시간이 플레이 길이에 드러난다.
- `CityArrived`·`TimeAdvanced`는 분석 로그에 없다. `TelemetryRecorder`는 run·scene·upgrade·panel·trade 5개만 구독하고, 물건 보유 시간은 `Debug.Log`만 남긴다(`TelemetryEvents`에 이동·시간 사건 정의 없음).
