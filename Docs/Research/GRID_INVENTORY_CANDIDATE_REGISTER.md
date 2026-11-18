# 그리드 인벤토리 UI — 후보 레지스터 (R1)

> 조사일: 2026-11-18 · 대상: PC 마우스·키보드 · 상태: **1차 선별 완료 / MobyGames 등재 대조 및 화면 심층 검증 미완료**  
> 범위: 그리드 관련 후보 30개 + 비그리드 D 비교군 3개, 심층 제안 15개. 런타임·프리팹 변경 없음.

## 1. 조사 방법과 증거 한계

기준은 [조사 계획 §4.3–4.4](GRID_INVENTORY_LAYOUT_RESEARCH_PLAN.md)다. 다음 기존 문서를 후보 발견과 중복 확인에만 사용했다.

- [ARPG](GRID_INVENTORY_ARPG.md), [INDIE](GRID_INVENTORY_INDIE.md), [SURVIVAL](GRID_INVENTORY_SURVIVAL.md)
- [백팩 덱빌더](BACKPACK_DECKBUILDER_UI_RESEARCH.md), [무역 게임](TRADE_GAME_UI_RESEARCH.md)

### MobyGames 접근 결과

시작 URL: <https://www.mobygames.com/group/13289/gameplay-feature-grid-inventory/>

2026-11-18 HTTP 요청 결과는 **403 Forbidden / Cloudflare challenge**였다. 목록 본문·페이지네이션·개별 게임 등재는 확인하지 못했다. 따라서 아래 30개를 **“MobyGames 그룹에서 추출·등재 확인한 30개”라고 부르지 않는다**. 기존 문서에서 후보를 추출하고, 접근 가능한 공식 매뉴얼·Steam 상품 API로 제목·버전·일부 기능을 보완한 대체 모집단이다. MobyGames 그룹 포함 여부는 전 행 **미확인**이며 개별 MobyGames URL을 추측해 만들지 않았다.

### 증거 등급과 점수의 의미

- **E1**: 이번 실행에서 접근한 공식 문서/상품 설명에 해당 기능을 직접 서술. 게임 존재·발매일 확인과 기능 확인을 구분한다.
- **E2**: 기존 조사 문서의 기능 서술만 존재. 이번 실행에서 해당 기능을 독립 재검증하지 못함. **잠정 점수**다.
- **U**: 존재 확인만 가능하거나 기존 설명이 불명확·상충. 점수는 선별용 보수적 하한이며 기능 부재의 입증이 아니다.
- Steam 상품 API의 제목·발매일·플랫폼과 설명문을 실제 읽었다. **상품 페이지 URL 또는 스크린샷 메타데이터만으로 화면 상태를 관찰했다고 간주하지 않았다.** 이번 실행에서 게임 실행, 드래그 영상, 해상도 실측은 하지 않았다.
- 여섯 점수는 `공간 / 이동 / 거래 / 시너지 / 목적지 / 화면`, 각각 **0 없음 / 1 부분적 / 2 핵심**의 계획상 척도를 쓴다. 다만 E2/U의 0은 “현재 증거로 가점 불가”일 수 있으며, 이를 실제 부재와 혼동하지 않는다. 1점도 화면·기능 확정이 아니라 검증 가설일 수 있다.
- 특히 **화면 점수는 전부 잠정**이다. 두 컨테이너를 다루더라도 1920×1080에서 두 보드·가격·설명을 동시에 보는지는 R2에서 검증해야 한다. 화면 확정 점수 2는 주지 않았다.
- 총점 ≥7을 우선하되 A/C/D의 독자적 비교 가치는 예외로 남긴다. 점수는 검증 우선순위이지 최종 레이아웃 평가가 아니다.

군: **A** 공간 덱빌딩·직접 유사 / **B** 두 그리드 이동·거래 / **C** 순수 공간 퍼즐 / **D** 가격·목적지 판단. B에는 해당 패턴의 검증 후보와 근거 부족 보류작도 포함한다.

## 2. 30개 그리드 관련 후보의 1차 선별

버전/연도는 비교 대상으로 삼을 버전이다. Steam 이식·정식 출시일을 원작 발매일로 바꾸지 않았다. `★`는 심층 제안, `조건부`는 화면·기능 근거 확보 후 확정이다. 출처 링크의 확인일은 모두 2026-11-18이다.

| ID | 게임 · 비교 버전 / 플랫폼 | 군 | 공간 | 이동 | 거래 | 시너지 | 목적지 | 화면 | 합 | 판단 · 선정/보류 이유 | 근거 URL / 신뢰도 · 미확인점 |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| G01 | Backpack Battles · 정식(2025) / PC | A | 2 | 1 | 2 | 2 | 0 | 1 | 8 | ★ 상점 구매와 배치 빌드를 연결하는 직접 유사군 | [Steam](https://store.steampowered.com/app/2427700/) E1 구매·제작·배낭 배치; 시너지/두 보드 형태 E2. 상점은 동일 격자라는 전제 금지 |
| G02 | Backpack Hero · 정식(2023) / PC | A | 2 | 1 | 1 | 2 | 0 | 1 | 7 | ★ 위치가 성능을 바꾸는 원리와 상세 설명 비교 | [Steam](https://store.steampowered.com/app/1970580/) E1 “Placement has a huge impact”; 상점 동시 표시·회전 입력 E2 |
| G03 | NEODUEL: Backpack Monsters · 2024 / PC | A | 2 | 1 | 1 | 2 | 0 | 1 | 7 | ★ 조건부: 백팩 콤보·수집형 배치 비교 | [Steam](https://store.steampowered.com/app/2693940/) E1 전략적 배낭·콤보; 공간 모양·상점·인접의 정확한 조건 E2 |
| G04 | God Of Weapons · 2023 / PC | A | 2 | 1 | 1 | 1 | 0 | 1 | 6 | ★ 조건부·점수 예외: 제한 공간의 무기 빌드; A 네 번째 보완 | [Steam](https://store.steampowered.com/app/2342950/) E1 제한된 인벤토리·전략적 정리; 인접 시너지·구매/회전 방식 U. 단순 동시 소지 보너스를 인접으로 단정하지 않음 |
| G05 | DREDGE · 기본 게임(2023) / PC | B | 2 | 2 | 2 | 0 | 1 | 1 | 8 | ★ 어획물 적재→항구 판매와 시간 가치; 직접 경제 루프 보완 | [Steam](https://store.steampowered.com/app/1562430/) E1 어획 판매·선박 업그레이드; 격자/보관 이동 E2. 부패와 항구 가격 차이는 별도 검증 |
| G06 | ZERO Sievert · 정식(2024) / PC | B | 2 | 2 | 2 | 0 | 0 | 1 | 7 | ★ 전리품·창고·상인의 반복 이동 | [Steam](https://store.steampowered.com/app/1782120/) E1 상인·전리품 거래; 격자·정렬·탭 수 E2. 기존 2022 EA 서술과 정식판을 혼합하지 않음 |
| G07 | Escape from Tarkov · PC 라이브, 빌드 미확인 | B | 2 | 2 | 2 | 0 | 0 | 1 | 7 | ★ 조건부: 중첩 컨테이너와 상인 거래 비교 | [공식](https://www.escapefromtarkov.com/) / [위키 거래](https://escapefromtarkov.fandom.com/wiki/Trading) E2; 위키 403. 버전·정렬 지원·확정 시점 미검증 |
| G08 | SCUM · 정식(2025) / PC | B | 2 | 2 | 1 | 0 | 0 | 1 | 6 | ★ 조건부·점수 예외: 칸/중량의 실패 사유 비교 | [Steam](https://store.steampowered.com/app/513710/) 존재·정식일 E1; [규격표](https://scum.fandom.com/wiki/Storages_Tables) 403, 기능 E2. 상인과 두 그리드 동시 표시 미확인 |
| G09 | DayZ · PC 라이브, 빌드 미확인 | B | 2 | 2 | 0 | 0 | 0 | 1 | 5 | ★ 조건부·점수 예외: Vicinity↔컨테이너 이동 전용 비교 | [Steam](https://store.steampowered.com/app/221100/) 존재 E1; [인벤토리](https://dayz.fandom.com/wiki/Inventory) 403, 기능 E2. 바닐라 상인 거래를 가정하지 않음 |
| G10 | Resident Evil 4 · 리메이크(2023) / PC, 공식 매뉴얼은 PS5 | B | 2 | 1 | 2 | 0 | 0 | 1 | 6 | ★ 점수 예외: 구매·케이스·보관의 서로 다른 확정 단계 | [Steam](https://store.steampowered.com/app/2050650/) / [공식 매뉴얼 데이터](https://game.capcom.com/manual/re4/locale/data/en.json) E1 케이스 정리·보관 전송·매수/매도. 상점은 두 격자라고 확인되지 않음; PC 입력 미확인 |
| G11 | Resident Evil 4 · 2005 원작, PC HD 이식(2014) | B | 2 | 1 | 2 | 0 | 0 | 1 | 6 | 보류: G10과 같은 시리즈, 개편 차이가 필요한 경우에만 비교 | [Steam](https://store.steampowered.com/app/254700/) 존재·이식일 E1; 케이스·거래 E2. 심층 표본 수에 중복 산입하지 않음 |
| G12 | Resident Evil Village · 2021 / PC, 공식 매뉴얼은 PS5 | B | 2 | 1 | 2 | 0 | 0 | 1 | 6 | 예비: G10 대체 가능한 회전·상인 사례 | [Steam](https://store.steampowered.com/app/1196590/) / [공식 데이터](https://game.capcom.com/manual/village/locale/data/en.json) E1 90° 회전·Duke 구매/판매. 콘솔 키 표기를 PC 키로 전용하지 않음 |
| G13 | Diablo II · Lord of Destruction(2001) / PC | B | 2 | 2 | 1 | 0 | 0 | 1 | 6 | ★ 점수 예외: 작은 외부 점유와 큰 내부 컨테이너의 관계 | [Arreat Summit: Cube](https://classic.battle.net/diablo2exp/items/cube.shtml) E1 2×2 점유·드래그 이동·중첩 금지; 거래 화면 E2. D2R 창고 규격을 원작에 소급하지 않음 |
| G14 | Diablo · 원작(1996) / PC | B | 2 | 0 | 1 | 0 | 0 | 1 | 4 | 보류: 고전 최소형, G13보다 직접 비교 정보가 적음 | [기존 ARPG §1](GRID_INVENTORY_ARPG.md#1-diablo-1996-blizzard-north) E2. 정확한 점유 규격·상인 UI 미검증 |
| G15 | Grim Dawn · 기본 게임(2016), 빌드 미확인 / PC | B | 0 | 1 | 1 | 0 | 0 | 0 | 2 | 보류: 기존 “모두 1×1” 서술을 재검증해야 함 | [Steam](https://store.steampowered.com/app/219990/) 존재 E1; 기능 U. 추측으로 공간 점수 복구하지 않음 |
| G16 | Titan Quest Anniversary Edition · 2016 / PC | B | 2 | 2 | 1 | 0 | 0 | 1 | 6 | 예비: 다중 가방·창고; A/B 핵심 표본보다 우선순위 낮음 | [Steam](https://store.steampowered.com/app/475150/) E1 larger stash·UI scaling; 가방 규격·정렬 도입시기 E2 |
| G17 | Last Epoch · 정식(2024), 빌드 미확인 / PC | B | 2 | 2 | 1 | 0 | 0 | 1 | 6 | 예비: 검색/창고 비교용, 가격·목적지 질문에는 약함 | [Steam](https://store.steampowered.com/app/899770/) 존재 E1; 격자/이동 E2. 현재 거래 파벌·상점 상태 미검증 |
| G18 | Path of Exile · 1편(2013), 리그 미확인 / PC | B | 2 | 2 | 1 | 0 | 0 | 1 | 6 | 예비: 창고·전용 탭; 2편과 혼합 금지 | [Inventory](https://www.poewiki.net/wiki/Inventory) 봇 확인 페이지(본문 불가), [기존 ARPG §12](GRID_INVENTORY_ARPG.md) E2. Steam API app 238960은 이번 요청에서 데이터 없음 |
| G19 | Sacred 2 Gold · Fallen Angel+Ice and Blood / PC | B | 2 | 1 | 1 | 0 | 0 | 1 | 5 | 보류: 탭 분류·정렬의 추가 비교가 필요할 때만 | [Steam](https://store.steampowered.com/app/225640/) 판본 E1; 기능 E2. Sacred 1의 빠른 이동을 2에 적용하지 않음 |
| G20 | NEO Scavenger · 2014 / PC | B | 2 | 2 | 1 | 0 | 0 | 1 | 6 | 1순위 예비: G08/G09 근거 실패 시 대체; 컨테이너 증거가 명확 | [Steam](https://store.steampowered.com/app/248860/) E1 “Fit items in grid spaces”, containers·encumbrance; 상인·회전 입력 E2 |
| G21 | System Shock 2 · 1999 원작의 Steam 이식 / PC | B | 2 | 1 | 0 | 0 | 0 | 1 | 4 | 보류: 성장형 공간 제한; 현재 거래 질문과 거리 있음 | [Steam](https://store.steampowered.com/app/238210/) 원작 판본 E1; 기능 E2. 리마스터와 혼합 금지 |
| G22 | Deus Ex: GOTY · 원작 기반 / PC | B | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 근거 부족 보류: 제목만 확인, 격자 세부 채점 유보 | [Steam](https://store.steampowered.com/app/6910/) 존재 E1; 기능 U. Human Revolution 등 후속작 정보 혼합 금지 |
| G23 | Unturned · 정식(2017), 빌드 미확인 / PC | B | 0 | 0 | 1 | 0 | 0 | 0 | 1 | 근거 부족 보류: NPC trade 문구만 이번에 확인 | [Steam](https://store.steampowered.com/app/304930/) E1 NPC 거래; [Inventory](https://unturned.wiki.gg/wiki/Inventory) 403. 격자 기능 U |
| G24 | Dungeon Siege · 원작(2002), Steam 이식 / PC | B | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 근거 부족 보류: 컨테이너 화면 근거 확보 전 제외 | [Steam](https://store.steampowered.com/app/39190/) 존재 E1, 기능 U. Steam 표시 2011을 원작 연도로 쓰지 않음 |
| G25 | Dungeon Siege II · 2005 / PC | B | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 근거 부족 보류: G24와 같은 계보, 대표작 하나만 후속 검증 | [Steam](https://store.steampowered.com/app/39200/) 존재 E1, 기능 U |
| G26 | Stoneshard · 2020 EA 시작, 현재 빌드 미확인 / PC | B | 0 | 0 | 1 | 0 | 1 | 0 | 2 | 예비 보류: caravan·지역 교역 문구는 유망하나 화면 미확인 | [Steam](https://store.steampowered.com/app/625960/) E1 caravan·“trade in a variety of goods”; 격자/도시별 가격 U |
| G27 | Marauders · 2022 EA 시작, 현재 빌드 미확인 / PC | B | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 근거 부족 보류: extraction이라는 장르만으로 격자 추정 금지 | [Steam](https://store.steampowered.com/app/1789480/) 존재·looter shooter E1; 거래·그리드 U |
| G28 | 7,62 Hard Life · 2015 / PC | B | 0 | 0 | 1 | 0 | 0 | 1 | 2 | 보류: 유한 상인 현금·와이드 화면만 직접 확인 | [Steam](https://store.steampowered.com/app/306290/) E1 trader cash 제한·widescreen adaptation; 그리드·두 보드 U. High Calibre 원본과 구분 |
| G29 | Kenshi · 정식(2018) / PC | B | 0 | 0 | 1 | 0 | 0 | 0 | 1 | 예비 보류: 무역·상점 재고 설명은 있으나 칸 배치 독립 근거 부족 | [Steam](https://store.steampowered.com/app/233860/) E1 trader·shops inventory; 그리드·지역별 가치 U |
| G30 | Save Room - Organization Puzzle · 2022 / PC | C | 2 | 1 | 0 | 0 | 0 | 1 | 4 | ★ 점수 예외: 경제 정보 없는 순수 적합성 비교 기준 | [Steam](https://store.steampowered.com/app/1955330/) E1 “make all the items fit”; 두 영역·회전·무효 드롭 피드백은 화면 확인 필요 |

**집계:** 30행 = A 4 / B 25 / C 1. 존재만 확인한 U 후보를 유효 그리드 사례로 확정하지 않았으므로, “검증된 그리드 게임 30개” 달성으로 보고할 수 없다. 특히 G22–G29는 추가 화면/매뉴얼을 찾거나 교체해야 한다.

## 3. D군 — 비그리드 외부 비교군 3개

이 세 게임은 MobyGames 그리드 모집단 실적으로 세지 않는다. 공간 퍼즐과 목적지 가격 판단을 한 게임이 모두 해결한다는 가설 대신, 다른 UI 패턴을 조합하기 위한 의도적 외부 표본이다.

| ID | 게임 · 버전 / 플랫폼 | 군 | 공간 | 이동 | 거래 | 시너지 | 목적지 | 화면 | 합 | 선정 이유 / 출처 및 한계 |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| D01 | Merchant of the Skies · 정식(2020) / PC | D | 0 | 0 | 2 | 0 | 2 | 1 | 5 | ★ [Steam](https://store.steampowered.com/app/1040070/) E1 trade routes·섬 간 이동·자동 운송. 기존 2019 표기는 정식일과 구분. 가격 표 열·Max 조작·정확한 정보 공개 범위 E2 |
| D02 | Patrician IV · 기본 게임(2010) / PC | D | 0 | 0 | 2 | 0 | 2 | 1 | 5 | ★ [Steam](https://store.steampowered.com/app/57620/) E1 merchant fleet·trade routes·수요. 도시 공급 지표·가격 상한 규칙·슬라이더 E2. Special Edition/확장과 기본판 혼합 금지 |
| D03 | X4: Foundations · 기본 게임(2018), 빌드 미확인 / PC | D | 0 | 0 | 2 | 0 | 2 | 1 | 5 | ★ [Steam](https://store.steampowered.com/app/392160/) E1 simulated economy·trade networks·production chains. 지도 가격 오버레이·정보 갱신 조건 E2. DLC·패치·모드 여부를 캡처마다 구분 |

D군 세 사례 모두 총점 예외다. 필요한 것은 인벤토리 모양이 아니라 **현재 가격 / 알고 있는 타 도시 기회 / 이동 비용 / 정보 신선도**를 분리해 읽는 패턴이다. 운임·시간·위험을 같은 화면에서 모두 비교할 수 있다고 아직 주장하지 않는다.

## 4. 심층 표본 제안 — 15개, 확정이 아닌 검증 큐

A 4 + B 7 + C 1 + D 3 = **15개**. 같은 시리즈의 유사 UI를 중복 산입하지 않는다. 후보 수를 맞추려고 조건부 사례를 관찰 완료로 바꾸지 않는다.

| 순서 | ID / 게임 | 다음 조사에서 먼저 볼 화면 상태 | 이번 프로젝트에서 얻을 답 | 선정 위험 / 교체 규칙 |
|---:|---|---|---|---|
| 1 | G01 Backpack Battles | 평상시 상점 → 선택 → 구매 전 배치 → 거래 직후 | 구매와 인접 영향의 동시 판독 | 상점이 격자인지 별도 기록; 양쪽 모두 그리드라고 가정 금지 |
| 2 | G02 Backpack Hero | 선택 → 드래그 → 인접 효과 변화 → 상점 | 위치에 따른 성능 변화를 설명하는 최소 정보 | 상점/전투 화면을 합성해 한 화면인 것처럼 제시 금지 |
| 3 | G03 NEODUEL | 정비 → 선택 → 콤보/융합 미리보기 | 몬스터와 아이템의 연결·영향 대상 표시 | 정확한 인접 조건·시장 화면이 없으면 A 대체작 탐색 |
| 4 | G04 God Of Weapons | 획득/상점 → 정리 → 드롭 전 | 공간 압박과 빌드 가치의 정보 우선순위 | 인접이 없으면 위치 시너지 사례로 세지 않음; A 네 번째 미충족 공개 |
| 5 | G05 DREDGE | 선창 ↔ 보관 → 판매 → 목적지/시간 | 적재 공간·판매·품질/시간의 연결 | 정박지 가격 차이와 부패는 다른 변수로 측정 |
| 6 | G06 ZERO Sievert | 창고 ↔ 가방 → 상인 → 실패 드롭 | 두 보드 거리, 거래 전후 자금·재고 피드백 | 정렬에 대한 과거 커뮤니티 평을 현 버전 사실로 인용 금지 |
| 7 | G07 Escape from Tarkov | 상인/창고 ↔ 가방 → 드래그 → 거래 확정 | 컨테이너 계층과 거래 확정 경계 | 버전·화면 확보 실패 시 G20 NEO Scavenger 대체 |
| 8 | G08 SCUM | 컨테이너 ↔ 컨테이너 → 공간/무게 실패 | 빈칸이 있어도 못 넣는 이유의 중복 표현 | 근거 실패 시 G20 대체; 중량을 현 제품 새 규칙으로 제안하지 않음 |
| 9 | G09 DayZ | Vicinity ↔ 의복/가방 → 회전 → 실패 | 주변/내 소유 구분과 다중 컨테이너 탐색 | 바닐라 PC만; 거래 모드를 기본 게임 증거로 사용 금지 |
| 10 | G10 RE4 리메이크 | 케이스 → 상인 → 보관 → 확정 직후 | 공간 미리보기와 금전 확정의 분리 | 두 보드가 아니어도 반대 비교군으로 유지; 원작과 이중 집계 금지 |
| 11 | G13 Diablo II LoD | 인벤토리 ↔ 큐브/보관함 → 상인 | 작은 컨테이너 표시와 추가 공간의 관계 | 원작 화면 확보 실패 시 D2R 대표로 교체하고 버전 재명시 |
| 12 | G30 Save Room | 평상시 → 회전 → 유효/무효 위치 → 완료 | 가격 정보가 없을 때의 순수 공간 가독성 기준 | 부피·보드 크기·셀 수는 실제 화면에서 측정 |
| 13 | D01 Merchant of the Skies | 현재 시장 → 항로/목적지 → 거래 직후 | 작은 규모의 가격·목적지 정보 계층 | 미탐사 가격과 알려진 가격을 따로 기록 |
| 14 | D02 Patrician IV | 상품 선택 → 도시 비교 → 경로 조건 | 정확한 숫자와 수급 등급의 역할 분담 | 지표 모양/개수는 직접 캡처 후 확정 |
| 15 | D03 X4: Foundations | 상품 필터 → 지도 오퍼 → 주문/거래 | 지도 위 기회와 데이터 신선도·거리 비교 | 모드 제외; 평균가·호가·예약 물량을 섞지 않음 |

### 후속 작업의 증거 계약

각 게임은 계획 §5의 여섯 상태를 기준으로 `지원 / 해당 없음 / 확인 불가`를 구분한다. 캡처 또는 영상 타임스탬프, **정확한 빌드·플랫폼·해상도·URL·확인일**을 남기고, 선택/드래그/미리보기/확정 후의 변화가 같은 버전임을 확인한다. URL만 있는 지금의 표는 화면 증거 아틀라스를 대신하지 못한다.

- R2 우선: A의 위치 시너지 3건 이상, B의 두 그리드 이동 3건 이상을 실제 화면으로 확보.
- R3 우선: D의 가격·목적지 비교 3건을 확보하되 정확값/범위/등급/미확인 정보를 구분.
- G03/G04 검증 실패 시 A 4개 조건은 **미달**이다. 기존 Backpack Dungeon 등도 제목·기능 확인 전 자동 편입하지 않는다.
- B 교체는 G20 → G16 → G17 순. 대체 후 두 그리드 이동 자체가 실제로 보이는지부터 확인한다.
- 모든 화면이 PC 16:9 조건을 충족한다는 보장은 없다. 콘솔 공식 매뉴얼은 기능 확인에만 쓰고 PC 조작·레이아웃은 별도 증거를 얻는다.

## 5. 기존 문서에서 그대로 옮기지 않은 항목

기존 문서는 다음과 같이 신뢰도 차이가 크므로, 제목에 “그리드”가 있거나 장르가 유사하다는 이유만으로 포함하지 않았다. 아래 항목은 30행 밖의 **제외/재검증 로그**다.

| 항목 | 처리 | 이유 / 필요한 검증 |
|---|---|---|
| SIGNALIS | 비정형 공간 모집단 제외 | 기존 두 문서 사이에 6개 상한과 다칸·회전 추정이 섞임. 개수 제한과 다칸 공간 제한은 다르며 추정 기능을 승계하지 않음 |
| Project Zomboid | 바닐라 제외 / 모드는 별도 모집단 | 기존 문서도 바닐라 리스트형임을 명시. GridInventory/Inventory Tetris 모드를 기본 게임 기능으로 세지 않음 |
| Crow Country, Tormented Souls | 근거 부족 제외 | 기존 “여러 칸·회전·보관 상자” 서술에 추정/확인 불가가 섞임. 독립 매뉴얼/화면 없이는 후보 점수 승계 금지 |
| Diablo III·IV, Minecraft·Terraria·Valheim 등 | 비정형 공간 비교에서 제외 | 일반 슬롯/아이콘 격자를 다칸 패킹과 구분. 각 게임의 모든 버전에 대한 부재 판정은 아님 |
| Grim Dawn | G15 보류로 유지 | 기존 “전 아이템 1×1”과 장르 일반론만으로 포함/제외하지 않고 현 화면부터 재확인 |
| Moonlighter, Starsector, Voidigo, Undungeon | 근거 부족 제외 | 기존 문서의 인접·다칸·회전 서술에 독립 근거 부족. 무역·아이콘 격자·장비 배치도를 인벤토리 패킹으로 혼동 금지 |
| The Cycle: Frontier | 우선순위 제외 | 서비스 종료작. 동시대 버전 영상으로는 조사 가능하지만 현행 PC 상호작용 검증 비용이 큼 |
| Backpack Dungeon, Backpack Jianghu, 기타 최근작 | 예비 발견명만 유지 | 기존 날짜·리뷰·조작 주장 미재검증. A 수량을 채우는 용도로 바로 확정하지 않음 |
| The Bazaar, Cosmoteer | 직접 패킹 후보로 자동 편입 안 함 | 길이 슬롯/함선 건설과 2D 다칸 인벤토리의 차이를 먼저 검증해야 함 |

## 6. 실제 접근한 출처와 재현 방법

확인일은 모두 **2026-11-18**. 아래는 이번 실행에서 실제 읽은 내용과 실패를 구분한 접근 로그이며, 재현 시 사이트 상태는 달라질 수 있다.

| 출처 | 결과 | 실제 확인한 범위 |
|---|---|---|
| MobyGames 그룹 URL(§1) | HTTP 403, `Cf-Mitigated: challenge` | 차단 응답만 확인; 목록·등재·개별 URL 미확인 |
| Steam `https://store.steampowered.com/api/appdetails?appids=<ID>&l=english` | 아래 ID는 JSON `success=true` | `name`, `release_date`, `platforms`, `short_description`, `detailed_description`. 이미지·영상 화면 관찰 아님 |
| [RE4 공식 웹 매뉴얼](https://game.capcom.com/manual/re4/en/ps5/page/6/1) 및 [영문 JSON](https://game.capcom.com/manual/re4/locale/data/en.json) | HTTP 200; 본문은 JS 앱이므로 JSON 확인 | attaché case 정리·Send to Storage·typewriter 보관·Merchant 매수/매도/교환·할인 조건. PS5/VR 서술을 PC 입력으로 바꾸지 않음 |
| [Village 공식 매뉴얼](https://game.capcom.com/manual/village/en/ps5/page/4/1) 및 [영문 JSON](https://game.capcom.com/manual/village/locale/data/en.json) | HTTP 200; JSON 확인 | Rotate Items 90°, crafting 탭, Duke 구매·판매·조리. VR 문단과 일반 조작 문단을 구분 |
| [Arreat Summit Cube](https://classic.battle.net/diablo2exp/items/cube.shtml) | HTTP 200, 실본문 확인 | 2×2 외부 점유, 드래그 삽입·열기, 큐브 안 큐브 금지. 내부 치수는 이번 인용에서 확정하지 않음 |
| `https://classic.battle.net/diablo2exp/basics/inventory.shtml` | HTTP 200이나 DOCUMENT NOT FOUND | 성공 출처로 세지 않음 |
| `https://www.grimdawn.com/guide/character/inventory/` | HTTP 404 | 기능 근거로 세지 않음 |
| PoE Inventory | HTTP 200이나 Anubis 봇 확인 화면 | 본문을 읽은 것으로 세지 않음 |
| Tarkov/DayZ/SCUM/NEO Scavenger Fandom, Unturned wiki.gg | HTTP 403 | 기존 문서 URL은 추적용으로만 유지, 독립 검증 아님 |

레지스터에 사용한 Steam API 성공 ID:

`2427700, 1970580, 2693940, 2342950, 1562430, 1782120, 513710, 221100, 2050650, 254700, 1196590, 219990, 475150, 899770, 225640, 248860, 238210, 6910, 304930, 39190, 39200, 625960, 1789480, 306290, 233860, 1955330, 1040070, 57620, 392160`.

説明の直接確認例:

- Backpack Battles: “Buy and craft powerful items, then carefully arrange them in your backpack.”
- Backpack Hero: “Placement has a huge impact on your items”.
- God Of Weapons: “Your inventory space is limited” 및 전략적 정리 설명. 인접 규칙을 입증하는 인용은 아님.
- NEO Scavenger: “Fit items in grid spaces, and manage encumbrance.”
- Save Room: “The goal is to make all the items fit in your inventory”.
- Merchant of the Skies: “Find the best trade routes and profit from them”.

## 7. R1 결론 / 남은 게이트

**산출:** 33개 행에 동일한 여섯 항목의 1차 점수, 군·판본·플랫폼·선정/보류 이유·URL·신뢰도·미확인점을 기록했고, 심층 검증 큐 15개를 제안했다. 고득점만 택하면 A/B의 공간·구매 패턴에 치우치므로, 낮은 점수의 C 1개와 비그리드 D 3개를 명시적으로 남겼다.

**미달을 숨기지 않는 조건:** MobyGames 등재 대조, 유효 그리드 30개 독립 검증, A 4개의 화면 근거, 15개 상태별 관찰은 아직 완료되지 않았다. R2/R3는 위 조건부·교체 규칙을 따라 증거부터 확보해야 하며, 이 문서만으로 계획 §10의 조사 완료 또는 구현 착수 게이트를 통과했다고 판단하지 않는다.
