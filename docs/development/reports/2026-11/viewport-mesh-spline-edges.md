# Viewport mesh: B-spline edges on planar faces

## What changed

`SamplePlanarBSpline` (the planar face path) sampled every B-spline edge with the fixed, control-point-driven count
(`CurveSampler.SampleBSpline(spline, interval)`: 2 × control points, 8 to 128 segments, never refined). The drawn edge
polyline and every curved face that meshes from its edges use the overload refined to the mesh's chord and angle
tolerances. A plane bounded by a spline therefore had its own coarser boundary: cut across the curve between its
samples (the jaggy rim) and shared no vertices with its neighbour (the gaps). Presentation meshes now sample planar
spline edges with the refined overload, as `TessellateEdge` does, which is the same treatment circular edges got in
`2213054b`. Ellipses were already consistent: the planar path subdivides the same trim with the same segment count.

## Evidence

Probe over 16 NIST models and 4 Firmament parts with ellipse and spline edges, viewport options. Open edges are triangle
edges used once after welding; daylight is an open edge more than 1 µm from any other face's boundary.

| Model | Before: open / daylight (widest) | After |
| --- | --- | --- |
| lamp-shade-loft (lofted B-spline wall, planar caps) | 117 / 117 (0.413 mm) | 0 / 0 |
| other 19 models | unchanged | unchanged |

Edge polylines themselves were within tolerance on every model (worst ellipse 0.237 mm, spline 0.2 mm against a
0.25 mm chord), so the jaggy look was the faces, not the edges. Regression test: `lamp-shade-loft` and
`closed-ellipse-extrusion` join `ViewportMeshIsClosedAcrossCurvedEdges` (zero open edges).

## Not fixed: spline edges on the general UV-chart path

Remaining open edges on spline edges are in NIST models whose sphere and cylinder faces the edge-bound mesher declines,
so they mesh through `TrimmedSurfaceTessellator` / `BoundaryConformingTrimTessellator`. Measured on ftc_07 (the only
one with visible daylight on spline edges, up to 0.226 mm; stc_07, stc_10, ctc_02 and ftc_10 have hairline mismatches):

1. **Sphere pcurve resample.** The sphere's general path replaces a coedge's shared samples with 32 or more of its own
   along the pcurve (sphere face 31 keeps 2 of spline edge 122's 17 samples, its endpoints).
2. **Boundary re-evaluation.** The conforming mesher re-evaluates every boundary vertex from its chart coordinates,
   moving it onto the surface by the edge-to-surface residual (about 1e-4 mm there), off the neighbour's exact sample.

Tried and reverted, each kept only on measured gain:
- **Inserting a neighbour's vertices by position along the edge** in `DisplayEdgeConformer`. Fixed lamp-shade but
  zigzagged between parallel edges a narrow face apart (stc_10 daylight 0 → 284). The planar sampler fix makes it
  unnecessary.
- **Keeping the shared samples on spheres.** Alone it had no gain: the chart's collinear pass straightened them again.
- **Requiring 3D straightness in `RemoveCollinearVertices`.** Mixed: ftc_06 daylight 11 → 2, but ctc_04 open edges
  0 → 79.

## Verdict

Planar faces with spline edges: fixed, no regressions. The general chart path needs boundary vertices to keep their
input 3D positions through the chart (`BoundaryConformingTrimTessellator` taking the 3D loops alongside the UV ones);
that is a change to every caller of that path, not part of this fix.
