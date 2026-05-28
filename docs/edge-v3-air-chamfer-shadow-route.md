# EDGE-V3 — Non-authoritative AirChamfer shadow route

## Purpose and scope
EDGE-V3 introduces `AirChamferShadowRoute`, an internal/test-only shadow seam that runs the controlled AirChamfer candidate beside legacy chamfer authority for the exact bounded convex planar single-edge case. The route is diagnostic only: it can produce and inspect an AirChamfer candidate report, but it cannot replace or mutate the production `BrepBoundedChamfer` result.

The milestone answers whether AirChamfer can run as a deterministic shadow candidate behind a controlled seam while production output remains unchanged.

## References
- V2 architecture doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- Legacy topology / parallel lane doctrine: `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`
- AirEdgeSweep audit: `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- EDGE-X3 JudgmentEngine-backed convex planar AirChamfer policy lab
- EDGE-X4 convex planar AirChamfer topology-plan lab
- EDGE-X5 convex planar AirChamfer geometry-artifact lab
- EDGE-X6 convex planar AirChamfer closed-witness lab
- EDGE-V1 internal production-adjacent convex planar prototype
- EDGE-X7 controlled real-body integration probe
- EDGE-X8 controlled local topology graft lab
- EDGE-V2 `AirChamferRealBodyPrototype`
- EDGE-X9 `AirChamferFeatureRecognitionParityLab`

## Component and internal API shape
Component: `AirChamferShadowRoute` in the FrictionLab CIRLab namespace.

Request shape (`AirChamferShadowRouteRequest`):
- controlled source body,
- explicit target edge endpoints,
- explicit adjacent face normals,
- positive finite chamfer distance,
- face-family marker,
- edge-chain / corner-chain / legacy-dependency guards,
- deterministic convexity expectation,
- orthogonal/non-orthogonal marker,
- reference envelope,
- optional STEP smoke flag.

Result shape (`AirChamferShadowReport`):
- `LegacyAuthoritative` is always `true`,
- `ProductionOutputChanged` is always `false`,
- `ShadowCandidateProduced`,
- `ShadowCandidateStatus`,
- `AirChamferDecision`,
- `TopologySummary`,
- `StepSmokeSummary`,
- `FeatureRecognitionSummary`,
- `FirstDivergence`,
- finite deterministic `Recommendation`,
- optional `ShadowCandidateBody`,
- deterministic diagnostics.

The result model keeps the candidate body under `ShadowCandidateBody` and separately marks legacy authority and unchanged production output, making accidental authoritative treatment explicit and testable.

## Supported controlled scope
Accepted shadow candidates are limited to:
- controlled/simple body,
- one explicitly selected convex planar edge,
- two explicitly resolved adjacent planar faces,
- positive finite chamfer distance,
- deterministic convex classification,
- no edge chain,
- no corner chain,
- no legacy-dependent topology flag,
- no unsupported face family.

A safe non-orthogonal controlled planar face pair is also covered because EDGE-V2 and EDGE-X9 support it cleanly.

## Pipeline reuse
`AirChamferShadowRoute` invokes `AirChamferRealBodyPrototype` directly, preserving the EDGE-V2 controlled body/topology/STEP pipeline. It then reuses EDGE-X9 recognition parity logic through `AirChamferFeatureRecognitionParityLab.EvaluateCandidateEvidence(...)` so recognition-summary, first-divergence, and readiness semantics are not duplicated.

The AirChamfer path continues to rely on the EDGE-X3 JudgmentEngine-backed policy chain, and the shadow report emits `edge-v3-judgment-engine-used` after prototype invocation.

## Diagnostics contract
EDGE-V3 emits deterministic diagnostics including:
- `edge-v3-shadow-route-started`
- `edge-v3-shadow-route-internal-only`
- `edge-v3-legacy-authority-preserved`
- `edge-v3-production-output-unchanged`
- `edge-v3-air-chamfer-real-body-prototype-invoked`
- `edge-v3-judgment-engine-used`
- `edge-v3-shadow-candidate-produced`
- `edge-v3-shadow-candidate-step-smoke-succeeded`
- `edge-v3-shadow-feature-recognition-captured`
- `edge-v3-shadow-feature-recognition-parity-succeeded`
- `edge-v3-shadow-feature-recognition-parity-mismatch:<payload>`
- `edge-v3-shadow-route-rejected:<reason>`
- `edge-v3-shadow-route-deferred:<reason>`
- `edge-v3-no-production-route-replacement`
- `edge-v3-no-3d-boolean-used`

## Recommendations
Finite recommendations are constrained to:
- `air-chamfer-shadow-ready-for-controlled-opt-in-route`
- `air-chamfer-shadow-needs-recognition-hardening`
- `air-chamfer-shadow-needs-body-prototype-hardening`
- `air-chamfer-shadow-rejected-invalid`
- `air-chamfer-shadow-deferred-unsupported`
- `air-chamfer-shadow-keep-legacy-authority`

## Accepted, deferred, and rejected cases
Accepted:
- canonical controlled convex planar single-edge,
- safe non-orthogonal controlled convex planar single-edge.

Rejected/deferred with no shadow candidate body:
- invalid distance,
- invalid target edge,
- missing adjacent face,
- non-planar adjacent marker,
- edge chain,
- corner chain,
- legacy-dependent triangle/chamfer fixture.

All rejected/deferred cases preserve `LegacyAuthoritative=true` and `ProductionOutputChanged=false`.

## STEP and feature-recognition findings
For accepted controlled cases, the shadow candidate is produced, STEP smoke succeeds, and feature recognition parity is captured. The current accepted cases satisfy the recognition contract: one chamfer face, two trimmed adjacent faces, two transition edges, no cylindrical faces, and original sharp-edge replacement.

When recognition parity fails in a future fixture, EDGE-V3 reports `FirstDivergence` and recommends `air-chamfer-shadow-needs-recognition-hardening` without changing production output.

## Legacy authority relationship
Legacy `BrepBoundedChamfer` remains production-authoritative. EDGE-V3 does not replace production chamfer routing and does not alter normal Firmament chamfer execution. The shadow route is internal/test-only and exists to produce readiness evidence beside legacy behavior.

## Explicit unchanged-production statement
Production output is unchanged by construction and by test assertions: `ProductionOutputChanged=false` on every report, including accepted, rejected, deferred, and legacy-dependent fixtures.

## No-3D-Boolean guarantee
The shadow path emits `edge-v3-no-3d-boolean-used` and does not invoke 3D Boolean for the AirChamfer candidate path. Boolean core behavior is unchanged.

## Tests run
EDGE-V3 adds focused FrictionLab tests for deterministic accepted, rejected, deferred, and legacy-dependent shadow route cases. The intended validation set is:
- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirChamferShadow|AirChamferFeatureRecognition|AirChamferControlledBody|AirChamferTopologyGraft|AirChamferClosedWitness|AirChamferGeometryArtifact|AirChamferTopologyPlan|AirChamferJudgment|AirChamferPolicy|AirChamferPatch|EdgeSweep|Chamfer|Fillet|EdgeFinish|CIRLab"`
- `dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|TriangularPrism|FirmamentPrimitive|FirmamentStepExporter|LineArcProfileExtrude|SemanticRecovery|FrepMaterializer|Rematerialize"`
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Judgment|Chamfer|Fillet|Corner|TriangularPrism|BrepPrimitives|Step242|Primitive|Extrude|Boolean|SafeComposition"`

## Limitations
Still deferred:
- production route replacement,
- arbitrary model selection,
- normal Firmament chamfer execution replacement,
- multiple edges,
- corner patches,
- non-planar adjacent faces,
- cylindrical adjacent faces,
- fillets,
- variable distance,
- legacy triangle/chamfer topology,
- ambiguous convex/concave cases,
- triangle migration retry,
- sketch solver, clipping engine, NURBS, or freeform support.

No public API, STEP exporter/importer, Boolean core, production chamfer/fillet behavior, fillet geometry, chain/corner implementation, triangle migration, sketch solver, clipping engine, or NURBS/freeform support changed.

## Next recommended milestone
Because the canonical and safe non-orthogonal shadow route fixtures are expected to stay green, the next recommended milestone is EDGE-X10 controlled opt-in route fixture. If future shadow parity issues appear, run EDGE-X9.1 recognition/body hardening instead, scoped by the first-divergence payload.

## EDGE-X10 export-artifact note
EDGE-X10 adds the first CLI-visible AirChamfer candidate STEP artifact route: `aetheris experimental airchamfer-cube --out <path> [--json]`. The route uses `AirChamferShadowRoute` to invoke the EDGE-V2 real-body prototype and exports the resulting controlled one-edge candidate body to `edge-x10-airchamfer-cube-one-edge.step` by convention. The route remains experimental/lab-only; legacy `BrepBoundedChamfer` stays production-authoritative, normal Firmament chamfer routing is not replaced, and the AirChamfer candidate path still uses no 3D Boolean.
## EDGE-X11 corpus note

EDGE-X11 adds a tiny deterministic regression corpus command: `aetheris experimental airchamfer-corpus --out-dir <dir> [--json]`. It reuses the same non-authoritative `AirChamferShadowRoute->AirChamferRealBodyPrototype` path as EDGE-X10, writes STEP only for successful controlled candidate cases, and emits JSON-only diagnostics for rejected/deferred rows. Legacy `BrepBoundedChamfer` remains production-authoritative; no production route replacement and no 3D Boolean fallback are introduced.


## EDGE-A1 support-gates note

EDGE-A1 now defines the full chamfer/fillet support taxonomy, compatibility rows, and production-readiness gates in `docs/edge-a1-chamfer-fillet-support-compatibility-matrix.md`. EDGE-V3 remains non-authoritative shadow evidence for controlled AirChamfer rows; it is one readiness input, not proof of full support or production route replacement.

## EDGE-X13 Firmament diagnostics note

EDGE-X13 surfaces the existing `AirChamferShadowRoute` through a Firmament-facing, test-only diagnostics probe. The real Firmament fixture still executes legacy `BrepBoundedChamfer` first; the AirChamfer route is invoked afterward as a sidecar using `AirChamferShadowRoute->AirChamferRealBodyPrototype`. The probe records EDGE-X13 diagnostics for legacy authority, unchanged production output, no production route replacement, no 3D Boolean use, feature-recognition capture, and STEP smoke capture. No public API, normal Firmament route, STEP exporter, Boolean core, chamfer/fillet production behavior, arbitrary selection, chain/corner support, or fillet geometry changed.
