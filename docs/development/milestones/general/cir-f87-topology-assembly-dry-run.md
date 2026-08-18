# CIR-F8.7: topology assembly dry-run contracts for surface-family materialization

CIR-F8.7 introduces a topology **planning** dry-run layer that consumes F8.3/F8.4/F8.5/F8.6 candidate and loop-group contracts and reports whether future BRep topology assembly appears exact-ready, special-case-ready, deferred, unsupported, or not applicable.

## Scope

Added dry-run contracts:

- `TopologyAssemblyDryRunResult`
- `PlannedFacePatch`
- `PlannedLoop`
- `PlannedAdjacency`
- `TopologyAssemblyReadiness`

Still explicitly deferred:

- no `BrepFace`/`BrepLoop`/`BrepEdge`/`BrepCoedge`/`BrepVertex`/`BrepShell` emission,
- no trim parameter solving,
- no loop closure proof,
- no STEP export behavior change,
- no boolean behavior expansion.

## Readiness semantics and escalation

`TopologyAssemblyReadiness` values:

- `ExactPlanReady`
- `SpecialCasePlanReady`
- `Deferred`
- `Unsupported`
- `NotApplicable`

Deterministic conservative reduction:

1. any `Unsupported` => `Unsupported`
2. else any `Deferred` => `Deferred`
3. else any `SpecialCasePlanReady` => `SpecialCasePlanReady`
4. else all `ExactPlanReady` => `ExactPlanReady`
5. else `NotApplicable`

## Deterministic ordering

Planned faces are ordered by:

`{surfaceFamily}|{retentionRole}|{readiness}|{orientationPolicy}|{loopGroupCount}`

Planned loops preserve F8.6 group ordering keys.

## Adjacency hints

Adjacency hints are emitted only when two planned faces share at least one trim curve family from loop descriptors.
If no shared trim-family hint exists, adjacency is marked deferred with explicit diagnostics.

## JudgmentEngine usage decision

`JudgmentEngine` was not used in F8.7 because readiness and ordering are pure monotonic reductions over existing dry-run descriptors, and adjacency hints use deterministic shared-family intersection without competing scored alternatives.

## Expected outcomes

- `Subtract(Box, Cylinder)`: planned base/tool faces and planned loops are emitted; readiness typically exact or special-case depending on loop-group statuses.
- `Subtract(Box, Sphere)`: spherical planned face is emitted; circular loop groups represented without generic unsupported claims.
- `Subtract(Box, Torus)`: toroidal planned face exists; readiness remains deferred and diagnostics retain trim-matrix rationale (including quartic/algebraic deferral reason).
- non-subtract roots: `NotApplicable` with explicit topology-plan-not-applicable diagnostic.

## SEM-A0 status

SEM-A0 guardrails preserved: no generated topology naming was introduced.

## Recommended next step

CIR-F8.8 should formalize dry-run edge-use/coedge-use pairing and minimal loop-closure evidence contracts so adjacency hints can evolve from shared trim-family signals to deterministic edge pairing plans.
