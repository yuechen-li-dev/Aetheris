# CIR-F9: first gated PlanarSurfaceMaterializer BRep emission

## Scope

CIR-F9 introduces the first bounded surface-family emission path from a `FacePatchDescriptor` to concrete BRep topology for a single trivial planar case.

Supported in this milestone:

- `PlanarSurfaceMaterializer.Emit` for **rectangular untrimmed planar patches** encoded by `SourceSurfaceDescriptor.ParameterPayloadReference` as `rect3d:x1,y1,z1;x2,y2,z2;x3,y3,z3;x4,y4,z4`.
- Emission of a minimal single-face BRep body (face/loop/edge/coedge/vertex topology, planar surface binding, line edge bindings).

## Readiness gate

The emission method is explicitly gated by `MaterializationReadinessReport`.

Rule: **No readiness, no emission.**

Emission is rejected when `OverallReadiness` is:

- `NotApplicable`
- `Deferred`
- `Unsupported`

This preserves CIR-F8.10 semantics while allowing a narrow local implementation.

## Rejected cases

CIR-F9 rejects:

- non-planar source surface families,
- any patch with outer or inner trims (including circular trims),
- planar descriptors missing the bounded `rect3d:` payload.

Diagnostic messages are returned in `SurfaceMaterializationResult.Diagnostics`.

## Relation to CIR-F8.10

`MaterializationReadinessAnalyzer` still reports global topology emission as not implemented. CIR-F9 does not generalize that; it adds a localized, explicitly constrained emission path in `PlanarSurfaceMaterializer` only.

## Non-goals

CIR-F9 does **not** implement:

- general topology assembly,
- spherical/cylindrical/conical/toroidal emission,
- circle/ellipse trim solving,
- public CLI exposure,
- production STEP/export behavior changes,
- generated topology naming.

## Next step

CIR-F10 should replace synthetic `rect3d:` input with direct consumption of real dry-run planned patch/loop evidence for exact-ready planar faces without expanding to circular trims.
