# 기초데이터 추가 이관 후보

아래 항목은 **아직 이관하지 않은 후보**다. 런타임·에디터 제작 코드와 Game 씬·인벤토리/지도 프리팹의 직렬화 설정을 점검했다. 공통 게임 화면 배율·주요 버튼 테마·술집 버튼 스타일·본문 글자 배치는 `ui.layout.*`로 이관했다. 이미 기초데이터에 있는 수치·문구·색상은 중복 후보로 세지 않는다.

## 우선 이관 후보

| 항목 | 남아 있는 값·위치 | 제안 | 고려 사항 |
|---|---|---|---|
| 태그 표시 순위 일부 | `Presentation.GetTagSortWeight`: `fish => 4` | `presentation.tagSort.fish` | 나머지 태그 순위와 같은 방식으로 바로 이관 가능 |
| 지도 이름 우선순위 | `PuzzleMapNodeView.LabelPriority`: 현재/선택/목표/직결/환승/기타에 5/4/3/2/1/0 | `map.labelPriorities` | 우선순위 숫자와 `PuzzleMapView`의 중요 라벨 기준을 함께 맞춰야 함 |
| 지도 시간 라벨 우선순위 일부 | `PuzzleMapView.RoutePriority`: 현재 역 2, 선택 역 1, 기타 0. 현재+선택 값만 이미 표에 있음 | `map.routePriorities` | 정렬 기준을 한 묶음으로 관리 |
| 버튼 테마 잔여분 | 화물칸 선택/증차 버튼의 원본 ColorBlock, 비활성 구형 마을 화면과 제작 도구의 복제 원본 | `ui.layout.button.*` 재사용 | 현재 HUD·역 메뉴·지도·모달은 공통 테마 적용. 화물칸은 런타임 선택/드래그 강조와 함께 통합할 필요가 있음 |
| 인벤토리 원본 프리팹 스타일 | `PuzzleInventoryUI.prefab`의 TMP 글자 크기 32/19/21/26과 `m_fontColor` | `ui.layout.*`를 프리팹 제작에도 적용 | Game 씬의 제목·안내·금액은 이미 공통 값으로 덮어쓴다. 원본 프리팹 기본 스타일 중복 정리가 잔여 후보 |

## 레코드 형태를 정한 뒤 이관할 후보

| 항목 | 남아 있는 데이터·위치 | 권장 소유권·이유 |
|---|---|---|
| 태그별 표시 매핑 | `Presentation`의 `PUZZLE_TAG_LABELS`, `PuzzleTagBackgroundColor`, `GetTagSortWeight`가 태그 ID와 문구/색/순위 키를 연결 | 기초데이터에 태그 표시 레코드 추가. 실제 문구·색·대부분 순위는 이미 표에 있으나 새 태그 등록 시 코드 switch도 수정해야 함 |
| 지역 색 매핑·범례 순서 | `Presentation.PuzzleMapRegionColor`의 지역 ID switch, `MapPresentationTool.Apply`의 7개 지역 배열 | 기초데이터의 지역 표시 레코드에 색 키·범례 순위 저장. 지역 ID는 도메인 표와 참조 검증 |
| 라벨 배치 방향·시도 순서 | `PuzzleMapView.LABEL_DIRECTIONS`: 상/우/하/좌/대각선 8방향 | 배치 튜닝이 필요하면 기초데이터에 순서 필드가 있는 좌표 레코드 추가. 단위 방향 자체보다 시도 우선순위가 조정 대상 |
| 날짜 변경 부산물 정책 | `PuzzleInventoryService.SpawnAnimalWaste`: 동물 1마리당 1개, 여러 날짜를 지나도 호출당 1회, 빈자리 없으면 중단 | 게임 규칙이므로 시간/부산물 도메인 표 권장. 수량·여러 날 처리 정책을 먼저 확정. 기초데이터로 옮긴 동물 태그·부산물 ID와는 별개 |
| 술집 소식 선택 정책 | `PuzzleInventoryPresenter.GetTavernRumor` 및 `PuzzleCityView.HearRumor`: 미방문 직결 도시·판매 허용·고가 수요 필터, 처음 찾은 한 항목, 방문당 동일 소식 | 술집 도메인 표 권장. 소식 개수·선택 순서·재방문 갱신 기준을 먼저 확정. 문구는 이미 기초데이터 사용 |

## 이관 대상에서 제외한 값

- 60분/시간·24시간/일·백분율 100, 네 방향·90도 회전, 인덱스·비트 연산·중심 좌표와 판별용 -1은 단위·알고리즘 정의다.
- 경로·Input Action·직렬화 필드·씬 오브젝트·이벤트/분석 스키마 이름은 코드/자산 계약이다. 서버 주소·키는 환경변수/LogKit 계약을 유지한다.
- 스프라이트·폰트·도시 ID·사전 배치 슬롯의 오브젝트 참조는 씬/프리팹에 남긴다. 슬롯 용량 확장은 도메인 보드 크기와 제작 도구에서 산출하는 편이 우선이며 임의의 공통 숫자를 추가하지 않는다.
- 분석 운영 값은 기존 `Resources/LogData.json`에서 관리한다. 기초데이터와 중복하지 않는다.

이관 완료 항목과 편집 방법은 [기초데이터 계약](BASIC_DATA.md), 실행 상태는 [CURRENT_STATE](CURRENT_STATE.md)를 본다.
