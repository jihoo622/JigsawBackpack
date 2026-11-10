# 통합 로그 규약 (분석 파일 / LogKit)

두 기록 경로를 목적별로 분리한다. 같은 게임 사건을 두 경로에 다시 기록하지 않는다.

| 경로 | 무엇 | 어디 | 누가 |
|---|---|---|---|
| 분석 로그 | 게임 사건·경제·진행의 JSONL | `Application.persistentDataPath/Telemetry/{sessionId}.jsonl` | `TelemetryLog` (이 저장소의 코드) |
| 콘솔·오류 수집 | `Debug.Log` 계열·예외 스택·지표 집계와 서버 전송 | `Application.persistentDataPath/Logs/{sessionId}.jsonl` | LogKit 패키지(`com.team.logkit`) |

**분석 JSONL은 LogKit 업로드 대상에 자동으로 포함되지 않는다.** 서버 수집기를 붙이는 일은 별도 작업이며, 이 템플릿은 파일 생성과 계약 검증까지 제공한다.

## 세션 계약

1. **단일 소유자**: `sessionId`·`sequence`·게임 시간 시계는 `TelemetryLog`만 만든다. 개별 시스템이 자체 `runId`나 `Time.time`/`DateTime`을 섞어 별도 시계를 만들면 판 대조가 깨진다.
2. **`sequence`**: 한 줄 쓸 때마다 1씩 증가한다. 파일 안에서 누락·중복이 없어야 하며, 분석 쪽은 이것으로 순서를 되짚는다.
3. **`time`**: `FixedUpdate` 누적 게임 시간. 일시정지·선택 화면에서 멈추는 기준을 이 값 하나로 통일한다. 벽시계 시간이 필요하면 같은 줄의 `timestampUtc`를 함께 본다.
4. **`summaryId` / `revision`**: 요약은 `WriteSummary`로 남기고 `revision`은 자동 증가한다. 같은 `summaryId`에서는 **revision이 가장 큰 레코드만** 채택한다(부분 요약 재사용 금지).
5. **경계**: `session_start`로 시작하고 `session_end`로 끝난다. 종료 이후에는 `Write`가 경고 한 번 후 무시되므로, 강제 종료가 아닌 한 종료 기록 뒤에 줄이 붙지 않는다. 종료는 `LogKitSetup.BeforeShutdown`과 `OnApplicationQuit`에서 들어오지만 **종료 표시를 먼저 세워** 이중 기록을 막는다.
6. **`dataJson`**: payload JSON을 이스케이프한 문자열로 넣는다(LogKit `LogEntry.dataJson`과 같은 형태). payload는 `[Serializable]` 구조체만 직렬화된다(JsonUtility는 익명 타입을 처리하지 못한다). 필드 이름·뜻을 바꾸지 말고 새 필드를 추가한다.

```csharp
// 이벤트 한 줄(이름은 TelemetryEvents에 상수로 둔다)
TelemetryLog.Write(TelemetryEvents.RUN_END, new RunPayload { scene = "Game", time = 124.5, reason = "clear" });

// 요약: 같은 summaryId의 최신 revision이 정답이 된다
wave.counted = killed;
TelemetryLog.WriteSummary($"wave:{wave.index}", wave);   // revision 1, 2, 3 ...

// 고빈도 사건은 개별 줄 대신 집계 후 요약으로 남긴다(행 크기·프레임).
```

## 설정

- **분석 로그**: `Assets/Resources/LogData.json`이 원본이다(`enabled`, `bufferLines`, `retentionDays`). 파일이 없으면 분석 경로만 꺼지고 콘솔 수집은 계속 동작한다. 코드에 폴백 수치를 두지 않는다.
- **LogKit 전송**: 씬의 `GameLogger` Inspector, 또는 `GameLogger.Configure(...)`로 넣은 값이 원본이다. 빌드에서는 LogKit 빌드 단계가 CI 환경변수 `LOGKIT_SERVER_URL`, `LOGKIT_UPLOAD_KEY`를 읽어 `Assets/Resources/LogKitBuildConfig.json`에 구워 넣는다(값 없으면 빌드 실패, 로컬은 `LOGKIT_OPTIONAL=1`, localhost는 `LOGKIT_ALLOW_LOCALHOST=1`).
- **로컬 오버라이드**: 에디터에서는 같은 이름의 환경변수를 PC에 설정하면 `LogKitSetup`이 기동 시 적용한다. 서버 주소·키를 저장소에 커밋하지 않고, 전송 기본값은 꺼 두었다.
- 오래된 분석 파일은 기동 시 `retentionDays` 기준으로 정리한다. 서버 전송은 별도 구현이 필요하다.


## 검증

- **플레이 후**: `Tools > MyJG > Verify Telemetry File`이 가장 최근 분석 파일의 sessionId·sequence 연속성·session_start/end 경계·summaryId revision·파싱 실패를 검사한다. 씬을 바꾸거나 게임을 실행하지 않는다.
- **기동 검사**: `-projectSmoke` 실행 인자(SmokeCheck)가 세션 열기 → 기록 → 이중 종료 방지 → 줄 수 계약을 확인한다.
- **폴더 열기**: `Tools > MyJG > Reveal Telemetry Folder`.
- 파일 계약이 통과해도 **값의 정확성은 확인하지 못한다**. 보상·피해·진행이 실제로 맞는지는 아래 항목을 플레이로 확인한다(사용자 담당).
  - 판 시작·종료, 재시작 시 새 sessionId, 종료 후 추가 기록 없음
  - 웨이브·상점·해금처럼 요약을 다시 쓰는 지점에서 revision이 계속 커지는지
  - 씬 이동·패널 온·오프가 이벤트 이름 그대로 박혀 있는지

## 템플릿에 없는 것

- 게임별 수집기(경제·전투·상점 등)와 이벤트 추가: `GameEvents`에 이벤트를 더하고 `TelemetryRecorder`에서 받아 쓰는 구조를 그대로 쓴다.
- 분석 파일의 서버 전송·수집기, 대시보드, 리플레이 백엔드.
- 맵 배경 캡처는 LogKit 메뉴(`Tools > LogKit > 맵 윗면 캡처`)가 그대로 맞다.
