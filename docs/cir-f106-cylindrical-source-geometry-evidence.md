# CIR-F10.6: canonical cylindrical source geometry evidence

## Why this milestone was needed

CIR-F10.5 introduced canonical retained circular loop binding, but the real box-minus-cylinder path still deferred because real cylindrical source descriptors only carried family/role/provenance. They did not carry canonical axis/radius/span evidence.

## What was added

`SourceSurfaceDescriptor` now carries optional `CylindricalSurfaceGeometryEvidence` with:

- `AxisOrigin`
- `AxisDirection`
- `Radius`
- `Height`
- `BottomCenter`
- `TopCenter`

This evidence is internal-only dry-run geometry evidence and does not introduce generated topology naming (SEM-A0 preserved).

## Extractor behavior

For `CirCylinderNode` side descriptors:

- canonical cylindrical evidence is populated from real cylinder semantics,
- identity and translation transforms preserve evidence,
- non-uniform radial transforms (shear/non-uniform scale-like behavior) are deferred with diagnostics.

Cap descriptors keep existing F10.2 circular bounded planar geometry behavior.

## Binder behavior

`RetainedLoopGeometryBinder.TryBindCircularLoop(...)` now consumes cylindrical evidence directly (rather than ad hoc payload radius parsing), verifies planar normal and cylinder axis compatibility, computes plane/axis intersection center, and binds canonical `RetainedCircularLoopGeometry` when safe.

## What now succeeds

Real `Subtract(Box, Cylinder)` dry-run candidate generation can now produce retained inner circular loop geometry on base-side planar candidates using real extracted cylindrical source evidence.

## Still deferred

- inner-loop BRep emission (F10.4 policy still blocks),
- annular planar face emission,
- cylindrical side BRep emission,
- torus and other higher-order special cases.

## Recommended next step

Consume `RetainedCircularLoopGeometry` in a tightly scoped planar inner-loop emitter milestone (outer loop + single inner circle), retaining readiness gates and explicit orientation policy.
