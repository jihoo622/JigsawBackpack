# 플레이 로그 이벤트 카탈로그

기록 경로·세션 계약·설정·검증은 `Docs/TELEMETRY.md`를 본다. 이 문서는 **분석 파일에 남기는 이벤트 이름과 필드**의 원본으로, 분석 서버·`TelemetryEvents`/`TelemetryPayload` 코드와 서로 맞아야 한다.

## 이름·필드 규칙
- 이벤트 이름은 `TelemetryEvents`의 상수가 원본이다. 코드에 문자열 리터럴로 퍼뜨리지 않고, 이름을 바꿀 때는 서버와 이 문서를 함께 고친다.
- 발행은 `GameEvents`가 하고, 분석 파일로 쓰는 곳은 `TelemetryRecorder` 한 곳이다. 개별 시스템이 `TelemetryLog`를 직접 호출해 이벤트가 흩어지지 않게 한다.
- payload는 `[Serializable]` 구조체(`TelemetryEvents.cs`)로 두고, 필드는 camelCase. 호환이 깨지니 기존 필드의 이름·뜻을 바꾸지 말고 새 필드를 추가한다.
- `time`은 모든 줄이 같은 기준(`TelemetryLog`의 FixedUpdate 누적 게임 시간)을 쓴다. 개별 시스템이 `Time.time`을 직접 넣지 않는다.
- 좌표는 `TelemetryPayload.Position`으로 1cm 단위에 담아 행 크기를 낮춘다.
- 판 단위 식별자가 필요하면 `sessionId`를 쓰고, 판 안에서 다시 쪼개야 하면 `attemptId`·`stageVisitId` 같은 id를 `GameEvents.NextId()`로 발급해 같은 판의 줄들을 묶는다.
- 요약이 필요한 집계(웨이브·상점·경제)는 `WriteSummary(summaryId, payload)`를 쓴다. 같은 `summaryId`의 가장 큰 revision만 채택된다.

## 템플릿 기본 이벤트
| 이벤트 | 발행 지점(GameEvents) | payload | 필드 |
|---|---|---|---|
| `session_start` | 기동 시 `TelemetryLog` 자동 | `SessionPayload` | `reason`, `appVersion`, `unityVersion`, `platform`, `isEditor`, `scene` |
| `run_start` | `GameEvents.RaiseRunStarted` | `RunPayload` | `scene`, `time`, `reason`(빈 값) |
| `run_end` | `GameEvents.RaiseRunEnded` | `RunPayload` | `scene`, `time`, `reason` |
| `scene_change` | `GameEvents` 씬 훅(자동) | `ScenePayload` | `scene`, `loaded` |
| `upgrade_granted` | `UpgradeManager.Grant` → `GameEvents.RaiseUpgradeGranted` | `UpgradePayload` | `id`, `displayName` |
| `panel_visibility` | `PanelBase.SetOpen` → `GameEvents.RaisePanelVisibility` | `PanelPayload` | `panel`, `open` |
| `trade_completed` | `PuzzleInventoryService.Drop` → `GameEvents.RaiseTradeCompleted` | `TradePayload` | `cityId`, `itemId`, `instanceId`, `direction`(`buy` / `sell`), `price`, `fundsAfter`, `x`, `y`, `clockwiseTurns` |
| `summary` | 게임 쪽 `TelemetryLog.WriteSummary` | (집계 자료형) | `summaryId`, `revision` |
| `session_end` | 종료 훅(`TelemetryLog.Close`) | `SessionPayload` | `reason`(`shutdown` / `application_quit`) |

`trade_completed`는 보드 간 구매·판매가 확정될 때 기록한다. `x`·`y`는 대상 보드에서 회전한 화물의 왼쪽 위 기준점이다.

## 이벤트 추가할 때
1. `GameEvents`에 `struct` 페이로드와 `Raise*`를 추가한다.
2. `TelemetryEvents`에 이벤트 이름 상수, `TelemetryEvents.cs`에 `[Serializable]` payload 자료형을 추가한다.
3. `TelemetryRecorder`에서 구독해 `TelemetryLog.Write`로 넘긴다. 생산자 코드에는 `TelemetryLog` 호출을 두지 않는다.
4. 이 문서의 표에 행을 추가하고, 분석 서버 쪽 이름과 맞는지 확인한다.

## LogKit 쪽(콘솔·오류) 사용법
`Debug.Log`·`Debug.LogError`는 LogKit이 자동 수집한다. 개별 호출을 늘리지 말고, 집계 지표가 필요할 때만 LogKit을 직접 쓴다.

```csharp
LogKit.GameLogger.Info("System", "씬 진입: SampleScene");   // 콘솔성 정보
LogKit.GameLogger.Count("UpgradeGranted");                  // 발생 횟수 집계
LogKit.GameLogger.Measure("RunDuration", 124.5);            // 평균 집계
LogKit.GameLogger.FlushNow();                               // 씬 전환처럼 지금 보낼 때
```

게임 사건의 상세는 분석 파일에 남기고, LogKit에는 콘솔·오류·지표만 남긴다. 같은 내용을 두 경로에 복붙하지 않는다.

## 맵 배경 캡처(히트맵용)
`Tools > LogKit > 맵 윗면 캡처`로 씬 PNG와 범위 JSON(`<씬>.json`: minX·maxX·minZ·maxZ)을 만들어 분석 서버의 리플레이·히트맵 배경에 등록한다. 실내 맵은 바닥 + 2~3m 절단 높이를 권한다.
