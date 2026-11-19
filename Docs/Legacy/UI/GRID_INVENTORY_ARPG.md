# ARPG·핵앤슬래시·샌드박스 — 그리드 인벤토리 UI 조사

> 백팩 덱빌더 계열(Backpack Hero·Backpack Battles·The Bazaar·Cosmoteer 등)은 BACKPACK_DECKBUILDER_UI_RESEARCH.md 참조 — 본 문서에서는 제외.
> 조사일 2026-11-18. 웹 검색(공식 패치노트·위키·Steam 커뮤니티) 기반 2차 요약이며 실제 플레이 대조는 하지 않음. 확신 없는 항목은 "(추정)", 확인 불가는 "(확인 안 됨)".

---

## 1. Diablo (1996, Blizzard North)
- 장르·플랫폼: 아이소메트릭 핵앤슬래시 ARPG, MS-DOS/Windows(이후 콘솔 이식)
- 그리드 방식: 인벤토리 10×4=40칸 고정. 아이템이 칸을 가변 점유 — 물약·반지 1×1, 검류 1×3, 방패·투구 2×2, 갑옷 2×3, 활 2×4 등(품목별 세부 배치는 (추정)). 회전 불가 — 2칸 폭 아이템은 세워 넣을 수 없어 배치 위치가 제한됨. 무게 없음, 칸 수만이 제약.
- UI 레이아웃 특징: 좌측 종이인형(장비 슬롯)+우측 격자 동시 표시. 물약 벨트 8칸(1×1 전용 퀵슬롯, 숫자키 사용). 골드가 별도 슬롯을 점유(칸 1개, 5,000골드 단위 적재) — 돈도 공간 비용. 정렬·필터 없음, 전량 수동 드래그&드롭, 호버 툴팁(비교 툴팁 없음). 인벤토리 확장·프리셋 없음. 칸 점유 크기 자체가 "큰 보상 = 큰 카드"의 시각 언어로 기능.
- 참고 포인트: ① 골드·특수 화물도 칸을 점유하게 하는 "돈도 공간 비용" 설계는 화물 간 적재 트레이드오프 강화용. ② 회전 불가의 배치 좌절감 — 회전 허용 여부가 곧 난이도·편의 파라미터임을 보여주는 원점 사례(현 JigsawBackpack은 회전 허용).

## 2. Diablo II (2000, Blizzard North) / Resurrected (2021)
- 장르·플랫폼: 핵앤슬래시 ARPG, PC(확장 LoD 2001). D2R은 PC+콘솔
- 그리드 방식: 가변 점유 1×1(반지·아뮬렛·룬·참)~2×4(활·폴암·대형 갑옷). 검 1×3, 갑옷 대부분 2×3, 투구·방패·장갑·신발·벨트 2×2, 화살통 1×2. 회전 불가. 인벤토리 10×4=40칸, 스태시 6×8=48칸(원작). 물약 벨트 최대 4×4=16칸(착용 벨트 등급으로 행 확장, 키 1~4). 무게 없음.
- UI 레이아웃 특징: 정렬 버튼 없음 — 수동 정리가 표준 플레이의 일부. Horadric Cube: 인벤토리에서 2×2 점유, 내부 3×4=12칸의 이동식 보조 격자+변환 레시피기("4칸을 쓰고 12칸을 얻는 가방 안의 가방"). Shift 클릭 이동·호버 비교 툴팁. Resurrected: 개인·공유 스태시 10×10=100칸으로 확대, 공유 스태시 3탭(계정 공유), 컨트롤러 조작(패드 커서). 인벤토리 40칸·큐브·회전 불가는 원작 그대로.
- 참고 포인트: ① Horadric Cube식 "점유 대비 초과 수납 + 기능(조합) 내장 컨테이너"는 업그레이드형 객차·특수 화물 아이템의 직접 레퍼런스. ② 보조 격자 안에서만 성립하는 레시피는 화물 조합(작업장) 콘텐츠와 결합 가능.

## 3. Diablo III (2012, Blizzard Entertainment)
- 장르·플랫폼: 핵앤슬래시 ARPG, PC+콘솔(UEE 2014)
- 그리드 방식: 모든 아이템 1×1 규격화+스택. 가방 7개(각 10칸 — 시작 1개, 이후 구매)=총 70칸. 회전 개념 소멸. 무게 없음.
- UI 레이아웃 특징: 장비는 종이인형 영역, 가방은 아이콘 그리드로 분리. 자동 정렬 버튼 없음(출시 이후 줄곧 최다 요청 중 하나, 수동 정리 유지). Junk 마킹 후 대장장이에서 등급별 Salvage All(일괄 분해). 인벤토리 확장 없음 — 저장 확장은 스태시 탭 추가(탭당 10×7=70칸)로만. 나란히 붙는 장비 비교 툴팁이 시리즈 표준으로 정착. 콘솔판은 패드 전용 UI·우스틱 클릭 Junk 마킹.
- 참고 포인트: ① 1×1 규격화가 "그리드 재미"를 지운 반면교사 — 칸 점유 다양성이 장르 정체성임을 입증. ② 등급별 일괄 분해·판매 버튼은 다량 수거(화물) 후 처리 동선을 압축하는 UX.

## 4. Diablo IV (2023, Blizzard Entertainment)
- 장르·플랫폼: 오픈월드 핵앤슬래시 ARPG, PC+PS+Xbox(크로스플레이)
- 그리드 방식: 전 아이템 1×1 아이콘 그리드, 회전 개념 없음. 무게 시스템 없음 — 패치노트·위키 전반에서 무게/적재 한도 확인 안 됨, 인벤토리가 가득하면 줍기만 불가(패널티 없음).
- UI 레이아웃 특징: 탭 구성(장비/소모품/재료(+퀘스트)). 패치 흐름이 "정렬 수요를 격자 밖으로 돌려보내기": ① 1.2.0 Season of Blood(2023.10) — 보석을 재료 파편으로 통합해 인벤토리 비점유화, 스태시 텍스트 검색+필터(무기/방어구/등급), 웨이포인트 있는 소도시마다 스태시 설치. ② 1.4.0 Loot Reborn(2024.5) — 제작 재료 대규모 통합, 재료 획득 HUD 팝업, 스태시 검색에 아이템 위력 필터. ③ 1.5 — 희귀 재료 자동 줍기 해제, 어스펙트 즐겨찾기+필터. ④ 2.0 Vessel of Hatred(2024.10) — 즐겨찾기 1클릭·Junk 2클릭으로 조작 재배치. 자동 정렬(Sort) 버튼: 1.2.0~2.0 주요 패치노트 범위에서 추가 확인 안 됨 — 재료 비점유화·검색·Junk·즐겨찾기로 정리 부담을 우회하는 것이 공식 해법. Junk 마킹→일괄 판매·분해 체계, 스태시 탭은 골드 구매. 컨트롤러 기본 지원(패드로 인벤토리·탭 조작).
- 참고 포인트: ① 소모품·재료를 격자 밖 전용 저장소로 밀어내 핵심 화물의 배치 재미를 지키는 패턴 — 재료성 화물의 전용칸·자동 분류 근거. ② 라이브 QoL 우선순위(비점유화→검색/필터→일괄 처리→즐겨찾기)는 출시 후 개선 로드맵의 레퍼런스.

## 5. Titan Quest (2006, Iron Lore Entertainment, Anniversary Edition 2016)
- 장르·플랫폼: 신화 배시 핵앤슬래시 ARPG, PC(콘솔 이식)
- 그리드 방식: 가변 점유 그리드 — 장비는 2칸 이상, 반지·물약·두루마리 등 소형품 1×1(품목별 세부 수치는 (추정)). 회전 불가. 무게 없음. 가방(색) 1개 기본, 메인 퀘스트 완료로 최대 4개까지 지급 — 가방당 8×5=40칸, 합계 약 160칸((추정)).
- UI 레이아웃 특징: 좌측 종이인형+우측 다중 가방. 자동 정렬 버튼 — 세로/가로 정렬 방향을 선택해 재배치(원작(2006)에는 없었고 확장팩 Immortal Throne(2007) 무렵 추가, (추정)). 캐러밴(Caravan Driver) 창고: 캐릭터 간 아이템 이전 가능, 골드로 확장(확장팩에서 추가). 창고 부족은 외부 도구(TQVault)로 보완하는 문화.
- 참고 포인트: ① 세로/가로 선택형 자동 정렬 — 원클릭 정렬을 취향 옵션으로 풀어낸 초기 사례. ② "캐러밴"이라는 세계관적 이동 창고 — 기차 게임에서는 정거장 창고·연결 객차로 직역 가능.

## 6. Sacred 2: Fallen Angel (2008, Ascaron) — Sacred(2004) 병기
- 장르·플랫폼: 핵앤슬래시 ARPG, PC+PS3+Xbox 360(2025 리마스터)
- 그리드 방식: 고정 격자에 가변 점유(무기·방어구 2×1~2×3 직사각), 총 약 168칸((추정)). 회전 불가. 무게 없음 — 힘 등 스탯이 소지력에 영향 없음, 제한은 칸 수뿐.
- UI 레이아웃 특징: 인벤토리 정렬 버튼 — 위→아래/왼→오른 2방향 선택(TQ와 같은 방향 선택형). 장착 무기·방패는 좌하단 슬롯 드래그&드롭. 영웅 상자(Hero Chest): 탭 5개(무기/방어구/유물/기타/물약) 품목별 자동 분류. 인벤토리↔창고 빠른 이전은 Sacred 1(2004)에 있었고 2에서 제거 — 원조격 Sacred 1은 대형 그리드 인벤토리의 초기 표준.
- 참고 포인트: ① 탭 자동 분류 창고 — 화물칸을 품목별 자동 분류함으로 확장하는 방식. ② 창고 탭 = 품목 필터의 가장 직관적 구현.

## 7. Book of Demons (2018, Thing Trunk)
- 장르·플랫폼: 종이 컷아웃 테마 핵앤슬래시+덱빌딩 하이브리드, PC+Switch
- 그리드 방식: 테트리스식 격자 대신 "모든 것이 카드" — 카드 1장=슬롯 1개((추정), 세부 수치 확인 안 됨). 대신 아티팩트(장비) 카드가 마나 풀 일부를 상시 점유하는 자원 점유형 용량 시스템이 별도 존재.
- UI 레이아웃 특징: 인벤토리=카드 목록/덱 화면. 시작 카드 슬롯 3개 → 진행하며 구매 확장. 카드 룬 업그레이드·동일 카드의 희귀도 변형 관리. 정렬·필터 없음 — 같은 카드의 다른 희귀도를 주울 때 교체·처분 수단이 마땅치 않은 점이 커뮤니티 불만(판매는 마을 Ferryman).
- 참고 포인트: ① "장비 = 자원(마나) 점유"는 칸 점유와 병행 가능한 2축 제약 — 화물 칸+연료/부하 개념으로 이식 가능. ② 개별 처분 동선 부재가 낳은 불만 = 수거한 화물의 폐기·판매 UX를 반드시 설계할 것(반면교사).

## 8. Grim Dawn (2016, Crate Entertainment)
- 장르·플랫폼: 다크판타지 핵앤슬래시 ARPG, PC(콘솔 이식)
- 그리드 방식: 전 아이템 1×1+스택(부품·소모품). 인벤토리 탭 최대 5개(기본 1 + 퀘스트·구매로 추가), 탭당 16칸((추정)). 무게 없음.
- UI 레이아웃 특징: 자동 정렬 버튼 존재(스태시 화면 좌측 — 도입 시점 확인 안 됨). 밀수꾼(Smuggler) 창고: 개인용(갈색 아이콘)·이전용(보라색, 캐릭터 간 공유)을 아이콘 색으로 구분, 탭 추가 구매. Shift+호버 비교 툴팁, Shift+클릭 즉시 이동, 부품 일괄 이동 버튼(v1.1.4.1, 2019). v1.3.0(확장 Fangs of Asterkarn기) 스태시 개편 — 공유 재료 전용 페이지. 게임패드 지원.
- 참고 포인트: ① 개인(갈색)/공유(보라) 아이콘 색 구분 — 화물칸의 "소유권/공유" 시각 구분에 전용. ② 부품 일괄 이동 버튼 — 재료성 화물의 원클릭 정리 장치.

## 9. Torchlight (2009, Runic Games)
- 장르·플랫폼: 핵앤슬래시 ARPG(디아블로 개발진 창사), PC(2011 XBLA 이식)
- 그리드 방식: 균일 격자 — 아이템 전부 1칸 점유, 소모품 스택. 격자 크기 수치는 확인 안 됨. 무게 없음.
- UI 레이아웃 특징: 펫 인벤토리 — Shift+클릭으로 펫 가방에 입고, 펫을 마을로 보내면 자동 판매(플레이어가 마을에 돌아가지 않아도 전리품 정산). 마을에 개인 Stash+계정 공유 Shared Stash 2중 창고. 자동 정렬은 기본 미제공((추정)) — 정렬 모드가 모드 생태계 단골. 콘솔판 별도 컨트롤러 UI.
- 참고 포인트: ① 펫 = 움직이는 자동 판매 창고 — "짐수래 동물" 화물칸·역 도착 자동 정산 콘텐츠로 직역 가능. ② 개인/공유 2중 창고 표준 확립.

## 10. Torchlight II (2012, Runic Games)
- 장르·플랫폼: 멀티플레이 핵앤슬래시 ARPG, PC(콘솔 이식), Steam Workshop
- 그리드 방식: 균일 격자 — 전부 1×1, 소모품 스택. 인벤토리 약 40칸(5×8, (추정)), 펫 가방 15칸 내외((추정)). 무게 없음.
- UI 레이아웃 특징: 인벤토리에는 정렬 버튼, 개인·공유 창고에는 미제공(비대칭) → 보완 모드 난립(스택 상한·창고 확장이 워크샵 표준 QoL). 펫이 판매까지 수행(TL1은 이동만). 공유 창고 자동 백업 생성(1.12.x.7, 2012 — 유실 방지).
- 참고 포인트: ① "일부 화면만 정렬 제공"이 낳은 모드 의존 — 전 화면 동등 정렬이 정답임을 보여주는 반면교사. ② 공유 창고 자동 백업 = 데이터 안전장치 설계.

## 11. Torchlight III (2020, Echtra Games)
- 장르·플랫폼: 핵앤슬래시 ARPG, PC+콘솔(EA 2020.6 → 정식 2020.10)
- 그리드 방식: 캐릭터 인벤토리 20칸으로 대폭 축소 + 계정 공유 저장고 180칸. 아이템 규격 확인 안 됨. 무게 없음.
- UI 레이아웃 특징: Fort(요새) 시스템 — 배치형 보관 프롭(상자, 장비 전용 Wardrobe, 펫 수납)으로 보관 확장, 계약·퀘스트로 프롭 추가. 펫이 전리품을 마을까지 운반·판매, 펫 자체 장비 슬롯. 공유 저장고는 하드코어/일반 분리. 초기 공유 저장고 아이템 유실 버그 보고(2020.10) 다수.
- 참고 포인트: ① 요새에 "배치하는 보관 가구" — 화물칸·선실을 배치형 보관 프롭으로 확장하는 아이디어. ② 극소 인벤토리+외부 저장 유도 = 짐 싣기/내리기 루프를 의도적으로 강제한 설계.

## 12. Path of Exile (2013, Grinding Gear Games) / Path of Exile 2 (2024 조기 액세스)
- 장르·플랫폼: 탑다운 핵앤슬래시 ARPG, PC+PS+Xbox
- 그리드 방식: 캐릭터 인벤토리 12×5=60칸 — 전 아이템 1×1, 회전·무게 없음(인벤토리 확장 수단 사실상 없음). 표준 스태시 탭은 12×12=144칸에 아이템 크기별 점유(갑옷 2×3 등). PoE2 변화: 인벤토리에도 아이템 크기별 칸 점유 도입(규격 수치 확인 안 됨), 스킬 젬을 인벤토리에서 분리해 전용 Skills 패널로(Gemcutting에서 Uncut Gem 개봉) — "스킬 그리드"가 캐릭터 화면으로 이동.
- UI 레이아웃 특징: 스태시 전용 탭 — 통화/지도/젬/예언 카드/파편/에센스/플라스크/유니크 컬렉션 등 품목별 고정 셀·스택 헤더 UI, 탭 폴더 그룹화(2021년 3.13). Affinity: 탭마다 아이템 유형 지정 → 인벤토리에서 Ctrl+클릭 시 해당 탭으로 자동 이동·정렬(Ctrl+Shift는 현재 열린 탭 우회). Ctrl+클릭 빠른 이동이 사실상 표준 조작. 지면 전리품은 라벨 표시가 기본 + loot filter가 표시/숨김·색·알림음까지 제어(NeverSink+FilterBlade가 표준). 관리 부담을 전용 탭·필터로 해소하는 설계.
- 참고 포인트: ① Affinity+Ctrl+클릭 = "품목별 목적지 자동 배정" — 화물 자동 적재 규칙 UI로 직역 가능. ② 통화 탭식 "품목별 고정 셀+스택 압축" — 화물 종류별 정리 UX의 모델.

## 13. Last Epoch (1.0 2024, Eleventh Hour Games)
- 장르·플랫폼: 시간여행 핵앤슬래시 ARPG, PC+PS5+Xbox Series X|S
- 그리드 방식: 인벤토리 60칸(6×10) 고정, 전 아이템 1×1, 회전·무게 없음.
- UI 레이아웃 특징: Sort 버튼 — 인벤토리·스태시 즉시 정렬(타입·레어도 등 기준). 스태시 기본 6탭+골드 추가 구매(1.0.3에서 비용 대폭 인하), Set·모놀리스 보상 전용 탭. 탭 내 검색바(이름·어픽스·타입 키워드). Shift+우클릭(콘솔 X/□) 예치 + 탭 우선순위 규칙(Stash Tab Priority — 2025년 무렵 추가, (추정)) = PoE affinity 계열. 호버 비교 툴팁, 지면 툴팁 토글(Z).
- 참고 포인트: ① 탭 우선순위 규칙+원클릭 예치 — 화물칸 적재 자동화의 최신 간결형. ② 스태시 단위 검색바 — 대량 보관 검색 UX의 벤치마크.

## 14. Minecraft (2011, Mojang)
- 장르·플랫폼: 복셀 샌드박스, PC+콘솔+모바일 전 플랫폼
- 그리드 방식: 본체 27칸(3×9)+핫바 9칸(+방어구 4·오프핸드 1). 전 아이템 1×1, 스택 한도 64/16/1 3종. 무게 개념 없음.
- UI 레이아웃 특징: Shift+클릭 = 열린 창(상자·제작대·광산 수레 등)↔인벤토리↔핫바 간 스택 단위 즉시 이동 — 벌크 이동 UX의 원형. Shift+더블클릭으로 동일 품목 전부 이동, 더블클릭으로 커서에 동일 품목 모으기. 우클릭 반 나누기/그리드 위 1개 배치. 상자 27칸, chest minecart는 이동 가능한 동일 상자. 슐커 박스 = 27칸 보관함이 파괴 없이 통째로 아이템이 되어 이동(보관 단위 수송, 슐커 안 슐커 불가). Bundle(1.17 실험 → 1.21.2 Java 정식): 1슬롯에 서로 다른 품목 여러 종을 "스택 1개분(64단위)"로 수납, 16스택 품목은 1개당 4단위·비스택 아이템은 전체 점유, 호버 시 내용물 슬롯 패널(초과분 "+N")+휠 항목 선택+우클릭 꺼내기, 아이콘에 잔량 게이지. 크리에이티브는 카테고리 탭+키워드 검색 탭. Bedrock은 터치·컨트롤러 지원+UI 프로필(포켓/클래식) 선택.
- 참고 포인트: ① Bundle의 "슬롯 1칸 안의 소형 서브 인벤토리+호버 스크롤" — 화물 팔레트(소분류 묶음) 표현에 직접 적용 가능. ② 슐커 박스식 "보관 단위 통째 수송"과 Shift+더블클릭 일괄 이동.

## 15. Terraria (2011, Re-Logic)
- 장르·플랫폼: 2D 샌드박스 액션 어드벤처, PC+콘솔+모바일
- 그리드 방식: 인벤토리 40칸(4×10, 최상단이 핫바 1~0)+동전 4·탄약 4·장비/바니시/염료 전용 슬롯. 전 아이템 1×1, 최대 스택 9999(1.4.4에서 999→9999). 무게 없음.
- UI 레이아웃 특징: Shift+클릭 빠른 이동 — 상자 열림 시 상자로, 아니면 Trash 슬롯으로(Quick Trash). Trash 슬롯 1칸: 회수 가능, 새 아이템을 얹으면 기존 것 소멸, 사망·월드 종료 시 비움. 상자 UI 버튼 3종(Loot All/Quick Stack/Deposit All — Desktop 1.0.6), Quick Stack to Nearby Chests(Desktop 1.3.0.1 — 37.5타일 반경의 근처 상자 전체에 자동 분산 정리, 1.4.4에서 반경 대폭 확대+아이템 비행 연출), Similar Stack 모드(1.4.5 — 버튼 우클릭 토글, 유사 품목 상자 우선). 장비 로드아웃 3세트 프리셋(1.4.4, F1~F3 즉시 전환). 우클릭 1개 꺼내기(홀드 연속), 스택 커서(동일 아이템 위에서 스택 합침). 즐겨찾기(하트) 항목은 일괄 정리 제외.
- 참고 포인트: ① "근처 저장소 전체 일괄 정리(Quick Stack to Nearby Chests)" — 역 도착 시 여러 화물칸·창고에 화물을 한 번에 내리는 UX의 직접 레퍼런스. ② Trash 슬롯의 실수 방지 구조+하트 즐겨찾기 제외 규칙.

## 16. Starbound (2016, Chucklefish)
- 장르·플랫폼: 2D 우주 탐사·생활 샌드박스, PC+콘솔
- 그리드 방식: 카테고리 백 5종(Main/Material/Object/Reagent/Food)×40칸=200칸, 획득 시 품목별 자동 분류. 전 아이템 1×1, 스택 한도 품목별(블록 최대 1000, (추정)). 무게 없음.
- UI 레이아웃 특징: 핫바 2열(각 10슬롯, X 전환, 1~6 선택) — 가운데 4칸은 도구 전용, 좌/우 손 아이템 할당(양손 아이템은 양쪽 점유), 잠금 아이콘으로 슬롯 고정. 인벤토리 우하단 원클릭 정렬 버튼(스택 합치기+빈칸 압축+정렬 동시). 컨테이너: Shift+클릭 이동, 우클릭 1개 꺼내기, Shift+우클릭 반 나누기, Q 버리기. Trash 슬롯(1초 유예 후 영구 삭제). 픽셀(화폐)은 슬롯 미점유·좌하단 수치 표시, Pixel Compressor로 아이템화해 사망 손실 방어(압축 시 손실 존재, 비율 (추정)).
- 참고 포인트: ① 카테고리 백 자동 분류 = 품목별 화물칸 자동 배정의 가장 단순한 구현 예. ② 화폐 비점유 표시+원클릭 압축 정렬.

## 17. Valheim (2021, Iron Gate Studio)
- 장르·플랫폼: 바이킹 오픈월드 생존 협동, PC+Xbox/게임패스
- 그리드 방식: 인벤토리 8×4=32칸(최상단 8칸이 핫바 — 1~8키로 인벤토리를 열지 않고 사용), 확장 후 48칸(8×6). 전 아이템 1칸(스택은 한 칸에 수납), 스택 한도 품목별. 무게 병행 — 기본 한도 300, 초과 시 Encumbered(보행만 가능, 공격·방어 불가). Megingjord 벨트 +150, Moder 보스 강화 +300(5분) 등 버프로 최대 1,150까지 상향.
- UI 레이아웃 특징: 전용 장비 슬롯 없음 — 장착품은 그리드에서 파란 배경 표시(오른손/왼손/머리/가슴/다리/망토/장신구/부적, 양손 아이템은 양손 점유). 획득 아이템은 좌하→우상, 제작품은 좌상→우하로 채움. 정렬 버튼 없음(모드로 보완하는 문화), 획득 시 자동 스택 병합, Ctrl+클릭 상자↔인벤토리 빠른 이동, Shift+클릭 스택 분할(수량 입력). 상인 Haldor에서 Wider Pockets/Deeper Pockets(각 +8칸, 보스 격파 후 구매) — 칸 확장은 편의, 무게 한도는 불변으로 역할 분리. 금속 광석·주괴는 포탈 통과 불가 → 배·수레 물리 수송 필수.
- 참고 포인트: ① "칸 수 확장(편의)과 무게(본질 제약)의 역할 분리" — 이동 적재 요금 밸런스 설계에 적용 가능. ② 포탈 불가 품목 같은 "특정 화물만 특별 운송 규칙"은 화물 미션·운임 설계의 레퍼런스.

---

## 공통 UI 트렌드 요약
- 가변 점유+회전 불가가 고전 문법(1996~2008), 그 뒤 시리즈는 1×1 규격화(D3·D4·TL·GD·LE)로 갈라짐 — 칸 점유·회전 여부 자체가 난이도이자 장르 정체성. JigsawBackpack식 "가변 점유+회전 허용"은 그 사이의 실험적 지점.
- 정렬 버튼이 특별 옵션(2006~2008 TQ·Sacred의 방향 선택형)에서 기본 기능(Starbound·LE의 원클릭 압축 정렬)으로 보편화. 미제공이면 모드가 갭을 메꿈(TL2·Valheim) — 전 화면 동등 제공이 정답.
- 칸 점유를 "재미있는 것"에만 남기는 우회 장치의 보편화: 재료·통화·스킬 비점유화(D4·PoE2), 전용 탭+affinity 자동 라우팅(PoE·LE), 카테고리 백 자동 분류(Starbound), 화폐 비점유(Starbound·D1의 반대 사례).
- 컨테이너 간 벌크 이동 표준화: Shift/Ctrl+클릭 빠른 이동, 더블클릭 스택 수집, 근처 저장소 일괄 정리(Terraria), 탭 우선순위+원클릭 예치(LE) — "한 칸씩 옮기기"는 사라진 조작.
- 무게는 생존 샌드박스(Valheim)의 본질 제약, ARPG에선 부재 — 칸 수는 편의 확장, 무게는 밸런스 축으로 역할 분리하는 설계가 정착.

## 사용한 출처
- Diablo 1·2: https://diablo.fandom.com/wiki/Inventory · https://diablo2.diablowiki.net/Inventory_size · https://diablo-archive.fandom.com/wiki/Horadric_Cube_(Diablo_II) · http://www.arreatsummit.com
- Diablo 3: https://diablo.fandom.com/wiki/Bag · https://diablo.fandom.com/wiki/Stash
- Diablo 4: https://blizzardwatch.com/2023/10/05/inventory-improvements-diablo-4-season-blood · https://news.blizzard.com/en-us/article/24140808/diablo-iv-patch-notes-2-0 · https://maxroll.gg/d4/news/diablo-4-patch-1-3-0-notes · https://maxroll.gg/d4/news/diablo-4-patch-1-4-notes · https://maxroll.gg/d4/news/diablo-4-patch-1-5-notes · https://maxroll.gg/d4/news/2-0-ptr-patch-notes
- Titan Quest: https://titanquest.fandom.com/wiki/Inventory · https://steamcommunity.com/app/475150/discussions/0/2443713720483509235
- Sacred 2: https://sacredwiki.org/index.php/Sacred_2:Hero_Chest · https://gamefaqs.gamespot.com/ps3/949630-sacred-2-fallen-angel/answers/67680-is-there-any-weight-to-increase-you-inventory-quotcarry-weightquot
- Book of Demons: https://media.thingtrunk.com/press-kit/book-of-demons · https://bookofdemons.fandom.com · https://steamcommunity.com/app/448030/discussions/
- Grim Dawn: https://grimdawn.fandom.com/wiki/Smuggler · https://steamcommunity.com/app/219990/discussions/0/2132869574259996331 · https://forums.crateentertainment.com/t/shared-stash-not-linked/152552
- Torchlight 1·2·3: https://torchlight.fandom.com/wiki/Pets_(T2) · https://torchlight.fandom.com/wiki/Torchlight_II_version_history · https://www.arcgames.com/en/corp-news/detail/11457693-torchlight-iii-early-access-%27state-of-the-game%27
- Path of Exile: https://pathofexile.fandom.com/wiki/Stash · https://www.pathofexile.com/forum/view-thread/2986656 · https://www.poewiki.net · https://www.filterblade.xyz · https://www.rockpapershotgun.com/path-of-exile-2-skill-gems-and-gemcutting-guide · https://pathofexile2.wiki.fextralife.com/Stash_Tabs
- Last Epoch: https://lastepoch.fandom.com/wiki/Inventory · https://lastepoch.fandom.com/wiki/Stash · https://forum.lastepoch.com/t/how-does-the-sort-feature-work/65917
- Minecraft: https://minecraft.wiki/w/Bundle · https://minecraft.wiki/w/Inventory · https://minecraft.wiki/w/Creative_inventory
- Terraria: https://terraria.wiki.gg/wiki/Inventory · https://terraria.wiki.gg/wiki/Storage_items
- Starbound: https://starbounder.org/Inventory · https://starbounder.org/User_Interface · https://starbounder.org/Pixel_Compressor
- Valheim: https://valheim.weirdgloop.org/w/Inventory · https://valheim.weirdgloop.org/w/Moder · https://eip.gg/valheim/how-to-expand-inventory-valheim
