# CIR-F8.1 — Surface patch descriptor + surface-family materializer registry scaffold

## Why this scaffold exists

CIR-F0–F7 proved replay-guided CIR→BRep rematerialization for selected pair-specific patterns (`subtract(box,cylinder)`, `subtract(box,box)`) and explicit unsupported diagnostics for torus pressure tests. CIR-F8.1 introduces the architecture shape needed to avoid pair-specific materializer explosion.

Instead of growing:

- `subtract_box_cylinder`
- `subtract_box_box`
- `subtract_box_sphere`
- `subtract_box_torus`

the forward path is descriptor-fed surface-family materializers:

- `PlanarSurfaceMaterializer`
- `CylindricalSurfaceMaterializer`
- `ConicalSurfaceMaterializer`
- `SphericalSurfaceMaterializer`
- `ToroidalSurfaceMaterializer`
- `SplineSurfaceMaterializer`

Pair-specific materializers remain intact as fast-path/compatibility strategies in `CirBrepMaterializer` during this transition.

## Descriptor model introduced

CIR-F8.1 adds internal scaffold descriptors:

- `SourceSurfaceDescriptor`
  - surface family
  - parameter payload reference
  - transform
  - provenance and replay op reference
  - owning CIR node kind
  - orientation role
- `TrimCurveDescriptor`
  - curve family
  - payload reference
  - provenance and replay op reference
  - domain interval
  - capability tag
- `FacePatchDescriptor`
  - source surface descriptor
  - outer/inner trim loops
  - orientation and role
  - adjacency hints

## Family and capability enums

Surface families:

- `Planar`, `Cylindrical`, `Conical`, `Spherical`, `Toroidal`, `Spline`, `Prismatic`, `Unsupported`

Trim curve families:

- `Line`, `Circle`, `Ellipse`, `BSpline`, `Polyline`, `AlgebraicImplicit`, `Unsupported`

Trim capability tags:

- `ExactSupported`, `SpecialCaseOnly`, `Deferred`, `Unsupported`

## Registry skeleton

`SurfaceFamilyMaterializerRegistry` is validation/admission-only in CIR-F8.1.

- Accepts a `FacePatchDescriptor`.
- Runs family materializer candidates through `JudgmentEngine`.
- Returns selected candidate or structured rejection diagnostics.
- Does **not** emit BRep topology/geometry in this milestone.

## Initial family readiness in this milestone

- Planar: admissible for planar patches with exact-supported trims.
- Cylindrical: admissible for cylindrical patches with exact-supported trims.
- Spherical: admissible for spherical patches with exact-supported trims.
- Conical: recognized, explicitly deferred.
- Toroidal: recognized, explicitly deferred.
- Spline: recognized, explicitly deferred.

## SEM-A0 guardrails

No topology naming generation is introduced. Descriptors carry provenance handles only. This preserves SEM-A0 constraints while improving diagnosability.

## Behavior impact

No production CIR→BRep behavior changes in CIR-F8.1:

- existing pair-specific strategy registry remains unchanged,
- no new pair-specific materializers were added,
- no STEP export surface/curve behavior was modified.

## Next step (recommended CIR-F8.2)

Add centralized trim capability matrix (surface-family × trim-curve family × status), then connect descriptor extraction from real CIR + replay operations before any BRep emission from surface-family handlers.
