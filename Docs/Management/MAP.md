# 맵 데이터와 화면 구성

맵(교역 지도) 관련 사실의 위치를 한 곳에 모은 참조 문서다. 다른 세션이 기획서를 읽지 않아도 **어디서 무엇을 고쳐야 하는지** 알 수 있게 저장소 좌표만 적는다. 게임 규칙·의도는 [게임 기획서](<../게임 기획서.md>) 3장·7장, 테이블 일반 계약은 [TABLES](TABLES.md), 시간 구현 계약은 [게임 시간 시스템](<게임 시간 시스템.md>)이 원본이다.

## 1. 맵을 구성하는 저장소

맵은 **테이블 4종 + 지도 프리팹 + 지형 배경 이미지 + 시간 계산 코드**로 구성한다.

| 저장소 | 경로 | 맵에서 담당하는 것 |
|---|---|---|
| 노드 테이블 | `Assets/Resources/Tables/PuzzleTableData.json` | 마을 30·지역 7·`startCity`·`planningTargetCityId`, 각 마을의 시장 격자와 재고 |
| 노선 테이블 | `Assets/Resources/Tables/PuzzleRouteTableData.json` | 방향 노선 86항목의 고정 `points`·요금용 `distance`·`cost`·`highSpeed`, 거리·적재 단가 |
| 시간 테이블 | `Assets/Resources/Tables/PuzzleTimeTableData.json` | K·시작 시각·일출 시각·일출/야간 길이 |
| 시간·판정 테이블 | `Assets/Resources/Tables/PuzzleTradeTableData.json` | `deadlineHours` 336(지도 경로 설계의 시간 총량)·`debtCityId` |
| 지도 프리팹 | `Assets/Art/PuzzleWorldMap.prefab`<br>guid `ab2441b70b1c4490988e4eb2ca06784a` | 화면 좌표·선분·라벨 등 모든 시각 요소, 그리고 Presenter/View/NodeView 컴포넌트 자체 |
| 시간 계산 | `Assets/Scripts/Runtime/Core/PuzzleTimeManager.cs` | 분 단위 시계·폴라인 길이 계산·야간 판정. 밸런스는 시간 테이블이 원본 |

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

## 3. 노선과 소요 시간 — 잠정

- `PuzzleRouteTableData.routes[].points`가 게임 시간 계산용 고정 폴라인 좌표다. 방향 86개(왕복 43철도)를 기록하며 역방향은 같은 점의 역순이다. 기존 `travelTimeWeight`는 제거했다.
- `PuzzleTimeTableData.pixelsPerHour`가 환산 비율 K다. 현재 28픽셀/시간은 잠정치이며, 소요 분은 `ceil(구간 길이 합 / K × 60)`이다. 화면 줌·UI 배율은 계산에 참여하지 않는다.
- 소요 시간 범위는 잠정 K 기준 2시간 54분~30시간 51분이다. 요금용 `distance` 1~9, 기본요금 6, 거리·적재 단가 1은 변경하지 않았다.
- `PuzzleTimeManager`가 분 단위 시계를 보유하고 표시·이동·물품 보유 시간·가치 누적이 같은 소요 분을 사용한다. 시작 06:00, 일출 06:00~24:00, 야간 00:00~06:00이다.
- `Tools/MyJG/Apply Time Planning`은 표 캐시를 갱신하고 지도 시간 라벨·시장 입력 그룹·Game 씬의 아침 대기·예측 목적지 버튼을 연결한다. 원본 레이아웃의 좌표와 노선 연결은 유지했다.

### 금융마을까지 최단 이동 시간 — K=28 잠정 산출

| 출발 마을 | 이동 시간만 합산 |
|---|---|
| 카르포트 | 25시간 36분 |
| 노르노바 | 36시간 26분 |
| 에른아 공업역 | 39시간 53분 |
| 헤일포드 | 45시간 26분 |
| 포르베르 | 54시간 46분 |

고정 좌표의 경로 합산값이며 대기·거래·자금·화물 상태와 플레이 가능성을 검증한 결과가 아니다.

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

- `PuzzleMapView.Awake`는 지역 라벨·노선 색과 폴라인 기반 소요 분 라벨을 채운다. 표에 없는 노선 바인딩은 갱신하지 않는다.
- `PuzzleMapPresenter` — `SceneServices.For<PuzzleInventoryService>`로 세션을 얻고, 노드 Hover/Click을 구독한다. Click → `EvaluateTravel` → 확인·취소 모달 → 확인 시 재판정 → 이동 연출 → `Travel` 1회 → 도착 표시 → 지도 Close 순서다. 실패는 지도 `information`에 표시한다.
- `PuzzleMapTravelView : PanelBase`는 `MapPanel/MapTravelPanel` 아래에 사전 배치한 확인 카드·이동 카드·기차·진행 막대를 제어한다. 기본 선택은 취소이며 `UI/Cancel`도 취소다. 이동 중에는 확인·취소·지도 조작을 막는다. 패널이 닫히거나 비활성화되면 Presenter의 대기 코루틴을 중단한다. 이동 확정 전 중단은 도시·시간·자금을 바꾸지 않는다.
- `Tools/MyJG/Apply Map Travel UI`(`MapTravelSetupTool`)로 모달·연출·Presenter 참조를 다시 저장한다. 연출 실제 초 단위 시간은 `PuzzleTradeTableData.travelEffectSeconds`·`travelArrivalHoldSeconds`, 문구·색은 `Presentation`이 원본이다. 실제 게임 시간·요금 계산은 기존 `PuzzleInventoryService.Travel`만 수행한다.
- 노드 상태는 `HasRoute(현재, target)` + `EvaluateTravel` + `session.HasVisitedCity` + `planningTargetCityId` 비교로 계산한다. **화면 좌표와 선분은 프리팹에 있고, 인접 관계·시간·비용·방문 여부는 전부 테이블과 세션 조회 결과다.** 프리팹에 인접 목록이 중복 저장되지는 않는다.
- `GameEvents.CityArrived`를 받아 도착 후 다시 갱신한다.
- 노선 라벨은 `PuzzleMapTime(소요 분)`으로 시간·분을 표시한다.

## 5. 표시와 입력

- 문구·색·시간 서식은 `Presentation`이 원본이다. 지도·견적·호버에서 계산된 같은 소요 분을 사용한다.
- 이동 확인창은 목적지·시간·도착 시각과 야간 여부·요금·이동 후 자금을 표시한다.
- Game 씬 HUD의 예측 목적지 버튼은 직접 연결된 도시를 ID 순서로 순환 선택한다. 미선택 시 미래 가치만 숨기고, 지도에서 역을 선택하면 같은 예측 목적지로 기록한다.
- 야간 시장은 `CanvasGroup`으로 흐리게 표시하고 입력을 끈다. 모델의 `EvaluateDrop`도 시장을 오가는 거래와 시장 내 재배치를 거부한다. 기차 내 조작과 다른 역으로 이동은 허용한다.
- 아침 대기는 다음 일출까지 시간·보유 시간을 진행한다. 이동 횟수·요금·시장 재고는 변경하지 않으며 시너지 정산 주기를 넘으면 가치를 정산한다.

## 6. 세션 시간

- `PuzzleInventoryService.Time.ElapsedMinutes`가 단일 게임 시간이다. UI 시계는 시작 시각을 더해 시·분을 표시하고, 날짜는 자정 경계에서 바뀐다. 기한은 시작부터의 경과 분으로 비교한다.
- 소요 시간은 `PuzzleTimeManager.TravelMinutes(route, timeRules)`이며 환산 후 분 미만을 올림한다. `Time.ArrivalAt`은 누적 범위를 먼저 검사한다.
- 이동은 가치·보유 시간 범위를 검사한 후 비용·도시·시간을 한 번 확정한다. 가치 변화는 `(시너지−기본 가치) × 행동 중 통과한 6시간 정산 횟수`다. 호버 예측도 같은 횟수를 사용한다.
- 보유 시간은 상태 확정 중 갱신하고, 날짜 변경 부산물을 생성한 뒤 `TimeAdvanced`를 발행한다. 새 부산물의 보유 시간은 0에서 시작한다. 이벤트는 `previousMinutes`·`elapsedMinutes`·`DeltaMinutes`를 제공한다.

## 7. 표 검증

- 환산 K는 유한한 양수다. 일출·야간은 양수이며 합이 24시간이어야 한다. 시작·일출 시각은 0~23 범위다.
- 폴라인은 유한한 x,y 쌍이 두 점 이상이고 길이가 양수여야 한다. 왕복 폴라인은 정확한 역순이다. 계산 가능한 소요 분인지도 검사한다.
- 기존 도시 참조·노선 중복·거리·비용·양방향 연결과 근거리 교역 제한 검증을 유지한다. 테이블 오류는 모아서 보고하고 사용 전에 `RequireValid()`로 차단한다.

## 8. 변경 위치

| 변경 | 원본 |
|---|---|
| 마을 위치·노선 모양 | 레이아웃 JSON과 노선 표 `points`를 함께 갱신한 뒤 레이아웃 적용 |
| 이동 환산 비율 | `PuzzleTimeTableData.pixelsPerHour` |
| 시작·일출·야간 | `PuzzleTimeTableData.startHour`·`daylightStartHour`·`daylightHours`·`nightHours` |
| 요금용 거리·단가 | `PuzzleRouteTableData`의 `distance`·`cost`·`costPerDistance`·`costPerCell` |
| 기한 | `PuzzleTradeTableData.deadlineHours` |
| 툴팁 문구 | `BasicDataTableData`의 `presentation.PuzzleTooltip*` 항목 |
| HUD·시장 그룹·지도 시간 연결 | `Tools/MyJG/Apply Time Planning` |
| 표 캐시 | `Tools/MyJG/Tables/Reload Tables` (플레이 중 제외) |

## 9. 남은 확인

- K=28과 거리 비례 가치 누적의 경제 밸런스는 잠정이며 플레이 검증이 필요하다.
- 일출 안의 연속 정차 수, 야간 대기와 상환 기한·자금 압박은 실제 플레이로 확인하지 않았다.
- `highSpeed`는 시간 계산에 참여하지 않는 기존 필드다.
