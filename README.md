# JigsawBackpack 메모

Unity 6(URP) 기반의 2D 기차 화물 인벤토리 프로젝트. 에이전트 작업 지침은 AGENTS.md.

## 화물 인벤토리

- Game 씬 왼쪽은 기차, 오른쪽은 현재 도시의 시장이다. `PuzzleTableData`가 기차와 7개 마을(city.a~city.g: 산림·농업·수원·광산·공업·어촌·염전)의 보드 크기·초기 배치를 정의한다. 이전·다음 버튼은 시작 도시 우선, 나머지 ID 오름차순으로 도시를 순환한다.
- 도시를 바꾸면 진행 중 드래그를 취소하고 오른쪽 시장을 교체한다. 씬 로컬 세션이 기차와 도시별 시장의 화물 ID·위치·회전을 유지한다. **도시를 떠나면 그 도시의 시장만 테이블의 시작 배치로 되돌아간다.** 새 인스턴스 ID를 발급해 과거 배치는 남지 않고, 재방문하면 항상 테이블과 같은 배치다. 기차는 도시 이동과 무관하게 인스턴스·좌표·회전을 유지하고, Game 씬을 다시 로드하면 기차도 테이블의 시작 배치로 초기화한다.
- `PuzzleTableData.pieces`의 샘플 아이템 정의가 이름·설명·모양·태그·아이콘 키를 제공하고, `PuzzleItemPriceTableData`가 아이템 ID별 기본 가치의 유일한 원본이다. 같은 종류의 화물은 정의를 공유하며 각 인스턴스가 소속·좌표·회전을 가진다. 기존 아이콘 키를 쓰면 테이블만 편집해 아이템을 추가할 수 있다.
- 시장은 모두 7×6이고 생산품을 3개씩 배치한다. 곡물은 `Grain`으로 구분한다. 풍부/필요의 임시 구매·판매 배율은 각각 80/75%, 120/110%다. 염전은 소금이 풍부하고 기계를 필요로 한다. 배치 기준은 왼쪽 위이므로 높이 2칸 화물은 y가 1 이상이어야 한다. 가격 밸런스·시너지 재설계는 미완료다.
- 아이템별 이미지는 배치와 드래그 고스트에서 격자 크기와 회전에 맞춰 표시된다. 화물에 마우스를 올리면 마우스 커서를 추적하며 크기가 가변적으로 조절되는 호버 정보 패널(물건 이름, 항목 테마별 라운드 직사각형 종류 태그 배지, 실제 가치, 거래 배율, 적용 시너지 목록, 다음 턴 반영 미래 가치)이 표시된다. 기존 하단 고정 상세 텍스트는 호버 정보 패널로 대체되었다. 기차에는 같은 종류의 목재 묶음 두 개를 서로 다른 위치와 회전으로 배치했다.
- Inventory Canvas와 공용 인벤토리 프리팹의 텍스트는 NotoSansKR TextMeshPro를 사용한다. 기존 크기 16~24는 자동 크기 16~24로 설정했다.
- `PuzzleSynergyTableData`의 규칙은 같은 보드의 상하좌우 인접 화물에만 적용된다. 같은 쌍의 접촉 면이 여러 개여도 한 번 평가한다. 증가는 기본 가치 기준으로 합산하고 감소는 그 결과에 곱한다. 화물 상세에 이웃별 이름·비율·가치 변화량을 표시한다. 물고기는 `excludedTargetTag`를 사용해 주변 비물고기 화물의 가치를 0.8배로 낮춘다.
- 기차의 인접 시너지는 효과를 주는 화물에서 받는 화물 방향의 짧은 화살표로 표시한다. 증가와 감소는 각각 초록색과 빨간색이며, 같은 쌍의 여러 접촉 면 중 계산에 사용한 한 곳에 표시한다. 시장에는 화살표를 표시하지 않는다.
- 인접 시너지 결과는 배치만 바꿔도 실제 가치를 즉시 바꾸지 않는다. 도시를 이동할 때마다 기차 화물별로 기존 시너지 결과에서 기본 가치를 뺀 변화량을 실제 가치에 누적한다. 실제 가치는 화물 상세에 표시되고 보드 이동·도시 재방문에도 유지된다.
- 같은 보드 안의 드래그는 무료다. 구매가는 아이템 기본 가치, 판매가는 시너지로 누적된 화물의 실제 가치(ActualValue)에 도시·태그별 비율을 적용한다. 태그 규칙이 여럿 맞으면 priority가 가장 높은 하나를 쓰고, 없으면 도시의 명시적 기본 비율을 쓴다. 드래그 미리보기와 거래 결과에는 적용된 태그·비율이 함께 표시되고, 화물 상세에는 현재 도시의 선택 태그와 구매·판매 비율이 표시된다. `PuzzleTableData.cities`가 도시별 거래 허용 여부와 기본·태그별 가격 비율을 정하고, `PuzzleTradeTableData`는 시작 자금만 정한다. 도시 전환 중에도 자금을 유지한다. 오른쪽 시장 위에는 현재 도시의 태그를 구매·판매 비율 모두 100 초과면 `귀함`, 모두 100 미만이면 `흔함`으로 표시하고, 두 비율이 100을 사이에 두고 갈리면 그 태그는 표시하지 않는다. 시장 보드의 종합 가치 텍스트는 쓰지 않는다.
- 드래그 중 R로 잡은 슬롯 기준으로 회전한다. 보드 사이 드래그는 예상 가격과 실패 이유를 표시한다. 유효한 배치는 초록색이며, 배치나 거래가 불가능하면 빨간색이다. 놓는 순간 원본·대상 점유·자금을 다시 판정하고 성공 시 인스턴스 ID를 유지한다. 실패하거나 취소하면 배치와 자금을 보존한다.
- 두 View는 같은 `PuzzleInventoryUI` 프리팹의 보드·배치·고스트·미리보기 각 42슬롯을 사용한다. 기존 `PuzzleInventory`와 Unity Input System을 사용하며, 외부 패키지 조사와 적용 범위는 구현 계획서의 1단계에 기록했다.
- 도시 이동 한 번이 한 턴이다. 이동 비용은 `PuzzleRouteTableData`의 방향별 노선이 정하며 출발 직전 자금이 부족하면 **이동이 차단**된다(턴·도시·시장·기차 가치·자금 모두 그대로). 7개 마을 순환에 필요한 양방향 노선을 정의하며, 확장 구간은 임시 비용(다음 6·이전 3)을 사용한다. 비용은 출발 전에, 빚은 도착 후에 차감한다. 이동 2·4·6회에 50·100·200의 빚을 차감하며, 차감 전 자금이 부족하면 패배, 마지막 빚을 전액 갚으면 잔액이 0이어도 승리한다. 이동마다 기차 화물 실제 가치가 변하고, 종료 후 이동·거래·회전은 차단되며 현재 상태 문구를 표시한다. 이동 버튼에는 목적지 노선 비용이 함께 표시된다.

## 기반 구조
- **어셈블리**: asmdef·네임스페이스 없음. 런타임 `Assets/Scripts/Runtime`, 에디터 `Assets/Scripts/Editor`(폴더 규칙). 테스트 코드는 asmdef 없이는 컴파일되지 않으므로 두지 않고, 대신 `SmokeCheck`로 기동 검사를 대신한다.
- **테이블**: `Assets/Resources/Tables/*.json` → DTO → `TableManager.Instance`(지연 로드·`RequireValid`·`Reload`·에디터 디스크 원본 우선 읽기) → `GameTableRules`(인덱스 중복·참조 무결성·리플렉션 숫자 검사).`Tools/MyJG/Tables/Reload Tables`와 JSON 저장 시 자동 캐시 무효화.
- **이벤트 허브**: `GameEvents`가 Run/Scene/Upgrade/Panel/Trade 이벤트를 발행한다. 구독자 예외 격리, `NextId()` 진단 순번, 씬 진입 자동 발행을 제공한다.
- **확장 계약**: `IUpgrade`(Id/Apply/Remove) + `UpgradeManager`(Register/Grant/GrantNext/Capture/Restore, 씬 로컬 상태).
- **씬 로컬 자원**: `SceneServices.For<T>()`(씬 안 호스트 생성) + `ScenePool`(key별 Rent/Return/prewarm, 씬과 함께 사라짐).
- **씬 골격**: `Preload → Title → Game` 3개 씬을 만들고 Build Settings에 등록했다(시작 씬 = Preload). 진행 경로의 원본은 `FlowTableData.scenes`이고, ProjectSettings의 기본 씬도 Preload를 가리킨다. URP 샘플 씬(SampleScene)은 Game으로 흡수하고 제거했다.
- **표기 단일 출처**: `Presentation`(EffectCategory 색·제목, 리치 텍스트 Accent/Dim/Heading, 테이블 description 원본 참조).
- **UI 패널**: `PanelBase`(startOpen으로 시작 상태 명시, OnDisable 정리, `GameEvents.PanelVisibility` 발행).
- **레이어**: `GameLayers`(7 Ground / 8 Obstacle / 9 Player / 10 Enemy / 11 Prop / 12 Weapon / 13 Projectile)와 TagManager.asset 등록 완료.
- **LogKit**: git 패키지(v0.7.0) 설치, `LogKitSetup`이 `LOGKIT_SERVER_URL`/`LOGKIT_UPLOAD_KEY` 환경변수를 기동 시 적용. Debug.Log 계열은 자동 수집·전송.
- **분석 로그**: `TelemetryLog`가 sessionId·sequence·FixedUpdate 시간의 단일 소유자(JSONL은 `persistentDataPath/Telemetry`, LogKit 전송에 자동으로 포함되지 않음). `TelemetryRecorder`가 GameEvents를 구독해 한 곳에서 쓰고, 이벤트 이름·필드 원본은 `TelemetryEvents`/`TelemetryPayload`, 스위치는 `Resources/LogData.json`. 계약과 검증은 `Docs/TELEMETRY.md`.
- **설정**: Active Input Handling = Both(신규 Input System만 사용 권장, Validate Project가 안내), Enter Play Mode Options = 도메인 리로드 해제(정적 초기화 규약 필수).
- **도구**: `Tools/MyJG/Validate Project`(빈 스크립트·테이블·빌드 씬·레이어 동기화·입력 설정·LogData 유무), `Tools/MyJG/Verify Telemetry File`(최신 분석 파일의 session/sequence/revision 계약), `Tools/MyJG/Reveal Telemetry Folder`, `Tools/MyJG/Debug/Jump To Scene`, `Tools/MyJG/Build Windows (Development)`(활성 씬 사용 → `Artifacts/Windows`).
- **기동 검사**: 실행 인자 `-projectSmoke` → 테이블 검증·풀 왕복·해금/스냅샷 복원·이벤트 왕복·분석 로그 세션 계약까지 확인 후 `MYJG_SMOKE_PASS/FAIL`과 종료 코드.

## 프로젝트를 시작할 때 할 일
1. 샘플 데이터를 지운다: `GameplayTableData.json`의 `sample.*` 해금 항목과 예시 필드, `FlowTableData.json`의 씬 이름(기본은 Preload·Title·Game). 실제 씬을 추가하면 Build Settings와 표 양쪽에 같은 이름으로 등록한다.
2. `GameLayers`와 TagManager 레이어를 실제 게임에 맞게 고치고 `Tools/MyJG/Validate Project`로 확인한다.
3. 메뉴·로그의 `MyJG` 접두사를 프로젝트명으로 바꾼다(에디터 스크립트 내 문자열, `MYJG_SMOKE_*`).
4. `IUpgrade` 구현체를 만들어 `UpgradeManager.Register`로 연결하고, 패널은 `PanelBase`를 상속해 `startOpen`을 명시한다.
5. 분석 서버가 있으면 `Docs/PLAY_LOG.md`의 이벤트 이름을 서버 측과 맞춘다. 분석 파일의 전송·수집은 별도 작업이라 필요하면 직접 붙인다(`Docs/TELEMETRY.md`).

## 검증 · 미정
- 노선 비용과 시장 재설정(①~④)은 구현했으나 Play Mode 확인은 남았다. `PuzzleRouteTableData.json`의 비용은 `status: sample`이며 빚 일정 대비 밸런스를 아직 조정하지 않았다. 시장은 완전 재설정(a안)이라 재방문 시 시작 배치 화물이 복제된다.
- ⑤ 이자 부족 시 기차 화물 40% 강제매각은 계약만 확정되고 미구현이다(현재는 부족하면 즉시 패배). 설계는 `Docs/노선 비용과 시장 초기화 구현 계획.md`의 3단계.
- 5단계의 도시 간 구매·판매와 실패 시 상태 보존은 Play Mode에서 확인할 항목이다. 가격과 시너지 배율은 개발용 샘플이다. LogKit 패키지 버전은 `packages-lock.json`에 기록되어 있다.
- Preload·Title·Game 씬의 에디터 로드 확인이 남아 있다.
- Preload·Title은 씬 전환을 실행할 컴포넌트가 비어 있다. 인벤토리는 Game 씬에서 바로 진입한다.
- 코딩 스타일: 전 스크립트를 AGENTS 규칙(Allman, 파라미터 `_` 접두사, const UPPER_SNAKE, 한 줄 블록 금지)에 맞게 일괄 정리했다. 리터럴 문자열 안 텍스트와 이벤트/직렬화 필드 이름은 바꾸지 않았다.
- 분석 파일의 서버 전송·수집기는 없다(JSONL은 LogKit 업로드 대상에 자동으로 포함되지 않음). 파일 생성과 계약 검증(`Verify Telemetry File`)까지 제공한다.
- 씬 진행 상태의 판 단위 인계(장비·체력 이어받기)는 템플릿에 없다. 필요하면 명시적으로 추가한다.
- 오디오·저장·씬 로더(페이드) 공용 서비스는 없다. 게임이 정하면 테이블 4계층 규약으로 수치를 옮긴다.
- 테스트 asmdef가 없어 EditMode/PlayMode Test Runner 항목은 비어 있다. 테스트가 필요해지면 asmdef 예외를 만들지 정한다.
- LogKit 수신 서버와 환경변수는 이 PC에서 확인하지 않았다. 빌드 시 값이 없으면 LogKit 빌드 단계가 실패한다(`LOGKIT_OPTIONAL=1`로 로컬 우회).
