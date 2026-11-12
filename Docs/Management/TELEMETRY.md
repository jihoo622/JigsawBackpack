# 로그 기록·운영 계약

| 경로 | 내용 | 위치 | 소유자 |
|---|---|---|---|
| 게임 분석 | 시작 상태·확정 행동·변화의 원본 행 | LogKit Data의 Telemetry + 로컬 persistentDataPath/Telemetry/{sessionId}.jsonl | TelemetryLog / TelemetryRecorder |
| 분석 원본 표 | 내용 해시별 테이블 JSON | LogKit Data의 TelemetryRules + 로컬 persistentDataPath/Telemetry/Rules/<hash>.json | TelemetryPayload.PuzzleState |
| 콘솔·오류 | Unity 콘솔·예외 스택·서버 전송 | persistentDataPath/Logs/{sessionId}.jsonl | LogKit v0.7.0 |

게임 사건은 GameEvents에서 발행하고 TelemetryRecorder가 TelemetryLog에 넘긴다. TelemetryLog는 원본 행을 한 번 직렬화해 LogKit Data로 보내고 같은 내용을 로컬에 보관한다. Debug.Log로 게임 사건을 다시 출력하지 않으므로 콘솔 자동 수집과 이중 전송되지 않는다.

**콘솔·게임 분석·적용 테이블을 모두 LogKit으로 전송한다.** 기존 수신 API/주소/키를 그대로 사용한다. 서버에서 entries의 카테고리별 데이터를 추출하는 방법은 [PLAY_LOG](PLAY_LOG.md)에 명시했다. 게임 원본 계약은 schemaVersion 3이며 서버의 분석 가공·집계·대시보드는 외부에서 수행한다.

## 전송 계약

- `GameLogger.Data("Info", "Telemetry", eventName, 원본행JSON)`을 호출한다. entry.dataJson을 파싱하면 로컬 JSONL과 같은 TelemetryLine이며 그 안의 dataJson을 파싱하면 게임 payload다.
- 테이블은 `TelemetryRules` 카테고리의 `rules_chunk`로 보낸다. sessionId/rulesHash/chunkIndex/chunkCount/contentBase64를 담고, Base64를 조각별 디코딩한 바이트를 인덱스 순서로 합치면 해시 대상 원본 파일이 된다.
- 테이블은 앱 세션 안에서 해시별 한 번만 큐에 넣는다. 로컬 캐시가 이미 있어도 새 앱 세션에서는 전송한다. 서버의 기존 테이블 보유 여부를 추측하지 않는다.
- 서버 도착 순서를 게임 순서로 사용하지 않는다. 분석 중복 키는 내부 `(sessionId, sequence)`, 테이블 조각은 `(sessionId, rulesHash, chunkIndex)`다. 여러 앱 세션에서 모인 같은 해시의 완성 원본은 하나로 보관할 수 있다.
- 초기 분석 세션을 열 때 GameLogger.Init을 호출해 Data가 로거 생성 전 버려지지 않게 한다. LogKitSetup은 콘솔 수집·종료 전송·기동 재시도를 명시적으로 켠다.
- 프로젝트 종료 훅이 분석 run_end/session_end를 큐에 넣은 다음 LogKit의 종료 플러시가 실행된다. 통신 실패분의 보관·다음 기동 재시도는 LogKit을 사용한다. 강제 종료나 네트워크 장애에서 즉시 서버 도착까지 보장하는 것은 아니다.
- 로컬 파일 열기 실패는 분석 세션을 막지 않는다. 서버 전송을 먼저 큐에 넣으므로 로컬 보관에 실패해도 수집 원본은 계속 보낸다.

## 세션과 시간

- TelemetryLog가 분석 sessionId, sequence, FixedUpdate 누적 초 time을 소유한다. timestampUtc는 실제 UTC다. LogKit 업로드 봉투의 sessionId는 패키지의 별도 ID이므로 게임 판 식별에 사용하지 않는다.
- 게임 달력은 기존 PuzzleTimeManager.ElapsedMinutes가 소유한다. 분석기는 elapsedMinutes로 관측하며 별도 게임 시계를 만들지 않는다.
- 앱/플레이 시작에 세션 하나를 연다. 재시작은 같은 세션에서 판만 닫고 다시 연다. 외부 판 키는 `(sessionId, run_start.sequence)`다.
- BeforeClose에서 남은 판의 run_end를 기록한 뒤 session_end를 쓴다. closing으로 Close 재진입을 막고 closed 이후 Write는 경고 한 번 후 무시한다. 강제 종료는 끝 레코드를 보장하지 않는다.
- 한 줄마다 sequence를 증가시킨다. dataJson은 payload JSON 문자열이다. JsonUtility가 지원하는 Serializable 클래스/구조체를 사용하며 decimal은 invariant 문자열로 남긴다.
- WriteSummary(summaryId, payload)의 최신 revision 채택 계약은 확장용으로 유지한다. 현재 집계는 외부에서 수행하며 summary를 발행하지 않는다.

## 설정·보관

- 분석 설정은 Assets/Resources/LogData.json의 enabled/bufferLines/retentionDays다. 현재 true / 16줄 / 14일이다. enabled는 게임 원본 수집·전송·로컬 보관의 스위치이며 콘솔 자동 수집과는 독립적이다. bufferLines/retentionDays는 로컬 보관본에만 적용한다.
- 오래된 세션 JSONL은 기동 시 retentionDays로 정리한다. 공유 Rules는 자동 삭제하지 않는다. 외부 보관 완료 후 참조가 없는 Rules를 정리할 수 있다.
- Rules는 같은 테이블 내용을 한 번만 보관한다. 변경 데이터는 다른 해시 파일을 만든다. 보관 실패는 오류로 남기되 run_start를 누락시키지 않는다.
- LogKit 설정은 GameLogger Inspector / Configure / Resources/LogKitBuildConfig.json이다. LogKitSetup은 LOGKIT_SERVER_URL, LOGKIT_UPLOAD_KEY 환경변수를 기동 시 적용한다.
- 현재 저장소의 LogKitBuildConfig에는 연결 값이 존재하므로 전송 기본값 꺼짐으로 간주하지 않는다. 주소·키를 문서나 분석 로그에 복제하지 않는다. 빌드 필수 환경변수와 LOGKIT_OPTIONAL / LOGKIT_ALLOW_LOCALHOST 예외는 패키지 계약을 따른다.
- LogKit은 Unity 로그 자동 수집과 분석 Data를 같은 전송 큐로 처리한다. 기본 5초 주기 및 종료/백그라운드 전환 시 gzip JSON을 POST하고 통신 실패분은 다음 기동에 재시도한다. 프로젝트에서 Count/Measure 집계는 별도로 발행하지 않는다.

## 확인 범위

- Tools/MyJG/Verify Telemetry File은 최근 파일의 파싱·sessionId·sequence·세션 경계·summary revision을 확인한다. 게임 값이나 버전 3 재구성까지 검증하는 도구는 아니다.
- -projectSmoke는 명시적으로 실행했을 때 기존 세션/파일 기본 계약을 확인한다. 판 종료가 추가될 수 있으므로 Close 후 줄 수를 기준으로 이중 종료를 확인한다.
- Tools/MyJG/Reveal Telemetry Folder로 폴더를 연다.
- 사용자 플레이 확인 대상은 시작/재시작·승패 경계, 드래그/즉시 거래 한 건당 한 행동, 실패 시 변화 없음, 시너지 경계 전후 가치 변화, 날짜 변경 부산물, 시장 재방문 재고, 소문 획득 후 이동이다. 이번 변경에서 컴파일·플레이·입력 실험은 수행하지 않았다.
