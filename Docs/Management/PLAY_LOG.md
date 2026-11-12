# 외부 행동 복원 계약

이 문서와 schemaVersion 3 구조화 로그, 해당 Rules 테이블만으로 외부 도구가 행동과 관측 상태를 복원한다. Unity 실행·게임 소스·콘솔 문장 해석을 요구하지 않는다. 복원 규칙과 게임의 기대 규칙 검증을 구분한다. 실제 변화는 로그에서 적용하고, 기대 가격·시너지·조건은 테이블과 비교한다.

## LogKit 서버에서 원본 추출

콘솔과 분석·테이블 모두 LogKit으로 전송한다. 콘솔 메시지를 역해석할 필요 없이 다음 구조화 원본을 추출한다. 로컬 JSONL/Rules를 직접 사용하는 경우에도 이후 복원 계약은 같다.

1. 업로드 배치 entries 중 `category="Telemetry"`를 고른다. 각 entry.dataJson을 JSON으로 파싱하면 TelemetryLine이다. entry.message는 eventName이며 데이터의 원본은 내부 TelemetryLine이다.
2. 내부 TelemetryLine의 sessionId/sequence로 중복을 제거하고 순서를 정한다. 업로드 배치의 sessionId/partIndex는 LogKit 전송용이므로 판 키로 사용하지 않는다. 내부 dataJson을 한 번 더 파싱하면 아래 행동 payload다.
3. `category="TelemetryRules"`, `message="rules_chunk"`인 entry.dataJson은 sessionId/rulesHash/chunkIndex/chunkCount/contentBase64다. `(sessionId,rulesHash)`별로 묶어 chunkIndex 0부터 chunkCount-1까지 모두 모은다. 같은 인덱스 재수신은 중복 제거하되 내용이 다르면 손상으로 표시한다.
4. **각 조각을 따로 Base64 디코딩한 바이트를** chunkIndex 순서로 이어 붙인다. Base64 문자열 자체를 이어 붙이지 않는다. 합친 바이트의 SHA-256이 rulesHash와 일치해야 한다. UTF-8로 읽은 JSON이 Rules 원본이며 tables[].json은 다시 파싱한다.
5. 테이블 조각은 게임 sequence에 포함하지 않는다. 테이블 조각의 도착이 run_start보다 늦으면 참조 원본이 완성될 때까지 해당 판의 테이블 결합을 기다린다. run_start.rulesHash로 결합한다.

게임 분석 원본은 로컬과 서버 양쪽에서 수집해도 같은 `(sessionId,sequence)`면 한 건이다. 테이블은 앱 세션마다 해시별 한 번 보내고, 같은 앱에서 재시작할 때는 재전송하지 않는다. 통신 재시도로 조각/배치가 재수신될 수 있으므로 서버 도착 횟수를 행동 횟수로 계산하지 않는다.

## 입력과 완전성

- 입력: 세션 JSONL과 각 run_start.rulesHash에 해당하는 `Rules/<hash>.json`. 파일 바이트의 SHA-256이 hash와 같아야 한다. 파일을 재직렬화한 결과가 아니라 전달된 UTF-8 원본 바이트를 해시한다.
- Rules의 `tables[{name,json}]`에서 name은 확장자 없는 테이블 이름, json은 해당 원본 JSON 문자열이다. 판마다 참조가 고정되며 최신 테이블로 대체하지 않는다.
- JSONL의 dataJson은 JSON 문자열이므로 한 번 더 파싱한다. sessionId별 sequence 순으로 읽는다. timestampUtc나 디렉터리 나열 순서로 정렬하지 않는다.
- session_start.schemaVersion은 **3**이다. 없는 버전은 1, 이전 변화분 계약은 2다. 서로 같은 파서로 해석하지 않는다.
- run_start에서 새 판을 만들고 run_end에서 닫는다. 판 키는 `(sessionId, run_start.sequence)`다. instanceId는 판 안에서만 유일하다. 재시작 후 같은 번호를 이전 판 화물과 연결하지 않는다.
- 시퀀스 누락·중복·파싱 실패·run_start 부재·Rules 누락은 불완전 입력이다. 누락 부분을 가격·이동 성공 여부 등으로 추측해서 채우지 않는다. 마지막 연속된 원본까지의 복원만 확정한다.
- run_end/session_end가 없으면 완결된 승패가 아닌 중단된 접두 기록이다. 완전한 마지막 행동까지 복원할 수 있지만 이후 행동·종료 사유는 알 수 없다.

## 복원하는 상태

판 상태 S는 `cityId`, `funds`, `wagonCount`, `elapsedMinutes`, `items[instanceId]`, 소문 목록, 종료 사유다. items는 **모든 기차 화물 + 현재 도시의 시장 화물**만 포함한다. 미방문/현재 보이지 않는 시장의 무작위 내용은 복원 범위에 포함하지 않는다. UI 픽셀·마우스 궤적·취소된 중간 조작도 재생 대상이 아니다.

run_start의 cityId/funds/wagonCount/items로 S를 초기화하고 elapsedMinutes는 0으로 둔다. items는 보드별 일부 표본이 아닌 관측 범위 전체다. train:0부터 wagonCount-1까지 빈 화물칸도 존재한다. market:<cityId>는 현재 시장이다.

items/introduced 원소는 `instanceId`, `itemId`, `board`, `x`, `y`, `clockwiseTurns`, `actualValue`, `durability`다. actualValue는 invariant decimal 문자열이므로 이진 float로 바꾸지 않는다. 표시 이름·태그·모양·보드 치수는 테이블에서 결합한다.

| 조회 대상 | 테이블 키 |
|---|---|
| 화물 정의 | PuzzleTableData.pieces[].id = itemId |
| 기차 치수 | PuzzleTableData.train |
| 도시/시장 치수·거래 조건 | PuzzleTableData.cities[].id = cityId |
| 증차·상환·기한 | PuzzleTradeTableData |
| 노선 | PuzzleRouteTableData.routes의 fromCityId/toCityId |
| 게임 시각·휴식 | PuzzleTimeTableData |
| 시너지 | PuzzleSynergyTableData |

## 행동과 조작 방식

puzzle_action은 모델에 전달된 명시적 조작 한 건의 결과다. 의미인 action과 입력 방식인 method를 분리한다. 상태 변화나 돈의 부호로 행동 종류를 다시 추측하지 않는다.

| action | 의미 | method |
|---|---|---|
| buy | 시장에서 기차로 구매 시도 | drag / quick_transfer |
| sell | 기차에서 시장으로 판매 시도 | drag / quick_transfer |
| reposition | 동일 보드 또는 화물칸 사이 재배치 시도 | drag / quick_transfer |
| placement | 원본 ID나 대상 보드가 유효하지 않아 buy/sell/reposition으로 분류할 수 없는 배치 시도 | drag / quick_transfer |
| travel | 목적지 이동 확정 시도 | 비어 있음 |
| rest | 휴식 시도 | 비어 있음 |
| wait_for_daylight | 아침 대기 시도 | 비어 있음 |
| advance_time | 그 외 명시적 시간 진행 시도 | 비어 있음 |
| add_wagon | 증차 시도 | 비어 있음 |
| repay | 채무 상환 시도 | 비어 있음 |

failure가 null/빈 문자열이면 성공이고 그 외는 거부/오류다. buy/sell은 성공·실패 모두 같은 action을 사용한다. 무료 구매, 음수 판매가에 따른 처분 비용도 action으로 구분한다. 돈이 줄었다는 이유로 sell을 buy로 해석하지 않는다. placement는 정상 성공의 분류가 아니며 손상된 입력에 행동 의미를 지어내지 않기 위한 값이다.

## 행동 필드

| 필드 | 의미와 적용 |
|---|---|
| action, method, failure | 요청 의미·입력 방식·결과. 상태 변화가 아니라 행동 분류에 사용 |
| instanceId | buy/sell/reposition/placement의 대상 화물. 그 외 0 |
| elapsedMinutes | 행동 직후 실제 게임 누적 분. 항상 필요. 이전 값과 차이가 실제 소비 시간 |
| moneyDelta | 관측한 자금 증감. S.funds에 더함. 실패라도 실제 변화가 있으면 적용 |
| wagonDelta | 관측한 화물칸 수 증감. S.wagonCount에 더함. add_wagon 성공만 보고 +1을 추가하지 않음 |
| cityId | 실제 도시가 바뀐 경우의 새 도시. null/빈 값이면 도시 유지 |
| targetCityId | travel 요청 목적지. 성공하며 cityId와 같으면 중복 생략하며 요청 목적지는 cityId로 읽음. 실패 또는 실제 도착지와 다르면 유지 |
| attempt | 실패 요청의 추가 정보. 아래 분기 외에는 사용하지 않음 |
| introduced | 이번에 처음 관측한 화물의 완전 상태 |
| removed | 관측 범위 안에서 사라진 기존 화물 ID |
| moved | 위치가 바뀐 기존 화물의 instanceId/board/x/y/clockwiseTurns |
| values | 실제 가치가 바뀐 기존 화물의 instanceId/actualValue |
| durability | 내구도가 바뀐 기존 화물의 instanceId/durability |

introduced는 생성 이벤트가 아니다. market 보드의 introduced는 처음 도착해 본 실제 재고이며 생성 시각은 알 수 없다. train 보드의 introduced는 이번 행동 끝에 새로 생긴 화물이다. 구매는 이미 현재 시장에서 알고 있던 ID를 moved로 옮기므로 introduced로 중복되지 않는다.

성공 배치의 실제 대상 보드/위치/회전은 moved 중 instanceId가 같은 원소에서 읽는다. 원래 보드/위치/회전은 적용 전 S에서 읽는다. 정상 실패는 이동 결과가 없으므로 attempt에 요청 정보를 남긴다.

- 실패 배치: attempt.destination은 대상 보드, x/y는 놓으려 한 보드 셀, grabbedX/grabbedY는 잡은 원본 화물 셀, clockwiseTurns는 요청 회전이다.
- 실패 quick_transfer: 고정 목표 셀이 없으므로 destination과 clockwiseTurns(선호 회전)만 해석한다. x/y/grabbedX/grabbedY는 무시한다. 후보 탐색은 여러 행동이 아니다.
- 실패 rest/wait_for_daylight/advance_time: attempt.requestedMinutes는 요청한 소비 분이다. 실제 소비는 elapsedMinutes 차이로 얻는다. unavailable인 아침 대기는 유효한 대기 시간이 없어 0이다.
- 무관한 문자열은 null 또는 빈 값, 숫자는 0, 변화 배열은 빈 배열이다. 직렬화기가 비어 있는 attempt 객체를 출력해도 action/failure 분기에 맞는 필드만 읽는다. 누락된 필수 원본 필드를 기본값으로 보충하지 않는다.

## 상태 적용 순서

각 puzzle_action마다 다음 순서를 한 번 적용한다. failure가 있다는 이유로 변화분을 버리지 않는다.

1. 적용 전 S를 보존한다. action/method/failure, 대상 instanceId와 원래 위치를 여기서 읽어 행동 레코드를 만든다. 요청 목적지는 targetCityId가 있으면 그것, 성공 travel에서 생략됐으면 cityId다.
2. cityId가 비어 있지 않으면 이전 `market:<S.cityId>`에 있는 항목을 관측 상태에서 모두 빼고 S.cityId를 갱신한다. 이는 소멸이 아닌 관측 종료다. removed에 같은 시장 ID들을 다시 열거하지 않는다.
3. removed의 항목을 지운다. introduced를 새 ID로 삽입한다. 이어서 moved, values, durability로 해당 필드만 덮어쓴다. 배열에 없는 필드는 유지한다.
4. funds += moneyDelta, wagonCount += wagonDelta, elapsedMinutes = 레코드의 elapsedMinutes를 적용한다. action의 기대 효과를 추가로 실행하지 않는다.
5. 성공 여부와 무관하게 적용 후 상태를 관측 결과로 보관한다. 정상 실패라면 moneyDelta/wagonDelta/도시/시간/화물 변화가 모두 없어야 한다. 변화가 있으면 실패 시 원상 보존 위반으로 표시한다.

introduced의 ID는 적용 중인 S에 없어야 한다. moved/values/durability의 ID는 기존 항목이어야 한다. introduced와 removed/변경 배열은 같은 ID를 중복 사용하지 않는다. moved와 values처럼 다른 필드 변경끼리는 같은 ID가 나올 수 있다. 이러한 참조 규칙이 깨지면 임의의 새 화물을 만들지 말고 불완전/계약 위반으로 표시한다.

이동 성공 뒤 introduced에는 도착 시장의 모든 실제 재고가 들어간다. 출발 시 숨은 시장 재입고 내용은 복사하지 않으며 다시 방문할 때 관측한다. 이전 시장을 외부 캐시에 보관하더라도 그 도시를 떠난 순간부터 최신 상태로 간주하지 않는다.

시간 행동의 values는 행동 끝에 관측한 실제 가치다. 행동 도중 생성되거나 변환된 화물은 이후 정산에 참여할 수 있다. 외부 복원은 기록된 values를 적용한다. 동일 ID로 품목이 바뀌는 성장·식품 만료와 작업대 내부 상태는 현재 변화분 계약에서 복원되지 않는다. 작업 비용은 다음 `puzzle_action`에서 관측되는 잔액 차이에 포함될 수 있다.

도축의 `butchery_committed`와 `butchery_ready`는 보조 기록이다. 전자는 `actionId`, `cityId`, `inputCount`, `readyAtMinutes`, `time`을, 후자는 `actionId`, `cityId`, `outputCount`, `time`을 담는다.

## 격자·회전

- 보드 셀은 x 오른쪽, y 위쪽이다. 범위는 0 <= x < width, 0 <= y < height다. 로그 x/y는 회전 화물 외접 사각형의 왼쪽 위 기준 셀이며 픽셀이 아니다.
- 테이블 cells의 로컬 좌표는 x 오른쪽, y 아래쪽이다. clockwiseTurns는 0/1/2/3이며 각각 (x,y), (-y,x), (-x,-y), (y,-x)로 회전한다.
- 회전한 전체 셀의 최소 좌표를 빼 정규화한 좌표를 q라 하면 보드 점유 셀은 `(origin.x + q.x, origin.y - q.y)`다. 이 계산으로 적재율·인접·겹침을 외부에서 얻는다.
- 실패 drag의 x/y는 origin이 아니라 잡은 셀의 목표다. 정규화한 잡은 셀을 g라 할 때 요청 origin은 `(attempt.x - g.x, attempt.y + g.y)`다.

## 다른 이벤트와 시간

- rumor_learned는 targetCityId/tag/elapsedMinutes를 기록한다. 출처는 그 시점 S.cityId이며 의미상 소문 한 건을 목록에 추가한다. UI 문장·서식 전체 재생은 범위 밖이다.
- run_end.reason은 won/deadline/restart/scene_unloaded/shutdown/recorder_disabled/replaced다. 승패와 중단을 구분하고 S를 닫는다. 결과 패널/씬 이벤트가 뒤에 있어도 새 run_start 전까지 이전 판 행동으로 추가하지 않는다.
- session_start는 schemaVersion/reason/appVersion/unityVersion/platform/isEditor/scene, session_end는 reason이다. scene_change는 scene/loaded, panel_visibility는 panel/open, upgrade_granted는 id다. 원본 시각은 공통 봉투에서 읽는다.
- 공통 time은 일시정지를 반영하는 실제 플레이 누적 초, timestampUtc는 UTC, elapsedMinutes는 게임 달력이다. 실제 체류 시간과 게임 소비 시간을 섞지 않는다.
- 기본 기차 화물의 보유 시간은 0에서 시작하고 시장 화물은 미소유다. 성공 buy에서 0, sell에서 미소유로 바뀐다. 새 기차 화물은 해당 행동 끝에 보유를 시작한다. 이후 게임 소비 분을 합해 보유 기간을 계산한다.

## 외부 해석 예

아래 숫자는 형식 설명용이다. dataJson을 파싱한 후이며 무관한 빈 필드는 생략했다. 적용 전 화물 17이 현재 시장에 있고 자금은 200이라고 가정한다.

```json
{"action":"buy","method":"quick_transfer","instanceId":17,"failure":"","elapsedMinutes":0,"moneyDelta":-30,"wagonDelta":0,"moved":[{"instanceId":17,"board":"train:0","x":1,"y":3,"clockwiseTurns":1}]}
```
즉시 구매 성공이다. 기존 시장 화물 17을 지정 위치로 옮기고 자금은 170이다. itemId/태그/기본 가치는 기존 17의 상태와 Rules에서 얻는다. 별도 거래 로그나 fundsAfter가 필요하지 않다.

```json
{"action":"sell","method":"drag","instanceId":17,"failure":"trade.InsufficientFunds","elapsedMinutes":0,"moneyDelta":0,"wagonDelta":0,"attempt":{"destination":"market:city.example","x":2,"y":4,"grabbedX":0,"grabbedY":0,"clockwiseTurns":1}}
```
판매 시도 거부다. 변화 배열이 모두 비었으면 기차·자금은 유지된다. 거부가 구매였는지 판매였는지 돈의 부호로 추측하지 않는다. 요청 위치는 attempt로 재구성한다. 예제는 빈 배열 필드를 생략했지만 실제 로그는 빈 배열로 출력한다.

## 실패 값 사전

외부에서 C# enum을 찾지 않도록 현재 출력 가능한 값을 고정한다.

- travel: Blocked, NotAvailable, SameCity, InvalidCity, MissingRoute, InsufficientFunds, CostOverflow, TimeOverflow.
- placement 접두사: StaleSource, InvalidDestination, InvalidGrabbedCell, InvalidRotation, CellOutsideBoard, CellOccupied, NoSpace, MarketClosed.
- trade 접두사: Disabled, InsufficientFunds, PriceOverflow, BalanceOverflow, MarketClosed. 예: trade.MarketClosed.
- 증차/상환: run_finished, insufficient_funds, max_wagons, wrong_city, overdue. 시간: time_blocked, invalid_duration, time_overflow, unavailable.
- unknown 값/버전은 성공으로 치환하지 않는다. 원문을 보존하고 해석 미지원으로 표시한다.

## 수집 경계와 버전 변경

매 프레임 호버·견적·고스트·후보 탐색·선택 화물칸 변경, 드래그 중간 회전/취소, 이동 확인창 취소, 같은 위치·회전의 무변화 성공, 0분 진행은 수집하지 않는다. 로그에 없다고 해당 마우스 조작이 없었다고 판단할 수 없다. 미구현 사건과 숨은 시장 RNG 전체도 복원하지 않는다. 현재 summary 집계는 발행하지 않는다.

버전 3은 버전 2의 place/quick_transfer를 의미 action과 method로 나누고, created를 introduced로 명확히 했다. 도시·화물칸 수는 성공 여부로 가정하지 않고 관측 변화로 적용한다. 실패 시간 요청값도 보존한다. 가격·잔액·통계·전후 전체 상태를 별도 이벤트로 중복 출력하지 않는다. 운영/보관은 [TELEMETRY](TELEMETRY.md)를 따른다.
