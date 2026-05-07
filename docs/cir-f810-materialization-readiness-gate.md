# CIR-F8.10: topology readiness summary + first emission gate

CIR-F8.10 adds `MaterializationReadinessAnalyzer` as a dry-run-only aggregation gate over existing F8 evidence layers.

## Purpose

Provide a deterministic readiness report answering whether a CIR subtree has enough dry-run evidence for future BRep emission attempts, while explicitly keeping topology emission unimplemented.

## Evidence readiness vs emission implementation

- `EmissionReadiness` reports **evidence readiness only**.
- `TopologyEmissionImplemented` remains `false` in all outcomes for F8.10.
- No BRep face/loop/edge/coedge/vertex/shell entities are created.

## Readiness values

- `EvidenceReadyForEmission`
- `SpecialCaseReady`
- `Deferred`
- `Unsupported`
- `NotApplicable`

Conservative precedence: `Unsupported > Deferred > SpecialCaseReady > EvidenceReadyForEmission > NotApplicable`.

## Blocking reasons

- `SourceSurfaceExtraction`
- `TrimCapability`
- `RetentionClassification`
- `LoopScaffolding`
- `LoopGrouping`
- `TopologyPlanning`
- `LoopClosure`
- `CoedgePairing`
- `Adjacency`
- `UnsupportedSurfaceFamily`
- `NonSubtractNotApplicable`
- `TopologyEmissionNotImplemented`

## Layer summaries

The report summarizes:

1. source-surface extraction
2. face patch candidate/retention/loop scaffold evidence
3. topology dry-run planning
4. edge/coedge pairing and loop-closure evidence

Each layer carries readiness, blocking reasons, diagnostics, and deterministic counts.

## Example outcomes

- `Subtract(Box, Cylinder)`: typically `EvidenceReadyForEmission` or `SpecialCaseReady`; may still be `Deferred` if pairing/closure evidence is deferred.
- `Subtract(Box, Sphere)`: not generic unsupported; reports planar/spherical circle-exact diagnostics and any true deferred blocker.
- `Subtract(Box, Torus)`: blocked at trim capability with planar/toroidal quartic/algebraic deferred diagnostics.
- non-subtract roots: `NotApplicable` for this subtract-focused gate.

## JudgmentEngine usage decision

`JudgmentEngine` is not used in F8.10. The gate logic is a monotonic conservative reduction over existing layer statuses without competing bounded strategies.

## Deferred in F8.10

- Actual topology emission remains unimplemented.
- Exact parameter solving and full coedge/adjacency closure materialization remain future work.

## Recommended next step

Use this report as the first preflight contract before adding family-specific BRep emission stages, failing early on explicit layer blockers rather than attempting unsafe partial emission.
