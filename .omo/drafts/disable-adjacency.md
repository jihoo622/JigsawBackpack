---
slug: disable-adjacency
status: awaiting-approval
intent: clear
review_required: false
pending-action: write .omo/plans/disable-adjacency.md
approach: Disable adjacent valuation collection and drag preview; preserve unrelated systems and table content.
---

# Draft: disable-adjacency

## Affected user and ideal state
- IS-1: 플레이어의 인접 배치로 가치가 증가하거나 감소하지 않는다. 기존 Board 규칙과 다른 게임 효과는 유지한다.
- IS-2: 인접 가치 효과의 화살표와 드래그 미리보기가 표시되지 않는다. 냉장 화살표는 유지한다.
- GAP-1: PuzzleInventoryValuation.Recalculate가 Adjacent 효과를 수집하고 정산에 반영한다.
- GAP-2: 드래그 미리보기는 규칙을 직접 조회하며 별도로 표시한다.

## Components (topology ledger)
- 가치 계산 | 인접 효과 수집 중단 | active | Assets/Scripts/Runtime/Inventory/PuzzleInventoryValuation.cs:102
- 인접 가치 표시 | 가치 화살표 및 미리보기 중단 | active | Assets/Scripts/Runtime/UI/PuzzleInventoryPresenter.cs:899; Assets/Scripts/Runtime/UI/PuzzleInventoryView.cs:234

## Open assumptions (announced defaults)
- 인접 가치 증가와 감소 양쪽을 중단한다. 냉장·번식·배설과 기존 누적 가치는 유지한다.
- 툴팁·상성표·테이블은 수정하지 않는다. 향후 복구를 위해 규칙을 남긴다.
- 사용자 지침에 따라 테스트·플레이·실험은 수행하지 않는다.

## Findings (cited - path:lines)
- PuzzleInventoryValuation.cs:102-136에서 인접과 Board를 별도로 수집한다. 결과는 PuzzleInventory.cs:133에서 가치에 누적된다.
- PuzzleInventoryView.cs:234-260의 가치 화살표는 계산 결과의 접촉 정보를 사용하며 냉장 화살표는 별도 경로다.
- PuzzleInventoryPresenter.cs:899-901의 드래그 미리보기는 별도로 규칙을 계산한다.

## Decisions (with rationale)
- 공통 테이블 필터는 상성표 및 툴팁에도 영향을 주므로 사용하지 않는다.
- 인접 수집 호출을 중단하고 드래그 미리보기 호출을 중단한다. Board와 냉장은 유지한다.

## Scope IN
- 인접 가치 증감, 관련 가치 화살표, 드래그 시 인접 가치 미리보기.

## Scope OUT (Must NOT have)
- 설명, 상성표, 테이블 변경; 냉장, 동물 효과, 기존 누적 가치 변경; 테스트 및 플레이 실행.

## Open questions
- 없음.

## Approval gate
status: awaiting-approval
- 승인 시 계획 문서만 작성하고 구현은 별도 요청을 기다린다.
