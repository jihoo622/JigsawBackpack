# 맵 데이터와 화면 구성

맵(교역 지도) 관련 사실의 위치를 한 곳에 모은 참조 문서다. 다른 세션이 기획서를 읽지 않아도 **어디서 무엇을 고쳐야 하는지** 알 수 있게 저장소 좌표만 적는다. 게임 규칙·의도는 [게임 기획서](<../게임 기획서.md>) 3장·7장, 테이블 일반 계약은 [TABLES](TABLES.md), 시간 구현 계약은 [게임 시간 시스템](<게임 시간 시스템.md>)이 원본이다.

## 1. 맵을 구성하는 저장소

맵은 **테이블 4종 + 지도 프리팹 + 지형 배경 이미지 + 시간 계산 코드**로 구성한다.

| 저장소 | 경로 | 맵에서 담당하는 것 |
|---|---|---|
| 노드 테이블 | `Assets/Resources/Tables/PuzzleTableData.json` | 마을 30·지역 7·`startCity`·`planningTargetCityId`, 각 마을의 시장 격자와 재고 |
| 노선 테이블 | `Assets/Resources/Tables/PuzzleRouteTableData.json` | 방향 노선 100항목의 표시 선과 같은 `points`·요금용 `distance`·`cost`·`highSpeed`, 거리·적재 단가 |
| 시간 테이블 | `Assets/Resources/Tables/PuzzleTimeTableData.json` | K·시작 시각·일출 시각·일출/야간 길이 |
| 시간·판정 테이블 | `Assets/Resources/Tables/PuzzleTradeTableData.json` | `deadlineHours` 336(지도 경로 설계의 시간 총량)·`debtCityId` |
| 지도 프리팹 | `Assets/Art/PuzzleWorldMap.prefab`<br>guid `ab2441b70b1c4490988e4eb2ca06784a` | 화면 좌표·선분·라벨 등 모든 시각 요소, 그리고 Presenter/View/NodeView 컴포넌트 자체 |
| 시간 계산 | `Assets/Scripts/Runtime/Core/PuzzleTimeManager.cs` | 분 단위 시계·폴라인 길이 계산·야간 판정. 밸런스는 시간 테이블이 원본 |

- 맵 UI 스크립트(PuzzleMapPresenter 1·PuzzleMapView 1·PuzzleMapNavigation 1·PuzzleMapTravelView 1·PuzzleMapNodeView 30)는 **전부 프리팹 안에 자기 완결로 들어 있다.** `Assets/Scenes/Game.unity`는 프리팹 인스턴스 하나를 사용한다.
- `Game.unity`의 인스턴스는 루트 RectTransform(fileID 700011)에 프로퍼티 오버라이드 11개(pivot·anchor·sizeDelta·localScale)를 저장한다. 노드·선분·라벨 좌표는 전부 프리팹에 있고 씬 오버라이드는 없다.
- 배경 이미지의 지리 원본은 `rail_map_geography_seed_20265146.json`이며, `Game.unity`는 지도 프리팹 인스턴스를 사용한다.
- 원본 43개 역 연결·운임·시간 좌표는 생성 데이터 `ProjectWorldData.json`(seed 20265146)의 `projectMapData`를 유지한다. 표시 경로는 별도의 우회선으로 정돈한다. 표에 들어갈 값은 `targetTables`가 지정한 3개 테이블에 넣고, 표시 좌표와 노선 폴리라인은 [rail_map_layout_seed_20265146.json](rail_map_layout_seed_20265146.json)에 따로 저장한다. 원본 `ProjectWorldData.json`(1.5MB, 지형 배열 포함)은 저장소에 넣지 않고 위 파일만 보관한다. 지역 구분·태그 경제·시장 격자는 생성 데이터 대신 기존 테이블 값을 유지한다.
- 프리팹에도 노드 좌표·노선 선분·시간 라벨·환승 표시를 적용해 저장했다. `Tools/MyJG/Apply Rail Map Layout (seed 20265146)`으로 같은 레이아웃을 다시 적용할 수 있다(§4).

## 노선 데이터 무결성

- 실행 표의 88개 방향 연결과 레이아웃의 44개 철도, 지도 프리팹의 시간/배경판 44개 참조가 일치한다. 자기 연결·중복 방향·누락 도시·끊긴 참조가 없고 모든 역이 출발지에서 도달 가능하다.
- 왕복 점 배열은 역순으로 일치하며 거리·고속 여부도 같다. 폴라인 끝점은 역 좌표와 일치하고 실행 표와 레이아웃의 원본 `points`·거리·기본요금이 일치한다. `displayPoints`를 시간 계산용 `points`와 왕복 테이블에 동기화해 같은 표시 선 길이로 시간을 계산한다. 기존 노선 구성·운임은 유지하고 R44 카르포트↔포르베르 공업 연결을 추가했다.
- 현재 역과 직접 연결된 노선만 시간 표시 대상이며 자금 부족 여부로 숨기지는 않는다. 다른 역의 선택·호버는 표시 대상을 확장하지 않는다. 줌·겹침 회피 조건은 유지한다.
- 최단 단일 구간은 베르스카↔헤일하임 2시간 54분, 최장은 라센턴↔피오아 항 30시간 51분이다. 요금 거리와 시간은 별개다. 구조 확인은 경제/야간 동선의 플레이 밸런스 검증을 뜻하지 않는다.

- R44는 카르포트↔포르베르의 공업 지역 왕복 연결이다. 기존 43개 연결을 보존하고 시간용 좌표 `[-500.12,115,-340.12,-45,-272.22,-45]`, 요금 거리 3·기본요금 3을 사용한다. K=28 기준 631분이며 포르베르→금융마을 최단 이동 합은 2167분이다.
- 지도 재열기 시 확대 배율과 이동 위치를 유지하며 최초 열기·전체 보기 요청 때만 전체 배치를 맞춘다.

### 지명과 역의 대응

- 노드의 cityId로 테이블 이름을 표시하고, 위치와 클릭/호버도 같은 노드 ID를 사용한다. 다르안은 city.mine.salt, 세르그라드는 city.capital.merchant다. 줌 시 다른 역 쪽으로 배치된 지명이 잘못 연결되어 보이지 않도록 각 지명에 해당 역까지 안내선을 붙인다.
- 최초에는 프리팹의 이름 배치 방향, 이후에는 직전 성공 방향을 먼저 사용한다. 공간이 없으면 테이블의 탐색 거리/간격 안에서 다른 방향·거리를 찾는다. 이름·배지·역 링·현재 위치 테두리는 실제 RectTransform/텍스트 크기로 예약하고, 안내선이 다른 역·이름·안내선을 가로지르는 후보는 제외한다.
- 지역의 mapLabelPriority와 역의 mapLabelImportance가 기본 가중치다. 여기에 현재/호버/목표/직결/환승/기타 중 해당하는 상태 가산점을 더한다. 현재와 호버 가산점은 기본 중요도보다 높게 설정했다. 같은 점수는 cityId 순으로 결정한다.
- 85% 미만에서는 중요도 200 이상 핵심 역과 현재/호버/목표 역의 이름을 우선 표시한다. 일반 역 표식은 유지하고 확대하면 일반 역명도 배치한다. 핵심 역은 더 큰 굵은 글씨로 구분한다. 기준은 ui.mapLabels.majorThreshold/detailMinZoom 및 presentation.map.majorStationFontSize다.
- 100% 미만에서는 같은 지역에 이미 표시한 이름 수 × regionCrowdingPenalty를 가중치에서 빼 다음 후보를 고른다. 한 지역이 이름 공간을 먼저 차지하지 않게 하며 실패한 배치는 지역 표시 수에 포함하지 않는다. 범위는 regionBalanceBelowZoom으로 조정한다.
- 겹침 허용 예외는 없다. 후보가 없으면 이름만 생략하고 역은 유지한다. 호버 시 이름을 우선 배치하고 상세 정보에도 같은 cityId를 표시한다. 지역마다 연결 수가 가장 많은 역을 초기 핵심으로 지정했으며 현재 가중치는 테이블에 명시돼 있다. 노선 모양을 바꿔도 중요도가 자동으로 바뀌지는 않는다.
- LabelConnector Image 30개는 프리팹의 각 노드에 저장하며 런타임에 만들지 않는다. 기초데이터 `ui.PuzzleMapView.labelSearchStep`/`labelSearchDistance`/`labelConnectorThickness`가 탐색·선 두께의 원본이다.
- 축소 시 입력은 실제 역 표식 영역으로 제한한다. 두 표식이 겹치는 위치는 더 가까운 역이 입력을 받고, 같은 거리이면 cityId 순으로 결정한다. 이전 역의 늦은 포인터 이탈이 새 역의 설명을 지우지 않도록 이탈 ID도 확인한다.
- 줌·이동·화면 크기 변경 때 다시 배치한다. 변경 후 플레이/화면 확인은 수행하지 않았다.

### 표시 선과 시간·수동 라벨 계약

- 이동 시간과 요금 거리 산출은 같은 표시 폴라인 길이를 사용한다. 맵/경유점 에디터는 displayPoints를 레이아웃 points와 왕복 노선 테이블 points에 동기화한다. 역·선 모양을 수정하면 시간도 바뀌며 요금 거리 정수는 기존 자동 계산 버튼으로 적용한다. 시간 전용 경로 편집 모드는 제거했다.
- labelManual=true인 라벨은 자동 배치·프리팹 역 겹침 보정·런타임 위치 이동에서 제외한다. 좌표 입력/드래그는 자동으로 고정하며 기존 저장 좌표도 고정으로 보존했다. 실행 화면은 고정 라벨 영역부터 예약해 역명을 피하도록 배치한다. 화면 밖·역 표식·다른 고정 라벨과 충돌하면 임의로 옮기지 않고 숨긴다.
- 선택 노선의 자동 배치는 해당 고정을 해제한다. 전체 자동 배치는 고정되지 않은 것만 처리하며 `고정 해제 후 전체 자동 배치`로 명시적으로 전체를 풀 수 있다. 라벨 좌표는 저장 후 재적용해도 같은 값을 쓴다.

### 통합 맵 에디터

- `Tools/MyJG/Map Editor`에서 역·노선·지역·공통 설정을 편집한다. 창 내부에 왼쪽 목록·가운데 지도 캔버스·오른쪽 속성 편집을 배치했다. 프리팹/Scene을 열지 않고 지형 이미지와 초안의 역·역명·노선·선택 경유점·선택적 시간을 직접 렌더링하며, 필드 변경과 드래그를 즉시 반영한다.
- 캔버스에서 역 표식을 클릭/드래그해 선택·이동하고 선을 클릭해 노선을 선택한다. 선택 노선의 사각 경유점을 드래그해 편집하며 같은 표시 경로와 이동 시간에 반영한다. 시간 표시를 켜면 선택 노선의 시간 라벨 중심도 드래그할 수 있다. 휠은 포인터 기준 확대/축소, 빈 곳 또는 중간/오른쪽 버튼 드래그는 지도 이동이다. `전체 보기`·`선택 보기`, 지형/격자/역명/시간 표시 토글을 제공한다. 역명은 겹치지 않는 후보 위치를 찾아 해당 역까지 안내선으로 연결한다.
- **역**: 이름·지역·지명 중요도·환승역 표식·좌표·구매/판매 허용·기본 배율·특산/재고 ID를 수정한다. 시장 보드·태그별 가격 등 나머지 도시 필드는 창 안의 상세 JSON을 수정한 뒤 `상세 데이터를 초안에 적용`한다. ID는 기존 참조를 보존하기 위해 고정한다. 추가는 선택 역의 시장·교역 설정을 복제하므로 새 ID/이름을 입력하고 위치·설정을 조정한다. 삭제는 연결 노선도 제거하며 시작/목표/상환 역 삭제는 차단한다.
- **노선**: 기존 노선을 분류·고속선 템플릿으로 선택하고 출발/도착 역을 지정해 왕복 노선을 추가한다. 연결 역 변경·삭제, 왕복 거리/고속 여부·방향별 기본요금·색 그룹·분류·시간 라벨 위치를 수정한다. 경유점은 표시와 시간 계산에 공통으로 사용하며 왕복 테이블에 역순으로 반영한다. 새 연결은 처음에 두 역 사이 직선으로 생성하며 기준 기본요금과 선 길이에 따른 요금 거리를 적용한다.
- **요금 자동 설정**: `공통 설정 → 노선 요금 일괄 설정`에서 기준 기본요금·거리 1당 지도 길이를 편집한다. 기준값은 PuzzleRouteTableData.defaultBaseCost(현재 3)와 fareDistancePixelsPerUnit(초기 100)이다. `전체 왕복 기본요금에 적용`과 `전체 요금용 거리 자동 계산`은 초안만 갱신하며 Undo 가능하다. 노선별 길이·기존 거리→계산 거리 미리보기를 제공하고, 선택 노선의 속성에서도 `선 길이로 거리 계산`할 수 있다. 거리는 displayPoints(없으면 points)의 전체 구간 길이 합 ÷ 기준 길이를 반올림(정확한 중간은 큰 쪽), 최소 1로 계산해 왕복과 레이아웃에 동일하게 기록한다. 줌·교차점 표시 틈은 계산에 영향을 주지 않는다. 요금용 거리 정수는 버튼으로 재계산하며 이동 시간은 표시 경로 변경에 따라 함께 바뀐다.
- **역 이동**은 연결 노선 끝점을 함께 옮기며 표시 선 길이에 비례해 이동 시간도 바뀐다. 표시 경로와 시간 경로를 따로 편집하지 않는다.
- **시간 라벨 자동 배치**: 선택 노선의 속성에서 `이 노선 시간 라벨 자동 배치`, 공통 설정에서 `전체 시간 라벨 자동 배치`를 사용한다. 역·노선·이름 변경 후 자동 재배치는 기본 켜짐이며 수동으로 옮긴 라벨은 고정해 유지한다. 드래그 중에는 배치를 미루고 놓을 때 한 번 계산해 같은 Undo 단위로 묶는다. 표시 경로의 길이상 중간을 우선하며 역 표식·기본 역명 영역·다른 시간 라벨·다른 노선을 피하는 후보를 탐색한다. 빈 공간이 없으면 기존 위치를 유지하고 해당 노선 ID를 알려준다. 배치는 지도 좌표로 저장되며 줌별 최종 겹침 처리는 런타임 지도의 기존 배치 규칙을 따른다.
- **지역**: 이름·지명 우선순위·물가와 상세 지역 데이터를 수정한다. **공통 설정**: 시작 역·지도 목표 표식·거리당/적재 칸당 단가를 수정한다. 상환 장소는 별도 `PuzzleTradeTableData.debtCityId` 설정을 유지한다.
- 조작은 Undo/Redo 가능한 초안이다. `전체 저장 · 프리팹 반영`이 PuzzleTableData·PuzzleRouteTableData·레이아웃 JSON과 프리팹을 함께 갱신한다. 새 역은 기존 역 프리팹 구조를 복제해 nodes 참조까지 연결하며 삭제된 역·노선의 표시도 정리한다. 기존 도구의 미리보기는 통합 편집 창이 열려 있는 동안 숨긴다.
- 외부 파일 변경·프리팹의 미저장 편집·중복 ID/연결·잘못된 좌표·테이블 규칙 위반 시 반영을 중단한다. 파일 기록/프리팹 반영 중 실패하면 원본을 복구한다. `초안 내보내기`로 세 원본의 초안을 별도 JSON에 보관할 수 있다. 저장은 초안 Undo 이력을 초기화한다.
- 통합 맵 에디터의 저장·캔버스 입력·플레이 확인은 수행하지 않았다. 기존 경유점 도구의 확인 범위는 아래와 같다.

### 기존 노선 모양·경유점 편집

1. `Tools/MyJG/Edit Rail Routes`를 연다.
2. `지도 프리팹 열기`를 누르거나 Game 씬의 PuzzleMapView를 편집 지도에 지정한다. sourceId와 출발/도착 이름으로 노선을 선택하고 `선택 노선 보기`로 2D Scene 화면을 맞춘다. Scene 창이 없으면 자동으로 열며, 지도 패널이 비활성이어도 역·전체 노선·선택 경유점을 편집용 미리보기로 표시한다.
3. 경유점을 Scene 핸들로 드래그하거나 창에서 좌표를 입력한다. 선택 점 다음에 추가/삭제, 격자 스냅, 선택적 시간 라벨 위치 편집을 지원한다. 출발/도착 역 좌표는 고정한다. Undo/Redo는 미저장 초안에 적용된다.
4. `저장하고 지도 프리팹 반영`을 누른다. 레이아웃 JSON의 해당 sourceId.displayPoints와 labelX/Y를 저장하고 기존 RailMapLayoutTool로 프리팹에 적용한다. 저장은 초안 Undo 이력을 초기화한다.

- 편집한 표시 경로를 레이아웃 points와 왕복 시간 계산 테이블에도 동기화한다. 역 연결·운임·요금 거리·다른 메타데이터는 유지한다. 불러온 JSON의 알 수 없는 필드도 보존한다. 수동으로 옮긴 시간 라벨은 고정해 저장하고 역 겹침 보정에서 제외한다.
- 외부에서 레이아웃 파일이 바뀌거나 지도 Prefab Stage에 다른 미저장 변경이 있으면 적용을 막는다. 경유점 이동 중에는 씬/프리팹을 수정하지 않는다. 프리팹 적용 실패 시 이 창이 기록한 레이아웃 변경은 복구한다.
- 현재 연결의 모양만 편집하며 역 연결 추가/삭제는 제공하지 않는다. Unity 에디터 컴파일, 실제 메뉴/창 열기, 프리팹 연결, 경유점 드래그·추가·삭제와 Undo/Redo를 확인했다. 확인에 사용한 초안은 원상 복구했다. 저장/프리팹 반영과 플레이 확인은 수행하지 않았다.

### 표시 경로와 교차점

- R02 라센턴↔피오아 항은 테라카 북쪽으로 우회한다. R03 피오아 항↔이브트는 금융마을 동쪽·남쪽으로 우회한다.
- R04·R07·R08·R11·R25·R32·R33·R37·R43은 중앙부 진입 방향과 겹치는 직선을 분리한다. 원본 역 좌표를 옮기지 않는다.
- 표시 경로는 수평·수직·45도로 구성하며 비경유 역 중심에서 18 지도 단위 이내를 지나지 않고, 서로 다른 노선의 18 단위 초과 공통 직선도 없다.
- 교차점의 틈은 비환승 교차를 나타낸다. 정차/이동 가능 여부는 역 표식과 실행 테이블 연결로만 판정한다. 지도 우회선의 길이로 시간·운임을 다시 계산하지 않는다.

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
| `region.capital` | 수도권 | — | — | 100%(할증은 도시 기본 배율 125%) | O |
| `region.coast` | 해안 지역 | Fish | Fish, Water | 100% | — |
| `region.farm` | 농업 지역 | Fruit | Grain, Fruit, Wood | 100% | — |
| `region.common` | 일반 마을권 | — | — | 100% | O |

### 마을 30

`displayName`은 생성 원본의 역 이름이고 **이름과 ID는 따로 관리**한다. 이번 판에서 ID 대응이 바뀌어 같은 이름이 다른 `city.*`에 붙었다(라센턴은 `city.mine.iron`가 아니라 `city.mine.dig`). `tagPrices`가 6개인 26개 마을(생산 지역 21 + 수도권 5)은 태그 규칙을 쓰고, 중립 경유 마을 4개는 0개라 기본 배율 100/90만 쓴다. 지역 열은 `region.` 접두사를 생략했다.

| id | 이름 | 지역 | 특산 태그 | 배율 | `tagPrices` | 생성 역·`sourceId` | 타일 (x,y) | 차수 |
|---|---|---|---|---|---:|---|---|---:|
| `city.industry.assembly` | 포르베르 | industry | Machinery | 100/90 | 6 | 포르베르 `S10` | 22, 29 | 2 |
| `city.industry.engine` | 노르노바 | industry | Machinery | 100/90 | 6 | 노르노바 `S11` | 19, 62 | 2 |
| `city.industry.ironworks` | 에른아 공업역 | industry | Machinery | 100/90 | 6 | 에른아 공업역 `S01` | 13, 47 | 5 |
| `city.industry.logistics` | 헤일포드 | industry | Machinery | 100/90 | 6 | 헤일포드 `S12` | 4, 37 | 2 |
| `city.industry.meatlover` | 카르포트 | industry | Machinery | 100/90 | 6 | 카르포트 `S13` | 40, 45 | 3 |
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

- `PuzzleRouteTableData.routes[].points`가 표시 선과 동기화된 게임 시간 계산 좌표다. 방향 100개(왕복 50철도)를 기록하며 역방향은 같은 점의 역순이다. 기존 `travelTimeWeight`는 제거했다.
- `PuzzleTimeTableData.pixelsPerHour`가 환산 비율 K다. 현재 28픽셀/시간은 잠정치이며, 소요 분은 `ceil(구간 길이 합 / K × 60)`이다. 화면 줌·UI 배율은 계산에 참여하지 않는다.
- 소요 시간 범위는 잠정 K 기준 3시간 41분~20시간 37분이다. 요금용 `distance` 1~9, 거리·적재 단가 1은 변경하지 않았다. 기본요금은 이후 경제 조정으로 3이 되었다.
- `PuzzleTimeManager`가 분 단위 시계를 보유하고 표시·이동·물품 보유 시간·가치 누적이 같은 소요 분을 사용한다. 시작 06:00, 일출 06:00~24:00, 야간 00:00~06:00이다.
- 지도 시간 라벨·시장 입력 그룹·Game 씬의 아침 대기와 상환 금액 배치는 프리팹에서 수정한다. 원본 레이아웃의 좌표와 노선 연결은 유지했다.

### 금융마을까지 최단 이동 시간 — K=28 잠정 산출

| 출발 마을 | 이동 시간만 합산 |
|---|---|
| 카르포트 | 25시간 52분 |
| 노르노바 | 37시간 50분 |
| 에른아 공업역 | 35시간 28분 |
| 헤일포드 | 41시간 35분 |
| 포르베르 | 30시간 21분 |

고정 좌표의 경로 합산값이며 대기·거래·자금·화물 상태와 플레이 가능성을 검증한 결과가 아니다.

## 4. 프리팹 — `Assets/Art/PuzzleWorldMap.prefab`

`PuzzleMapView : PanelBase`(`startOpen: 0`)가 루트에 있고, 아래 배열은 **모두 Inspector 수동 바인딩**이다. 키는 cityId·regionId 문자열이라 배열 순서에 의미는 없지만, **누락이나 오탈자는 검증되지 않는다.**

| 배열 | 개수 | 내용 |
|---|---|---|
| `nodes` | 30 | `PuzzleMapNodeView` (`cityId` 30종과 1:1, 누락·여분 0건) |
| `routeTimes` | 44 | `fromCityId`/`toCityId` + `Text`. 레이아웃 도구가 레일 44개와 1:1로 다시 묶는다 |
| `regions` | 7 | `regionId` + 범례 라벨·스와치(Image) |
| `lines` | 8그룹 / 107조각 | `region.*` 7개와 지역 간 흰색 `line.regional`의 노선 색 그룹. 중립 노선은 전용 밝은 회색을 사용 |
| `routeLines` | 107 | 활성 표시 조각 목록. 확대 시 각 조각의 선 굵기를 화면 크기로 보정 |

- 노드 30개의 `m_AnchoredPosition`은 프리팹 저장 값이며, 레이아웃 도구를 돌리면 생성 데이터의 `position`(MapPanel 1920×1080·중심 기준)으로 바뀐다. 범위는 x -728.02~550.77, y -365~355.

오브젝트 네이밍:

- `Village_<cityId>` 24개, `Station_<cityId>` 6개. Station은 `city.capital.finance`(금융마을)·`city.common.*` 4개·`city.industry.logistics`(헤일포드)로, 허브 성격을 그림으로 구분한 것이다. 이름은 바뀌어도 오브젝트 이름은 ID를 쓴다.
- `Track_<n>_<m>` = 레일 `n`(0~43)의 표시 조각 `m`(1부터). 시간용 폴리라인은 87구간이며, 우회선·교차점 끊김을 반영한 표시 조각은 R44를 포함해 107개다. 표시 조각 수는 연결/역/시간 단위가 아니다.
- 노드 하위에 `Label`·`Marker`·`StationRing`·`StationFace`·`CurrentHalo`·`CurrentBadge`가 각각 30개 있다. 지도 버튼들도 별도 `Label`을 사용한다.
- 계층: `PuzzleWorldMap`(루트·`PuzzleMapView` + Canvas) → `OpenMap` 버튼과 `MapPanel`. `PanelBase.root`는 `MapPanel`(fileID 700040)을 가리킨다.
- `MapPanel`의 고정 영역은 `Title`·`Guide`·`CloseMap`·`CityInformation`·`SubwayMapLabels`·`MapControls`이고, 지도 내용은 `MapViewport/MapContent` 아래에 있다.
- `SubwayMapTracks`는 활성 `Track_*` 107개와 재적용 시 비활성화된 이전 표시 조각을 보관한다. `TimePlate_*` 44·`Time_*` 44개는 지도 내용과 함께 확대한다. `SubwayMapLabels`에는 고정 범례인 `LineSwatch_*` 7·`LineName_*` 7만 남긴다.

### `PuzzleMapNodeView` 필드

| 필드 | 개수·분포 |
|---|---|
| `cityId` | 30 (테이블 도시 ID와 정확히 일치) |
| `interchange` | 도구 실행 후 true 15 · false 15 (차수 ≥3과 정확히 일치) |

색 분기만 쓰는 시각 플래그이고 테이블에는 없는 프리팹 전용 값이다. 현재 프리팹에도 생성 데이터의 환승 표시가 저장되어 있다.

### 지도 그림을 갱신하는 도구

배경·확대/축소·마진 구성:

- `Assets/Art/PuzzleMapTerrain.png`는 외부 지형의 육지·바다·고도·강·경제 지역 경계를 그린 배경이다. `rail_map_geography_seed_20265146.json`에 필요한 원본 배열을 보관하고, `Tools/MyJG/Generate Map Terrain Image`로 재생성한다. 국가 경계와 경제 지역 경계는 서로 다른 정보이며, 현재 배경에는 경제 지역 경계만 표시한다.
- 지도 프리팹은 `MapViewport`(RectMask2D) 아래 `MapContent`에 배경·노드·선분·시간 라벨을 함께 배치한다. 배경 크기는 1570×820이며 타일 중심과 역 좌표를 일치시킨다.
- `PuzzleMapNavigation`은 복제한 `InputSystem_Actions`의 `Map/Zoom`·`Map/Point`를 사용한다. 휠은 포인터 위치 기준 35~250% 확대·축소, UI 드래그는 지도 이동이다. 가장자리를 제한하고, 전체 보기와 현재 위치 버튼을 연결한다. 패널을 닫으면 입력 구독을 해제한다.
- 지형과 역 좌표는 줌에 따라 변하지만 노드 전체·시간 라벨·시간 배경은 역배율로 보정해 화면상 크기를 유지한다. 노선 길이는 확대하고 굵기는 역배율로 보정한다. 이름은 전체 보기에서 18, 상세에서 22, 현재 역은 24 크기다. 시간은 18, 현재 위치 배지는 16 크기다. 시각 크기의 원본은 `Presentation`이다.
- 확대·축소·드래그·호버·도착 때 `PuzzleMapView.UpdateMapLayout`이 라벨을 재배치한다. 이름은 현재 역·가리킨 역·목표 역·직접 연결 역·환승역·나머지 역 순으로 처리하고, ID로 동률을 정렬한다. 마커를 피하는 방향을 선택하고 자리가 없으면 이름만 생략한다. 현재 역과 가리킨 역의 이름은 우선 보존한다. 시간은 85% 이상에서 남은 공간에 배치하고, 겹치는 시간 라벨은 숨긴다. 생략된 도시 정보·이동 시간은 역을 가리켜 하단 정보에서 확인한다.
- 제목·범례·도시 정보·버튼은 확대 대상 밖에 고정한다. 좌우 여백은 48px, 지도 영역은 위아래 228px를 비운다. 현재 역은 금색 링·배지·굵은 글씨로 강조한다.
- 지도 배치는 프리팹(`Assets/Art/PuzzleWorldMap.prefab`)에서 수정한다. 지형 이미지는 `Tools/MyJG/Generate Map Terrain Image`로 재생성한다. 이 작업에서 플레이·입력 실험·화면 캡처는 수행하지 않았다.

`Tools/MyJG/Apply Rail Map Layout (seed 20265146)` — `Assets/Scripts/Editor/Build/RailMapLayoutTool.cs`. 레이아웃 파일 [rail_map_layout_seed_20265146.json](rail_map_layout_seed_20265146.json)을 읽어 **프리팹만** 고쳐 저장한다(씬·테이블은 건드리지 않는다).

| 작업 | 내용 |
|---|---|
| 노드 | `cityId`로 노드를 찾아 `anchoredPosition` = 레이아웃 `x`·`y`, `interchange` = 차수 ≥3(15개 true) |
| 노선 | `displayPoints`가 있으면 그 좌표로, 없으면 원본 `points`로 그린다. 교차점 틈을 반영한 조각을 `Track_<railIndex>_<part>`로 저장하며 부족한 조각은 복제하고 남는 조각은 비활성화한다 |
| 굵기 | 모든 노선 5px 제작값(런타임 화면 굵기 보정 공통 적용) |
| 시간 라벨 | `routeTimes[i]`의 출발·도착을 레일 `i`로 다시 묶고 `label` 참조를 `Time_<railIndex>`의 Text로 고정, `Time_*`·`TimePlate_*`를 `labelPosition`으로 이동 |
| 노선 색 그룹 | `lines[].segments`를 `regionId`로 묶는다. 지역 간 흰색→중립 밝은 회색→지역 내부 색 순으로 그리고, 같은 계층은 `sourceId`로 정렬한다 |
| 보정 | 라벨이 노드와 60px 이내로 겹치면(18개) 노드에서 바깥쪽으로 34px 밀어 배치 |
| 경고 | 노드 좌표와 폴리라인 끝이 2px 이상 어긋나거나 레이아웃에 없는 `cityId`가 나오면 로그에 남긴다 |

- R44를 포함한 표시 조각은 레이아웃 도구가 교차 간격을 반영해 저장한다. 교차점에서는 아래 노선에 12 지도 단위 틈을 만들고 실제 역 접속 부근은 끊지 않는다. 틈·접속 보호 거리·그리기 순서는 기초데이터가 원본이다. 런타임 오브젝트 생성은 없다.
- 노드 2개(`city.farm.field` 145.6,-365 / `city.ranch.jerky` -158.27,-365)는 y=-365로 하단 `CityInformation`(중심 y=-453·높이 140)과 근접한다. 겹치면 두 노드의 y만 위로 조정한다.
- `Track_`를 참조하는 코드는 여전히 0건이다. 도구가 프리팹에 그림을 심고, 실행 시점의 인접·시간·색은 전부 표 조회 결과다.


### 코드에서 읽는 경로

- `PuzzleMapView.Awake`는 지역 라벨·노선 색과 폴라인 기반 소요 분 라벨을 채운다. 표에 없는 노선 바인딩은 갱신하지 않는다.
- `PuzzleMapPresenter` — `SceneServices.For<PuzzleInventoryService>`로 세션을 얻고, 노드 Hover/Click을 구독한다. Click → `EvaluateTravel` → 확인·취소 모달 → 확인 시 재판정 → 이동 연출 → `Travel` 1회 → 도착 표시 → 지도 Close 순서다. 실패는 지도 `information`에 표시한다.
- `PuzzleMapTravelView : PanelBase`는 `MapPanel/MapTravelPanel` 아래에 사전 배치한 확인 카드·이동 카드·기차·진행 막대를 제어한다. 기본 선택은 취소이며 `UI/Cancel`도 취소다. 이동 중에는 확인·취소·지도 조작을 막는다. 패널이 닫히거나 비활성화되면 Presenter의 대기 코루틴을 중단한다. 이동 확정 전 중단은 도시·시간·자금을 바꾸지 않는다.
- 이동 모달·연출 UI 배치는 지도 프리팹에서 수정한다. 연출 실제 초 단위 시간은 `PuzzleTradeTableData.travelEffectSeconds`·`travelArrivalHoldSeconds`, 문구·색은 `Presentation`이 원본이다. 실제 게임 시간·요금 계산은 기존 `PuzzleInventoryService.Travel`만 수행한다.
- 노드 상태는 `HasRoute(현재, target)` + `EvaluateTravel` + `session.HasVisitedCity` + `planningTargetCityId` 비교로 계산한다. **화면 좌표와 선분은 프리팹에 있고, 인접 관계·시간·비용·방문 여부는 전부 테이블과 세션 조회 결과다.** 프리팹에 인접 목록이 중복 저장되지는 않는다.
- `GameEvents.CityArrived`를 받아 도착 후 다시 갱신한다.
- 노선 라벨은 `PuzzleMapTime(소요 분)`으로 시간·분을 표시한다. 시간 배경 전체를 먼저, 시간 글자 전체를 나중에 그리도록 형제 순서를 고정한다. 새 노선 복제 시 배경이 글자를 가리던 문제를 프리팹·두 지도 제작 도구·런타임 초기 배치에서 함께 수정했다.

## 5. 표시와 입력

- 문구·색·시간 서식은 `Presentation`이 원본이다. 지도·견적·호버에서 계산된 같은 소요 분을 사용한다.
- 이동 확인창은 목적지·시간·도착 시각과 야간 여부·요금·이동 후 자금을 표시한다.
- 하단 예측 목적지 선택 UI는 없다. 지도에서 이동할 역을 선택하면 호버 미래 가치의 목적지로 기록하며 미선택 시 미래 가치만 숨긴다.
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
| HUD·시장 그룹·지도 시간 연결 | 해당 프리팹에서 수정 |
| 표 캐시 | `Tools/MyJG/Tables/Reload Tables` (플레이 중 제외) |

## 9. 남은 확인

- K=28과 거리 비례 가치 누적의 경제 밸런스는 잠정이며 플레이 검증이 필요하다.
- 일출 안의 연속 정차 수, 야간 대기와 상환 기한·자금 압박은 실제 플레이로 확인하지 않았다.
- `highSpeed`는 시간 계산에 참여하지 않는 기존 필드다.
