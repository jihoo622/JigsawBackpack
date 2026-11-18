# 로그 기록·운영 계약

| 경로 | 내용 | 위치 | 소유자 |
|---|---|---|---|
| 게임 분석 | 시작 상태·확정 행동·변화의 원본 행 | LogKit Data의 Telemetry + 로컬 persistentDataPath/Telemetry/{sessionId}.jsonl | TelemetryLog / TelemetryRecorder |
| 분석 원본 표 | 내용 해시별 테이블 JSON | LogKit Data의 TelemetryRules + 로컬 persistentDataPath/Telemetry/Rules/<hash>.json | TelemetryPayload.PuzzleState |
| 콘솔·오류 | Unity 콘솔·예외 스택·서버 전송 | persistentDataPath/Logs/{sessionId}.jsonl | LogKit v0.7.0 |

게임 사건은 GameEvents에서 발행하고 TelemetryRecorder가 TelemetryLog에 넘긴다. TelemetryLog는 원본 행을 한 번 직렬화해 LogKit Data로 보내고 같은 내용을 로컬에 보관한다. Debug.Log로 게임 사건을 다시 출력하지 않으므로 콘솔 자동 수집과 이중 전송되지 않는다.

**콘솔·게임 분석·적용 테이블을 모두 LogKit으로 전송한다.** 기존 수신 API/주소/키를 사용한다. 게임 원본 계약은 schemaVersion 9다. 6에서 작업대·화차 행동, 작업대 보드 관측, `transformed`, `workbench_ready`·`route_stop_changed`·`station_screen`을 더했고, 7에서 게임 시간을 턴(`elapsedTurns` 등)으로 바꾸고 역 시설 진입 행동(`enter_market`·`enter_tavern`·`enter_wagon_facility`)을 더했으며, 8에서 정수 게임 시간(`elapsedHours` 등)과 실시간 시계(1시간마다 `advance_time`), 출발 `depart`를 더했고, 9에서 턴제(1턴 = 6시간)로 바꿔 `depart`와 시간당 `advance_time`을 없애고 경로 계획을 `totalTurns`로 바꿨다. 분석 가공·집계·대시보드는 별도 저장소 `logkit-analytics-trainventory`(`/trainventory`)가 수행하며 schemaVersion 3~6을 읽는다. 수신 서버 CSV는 DataJson을 4000자에서 잘라 `rules_chunk`는 서버 미러에서 복원되지 않는다. [PLAY_LOG](PLAY_LOG.md)

## 전송 계약

- `GameLogger.Data("Info", "Telemetry", eventName, 원본행JSON)`을 호출한다. entry.dataJson을 파싱하면 로컬 JSONL과 같은 TelemetryLine이며 그 안의 dataJson을 파싱하면 게임 payload다.
- 테이블은 `TelemetryRules` 카테고리의 `rules_chunk`로 보낸다. sessionId/rulesHash/chunkIndex/chunkCount/contentBase64를 담고, Base64를 조각별 디코딩한 바이트를 인덱스 순서로 합치면 해시 대상 원본 파일이 된다.
- 테이블은 앱 세션 안에서 해시별 한 번만 큐에 넣는다. 로컬 캐시가 이미 있어도 새 앱 세션에서는 전송한다. 서버의 기존 테이블 보유 여부를 추측하지 않는다.
- 서버 도착 순서를 게임 순서로 사용하지 않는다. 분석 중복 키는 내부 `(sessionId, sequence)`, 테이블 조각은 `(sessionId, rulesHash, chunkIndex)`다. 여러 앱 세션에서 모인 같은 해시의 완성 원본은 하나로 보관할 수 있다.
- 기동 준비는 Preload 씬의 `AppBootstrap`이 Awake에서 `LogKitSetup.Initialize` → `TelemetryLog.Initialize`(세션 열기, GameLogger.Init) → `TelemetryRecorder.Initialize` 순으로 한 번 한다. LogKit 자동 부트스트랩(AfterSceneLoad)과 첫 scene_change보다 앞선다. 분석 세션을 열 때 GameLogger.Init을 호출해 Data가 로거 생성 전 버려지지 않게 한다. Preload를 거치지 않고 다른 씬으로 시작하면 AppBootstrap이 첫 씬 Awake 전에 같은 준비를 하고 경고를 남긴다(이때 GameLogger는 패키지 코드 기본값으로 만들어진다).
- 프로젝트 종료 훅이 분석 run_end/session_end를 큐에 넣은 다음 LogKit의 종료 플러시가 실행된다. 통신 실패분의 보관·다음 기동 재시도는 LogKit을 사용한다. 강제 종료나 네트워크 장애에서 즉시 서버 도착까지 보장하는 것은 아니다.
- 로컬 파일 열기 실패는 분석 세션을 막지 않는다. 서버 전송을 먼저 큐에 넣으므로 로컬 보관에 실패해도 수집 원본은 계속 보낸다.

## 세션과 시간

- TelemetryLog가 분석 sessionId, sequence, FixedUpdate 누적 초 time을 소유한다. timestampUtc는 실제 UTC다. LogKit 업로드 봉투의 sessionId는 패키지의 별도 ID이므로 게임 판 식별에 사용하지 않는다.
- 게임 달력은 PuzzleTimeManager.ElapsedHours(정수 게임 시간)가 소유한다. 분석기는 elapsedHours로 관측하며 별도 게임 시계를 만들지 않는다. schemaVersion 7은 턴 단위(elapsedTurns, 1턴=3시간), 6 이하는 분 단위(elapsedMinutes)다.
- 앱/플레이 시작에 세션 하나를 연다. 재시작은 같은 세션에서 판만 닫고 다시 연다. 외부 판 키는 `(sessionId, run_start.sequence)`다.
- BeforeClose에서 남은 판의 run_end를 기록한 뒤 session_end를 쓴다. closing으로 Close 재진입을 막고 closed 이후 Write는 경고 한 번 후 무시한다. 강제 종료는 끝 레코드를 보장하지 않는다.
- 한 줄마다 sequence를 증가시킨다. dataJson은 payload JSON 문자열이다. JsonUtility가 지원하는 Serializable 클래스/구조체를 사용하며 decimal은 invariant 문자열로 남긴다.
- WriteSummary(summaryId, payload)의 최신 revision 채택 계약은 확장용으로 유지한다. 현재 집계는 외부에서 수행하며 summary를 발행하지 않는다.

## 설정·보관

- 분석 설정은 Assets/Resources/LogData.json의 enabled/bufferLines/retentionDays다. 현재 true / 16줄 / 14일이다. enabled는 게임 원본 수집·전송·로컬 보관의 스위치이며 콘솔 자동 수집과는 독립적이다. bufferLines/retentionDays는 로컬 보관본에만 적용한다.
- 오래된 세션 JSONL은 기동 시 retentionDays로 정리한다. 공유 Rules는 자동 삭제하지 않는다. 외부 보관 완료 후 참조가 없는 Rules를 정리할 수 있다.
- Rules는 같은 테이블 내용을 한 번만 보관한다. 변경 데이터는 다른 해시 파일을 만든다. 보관 실패는 오류로 남기되 run_start를 누락시키지 않는다.
- LogKit 수집·전송 동작(콘솔 수집, flush·업로드 주기, 종료·일시정지 전송, 기동 재시도, 요청 제한 시간)은 Preload 씬 `LogKit` 오브젝트의 GameLogger Inspector에서 정한다. 코드는 이 값을 Configure로 덮지 않는다. 같은 Inspector의 서버 주소·업로드 키도 여기서 정할 수 있고 값이 있으면 가장 우선한다. 업로드 키는 씬에 저장하면 저장소에 남으므로 빈 값으로 두는 것을 원칙으로 하며, ProjectValidation이 빌드 씬에 저장된 키를 문제로 보고한다(주소는 안내만 한다).
- 서버 주소·업로드 키 우선순위는 GameLogger Inspector > 환경 변수(에디터만, Inspector가 빈 값이거나 주소가 패키지 기본값인 항목만 채움) > Resources/LogKitBuildConfig.json(그래도 빈 키·기본 주소 자리만 채움) > 패키지 기본값(localhost, 키 없음)이다. LogKit 요청에는 프로젝트 필드가 없고 `X-Api-Key` 헤더의 키만 보낸다. 앱 버전은 `Application.version`(PlayerSettings bundleVersion, 현재 0.1.0 고정)이다. 서버는 업로드 키로만 프로젝트를 정하며 이 게임의 프로젝트는 `trainven_kjgl6b2`다. `dev-log-key`는 다른 게임 프로젝트로 분류되고 등록되지 않은 키는 401로 거부된다(빌드는 성공하고 로그는 로컬에만 쌓인다).
- 빌드: LogKit 빌드 단계가 빌드 PC(CI `Trainventory` 항목)의 LOGKIT_SERVER_URL, LOGKIT_UPLOAD_KEY로 LogKitBuildConfig를 만들어 넣고 빌드 후 지운다. 두 값이 없으면 빌드가 실패한다. LogKitSetup은 빌드 실행 중 환경 변수를 읽지 않는다.
- 에디터: LogKitSetup이 Preload의 GameLogger보다 먼저(PreloadController 실행 순서 -1000) Inspector 값을 읽고, 비어 있는 항목만 환경 변수로 Configure한다. 여러 게임을 같은 PC에서 작업하면 사용자 범위에 두지 말고 에디터를 띄우는 셸에서만 설정한다. 사용자 범위에 다른 게임 키가 있으면 지우고 Unity Hub·에디터를 완전히 종료한 뒤 다시 연다. 전송하지 못한 로그는 다음 실행의 주소·키로 재전송되므로 그때 설정된 프로젝트로 분류된다.
- 저장소에 추적 중인 Resources/LogKitBuildConfig.json에는 `dev-log-key`가 들어 있어 환경 변수 없이 에디터에서 플레이한 로그는 이 게임 프로젝트로 분류되지 않는다. 정리 방법(추적 해제 등)은 미정이다. 주소·키를 문서나 분석 로그에 복제하지 않는다. LOGKIT_OPTIONAL / LOGKIT_ALLOW_LOCALHOST 예외는 패키지 계약을 따른다.
- 확인: 빌드 콘솔의 `[LogKit] 빌드 설정을 넣었습니다: ... 업로드 키 16자`(11자면 dev-log-key), GM 툴에서 `trainven_kjgl6b2` 프로젝트에 세션이 보이는지, `Api Key가 비어 있어 서버 업로드가 거부됩니다` 경고가 없는지.
- LogKit은 Unity 로그 자동 수집과 분석 Data를 같은 전송 큐로 처리한다. 기본 5초 주기 및 종료/백그라운드 전환 시 gzip JSON을 POST하고 통신 실패분은 다음 기동에 재시도한다. 프로젝트에서 Count/Measure 집계는 별도로 발행하지 않는다.

## 확인 범위

- Tools/MyJG/Verify Telemetry File은 최근 파일의 파싱·sessionId·sequence·세션 경계·summary revision을 확인한다. 게임 값이나 버전 3 재구성까지 검증하는 도구는 아니다.
- -projectSmoke는 명시적으로 실행했을 때 기존 세션/파일 기본 계약을 확인한다. 판 종료가 추가될 수 있으므로 Close 후 줄 수를 기준으로 이중 종료를 확인한다.
- Tools/MyJG/Reveal Telemetry Folder로 폴더를 연다.
- 사용자 플레이 확인 대상은 시작/재시작·승패 경계, 드래그/즉시 거래 한 건당 한 행동, 실패 시 변화 없음, 시너지 경계 전후 가치 변화, 날짜 변경 부산물, 시장 재방문 재고, 소문 획득 후 이동이다. 이번 변경에서 컴파일·플레이·입력 실험은 수행하지 않았다.
