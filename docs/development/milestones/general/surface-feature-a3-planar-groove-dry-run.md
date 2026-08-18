# SURFACE-FEATURE-A3 — planar round-groove dry-run patch generation

## Purpose

SURFACE-FEATURE-A3 consumes SURFACE-FEATURE-A2 planning output and produces a **non-emitting dry-run report** for one constrained feature intent:

- `RoundGrooveFeatureDescriptor`
- host: `SurfacePatchFamily.Planar`
- path: `SurfaceFeaturePathKind.CircleOnPlane`
- profile: `SurfaceFeatureProfileKind.CircularArc`
- direction: `SurfaceFeatureDirection.Remove`

This milestone does not emit BRep topology or materialize geometry.

## Supported descriptor shape

A3 supports exactly the constrained planar round-groove descriptor above.
All other descriptors are returned as planner-terminal (`Invalid` / `Unsupported` / `Forge` / `Deferred`) or A3 dry-run deferred scope.

## Dry-run output model

`SurfaceFeatureDryRunResult` reports:

- planned/deferred/terminal status,
- capability tier,
- `MaterializationClaimed = false`,
- structured patch expectations,
- structured trim expectations,
- explicit blockers and diagnostics.

Patch expectations are represented by `SurfaceFeaturePatchDryRun` and include roles such as:

- `HostRetainedPlanarPatch`
- `GrooveWallPatch`
- `GrooveBottomOrProfilePatch`
- `DeferredPatch`

Trim expectations are represented by `SurfaceFeatureTrimDryRun` and include roles such as:

- `OuterGrooveBoundary`
- `InnerGrooveBoundary`
- `ProfileBoundary`
- `DeferredTrim`

## Patch/trim expectation policy

For constrained planar circular grooves, A3 reports:

- retained planar host patch expectations around the annular groove region,
- groove wall/profile transition expectations as toroidal/revolved-profile-like surfaces,
- circular boundary trim expectations on planar host,
- deferred `BSpline` policy for profile coupling when exact elementary curve representation is not yet available.

## Exact vs spline/deferred

A3 can claim **planning readiness only**.
It does not claim exact emitted toroidal/profile patches or STEP exactness.
Any spline route is diagnostic-only and must remain explicit in the dry-run report.

## Explicit non-goals

A3 does **not** implement:

- BRep face/edge/loop/coedge emission,
- topology materialization,
- STEP export behavior changes,
- CIR Boolean mutation,
- generic torus Boolean support,
- thread geometry,
- public CLI exposure,
- generated topology naming.

## SEM-A0 guardrails

A3 preserves SEM-A0 boundaries:

- no generated topology naming,
- no expansion of public topology identity policy,
- provenance remains descriptor/planning-oriented.

## JudgmentEngine usage decision

A3 dry-run generation is deterministic descriptor-to-report mapping for one accepted intent.
No competing strategy set is currently selected at runtime; therefore `JudgmentEngine` is intentionally not used in this milestone.

## Next milestone (A4)

SURFACE-FEATURE-A4 should prototype **internal patch/trim evidence realization** for the planar groove case:

- bind dry-run patch roles to concrete internal evidence structures,
- keep non-emitting behavior,
- harden exact-vs-approx policy boundaries before any BRep materialization milestone.
