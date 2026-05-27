# V2-V4 — Line/Arc profile extrude production-adjacent evaluation

Introduces internal `LineArcProfileExtrudeEmitter` as a production-adjacent emitter for validated line/arc/full-circle loop profiles with linear height extrusion and direct analytic BRep emission (no 3D boolean).

## Scope
- One outer loop plus zero or more hole loops.
- Supported curves: line segment, circular arc, full circle.
- Positive finite height only.
- No production routing broadening in this milestone.

## References
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- `docs/frictionlab/v2-x6-slot-capsule-profile-lab.md`
- `docs/frictionlab/v2-x7-line-arc-profile-extrude-lab.md`
- `docs/v2-v1-profile-hole-extrude-production-evaluation.md`
- `docs/v2-v2-profile-hole-extrude-through-hole-integration.md`

## Emission model
- line segment -> planar side face.
- circular arc -> cylindrical side face.
- full circle -> cylindrical side face.
- top and bottom planar caps emitted with outer + inner loops.

## Current findings
- Rectangle-only, rectangle+circle hole, rectangle+two circles, and rectangle+horizontal slot are covered by tests.
- STEP smoke remains valid via existing exporter (`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`, plus `CYLINDRICAL_SURFACE` when expected).
- `BREP_WITH_VOIDS` is not used in covered cases.
- Diagnostic includes explicit `v2-v4-no-3d-boolean-used`.

## Invalid/deferred behavior
Current implementation rejects invalid height, unsupported topology (not exactly one outer), and basic invalid curve dimensions before emission.

## Relationship to existing emitter
`ProfileHoleExtrudeEmitter` remains the bounded production through-hole route. `LineArcProfileExtrudeEmitter` is internal and production-adjacent only in V2-V4.

## Optional polygon support
Triangle/hex generic polygon outer-loop enablement was not included in this milestone and remains a next audit target.

## Non-goals confirmed
No changes were made to production routing, sketch solver, full clipping engine, STEP exporter, boolean core, blind/counterbore/stepped/cross-axis support, or NURBS/freeform support.

## Next milestone recommendation
Promote slot/capsule production integration through the existing bounded routing gates after additional invalid/deferred profile coverage parity with V2-X1.
