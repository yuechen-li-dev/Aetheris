# EDGE-PROFILE-X1 — Vertical-edge chamfer as profile extrusion lab

## Purpose and scope

EDGE-PROFILE-X1 is a lab-only proof that a vertical-edge chamfer on a rectangular prism can be authored as profile geometry before extrusion. It validates the EDGE-A2 constructive chamfer reframing for the narrow history-known case where the selected edge is parallel to the extrusion axis.

EDGE-PROFILE-V1 now packages this proof into the internal production-adjacent `ProfileVertexChamferExtrudeEmitter`. The V1 emitter keeps the same profile-first/extrude-second construction, adds bounded request/result diagnostics, rectangle invalid-case rejection, STEP smoke summarization, and evaluation-only convex polygon support without changing production routing.

The lab does **not** change production chamfer or fillet behavior. It does not change public APIs, STEP export/import, Boolean core behavior, AirEdgeSweep behavior, triangle migration, sketch solving, clipping, or NURBS/freeform support.

## EDGE-A2 reference

`docs/development/milestones/general/edge-a2-constructive-chamfer-reframing-audit.md` classifies vertical extrusion-edge chamfers as profile modifications: the sharp edge should not be emitted and then trimmed. Instead, the rectangle corner is replaced by a bevel segment in 2D, and the final pentagonal profile is extruded directly.

## Theory

For a centered rectangle with width `w` and depth `d`, the unchamfered profile is:

1. `(-w/2, -d/2)`
2. `( w/2, -d/2)`
3. `( w/2,  d/2)`
4. `(-w/2,  d/2)`

For the `+X,+Y` vertical edge, EDGE-PROFILE-X1 replaces corner `(w/2, d/2)` with two offset points:

- `(w/2, d/2 - chamferDistance)`
- `(w/2 - chamferDistance, d/2)`

The result is a five-line pentagon. Extruding that pentagon along +Z directly emits the chamfered rectangular prism. The bevel face is the side face corresponding to the inserted bevel segment.

## Candidate construction

The lab implementation is `ProfileChamferExtrudeLab` in `Aetheris.Firmament.FrictionLab/CIRLab/ProfileChamferExtrudeLab.cs`.

Construction steps:

1. Validate finite positive `width`, `depth`, and `height`.
2. Validate finite positive `chamferDistance` and reject distances greater than or equal to half of the smaller adjacent rectangle dimension.
3. Build the line-only pentagon profile for the `+X,+Y` corner.
4. Validate the profile through the lab `ResolvedProfile2DLab` contract.
5. Lower through `BrepExtrude.Create` using an `ExtrudeFrame3D` with origin `(0,0,0)`, +Z normal, and +X U axis.
6. Export the resulting body with the existing `Step242Exporter`.
7. Summarize topology, STEP smoke markers, diagnostics, and recommendation.

`BrepExtrude.Create` was used because it is the direct existing polyline extrusion route for a single line-only outer loop. The result remains aligned with the V2 profile doctrine: the candidate path authors the chamfer in 2D profile space and emits the final prism directly.

## Test cases and results

Valid cases:

| Case | Width | Depth | Height | Chamfer distance | Result |
|---|---:|---:|---:|---:|---|
| `canonical-centered-box` | 10 | 8 | 6 | 1 | Succeeds |
| `larger-valid-chamfer` | 10 | 8 | 6 | 2 | Succeeds |
| `non-square-rectangle` | 12 | 5 | 7 | 1 | Succeeds |

Invalid cases reject before extrusion:

| Case | Reason |
|---|---|
| `invalid-zero-chamfer-distance` | `chamferDistance <= 0` |
| `invalid-too-large-chamfer-distance` | `chamferDistance >= min(width, depth) / 2` |
| `invalid-width` | non-positive width |
| `invalid-depth` | non-positive depth |
| `invalid-height` | non-positive height |
| `invalid-non-finite-width` | non-finite dimension |

## Topology findings

The valid EDGE-PROFILE-X1 cases produce the expected one-corner vertical-edge chamfered prism topology:

| Metric | Expected | Observed |
|---|---:|---:|
| Profile vertices | 5 | 5 |
| Body vertices | 10 | 10 |
| Edges | 15 | 15 |
| Faces | 7 | 7 |
| Cap faces | 2 | 2 |
| Side faces | 5 | 5 |
| Chamfer/bevel side faces | 1 | 1 |
| Planar faces | 7 | 7 |
| Cylindrical faces | 0 | 0 |
| Loops | 7 | 7 |
| Coedges | 30 | 30 |

Canonical bounds are `[-5,-4,0]..[5,4,6]`.

## STEP smoke findings

The existing `Step242Exporter` successfully exports the valid lab body.

Required markers are present:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Forbidden markers are absent:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

## Optional legacy comparison status

Legacy comparison is deferred for this milestone. The expected count pattern matches the bounded one-corner box chamfer family (`10` vertices, `15` edges, `7` faces, all planar), but EDGE-PROFILE-X1 intentionally avoids contorting the lab around legacy face/edge identifiers or corner conventions. The lab evidence is the direct profile-extrusion construction itself.

## No-trim/no-graft/no-AirEdgeSweep/no-Boolean guarantee

The candidate path does not call or route through:

- `AirEdgeSweep`
- `BrepBoundedChamfer`
- topology graft/body mutation code
- 3D Boolean fallback
- BRep trimming/splitting/stitching

The lab records deterministic diagnostics for successful candidate cases:

- `edge-profile-x1-no-air-edge-sweep-used`
- `edge-profile-x1-no-brep-bounded-chamfer-used`
- `edge-profile-x1-no-topology-graft-used`
- `edge-profile-x1-no-3d-boolean-used`

## Invalid/rejected cases

Invalid cases are rejected before extrusion with machine-checkable diagnostics:

- `edge-profile-x1-invalid-dimensions-rejected`
- `edge-profile-x1-invalid-chamfer-distance-rejected`
- `edge-profile-x1-chamfer-distance-too-large-rejected`

The profile validation path can also surface `edge-profile-x1-profile-self-intersection-rejected` if a future case introduces a self-intersecting profile.

## Non-goals

EDGE-PROFILE-X1 does not attempt:

- production route replacement;
- top-edge or horizontal-edge chamfers;
- profile-stack chamfers;
- three-edge/corner chamfers;
- AirEdgeSweep route changes;
- STEP exporter/importer changes;
- Boolean core changes;
- triangle migration;
- sketch solver work;
- clipping engine work;
- NURBS/freeform support.

## Recommendation

The lab converges on `profile-chamfer-extrude-ready-for-production-evaluation` for the supported rectangular-prism vertical-edge case. Recommended next milestones:

1. **EDGE-PROFILE-V1**: production-adjacent profile chamfer emitter/admissibility work for history-known line-only prism edges.
2. **EDGE-PROFILE-X2**: profile-stack top-edge chamfer lab for edges perpendicular to the extrusion axis.
