# AIR-X1: minimal Profile IR + AirProfileStackExtrude EVT lab

## 1) Purpose and EVT scope
AIR-X1 is an EVT-only architecture proof for the first AIR atom (`AirProfileStackExtrude`) and a minimal Profile IR sufficient for current bounded cylindrical hole-family profile-stack scenarios. This lab is confined to FrictionLab code/tests and does not change production routing.

## 2) Code/docs inspected
Inspected before implementation:
- AIR docs: `docs/development/milestones/general/air-a0-atomic-intermediate-representation-kickoff.md`, `docs/development/milestones/general/air-a0-architecture-probe-report.md`.
- Sweep/profile-stack docs: `docs/development/milestones/frictionlab/cir-sweep-x0-profile-stack-extrude-lab.md`, `docs/development/milestones/general/cir-sweep-v1-profile-stack-extrude-executor.md`, `docs/development/milestones/general/cir-sweep-v2-cylindrical-hole-profile-stack-migration.md`.
- Hole-family production: `HoleRecoveryPolicy`, `HoleRecoveryPlan`, `HoleProfileSegment`, `HoleRecoveryExecutor`, `ProfileStackExtrudeSpec`, `ProfileStackLayer`, `ProfileStackExtrudeExecutor`, `ProfileStackExtrudePlanAdapter`, `FrepSemanticRecoveryRematerializer`, and V19 manifest.
- BRep/STEP: `BrepBody`, `TopologyModel`, `BrepBooleanBoxCylinderHoleBuilder`, `Step242Exporter.ExportBody`.
- Tests: FrictionLab profile-stack tests, placement semantics and executor tests, coverage matrix/rematerializer tests.

## 3) Minimal Profile IR proposal
Proposed minimal EVT profile IR:
- `AirRectangleProfile(Width, Height)`
- `AirCenteredCircleLoop(Radius)` (optional inner loop)
- `AirProfileRegion2D(OuterRectangle, InnerCircle?, SemanticRole)`
- `AirProfileStackLayer(ZMin, ZMax, Region, LayerRole, Diagnostics)`
- `AirProfileStackExtrude(Layers, GlobalZMin, GlobalZMax, Diagnostics)`

This is intentionally bounded (rectangle outer + optional centered circle inner + ordered z-layers).

## 4) Profile IR type/field table
Essential now:
- Outer rectangle extents (host region)
- Optional centered circular inner loop
- Layer z interval
- Layer semantic role
- Diagnostics

Deferred intentionally:
- arbitrary polygon/arc loops
- multiple inner loops and loop orientation
- non-centered loops
- 2D boolean provenance
- topology naming or sketch editing operations

## 5) Mapping from `HoleRecoveryPlan`
Observations:
- `HoleRecoveryPlan.ProfileStack` already carries per-segment z span, radius, anchor/through semantics and diagnostics.
- AIR mapping from plan is richest in semantic provenance (best for intent traceability).
- For executable cylindrical lanes, plan can first normalize into `ProfileStackExtrudeSpec` under current adapter validation.

## 6) Mapping from `ProfileStackExtrudeSpec`
Implemented prototype mapper:
- `TryMapFromProfileStackSpec(ProfileStackExtrudeSpec -> AirProfileStackExtrude)`
- preserves per-layer role/z/radius and carries diagnostics.

Recommendation:
- AIR-V1 should accept plan-origin provenance but normalize execution contract through `HoleRecoveryPlan -> ProfileStackExtrudeSpec -> AIR`.

## 7) Scenario results
- Through-hole: represented and emitted successfully (success path).
- Stepped-hole: represented and emitted successfully (flagship X1 proof).
- Blind-hole: representable in AIR model (solid+cut layers), blocked by current executor shape gate requiring positive radius in every layer.
- Counterbore: representable in AIR model as layered radii, emitted in this contiguous-layer encoding.

## 8) STEP smoke results
Successful scenarios verified markers:
- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `CYLINDRICAL_SURFACE`
- no `BREP_WITH_VOIDS`

## 9) Current production path comparison
- Through-hole profile-stack active in production and aligns directly with AIR-X1 representation.
- Stepped-hole profile-stack active in production and aligns directly with AIR-X1 representation.
- Blind-hole remains legacy in production; AIR can represent it, but current ProfileStack executor contract blocks no-hole intervals.
- Counterbore remains legacy in production by adapter policy, though AIR-X1 contiguous stack representation can emit in lab.

## 10) What Profile IR does not support yet
- Generic 2D sketch kernel
- arbitrary loop classes
- conical/chamfer/countersink profile primitives
- cross-axis and interacting feature resolution

## 11) Recommended AIR-V1 DVT scope
- Formalize `AirProfileStackExtrude` around cylindrical contiguous stacks.
- Preserve plan provenance from `HoleRecoveryPlan` in AIR diagnostics.
- Keep emission on existing `ProfileStackExtrudeExecutor`/builder routes.
- Explicitly document blind/counterbore migration boundary and required executor evolution for no-hole/overlap spans.

## 12) Risks / guardrails
Risks:
- over-expanding AIR into sketch kernel work,
- conflating AIR model-side expressiveness with current emitter capability.

Guardrails:
- bounded analytic profiles only,
- explicit blocker diagnostics by model-side vs emitter-side,
- no production route changes in EVT.

## 13) Confidence ratings
- Minimal IR adequacy for through/stepped: High.
- Mapping recommendation quality: Medium-High.
- Blind/counterbore generalized migration readiness: Medium (emitter contract constraints remain explicit).
