# Viewport meshing of curved faces from their edges

Follow-up to `viewport-tessellation-conformance.md`. That change closed seams where faces happened to sample edges
alike; the remaining daylight was on faces whose meshers sampled their own boundaries (spline-trimmed cylinder fillets,
cone sectors at an apex, tori, spheres, B-spline faces).

## Change

`EdgeBoundTessellator.cs`, presentation only (`DisplayTessellationOptions.Presentation`), tried first for cylinder
(after the revolved band), cone (after the band), sphere, torus and B-spline faces. Declines fall through to the
existing paths unchanged.

1. **Boundary = edge samples.** Each loop is the coedges' canonical samples (`TrySampleCoedgeForTrimEvaluation`), at
   their exact 3D positions; seam edges are cut out and the pieces rejoined. Boundary edges are never split, so every
   neighbour sampling the same edges shares those vertices.
2. **One plane chart per face.** Cylinders: developed (exact), compressed 4× along the generators so triangles run along
   them. Cones: developed about the apex, which becomes one point (apex sectors work). Faces that go once around a
   periodic direction between two rings: a conformal annulus, `r = exp(t(v))`, angle = u. Otherwise metric-affine at
   the parameter centre. Spheres are re-parameterized about an axis perpendicular to the face's mean direction so the
   poles are off the face. Loops are unwrapped across periods; a loop through the apex skips the undefined step.
3. **Triangulate, Delaunay, refine.** Ear clipping, collinear vertices it drops reinserted on their hull edges, Lawson
   flips, then interior edges split at their chart midpoints (longest first) while an interior edge's midpoint sags
   past the chord tolerance, its normals turn more than the angular tolerance, or an interior triangle's centroid sags.
   Two relaxation passes move interior vertices toward their neighbours' mean where nothing turns over.
4. **Checks, else decline.** Self-crossing loops, an area mismatch after triangulation, a triangle facing against the
   surface normal, and on closed surfaces in an open chart a loop running against the face sense (the complement).

## Evidence

Visible daylight (open edges > 1 µm from any other boundary), viewport triangles and `TessellateBoundedPartial` time,
NIST at `ForViewport`, against the previous commit (band + conformer):

| Model | Gaps before | Gaps after | Triangles before | Triangles after | ms before | ms after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| CTC-01 | 46 | 0 | 18.1k | 4.4k | 535 | 381 |
| CTC-02 | 2122 | 2 | 219k | 52k | 1039 | 1557 |
| CTC-03 | 0 | 0 | 3.6k | 3.6k | 32 | 40 |
| CTC-04 | 34 | 0 | 123k | 56k | 364 | 444 |
| CTC-05 | 189 | 0 | 56k | 36k | 135 | 269 |
| FTC-06 | 71 | 11 | 49k | 48k | 104 | 276 |
| FTC-07 | 1813 | 50 | 243k | 65k | 622 | 463 |
| FTC-08 | 0 | 0 | 57k | 16k | 171 | 280 |
| FTC-09 | 0 | 0 | 6.6k | 6.3k | 95 | 77 |
| FTC-10 | 5739 | 3 | 393k | 108k | 1141 | 1050 |
| FTC-11 | 630 | 0 | 5.7k | 9.5k | 52 | 291 |
| STC-06 | 2 | 2 | 26k | 33k | 72 | 345 |
| STC-07 | 726 | 9 | 184k | 49k | 368 | 395 |
| STC-08 | 22 | 3 | 61k | 16k | 140 | 179 |
| STC-09 | 0 | 0 | 5.9k | 5.9k | 63 | 83 |
| STC-10 | 183 | 0 | 109k | 131k | 271 | 924 |

Daylight is down from 11,577 open segments to 80; triangles from 1.56M to 0.64M. Meshing is slower on small parts
(per-face refinement passes in managed code), at most ~0.65 s more on STC-10. Rendered samples (fillet cylinders, apex
cone sectors, tori, sphere corners) show graded Delaunay meshes running along cylinder generators.

Remaining: FTC-07's cone faces (imported vertices ~0.28 mm off their spline edges' ends; both neighbours use the
vertex, the drawn edge does not), a few spheres whose loop runs against the face sense or whose spline trims sit more
than 2% of the radius off the surface, and planar faces next to declined faces.

`DisplayMeshConformanceTests` ceilings tightened (CTC-04 60 → 4, STC-08 40 → 8; CTC-02 and FTC-10 added) and a
triangle-budget test added for FTC-10. The server test for a lone bounded spline face now expects the tessellator patch: the edge-bound mesh beats the soft UV scaffold, which is rejected as lower fidelity.

## Verdict

Success: the daylight between curved faces is gone on the NIST set and the viewport meshes are 2.5× lighter overall. Next, if time matters: the refinement recomputes every interior edge each pass; a work queue of edges touched by the last splits would cut the small-part overhead.
