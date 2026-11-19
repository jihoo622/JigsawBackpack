# 전체 화면 배치 개편 계획 — develop + 행선지 렌즈 기준

작성: 2026-11-19 · 브랜치 `feature/ui-relayout` (기반 `feature/route-lens` fe04d00)

## 배경

행선지 렌즈(L안)를 구현한 뒤 플레이 화면을 비교해 보니 기존 화면과 차이가 거의 보이지 않았다.
- 평상시 화면은 develop과 같다(D1 현행 좌표 유지).
- 화물을 잡았을 때도 보드 아래 글자 패널과 작은 후보 표식뿐이다.
- 1순위 외곽선이 칸 윗변만 그려지고, Δ 0 후보도 찍히는 결함이 있다.

사용자는 화면 배치 자체를 개편하기로 했다.

## 사용자 결정(2026-11-19)

| 번호 | 항목 | 결정 |
|---|---|---|
| H1 | 기본 방향 | C안(통합 작업 공간)을 출발점으로 새 시안 2~3개를 그려 비교한 뒤 하나를 고른다 |
| H2 | 범위 | 지도·수첩을 포함한 게임 화면 전체 |
| H3 | 보드 칸 크기 | 시안마다 적절히 정한다. 화물 그림을 알아볼 수 없을 만큼 작아지는 안은 버린다 |
| H4 | 확인 시점 | 시안만 사용자가 확인한다. 고른 뒤에는 멈추지 않고 구현·검증까지 진행한다 |
| — | 이전 방침 유지 | 작업은 Hydrogen(agy)·하이쿠 위주, 검증은 비전 기능이 있는 다른 모델, 테스트는 마지막에, 단계별 커밋, 푸시 안 함 |

### 시안 선택(2026-11-19 사용자 확정)

| 번호 | 항목 | 결정 |
|---|---|---|
| H5 | 채택 시안 | 시안 3의 창 팝업 방식은 유지하고, 배경을 지도에서 장면으로 바꾼다. 왼쪽은 기차길·플랫폼, 가운데 좁은 띠는 기차길과 화차 요약, 오른쪽은 위에서 본 마을(시장·술집·생산시설·시청/마을회관·역사·여관). 사용자 스케치: `.orca/analysis/relayout/sketch-3b.png` |
| H6 | 지도 | 수첩의 탭으로 옮긴다 |
| H7 | 가운데 띠와 화차 창 | 가운데 띠의 기차길에 내 기차(기관차와 화차들)가 정차해 있다. 화차를 누르면 그 화차의 인벤토리 창이 화면 오른쪽 아래에 열린다 |
| H8 | 왼쪽 기차길·플랫폼 | 말 그대로 배경 그림이다. 팝업과 정보창이 그 위에 뜬다. 기차는 가운데 띠에만 그린다 |
| H10 | 창 배치 원칙(H7 정정) | 마을 관련 창(시장·술집·여관·생산시설·역사·시청)은 오른쪽 마을 위, 개인 관련 창(화차 인벤토리·렌즈 정보·수첩과 지도·툴팁)은 왼쪽 플랫폼 위에 뜬다. 가운데 선로의 기차는 가리지 않는다 |
| H11 | 선로 스크롤 | 가운데 선로는 세로로 스크롤되어 화차가 8량을 넘어도 처리한다(최대 개수를 가정하지 않는다) |
| H12 | 화차 미니 격자 | 선로의 각 화차 위에 인벤토리 상태를 미리보기 격자로 표시한다. 빈칸은 회색, 화물 칸은 노란색 |
| H13 | 창 크기·위치 | 모든 UI 창을 크기 조정하고 자유롭게 옮길 수 있게 한다 |
| H14 | 레이아웃 확정 | 시안 3b(H5~H13 반영)로 확정. 그 밖의 레이아웃 자료는 `Docs/Legacy/`로 격리한다 |
| H9 | 오른쪽 마을 | 다이제틱 UI. 건물을 눌러 각 기능과 메뉴로 이동하며, 따로 된 역 행동 목록·탭은 없다. 상태는 간판·칠판·창문 불빛 같은 세계 안 표시로 보인다 |

## 임의 결정 기록

| 번호 | 결정 | 이유 |
|---|---|---|
| R1 | 새 브랜치 `feature/ui-relayout`를 `feature/route-lens` 위에서 딴다 | 렌즈 계산 계층과 프리팹 빌더를 개편안에서 그대로 재사용한다 |
| R3 | 시안용 화물 그림은 256px로 줄여 `relayout/assets/`에 둔다(원본 22MB → 0.9MB) | 문서 미리보기용이라 원본 해상도가 필요 없고 저장소가 불어나지 않게 한다 |
| R4 | 건물별 창: 시장=거래, 술집=소문+의뢰, 여관=휴식(아침까지 대기 포함), 생산시설=도시별 작업대(없으면 빈 터), 역사=화차 시설·출발, 시청=도시 정보·평판·결산 진행(새 규칙 없음) | 현재 기능을 그대로 옮기고 시청에는 흩어진 도시 정보를 모은다 |
| R5 | 도착 직후에는 기차 창과 시장 창이 열려 있고, 둘 다 닫으면 배경 전체가 보인다 | 핵심 루프인 거래로 바로 들어가게 한다 |
| R6 | 화물을 잡은 채로는 수첩을 열 수 없으므로, 수첩 지도 탭의 행선지 핀은 마지막으로 선택한 화물을 기준으로 띄운다 | H6과 렌즈 L3를 함께 살린다 |
| R7 | 마을·플랫폼 배경은 우선 도형과 아이콘으로 된 임시 그림으로 구현하고, 본 그림은 나중에 스프라이트만 바꾼다 | 프로젝트에 해당 그림이 없다. 배치와 기능을 먼저 완성한다 |
| R8 | 미니 격자는 화차 보드와 같은 7×5 방향으로 그리고, 막힌 칸은 배경에 가까운 어두운색, 인접한 서로 다른 화물은 노란색 두 단계로 구분한다. 칸은 약 9~10px | 보드와 방향이 같아야 한눈에 대응되고, 같은 노란색만 쓰면 화물 경계가 사라진다 |
| R9 | H13 적용 범위는 팝업·정보 창 전부(화차·렌즈·수첩·시장·술집·여관·작업대·역사·시청·일시정지·결산). 상단 정보 바, 도구줄, 가운데 선로, 플랫폼·마을 배경은 고정 틀로 둔다. 툴팁은 자동 위치 | 배경과 틀까지 움직이면 다이제틱 마을과 선로의 의미가 사라진다 |
| R10 | 창 조작 규칙: 제목줄로 끌기, 가장자리·오른쪽 아래 모서리로 크기 조정(창마다 최소·최대 크기), 화면 밖과 상단 정보 바는 침범 불가, 누르면 맨 앞으로, 12px 안에서 화면 가장자리·다른 창에 붙음 | 자유 배치하면서도 창을 잃어버리거나 정보 바를 가리지 않게 한다 |
| R11 | 창 안의 보드(화차 7×5, 시장 13×9, 작업대)는 창 크기에 맞춰 칸 크기를 40~72px 사이에서 다시 계산하고, 최소보다 작아지면 스크롤한다 | 크기 조정의 의미가 보드 크기 조정이어야 하고, 화물 그림이 읽히는 하한을 지킨다 |
| R12 | 창 위치·크기는 창마다 저장(PlayerPrefs)하고, 도구줄과 일시정지 메뉴에 "창 배치 초기화"를 둔다. 기본 배치는 H10 | 사용자가 만든 배치를 유지하되 언제든 되돌릴 수 있게 한다 |
| R13 | 격리 범위: 옛 UI 조사·다른 레이아웃 안·L안 개편안·렌즈 UI 제안과 목업·시안 1~3을 `Docs/Legacy/UI/`로 옮긴다. 렌즈 구현 계획은 계산 계층과 규칙 비교가 유효하고 코드 주석이 가리키므로 제자리에 두고 대체 범위를 머리에 적는다. `Management`의 UI_LAYOUT·HUD구성및플로우·UI_DESIGN_HANDOFF는 개편 전 현재 구현 기록이라 제자리에 두고 머리에 개편 확정 안내를 단다. 현재 화면 목록(UI_SCREEN_INVENTORY)은 구현 시 대응표의 원본이라 남긴다 | 다른 안이 기준 자료로 잘못 인용되지 않게 하면서, 아직 유효한 규칙·현재 구현 기록은 잃지 않는다 |
| R14 | O1: 화차 창 아래에는 잡은 화물의 견적·잔액 변화·정산 Δ 두 줄 요약을 두고, 행선지 비교(현재·방문·소문 탭)는 별도 렌즈 창으로 분리한다. 렌즈 창은 도구줄과 화차 창 버튼으로 연다 | 드래그 중 판단에 필요한 숫자는 보드 바로 곁에, 비교는 넓은 창에서 본다. 정보가 두 곳에 중복되지 않는다 |
| R15 | O2: 화차 창은 하나이고, 선로의 화차를 누르면 그 창의 내용이 바뀐다 | 기존 단일 `trainView` 구조와 맞고, 창이 여러 개 겹쳐 선로를 가리지 않는다 |
| R16 | O3: 선로 화차 칸은 개수 상한이 있는 고정 풀이 아니라 보이는 칸 수 + 여유 2칸을 미리 배치하고 스크롤 위치에 따라 데이터를 다시 묶는 가상 목록으로 만든다 | H11(최대 개수를 가정하지 않음)과 런타임 UI 생성 금지를 함께 지킨다 |
| R17 | O4: 작업대는 창 하나에 보드 1개와 모드 영역을 두고, 역의 작업대 종류에 따라 내용을 바꾼다 | 한 역에는 작업대가 최대 하나다 |
| R18 | O5: "배경만 보기"는 열린 창만 잠시 숨기고(열림 상태 기억 후 복원) 상단 정보 바·도구줄·선로는 그대로 둔다 | 배경을 보려는 목적에 맞고 조작 수단을 잃지 않는다 |
| R19 | O6: 글자 입력 칸에 포커스가 있으면 단축키 전체를 막는다(기존 `GameEvents.TextInputFocusChanged` 구독) | 지도 검색 중 숫자·문자가 창을 여닫지 않게 한다 |
| R20 | 창 테두리·제목줄 등 새 UI 색은 색 정의 에셋 하나에서 읽는다. 팔레트 확정 전에는 현행 색(후보 A)을 넣어 두고, 확정되면 에셋 값만 바꾼다 | 팔레트 결정과 구현을 병행한다 |
| R2 | 시안 작성 전에 현재 화면을 전부 캡처하고 요소 목록(`UI_SCREEN_INVENTORY.md`)을 만든다 | 범위가 화면 전체라 빠뜨리는 기능이 없어야 한다 |

## 진행 단계

1. 현재 화면 전수 캡처·UI 목록 — 완료(`UI_SCREEN_INVENTORY.md`, 21장, 막힌 화면 없음)
2. 시안 3개(1920×1080, 게임 색·글꼴 재현) — 완성 후 시안 3을 고친 3b로 확정(H14). 시안 1~3은 `Docs/Legacy/UI/relayout/`로 격리

   | 시안 | 칸 크기 | 배치 | 작업자 메모 |
   |---|---|---|---|
   | 1 나란히 거래대 | 기차·시장 64px | 행동 레일 96 · 기차 열 480 · 시장/역 행동 884 · 판단 패널 388 | 가로 여백 0px, 패널 안쪽 여백 고정 필요 |
   | 2 위아래 열차 편성 | 기차·시장 48px | 위 시장 진열대, 아래 화차 3량 가로 배치, 오른쪽 탭 열(역 메뉴·렌즈·수첩·지도) | 4량 이상은 가로 스크롤 또는 38px 압축 보기 |
   | 3 지도 중심 허브 | 기차·시장 56px | 노선도 상시 배경, 왼쪽 아래 기차 독(16,616 520×448), 오른쪽 5탭 역 독(1144,76 760×988) | 렌즈를 지도 위 핀으로 표시. 월드→화면 좌표 동기화와 독 접힘 시 줌 보정 필요. 타이틀 화면은 범위 밖 |

### 캡처 작업에서 확인된 현재 문제(시안이 해결해야 할 것)

- 시장 화물에 가격이 표시되지 않고, 자금·시세·가치를 보려면 시선이 세 방향으로 흩어진다.
- 수첩 상성표 탭에서 뒤쪽 대화 기록 글자가 표 뒤로 비쳐 겹친다(버그).
- 지도의 경로 계획·역 툴팁이 오른쪽 역 노드를 가리고, 하단 도시 정보와 내용이 겹친다.
- 런 종료 화면이 일시정지 창을 재사용해 "배치 후보 표시" 토글이 보이고, 플레이 요약 통계가 없다.
- 행동 목록·휴식·화차 상점·작업대 화면과 기차 열 아래에 빈 공간이 많고, 수첩·지도로 들어가는 경로가 중복된다.
3. 선택안 구현 계획(프리팹 빌더 방식, 단계·커밋 단위)
4. 구현
5. 비전 모델 플레이 검증


---

## 6. 구현 구조 (시안 3b 기준)

> **프로젝트 규칙 준수**: 런타임 UI 오브젝트 동적 생성(`Instantiate` / `LayoutGroup` 동적 추가)을 금지한다. 모든 창·슬롯·표식은 `Assets/Art/UI/` 하위 프리팹에 사전 배치하고 `SetActive`로 토글한다. 글자 크기는 `Docs/Management/UI_LAYOUT.md` §0-1의 6단계(`32/24/20/16/14/12pt`)와 `NotoSansKR-Medium SDF`를 따르며, 에디터 빌더(`RouteLensUIBuilder.cs` 패턴)로 프리팹을 구성·직렬화한다.

### 6-1. 전체 씬/프리팹 계층 (`GameUI.prefab` / `GameCanvas.prefab`)

`Game` 씬에는 `Assets/Art/UI/Scene/GameUI.prefab` 인스턴스 하나만 두며, 아래 순서(sortingOrder / Sibling)로 계층을 구성한다.

```text
GameUI.prefab
├─ UIEventSystem (UIEventSystem.prefab)
├─ WorldSceneCanvas (Canvas, sortingOrder 0) — 고정 배경·다이제틱 마을
│  ├─ PlatformScene (Assets/Art/UI/Relayout/PlatformScene.prefab)      [26, 134, 510, 929]
│  └─ TownScene (Assets/Art/UI/Relayout/TownScene.prefab)              [653, 82, 1247, 977]
├─ FixedFrameCanvas (Canvas, sortingOrder 10) — 고정 HUD·툴바·선로·배너
│  ├─ RailStrip (Assets/Art/UI/Relayout/RailStrip.prefab)              [549, 82, 91, 998]
│  ├─ TopHUD (Assets/Art/UI/City Navigation.prefab 개편)                [16, 12, 1888, 56]
│  ├─ QuickToolbar (Assets/Art/UI/Relayout/QuickToolbar.prefab)        [26, 82, 510, 38]
│  ├─ StatusBanner (Assets/Art/UI/JourneyFooter.prefab 개편)           [660, 90, 460, 34]
│  └─ TravelBanner (Assets/Art/UI/Relayout/TravelBanner.prefab)        [690, 110, 680, 110]
├─ FloatingWindowCanvas (Canvas, sortingOrder 50) — 자유 이동·크기 조절 창 레이어
│  ├─ FloatingWindowManager (컴포넌트: 스냅·클램프·저장·초기화·Esc 스택)
│  ├─ TrainWindow (Assets/Art/UI/Relayout/TrainWindow.prefab)          [26, 516, 510, 544]
│  ├─ LensWindow (Assets/Art/UI/Relayout/LensWindow.prefab)            [26, 134, 510, 366] (기본 닫힘)
│  ├─ NotebookWindow (Assets/Art/UI/Relayout/NotebookWindow.prefab)    [16, 128, 522, 928] (5탭: 지도/소문/의뢰/숙련/상성표)
│  ├─ MarketWindow (Assets/Art/UI/Relayout/MarketWindow.prefab)        [1136, 96, 764, 690]
│  ├─ TavernWindow (Assets/Art/UI/Relayout/TavernWindow.prefab)        [1136, 96, 764, 540]
│  ├─ RestWindow (Assets/Art/UI/Relayout/RestWindow.prefab)            [1136, 96, 764, 440]
│  ├─ StationHallWindow (Assets/Art/UI/Relayout/StationHallWindow.prefab) [1136, 96, 764, 540]
│  ├─ WorkshopWindow (Assets/Art/UI/Relayout/WorkshopWindow.prefab)    [1136, 96, 764, 560]
│  └─ CityHallWindow (Assets/Art/UI/Relayout/CityHallWindow.prefab)    [1136, 96, 764, 500]
├─ OverlayCanvas (Canvas, sortingOrder 200) — 모달·목표 팝업
│  ├─ RepaymentObjective (Assets/Art/UI/RepaymentObjective.prefab)
│  ├─ PauseModal (Assets/Art/UI/Scene/PauseCanvas.prefab 개편)        [740, 286, 440, 508]
│  └─ RunEndModal (Assets/Art/UI/Relayout/RunEndModal.prefab)          [640, 210, 640, 660]
└─ TooltipCanvas (Canvas, sortingOrder 270~272) — 입력 비차단 툴팁·미리보기
   ├─ PuzzleItemTooltip (Assets/Art/PuzzleItemTooltip.prefab)
   ├─ EntityPreviewCard (Assets/Art/UI/Components/EntityPreviewCard.prefab)
   ├─ ItemInfoPopup (Assets/Art/UI/Components/ItemInfoPopup.prefab)
   └─ EntityContextMenu (Assets/Art/UI/Components/EntityContextMenu.prefab)
```

### 6-2. 고정 틀 (Fixed Frame) 상세

| 구성 요소 | 프리팹 / 스크립트 | 역할 및 내부 계층 | 비고 |
|---|---|---|---|
| **TopHUD** | `City Navigation.prefab` 개편 · `TopHudView` (`PuzzleCityView` 분리) + `PuzzleSettlementGaugePresenter`/`View` + `TrainventoryReputationView` | 현재 역·지역명(`Current City`, `TextLinkHandler`), 기한(`DeadlineCard`), 시각·24시간 막대(`DayNightBar` + `Tick_0~7`), 정산 요약(`Settlement`), 평판 버튼(`Reputation`), 자금(`Funds`), 목표 버튼(`Game Clear Button` → `CityHallWindow` 열기), `⚙ 일시정지` 버튼 | 기존 `PuzzleCityView`에서 술집(`talkButton`)·휴식(`waitButton`) 참조를 제거한다 |
| **QuickToolbar** | 신규 `QuickToolbar.prefab` · `QuickToolbarView` | `수첩(N)`, `노선도(M)`, `배경만 보기`(열린 창 일시 토글), `창 배치 초기화`, 단축키 요약 라벨 | 기존 `FixedBottomBar.prefab`의 수첩/지도 버튼을 대체 |
| **StatusBanner** | `JourneyFooter.prefab` 개편 | 야간 휴업·자금 부족·패배/승리·숙련 상승 알림 띠 | 상단 중앙(`660, 90`) 배치, 내용 없을 때 숨김 |
| **PlatformScene** | 신규 `PlatformScene.prefab` · `PlatformSceneView` | 좌측 승강장·기차길 배경(R7: 초기 도형/아이콘 → 본 스프라이트 교체), 이동 중 속도선 오브젝트(`TravelSpeedLines`) | `RaycastTarget = false` 순수 배경(H8) |
| **RailStrip** | 신규 `RailStrip.prefab` · `RailStripView` (기존 `PuzzleTrainBarView` 대체) | `ScrollRect`(세로, `Viewport` + `Content`) + 선로 배경 + 기관차 헤더 + 사전 배치 화차 슬롯 풀(`RailWagonCar` 16~24개 풀링 또는 동적 데이터 바인딩; H11) + 상하 페이드·▲▼ 버튼. 각 `RailWagonCar`는 `Button` + 7×5 `MiniGrid`(35개 `Image`, `9×9px`, 간격 `1.5px`: 빈 칸 회색 / 화물 2톤 노랑 / 막힌 칸 어두운 색, R8) + 번호·적재량·가치 라벨 | 기존 `FixedBottomBar/TrainBar`(최대 8량 고정)를 대체. 드래그 앤 드롭(화차 간 화물 이동, 화차 순서 변경, 화차 판매) 이벤트 인터페이스 유지 |
| **TownScene** | 신규 `TownScene.prefab` · `TownSceneView` + `TownBuildingButton` × 6 | 우측 마을 배경 + 다이제틱 건물 `Button` 6종(`Market`, `Tavern`, `Rest`, `StationHall`, `Workshop`/`EmptyLot`, `CityHall`). 각 버튼 자식에 세계 안 상태 오브젝트(`ChalkboardTMP` 물가 칠판, `RumorBubble` 말풍선, `ChimneySmoke` 굴뚝 연기, `WindowGlow` 야간 불빛, `ClockHand` 시계) 배치 | 기존 `StationActions.prefab` 행동 카드 6종 및 `RumorSummary`/`QuestSummary` 대체(H9) |

### 6-3. 창 시스템 (`FloatingWindow` 공용 컴포넌트)

신규 `Assets/Scripts/Runtime/UI/Windowing/` 아래 3개 컴포넌트를 둔다.

- **`FloatingWindow.cs`** (모든 창 루트 프리팹에 부착):
  - `[SerializeField] string windowId`, `RectTransform titleBar`, `Button closeButton`, `Vector2 defaultPosition`, `Vector2 defaultSize`, `Vector2 minSize`, `Vector2 maxSize`, `bool startOpen`.
  - **제목줄 드래그**: `titleBar`에 부착된 `FloatingWindowDragHandle`(`IBeginDragHandler`, `IDragHandler`)가 `RectTransform.anchoredPosition`을 이동.
  - **가장자리·모서리 리사이즈**: 상/하/좌/우 4변(두께 `8px`)과 우하단 모서리(`18×18px`)에 사전 배치된 `FloatingWindowResizeHandle`(`IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`)이 `minSize`~`maxSize` 범위 내로 `sizeDelta`와 위치를 갱신하고 `Resized` 이벤트를 발행.
  - **화면·HUD 클램프**: `1920×1080` 기준 `x: [0, 1920 - w]`, `y: [76, 1080 - h]`로 제한해 상단 `TopHUD`(`y=12..68`)를 침범하지 못하게 함(R10). 중앙 선로(`549..640`)나 마을 위로 옮기는 것은 허용하되 기본 위치는 H10을 따름.
  - **최상위 승격**: `IPointerDownHandler`에서 `transform.SetAsLastSibling()` 호출.
  - **12px 스냅**: 드래그/리사이즈 중 화면 4변(`0, 1920, 76, 1080`) 및 다른 열린 `FloatingWindow` 외곽선과 `12px` 이내로 근접하면 자석처럼 붙임.
  - **영속성(PlayerPrefs)**: `OnEndDrag` / `OnResizeEnd` / `SetOpen` 시 `windowId` 키(`ui.win.<windowId>`)로 `{x, y, w, h}` JSON 저장.
- **`FloatingWindowManager.cs`** (`FloatingWindowCanvas`에 부착):
  - 씬 내 모든 `FloatingWindow` 등록, 스냅 대상 목록 제공, `ResetAllLayouts()`(H10 기본 좌표·크기로 복구), `ToggleBackgroundOnly()`(열린 창 일괄 숨김/복원), `TryConsumeCancel()`(가장 위(`GetSiblingIndex()` 최대)에 있는 열린 창 닫기 → 없으면 `PauseModal`로 넘김).

### 6-4. 창별 내용 및 보드 셀 크기 동적 재계산

| 창 | 기본 Rect (`1920×1080`) | 내용 및 View/Presenter 구성 | 신규 / 재사용 | 대체하는 기존 프리팹/뷰 |
|---|---|---|---|---|
| **TrainWindow** | `(26, 516, 510, 544)` | 상단 화차 제목·가치·특수효과·숙련 칩(`TrainventoryProficiencyChipsView`) + 중앙 7×5 보드(`PuzzleInventoryView` + `BoardScrollRect`) + 하단 Δ·행선지 렌즈 비교 요약(`PuzzleRouteLensView` 요약 또는 임베드) | **재사용+개조** (`PuzzleInventoryView`, `TrainventoryProficiencyChipsView`) | `GameplayLayout/TrainColumn/Train Inventory` |
| **LensWindow** | `(26, 134, 510, 366)` | 잡은/선택한 화물의 행선지별 거래 견적, 다음 정산 Δ, 상위 후보 목록, `현재/방문 도시/소문 도시` 탭 | **재사용+래핑** (`PuzzleRouteLensView` + `FloatingWindow`) | `GameplayLayout/TrainColumn/RouteLens` |
| **NotebookWindow** | `(16, 128, 522, 928)` | 5탭(`지도 / 소문 / 의뢰 / 숙련 / 상성표`). `지도` 탭에 `PuzzleMapView` + `MapSearchView` + `PuzzleRoutePlanView` 임베드, `상성표` 탭에 `PuzzleSynergyChartView` 임베드 | **개조+통합** (`PuzzleTradeNotebookView`/`Presenter` 확장) | `PuzzleTradeNotebook.prefab`, `PuzzleWorldMap.prefab`, `PuzzleSynergyChart.prefab` |
| **MarketWindow** | `(1136, 96, 764, 690)` | 상단 시세 띠(`CityTradePreferencesView`) + 13×9 보드(`PuzzleInventoryView` + `BoardScrollRect` + 화물별 가격표 라벨) + 하단 조작 안내 | **재사용+개조** (`PuzzleInventoryView`, `CityTradePreferencesView`) | `StationActions/MarketContent` |
| **TavernWindow** | `(1136, 96, 764, 540)` | 술집 소문(`TavernRumorView`, `PuzzleCityView`에서 분리) + `TrainventoryQuestBoardView` + `TrainventoryQuestDetailView` | **개조** (`Tavern.prefab` → `FloatingWindow` 래핑) | `Assets/Art/UI/Station/Tavern.prefab` |
| **RestWindow** | `(1136, 96, 764, 440)` | 1~4턴 휴식 조절(`RestEarlier`/`RestDuration`/`RestLater`) + `아침까지 대기`(`PuzzleCityView`에서 분리) + 정산 안내 | **개조** (`Rest.prefab` → `FloatingWindow` 래핑) | `Assets/Art/UI/Station/Rest.prefab` |
| **StationHallWindow** | `(1136, 96, 764, 540)` | 화차 시설(`PuzzleWagonProductRow[]`, `PuzzleWagonDetailView`, 클릭 구매 + 드래그 구매·판매) + 출발/노선도 열기 버튼 | **개조** (`WagonShop.prefab` + 출발 버튼 통합) | `Assets/Art/UI/Station/WagonShop.prefab`, `TravelAction` |
| **WorkshopWindow** | `(1136, 96, 764, 560)` | 역 `workshopIds`에 따라 도축/개조(모드 선택)/정제 UI 표시, 시설 없는 역은 `공터` 안내 표시 | **통합+개조** (`Butchery`/`Modification`/`Refining` → `WorkshopWindow` 단일 창) | `Assets/Art/UI/Workbench/Butchery.prefab`, `Modification.prefab`, `Refining.prefab` |
| **CityHallWindow** | `(1136, 96, 764, 500)` | 현재 도시 개요, 물가/선호/희귀·특산 요약, 평판·숙련 단계, 목표 자금(`1,500$`) 및 결산 카운트다운 | **신규** (`CityHallView` / `CityHallPresenter`) | `StationHome` 요약 + `City Navigation` 목표 팝업 |
| **PauseModal** | `(740, 286, 440, 508)` | 계속하기, 재시작, 배치 후보 표시 토글, **창 배치 초기화**, 게임 종료 | **개조** (`PauseMenu.cs`, 결산 기능 제거) | `Assets/Art/UI/Scene/PauseCanvas.prefab` |
| **RunEndModal** | `(640, 210, 640, 660)` | 승패 헤더, 최종 잔액/목표 차액, 경과 턴·거래/의뢰 통계 요약, 무한 모드(승리 시)/재시작/종료 | **신규** (`RunEndView` / `RunEndPresenter`) | `PauseMenu.ShowCompletion` 재사용 제거 |
| **TravelBanner** | `(690, 110, 680, 110)` | 이동 확인(운임·소요 턴) 및 이동 중 출발→도착 진행 바·시각 표시 | **개조** (`PuzzleMapTravelView`) | `PuzzleWorldMap/MapTravelPanel` |
| **ItemTooltip** | 자동 위치 | 커서 기준 또는 화차 창 상단, 화면 밖 클램프 | **재사용** (`PuzzleItemTooltipView`) | `Assets/Art/PuzzleItemTooltip.prefab` |

**보드 셀 크기 동적 재계산 (R11)**:
- `PuzzleInventoryView`에 `ScrollRect boardScrollRect`, `RectTransform boardViewport`, `RectTransform boardContent`를 추가한다.
- 창 리사이즈(`FloatingWindow.Resized`) 또는 `OnRectTransformDimensionsChange()` 시:
  ```csharp
  float raw = Mathf.Min(boardViewport.rect.width / cols, boardViewport.rect.height / rows);
  cellSize = Mathf.Clamp(raw, 40f, 72f);
  ```
- `raw < 40f`이면 `boardContent.sizeDelta = new Vector2(cols * 40f, rows * 40f)`로 설정해 `boardScrollRect` 스크롤을 활성화하고, `raw >= 40f`이면 `boardContent`를 `boardViewport` 크기에 맞춰 중앙 정렬한다.
- 갱신된 `cellSize`로 `boardCells`, `placementSlots`, `ghostSlots`, `outlineSlots`, `synergyArrows`, `candidateMarkers`의 위치·크기를 일괄 재계산한다.

---

## 7. 코드 영향 (`Assets/Scripts/Runtime/UI/**`)

### 7-1. 기존 Presenter / View 전수 분류표

| 파일 (`Assets/Scripts/Runtime/UI/`) | 분류 | 사유 및 변경 내용 |
|---|---|---|
| `PuzzleInventoryPresenter.cs` | **adapt** | 단일 `trainView`·`trainBarView`·`stationActions.IsWagonShopTarget`·`isMapOpen` 차단(`MarketOperationsBlocked`) 하드 커플링 제거. `RailStripView` 이벤트 연결 및 `StationHallWindow` 드롭 판정으로 교체 |
| `PuzzleInventoryPresenter.Lens.cs` | **adapt** | 드래그 중뿐 아니라 선택 화물·`LensWindow` 표시와 연동, `NotebookWindow` 지도 탭 핀과 마지막 선택 화물 동기화(R6) |
| `PuzzleInventoryView.cs` | **adapt** | `SetBoardSize` 1회 계산 → `RecalculateLayout()` 동적 셀 크기(`40~72px` + `ScrollRect` 미만 스크롤) 지원, `boardCells` 부모 검증(`board.rectTransform` → `boardContent`) 완화, 시장 화물 가격표 라벨 슬롯 추가 |
| `PuzzleInventoryInput.cs` | **adapt** | `FloatingWindow` 드래그/리사이즈 중 화물 입력과 충돌하지 않도록 포인터 캡처 조건 보완 |
| `PuzzleInventoryDrag.cs` | **keep** | 그리드 좌표/드래그 상태 계산 로직은 보드 셀 크기 변경과 무관하게 재사용 |
| `PuzzleSlotVisual.cs` | **keep** | `cellSize` 인자를 받아 외곽선/슬롯을 그리므로 그대로 재사용 |
| `PuzzleRouteLensView.cs` | **adapt** | `FloatingWindow` 내부에 들어가거나 `TrainWindow` 하단 스트립과 데이터를 공유하도록 `Panel`/버튼 바인딩 보완 |
| `PuzzleCityView.cs` | **adapt** | `waitButton`(Rest)·`talkButton`/`tavernTitle`/`rumorLabel`(Tavern) 강결합 제거. TopHUD 전용으로 정리하고 `⚙ 일시정지`·`목표→시청` 버튼 연결 |
| `PuzzleSettlementGaugePresenter.cs` / `View.cs` | **keep** | `TopHUD`의 `DayNightBar`·`Settlement`에 그대로 유지 |
| `TrainventoryReputationView.cs` | **adapt** | 클릭 시 `NotebookWindow` 숙련 탭 또는 `CityHallWindow` 열기 연결 |
| `TrainventoryProficiencyChipsView.cs` / `EntryView.cs` / `PanelView.cs` / `Presenter.cs` | **keep** | `ChipsView`는 `TrainWindow`, `PanelView`는 `NotebookWindow` 숙련 탭에서 그대로 재사용 |
| `PuzzleTrainBarView.cs` | **replace** | `MaxWagonCountCeiling` 고정 배열 검사 제거 → 무제한 세로 `ScrollRect` + 7×5 미니 그리드를 갖춘 `RailStripView`로 대체 |
| `PuzzleStationActionsPresenter.cs` / `View.cs` | **replace** | `actionsList` 허브 제거(H9). 각 건물 창(`MarketWindow`, `TavernWindow`, `RestWindow`, `StationHallWindow`, `WorkshopWindow`, `CityHallWindow`)의 개별 Presenter/View로 분해 |
| `PuzzleStationSummaryPresenter.cs` / `View.cs` | **replace** | `StationHome` 상단 요약 띠 제거 → 소문 말풍선(`TownScene`) 및 `NotebookWindow`로 통합 |
| `PuzzleWagonDetailView.cs` / `PuzzleWagonProductRow.cs` | **adapt** | `StationHallWindow`로 이동, 드래그 구매 외에 **클릭 구매 버튼** 지원 추가 |
| `PuzzleWorkWaitView.cs` | **keep** | 작업 접수/진행 대기 표시에 필요 시 재사용(현재 작업 대기 팝업 없음 규칙과 정합 확인) |
| `PuzzleTradeNotebookPresenter.cs` / `View.cs` | **adapt** | 4탭 → **5탭**(`Map`, `Rumors`, `Quests`, `Proficiency`, `Synergy`) 확장, `widePanelLeft`/`widePanelWidth` 및 `SideHandle` 제거, `FloatingWindow` 래핑 |
| `PuzzleEdgeHandleDrag.cs` | **replace** | 우측 끝 세로 손잡이(`SideHandle`) 폐기(QuickToolbar·단축키 `N`으로 대체) |
| `PuzzleMapPresenter.cs` / `View.cs` / `Navigation.cs` | **adapt** | 독립 전체/우측 패널(`closeButton`·`MapToggleRequested`)에서 `NotebookWindow` 지도 탭 임베드용으로 전환. `OnRectTransformDimensionsChange` 기반 리사이즈 대응 유지, 마지막 선택 화물 행선지 핀(R6) 추가 |
| `PuzzleMapToggleButton.cs` | **replace** | 하단 고정 바 제거 → `QuickToolbarView`의 `노선도(M)` 버튼으로 대체 |
| `PuzzleMapNodeView.cs` | **keep** | 역 노드 호버/클릭/레이캐스트 필터 그대로 재사용 |
| `MapSearchPresenter.cs` / `View.cs` | **keep** | `NotebookWindow` 지도 탭 상단에 그대로 배치 |
| `PuzzleRoutePlanPresenter.cs` / `View.cs` | **adapt** | 좁은 `522px` 지도 탭 폭에 맞게 카드 폭/배치 조정 |
| `PuzzleMapTravelView.cs` | **adapt** | 지도 위 딤 오버레이 → `TravelBanner`(상단 중앙 스트립) + 승강장 주행 연출(`PlatformSceneView`) 트리거 |
| `PuzzleSynergyChartPresenter.cs` / `View.cs` | **adapt** | 별도 Canvas(`RaiseSynergyChartShowRequested`) 및 `1840px` 확장 제거 → `NotebookWindow` 상성표 탭 내부 `ScrollRect` 컨테이너로 임베드(`ConversationLog` 겹침 버그 근본 해결) |
| `TrainventoryQuestBoardPresenter.cs` / `View.cs` / `DetailView.cs` | **keep** | `TavernWindow` 내부에서 그대로 재사용 |
| `CityTradePreferencesView.cs` | **keep** | `MarketWindow` 상단 시세 띠 및 `CityHallWindow`에서 재사용 |
| `PuzzleItemTooltipView.cs` / `PuzzleEffectDisplay.cs` | **keep** | 자동 위치 클램프만 보완해 재사용 |
| `EntityPreviewPresenter.cs` / `View.cs`, `ItemInfoPopupPresenter.cs` / `View.cs`, `EntityContextMenuPresenter.cs` / `View.cs` | **keep** | `TooltipCanvas`에서 그대로 재사용 |
| `EntityLinkRouter.cs` / `Navigation.cs` / `History.cs` / `Kind.cs`, `TextLinkHandler.cs` | **adapt** | `EntityLinkNavigation.Navigate`가 지도 이동 시 `NotebookWindow` 지도 탭을 열도록 연결 |
| `PauseMenu.cs` | **adapt** | `ShowCompletion` 분리(`RunEndModal`), `CandidateToggleButton` 유지 + `창 배치 초기화` 버튼 추가, `TryConsumeCancel`을 `FloatingWindowManager` 스택과 연동 |
| `PuzzleObjectiveView.cs` | **keep** | 런 시작 목표 팝업 유지(또는 `CityHallWindow`와 문구 공유) |
| `FlexibleGridLayoutGroup.cs`, `QuarterTurnFitter.cs`, `TagBadgeGroup.cs`, `TagBadgeView.cs` | **keep** | 공용 UI 유틸리티/부품 유지 |
| `TitlePresenter.cs` / `TitleView.cs` | **keep** | `Title` 씬은 이번 개편 범위 밖 |

### 7-2. 확인된 하드 커플링 목록

1. **`PuzzleInventoryPresenter`의 단일 `trainView` · `trainBarView` · `isMapOpen` 결합** (`PuzzleInventoryPresenter.cs:15,33,50,69,83-90,162-173`):
   - `OnEnable`에서 `trainView.transform.IsChildOf(transform)`를 강제하고, `trainBarView.TryGetWagonAtScreenPosition`과 `stationActions.IsWagonShopTarget`에 직접 의존한다.
   - `MarketOperationsBlocked => isMapOpen || isTravelOpen` 때문에 지도가 열리면 시장 조작이 막히는데, 3b에서는 왼쪽 `NotebookWindow(지도)`와 오른쪽 `MarketWindow`를 동시에 열 수 있어야 하므로 `isMapOpen` 차단을 제거해야 한다.
2. **`PuzzleCityView`의 HUD + 술집 + 휴식 삼중 결합** (`PuzzleCityView.cs:24-36,65-67`):
   - `City Navigation`에 붙은 `PuzzleCityView`가 `OnEnable`에서 `waitButton`(Rest)과 `talkButton`(Tavern)을 필수로 검사한다. 두 창이 독립 프리팹으로 분리되면 `enabled = false`가 되어 TopHUD 전체가 죽으므로 즉시 분리해야 한다.
3. **`PuzzleStationActionsView`의 모놀리식 허브 강결합** (`PuzzleStationActionsView.cs:14-66,86-96`):
   - `ButcheryWired` / `ModificationWired` / `RefiningWired`가 `actionsList`의 버튼 참조를 필수로 요구한다. `actionsList`를 제거하고 건물별 창으로 분리해야 한다.
4. **`PuzzleTrainBarView`의 최대 화차 수 고정 가정** (`PuzzleTrainBarView.cs:75-79`):
   - `Awake()`에서 `wagonSlots.Length < maxWagonCount`이면 `InvalidOperationException`을 던져 H11(8량 초과 스크롤)을 막는다.
5. **`PuzzleTradeNotebookView` / `PuzzleSynergyChartView`의 별도 Canvas 확장 구조** (`PuzzleTradeNotebookView.cs:46-48`, `PuzzleSynergyChartPresenter.cs:21`):
   - 상성표 탭 선택 시 `LogPanel`을 `1840px`로 늘리고 별도 Canvas를 띄우며 `ConversationLog`가 뒤에 남아 텍스트가 겹치는 버그가 있다.
6. **`PauseMenu`의 구형 패널 직접 탐색 및 결산 화면 겸용** (`PauseMenu.cs:35-43,45`):
   - `Resolve<T>`로 9개 구형 View를 직접 찾고, `ShowCompletion`이 `Pause Menu Panel`을 재사용해 결산 화면에 `배치 후보 표시` 토글이 노출된다.
7. **`PuzzleInventoryView`의 고정 보드 부모 검증 및 1회 셀 크기 계산** (`PuzzleInventoryView.cs:107,186-188`):
   - `boardCells`가 `board.rectTransform`의 직계 자식이어야만 초기화가 통과하고, `SetBoardSize`에서만 `cellSize`를 1회 계산해 창 리사이즈(`40~72px` + 스크롤)에 대응하지 못한다.

---

## 8. 단계별 구현 계획 (커밋 단위)

각 단계는 독립적으로 컴파일·실행·플레이 확인이 가능하도록 자른다.

| 단계 | 목표 및 범위 | 대상 파일 (신규/수정) | 에디터 빌더 메뉴 | 플레이 모드 비전 모델 검증 항목 | 리스크 |
|---|---|---|---|---|---|
| **S1** | **창 시스템 + 일시정지/결산 분리** | 신규: `FloatingWindow.cs`, `FloatingWindowDragHandle.cs`, `FloatingWindowResizeHandle.cs`, `FloatingWindowManager.cs`, `RunEndView.cs`, `RunEndPresenter.cs`<br>수정: `PauseMenu.cs`<br>프리팹: `PauseCanvas.prefab`, `RunEndModal.prefab` | `Tools/MyJG/UI/Relayout/S1 Build Window System` | 제목줄 드래그, 4변/모서리 리사이즈, 상단 HUD 침범 불가 클램프, 12px 스냅, 클릭 시 최상위 승격, `창 배치 초기화`, 결산 모달에 `배치 후보 표시` 미노출 | UI 입력 모듈과 드래그 이벤트 충돌; `FloatingWindowManager`와 기존 `PanelBase.Esc` 우선순위 경합 |
| **S2** | **고정 프레임(`TopHUD`, `QuickToolbar`, `StatusBanner`, `PlatformScene`, `TownScene`) + `PuzzleCityView` 분리** | 신규: `TopHudView.cs`, `QuickToolbarView.cs`, `PlatformSceneView.cs`, `TownSceneView.cs`, `TownBuildingButton.cs`<br>수정: `PuzzleCityView.cs`<br>프리팹: `City Navigation.prefab`, `JourneyFooter.prefab`, `QuickToolbar.prefab`, `PlatformScene.prefab`, `TownScene.prefab` | `Tools/MyJG/UI/Relayout/S2 Build Fixed Frame` | 상단 HUD(56px) 항목 정렬·클리핑 없음, 좌측 툴바·승강장 임시 배경, 우측 다이제틱 마을 건물 6개 배치 및 호버 하이라이트, 기존 `PuzzleCityView` 분리 후 NRE 없음 | `PuzzleCityView`에서 `waitButton`/`talkButton` 분리 시 기존 `Rest`/`Tavern` 프리팹 참조 일시 단절(S6 전까지 임시 어댑터 또는 S2에서 즉시 연결) |
| **S3** | **중앙 선로(`RailStrip`) + `TrainWindow` + 동적 셀 크기(`40~72px`/스크롤)** | 신규: `RailStripView.cs`, `RailWagonCarView.cs`, `TrainWindowPresenter.cs`<br>수정: `PuzzleInventoryView.cs`, `PuzzleInventoryPresenter.cs`<br>프리팹: `RailStrip.prefab`, `TrainWindow.prefab` | `Tools/MyJG/UI/Relayout/S3 Build Rail And Train` | 세로 선로 위 기관차·화차 3량 이상 및 8량 초과 스크롤, 7×5 미니 그리드(빈칸 회색·화물 2톤 노랑·막힌 칸 어두운 색), 화차 클릭 시 `TrainWindow` 전환, 창 리사이즈 시 셀 `40~72px` 변화 및 40px 미만 스크롤 | `PuzzleInventoryView`의 `boardCells`/`placementSlots` 좌표를 `BoardContent` 기준으로 옮길 때 드래그/화살표 오프셋 어긋남 |
| **S4** | **`NotebookWindow` 5탭 통합(지도·상성표 임베드)** | 수정: `PuzzleTradeNotebookView.cs`, `PuzzleTradeNotebookPresenter.cs`, `PuzzleMapView.cs`, `PuzzleMapPresenter.cs`, `PuzzleSynergyChartView.cs`, `PuzzleSynergyChartPresenter.cs`, `EntityLinkNavigation.cs`<br>프리팹: `NotebookWindow.prefab` | `Tools/MyJG/UI/Relayout/S4 Build Notebook Window` | 5탭(`지도/소문/의뢰/숙련/상성표`) 전환, `522px` 폭 안에서 지도 줌/패닝/검색/경로계획 정상 동작, 상성표 탭에서 텍스트 겹침 없음, 고유명사 링크 클릭 시 해당 탭 자동 열림 | `PuzzleMapView`의 줌 역배율 라벨·`RoutePlan/Card`가 `522px` 좁은 폭에서 겹칠 수 있음 |
| **S5** | **시장 창(`MarketWindow`) + 렌즈(`LensWindow`/비교 스트립) + 툴팁** | 신규: `MarketWindowView.cs`, `MarketWindowPresenter.cs`<br>수정: `PuzzleInventoryPresenter.cs`, `PuzzleInventoryPresenter.Lens.cs`, `PuzzleRouteLensView.cs`, `PuzzleItemTooltipView.cs`<br>프리팹: `MarketWindow.prefab`, `LensWindow.prefab` | `Tools/MyJG/UI/Relayout/S5 Build Market And Lens` | 도착 직후 `TrainWindow`+`MarketWindow` 자동 오픈(R5), 시장 13×9 보드(56px)와 화물 가격표 표시, 시장↔화차 드래그·Ctrl+클릭 거래, 왼쪽 수첩(지도) 열린 상태에서도 시장 조작 가능, 잡은 화물 Δ·후보·렌즈 창 정상 표시 | 시장 화물 가격표 TMP가 화물 스프라이트와 겹치거나 드래그 중 레이캐스트를 가로챌 수 있음(`raycastTarget = false` 필수) |
| **S6** | **마을 건물 창 5종(`Tavern`, `Rest`, `StationHall`, `Workshop`, `CityHall`) + `TravelBanner`** | 신규: `CityHallView.cs`, `CityHallPresenter.cs`, `WorkshopWindowView.cs`, `WorkshopWindowPresenter.cs`, `StationHallView.cs`<br>수정: `PuzzleMapTravelView.cs`, `PuzzleWagonProductRow.cs`<br>프리팹: `TavernWindow.prefab`, `RestWindow.prefab`, `StationHallWindow.prefab`, `WorkshopWindow.prefab`, `CityHallWindow.prefab`, `TravelBanner.prefab` | `Tools/MyJG/UI/Relayout/S6 Build Town Windows` | 마을 건물 6종 클릭 및 단축키(`1~6`)로 각 창 열림, 화차 클릭/드래그 구매 및 선로 드래그 판매, 작업대(도축/개조/정제 및 공터) 동작, 시청 정보/목표 표시, 이동 시 상단 `TravelBanner` 진행 표시 | `WorkshopWindow`에 도축·개조·정제 3종 보드 로직을 통합할 때 `PuzzleInventoryPresenter`의 각 보드 참조(`butcheryView` 등) 매핑 누락 주의 |
| **S7** | **다이제틱 상태 연동 + 구형 UI 정리 + 문서 현행화** | 수정: `TownSceneView.cs`, `PlatformSceneView.cs`, `GameUI.prefab`, `GameCanvas.prefab`<br>제거/비활성: 구형 `GameplayLayout`, `FixedBottomBar`, `PuzzleWorldMap` 루트<br>문서: `Docs/Management/UI_LAYOUT.md`, `CURRENT_STATE.md`, `HUD구성및플로우.md`, `UI_DESIGN_HANDOFF.md` | `Tools/MyJG/UI/Relayout/S7 Finalize And Cleanup` | 마을 건물 위 물가 칠판·소문 말풍선·굴뚝 연기·야간 불빛 실시간 반영, 이동 중 승강장 속도선, 전체 창 닫고 `배경만 보기` 확인, 전체 루프(도착→거래→의뢰→작업대→휴식→이동→결산) 플레이 통과 | 구형 프리팹 참조를 참조하는 테스트/에디터 스크립트(`RouteLensUIBuilder.cs` 등) 컴파일 오류 점검 필요 |

> **S1 구현 결과 (2026-11-19):** `Assets/Scripts/Runtime/UI/Windowing/`(`FloatingWindow`, `FloatingWindowMath`, `FloatingWindowDragHandle`, `FloatingWindowResizeHandle`, `FloatingWindowManager`, `UIPalette`, `UIPaletteBinder`), `Assets/Resources/UI/UIPalette.asset`, `RunEndView`/`RunEndPresenter` + `RunEndModal.prefab`, `PauseMenu` '창 배치 초기화' 버튼 및 Esc 스택 연동, `RelayoutS1Builder.cs`(`Tools/MyJG/UI/Relayout/S1 Build Window System`) 구현 및 플레이 스모크 캡처(`.orca/analysis/relayout/s1/`) 확인 완료. 거래 횟수(`trade count`)는 `PuzzleInventoryService`에 미추적 상태이므로 결산 모달에서 생략.

---

## 9. 작업 배분 제안

- **빠른 코드 워커(Fast Code Worker) 적합 작업**:
  - **S1** (`FloatingWindow` 컴포넌트 3종, `RunEndView`/`Presenter` 신규 작성): `PuzzleInventoryService`와 결합도가 낮고 독립적인 uGUI 수학/직렬화 작업.
  - **S2** (`QuickToolbarView`, `PlatformSceneView`, `TownSceneView` 껍데기 및 에디터 빌더 스크립트 작성): 정적 배치와 단순 이벤트 위임 위주.
  - **S6 일부** (`CityHallView`/`Presenter`, `RestWindow`, `TavernWindow` 래핑): 기존 로직을 독립 창으로 옮기는 정형화된 작업.
  - **S7 문서 현행화**: 구현 완료 후 실측 좌표/프리팹 구조 기반 문서 업데이트.
- **신중한 통합(Careful Integration) 필요 작업**:
  - **S3** (`RailStripView` + `PuzzleInventoryView` 동적 셀 크기 + `PuzzleInventoryPresenter` 분리): 드래그 앤 드롭 좌표 변환, 화차 간/선로 드롭, 시너지 화살표·고스트 슬롯 오프셋이 얽혀 있어 회귀 위험이 가장 높음.
  - **S4** (`NotebookWindow` 5탭 통합): `PuzzleMapView`·`PuzzleMapNavigation`·`PuzzleSynergyChartView`의 캔버스/레이캐스트/줌 보정과 `EntityLinkNavigation` 연동을 함께 맞춰야 함.
  - **S5** (`MarketWindow` + `LensWindow` + `isMapOpen` 차단 해제): 거래 견적·렌즈·툴팁·드래그 상태가 맞물리는 핵심 거래 루프.
- **병렬화 가능 지점**: S1 완료 후 **S2(고정 프레임)** 와 **S4(수첩 5탭 프로토타입)** 는 서로 다른 프리팹을 다루므로 병렬 진행이 가능하다.

---

## 10. 열린 결정 (미확정 구현 세부 사항)

H1~H14 및 R1~R13으로 확정되지 않았으나 구현 전 확정이 필요한 사항과 권장안은 다음과 같다.

| 번호 | 항목 | 선택지 / 쟁점 | 권장안 |
|---|---|---|---|
| **O1** | **`LensWindow` 독립 창 vs `TrainWindow` 하단 고정** | H10/R9는 `렌즈 정보`를 독립 자유 창 목록에 포함했고, `concept-3b.html`은 `TrainWindow` 하단에 비교 줄을 두고 있다. 둘 다 두면 정보가 중복될 수 있다. | **`TrainWindow` 하단에 핵심 Δ·견적 2줄 요약을 두고, 상세 비교(방문/소문 도시 탭)는 `LensWindow` 독립 창(도구줄/화차 창 버튼으로 토글)으로 분리**한다. |
| **O2** | **다중 화차 창 동시 열기 여부** | H7·H10에서 선로의 화차를 누를 때 기존 `TrainWindow`의 내용을 해당 화차로 전환할지, 화차마다 별도 `TrainWindow`를 여러 개 띄울지 미정이다. | **단일 `TrainWindow` 내용 전환**을 권장한다. `PuzzleInventoryPresenter`의 단일 `trainView` 구조와 호환성이 높고, 여러 창이 겹쳐 선로·플랫폼을 가리는 문제를 막는다(화차 간 이동은 `RailStrip` 드롭으로 이미 지원). |
| **O3** | **`RailStrip` 사전 배치 화차 슬롯 풀 상한** | 런타임 UI 생성 금지 규칙 아래 H11(8량 초과 스크롤, 최대 개수 미가정)을 만족하려면 프리팹에 미리 몇 개의 슬롯을 둘지 정해야 한다. | 에디터 빌더로 **`RailWagonCar` 24개**를 사전 배치 풀로 둔다(실제 게임에서 24량을 넘기면 상한 경고 또는 24량 초과 시에만 예외 처리). |
| **O4** | **`WorkshopWindow` 내부 구조** | 기존 `Butchery`, `Modification`, `Refining` 프리팹이 별도 존재한다. 단일 `WorkshopWindow` 안에서 3개 서브 패널을 토글할지, 공용 보드 1개에 모드만 바꿀지 결정 필요. | **단일 `WorkshopWindow` 안에 공용 `PuzzleInventoryView` 1개 + 모드/설명 서브 영역**을 두고 역의 `workshopIds`에 따라 바인딩하는 방식을 권장한다(현재 1역 최대 1작업장). |
| **O5** | **`QuickToolbar` "배경만 보기" 범위** | `배경만 보기`를 눌렀을 때 `FloatingWindow`만 숨길지, `StatusBanner`·`QuickToolbar` 자체의 다른 버튼 상태까지 영향을 줄지 결정 필요. | **`FloatingWindowCanvas` 내 열린 모든 `FloatingWindow`만 일시 토글**(위치·열림 상태 기억 후 복원)하고 `TopHUD`·`QuickToolbar`·`RailStrip`은 유지한다. |
| **O6** | **단축키(`1~6`, `M`, `N`, `Tab`) 입력 시스템 충돌** | `MapSearch` 입력 칸에 포커스가 있을 때 숫자/문자 단축키가 건물 창을 열거나 수첩을 여닫으면 안 된다. | 기존 `PuzzleInventoryInput`의 `GameEvents.TextInputFocusChanged`를 `QuickToolbar`/단축키 핸들러에서도 구독해 **텍스트 입력 중 단축키를 일괄 차단**한다. |
