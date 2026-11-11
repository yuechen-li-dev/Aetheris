# Viewport tessellation: closed seams on circular edges, structured hole walls, size-scaled detail

## Problem

Helios showed slivers of daylight along circular edges (hole rims, countersinks, fillets) and visibly faceted
cylinders. Every face sampled its own boundary: a planar cap sampled its circle with `CurveSampler`, the cylinder wall
with its own angular grid, the drawn edge with a third rule (`TessellateEdge` ignored the options for circles). Where
counts or phases differed the two sides of an edge did not share vertices. The browser runtime also used a fixed
budget (`π/16`, 0.3 mm chord, at most 64 segments) regardless of part size.

## Changes

All behind `DisplayTessellationOptions.Presentation`, which `ForViewport` sets. Analyses that read per-face meshes
(blend recognition, mesh mass properties, orientation resolution, picking) keep the existing meshing: their
calibrations (e.g. the STC-06 volume window, rounded-solid volume equality) are tied to it, and they do not need
watertight seams.

1. **Edge-first sampling.** Presentation edges sample circles with the mesh's own tolerances and refine B-spline edges
   until each chord is within the chord tolerance and turn limit (`CurveSampler.SampleBSpline(…, options)`).
2. **Revolved bands** (`AnalyticBandTessellator.cs`). A cylinder or cone face bounded by two coaxial circular rims —
   whole circles, or arcs closed by two straight generators — is one row of quads between the rims. Columns are the
   union of both rims' edge samples, and rim vertices are those samples verbatim, so caps and neighbours that sample
   the same edges meet exactly. Generators are straight on both surfaces, so a single row is exact along them; normals
   are analytic (including the nappe behind a cone's apex). Elliptic or spline trims, apex rims and partial coverage
   fall through to the existing paths.
3. **Edge conformance** (`DisplayEdgeConformer.cs`). After all faces are meshed, each B-rep edge collects the boundary
   vertices any adjacent face placed on it (within 1.5× chord) plus its drawn samples. Each adjacent face's boundary
   triangles are fanned at the vertices they lack, from the opposite corner, so no vertex is placed off the surface;
   inserted vertices take the support's exact normal where the face is analytic. Vertices two faces placed within a
   weld distance of each other take one position. Faces are never moved.
4. **Size-scaled detail.** `ForViewport`: chord = diagonal / 600 (0.005–0.25 mm), at most 11.25° per segment, up to 1024
   segments. Small holes stay light, large bores get more columns. The browser runtime and the PMI projection now use
   it; a view-dependent LoD was not needed at these budgets.

## Measurements

Visible daylight = open triangle edges whose midpoint is more than 1 µm from any other boundary segment, NIST models at
`ForViewport`:

| Model | Before | After |
| --- | ---: | ---: |
| CTC-01 | 1160 | 46 |
| CTC-02 | 20756 | 2122 |
| CTC-03 | 1081 | 0 |
| CTC-04 | 11650 | 34 |
| CTC-05 | 2321 | 189 |
| FTC-06 | 2894 | 71 |
| FTC-07 | 11633 | 1797 |
| FTC-08 | 2398 | 0 |
| FTC-10 | 11357 | 5741 |
| STC-06 | 868 | 2 |
| STC-07 | 10591 | 611 |
| STC-08 | 3846 | 22 |
| STC-09 | 1182 | 0 |
| STC-10 | 4411 | 183 |

(The "after" column was measured before the per-span minimum change was reverted; the regression test's figures for
CTC-03/04, FTC-08, STC-08/09 are the final ones: 0, 34, 0, 22, 0.) FTC-11 is slightly worse (293 → ~630, all under
0.08 mm). The remaining slivers sit on spline, torus and sphere faces whose own meshes miss their edges by more than the
chord tolerance (FTC-10's cylinder fillets between spline edges are the largest share); FTC-08 `-tg` is tessellated
geometry and out of scope. Native Firmament holes, counterbores and countersinks mesh closed (0 open edges).

## Tests

`DisplayMeshConformanceTests`: native machined block and countersink close with every hole wall a single band; NIST
ceilings above. `KernelApiIntegrationTests.DisplayPrepare_MeshesACylinderWallBetweenItsRims` replaces the expected
failure on `cylinder_hole_unsupported.step`: the band meshes the wall that used to be skipped with a warning.

## Not done

- Structured meshing of tori, spheres and spline faces against their edges (the remaining slivers).
- Analysis consumers still use the per-face meshing; moving them to the presentation mesh needs their calibrations
  re-derived.
