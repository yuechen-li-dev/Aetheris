# EDGE-PROFILE-X2 — Top-edge chamfer as profile-stack / section-transition lab

## Purpose and scope

EDGE-PROFILE-X2 is a FrictionLab-only experiment for expressing one horizontal/top-edge chamfer constructively. It tests whether a known rectangular prism with a top edge chamfer can be emitted directly from section/profile evolution along Z rather than by BRep trimming, topology grafting, AirEdgeSweep, BrepBoundedChamfer, or a 3D Boolean.

This lab does **not** change production chamfer/fillet behavior, production profile-stack routing, STEP export/import, the Boolean core, AirEdgeSweep, triangle migration, sketch solving, clipping, or NURBS/freeform support.

## Reference context

EDGE-A2 reframed many chamfers as final topology that should be authored directly when construction history is known. EDGE-PROFILE-X1 proved the vertical-edge case by replacing a rectangle corner with a bevel segment and extruding the final pentagon profile directly. EDGE-PROFILE-V1 packaged that vertical-edge/profile-authored proof into a production-adjacent emitter.

EDGE-PROFILE-X2 extends the same constructive doctrine to the horizontal/top-edge case by testing a profile stack / section-transition interpretation.

## Theory under test

For an extruded rectangular prism, a top horizontal edge chamfer can be represented as section/profile evolution along Z:

- lower section interval: full rectangle,
- top section: related rectangle with the chosen top boundary inset,
- transition interval: ruled planar faces between corresponding profile edges,
- final body: emitted directly from these known sections.

The bounded contract chosen for this lab is a top +X side chamfer, not a local corner chamfer. The +X side of the top profile is inset by the chamfer distance while the lower prism remains a full rectangle.

## Routes explored

### Route A — Existing profile-stack executor probe

The existing FrictionLab/production-adjacent profile-stack concepts are currently centered on rectangular host extents plus optional circular inner cut intervals. The EDGE-PROFILE-X2 probe records Route A as attempted but blocked for this case because the current layer model has no arbitrary polygon section profile contract and no ruled polygon-to-polygon transition emitter.

Deterministic blockers:

- `edge-profile-x2-profile-stack-polygon-profile-blocker`
- `edge-profile-x2-ruled-transition-emitter-missing-blocker`

The lab intentionally does not broaden the existing `ProfileStackExtrudeExecutor` or production profile-stack behavior.

### Route B — Lab-only polygon section-transition emitter

Route B succeeds with a small lab-only line-only section-transition emitter. It accepts the explicit three-section contract below, creates fixed correspondence by vertex index, and emits all planar faces directly.

The route emits:

- bottom cap,
- top cap,
- four lower vertical prism side faces,
- four upper section-transition faces,
- one of those transition faces classified as the chamfer face.

### Route C — Direct constructive witness

Route C was not needed because Route B produced the closed constructive witness. The Route B implementation is still intentionally bounded and lab-only; it is not a reusable production section-transition emitter yet.

## Candidate geometry

Canonical case: `canonical-top-pos-x-edge`

- width = 10
- depth = 8
- height = 6
- chamferDistance = 1
- z0 = 0
- z1 = height - chamferDistance = 5
- z2 = height = 6
- bottom/lower full rectangle at z0 and z1:
  - (-5, -4)
  - (5, -4)
  - (5, 4)
  - (-5, 4)
- top inset rectangle at z2:
  - (-5, -4)
  - (4, -4)
  - (4, 4)
  - (-5, 4)

The profile correspondence is explicit by vertex/edge index. Edge index 1, from +Y/-Y endpoints on the +X side, transitions from x = 5 at z = 5 to x = 4 at z = 6 and is the chamfer transition face.

## Topology findings

The successful Route B witness is a closed planar BRep with stable lab counts:

- vertices: 12
- edges: 20
- faces: 10
- planar faces: 10
- cylindrical faces: 0
- lower prism side faces: 4
- transition faces: 4
- chamfer transition faces: 1
- loops: 10
- coedges: 40
- bounds: `[-5,-4,0]..[5,4,6]`

The topology intentionally keeps the lower vertical side faces and upper transition faces split at z = 5, even where adjacent portions are coplanar, because this preserves the section-stack witness and makes the transition interval machine-checkable.

## STEP smoke findings

The Route B witness exports through the existing `Step242Exporter` and passes the lab smoke contract.

Required markers present:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Required markers absent:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

## No-trim/no-graft/no-legacy-route guarantee

The candidate path records deterministic diagnostics confirming that it does not use:

- AirEdgeSweep,
- BrepBoundedChamfer,
- topology graft/body mutation,
- 3D Boolean.

The lab also uses no BRep trimming, no sketch solver, no clipping engine, and no NURBS/freeform support.

## Invalid/rejected cases

Invalid cases reject before Route A/B geometry attempts:

- non-positive or non-finite dimensions: `edge-profile-x2-invalid-dimensions-rejected`
- non-positive, non-finite, or too-large chamfer distance: `edge-profile-x2-invalid-chamfer-distance-rejected`

## Non-goals

- no production route replacement,
- no production chamfer/fillet behavior change,
- no vertical-edge chamfer work beyond referencing X1/V1,
- no local top-corner chamfer,
- no AirEdgeSweep route,
- no BrepBoundedChamfer route,
- no STEP exporter/importer changes,
- no Boolean core changes,
- no triangle migration,
- no sketch solver,
- no clipping engine,
- no NURBS/freeform support.

## Recommendation

`profile-stack-chamfer-needs-section-transition-emitter`

EDGE-PROFILE-X2 proves the top +X horizontal edge chamfer theory for one bounded all-planar witness through a lab-only section-transition emitter. The next production-adjacent step is not to generalize the existing circular-hole profile-stack executor in-place, but to define a first-class polygon section-transition/profile-correspondence contract and then evaluate a production-adjacent profile-stack chamfer emitter against that contract.

Follow-up: EDGE-PRISMATIC-A0 creates that first-class contract in `docs/development/milestones/general/edge-prismatic-a0-section-transition-contract-audit.md`, using the term **prismatic section transition** for axis-stacked resolved profile evolution with explicit correspondence and deterministic transition-face emission. Future top/horizontal chamfer work should target that contract rather than extending this one-off witness directly.

## EDGE-PRISMATIC-X1 follow-up

EDGE-PRISMATIC-X1 generalizes this lab's Route B section-transition witness into a reusable FrictionLab-only `PrismaticSectionTransitionEmitter`. Instead of the one-off top +X chamfer construction, X1 consumes explicit Z-stacked line-only sections, identity correspondence, and deterministic transition intervals, then emits closed planar BReps directly.

The X1 result preserves the EDGE-PROFILE-X2 conclusion that horizontal/top-edge chamfer construction should proceed through prismatic section evolution rather than AirEdgeSweep, `BrepBoundedChamfer`, topology grafting, or 3D Boolean fallback. It also keeps the current `ProfileStackExtrudeExecutor` unchanged; X1 is lab-only evidence for a generic prismatic emitter, not a production profile-stack replacement.


## EDGE-PRISMATIC-X2 follow-up

EDGE-PRISMATIC-X2 re-expresses this lab's Route B top `+X` chamfer witness through the reusable lab-only `PrismaticSectionTransitionEmitter` introduced by EDGE-PRISMATIC-X1. The canonical topology and STEP smoke contract remain the same, but the motivating proof path is no longer a one-off Route B topology builder; it is now a prismatic-emitter-backed lab route.

This follow-up still leaves `ProfileStackExtrudeExecutor` unchanged and does not admit a production chamfer/fillet route.
