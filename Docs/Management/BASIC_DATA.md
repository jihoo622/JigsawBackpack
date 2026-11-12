# 기초데이터

`Assets/Resources/Tables/BasicDataTableData.json`은 공통 표시·조정 값의 원본이다. 기초데이터에는 문구, 의미가 있는 색(Presentation 경유), 게임 규칙 수치만 둔다. 게임 규칙의 소속이 분명한 수치는 해당 도메인 표에 둔다. UI 배치 수치(`ui.*`·`editor.*`)는 새로 추가하지 않으며 프리팹을 원본으로 삼는다.

## 그룹과 조회

현재 39개 그룹에 837개 항목을 관리한다. `groups`의 각 항목은 `id`와 `numbers`·`texts`·`colors` 배열을 가진다. 빈 배열과 기본값인 제약 필드는 생략할 수 있다.

```json
{
  "groups": [
    {
      "id": "ui.layout",
      "numbers": [{ "id": "edge", "value": 40, "whole": true, "positive": true }]
    }
  ]
}
```

- 조회 키는 `그룹 ID.항목 ID`다. 위 항목은 `ui.layout.edge`로 조회한다. 배열 순서에는 의미가 없다.
- 수치의 `whole`은 정수, `positive`는 양수 제약이다. 문구의 `arguments`는 서식 인자 수다. 색상은 RGBA 0~1이다.
- 사용하지 않던 `source` 경로 메타데이터는 제거했다. 소비 위치는 전체 조회 키로 코드 검색한다.
- `BasicData`는 공통 TableManager의 조회 창구이며 별도 로더·캐시·기본값을 갖지 않는다. 화면 문구와 색은 `Presentation`을 거친다.
- 필수 키·중복·유한값·서식 인자·색상 범위를 검증하고 오류 시 `RequireValid()`로 초기화를 차단한다.

## 편집과 제작

- 값만 바꾸면 JSON 저장으로 캐시가 무효화된다. 수동 갱신은 `Tools/MyJG/Tables/Reload Tables`다.
- 키 추가·삭제·이름 변경 또는 서식 인자 수 변경 시 소비 코드도 수정하고 `Tools/MyJG/Tables/Generate Basic Data Contract`를 실행한다. 생성된 `BasicDataContract.cs`는 필수 키와 인자 수만 보유하며 직접 편집하지 않는다. 컴파일 후 표를 다시 읽는다.
- 계약은 JSON 저장 때 자동 생성하지 않는다. 실수로 필수 항목을 삭제하면 기존 계약이 누락을 발견하도록 유지한다.
- `ui.layout.*`는 HUD·역 메뉴 공통 배치의 원본이다. `Tools/MyJG/Apply UI Overhaul`로 씬에 저장한다([UI 배치](UI_LAYOUT.md)). 역 메뉴·시간 UI 제작 도구도 마지막에 같은 배치 도구를 호출하며 중복 치수는 갖지 않는다.
- 신선도 표시의 색·구간·치수는 `presentation.freshness.*`·`ui.freshness.*`, 툴팁 포인터는 `ui.tooltip.*`에서 읽는다. 신선도 슬롯을 새로 제작하면 `Tools/MyJG/Apply Freshness Display`를 실행한다. 최신 툴팁의 삭제된 런타임 글자 크기 설정은 표에서도 제거했다.
- 다른 `editor.*` 제작 수치는 관련 제작 메뉴를 다시 적용해야 씬·프리팹에 반영된다. 기존 `ui.*`·`editor.*` 수치는 프리팹 전환 중이며 단계적으로 제거한다.
- 지도 지명은 ui.PuzzleMapView.labelSearchStep/labelSearchDistance로 기존 방향의 빈 공간을 먼저 탐색하며 labelConnectorThickness가 안내선 두께다. 고정 글자/배지 높이만 가정하던 범위와 겹침 강제 표시 임계값은 제거하고 실제 표시 영역을 사용한다. 안내선 오브젝트는 지도 프리팹에 저장하며 MapPresentationTool도 같은 참조를 구성한다.
- 지도 이름의 상태 가산점·일반 역 표시 줌·지역 표시 수 감점은 ui.mapLabels, 핵심 역 글자 크기는 presentation.map.majorStationFontSize가 소유한다. 지역/역별 기본 가중치는 PuzzleTableData.regions[].mapLabelPriority와 cities[].mapLabelImportance다.

- 기본 가치·현재 적용 효과 행과 일반 효과 슬롯은 `Tools/MyJG/Apply Tooltip Trade Details`로 툴팁 프리팹과 Game 씬에 연결한다. 일반 효과와 가능 가치 시너지 제목은 `presentation.tooltip.*`에서 읽는다. 일반 효과 아래 붙는 진행도와 공간 부족 문구도 `presentation.tooltip.effectProgress`·`presentation.tooltip.effectSpaceBlocked`가 원본이며, 채워 넣는 값은 표가 아니라 세션의 대기·진행 상태다.

- 소문 로그의 문구/날짜·기록 형식은 `presentation.rumorLog.*`, 패널/스크롤 치수와 표시 순서는 `ui.rumorLog.*`다. `Tools/MyJG/Apply Rumor Log Panel`로 프리팹·씬에 저장하며 펼침 애니메이션 상수는 제거했다. 지도 미확인 소문의 `?` 및 안내 서식은 `presentation.map.rumorStation`·`rumorInformation`을 사용한다.

## 소유권과 잔여 후보

- **기초데이터에 두는 것**: 문구, 의미가 있는 색(`Presentation` 경유), 게임 규칙 수치(지도 라벨/노선 우선순위·태그 표시 순위·내구도 표시 대상 태그 등).
- **기초데이터에 두지 않는 것**: 도메인 표 소유 수치(이동 K·시각·휴식은 시간 표, 거래·연출은 거래 표, 화물 정의·기본 가치·재고 프로필은 퍼즐 표, 시너지는 시너지 표), UI 배치 수치(위치·크기·간격·폰트 크기·레이아웃 그룹 등은 프리팹이 원본이며 새로 추가하지 않음).
- 기존 `ui.*`·`editor.*` 수치는 프리팹 전환 중이며 단계적으로 제거한다.

분/시간·백분율 변환, 방향·회전·인덱스 연산·판별용 센티널은 단위·알고리즘 정의로 코드에 남긴다. 자산/씬 참조와 입력·이벤트 스키마 이름도 코드/자산 계약이다. 분석 운영 설정은 `Resources/LogData.json`과 환경변수/LogKit 계약을 유지한다.

남은 공통 스타일·태그/지역 매핑·게임 정책 후보는 [추가 이관 후보](BASIC_DATA_CANDIDATES.md)를 본다.
