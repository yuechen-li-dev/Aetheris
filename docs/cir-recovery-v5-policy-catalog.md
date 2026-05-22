# CIR-RECOVERY-V5: expandable semantic recovery policy catalog scaffold

## Purpose
CIR-RECOVERY-V5 introduces a production policy catalog layer for semantic FRep→BRep recovery planning so new bounded recovery policies can be added with local changes.

## Catalog structure
`FrepMaterializerPolicyCatalog` is the single default registration location in `Aetheris.Kernel.Firmament.Materializer`.

It exposes:
- `DefaultRegistrations()` for ordered policy registrations.
- `Default()` for planner-consumable `IFrepMaterializerPolicy` list.
- `SnapshotDefault()` for deterministic policy-order diagnostics.

## Policy categories
Catalog-side metadata uses `FrepMaterializerPolicyCategory`:
- `SemanticExact`
- `CirOnlyFallback`

## Default policy list (deterministic order)
1. `HoleRecoveryPolicy` (`SemanticExact`)
2. `CirOnlyFallbackPolicy` (`CirOnlyFallback`)

## Fallback behavior
`CirOnlyFallbackPolicy` is always admissible with low score and `FrepMaterializerCapability.CirOnly`.
It preserves intent/diagnostics and intentionally emits no executable BRep plan.

## Planner behavior
`FrepMaterializerPlanner` remains the existing generic JudgmentEngine selector.
No policy-specific dispatch logic was added to planner internals.

## Rematerializer wiring
`FrepSemanticRecoveryRematerializer` now consumes `FrepMaterializerPolicyCatalog.Default()` instead of manually constructing a single policy list.
If fallback is selected, rematerializer records non-executable diagnostics and does not report BRep recovery success.

## Locality-of-change rule
Adding a new semantic recovery capability should require only:
1. Implementing a new `IFrepMaterializerPolicy`.
2. Registering it in `FrepMaterializerPolicyCatalog` in the intended order/category.
3. Adding focused tests for recognition, admissibility, and rematerializer behavior.

No `FrepMaterializerPlanner` logic edits are required.

## Non-goals in V5
- New exact semantic recovery implementations beyond through-hole.
- STEP export behavior changes.
- Generic boolean dispatcher.
- Public CLI/API expansion.
- Generated topology naming.

## Recommended next milestone
Add next semantic exact family policies (for example counterbore/countersink) as bounded `IFrepMaterializerPolicy` implementations and register them above fallback with explicit diagnostics and evidence contracts.
