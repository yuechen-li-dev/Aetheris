# EDGE-X2 — Concave planar chamfer AirEdgeSweep patch lab

## Purpose and scope
This milestone adds a **lab-only constructive proof** for `AirChamfer` as an `AirEdgeSweep`-style edge patch over a single concave planar edge. It does not modify production body-editing behavior.

References:
- `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`

## Why concave planar chamfer first
EDGE-A0 and EDGE-X1 concluded that the lowest-risk first lane is a local concave planar chamfer patch with no edge chains/corners and no migration of legacy routes.

## Local coordinate model
Canonical case:
- shared edge on Z axis from `(0,0,-h/2)` to `(0,0,+h/2)`
- Face A normal `+X`
- Face B normal `+Y`
- chamfer distance `d > 0`

## Offset-curve construction
Given edge direction `e` and planar face normals:
- offset-A direction: `normalize(cross(e, nA))`
- offset-B direction: `normalize(cross(nB, e))`

Offsets are applied to both edge endpoints at distance `d`, yielding two offset lines (`A0-A1`, `B0-B1`).

## Ruled/planar patch construction
The patch artifact is a deterministic quad:
`[A0, A1, B1, B0]`, with boundary edges `(0-1,1-2,2-3,3-0)`.
Plane normal is derived from cross products of quad spans, and area is reported.

## Test cases and results
Covered cases:
- valid canonical `h=10, d=1`
- valid canonical `h=10, d=2`
- valid canonical `h=7.5, d=1`
- invalid: `d<=0`
- invalid: non-finite `d`
- invalid: zero edge length
- invalid: non-finite edge endpoint
- invalid: degenerate/parallel face adjacency

Result: constructive proof succeeded for valid canonical cases, with deterministic topology/artifact rows and deterministic diagnostics.

## Topology/artifact findings
For valid canonical rows:
- patch produced
- vertices: 4
- edges: 4
- faces: 1
- planar faces: 1
- boundary loops: 1
- coedges: 4

## STEP/export status
`edge-x2-step-smoke-deferred:open-patch-export-unsupported` is emitted in this lab.
No `Step242Exporter` changes were made.

## No-3D-Boolean guarantee
Each run emits `edge-x2-no-3d-boolean-used`.
No boolean subtraction or 3D boolean fallback is used in the candidate path.

## Invalid-case handling
Invalid input is rejected before patch construction with explicit diagnostics:
- `edge-x2-invalid-distance-rejected`
- `edge-x2-invalid-edge-rejected`
- `edge-x2-invalid-face-adjacency-rejected`

## Non-goals (preserved)
- no production chamfer behavior change
- no convex replacement
- no fillet implementation
- no edge chains/corner chains
- no STEP importer/exporter changes
- no boolean core changes
- no sketch solver/clipping/NURBS/freeform additions

## Recommendation for next milestone
Recommended next step: **EDGE-X2.1 non-orthogonal concave patch lab** to expand admissible planar adjacency while keeping local patch-only scope.


> Note: EDGE-X2.1 adds a policy scaffold lane before any geometry expansion beyond canonical concave planar patch.
