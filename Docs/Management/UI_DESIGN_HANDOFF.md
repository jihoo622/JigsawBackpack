# UI 디자인 아웃소싱용 정리 — 씬 플로우 및 화면 구성

> 작성 목적: UI 디자인 및 아트 적용 시 참고하는 게임의 씬 흐름, 개편 레이아웃 구조, 화면별 기능과 제작 규약을 정리한다.
> 규칙·수치의 원본은 [게임 기획서](../게임 기획서.md), 프리팹 실측 좌표는 [UI 배치](UI_LAYOUT.md)가 원본이다.

## 1. 게임 개요

| 항목 | 내용 |
|---|---|
| 게임 명 | Trainventory (저장소명 JigsawBackpack) |
| 장르 | 기차 화물 인벤토리 관리 + 지역 거래 게임 |
| 목표 | 금융마을에서 출발해 14일(56턴) 동안 교역하고 결산 잔액을 목표(1,500$) 이상으로 만들기 |
| 시작 조건 | 시작 자금 200$, 출발지 금융마을(`city.finance`) |
| 플랫폼 입력 | 마우스·키보드 (드래그&드롭 중심, 단축키 `N/M/L`, `1~6`, `Esc`) |
| 기준 해상도 | 1920×1080, CanvasScaler Expand(16:9) |

## 2. 씬 플로우

`Preload → Title → Game` 순서로 진입한다.
- `Title`: 게임 시작·종료 임시 화면(`Assets/Art/UI/Title/TitleScreen.prefab`).
- `Game`: 본플레이. 승리·패배 모두 `RunEndModal`로 이어지며, 무한 모드 계속 또는 재시작을 제공한다.

## 3. 개편 화면 레이아웃 (3계층 구조)

```
┌─────────────────────────── TopHUD (56px, 상단 고정) ───────────────────────────┐
├──────────┬──────────────────────────┬──────┬───────────────────────────────────┤
│          │                          │      │  StatusBanner (상단 상태 배너)    │
│ Quick    │   PlatformScene (좌측)   │ Rail │                                   │
│ Toolbar  │   (승강장 배경)          │ Strip│  TownScene (우측 다이제틱 마을)    │
│ (좌측)   │                          │ (91) │   · 시청(6)        시장(1)        │
│          │  [부동 창 레이어]         │      │   · 역사(4)        생산시설(5)    │
│          │   · TrainWindow (다중)   │      │   · 술집(2)        여관(3)        │
│          │   · NotebookWindow (5탭) │      │  (건물별 1줄 현황 + 부동 창)      │
│          │   · LensWindow           │      │                                   │
└──────────┴──────────────────────────┴──────┴───────────────────────────────────┘
```

- **WorldSceneCanvas**: 좌측 `PlatformScene`(열차/승강장) + 우측 `TownScene`(6개 건물과 1줄 현황).
- **FixedFrameCanvas**: `TopHUD` · `QuickToolbar` · `StatusBanner` · `RailStrip`(중앙 세로 선로 14슬롯 가상화 풀, 7×6 미니 격자).
- **FloatingWindowCanvas**: 고정 크기 자유 이동 창(`TrainWindow`, `NotebookWindow`, `MarketWindow`, `LensWindow`, `TavernWindow`, `RestWindow`, `StationHallWindow`, `WorkshopWindow`, `CityHallWindow`) + `TravelBanner`.

## 4. 화면별 기능 명세

- **TopHUD / QuickToolbar / StatusBanner**: 현재 도시·기한·시간/정산 막대·평판·자금·목표 표시, `수첩(N)`/`노선도(M)`/`렌즈(L)`/`배경만 보기`/`창 배치 초기화` 제공.
- **TrainWindow (다중 창)**: 화차별 7×5 보드(56px 셀), 2줄 렌즈 Δ 요약, `렌즈` 버튼. 선로(`RailStrip`)에서 화차 클릭 시 창 열기/포커스, 드래그로 편성 순서 변경.
- **MarketWindow**: 13×9 보드(56px 셀) + 상단 시세 띠 + 구매/판매가 라벨. 도착 시 자동 오픈, 야간에는 열림 차단.
- **NotebookWindow (5탭)**: `지도`(줌/패닝/검색/경로 계획/핀 16개) · `소문` · `의뢰` · `숙련` · `상성표`.
- **LensWindow**: 잡은/선택한 화물의 행선지별 판매 견적(`현재` / `방문 도시` / `소문 도시`).
- **마을 창 5종**:
  - **TavernWindow (술집)**: 무료 소문 듣기 + 9×5 의뢰 게시판(화물 조각 드래그로 위치 지정 수락, 목적지 게시판 드롭으로 완료).
  - **RestWindow (여관)**: 1~4턴 휴식, 아침까지 대기, 결산 미리보기.
  - **StationHallWindow (역사)**: 화차 상품 드래그 → 선로 삽입 구매, 선로 화차 → 판매 구역/역사 건물 드롭 판매, 출발 버튼.
  - **WorkshopWindow (생산시설)**: 도시 작업장(도축/개조/정제)에 따라 보드·모드·확인 버튼 전환(공터 도시는 비활성/디밍).
  - **CityHallWindow (시청)**: 읽기 전용 도시 물가·선호·산업·평판·목표 진행.
- **TravelBanner**: 지도에서 역 선택 시 상단 중앙에 출발 확인(운임·소요 턴)과 이동 진행 막대 표시.

## 5. 디자인 제작 규약

1. **사전 배치 원칙**: 모든 UI는 `Assets/Art/UI/` 프리팹에 사전 배치하며 런타임 생성·동적 LayoutGroup 사용 금지.
2. **평면 프리미티브 & 팔레트**: H16 규칙에 따라 단색 배경 + 얇은 테두리(`UIPalette.asset`)를 기본으로 하고, `NotoSansKR-Medium SDF`(TMP)만 사용.
3. **고정 크기 부동 창**: H21에 따라 부동 창 리사이즈 핸들을 두지 않으며, 위치·열림 상태만 저장한다.
4. **현재 보류/임시 사항**: `UIPalette.asset`은 팔레트 A 임시 적용 상태이며, `PlatformScene`/`TownScene` 배경·건물 아트는 임시 플레이스홀더 사각형이다.
