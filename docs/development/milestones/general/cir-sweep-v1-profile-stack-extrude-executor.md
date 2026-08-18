# CIR-SWEEP-V1: production profile-stack extrude executor scaffold (bounded stepped-hole)

## Scope
CIR-SWEEP-V1 productionizes only the bounded stepped-hole executor lane using profile-stack extrusion semantics and existing safe-composition BRep construction.

## Why this exists
Stepped-hole repeated 3D subtract sequencing is brittle and opaque for topology diagnosis. This lane emits ordered coaxial z-layer intent directly and calls the existing composition builder.

## Supported V1 shape
- Rectangular host box.
- Z-axis coaxial centered stepped family.
- Exactly three cylindrical semantic tiers from recovery plan.
- Bounded stepped arrangement with explicit per-segment placement metadata.

## Model shape
- `ProfileStackExtrudeSpec`: host extents + ordered layers + diagnostics.
- `ProfileStackLayer`: z interval + inner radius + role + diagnostics.

## Conversion from `HoleRecoveryPlan`
`ProfileStackExtrudePlanAdapter.TryFromSteppedHolePlan(...)` accepts only `HoleKind.Stepped` + `ThroughWithEntryRelief` under explicit placement validation. It derives ordered z-interval layers from segment spans and assigns canonical role/radius semantics.

## Execution route
`ProfileStackExtrudeExecutor.Execute(...)` performs:
1. stack validation (ordering, contiguity, full z coverage),
2. safe-composition hole-chain construction,
3. `BrepBooleanBoxCylinderHoleBuilder.BuildComposition(...)`.

No repeated `BrepBoolean.Subtract(...)` route is used in this stepped lane.

## STEP smoke
Stepped bodies produced through this route are validated in tests with existing `Step242Exporter.ExportBody(...)` smoke markers:
`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CYLINDRICAL_SURFACE`, and no `BREP_WITH_VOIDS`.

## Relationship to boolean fallback
Non-stepped or non-admissible families continue using existing recovery/execution routes; this is not a generic sweep kernel.

## Non-goals
- General profile-stack framework.
- Arbitrary sketch/kernel support.
- Revolve or 2D boolean.
- Exporter behavior changes.
- Public API/CLI expansion.

## V1.1 stabilization notes
- Failure classification from V1 red suite:
  - A/E/F/H: stale stepped diagnostics/expectations in coverage/rematerializer tests still referenced repeated-subtract/deferred wording.
  - B/C: stepped adapter interval construction had top/bottom interval ordering defects causing false `UnsupportedPlan` in canonical stepped rows.
- Stabilization fixes:
  - corrected stepped adapter interval ordering for top-entry and bottom-entry plans,
  - preserved explicit route marker diagnostics for placement-driven semantics (`no-hidden-placement-inference`) and profile-stack route markers,
  - aligned stepped coverage/rematerializer assertions to the profile-stack execution backend.
- Final V1.1 status:
  - stepped canonical route executes via profile-stack + safe composition builder,
  - no repeated 3D subtract route is used,
  - focused FrictionLab/Core suites remain green; Firmament stepped filters restored.
