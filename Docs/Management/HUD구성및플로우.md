# HUD 구성 요소 및 UI 플로우 정리

> 작성 목적: 개편 레이아웃(`RelayoutSettings.Enabled = true` 기본) 기준 HUD/창 구성 요소, 이벤트 흐름, 입력/차단 규약을 정리한다.
> 게임 규칙 원문은 [게임 기획서](../게임 기획서.md), 프리팹 좌표와 빌더 규격은 [UI_LAYOUT](UI_LAYOUT.md)이 원본이다.

## A. 공통 규약

1. **사전 배치 원칙**: 모든 뷰·슬롯 풀은 프리팹(`Assets/Art/UI/Relayout/`)에 미리 배치하며 런타임에 UI 오브젝트나 LayoutGroup을 생성하지 않는다.
2. **창 영속성**: 모든 자유 창은 `FloatingWindow`를 사용하며 고정 크기(H21)다. `PlayerPrefs`에 위치(`x`, `y`)와 열림(`open`)만 저장한다.
3. **공통 팔레트/스타일**: H16 평면 사각형·테두리 규격과 `UIPalette.asset` + `UIPaletteBinder`를 사용한다. 문구·서식은 `Presentation`/`BasicData`가 단일 출처다.
4. **구형 fallback 유지**: `RelayoutSettings.Enabled = false` 전환 시 기존 `GameplayLayout` 경로로 복원된다(삭제는 별도 후속 작업).

## B. 화면 구성 요소 인벤토리

| 스크립트 | 역할 요약 |
|---|---|
| `RelayoutSettings` | `ui.relayout.enabled` 스위치를 관리(기본값 `1`). 변경 시 `Changed` 이벤트 발행 |
| `RelayoutRootController` | `WorldSceneCanvas`/`FixedFrameCanvas`/`FloatingWindowCanvas`와 구형 UI 루트 전환, 건물 클릭(`OpenStationBuilding`)으로 마을 창 6종 라우팅 |
| `TopHudView` | 상단 56px 고정 바(현재 도시·기한·시계/낮밤 정산 막대·평판·자금·목표/결산·일시정지) |
| `QuickToolbarView` | 좌측 도구줄(`수첩(N)`, `노선도(M)`, `렌즈(L)`, `배경만 보기`, `창 배치 초기화`, `1~6` 안내) |
| `StatusBannerView` | 거래 결과·드래그 거부 사유·호버 안내 등 단문 상태 배너 |
| `PlatformSceneView` / `TownSceneView` / `TownBuildingButton` | 좌측 승강장 배경 + 우측 6개 건물(클릭, `1~6` 단축키, 이벤트 기반 1줄 현황 및 완료 가능 의뢰 테두리 강조, 공터/야간 디밍) |
| `RailStripView` / `RailWagonCarView` | 중앙 세로 선로 14슬롯 가상화 풀, 7×6 미니 격자, 순서 변경 드래그, 화차 상품 구매 삽입선, 역사 드롭 판매 |
| `TrainWindowManager` / `TrainWindowView` | 다중 화차 부동 창(`train_0..15`, LRU 교체, 2줄 렌즈 요약, 7×5 동적 보드) |
| `NotebookWindowView` | 5탭 수첩(`지도/소문/의뢰/숙련/상성표`), `PuzzleWorldMap` 임베드, 핀 풀 16개 |
| `MarketWindowView` | 13×9 시장 보드 + 상단 시세 띠, 구매/판매가 라벨, 도착 시 자동 오픈(야간 차단) |
| `LensWindowView` | 행선지 렌즈 독립 창(`현재/방문 도시/소문 도시` 탭) |
| `TavernWindowView` | 무료 소문 듣기(판 종료 시 차단) + 9×5 의뢰 게시판(조각 드래그 수락·목적지 완료·초과 건수 `외 n건`) |
| `RestWindowView` | 1~4턴 휴식, 아침까지 대기, 결산 미리보기(이동 연출 중 비활성화) |
| `StationHallWindowView` | 화차 상품 드래그 구매 + 선로 화차 판매 드롭 구역 + 노선도 출발 버튼 |
| `WorkshopWindowView` | 단일 생산시설 창(도시의 도축/개조/정제에 따라 보드·모드·확인 버튼 전환, 이동 연출 중 비활성화) |
| `CityHallWindowView` | 읽기 전용 도시 물가·선호/풍부/희귀·산업·평판·목표 진행 표시 |
| `TravelBannerView` | 지도 이동 확인(운임·소요 턴) 및 이동 진행 막대(구형 `PuzzleMapTravelView` 대체) |
| `PuzzleInventoryPresenter` | 기차·시장·의뢰 게시판·작업장 보드 드래그/호버/거래/의뢰 수락·완료/거절 사유 배너 총괄 |
| `PuzzleMapPresenter` | 지도 노드 클릭/경로 계획/자동 이동 및 `IsAnyTravelAnimating` 상태 관리 |

## C. 핵심 UI 이벤트 흐름

```
[도시 도착]            -> CityArrived → TrainWindow + MarketWindow 자동 오픈(야간이면 배너 안내), TownSceneView 6건물 1줄 현황 갱신
[건물 클릭 / 1~6]     -> TownSceneView → RelayoutRootController.OpenStationBuilding → 각 부동 창(Market/Tavern/Rest/StationHall/Workshop/CityHall) 오픈
[시장 거래]            -> MarketWindow ↔ TrainWindow 드래그 또는 Ctrl+좌클릭 → PuzzleInventoryService.Trade → FundsChanged/StatusBanner
[의뢰 수락/완료]       -> TavernWindow 9×5 게시판 ↔ TrainWindow 드래그(TryAcceptQuestAt / TryCompleteQuestWithCargo)
                         목적지 도착만으로는 자동 완료되지 않으며, 마지막 턴 Settle() 직전에만 도착 의뢰 자동 완료(R26)
[화차 구매/판매]       -> StationHallWindow 화차 카드 → RailStrip 드롭(TryDropPurchasedWagonAtScreenPosition)
                         RailStrip 화차 → StationHallWindow/역사 건물 드롭(TrySellWagon)
[이동 확인/연출]       -> NotebookWindow 지도 탭 역 클릭 → TravelBanner 확인 → IsAnyTravelAnimating=true(휴식/작업장 차단) → Travel 확정 → CityArrived
[일시정지 / Esc]       -> PauseMenu (일시정지, 창 배치 초기화, Relayout 전환 토글)
```
