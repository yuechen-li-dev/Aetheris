# SURFACE-FEATURE-A4 — planar round-groove evidence model

## Purpose

SURFACE-FEATURE-A4 converts the A3 planar round-groove dry-run into structured **internal evidence objects** suitable for future materializers, while preserving strictly non-emitting behavior.

This milestone is constrained to:

- `RoundGrooveFeatureDescriptor`
- `HostSurfaceFamily = Planar`
- `PathKind = CircleOnPlane`
- `ProfileKind = CircularArc`
- `Direction = Remove`

## Evidence model

A4 introduces `SurfaceFeatureEvidenceResult` and `PlanarRoundGrooveEvidence` with support records for:

- host/path/profile evidence,
- patch-role evidence,
- trim-role evidence,
- exactness/export policy,
- blockers and diagnostics,
- `MaterializationClaimed = false`.

Evidence generation explicitly consumes:

1. `SurfaceFeaturePlanner.Plan(...)` (A2),
2. `SurfaceFeatureDryRunGenerator.Generate(...)` (A3).

If planner or dry-run does not reach planned success, A4 returns terminal/deferred evidence state and does not fabricate geometry.

## Geometry completeness and deferred fields

A4 captures available deterministic descriptor parameters:

- profile radius,
- centerline radius,
- depth/height,
- derived profile width (`2 * profileRadius`).

Current descriptor contracts do not provide typed path placement (`center`, `normal`) for circle-on-plane. A4 therefore marks path geometry status as `DescriptorMissing` and emits explicit diagnostics identifying missing fields rather than inventing values.

## Patch and trim role evidence

A4 maps A3 dry-run role outputs directly into structured evidence:

- patch roles: host-retained planar patch, groove wall, groove bottom/profile,
- trim roles: outer circular boundary, inner circular boundary, profile boundary,
- capability and exactness policies retained from A3 outputs.

## Exact vs spline/deferred policy

A4 preserves policy distinction:

- circular host trims are expected exact when circle-on-plane constraints hold,
- profile coupling can require explicit BSpline approximation policy,
- export/materialization remains deferred.

No exact STEP export claim is made.

## Non-goals preserved

A4 does not implement:

- BRep materialization,
- topology emission,
- STEP export behavior changes,
- CIR Boolean mutation,
- generic torus Boolean support,
- thread geometry,
- public CLI exposure,
- generated topology naming.

## SEM-A0 guardrails

SEM-A0 remains preserved:

- no generated topology naming,
- no selector namespace expansion,
- diagnostics/provenance remain descriptor/planning/dry-run based.

## JudgmentEngine usage decision

A4 mapping is deterministic for one constrained accepted path (planar round groove via A2+A3). No competing runtime strategy set is selected in this milestone, so `JudgmentEngine` is intentionally not used.

## Next step

Extend descriptors/planning contracts with typed circle path placement evidence (center + normal frame), then promote path geometry status from `DescriptorMissing` to exact-ready for A5 materializer preparation.
