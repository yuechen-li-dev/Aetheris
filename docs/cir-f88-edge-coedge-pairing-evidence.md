# CIR-F8.8: edge/coedge pairing + loop-closure evidence contracts

CIR-F8.8 layers descriptor-only topology evidence on top of CIR-F8.7 topology dry-run planning.

## Purpose

Provide deterministic, diagnostic-rich evidence for future BRep topology emission without creating any BRep entities.

Added dry-run contracts:

- `PlannedEdgeUse`
- `PlannedCoedgePairing`
- `LoopClosureEvidence`
- `TopologyPairingEvidenceResult`
- `TopologyPairingReadiness`

Still deferred:

- no `BrepEdge`/`BrepCoedge`/`BrepLoop`/`BrepFace`/`BrepVertex`/`BrepShell` emission,
- no trim parameter solving,
- no loop-parameter closure solving,
- no STEP export behavior change,
- no boolean behavior expansion.

## Readiness semantics

`TopologyPairingReadiness` values:

- `ExactReady`
- `SpecialCaseReady`
- `Deferred`
- `Unsupported`
- `NotApplicable`

Escalation is conservative and deterministic:

1. any `Unsupported` => `Unsupported`
2. else any `Deferred` => `Deferred`
3. else any `SpecialCaseReady` => `SpecialCaseReady`
4. else all `ExactReady` => `ExactReady`
5. else `NotApplicable`

## Deterministic ordering

- Edge uses ordered by:
  `{faceKey}|{loopKey}|{trimFamily}|{sourceSurface}|{oppositeSurface}|{loopKind}`
- Pairings ordered by:
  `{edgeUseA}<->{edgeUseB|deferred}`
- Closure evidence ordered by:
  `{faceKey}|{loopKey}|closure`

## Closure semantics

- `ClosedByDescriptor` is emitted only for exact circular loop descriptors.
- Other families/statuses remain `ClosureDeferred` until explicit curve parameter/order solving exists.
- Unsupported loop descriptor families/statuses report `Unsupported`.

## Pairing semantics

`SharedTrimCurve` pairing requires deterministic evidence:

- same trim curve family,
- complementary source/opposite surface families,
- matching planned adjacency hint between source faces.

If one-to-one identity is ambiguous or missing, pairing is deferred with explicit diagnostics describing missing evidence.

## JudgmentEngine usage decision

`JudgmentEngine` is not used in F8.8. Current pairing and closure decisions are deterministic reductions/filtering with at most one admissible candidate after required evidence keys; there are no bounded competing scored strategies yet.

## Example outcomes

- `Subtract(Box, Cylinder)`:
  edge uses exist for base/tool retained loop descriptors, closure may defer for non-circular families, and pairings are either exact (where uniquely matchable) or deferred with identity diagnostics.
- `Subtract(Box, Sphere)`:
  circular edge uses exist and circular loops can be `ClosedByDescriptor`; coedge pairing may still defer when one-to-one identity is not provable.
- `Subtract(Box, Torus)`:
  evidence remains deferred due to trim-matrix deferral (`quartic/algebraic` rationale carried from prior milestones), with no exact closure claim.
- non-subtract trees:
  `NotApplicable`; no invented subtract pairings.

## SEM-A0 guardrails status

SEM-A0 preserved: no generated topology naming and no user-facing topology identity expansion were introduced.

## Recommended next step

CIR-F8.9 should add bounded provenance identity tokens to loop descriptors (or adjacency edges) so deferred pairing cases can be promoted from ambiguous to exact-ready without introducing generated topology naming.
