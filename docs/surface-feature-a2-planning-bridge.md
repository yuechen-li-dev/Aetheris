# SURFACE-FEATURE-A2: non-emitting feature planning bridge

Date: 2026-05-11  
Status: planning scaffold only (no materialization behavior change)

## Purpose

A2 introduces a planning bridge from validated `SurfaceFeatureDescriptor` intent into structured planning artifacts.

The bridge answers:
- required host family + semantic host selector expectations,
- required path/profile constraints,
- expected patch families,
- required trim curve families/capabilities,
- capability route (`CoreExact`, `CoreSplineApprox`, `Forge`, `Deferred`, `Unsupported`) and blockers.

A2 does not emit BRep topology, does not mutate CIR booleans, and does not alter STEP export behavior.

## Planning artifact model

`SurfaceFeaturePlanningResult` captures:
- planning status (`Planned`, `Deferred`, `Forge`, `Unsupported`, `Invalid`),
- feature identity/kind and capability tier,
- `MaterializationClaimed=false` always,
- host/path/profile requirement records,
- expected patch and trim requirements,
- required surface families,
- blocking reasons and diagnostics.

Supporting records:
- `SurfaceFeatureHostRequirement`
- `SurfaceFeaturePathRequirement`
- `SurfaceFeatureProfileRequirement`
- `SurfaceFeaturePatchExpectation`
- `SurfaceFeatureTrimRequirement`

## First-wave planning behavior

### Planar round groove
- Host requirement: `Planar`.
- Path requirement: `CircleOnPlane` with constrained circular alignment diagnostics.
- Profile requirement: `CircularArc` with bounded radius/depth summary.
- Expected patches: host retained/replacement + groove wall/transition expectations.
- Trim requirements: exact circle trims and deferred BSpline approximation policy notes.
- Status: `Planned`, still non-emitting.

### Cylindrical circumferential round groove
- Host requirement: `Cylindrical`.
- Path requirement: `CircumferentialOnCylinder` with coaxial/circumferential alignment diagnostics.
- Profile requirement: `CircularArc`.
- Expected patches/trims: same scaffold shape as planar with cylindrical host role.
- Status: `Planned`, still non-emitting.

### Ridge/bead additive
- Normalized kind is ridge family.
- Additive direction is represented as additive patch role expectations.
- Status: `Planned`, non-emitting.

### Thread
- Classified to `Forge` planning status.
- Diagnostics explicitly call out helical path/topology/export complexity.
- No Core materialization claim.

### Generic torus Boolean / unsupported
- Rejected as `Unsupported` with explicit diagnostic that generic torus Boolean remains unsupported.

### Invalid descriptors
- Return `Invalid` planning status with validation diagnostics.

## Forge/deferred routing

Planning delegates admissibility normalization to A1 validation contract:
- helical/thread -> `Forge`,
- arbitrary curve-on-surface path/profile mismatch -> `Deferred`,
- unsupported host/path or toroidal generic intent -> `Unsupported`.

## SEM-A0 guardrails

A2 preserves SEM-A0 boundaries:
- no generated topology naming,
- no selector namespace expansion,
- no provenance schema broadening,
- no topology/BRep emission.

## JudgmentEngine decision

A2 planner is deterministic descriptor-to-plan mapping with no competing bounded candidates at runtime.
Therefore `JudgmentEngine` is intentionally not used here; diagnostic notes are still explicit via validation + planning blockers.

## Recommended SURFACE-FEATURE-A3

Implement feature-specific dry-run patch generation that consumes `SurfaceFeaturePlanningResult` for planar/cylindrical constrained grooves/ridges and feeds readiness diagnostics into existing materialization-readiness reporting, while keeping `MaterializationClaimed=false` until real BRep emission is implemented.
