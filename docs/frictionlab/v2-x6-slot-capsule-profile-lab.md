# V2-X6 — Slot/capsule profile normalization and extrusion lab

## Purpose and scope
V2-X6 is a **lab-only** milestone to validate whether a slot/capsule can be represented as resolved line+arc 2D topology and advanced through admissibility checks toward through-extrusion **without any 3D Boolean**. This work does not broaden production routing.

## Architecture references
- V2 doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- V2-A1 resolved profile contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- Prior milestones: V2-X1, V2-X3, V2-X4, V2-V3

## Slot/capsule profile definition
The capsule hole loop is explicit: two line segments plus two semicircular arcs (`LabAirLineSegment2D` + `LabAirCircularArc2D`) in one closed loop.
Supported orientations in this lab: horizontal and vertical; rotated is explicitly deferred.

## Validation/admissibility rules
- finite center/length/radius
- `radius > 0`
- `length < 2r` rejected
- `length == 2r` deferred as degenerate-circle policy
- slot must be strictly contained inside rectangular outer profile
- touching or crossing the outer boundary is rejected
- deterministic orientation normalization inherits V2-X1 validator path

## Extrusion attempt status
Extrusion is attempted for valid profile rows and then intentionally blocked with deterministic diagnostic:
- `v2-x6-slot-extrude-blocked:current-emitter-assumes-full-circle-hole-loops`

This blocker is specific to current profile-hole emitter assumptions; no 3D Boolean was introduced.

## Topology / STEP findings
Because extrusion is blocked in this lab cut, topology/STEP smoke for slot bodies is not claimed. Evidence delivered is successful profile normalization + bounded deterministic blocker.

## Invalid/deferred cases
Covered explicitly:
- outside / touching / crossing boundary
- radius <= 0
- length < 2r
- length == 2r (deferred)
- rotated slot (deferred)

## Non-goals (unchanged)
- no production routing changes
- no general 2D clipping engine
- no sketch solver
- no blind/counterbore/stepped/cross-axis slot support
- no STEP exporter changes
- no Boolean core changes
- no NURBS/freeform support

## Recommended next step
Extend internal emitter capability from full-circle-only hole loops to bounded line+arc hole loops, then re-run V2-X6 as an extrusion-success candidate before any production route expansion.


Update note (V2-X7): lab-only line/arc profile extrusion now demonstrates slot/capsule hole side-face emission without 3D Boolean via `LineArcProfileExtrudeLab`; production routing remains unchanged.


Update note (V2-V4): slot/capsule horizontal hole topology is now additionally exercised through production-adjacent `LineArcProfileExtrudeEmitter` tests; broad production routing remains unchanged.
