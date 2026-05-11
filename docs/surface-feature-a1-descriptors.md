# SURFACE-FEATURE-A1: descriptors, capability taxonomy, and validation-only contracts

Date: 2026-05-11  
Status: descriptor-first scaffold only (no materialization change)

## Scope

This milestone introduces internal, test-facing surface feature vocabulary and validation contracts so groove/ridge-like intent is represented semantically rather than as arbitrary boolean tool-body subtraction/union.

Explicitly out of scope:
- BRep face/edge/vertex emission,
- topology naming expansion (SEM-A0 guardrails remain in force),
- STEP behavior changes,
- CIR boolean behavior changes,
- generic torus boolean materialization,
- thread geometry generation.

## Taxonomy

Added descriptor taxonomy includes:
- `SurfaceFeatureKind`: `RoundGroove`, `Ridge`, `Bead`, `Thread`, `Emboss`, `Deboss`, `Knurl`, `Dimple`, `Unsupported`.
- Host family alignment reuses existing `SurfacePatchFamily`: `Planar`, `Cylindrical`, `Conical`, `Spherical`, `Toroidal`, `Spline`, `Unsupported`.
- `SurfaceFeaturePathKind`: `CircleOnPlane`, `CircumferentialOnCylinder`, `LatitudeOnSphere`, `HelixOnCylinder`, `CurveOnSurface`, `Unsupported`.
- `SurfaceFeatureProfileKind`: `CircularArc`, `VProfile`, `Trapezoid`, `FlatBottom`, `Custom`, `Unsupported`.
- `SurfaceFeatureDirection`: `Remove` and `Add`.
- `SurfaceFeatureCapabilityTier`: `CoreExact`, `CoreSplineApprox`, `Forge`, `Deferred`, `Unsupported`.
- `SurfaceFeatureValidationStatus`: `Valid`, `WarningDeferred`, `Unsupported`, `Invalid`.

## Descriptor-first model

Primary internal model:
- `SurfaceFeatureDescriptor`: feature id/kind, host surface reference + family, path kind, profile kind, direction, bounded dimensions, alignment flag, capability target, and optional parameter dictionaries.
- `RoundGrooveFeatureDescriptor`: bounded constrained round-groove descriptor for first-wave planar/cylindrical circular/circumferential validation.

A1 keeps this internal and diagnostic-rich; it does not claim runtime surface-feature materialization.

## Validation-only contract

`SurfaceFeatureValidator.Validate(...)` returns `SurfaceFeatureValidationResult` containing:
- status,
- capability tier,
- explicit `MaterializationClaimed=false` indicator,
- normalized family classification,
- precise diagnostics.

### First-wave validation policy

Valid/planned (`CoreExact` target, still no materialization claim):
- round groove on planar host with circular path and circular-arc profile,
- round groove on cylindrical host with circumferential path and circular-arc profile,
- ridge/bead additive counterpart under the same constraints.

Deferred/unsupported:
- `Thread`/helical path: classified Forge/deferred due to helix topology/export complexity,
- arbitrary `CurveOnSurface` path: deferred,
- toroidal/generic torus-like modeling: unsupported with explicit diagnostic that generic torus boolean remains unsupported for exact materialization.

Invalid:
- non-positive radii/depth/height,
- missing alignment/coaxiality-style constraint,
- direction/sign mismatch against kind constraints.

## SEM-A0 guardrails maintained

No selector namespace expansion, no generated topology naming, and no provenance model broadening are introduced in A1.

## Recommended SURFACE-FEATURE-A2

Implement planning bridge from validated descriptors into non-emitting feature planning artifacts (host patch targeting + readiness diagnostics), still without BRep emission, then stage bounded materialization in a later milestone.
