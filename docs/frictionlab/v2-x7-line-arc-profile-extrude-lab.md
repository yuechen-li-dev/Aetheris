# V2-X7 — Generic line/arc profile-loop extrusion emitter lab

Purpose: lab-only validation of a generic profile-loop extrusion path for `ResolvedProfile2D`-style line/arc/full-circle loops into analytic BRep faces, without 3D Boolean.

## Scope and doctrine

Aligned to V2 doctrine (`docs/aetheris-v2-sweep-first-architecture.md`) and V2-A1 resolved profile contract (`docs/aetheris-v2-a1-resolved-profile2d-contract.md`).
References: V2-X1, V2-X3, V2-V1, V2-V2, V2-X6.

V2-X6 blocker addressed: prior emitter assumed full-circle hole loops only (`v2-x6-slot-extrude-blocked:current-emitter-assumes-full-circle-hole-loops`).

## Lab emitter model

`LineArcProfileExtrudeLab` emits:
- line segment boundary -> planar side face
- circular arc boundary -> cylindrical side face
- full circle boundary -> cylindrical side face

It also emits top and bottom planar cap faces with outer/inner loops.

## Supported subset

- One outer loop.
- Zero or more hole loops.
- Curves limited to line segment, circular arc, full circle.
- Deterministic rejection/defer for invalid/deferred cases.

## Results summary

Successful cases:
- rectangle only (20x10, h=5)
- rectangle + one centered circle hole
- rectangle + one centered horizontal slot/capsule hole
- rectangle + one off-center horizontal slot/capsule hole
- rectangle + two circle holes

Topology findings:
- rectangle only: planar=6, cylindrical=0
- rectangle + one circle: planar=6, cylindrical=1
- rectangle + slot: planar=8, cylindrical=2

STEP smoke findings (successful cases):
- contains `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`
- circle/slot cases contain `CYLINDRICAL_SURFACE`
- does not contain `BREP_WITH_VOIDS`

No-3D-Boolean guarantee:
- candidate path records `v2-x7-no-3d-boolean-used`.
- no `BrepBoolean.Subtract` and no 3D Boolean operation in lab emitter path.

## Invalid/deferred coverage

Includes deterministic stop examples for:
- zero/negative height rejection
- profile-validation deferred paths (e.g., multiple outers)

## Recommendation

Current evidence supports: `line-arc-profile-extrude-ready-for-production-evaluation` for covered bounded cases, with continued hardening for broader topology/deferred classifications.

## Non-goals

- no production routing changes
- no full clipping engine
- no sketch solver
- no STEP exporter/core Boolean changes
- no blind/counterbore/stepped/cross-axis
- no NURBS/freeform curves

## Next step

Production-adjacent generic line/arc profile extrude emitter evaluation and slot/capsule production admissibility audit.
