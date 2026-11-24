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

## Follow-up: watertight across imported spline edges

Two more causes, both on imported models, fixed:

1. **Drawn edges ended off their vertices.** `TessellateEdge` evaluated the curve at its trim, which on imported
   models sits up to 3e-5 mm from the B-rep vertex. Faces pin their boundary ends to the vertex, so at a corner the
   faces either side snapped to different edges' ends and missed each other by microns. Drawn edges now end exactly
   on their vertices (`TessellateEdge` wraps the curve sampler and pins the ends, within the chord tolerance).
2. **Chart paths re-evaluate their boundary.** Faces meshed through a surface chart (the sphere's stereographic chart,
   the cylinder and cone UV paths, the conforming trim mesher) place each boundary sample at `evaluate(u, v)`, on the
   surface rather than on the edge: off by the edge's distance from the surface (5e-4 mm on stc_10, up to 0.05 mm on
   ftc_07), beyond the conformer's 1e-3 × tolerance weld. `DisplayEdgeConformer` now first moves each boundary vertex
   onto the nearest sample of the face's own edges, within a fifth of the chord tolerance and a third of the spacing
   to the next sample. This does in one place, for every path, what carrying the 3D samples through each chart would
   have done in seven callers.

Each fix was measured alone and together:
- **Pinned ends without the snap:** stc_07 has 161 open edges, stc_10 51.
- **Snap without pinned ends:** ctc_02 has 64 open edges.
- **The snap's targets:** the first version took the first in-range edge and the second the nearest of all edges.
  Both broke previously matching pairs (corner asymmetry; a line-edge vertex snapping to a nearby curve's sample,
  stc_10 daylight 0 → 4).

| Model | Open edges / daylight before | After |
| --- | --- | --- |
| ctc_02 | 197 / 0 | 0 / 0 |
| ftc_10 | 51 / 3 | 0 / 0 |
| stc_10 | 52 / 0 | 0 / 0 |
| stc_07 | 412 / 9 | 6 / 3 (on lines) |
| ftc_06 | 41 / 11 | 15 / 11 (circles and lines) |
| ftc_07 | 137 / 50 | 60 / 42 |
| all others | unchanged (0, or 3–4 on lines and circles) | |

New test `NistModelsAreWatertight`: nine NIST models at zero open edges. The daylight ceilings are tightened to the
measured values.

Not fixed:
- **ftc_07: a helical spline across a cylinder's seam.** Cylinder face 283 has boundary segments joining samples of
  spline edge 64 up to 63 apart: the edge winds across the seam, the UV loop jumps by 2π, and the chart mesher
  connects across the jump. This is a periodic-seam bug in the chart path, not a sampling one.
- **Off-edge boundary points from refinement.** A few on ftc_07's sphere and cylinder (edges 119 and 120), up to
  0.048 mm off any sample.
- **Sphere pcurve resample.** Kept out: keeping the shared samples on spheres measured neutral (ftc_06 better, ftc_07
  worse) once the snap was in.

