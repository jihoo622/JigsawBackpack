# 그리드 인벤토리 게임 총조사 — 마스터 인덱스

> 조사일 2026-11-18 · Orca 오케스트레이션(워커 3: zcode×2 + pi·qwen3.8)으로 카테고리별 웹 조사 · 관리 세션 통합.
> 목적: JigsawBackpack 기차 화물칸·시장 그리드의 UI 레이아웃 개선 레퍼런스 확보.
> 전체 목록은 하위 파일이 원본이고 이 문서는 인덱스·마스터 표·적용 인사이트다.

## 1. 조사 파일 인덱스

| 파일 | 카테고리 | 게임 수 | 비고 |
|---|---|---:|---|
| [GRID_INVENTORY_ARPG.md](GRID_INVENTORY_ARPG.md) | ARPG·핵앤슬래시·샌드박스 | 17 | Diablo 4작·Torchlight 3작·PoE·Minecraft 등 |
| [GRID_INVENTORY_SURVIVAL.md](GRID_INVENTORY_SURVIVAL.md) | 서바이벌 호러·하드코어 | 18 | RE 4작·Tarkov류·비그리드 대조 2 |
| [GRID_INVENTORY_INDIE.md](GRID_INVENTORY_INDIE.md) | 인디·로그라이크·클래식 | 32 | 5개 그룹(호러 인디·생존·로그라이크·잼·원조) |
| [GRID_INVENTORY_LAYOUT_RESEARCH_PLAN.md](GRID_INVENTORY_LAYOUT_RESEARCH_PLAN.md) | 조사 계획 | — | 우리 화면 질문("두 그리드 사이 이동 순간의 비교 UI") 정의 |
| [GRID_INVENTORY_CANDIDATE_REGISTER.md](GRID_INVENTORY_CANDIDATE_REGISTER.md) | 후보 레지스터 R1 | 30+3 | 그리드 후보 30 선별·심층 제안 15 검증 큐 |
| [BACKPACK_DECKBUILDER_UI_RESEARCH.md](BACKPACK_DECKBUILDER_UI_RESEARCH.md) | (기존) 백팩 덱빌더 | — | Backpack Hero·Battles·Bazaar·Cosmoteer 등 |
| [TRADE_GAME_UI_RESEARCH.md](TRADE_GAME_UI_RESEARCH.md) · [TRAIN_GAME_UI_RESEARCH.md](TRAIN_GAME_UI_RESEARCH.md) | (기존) 무역·기차 | — | 중복 확인용으로만 사용 |

## 2. 마스터 게임 리스트

한 줄 요약은 그리드 방식·대표 UX 장치 기준. 상세는 각 파일 참조. (대조)=그리드가 아닌 비교 사례.

### ARPG·샌드박스 (17)
| 게임 | 연도 | 핵심 |
|---|---|---|
| Diablo | 1996 | 10×4 가변 점유·회전 불가 원점, 골드도 칸 점유 |
| Diablo II / R | 2000/21 | Horadric Cube(점유 2×2↔내부 3×4), 수동 정리 표준 |
| Diablo III | 2012 | 1×1 규격화 반면교사, Junk→일괄 분해 |
| Diablo IV | 2023 | 탭·검색·필터·즐겨찾기로 정리 부담 우회 |
| Titan Quest AE | 2006/16 | 방향 선택형 자동 정렬, 캐러밴 이동 창고 |
| Sacred 1·2 | 2004/08 | 품목별 자동 분류 탭 창고 |
| Book of Demons | 2018 | 자원(마나) 점유형 용량 — 2축 제약 |
| Grim Dawn | 2016 | 개인/공유 창고 색 구분, 부품 일괄 이동 |
| Torchlight 1·2·3 | 2009~20 | 펫 자동 판매(움직이는 창고) |
| Path of Exile 1·2 | 2013/24 | 12×5 격자+스태시 탭·프리미엄 탭 정렬 |
| Last Epoch | 2024 | 1×1+스택·검색 |
| Minecraft | 2011 | shift-클릭 빠른 이동 원조 |
| Terraria | 2011 | 정렬 버튼·즐겨찾기 잠금 |
| Starbound | 2016 | 컨테이너별 칸수 차등 |
| Valheim | 2021 | (대조) 무게제+슬롯 |

### 서바이벌 호러·하드코어 (18)
| 게임 | 연도 | 핵심 |
|---|---|---|
| Resident Evil 4 | 2005 | 어태케이 케이스 원조 — 회전·케이스 확장 판매·비일시정지 |
| Resident Evil 0 | 2002 | 체인지 박스(버리고 교환) — 저장소 없이 이송 |
| Resident Evil Village | 2021 | 그리드+케이스 확장, 판매·정리 간소화 |
| Resident Evil 4 Remake | 2023 | 케이스 자동 정렬·장신구 효과(칸 효율 보너스) |
| Signalis | 2022 | RE식 그리드 인디 재해석 |
| Dead Rising 시리즈 | 2006~24 | 대형 고정 슬롯+무비 컨테이너 |
| Escape from Tarkov | 2017~ | 무게+격자 병행, 도미노형 내부 격자 중첩 컨테이너, 검사(inspect) |
| DayZ | 2018 | 컨테이너 중첩·Vicinity 패널 |
| SCUM | 2020 | 칸+무게+체형 3중 제약 |
| Dark and Darker | 2024 | 빠른 슬롯+그리드, 루팡 UI |
| The Cycle: Frontier | 2022 | 종료 서비스 — 인터페이스 단순화 사례 |
| Gray Zone Warfare | 2024 | (대조) 무게제 |
| The Forever Winter | 2024 | (대조 계열) 무게 중심 |
| Zero Sievert | 2022 | Tarkov식 소형화 — 격자 UI 모바일화 |
| Project Zomboid | 2013~ | 컨테이너별 무게경감·근처 패널 |
| Sons of the Forest | 2023 | 팩 확장·슬롯 혼합 |
| Starfield | 2023 | (대조) 목록형+무게 |
| Stalcraft | 2022 | (대조) 슬롯·무게제 |

### 인디·로그라이크·클래식 (31)
| 게임 | 연도 | 핵심 |
|---|---|---|
| SIGNALIS | 2022 | RE식 그리드, 6칸 제한 긴장 |
| Crow Country | 2024 | 레트로 RE식 케이스 |
| Tormented Souls | 2021 | RE식 격자 현대화 |
| Project Zomboid | 2013~ | 컨테이너 속성 태그(무게경감%) |
| Cataclysm: DDA | 2016~ | 부피+무게 이중 제약, 포켓 모양·자동 픽업 규칙 |
| NEO Scavenger | 2014 | 도미노형 격자+부피 |
| Barotrauma | 2019 | 방수 등 컨테이너 속성 태그 |
| This War of Mine | 2014 | 소형 그리드·슬롯 혼합 |
| The Long Dark | 2017 | (대조 계열) 무게+목록 |
| DREDGE | 2023 | 도미노형 화물(물고기) 그리드 — 무역·그리드 결합 사례 |
| Starsector | 2019 | 화물·연료·승무원 분리 격자, 인접 조건 |
| Streets of Rogue | 2019 | 소형 그리드 |
| Voidigo | 2023 | 그리드+퀵슬롯 |
| Undungeon | 2021~24 | 장기 이식·시너지 격자 |
| Fate 계열 | 2005~ | 그리드+펫 판매 |
| Moonlighter 1·2 | 2016/26 | 그리드→가격책 정리 무역 루프 |
| Packmaster | 2023 | 팩 그리드 덱빌딩 |
| Dungeon Packer | 개발 중 | 그리드 퍼즐 이동 |
| Packmates | 2026 | 인접 시너지 그리드 |
| Grid Warriors: Battles | 2025 | 그리드 배틀 |
| Caselake·Greedy Goblin | 잼 | RE 케이스 미니 재해석 |
| itch.io "inventory+Tetris" 계열 | — | 잼 실험군 |
| System Shock 2 | 1999 | 그리드+검사 원조 |
| Arcanum | 1999 | 기술 도면 그리드 |
| Arx Fatalis·Dungeon Siege | 2002 | 자동 정렬·연속 그리드 원조 |
| KH 358/2 Days | 2009 | 인접 배율 패널(시너지 원조) |
| Infinite Space | 2009 | 함선 모듈 그리드 |
| Dark Cloud·Chronicle·Rogue Galaxy | 2000~05 | 도미노·중첩 불가·페이지형 격자 |

### 백팩 덱빌더 (기존 문서 참조)
Backpack Hero · Backpack Battles · The Bazaar · Cosmoteer · [기존 조사 파일](BACKPACK_DECKBUILDER_UI_RESEARCH.md)

## 3. 카테고리 트렌드 통합

- **제약의 3축**: 칸(공간) / 무게·부피 / 개수. 우리는 칸 단독 — 칸+무게를 더하면 생존류의 2게이지 UI 체계를 따른다.
- **회전**: 사실상 표준(회전 불가 작품은 "칸이 남는데 못 넣는다" 불만 고착). 우리는 이미 허용.
- **정렬의 진화**: 수동 → 자동 정렬 버튼(방향 선택형) → 규칙 프리셋(CDDA). "자동 규칙"이 상위 호환.
- **일괄 처리**: Junk 마킹→전체 판매·분해(D3·D4), 부품 일괄 이동(Grim Dawn) — 다량 화물 동선 압축의 표준.
- **검색·필터**: 스태시 텍스트 검색+필터(D4 1.2.0~), 품목별 자동 분류 탭(Sacred) — 시장·수첩 확장 근거.
- **컨테이너는 속성을 가진다**: 내부 격자 모양(Tarkov 도미노)·방수/무게경감 태그(Barotrauma·PZ)·점유 대비 초과 수납(Horadric Cube) — 객차·개조 확장의 3가지 문법.
- **시너지 표현**: 인접 발광·연결선이 표준(KH 패널→Backpack Battles→우리 시너지) — 텍스트 없이 읽히게.
- **확장 판매**: 케이스 M·L을 상인이 파는 RE4 구조 = 우리 화차 구매와 동일 계보. 케이스 장신구(RE4R)처럼 "칸 효율 보너스" 아이템도 있음.
- **골드·특수품도 칸을 점유**(Diablo 골드 스택) — 화물 간 적재 트레이드오프 장치.
- **페이징·창고 동일 격자**: Rogue Galaxy 7페이지, "창고도 같은 격자 문법"(Dark Cloud 계열) — 대형 열차·역 창고 일관성 근거.

## 4. JigsawBackpack 적용 후보(우선순위 임의 정렬 — 설계 논의용)

1. **객차 내부 격자 다양화**: 화차 상품별로 다른 칸 모양(도미노·L자 결손칸·냉장 전용칸) — Tarkov 내부 격자·Horadric Cube 문법. 개조 시스템과 결합.
2. **자동 정렬 버튼(방향 선택형)**: 역 화면·객차에 원클릭 정렬. 회전 허용·빈칸 압축 기준.
3. **시장 Junk 마킹→일괄 판매**: 하급·만료 임박 화물 표시 후 원클릭 매도.
4. **시장·수첩 검색·필터**: 품목명·태그·지역 필터(D4 스태시 검색 문법).
5. **특수 화물의 칸 점유 장치**: 골드/증서 등이 칸을 차지하거나(Diablo), 자원 점유(Book of Demons) 병행 — 적재 트레이드오프 콘텐츠.
6. **역 창고·시장·기차의 동일 격자 문법** 유지 확인(Dark Cloud 계열) + 대형화 대비 페이지형 UI(Rogue Galaxy).
7. **칸 효율 보너스 아이템**(RE4R 장신구 계열) — 덱빌딩(콤보)·숙련과 결합 가능한 확장.
8. **비일시정지 대신 "정리 비용"**: 턴제이므로 RE4의 긴장은 재배치 무료/역 행동 턴 구조로 이미 번역돼 있음 — 유지.

## 5. 다음 단계

- [CANDIDATE_REGISTER R1](GRID_INVENTORY_CANDIDATE_REGISTER.md)의 **심층 표본 15개** 검증 큐(화면 캡처·기능 확인)를 거쳐 권고 레이아웃 1안·구현 명세로 압축하는 것이 [조사 계획](GRID_INVENTORY_LAYOUT_RESEARCH_PLAN.md)이 정의한 최종 산출물이다.
- 경제·턴 구조는 이미 확정됐으므로 레이아웃 권고는 "기차 5×5 + 시장 보드 + 역 행동 패널"의 3분할 구조를 유지하는 선에서 제안될 것.
