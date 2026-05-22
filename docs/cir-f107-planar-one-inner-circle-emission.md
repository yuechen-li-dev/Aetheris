# CIR-F10.7: bounded planar one-inner-circle emission

CIR-F10.7 adds a tightly-scoped planar trimmed-face emission path in `PlanarSurfaceMaterializer`:

- supported shape: one planar face with outer rectangle + one inner circle,
- evidence source: canonical `RetainedCircularLoopGeometry` from real dry-run (`Subtract(Box,Cylinder)`),
- readiness gate: `No readiness, no emission` (deferred/unsupported readiness rejects).

## Supported/required evidence

`EmitRectangleWithInnerCircle(...)` requires:

1. planar source with `BoundedPlanarPatchGeometryKind.Rectangle`,
2. exactly one canonical retained inner-circle geometry,
3. readiness not deferred/unsupported.

If any is missing, emission is rejected with specific diagnostics.

## Topology convention

- one face,
- two loops in order: outer loop first, inner loop second,
- outer loop = four line edges,
- inner loop = one circular self-edge,
- inner coedge orientation is explicitly set for cavity convention,
- face is bound to one plane surface, and the inner edge is bound to circle geometry.

## Explicit rejections

- multiple inner loops,
- missing canonical circular loop geometry,
- missing bounded rectangle,
- deferred/unsupported readiness,
- non-circular inner loops,
- circular outer + inner annulus.

## Scope boundaries

This milestone does **not** implement:

- full box-cylinder body materialization,
- cylindrical side emission,
- shell closure/solid completion,
- multi-hole emission,
- STEP export behavior changes,
- public CLI exposure,
- generated topology names.

SEM-A0 guardrails remain: no generated topology naming/provenance expansion.
