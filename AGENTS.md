# 에이전트 작업 지침 (MyJG 템플릿)

프로젝트 전체에 적용한다. 이 저장소는 Unity 6 URP 프로젝트 시작 템플릿이다.

## 방향
- 예시 수치와 예시 항목(GameplayTableData의 sample.* 항목 등)은 실제 규칙이 아니다. 시작과 함께 교체한다.

## 씬 구성
- 빌드 씬 순서는 `Preload → Title → Game`을 기준으로 한다. Preload에서 테이블·에셋을 준비하고, Title은 진입·시작, Game은 본플레이만 담당하게 나누는 것을 권한다. 템플릿이 이 3개 씬을 만들어 Build Settings에 등록한 상태로 주며, 시작 씬은 Preload다.
- 씬 이름이 키이며 진행 경로의 원본은 `FlowTableData.scenes`다(`nextScene`·`restartScene`). 배열 순서에 의존하지 않고, 빌드 씬 목록과 표가 어긋나면 `ProjectValidation`이 잡는다.

## 구조 규칙
- **asmdef·네임스페이스를 쓰지 않는다.** 모든 런타임 코드는 전역 namespace(Assembly-CSharp)에 컴파일된다.
  - 에디터 코드는 반드시 폴더명이 `Editor`인 아래에 둔다(폴더 규칙으로 Assembly-CSharp-Editor에 들어간다).
  - 테스트 asmdef도 두지 않는다. 테스트 코드는 에디터 테스트 대신 `SmokeCheck`(실행 인자 `-projectSmoke`)과 사용자 플레이 확인으로 대체한다.
  - 외부 패키지의 asmdef(LogKit.Runtime 등)은 `autoReferenced`라 그대로 참조한다.
- **정적 상태 초기화**: `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)`에서 정적 필드·정적 이벤트를 정리한다. EditorSettings에서 도메인 리로드를 꺼두므로 이 규약이 곧 실행 안정성이다.
- 코드를 고칠 때 `AssetDatabase.ImportAsset`를 호출하지 않는다. 에디터 포커스 전환 컴파일을 이용한다.

## 입력
- 모든 조작은 Unity 새 Input System을 사용한다. `Assets/InputSystem_Actions.inputactions`에 맵·액션·바인딩을 추가하고 코드에서 `InputActionAsset`을 Instantiate한 뒤 `FindAction("맵/액션")`으로 찾는다.
- 구 Input API, 키보드·마우스 직접 폴링, 코드에만 정의된 바인딩은 사용하지 않는다.
- UI와 게임 입력이 겹치면 UI 모듈의 Submit/Cancel 액션을 우선 확인한다.

## 테이블
- `Assets/Resources/Tables/<데이터 클래스명>.json` → 순수 직렬화 DTO(`GameTables.cs`, `GameTableRecords.cs`) → 조회 인덱스·싱글톤(`TableManager`) → 검증·파생 계산(`GameTableRules`)의 4계층을 따른다.
- 밸런스·동작 시간·판정 수치는 테이블이 원본이다. 같은 값을 코드 기본값·폴백·씬에 다시 저장하지 않고, UI 수치도 같은 테이블에서 읽는다. 씬 배치·자산 참조·내부 알고리즘 상수는 제외한다.
- 키는 순서 무관 식별자(씬 이름·자산 GUID·id)를 쓰고 배열 순서에 의미를 두지 않는다. 노출용 목록은 결정적 정렬(기본 항목 우선, id 오름차순)을 쓴다.
- `Assets/Resources/LogData.json`은 도메인 테이블이 아닌 분석 로그 운영 설정이다. TableManager로 읽지 않고 `TelemetryLog`가 직접 읽는다(`Docs/TELEMETRY.md`).
- 테이블 오류는 항목별로 모아서 보고하고 `IsValid`로 제공한다. 데이터를 사용하는 초기화는 `RequireValid()`로 차단해 잘못된 기본값으로 동작하지 않게 한다.
- JSON을 저장하면 `TableEditor`가 캐시를 무너뜨린다. 수동 갱신은 `Tools/MyJG/Tables/Reload Tables`(플레이 중 제외).
- 새 테이블을 추가할 때: 데이터 클래스를 만들고 → `GameTables.cs`에 `[Serializable]` 정의 → `TableManager` 생성자에서 `Load<T>(ref loaded)` 한 줄 추가 → `GameTableRules.Validate`에 참조 무결성 검사 추가. 로더나 경로를 개별적으로 만들지 않는다.

## 이벤트 허브
- 게임 도메인 이벤트는 `GameEvents`에 `struct` 페이로드와 함께 추가하고, 생산자는 `Raise*`만 호출한다. 컴포넌트끼리 서로를 찾아 `Notify`하지 않는다.
- 구독은 `OnEnable`/`OnDisable`에서 대칭으로 한다. 해제하지 않으면 정적 이벤트에 갇혀 재진입 시 중복 호출된다.
- 구독자 예외는 발행지에서 삼키고 로그만 남긴다. 페이로드에는 시간·좌표처럼 분석에 쓰는 값을 함께 보낸다.

## 확장(해금) 계약
- 해금 효과는 `IUpgrade`(`Id`/`Apply`/`Remove`)로 표현하고 `UpgradeManager.Register`로 등록한다. ID는 `GameplayTableData.upgrades`의 `id`와 일치시키고 배열 순서에 의존하지 않는다.
- `Apply`는 중복 호출 안전, `Remove`는 적용 여부와 무관하게 호출되게 구현한다.
- 해금 상태는 씬 로컬인 `UpgradeManager`가 보유한다. 전역 테이블·정적 컬렉션에 저장하지 않는다.

## 씬 로컬 자원
- GameObject 풀과 씬 실행 상태는 `SceneServices.For<T>()`가 씬 안에 만든 호스트가 소유한다(ScenePool이 대표적 예). 전역 싱글톤·테이블에 씬 상태를 두지 않는다.
- `Rent`로 대여한 것은 `Return`으로 반납한다. 반납 오브젝트는 Destroy하지 않고 비활성화해 재사용하고, 씬 언로드와 함께 사라진다.
- HUD처럼 미리 배치한 슬롯 풀은 코드가 표시·색·시간만 제어하고 런타임 UI 생성을 하지 않는다.

## 표기 단일 출처
- 색·제목·설명·리치 텍스트 서식은 `Presentation` 한 곳에 둔다. 개별 UI·빌더가 자기 색과 문구를 가지지 않는다.
- 설명 원문은 테이블의 `description`이 원본이고, 비어 있으면 테이블 오류다(코드에 폴백 문구를 두지 않는다).

## UI 패널 온·오프
- 패널은 `PanelBase`를 상속한다. 시작 상태는 `startOpen`으로 코드에서 명시하고, 씬에 저장된 활성 상태에만 의존하지 않는다.
- 비활성화되면 열린 상태·대기 동작을 정리한다. 열린 여부 변화는 `GameEvents.PanelVisibility`로 흐르며 입력·카메라·일시정지 쪽이 구독한다.

## UI 구조·콘텐츠 제작
- UI는 MVP로 나눈다. Model(게임 상태)·View(프리팹에 배치된 표시)·Presenter(표시 갱신)를 분리하고, View 스크립트를 코드에서 생성하지 않는다.
- 메시·오브젝트·레벨 구성은 프로시저럴 생성 대신 프리팹·씬 에디터 기반으로 만든다. 런타임 Instantiate/새 오브젝트 생성은 게임플레이 스폰(적·탄환·효과·풀에서 꺼내는 오브젝트)만 허용한다.

## 코딩 스타일
- 중괄호는 BSD(Allman) 방식이고 여는 중괄호는 새 줄이다. `if`·`for` 같은 한 줄짜리 블록을 만들지 않고 중괄호를 생략하지 않는다.
- 메서드·클래스·프로퍼티는 PascalCase, 필드·로컬 변수는 camelCase(CamelCase). 너무 길어 가독성이 떨어질 때만 snake_case를 허용한다. 타입·스코프 접두사 같은 헝가리안 표기법은 쓰지 않는다.
- **메서드·런타임 파라미터에는 `_` 접두사를 쓴다**(`void SetDamage(int _damage)`). 필드와 이름이 겹쳐도 `this.` 없이 구분된다.
- `const`와 변하지 않는 정적 데이터는 UPPER_SNAKE(`const int MAX_LENGTH = 100;`), 재대입이 있는 정적 상태는 camelCase(`static TableManager instance`)로 구분한다.
- 메서드 파라미터와 지역 변수는 위 표기가 달라 구분되므로 헝가리안 표기처럼 보이지 않는다. 템플릿이 제공한 코드도 이 규칙을 따르므로, 예전 표기(`(string name)` 형태)를 발견하면 같은 작업에서 고친다.
- 커밋 전 Visual Studio `Ctrl+K, R`(정렬)을 적용해 들인 상태로 커밋한다.

## 로그·분석
- 기록 경로를 둘로 나눈다: 분석 사건은 `TelemetryLog`(JSONL, `persistentDataPath/Telemetry`), 콘솔·오류·지표는 LogKit. 같은 사건을 두 경로에 다시 기록하지 않는다(`Docs/TELEMETRY.md`).
- `sessionId`·`sequence`·게임 시간 시계는 `TelemetryLog`의 것이다. 개별 시스템이 자체 `runId`나 `Time.time` 기준 시계를 만들지 않는다.
- 집계 요약은 `WriteSummary(summaryId, payload)`로 남기고 `revision`이 가장 큰 레코드만 정답으로 본다. 고빈도 사건은 개별 줄로 남기지 말고 집계 후 요약한다.
- 게임 사건의 발행은 `GameEvents`, 파일로 쓰는 것은 `TelemetryRecorder` 한 곳이다. 이벤트 이름은 `TelemetryEvents` 상수가 원본이고 필드 대응은 `TelemetryPayload`가 한다.
- 서버 주소·키를 저장소에 두지 않는다. 전송 설정의 원본은 LogKit Inspector/`GameLogger.Configure`/빌드 환경변수다.

## 문서
- README.md는 다음 에이전트가 읽는 짧은 현재 상태 메모다. 사용자용 보고서처럼 관리하지 않는다.
- 기획·구현이 바뀌면 같은 작업에서 관련 항목만 갱신한다. 누적 일지, 날짜별 표, 긴 실행 설명, 도구 호출 기록은 만들지 않는다.
- 확정 규칙, 현재 구현, 미정 사항, 중요한 검증·제약을 짧은 목록으로 유지한다. 수행하지 않은 구현·검증을 완료로 적지 않는다.
- `Docs/TELEMETRY.md`는 기록 경로·세션 계약·설정·검증, `Docs/PLAY_LOG.md`는 이벤트 이름과 필드의 원본이다. 분석 서버 측 이름과 어긋나면 양쪽을 함께 고친다.

## 커밋
- 커밋 전: ① 코드 정리(`Ctrl+K, R`) ② 컴파일 경고 확인(`Tools/MyJG/Validate Project`) ③ 테이블을 건드린 경우 표·씬 값 같이 수정.
- 커밋 메시지는 "[범위] 내용" 형식을 권한다(예: `[tables] 분석 로그 설정 테이블 이동`).

## 작업
- 구현과 씬 연결을 우선한다. 테스트는 사용자가 담당하며, 사용자가 요청할 때만 테스트·플레이 실행·검증용 입력/캡처·원인 조사 목적의 실험을 수행한다.
- 완료 응답은 주요 변경을 짧게 설명하고, 요청받아 테스트한 경우에만 그 결과를 덧붙인다.
