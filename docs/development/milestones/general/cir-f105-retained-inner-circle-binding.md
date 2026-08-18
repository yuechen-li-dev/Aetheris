# CIR-F10.5: canonical retained-loop inner-circle geometry binding

## Goal

CIR-F10.4 deferred one-outer + one-inner circular planar loop emission because retained loop descriptors lacked canonical circle geometry/orientation evidence.

CIR-F10.5 adds **evidence-only binding** for retained-loop descriptors without enabling inner-loop BRep emission.

## What landed

- Added `RetainedCircularLoopGeometry` on `RetainedRegionLoopDescriptor`.
- Added `RetainedLoopGeometryBinder.TryBindCircularLoop(...)`.
- Binder currently supports one safe canonical case:
  - source surface: planar
  - opposite/tool surface: cylindrical
  - trim family: circle
  - loop status: exact/special-case ready
  - plane normal must be parallel to cylinder axis
  - cylinder radial transform must remain circular (no non-uniform radial scaling)
- Orientation policy is explicit on geometry evidence:
  - base-side planar retained inner loop uses `ReverseForToolCavity`
  - tool-side uses `UseCandidateOrientation`

## Explicit non-goals (still deferred)

- No inner-loop BRep topology emission
- No annular face materialization
- No STEP/export behavior changes
- No torus/quartic/algebraic loop binding
- No ellipse binding for non-perpendicular planar/cylindrical intersections
- No multi-inner-loop emission

## Diagnostics and safety gates

Binding is skipped/deferred when:

- loop is deferred/unsupported,
- trim family is not circle,
- source/opposite family pair is not planar/cylindrical,
- planar bounded geometry is unavailable,
- plane normal and cylinder axis are not parallel,
- transformed cylinder radial evidence is not circular.

This preserves CIR-F10.4 readiness behavior and SEM-A0 constraints.

## Next step

CIR-F10.6 can consume this canonical loop geometry evidence inside `PlanarSurfaceMaterializer` to emit one outer loop plus one inner circular hole loop (still bounded by current single-hole scope).
