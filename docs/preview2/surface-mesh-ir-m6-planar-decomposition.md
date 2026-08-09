# SurfaceMeshIR M6: planar domain decomposition

M6 treats a Plane as a topology problem. A flat support has zero curvature, so
interior refinement cannot improve geometric approximation. Shared trim samples
remain authoritative for watertight contact with adjacent analytic faces; all
other planar cells are selected in the plane's exact local `(u,v)` frame.

## PlanarDomain

`PlanarDomain` records an outer loop, inner feature loops, Plane-local boundary
coordinates, source B-rep boundary spans, concave vertices, and the chosen
planner path. `SurfaceMeshTrimLoop.BoundarySpans` preserves source edge IDs,
curve family, direction, and sample count, so a sampled circular edge remains a
single source feature rather than a set of unrelated corners. The debug IR now
exposes those spans and `PlanarPlannerPath` for every planar patch.

The bounded planner has three paths:

- A rectangular no-hole face remains one quad.
- A convex no-hole face remains one safe OBJ polygon.
- Concave and feature-loop domains are triangulated deterministically in local
  2D only long enough to establish a valid partition. Authoritative collinear
  boundary samples are reinserted, then edge-adjacent cells are greedily merged
  when their union is simple and convex (at most twelve vertices).

The merge score is deterministic: it prefers quads, then smaller polygon order,
then more compact cells, with a stable vertex-ID tie-break. Its adjacency map
makes the pass linear in mesh edges per bounded pass, not quadratic in cells.

This is topology optimization, not geometric approximation. The policy does
not use chordal error to add planar interior rows.

## Feature treatment and lowering

Inner loops participate as explicit planar-domain obstacles. Circle-heavy
through-hole caps retain the existing equal-count annular quad-band path; the
generic multiple-hole and slot route keeps every boundary sample, produces a
deterministic convex partition, and removes the arbitrary global triangle fan.
Slot provenance is classified from its straight and circular source spans.

M6 does **not** yet synthesize an offset ring or dedicated bridge for every
generic hole/slot. The remaining weakest cases are the largest multi-feature
faces (CTC faces 98 and 3), where a future band/bridge phase can make the
partition more locally feature-shaped. The current pass is intentionally
bounded: it removes gratuitous diagonals without moving shared boundaries or
introducing a fragile general remesher.

`BoundaryPolygonCell` is only emitted for convex cells. OBJ preserves those
polygons. Triangle-only consumers use deterministic plane-local ear clipping;
there is no centroid fan and no importer-selected triangulation. Quads retain
their deterministic shortest-diagonal lowering. Plane normals remain exact and
hard/smooth boundary rules are unchanged.

## CTC-01 audit and result

All 56 CTC-01 planar faces are included in
[`ctc-01-metrics.json`](evidence/surface-mesh-ir-m6/ctc-01-metrics.json).
It contains per-face area, loop counts/types, concavity, cell mix, internal-edge
metrics, aspect proxy, triangle-fan count, and planner path. The pre-M6 worst
faces were 98 (993 planar cells) and 3 (598); M6 reduces them to 494 and 290.
Faces 15, 48, 50, 55, and 56 are the next feature-loop cases to inspect.

| Metric | M5 | M6 |
|---|---:|---:|
| Total cells | 11,130 | 9,762 |
| Quads | 8,321 | 8,363 |
| Triangles | 2,766 | 1,120 |
| Safe n-gons | 43 | 279 |
| Quad percentage | 74.76% | 85.67% |
| Planar cells | 2,524 | 1,156 |
| Planar quads | 39 | 81 |
| Planar triangles | 2,442 | 796 |
| Planar n-gons | 43 | 279 |
| Mean cells / planar face | 45.07 | 20.64 |
| Maximum planar-face cells | 993 | 494 |
| Total internal planar edge length | 235,122.126 | 110,255.639 |
| Longest internal planar diagonal | 447.312 | 442.318 |
| Triangle-fan vertices | 178 | 75 |

The M6 OBJ has 9,762 polygons and the deterministically lowered STL has
20,888 triangles. Both preserve the 117-patch, zero-fallback analytic coverage:
the STL is watertight, connected, outward-oriented, crack-free, non-manifold
free, duplicate-free, and has zero zero-area triangles. A warm CTC planner run
took 2,324.886 ms; plane merge uses bounded edge-adjacency passes.

Focused regression coverage includes the existing through-hole plate (retained
annular quad bands), the new deterministic two-circular-feature domain fixture,
and the CTC spline/curved-support regression. No Fusion/Blender capture was
automated in this run; the OBJ evidence is supplied for manual wireframe review.

## Reproduction

```text
dotnet run --project Aetheris.CLI -- mesh testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --format obj --output docs/preview2/evidence/surface-mesh-ir-m6/ctc-01.obj --debug-ir docs/preview2/evidence/surface-mesh-ir-m6/ctc-01-surface-mesh-ir.json --json
dotnet run --project Aetheris.CLI -- mesh testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --format stl --output docs/preview2/evidence/surface-mesh-ir-m6/ctc-01.stl --json
```
