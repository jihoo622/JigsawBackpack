# 경로 계획·자동 이동·지도 검색 기술 계약

규칙 원본은 [게임 기획서](<../게임 기획서.md>) 8장 '경로 계획과 자동 이동'·'지도 검색'이다. 여기는 코드 구조·이벤트·확장 지점만 둔다.

## 경로 계획(모델)

- 경로 계산: `PuzzleRoutePlanner.FindFastestPath(TableManager, from, to)` — 구간 수가 가장 적은 역 목록(출발 포함). 이동은 구간마다 1턴이라 구간 수가 곧 이동 턴이다. 구간 수가 같으면 노선 거리 합(운임)이 작은 경로, 그것도 같으면 역 ID 서수 비교로 결정. 도달 불가면 null. 선택 인자 `_canUse`로 쓸 노선을 거른다. 경로 계획(`PlanRoute`)은 전장의 안개로 아는 노선(`IsRouteKnown`)만 넘기며, 지도에 없는 역은 목적지로 고를 수 없다(`InvalidCity`). 순수 함수로 두고 의뢰의 최단 턴 계산(전체 노선 기준)과 같은 가중치를 쓴다.
- 상태: `PuzzleInventoryService.RoutePlan`(`PuzzleRoutePlan`, 한 판에 하나, 없으면 null)과 `IsPassingThrough`. API는 `PuzzleInventoryService.Routes.cs`(`PlanRoute`·`SetRouteStop`·`CancelRoutePlan`·`QuoteRoutePlan`·`EvaluateNextRouteLeg`·`TravelNextRouteLeg`·`StopAtCurrentCity`).
- 수동 이동 연동: `Travel(city)`는 항상 정차다. 계획의 다음 역으로 가면 계획을 한 구간 진행하고(목적지면 `Completed`로 종료), 다른 역으로 가면 `Deviated`로 취소한다. 판이 끝나면(승패) `RunEnded`로 취소한다.
- 통과역: 도착 시 방문 기록(`visitedCities`)·의뢰 완료·`CityArrived`·도착 기록을 하지 않는다. 시간 진행(정산·배설·만료·성장·매 턴 시장 재입고)과 운임은 수동 이동과 같다.
- 이벤트: 계획이 바뀌면 `GameEvents.RaiseRoutePlanChanged(new RoutePlanInfo(session))`.

## 자동 이동(UI 실행)

- 실행 주체는 지도 Presenter(`PuzzleMapPresenter`)다. 구간마다 `EvaluateNextRouteLeg` → 이동 연출 → `TravelNextRouteLeg(out stopped)`를 반복하고, 정차하면 끝낸다.
- 중단: 다음 구간 평가 실패(운임 부족 등)·판 종료·중지 요청이면 루프를 멈추고, 통과역이면 `StopAtCurrentCity()`로 정차 처리한다. 중지 요청(버튼·Esc)은 달리는 구간을 마친 뒤 적용한다.
- 계획 창: `PuzzleRoutePlanView`/`PuzzleRoutePlanPresenter`(지도 프리팹 안). 거치는 역 슬롯(사전 배치, 넘치면 '외 N개')과 정차 토글, 요약, 자동 이동·중지·계획 취소 버튼.
- 노선 강조: `PuzzleMapView.HighlightPath(IReadOnlyList<string>)`/`ClearPathHighlight()`.
- 외부 요청: `GameEvents.RoutePlanRequested`(검색 결과의 '경로' 버튼 등)를 계획 Presenter가 받아 `PlanRoute` 후 창을 연다.

## 지도 검색

- 검색 규칙: `KoreanSearch`(순수) — 공백 무시·대소문자 무시 부분 일치와 한글 초성 일치(질의가 초성만이면 이름의 초성열과 비교). 점수: 정확 일치 > 앞부분 일치 > 포함, 같으면 ID 서수.
- 검색 대상: `SearchEntry { kind(EntityLinkKind), id, name }` 목록. 지도 검색은 `City`·`Region`만 만든다(이름만, 교역 정보 없음).
- UI: `MapSearchView`/`MapSearchPresenter`(지도 머리). 결과 슬롯 사전 배치 6개 + '외 N개'. 결과 선택 → `GameEvents.RaiseMapFocusRequested`, 역 결과의 '경로' 버튼 → `GameEvents.RaiseRoutePlanRequested`.
- 입력 칸 포커스: 포커스를 얻고 잃을 때 `GameEvents.RaiseTextInputFocusChanged`. 받는 쪽(`PuzzleInventoryPresenter` 회전·즉시 거래 단축키 등)은 포커스 중 키보드 게임 입력을 무시한다. Esc는 검색 칸이 포커스면 먼저 비우고 닫는다(일시정지 메뉴의 Esc 순서 맨 앞).

### 전역 검색 확장 지점

- 검색 규칙·정렬은 `KoreanSearch` 하나를 공유한다. 전역 검색은 `SearchEntry` 공급원만 늘린다(품목·태그·의뢰). 공급원은 화면별로 두고 검색 규칙에 화면 의존성을 넣지 않는다.
- 결과 이동은 고유명사 링크와 같은 `(EntityLinkKind, id)` 쌍을 쓰므로 `EntityLinkNavigation.Navigate`로 그대로 보낼 수 있다.
- 시세를 미리 알려 주지 않는 원칙 때문에 품목 검색은 이름·태그로만 찾고 '어디서 비싸게 파는지'로는 찾지 않는다.
- 단축키(예: Ctrl+F)는 `InputSystem_Actions`의 UI 맵에 액션으로 추가한다(코드 바인딩 금지).

## 분석 로그

`PLAY_LOG.md`의 `route_planned`·`route_ended`·`auto_travel_stopped`가 원본이다. 통과 구간도 기존 이동 기록에 `passThrough`로 남긴다.
