# 기초데이터 추가 이관 후보

아래 항목은 **아직 이관하지 않은 후보**다. 런타임·에디터 제작 코드와 Game 씬·인벤토리/지도 프리팹의 직렬화 설정을 점검했다. 이미 기초데이터에 있는 수치·문구·색상은 중복 후보로 세지 않는다.

## 우선 이관 후보

| 항목 | 남아 있는 값·위치 | 제안 | 고려 사항 |
|---|---|---|---|
| 태그 표시 순위 일부 | `Presentation.GetTagSortWeight`: `fish => 4` | `presentation.tagSort.fish` | 나머지 태그 순위와 같은 방식으로 바로 이관 가능 |
| 지도 이름 우선순위 | `PuzzleMapNodeView.LabelPriority`: 현재/선택/목표/직결/환승/기타에 5/4/3/2/1/0 | `map.labelPriorities` | 우선순위 숫자와 `PuzzleMapView`의 중요 라벨 기준을 함께 맞춰야 함 |
| 지도 시간 라벨 우선순위 일부 | `PuzzleMapView.RoutePriority`: 현재 역 2, 선택 역 1, 기타 0. 현재+선택 값만 이미 표에 있음 | `map.routePriorities` | 정렬 기준을 한 묶음으로 관리 |
| 버튼 공통 테마·전환 | `Game.unity`의 Button `m_Colors`, `m_Transition: 1`, `m_FadeDuration: 0.1` 및 제작 도구가 복제하는 버튼 원본 | `ui.buttonTheme.*` | 색·전환 시간을 제작 시 적용. disabled 색 등은 현재 Presentation으로 통일되지 않음 |
| 인벤토리 제목·안내·금액 스타일 | `PuzzleInventoryUI.prefab`의 TMP 글자 크기 32/19/21/26과 `m_fontColor` | `ui.inventory.typography.*` | 텍스트 내용은 Presentation을 쓰지만 스타일 일부는 프리팹 고정값. 씬의 override도 함께 정리 |
| 공통 화면 배율 | `Game.unity` Inventory Canvas의 CanvasScaler: 기준 1920×1080, match 0, scale 1 | `ui.canvas.*` | 제작 시 설정. 화면 기준 변경 시 HUD·지도·팝업 배치 영향 검토 필요 |
| 술집 버튼 스타일 | 원격에서 추가된 `Talk to Guest` 버튼·Label의 폰트 크기·상태 색·전환 설정 | `ui.tavern.talkButton.*` 또는 공통 버튼 테마 | 소식 문구·제목·버튼 문구와 주요 영역 치수는 이번 작업에서 이미 표로 이관. 버튼 세부 스타일은 잔여 후보 |

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
