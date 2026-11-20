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
| H15 | 제한 팔레트 | 팔레트마다 16비트 색 기준으로 압축한 제한판을 둔다. 도트 그래픽을 고려한다 |
| H16 | 표현 제약 | Unity에서 그대로 만들 수 있는 형태만 쓴다. 둥근 모서리 없음, 임시 이모지 없음, 과도한 장식 없음. 허용: 단색 사각형(반투명 가능), 1~2px 단색 테두리, 글자, 기존 화물 스프라이트, 정사각 칸 격자. 건물은 이름표가 붙은 단색 사각형, 플랫폼은 단색 바닥. **툴팁은 예외: 사용자 요청(2026-11-20)에 따라 기존 금색 테두리·둥근 모서리·화살표 디자인을 유지한다. S5의 공용 툴팁 직각/단색 덮어쓰기는 제거한다.** |
| H17 | 화차 구매·판매 | 역사 창의 화차 상품을 선로로 끌어다 놓으면 구매, 선로의 화차를 역사 쪽으로 끌어다 놓으면 판매. 버튼식 구매는 쓰지 않는다 |
| H18 | 화차 창 여러 개 | 화차 인벤토리 창을 여러 개 동시에 띄울 수 있다. 원칙은 전부 가능하고, 부하가 문제면 기초데이터에서 개수 제한을 건다 |
| H19 | 의뢰 조작 | 의뢰 게시판을 그리드 인벤토리로 둔다. 의뢰 화물 정보에 기한·보수·목적지를 보인다. 수락은 게시판에서 화차로 끌어다 놓기(구매처럼), 완료는 목적지 게시판으로 끌어다 놓기(판매처럼)만으로 한다(규칙 변경, 게임 기획서 11장 반영) |
| H20 | 화차 순서 | 선로에서 화차를 끌어다 놓아 순서를 바꾼다 |
| H21 | 크기 조정 철회 | 창 크기 조정 기능을 뺀다(H13의 크기 조정 부분 철회). 창 이동·붙음·위치 저장·배치 초기화는 유지 |
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
| R21 | 단계별 구현 중에도 게임이 계속 플레이 가능하도록 `RelayoutSettings.Enabled`(`PlayerPrefs` 키 `"ui.relayout.enabled"`, S7 전까지 기본값 `0`) 뒤에 새 레이아웃을 둔다. 에디터 메뉴 `Tools/MyJG/UI/Relayout/Toggle Relayout` 및 개발/에디터 일시정지 메뉴 토글을 제공한다 | S2~S6 사이 기존 기능 회귀 없이 점진적 전환·검증이 가능하게 한다 |
| R22 | 제한판 규격: 색은 RGB555(SNES식 15비트, RGB565에서도 정확) 격자에 맞춘다(채널 5비트 v → 8비트 (v<<3)|(v>>2)). 후보마다 최대 32색, 5~6개 색상 램프(램프당 3~6단, 밝은 쪽은 따뜻하게·어두운 쪽은 차갑게 색조 이동) + 검정·흰색 계열. 모든 토큰은 이 32색 중 하나에 대응한다 | 16비트 기기 팔레트 감각을 살리면서 UI 의미 색을 모두 담을 수 있는 최소 규모다 |
| R23 | 제한판 표현 규칙: 반투명·그라데이션·그림자·흐림 없음, 1~2px 단단한 테두리, 둥근 모서리 대신 직각 또는 2px 계단 모서리. 가독성 기준(본문 4.5:1 등)과 미니 격자 색 의미는 유지하고, 32색 안에서 못 지키면 넘기지 말고 보고한다. 제한판은 Aseprite·GIMP용 `.gpl`·`.hex`로도 내보낸다 | 도트 그림과 UI가 같은 색표를 쓰고, 그림 작업자가 바로 쓸 수 있게 한다 |
| R24 | H17 판매 놓는 곳: 열린 역사 창의 판매 영역 또는 마을의 역사 건물. 구매 시 선로의 화차 사이에 놓으면 그 자리에 들어간다(구매 후 기존 `TryMoveWagon`으로 위치 지정). 판정·금액은 기존 `TryPurchaseWagon`·`TrySellWagon` 그대로 | 끌어 놓기 로직은 이미 있어 화면만 옮긴다 |
| R25 | H18 구현: 실행 중 UI 생성 금지 규칙에 따라 화차 창을 미리 N개 배치한다. N은 기초데이터 `ui.maxTrainWindows`(기본 16)이고 빌더가 그 수만큼 만든다(값을 바꾸면 빌더를 다시 실행). 이미 열린 화차를 누르면 그 창을 앞으로, 상한을 넘으면 가장 오래 안 만진 창을 새 화차로 바꾼다. 창끼리 화물을 끌어 옮기면 화차 간 이동이다. 새 창은 앞 창에서 24px씩 비켜 연다 | 사용자 지시(전부 가능, 필요하면 기초데이터로 제한)를 규칙 안에서 구현한다 |
| R26 | H19 세부: 의뢰 게시판 격자는 9×5, 의뢰 화물은 화물 모양 그대로 놓인다. 다른 마을 의뢰 화물·일반 화물은 게시판에 놓을 수 없다. 받은 마을 게시판으로 되돌려 놓는 취소는 두지 않는다(기존 규칙에 취소 없음). 마지막 턴 행동으로 결산될 때 이미 목적지에 와 있는 진행 중 의뢰는 결산 직전에 완료로 처리한다 | 끌어 놓을 기회가 없는 결산 순간에 손해를 보지 않게 한다 |
| R27 | 일정: H18·H20과 H17의 선로 쪽은 S3b(S4 다음), H17의 역사 창 쪽과 H19는 S6에서 한다. H19 규칙 변경은 서비스 수정과 함께 별도 커밋으로 둔다 | 선로·화차 창 코드를 먼저 안정시킨 뒤 마을 창을 붙인다 |
| R28 | H21 적용: 창은 기본 크기로 고정하고 크기 저장값은 무시한다. 창 안 보드의 칸 크기는 창 기본 크기에서 한 번 계산해 고정한다(R11의 실시간 재계산 철회). 크기 조정 손잡이와 관련 문구를 없앤다 | 사용자 결정 반영. 칸 크기 계산식은 창마다 기본 칸 크기를 정하는 데만 쓴다 |
| R29 | 화차 창 기본 크기를 56px 칸 7×5 보드에 맞춰 약 440×430으로 줄이고, 처음 여는 위치는 칸 배치표를 따른다: 1·2번째는 왼쪽 열 아래·위, 3~8번째는 마을 영역 위쪽부터 3열×2행, 그 뒤로는 24px씩 비켜 연다 | 크기 조정이 없어졌으므로 여러 창이 처음부터 겹치지 않아야 한다(S3b 스크린샷에서 3창이 거의 포개짐) |
| R30 | S7에서 신규 개편 레이아웃을 **기본값**(`RelayoutSettings.Enabled` 기본 `1`)으로 전환한다. 구형 레이아웃은 당분간 스위치로 접근 가능한 fallback으로 유지하며, 구형 UI 오브젝트/코드 삭제는 별도 후속 작업으로 분리한다 | 코디네이터 결정. 기본 진입 경로를 신규 UI로 확정하되 회귀 비교용 fallback을 즉시 삭제하지 않는다 |
| R31 | 새 창의 보드는 입력 담당(`Inventory Input Area`)의 자식이 아니라 포인터 이벤트가 닿지 않는다. 보드 안쪽 영역(`BoardViewport`)에 투명 판정 이미지와 `InventoryPointerForwarder`를 붙여 입력 담당으로 넘긴다. 빌더 `Tools/MyJG/UI/Relayout/Wire Board Input Forwarders`(`RelayoutInputBuilder`)는 S1~S7 빌더를 다시 돌린 뒤 반드시 마지막에 실행한다 | 2026-11-19 사용자 보고(시장 클릭·툴팁 안 됨)로 확인. 그동안의 검증이 실제 입력 경로 대신 코드 직접 호출이라 놓쳤다 |
| R32 | ~~마을 창은 한 번에 하나만 연다~~ → 2026-11-20 변경: 마을 창(시장·술집·여관·역사·시청·작업장)은 여러 개를 함께 띄운다. 건물을 눌러도 다른 마을 창을 닫지 않는다. 시장 입장은 다른 시설 입장과 따로 기억해(`PuzzleInventoryService.marketEntered`) 술집·역사·작업장을 열어도 시장 거래가 이어진다. 시장 입장은 이동하거나 옛 역 행동 UI에서 다른 행동을 고를 때(LeaveFacility)만 풀린다 | 사용자 요청: 역 안 여러 기능 창을 동시에 사용 |
| R33 | 부동 창은 원하는 위치(desired rect)를 기억하고, 캔버스 크기가 바뀌면 그 위치로 다시 배치한다. 끌어 옮기면 그 자리가 새 원하는 위치가 된다 | 첫 프레임에 CanvasScaler 적용 전 크기로 위치를 제한해 시장 창이 가운데로 밀린 문제 |
| R34 | 창 배치 기준값을 `Assets/Resources/UI/UILayoutTokens.asset`(`UILayoutTokens`)에 둔다. 제목줄 30, 테두리 2, 닫기 24, 창 여백 8, 간격 8/4, 버튼 28/22, 글자 16/13/11/20/13, 글자 상자 높이 = 글자 크기 × 1.5. 값은 지금 창들이 쓰던 값에 맞췄다(바꾸면 모든 창 내용 위치를 다시 잡아야 해서) | 2026-11-20 사용자 질문(여백·패딩·정렬 점검 여부)에 대한 조치 |
| R35 | 모든 부동 창은 공용 틀 `Assets/Art/UI/Relayout/FloatingWindowFrame.prefab`의 프리팹 변형이다. 틀이 바탕·테두리·제목줄·제목·닫기·내용 기준 영역(`Content`)을 가진다. 틀은 처음 한 번만 새로 만들고 이후에는 같은 오브젝트를 기준값에 맞게 고쳐 저장한다(변형의 참조 유지). `Tools/MyJG/UI/Relayout/Build Window Frame` | 창마다 제목줄·닫기를 따로 만들어 크기와 위치가 제각각이었다(닫기 글자 상자가 버튼보다 176px 큼 등) |
| R36 | 배치 검사 도구 `RelayoutLayoutAudit`(`Run` 프리팹, `RunLive` 실행 중 화면). 부모 밖, 창 가장자리 여백 8px, 글자 넘침, 형제 겹침, 열 정렬(왼쪽 또는 가운데)을 본다. 보드 칸 내부·선로 침목·레일·도로·광장·시각 막대 표시는 일부러 겹치게 그린 장식이라 겹침·부모 밖·여백 검사에서 뺀다. 형제가 120개를 넘는 묶음(지도 그림)은 형제 비교를 건너뛴다 | 지도 탭 검사가 형제 비교 폭증으로 에디터를 멈춘 적이 있다 |
| R37 | 시작 목표 안내 창(RepaymentObjective)은 지도 미리보기를 숨긴 뒤 남은 빈 자리를 없애 1000×360으로 줄이고 목표 금액·안내문(두 줄)·확인을 위에서 차례로 둔다(`RelayoutS1Builder.FitObjectiveModal`). 시청 창의 시세 줄은 한 줄씩 세로로 쌓아(물가·선호·풍부·희귀·기피) 해당 품목이 없는 줄이 빈칸으로 남지 않게 한다 | 2026-11-20 배치 점검 중 화면 확인으로 발견 |
| R38 | 배치 후보 표식은 옅은 채움(알파 0.22) + 2px 테두리 선 4개로 그린다. uGUI `Outline` 효과는 채움 전체를 불투명한 효과 색으로 네 번 더 그려 반투명 채움이 불투명해지므로 쓰지 않는다. 순위·가치 글자는 밝은 금색 + 어두운 바탕(`<mark>`) | 2026-11-20 사용자 보고(후보 표식이 아래를 전부 가림) |
| R39 | 술집 창 하단은 한 줄 안내 대신 의뢰 정보 카드(제목 화물 → 목적지 · 최단 이동/기한/보수/숙련 네 칸 · 실패 조건)로 보여 준다. 게시판 화물에 마우스를 올리면 갱신되고, 아니면 안내 문구만 보인다. 역사 창 화차 카드는 왼쪽 그림 · 오른쪽 이름(크게)/보드 크기·가격/칸 칩/끌어 구매 안내, 넘김은 제목줄 오른쪽, 판매·출발은 카드 아래 한 줄(창 560×246) | 2026-11-20 사용자 요청(술집 하단 정보 안 보임, 역사 배치 개선) |
| R40 | 지도 노선 운임 라벨은 지금 출발하면 실제로 차감되는 운임(기본요금 + 거리 요금 + 기차 적재 칸 × 칸당 요금, `EvaluateTravel`과 같은 계산)으로 표시하고, 지도 갱신·화물 행동마다 다시 쓴다. 에디터 배치 도구의 라벨은 적재 요금을 뺀 기본 운임 그대로 | 2026-11-20 사용자 보고(지도 비용이 기본·거리 요금만 표시) |
| R41 | 수첩 상성표는 칸 60×52(8열, 본문 폭 494)로 줄이고 글자는 9~14pt(행 머리 7pt까지) 자동 크기. 축 설명은 모서리 칸에서 표 위 한 줄로 옮기고, 표·설명·품목 태그를 세로로 쌓아 세로 스크롤(오른쪽 스크롤바)로 본다 | 2026-11-20 사용자 보고(상성표가 너무 크고 비율이 맞지 않음) |
| R42 | 창 겹침 순서는 마지막으로 누른 창이 맨 위. 창 안 버튼·보드가 누름 이벤트를 먼저 받아 창의 OnPointerDown이 불리지 않으므로, `FloatingWindowManager`가 누른 순간 포인터 아래의 가장 위 창을 찾아 맨 앞으로 올린다 | 2026-11-20 사용자 요청 |
| R43 | 역 이동 연출: 가운데 선로의 기관차·화차 목록이 출발 때 점점 빨라지며 위로(기관차 쪽) 빠져나가고(이동 연출 시간 `travelEffectSeconds` 동안), 다음 역 도착 시 아래에서 들어와 감속하며 제자리에 멈춘다(0.9초). 경로 자동 이동의 통과역은 멈추지 않고 아래에서 위로 지나간다. 방향은 2026-11-20 사용자 요청으로 위로 바꿈. 취소·실패 시 제자리로 돌아온다. 그림 위치만 움직이고 게임 상태와 무관(`RailTrainMotion`, 선로 루트 RectMask2D로 밖은 가림) | 2026-11-20 사용자 요청 |
| R44 | 도구줄에서 `노선도`·`배경만 보기` 버튼을 없애고 단축키만 남긴다: 노선도 `M`(기존), 배경만 보기 `H`(새로 지정, 숨김 Hide). 버튼이 없으므로 도구줄 오른쪽 안내에 `M 노선도 · H 배경만 보기 · 1~6 건물`을 적는다 | 2026-11-20 사용자 요청. 배경만 보기는 원래 단축키가 없어 H를 정함 |
| R45 | 수첩 지도 미리보기(축소)에서도 역을 누르면 크게 보기와 같이 동작한다(연결된 역은 이동 확인, 연결 안 된 역은 경로 계획). 경로 계획 카드는 미리보기·크게 보기 모두 지도 화면(MapViewport) 우하단에 여백 8px로 고정하고, 계획이 없으면 숨긴다. 지도 빈 공간(역 노드가 아닌 곳) 더블클릭은 미리보기 ↔ 크게 보기를 바꾼다 | 2026-11-20 사용자 요청 |
| R46 | 역 이동 확인·진행 팝업(이동 배너)은 자기 Canvas(overrideSorting, sortingOrder 295)로 게임 화면 UI 중 맨 위에 그리고, 표시 중에는 지금 보이는 지도 화면(MapViewport, 수첩 미리보기·크게 보기 모두) 위쪽 가운데(여백 8px)에 붙어 따라다니고, 폭을 지도 화면 폭 안으로 줄여(최소 360) 지도 영역을 벗어나지 않는다. 진행 카드 요소는 폭 640 기준 가로 위치·폭을 같은 비율로 줄인다. 확인 카드는 질문·이동 정보를 왼쪽 위에서 아래로 쌓고 확인·취소를 오른쪽 아래에 두며, 이동 정보 줄 수만큼 높이를 늘린다(부동 창·지도 크게 보기·결산·툴팁·우클릭 메뉴·목표 안내 위, 일시정지 메뉴 300 아래). 창 올리기는 창 영역이 아니라 포인터 아래 가장 위에 그려진 UI가 속한 창만 올려, 크게 보기 레이어를 누를 때 그 아래 창이 지도 위로 올라오지 않게 한다(R42 보완) | 2026-11-20 사용자 요청: 이동 확인 팝업은 최상단 |
| R47 | 술집 창 의뢰 카드·소문 글자의 도시·지역 이름 링크를 누르면 수첩 지도로 가서 그 역을 강조한다(`TavernWindowView`가 글자에 `TextLinkHandler`를 붙임). 미발견(`PuzzleMapVisibility.Hidden`) 역은 카메라만 그 위치로 옮기고 강조·정보는 띄우지 않는다 | 2026-11-20 개편 술집 창에서 링크 클릭이 빠짐 |
| R48 | 개편 UI에서 밤(시장 휴업)이 되면 시장 창을 닫고 시장 입장도 푼다(`PuzzleInventoryService.LeaveMarket`). 낮에 시장 건물을 다시 눌러야 열린다. 옛 UI의 '들어간 뒤 밤이 되어도 이번 입장 동안 거래' 규칙은 개편 UI에서는 쓰지 않는다 | 2026-11-20 사용자 요청 |
| R49 | 술집 창 의뢰 카드의 보수 칸은 화차를 주는 의뢰면 화차 이름(고유명사 링크, 올리면 화차 효과 미리보기)을 함께 보인다(`presentation.quest.cardWagonRewardValue`·`cardRewardWithWagonValue`) | 2026-11-20 사용자 요청: 화차 보상을 술집에서 확인 |
| R50 | 화물 끌어 놓기·집기·올리기의 대상 보드는 포인터 아래 가장 위에 그려진 부동 창 안의 보드만 쓴다(`FloatingWindowManager.TopWindowAt` → `PuzzleInventoryPresenter.TryGetBoard`). 그 창에 보드가 없으면(제목줄·보드 없는 창이 덮음) 뒤 창의 보드로 넘기지 않는다. 부동 창이 없는 옛 UI는 기존 고정 순서(화차 → 시장 → 의뢰 게시판 → 작업장) | 2026-11-20 술집·시장 창이 겹치면 뒤 창 보드로 들어감 |
| R51 | Ctrl+클릭 화물 이동의 목적지는 마지막으로 포커스한 창이다: 화물이 있던 창을 빼고 보드가 있는 창 중 가장 위 창(`FloatingWindowManager.OpenWindowsTopFirst`). 화차 화물은 그 창(다른 화차·시장·작업장·의뢰 게시판)으로만 가고, 그 창이 받을 수 없으면 다른 창으로 넘기지 않는다(의뢰 게시판이면 의뢰 화물 완료·취소). 화차가 아닌 보드(시장·작업장·의뢰 게시판)의 화물은 가장 최근에 포커스한 화차 창으로 간다. 의뢰 게시판 화물은 Ctrl+클릭으로 수락해 그 화차부터 싣는다(자리 없으면 다른 화차) | 2026-11-20 사용자 요청: 마지막으로 포커스한 창으로 |
| R52 | 의뢰 취소: 의뢰를 받은 방문 동안(시장 되사기처럼 그 역을 떠나기 전까지, 다시 찾아와서는 불가) 화차의 의뢰 화물을 의뢰 게시판(술집 9×5 격자)에 내려놓으면 취소한다(`PuzzleInventoryService.TryReturnQuestCargo`/`TryCancelQuest`). 화물은 내리고 보증금은 돌려주며 지역 불이익은 없다. 그 의뢰는 게시판의 내려놓은 자리(안 맞으면 빈자리)에 다시 걸린다. 게시판은 칸 밖 여백을 포함해 영역 어디에 놓아도 받는다(`PuzzleInventoryView.TryGetBoardCellClamped`). 목적지 도시에서 놓으면 지금처럼 완료, 그 밖의 도시는 거부 | 2026-11-20 사용자 요청 |
| R53 | 의뢰 게시판(술집 9×5 격자) 안에서 의뢰 화물을 자유롭게 옮기고 돌려 놓을 수 있다(`PuzzleInventory.Move` 같은 보드). 수락·취소로 몇 개만 빠지거나 다시 걸리면 나머지 의뢰 자리는 그대로 두고 바뀐 것만 빼거나 빈자리에 놓는다(`TryUpdateQuestOfferBoard`). 새 방문처럼 겹치는 의뢰가 없을 때만 새로 배치한다 | 2026-11-20 사용자 요청 |
| R54 | 게임을 시작하면 마을 창(시장·술집·여관·역사·생산시설·시청)과 렌즈 창은 닫힌 채로 시작한다(`RelayoutRootController.Start`). 시장은 도착 처리로 입장한 경우만 열어 둔다. 화차 창·수첩 같은 개인 창은 저장된 열림 상태를 되살린다 | 2026-11-20 시작 시 지난 실행의 창이 전부 열림 |
| R55 | 수첩 지도 크게 보기 창은 화면에서 좌우 40px, 위아래 112px를 뺀 크기로 연다(최소 1084×780). 지도 화면·범례·아래 줄은 그 크기에 맞춰 늘리고, 연 뒤 화면이 작아지면 비율로 줄인다 | 2026-11-20 사용자 요청: 크게 보기 창을 더 크게 |
| R56 | 창 시작 위치 규칙 하나로 통일(`FloatingWindowManager.FindOpenPosition`, H10·R12·R29 정리): 사용자가 끌어 옮긴 창은 그 자리를 저장해 다시 열어도 지킨다(`SavedWindowState.moved`). 옮긴 적 없는 창은 열 때마다 ① 기본 위치(마을 창은 마을 영역 오른쪽 위에 오른쪽 끝 맞춤, 개인 창은 빌더의 defaultPosition: 화차 슬롯·렌즈·수첩)가 비어 있으면 거기 ② 아니면 자기 영역(개인: 왼쪽 플랫폼 x16~540, 마을: x653~1900, y134~)에서 다른 열린 창과 겹치지 않는 첫 자리(24px 격자, 마을은 오른쪽부터·개인은 왼쪽부터, 개인 창은 왼쪽이 차면 마을 영역도) ③ 그래도 없으면 24px 계단(마을은 왼쪽 아래, 개인은 오른쪽 아래). 화차 창의 별도 슬롯 계산(`ComputeNextWindowPosition`)은 이 규칙으로 대체. 창 배치 초기화는 옮긴 표시를 풀고 열린 창을 아래부터 다시 놓는다. 이전 저장값(옮긴 표시 없음)은 한 번 기본 규칙으로 돌아간다 | 2026-11-20 사용자 요청: 창 시작 위치가 제각각 |
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

신규 `Assets/Scripts/Runtime/UI/Windowing/` 아래 2개 핵심 컴포넌트(`FloatingWindow`, `FloatingWindowManager`) + `FloatingWindowDragHandle`을 둔다.

- **`FloatingWindow.cs`** (모든 창 루트 프리팹에 부착, **고정 크기 — H21에 의해 H13의 크기 조정 부분 철회**):
  - `[SerializeField] string windowId`, `RectTransform titleBar`, `Button closeButton`, `Vector2 defaultPosition`, `Vector2 defaultSize`, `bool startOpen`.
  - **제목줄 드래그**: `titleBar`에 부착된 `FloatingWindowDragHandle`(`IBeginDragHandler`, `IDragHandler`)가 `RectTransform.anchoredPosition`을 이동.
  - **화면·HUD 클램프**: `1920×1080` 기준 `x: [0, 1920 - w]`, `y: [76, 1080 - h]`로 제한해 상단 `TopHUD`(`y=12..68`)를 침범하지 못하게 함(R10). 중앙 선로(`549..640`)나 마을 위로 옮기는 것은 허용하되 기본 위치는 H10을 따름.
  - **최상위 승격**: `IPointerDownHandler`에서 `transform.SetAsLastSibling()` 호출.
  - **12px 스냅**: 드래그 중 화면 4변(`0, 1920, 76, 1080`) 및 다른 열린 `FloatingWindow` 외곽선과 `12px` 이내로 근접하면 자석처럼 붙임.
  - **영속성(PlayerPrefs)**: `EndDrag` / `SetOpen` 시 `windowId` 키(`ui.win.<windowId>`)로 `{x, y, open}` JSON 저장(구버전 저장값의 `w, h`는 무시하고 `defaultSize` 사용).
- **`FloatingWindowManager.cs`** (`FloatingWindowCanvas`에 부착):
  - 씬 내 모든 `FloatingWindow` 등록, 스냅 대상 목록 제공, `ResetAllLayouts()`(H10 기본 좌표로 복구), `ToggleBackgroundOnly()`(열린 창 일괄 숨김/복원), `TryConsumeCancel()`(가장 위(`GetSiblingIndex()` 최대)에 있는 열린 창 닫기 → 없으면 `PauseModal`로 넘김).

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
>
> **S2 구현 결과 (2026-11-19):** `RelayoutSettings`(R21, `ui.relayout.enabled` 기본 0), `RelayoutRootController`, `TopHudView`(`PuzzleCityView` HUD 분리 및 기존 게이지/평판 재사용), `QuickToolbarView`, `StatusBannerView`, `PlatformSceneView`, `TownSceneView` + `TownBuildingButton` 6종(시장·술집·여관·역사·생산시설·시청, 기존 역 행동 연결 및 `1~6` 단축키), `PauseMenu` 개발용 토글, `RelayoutS2Builder.cs`(`Tools/MyJG/UI/Relayout/S2 Build Fixed Frame` / `Toggle Relayout`) 구현 및 스모크 캡처(`.orca/analysis/relayout/s2/`) 확인 완료(콘솔 에러 0).
>
> **S3 구현 결과 (2026-11-19):** 중앙 선로 가상화 리스트(`RailStripView` + `RailWagonCarView` 14슬롯 풀, R8 2톤 미니 격자, R16), 기차 화차 부동 창(`TrainWindowView` + `FloatingWindow`, R14 2줄 렌즈 요약), `PuzzleInventoryView` 동적 셀 크기(`40~72px` + `ScrollRect`, R11) 및 `PuzzleInventoryPresenter` `ActiveTrainView` 전환/드래그 호버(`SwitchWagonDuringDrag`) 연동, `RelayoutS3Builder.cs`(`Tools/MyJG/UI/Relayout/S3 Build Rail And Train`) 구현 완료. 3량/11량 스크롤·소/대 리사이즈(40px/72px)·리사이즈(54.4px) 상태 실드롭(`(2,2)` 일치)·미니 격자 갱신·OFF 보존 캡처(`.orca/analysis/relayout/s3/`) 및 콘솔 에러 0건 확인.
>
> **S4 구현 결과 (2026-11-19):** S3 잔여 이슈(`TrainWindow` 내 `LocomotiveArtwork` 숨김) 수정 및 5탭 수첩 부동 창(`NotebookWindowView` + `MapDestinationPinView` 16개 풀 + `NotebookWindow.prefab`, 기본 `(16,128,522,928)`), `PuzzleWorldMap`/`PuzzleTradeNotebookView`/`PuzzleSynergyChartView`(불투명 배경 + `ScrollRect` + `RectMask2D`, 중첩 `Canvas` 제거) 임베드, `QuickToolbarView`(`수첩(N)`/`노선도(M)`)·`EntityLinkNavigation`·`PuzzleInventoryPresenter`(R6 마지막 선택 화물 핀) 연동, `RelayoutS4Builder.cs`(`Tools/MyJG/UI/Relayout/S4 Build Notebook Window`) 구현 완료. 5탭 전환·방문/소문 핀·검색/경로 계획·소/대 리사이즈(420×520, 900×920)·OFF 구형 수첩/지도 보존 캡처(`.orca/analysis/relayout/s4/`) 및 콘솔 에러 0건 확인.
>
> **H21 리사이즈 제거 (2026-11-19):** H13의 창 크기 조정 부분 철회에 따라 `FloatingWindowResizeHandle` 삭제, `FloatingWindow`의 `minSize`/`maxSize`/`Resized`/`BeginResize`/`UpdateResize`/`EndResize` 및 `w, h` 저장 제거(기존 PlayerPrefs의 `w, h`는 무시하고 `x, y, open`만 유지), `RelayoutS3Builder`/`RelayoutS4Builder`의 리사이즈 핸들 제거, `PuzzleInventoryView` 보드 셀 크기 고정(기본 사각형 기준 40~72px 1회 계산, 리사이즈 재계산 및 보드 스크롤 모드 제거) 반영 완료.
>
> **S3b 구현 결과 (2026-11-19):** S4 잔여 이슈(`NotebookWindow` 제목 `'수첩'` 변경, `MapDestinationPinView` 1px 리더 라인 + 수직 스태킹으로 겹침 방지) 수정 완료. `ui.maxTrainWindows`(기본 16) 기반 다중 화차 창(`TrainWindowManager` + `TrainWindow_0..15`, 24px 계단식 오프셋, LRU 교체, 창별/열린 화차 영속성, `PuzzleInventoryPresenter`/렌즈 다중 보드 라우팅, H18/R25), 선로 드래그 순서 변경(삽입선·상하 자동 스크롤·`TryMoveWagon`, H20), 선로 화차 → 마을 `역사` 드래그 판매(`PreviewWagonSale`/`TrySellWagon`, H17/R24) 및 S6 구매 드롭 타깃(`TryDropPurchasedWagonAtScreenPosition`) 구현 완료. Play Mode에서 3창 동시 오픈·창 A→창 B 실드롭 이동(`piece.verticalPair`, `w0:4->2, w1:0->2`)·드래그 순서 변경(`1,2->2,1`)·`역사` 드래그 판매(`3->2`, `+120$`)·LRU 교체(`maxWin=2` 시 `[0,2]` 유지)·Relayout OFF 보존·콘솔 에러 0건 확인(`.orca/analysis/relayout/s3b/`).
>
> **S3b 리뷰 수정 (2026-11-19):** R29 화차 창 기본 크기(440×430, 7×5 보드 56px 칸) 및 1~8번 비겹침 슬롯 배치(이후 24px 계단) 적용, `TrainWindowManager`의 인덱스 기반 바인딩을 `PuzzleInventory` 참조 기반으로 전환(화차당 최대 1창·이동 추적·판매 시 자동 닫힘 및 `RenderValues`/`RefreshTrainBar` 다중 창 보호), `RailWagonCarView`/`RailStripView` 드래그 시작 화차 캡처 및 자동 스크롤 시 첫 표시 인덱스 변경 시에만 풀 재바인딩, LRU 교체 시 드래그 출발/대상 창 보호 및 보류, 창 이동 시 이전 창 배치 후보 숨김 및 새 대상 창 `UpdateLensRanking` 재계산, 구매 드롭 시 `purchasedWagon` 참조 기준 창 열기, 열림 영속성 `RunId`+`WagonNumber` 키잉 및 변경 시마다 저장, `EventCamera` 사용 및 `relayoutTrainView` 초기화 보장을 반영하고 Play Mode 재검증(`fix_*.png`, 콘솔 에러 0건) 완료.
>
> **S5 구현 결과 (2026-11-19):** `MarketWindowView` + `MarketWindow.prefab`(1136, 96, 764×640 고정 크기, 상단 `CityTradePreferencesView` 시세 띠, 13×9 보드 56px 칸, 시장 화물 구매가 및 기차 화물 시장 위 드래그 판매가 라벨, 하단 조작 안내, 도착 시 `TryEnterMarket` 연동 자동 오픈(R5)·야간 차단/상태 배너 안내), `LensWindowView` + `LensWindow.prefab`(26, 134, 510×366 고정 크기, `현재/방문 도시/소문 도시` 탭 및 공개 규칙 준수, 도구줄 `렌즈 (L)` 및 화차 창 헤더 `렌즈` 버튼 토글), 배치 후보 표식 H16 채움 사각형 교정(양수 Δ만 표시), `PuzzleItemTooltipView` H16 직각/단색 스타일 전환(OFF 시 원복), `RelayoutS5Builder.cs`(`Tools/MyJG/UI/Relayout/S5 Build Market And Lens`) 구현 완료. Play Mode에서 도착 비겹침 오픈·실드롭/Ctrl+클릭 양방향 거래 금액 일치·방문/소문 탭 공개 규칙·양수 Δ 후보 필터링·H16 툴팁·야간 차단·Relayout OFF 보존·콘솔 에러 0건 확인(`.orca/analysis/relayout/s5/`).
>
> **S6 구현 결과 (2026-11-19):** S5 잔여 `MarketInfoPanel` 우측 돌출 수정(`RelayoutS5Builder` 스트레치 적용) 완료. **서비스 의뢰 규칙 변경**: `PuzzleInventory.CanPlaceAt`/`TryPlaceAt`/`CreatePoseAtSlot` 및 `PuzzleInventoryService`의 `EvaluateQuestAcceptAt`/`TryAcceptQuestAt`, `CanCompleteQuestCargoHere`/`TryCompleteQuestWithCargo`/`TryCompleteQuest`, `BuildQuestOfferBoard` 추가, 도착 시 전역 자동 완료 제거 및 최종 결산(`Settle()`) 직전 `CompleteArrivedQuests()` 실행(R26), OFF UI 최소 완료 경로(`TrainventoryQuestBoardPresenter` `완료` 버튼) 추가. **마을 창 5종 + 이동 배너**: `TavernWindowView`(소문 + 9×5 의뢰 게시판 드래그 수락·인도·거절 사유 배너), `RestWindowView`(1~4턴 휴식 + 아침까지 대기 + 결산 미리보기), `StationHallWindowView`(화차 드래그 선로 삽입 구매 + 판매 드롭 구역 + 노선도 출발 버튼), `WorkshopWindowView`(R17 단일 창 도시 작업장 전환·공터 차단), `CityHallWindowView`(물가·선호·산업·평판·결산 목표 읽기 전용), `TravelBannerView`(출발 확인 및 이동 진행 배너), `RelayoutRootController`/`PuzzleInventoryPresenter`/`PuzzleMapPresenter` 연동 및 `RelayoutS6Builder.cs`(`Tools/MyJG/UI/Relayout/S6 Build Town Windows`) 구현. Play Mode에서 시장 폭 수정·6종 창/배너·의뢰 수락/미완료 도착/인도/비의뢰 거절·2턴 휴식(+12h)·화차 드래그 구매/판매·작업장 공터 차단·Relayout OFF 보존·콘솔 에러 0건 확인(`.orca/analysis/relayout/s6/`).
>
> **S7 구현 결과 (2026-11-19):**
> - **마을 건물 1줄 현황(`TownSceneView`/`TownBuildingButton`/`RelayoutS2Builder`)**: 시장(`물가 n% · 선호 m종` / `영업 종료(밤)`), 술집(`새 소문 · 의뢰 n건` / 완료 가능 의뢰 보유 시 `완료 가능 의뢰 n건` + `SetPersistentHighlight(true)` 테두리 강조), 여관(`휴식 1~4턴`), 역사(`화차 n/최대 · 출발`), 생산시설(작업장명·수수료 / 공터 디밍), 시청(`평판 n · 목표 진행 x%`)을 관련 이벤트(`TimeAdvanced`, `CityArrived`, `FundsChanged`, `QuestChanged`, `TavernRumorRecorded`, `PuzzleActionCommitted`, `PuzzleRun*`)로 갱신(매 프레임 폴링 없음).
> - **기본 레이아웃 전환(R30)**: `RelayoutSettings.Enabled` 기본값을 `1`로 전환(`ui.*` PlayerPrefs 초기화 시 신규 레이아웃 기본 진입, 일시정지 토글로 구형 전환·복귀 가능).
> - **문서 현행화**: `Docs/Management/UI_LAYOUT.md`, `CURRENT_STATE.md`, `HUD구성및플로우.md`, `UI_DESIGN_HANDOFF.md`를 구현 상태에 맞게 갱신하고 구형 개편 안내 배너 제거(`Docs/Legacy/` 미수정).
> - **남은 일**:
>   1. **구형 UI 삭제(별도 후속)**: `GameplayLayout` 및 구형 역/하단 바/구형 지도 코드·프리팹 정리.
>   2. **팔레트 최종 선택**: 현재 `UIPalette.asset`은 팔레트 A 임시 적용 상태(사용자 최종 선택 대기).
>   3. **플레이스홀더 아트 교체**: `PlatformScene`·`TownScene` 배경 및 건물 임시 사각형 아트를 본 스프라이트로 교체.

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
