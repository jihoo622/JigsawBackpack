# HUD 구성 요소 및 UI 플로우 정리

> 작성 목적: 여러 버전의 HUD를 돌려 보기 전에 현행 구성·이벤트 흐름·시작 상태 규약을 한 데 모아 두는 기준 문서.
> 게임 규칙 원문은 [게임 기획서](../게임 기획서.md)이고, 이 문서에 나온 수치는 그 문서가 원본이다.

## A. 공통 규약 (코드에서 직접 확인되는 것)

1. **모든 UI 패널은 `PanelBase` 상속**: 시작 상태는 직렬화된 `startOpen` 필드로 코드에서 명시하며 씬에 저장된 activeSelf에만 의존하지 않는다. Awake에서 항상 SetOpen(startOpen) 적용 → 비활성화되면 열린 상태·대기 동작을 OnDisable에서 Close로 정리. 표시 여부 변화는 `GameEvents.PanelVisibility`(페이로드 `PanelInfo{패널명, 오픈 bool}`)로 흘리고 입력·카메라·일시정지가 구독한다.
2. **표시는 프리팹 배치 슬롯 풀만 사용** (`PuzzleTrainBarView.Slots Pool`, Inventory 보드의 `placementSlots/ghostSlots/outlineSlots` 등): 코드는 표시·색·시간만 제어하고 런타임 UI 생성이 없다.
3. **MVP 분리**: View는 프리팹에 배치된 표시이고 Presenter가 갱신한다. View 스크립트는 코드에서 생성하지 않는다. 예: `PuzzleInventoryView`가 셀·슬롯·아이콘(`ui.PuzzleInventoryView.itemIcons`)을 참조하고, 데이터 조회는 TableManager 경유 테이블 값이 우선(`AGENTS.md`).
4. **설명·색 문구 단일 출처**: 개별 UI가 자기 색과 문구를 따로 가지지 않음 — 설명의 원본은 테이블의 `description`.

## B. 화면 구성 요소 인벤토리

| 스크립트 | 역할 요약 |
|---|---|
| `PauseMenu` (`PanelBase`) | 일시정지 메뉴 조작 (Esc 계열). 구독자 예외 처리 대칭 OnEnable/OnDisable으로 PanelVisibility 발행 |
| `PuzzleInventoryView` | 좌측 기차 보드: 5×5 화물칸 격자(`boardCells`)와 사전 배치 슬롯 풀 `placementSlots`/`ghostSlots`/`outlineSlots`; 인접 시너지 화살표(`synergyArrows`, 증가/감소 구분 스프라이트); 아이템 상세·호버용 아이콘 키셋 `PuzzleInventoryUI.itemIcons`. 모델 조회 후 이동 가이드(`moveGuide`, 근거리 N시간·소요 시간 형식), 보드 제목/실제 가치 텍스트 갱신 담당 |
| `PuzzleInventoryPresenter` | 위 보드의 표시 갱신 Presenter. 패널 열림/닫힘 시 대기 코루틴 중단 등 정리(`Active=false` 경로 포함), OnEnable/OnDisable 대칭 구독 |
| `PuzzleItemTooltipView` (`PanelBase`) | Cargo 화물 호버 팝업: 기본·실제 가치, 적용 배율·정확한 거래 예상 금액, 현재 적용/가능 인접 효과, 소요 시간 기준 미래 가치(`futureValue*Text`). 폰트 스타일은 툴팁 프리팹이 소유하며 런타임 상하한 보정은 사용하지 않는다. 거래 예상은 실제 거래 모델과 같은 계산을 사용한다. 닫히면 열린 상태 정리 |
| `PuzzleCityView` | 현재 마을의 시장 보드: 재고 고정집산품·수입품·랜덤, 태그 선호·전 품목 구매·판매 배율 표시 |
| `PuzzleTrainBarView` | 하단 화물칸 바: WagonSlotUI 프리팹 슬롯 풀(button/Image/highlightBorder/numberText) 선택·증차 용도; 드래시 호버 배율(`wagonDragHoverScale`)로 색 보정 — 코드에서만 색·배율 변경 |
| `PuzzleSlotVisual` | 미리 배치된 HUD 슬롯 비주얼 — 시각·색·표시 타이밍을 코드가 제어, Instantiate 금지 원칙 준수 대상 |
| `PuzzleTradeNotebookView / PuzzleTradeNotebookPresenter` | 우측 접이식 대화 로그. 게임 날짜·시각 - 들은 마을 - 소문 내용 순으로 기록하고 새 기록은 맨 아래로 스크롤. PanelBase와 사전 배치 ScrollRect를 사용하며 지도/역 화면이 같은 로그를 공유 |
| `PuzzleSynergyChartView / PuzzleSynergyChartPresenter` | 하단 상태 바 오른쪽 `상성표` 버튼으로 여닫는 시너지 상성표. 대상 태그 × 이웃 태그 칸에 면당 효과 %, 제외 이웃 각주, 화물별 태그 배지를 표시한다. 프리팹 `PuzzleSynergyChart`가 배치 원본이고 Presenter는 시너지 표에서 문구·색만 채운다 |
| `PuzzleMapView` | 지역 철도 노선도 화면(어두운 배경, 생산 5개 지역·수도권 6색·일반 회색·연결역 이중 링·선 위 숫자는 소비 시간). 줌에 따라 이름·마커·현재 위치 배지·시간 라벨·굵기를 역배율로 보정; 겹치는 이름·시간 라벨은 우선순위별 생략; 85% 미만에서는 시간 라벨 숨김. 확대·축소·드래그·호버·도착 시 `UpdateMapLayout` 재배열 |
| `PuzzleMapNavigation` | 지도 줌 레벨 상수(`ui.PuzzleMapNavigation.*`) 보유 |
| `PuzzleMapNodeView` | 원형 노드 표식·현재 역 하이라이팅(_current 여부·planningTargetCityId 비교로 강조색 결정)·라벨 위치 프리팹 값(`ui.PuzzleMapNodeView.*`)으로 노드·환승 표기 |
| `PuzzleMapPresenter` | 지도 열림·마을 정보·이동 확인/연출과 모델 이동을 중계 |
| `PuzzleMapTravelView` | 확인창+이동 연출(확인 카드·이동 카드·기차·진행 막대), MapPanel 아래 `MapTravelPanel`. 취소 선택 기본값이며 `UI/Cancel` 액션도 취소로 반영. 진행률 코루틴은 비활성화 시 중단; 이동 확정 전 중단은 도시·시간·자금 유지; 확인 중·연출 중에는 중복 이동과 지도 조작 차단; 지도 열림 중 배경 드래그·즉시 거래 차단; 도착 표시 후 지도 닫기 |
| `PuzzleStationActionsView / PuzzleStationActionsPresenter` | 역 행동 패널(빌더가 `startOpen = true`로 열어 놓음) |

## C. 핵심 UI 이벤트 흐름

```
[화물 칸 선택]            -> bottom TrainBar의 WagonSlotUI 버튼 클릭 → 선택 표시만 갱신(색상/배율은 코드 담당)
    [시장 거래]            -> Ctrl+좌클릭 즉시거래 = 현재 회전으로 왼쪽 위부터 빈자리 찾기 없으면 다음 회전 순차 검사
                            같은 보드 내 이동은 무료, 공간/자금 조건 불충족이면 물건·자금 보존
    [노드 연결 확인]       -> 연결된 역 클릭 → 시간·비용 조건 검증(EvaluateTravel 기반: HasRoute+방문 여부+상환 예정지 비교) →
                              Pass하면 목적지·소요 시간·요금 확인창("이동하시겠습니까?" 확인/취소)
    [이동 확정 경로]       -> Confirm: 이동 연출 → 완료 시 이동 1회 확정 + 도착 표시 + 지도 닫힘 → TimeAdvanced 발생 →
                              실패면 도시·시간·화물·시장·자금 보존 후 지도에 사유 표시
    [완료 패널]           -> 시간 정지 + 인벤토리 입력 비활성화
    [ESC 경로]           -> 이동 확인창 취소 또는 드래그 취소 → 도시·시간·자금 변화 없음
    [일시정지 경로]       -> PauseMenu 열기/닫기 시 패널 Visibility 이벤트가 입력 쪽 구독자에게 흘림
```

이동 성공 순서(계약대로): 목적지·시장·자금을 먼저 확정 → 경과 시간과 기한 패배 판정 → `TimeAdvanced` → `CityArrived`. 실패한 이동에는 시간이 흐르지 않음.

## D. 테스트 버전 변형 지점 체크리스트

- 패널 시작 상태(`startOpen`) 오버라이드 가능성: 빌더 설정 도구들(Build Setup 계열)에서 각 패널의 직렬화된 `startOpen` 필드 값을 별도 보관함(`StationActionsSetupTool` 등은 true로 세팅됨). 다른 버전 테스트에서 어떤 패널을 처음부터 열지 결정하는 변수.
- 비활성화 복귀 정책: `OnDisable`는 열려 있으면 상태를 닫고 `OnShow` 초기화 루틴 없이 재점성 시 꺼진 상태로 시작. 비활성화된 뒤 다시 켜졌을 때 상태가 꼬이지 않는지 확인 항목.
- `TelemetryRecorder` 구독 범위: Run/Scene/Upgrade/Panel/Trade 다섯 개 이벤트만 기록하므로 HUD 버전 차이도 이 중 해당 이벤트로만 대조해야 함(물건 보유 시간은 Debug.Log로만 남고 분석 사건 로그 계약에 이동·시간이 정의되어 있지 않다는 점도 같이).
- 제출/취소 우선순위: UI 모듈의 Submit/Cancel 액션이 게임 입력보다 우선이므로, 여러 버전을 비교할 때 UI 입력과 게임 내 조작이 겹칠 때 어떤 쪽이 적용되는지도 확인.
- 줌 구간 경계: 85% 임계값에서 시간 라벨이 숨겨지는 동작, 겹친 시간 라벨 재배치, 확대 시 글자 크기 보정(화면 크기 보정과 역배율 보정 두 가지 모두 존재) — 여러 레이아웃 버전에서 겹침 결과를 눈으로 비교할 항목.
- 하드코딩 문자열 주의: 이전 초안에서 하드코딩되었던 "8시간"류 문자열은 산출값 기반으로 교체되었으므로 여러 버전이 서로 동일한 문안을 가지는지 확인(기획서 §8 참고).

## E. 미정(확인 필요) 사항

- PauseMenu/City/Travel의 각 `startOpen` 초기값을 빌드 도구가 어느 버전으로 유지하고 있는지: 이번 문서 작업에서 개별 확인이 안 된 항목은 플레이 실행 전까지 미확정으로 남김.
- 신선도는 Food 태그의 저장된 내구도 %를 표시한다. 현재 잠정 규칙은 시작 100·감소 없음이며 화면 변경만으로 감소 규칙을 추가하지 않는다.