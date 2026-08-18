# CIR-F10.3: circular planar cap emission in `PlanarSurfaceMaterializer`

## Scope

Implemented support for **one untrimmed circular planar patch** emitted from planar source descriptors whose bounded geometry is:

- `Kind = Circle`
- finite positive `Radius`
- non-zero `Normal`

This is strictly a single-face patch emission milestone, not full cylinder rematerialization.

## Readiness gate

Circular emission follows the same gate as rectangular emission:

- if readiness is `NotApplicable`, `Deferred`, or `Unsupported` => reject emission
- diagnostic includes `readiness-gate-rejected: no readiness, no emission.`

## Topology convention used

The emitter follows the circular edge convention already used in `BrepPrimitives.CreateCylinder(...)`:

- one self-loop edge (`startVertex == endVertex`)
- one loop containing one coedge
- one planar face bound to that loop
- one shell/body
- one circle curve binding with trim `[0, 2π]`

This keeps circular topology/export semantics aligned with existing kernel primitive practice.

## Diagnostics

On circular success, diagnostics now include explicit evidence for:

- readiness accepted
- circular bounded geometry accepted
- circular loop topology emitted

On failure (e.g., missing bounded circle), the rectangular payload parse path still fails and includes a circular evaluation diagnostic token.

## Rejected / deferred cases

Still intentionally rejected/deferred in CIR-F10.3:

- trimmed circular faces
- annular faces / inner holes / multiple loops
- non-planar surfaces
- full cylinder shell assembly
- cylindrical side emission
- elliptical/sheared cap emission

For sheared/non-uniform transforms, `SourceSurfaceExtractor` continues to defer circular bounded geometry and emits
`cylinder-cap-circular-geometry-deferred`.

## Relationship to future work

This unlocks cap-only circular planar emission from real cylinder source descriptors while preserving SEM-A0 guardrails (no generated topology naming) and without changing STEP exporter behavior.
