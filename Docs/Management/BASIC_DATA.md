# 기초데이터

`Assets/Resources/Tables/BasicDataTableData.json`의 표시 이름은 **기초데이터**다. 소속이 분명한 게임 수치는 기존 도메인 테이블에 두고, 공통 표시·조정 값은 이 표에서 관리한다.

| 구분 | 내용 |
|---|---|
| `numbers` | 지도 줌 범위·속도, 라벨 간격·크기, 화물 UI 간격, 툴팁 추적 속도, 화물칸 바 배치, 에디터 UI 제작 치수, 레이어 번호, 내구도·대시 횟수 상한 |
| `texts` | 화면 문구·서식, 태그 이름, 공통 규칙에서 참조하는 태그·화물 ID |
| `colors` | 화면·지도·툴팁·화물·에디터 제작 색상(RGBA 0~1) |

- `id`가 조회 키다. `source`는 사용 위치를 찾는 참고 정보이며 실행 로직에 쓰지 않는다. `whole`은 정수 여부, `positive`는 양수 제약, 문구의 `arguments`는 서식 인자 수다.
- `GameTables`·`GameTableRecords` → `TableManager` 공통 로드·인덱스 → `GameTableRules` 검증을 따른다. `BasicData`는 이 조회 경로의 얇은 창구이며 별도 로더·캐시·기본값은 없다. 문구·색을 쓰는 UI는 계속 `Presentation`을 거친다.
- `BasicDataContract`는 필수 ID와 서식 인자 수만 가진다. 키를 추가·삭제하면 소비 코드와 이 계약도 함께 수정한다. 누락·중복·범위 오류는 초기화를 막는다.
- 값 수정 후 표 캐시는 JSON 저장 시 무효화된다. 수동 갱신은 `Tools/MyJG/Tables/Reload Tables`를 사용한다. `editor.*`와 `station.market.*` 배치 값은 제작 시 씬·프리팹에 저장되므로 관련 제작 메뉴도 다시 적용한다. 역 메뉴는 `Apply Station Actions UI`, 시간/HUD는 `Apply Time Planning`이다.
- 이동 K·일출·야간·시작 시각·휴식 분수는 `PuzzleTimeTableData`, 거래·이동 연출 수치는 `PuzzleTradeTableData`, 화물·시장·가격·노선·시너지는 각 도메인 표가 원본이다.

## 코드에 남는 구조 값

감사 범위는 `Assets/Scripts`의 런타임과 UI 제작 코드다. 분/시간·시간/일·백분율·센티미터 변환, 네 방향·90도 회전, 인덱스·비트 연산·중심 좌표, 유효하지 않은 값의 판별자는 알고리즘·단위 정의다. 경로·직렬화 필드·Input Action·씬 오브젝트·이벤트·분석 스키마 이름과 오류 진단 문구도 코드 계약으로 유지한다. 씬의 도시 ID·스프라이트·폰트·사전 배치 슬롯 참조는 자산 연결이다.

분석 운영 설정은 기존 `Resources/LogData.json`이 원본이며 서버 주소·키는 환경변수/LogKit 설정을 따른다. 도메인 테이블에 합치지 않는다. 새 조정 값은 코드 상수·직렬화 기본값을 추가하지 않고 도메인 표 또는 기초데이터에 등록한다.

아직 표로 옮기지 않은 공통 스타일·우선순위·표시 매핑과 도메인 정책 후보는 [추가 이관 후보](BASIC_DATA_CANDIDATES.md)에 정리한다. 현재 모든 직렬화 스타일과 정책이 테이블화된 상태는 아니다.
