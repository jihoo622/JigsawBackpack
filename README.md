# JigsawBackpack

Unity 6.3 LTS(6000.3.22f1) URP 기반의 기차 화물 거래 게임. 작업 규칙은 [AGENTS.md](AGENTS.md), 문서 진입점은 [Docs/README.md](Docs/README.md)다.

게임 기획서는 [노션](https://app.notion.com/p/41400f902d2281849db4c5e61b7f5ac3)과 로컬을 함께 관리한다. 작업 시작 시 노션 변경을 로컬로 가져오고, 여기서 기획을 바꾸면 노션에도 반영한다. 현재 연결은 404이며 로컬의 잠정 규칙·UI 정합성 변경이 미반영 상태다. [동기화 규칙](Docs/Management/NOTION_SYNC.md)과 마지막 공통 기준을 먼저 확인한다.

## 현재 상태 요약

상세 구현 현황과 기획 대비 차이는 [현재 구현 상태](Docs/Management/CURRENT_STATE.md)를 본다.

- Game 씬에서 바로 진입한다. 상단 정보·좌측 기차/화물칸·우측 역 행동·하단 상태 안내로 배치했다. 우측에서 시장(7×6)·술집 소식·휴식·다른 역 이동을 선택한다. `ui.layout.*`와 `Tools/MyJG/Apply UI Overhaul`이 공통 배치 원본이다([UI 배치](Docs/Management/UI_LAYOUT.md)).
- seed 20265146의 지리·이름·43개 왕복 철도와 고정 좌표를 사용한다. 생산 마을 21개·수도권 5개·일반 마을 4개이며 금융마을을 상환 장소로 제한한다. 시작은 `city.industry.ironworks`(에른아 공업역)이고, 금융마을까지 최단 이동 합은 K=28 잠정치로 39시간 53분이다.
- 지도 프리팹에도 외부 데이터의 역 30개·철도 43개(표시 104조각)·시간 라벨을 저장했다. `Tools/MyJG/Apply Rail Map Layout (seed 20265146)`으로 다시 적용할 수 있다([MAP](Docs/Management/MAP.md) §4).
- 지도에 지형 배경·휠 확대/축소·드래그 이동·전체 보기/현재 위치 버튼을 연결했다. 현재 역은 금색 배지·링으로 강조한다. UI 여백과 연결 재적용은 `Tools/MyJG/Apply Map Presentation`이다.
- 줌 시 글자·마커·선 굵기는 화면 크기를 보정하고, 겹치는 이름·시간 라벨은 우선순위에 따라 재배치·생략한다. 시간 표시는 현재 역의 직결 노선으로 한정하며 85% 미만 전체 보기에서는 숨긴다. 실제 화면·입력 확인은 아직 수행하지 않았다.
- 역 클릭은 확인·취소 창을 거친다. 확인 시 기차 이동 연출 완료 후 이동을 확정하고, 도착 표시 후 지도를 닫는다. 이동 연출 시간은 거래 테이블, UI 재적용은 `Tools/MyJG/Apply Map Travel UI`다. 플레이 확인은 아직 수행하지 않았다.
- 이동 비용은 기본요금 + 거리 요금 + 전체 기차 적재 요금이다. 이동 중 지난 6시간 경계마다 각 화물칸의 시너지 변화량을 실제 가치에 누적하고 출발 시장만 새 인스턴스로 재생성한다.
- 시장 재고는 마을 특산 3개·지역 특산 1개·랜덤 2개이며 수도권은 집산·수입품을 사용한다. 시너지와 거래는 복수 태그를 공유하고 흔함을 귀함보다 우선한다. 구매는 기본 가치·도시 배율, 판매는 누적 실제 가치·도시 배율에 지역 물가를 곱한다. 귀함 150/120, 수도권 물가 125%다. 클리어 비용 지불로 승리하고 완료 패널에서 재시작·종료한다. 옛 이동 횟수 기반 빚 일정은 제거했다. 14일 기한·조기 상환 코드를 유지하고, `debtCityId`를 금융마을으로 정해 그 도시에서만 상환할 수 있다.
- 시간은 고정 폴라인 길이 ÷ K(잠정 28픽셀/시간)를 분 단위로 올림한다. `PuzzleTimeTableData`가 이동/시각 설정의 원본이다. 시너지는 `PuzzleSynergyTableData.activationIntervalHours`의 6시간마다 정산하고 이동·휴식·아침 대기에 공통 적용한다. 06:00 시작, 야간 00:00~06:00에는 시장을 차단하며 아침 대기 버튼을 제공한다. 하단 예측 선택 UI는 제거했으며, 호버 미래 가치는 지도에서 선택한 목적지를 사용한다. `Tools/MyJG/Apply Time Planning`으로 표 캐시·지도 시간·HUD 연결을 갱신한다.
- 화물 정의에 기본 가치를 통합하고 시장 재고는 7개 프로필을 공유한다. 기초데이터 필수 키 계약 생성 메뉴를 제공한다.
- 거래 견적은 모델/툴팁이 공유한다. 기본·실제 가치와 현재/가능 인접 효과를 나눠 표시하고 기차 합계는 누적 실제 가치를 사용한다. 패배도 결과 패널을 열며 사과는 일반 재고에 포함된다. 툴팁 행 제작은 `Tools/MyJG/Apply Tooltip Trade Details`다.
- 수치·재고·노선은 `Assets/Resources/Tables/*.json`이 원본이다. 공통 표시·UI 조정값은 **기초데이터**(`BasicDataTableData`)에 모았다([편집 계약](Docs/Management/BASIC_DATA.md)). 표기·색은 `Presentation`, 상태는 씬 로컬 `PuzzleInventoryService`, UI는 사전 배치한 View와 Presenter가 담당한다.
- 다르폴에서 3×3 도축대를 이용할 수 있다. 대상 화물과 작업 시간은 `PuzzleCityActionTableData`가 정하고, 도축대와 버튼은 Game 씬에 연결했다.
- 시작 객차의 송아지는 2×1, 기본 가치 40원이다. 시장 재고에서 제외되며 애기 태그가 있는 동안 도축할 수 없다. 기존 소와 같은 배설물 규칙을 따르며, 48시간 후 같은 객차에서 성체가 된다. 성체 배치 공간이 부족하면 마지막 1시간에 들어가는 시간 소비 행동을 막는다.
- 동물은 자정마다 자기 객차의 빈자리에 똥을 만든다. 똥은 인접 화물에 −50%, 동물은 인접 `Cargo`에 −10%를 적용한다. 같은 객차에서 상하좌우로 맞닿은 같은 품목의 동물 한 쌍은 48시간 후 송아지 1마리를 낳는다. 시간 경과 전 재결합은 진행도를 보존한다.

## 문서

- [게임 기획서](<Docs/게임 기획서.md>): 게임 목표·규칙·지역·경제·화면·미정 사항의 본문.
- [TABLES](Docs/Management/TABLES.md): 데이터 소유권과 검증 계약.
- [MAP](Docs/Management/MAP.md): 맵 데이터와 화면 구성의 저장소 좌표.
- [BACKLOG](Docs/Management/BACKLOG.md): 개발 기반 제약·남은 사용자 확인.
- [TELEMETRY](Docs/Management/TELEMETRY.md) / [PLAY_LOG](Docs/Management/PLAY_LOG.md): 기록 경로·설정 / 이벤트 필드.
- 초기 기획·단계별 작업 계획은 [Docs/Management/Archive](Docs/Management/Archive/)에 보존하며 현재 작업 기준으로 사용하지 않는다.

## 중요한 제약

- `FlowTableData`에는 Preload·Title·Game이 있지만 빌드는 Game만 활성이다. ProjectValidation의 Preload·Title 빌드 목록 누락이 남아 있고 진입·전환 흐름도 미연결이다.
- 테스트·플레이·입력 실험·캡처는 사용자 요청 시에만 실행한다. 임시 지역 맵·시간 소비·거래 경계·클리어 흐름의 남은 사용자 확인은 BACKLOG를 본다.
- 번식 진행은 현재 판에서만 보관하며 별도 진행도 표시가 없다. 배설·성장·출산의 플레이 확인은 [BACKLOG](Docs/Management/BACKLOG.md)에 남겼다.
- 컴파일과 씬 변경은 Unity CLI를 사용한다. 자동 에디터 설정이 열린 씬을 재저장할 수 있으므로 원격 씬을 병합한 후 메모리의 씬과 파일의 참조가 일치하는지 주의한다.
- asmdef·namespace 없이 새 Input System, `PanelBase`, `GameEvents`, 정적 상태 초기화 규약을 따른다. Enter Play Mode는 도메인 리로드 해제, Active Input Handling은 Both다.
- LogKit 패키지는 v0.7.0이다. 분석 JSONL은 서버로 자동 전송되지 않는다. 빌드 환경변수·로컬 우회 설정은 TELEMETRY를 본다. 서버 주소·키는 저장소에 넣지 않는다.
