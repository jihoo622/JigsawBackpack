# 게임 UI 배치

> **개편 레이아웃 기본 적용(S7, 2026-11-19):** `RelayoutSettings.Enabled`(`ui.relayout.enabled`) 기본값이 `1`(신규 개편 레이아웃)이다. 일시정지 메뉴(`PauseMenu`)의 개발용 토글로 구형 2열 레이아웃(`GameplayLayout`)으로 되돌릴 수 있으며, 구형 UI 오브젝트/코드 삭제는 별도 후속 작업이다. 설계 및 결정 내역은 [전체 화면 배치 개편 계획](../Research/UI_RELAYOUT_PLAN.md)과 [시안 3b](../Research/relayout/concept-3b.html)를 따른다.

UI 배치 원본은 프리팹(`Assets/Art/UI/Relayout/` 및 `Assets/Art/UI/Scene/`)이며 런타임에 UI 오브젝트와 Layout Group을 동적 생성하지 않는다. CanvasScaler는 1920×1080 기준 Expand 모드를 유지한다.

## 0. 캔버스 계층 및 Z-Order

| 캔버스 / 루트 | 프리팹 | 역할 |
|---|---|---|
| `WorldSceneCanvas` | `Assets/Art/UI/Scene/GameCanvas.prefab` | 좌측 승강장 배경(`PlatformScene`) + 우측 다이제틱 마을 6건물(`TownScene`) |
| `FixedFrameCanvas` | `Assets/Art/UI/Scene/GameCanvas.prefab` | 상단 정보 바(`TopHUD`, 56px) · 좌측 도구줄(`QuickToolbar`) · 상태 배너(`StatusBanner`) · 중앙 세로 선로(`RailStrip`) |
| `FloatingWindowCanvas` | `Assets/Art/UI/Scene/GameCanvas.prefab` | 고정 크기 자유 이동 부동 창(`FloatingWindow`) 및 `TravelBanner` |
| `PuzzleTooltip` / `PauseCanvas` | `Assets/Art/UI/Scene/GameUI.prefab` | H16 공용 툴팁 · 일시정지 모달(`PauseMenu`) · 결산 모달(`RunEndModal`) |

## 1. 고정 프레임 영역 (1920×1080 좌상단 원점)

| 영역 | 프리팹 / 컴포넌트 | 위치·크기 | 설명 |
|---|---|---|---|
| **TopHUD** | `TopHudView` | 상단 전체 폭 × 56px | 현재 도시·남은 기한·시계/낮밤 정산 막대·평판·보유 자금·목표/결산·일시정지 버튼 |
| **QuickToolbar** | `QuickToolbarView` | 좌측 상단(`TopHUD` 아래) | `수첩(N)` · `노선도(M)` · `렌즈(L)` · `배경만 보기` · `창 배치 초기화` · 건물 단축키 `1~6` |
| **StatusBanner** | `StatusBannerView` | 중앙 상단(`TopHUD` 아래) | 거래 결과·호버 사유·의뢰 수락/거절·이동 차단 등 단문 상태 배너 |
| **RailStrip** | `RailStrip.prefab` (`RailStripView`) | `x=549, y=82, w=91, h=998` | 가상화 14슬롯 풀(`RailWagonCarView`, R8 7×6 미니 격자), 드래그 순서 변경(H20)·구매 삽입선·역사 드롭 판매(H17) |
| **PlatformScene** | `PlatformScene.prefab` (`PlatformSceneView`) | 좌측(`RailStrip` 왼쪽) | 열차 배경(임시 아트) |
| **TownScene** | `TownScene.prefab` (`TownSceneView`) | 우측(`RailStrip` 오른쪽) | 마을 건물 6종(`시장(1)`, `술집(2)`, `여관(3)`, `역사(4)`, `생산시설(5)`, `시청(6)`) + 건물별 1줄 현황 |

### 마을 건물별 1줄 현황 (`TownSceneView` 이벤트 갱신)

- **시장 (1)**: 낮 `'물가 n% · 선호 m종'` / 밤 `'영업 종료(밤)'` + 디밍
- **술집 (2)**: `'새 소문 · 의뢰 n건'` / 현재 도시 완료 가능 의뢰 보유 시 `'완료 가능 의뢰 n건'` + 테두리 강조
- **여관 (3)**: `'휴식 1~4턴'`
- **역사 (4)**: `'화차 n/최대 · 출발'`
- **생산시설 (5)**: 작업장 보유 시 `'{작업장명} · {수수료}$'`(또는 작업장명) / 미보유 시 공터 디밍(호버 시 사유 표시)
- **시청 (6)**: `'평판 n · 목표 진행 x%'`

## 2. 부동 창(`FloatingWindow`) 및 배너 규격

모든 부동 창은 H21 정책에 따라 **고정 크기(리사이즈 핸들 없음)**이며, `PlayerPrefs`(`ui.win.<windowId>.x/y/open`)에 위치·열림 상태만 영속화한다.

| 창 ID (`windowId`) | 프리팹 / 뷰 | 기본 위치·크기 `(x, y, w, h)` | 주요 기능 |
|---|---|---|---|
| `train_0` ~ `train_15` | `TrainWindow.prefab` (`TrainWindowManager` / `TrainWindowView`) | `(16, 134, 440, 430)` 기준 비겹침 배치 / 이후 24px 계단 | 화차별 7×5 동적 보드(40~72px), 2줄 렌즈 요약, `렌즈` 토글, `ui.maxTrainWindows`(기본 16) LRU 관리 |
| `notebook` | `NotebookWindow.prefab` (`NotebookWindowView`) | `(16, 128, 522, 928)` | 5탭 통합(`지도` / `소문` / `의뢰` / `숙련` / `상성표`), `PuzzleWorldMap`·검색·경로계획·`MapDestinationPinView` 16개 풀 |
| `market` | `MarketWindow.prefab` (`MarketWindowView`) | `(1136, 96, 764, 640)` | 상단 `CityTradePreferencesView` 시세 띠 + 13×9 보드(56px) + 구매/판매가 라벨, 도착 시 자동 오픈(야간 차단) |
| `lens` | `LensWindow.prefab` (`LensWindowView`) | `(26, 134, 510, 366)` | `현재` / `방문 도시` / `소문 도시` 탭별 행선지 판매 견적·공개 규칙 준수 |
| `tavern` | `TavernWindow.prefab` (`TavernWindowView`) | `(660, 120, 620, 520)` | 무료 소문 듣기 + 9×5 의뢰 게시판(조각 드래그로 화차 수락, 목적지 드롭 완료, 초과 건수 안내) |
| `rest` | `RestWindow.prefab` (`RestWindowView`) | `(700, 180, 440, 310)` | 1~4턴 휴식 조정(`-`/`+`) · `휴식` · `아침까지 대기` · 결산 미리보기(이동 연출 중 비활성화) |
| `station_hall` | `StationHallWindow.prefab` (`StationHallWindowView`) | `(660, 110, 560, 560)` | 화차 상품(드래그로 선로 삽입 구매) · 판매 드롭 구역(선로 화차 드래그 판매) · 출발 버튼(수첩 지도 탭) |
| `workshop` | `WorkshopWindow.prefab` (`WorkshopWindowView`) | `(660, 120, 560, 480)` | 단일 창(R17): 도시 작업장 종류(도축/개조/정제)에 따라 보드·모드·확인 버튼 전환(이동 연출 중 비활성화) |
| `city_hall` | `CityHallWindow.prefab` (`CityHallWindowView`) | `(700, 140, 520, 400)` | 읽기 전용 도시 물가·선호/풍부/희귀·산업·평판·결산 목표 진행 표시 |
| `TravelBanner` | `TravelBanner.prefab` (`TravelBannerView`) | 상단 중앙 `(0, -92, 640, 148)` | 지도 이동 확인(운임·소요 턴) 및 이동 진행 막대 표시 |
| `RunEndModal` | `RunEndModal.prefab` (`RunEndView`/`Presenter`) | 화면 중앙 모달 | 승리/패배 결산 결과, 무한 모드 계속 / 재시작 |

## 3. 에디터 빌더 및 메뉴 항목

모든 개편 프리팹은 멱등 빌더(`Assets/Scripts/Editor/UI/Relayout/`)로 생성·갱신한다.

| 단계 | 스크립트 | Unity 메뉴 | 생성/갱신 대상 |
|---|---|---|---|
| **S1** | `RelayoutS1Builder.cs` | `Tools/MyJG/UI/Relayout/S1 Build Window System` | `UIPalette.asset`, `RunEndModal.prefab`, `PauseMenu` 창 배치 초기화 |
| **S2** | `RelayoutS2Builder.cs` | `Tools/MyJG/UI/Relayout/S2 Build Fixed Frame` / `Toggle Relayout` | `TopHUD`, `QuickToolbar`, `StatusBanner`, `PlatformScene`, `TownScene` |
| **S3/S3b** | `RelayoutS3Builder.cs` | `Tools/MyJG/UI/Relayout/S3 Build Rail And Train` | `RailStrip.prefab`, `TrainWindow.prefab` |
| **S4** | `RelayoutS4Builder.cs` | `Tools/MyJG/UI/Relayout/S4 Build Notebook Window` | `NotebookWindow.prefab` |
| **S5** | `RelayoutS5Builder.cs` | `Tools/MyJG/UI/Relayout/S5 Build Market And Lens` | `MarketWindow.prefab`, `LensWindow.prefab`, `QuickToolbar` 렌즈 버튼 |
| **S6** | `RelayoutS6Builder.cs` | `Tools/MyJG/UI/Relayout/S6 Build Town Windows` | `TavernWindow`, `RestWindow`, `StationHallWindow`, `WorkshopWindow`, `CityHallWindow`, `TravelBanner` 및 `GameCanvas.prefab` 배선 |
