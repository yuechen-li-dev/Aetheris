# BREP-HOLLOW-RADIAL-CUT-X0

**Executive verdict: Accepted for one local radial through-wall hole.** `BrepHollowRadialCut.Build` constructs an exact-support BRep for one circular cut through the near wall of a `Cylinder<Hollow>` body. It creates one outer opening, one inner opening, and one connecting cutter-cylinder face; the opposite wall is retained. Production STEP export and reimport remain enclosed and manifold. This is a kernel feature, with no Firmament pattern or syntax extension.

## Canonical case and topology

The host uses the existing `ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8)` semantic authority: outer radius 56 mm, inner radius 55.2 mm, height 142 mm, bottom thickness 0.8 mm, open top. One 9 mm diameter hole is centered at axial position 71 mm and angle 0.4 rad. The default finite cutter interval remains on the positive radial side, starts within the cavity, fully crosses both local wall intersections, and ends beyond the outside radius. Admission rejects incomplete extent, an interval reaching the opposite half, tangent/oversize holes, and end collisions.

The result contains **6 faces, 11 edges, 8 vertices, 9 loops, and 22 coedges**. There are 3 analytic cylinders (outer, inner, cutter) and 3 planes (outside bottom, inside bottom, top annulus). Each host cylinder has one opening loop with two certified B-spline edges. The one cutter wall face joins the four intersection edges and uses a single geometric seam edge twice with distinct periodic UV uses. Each edge has two adjacent face uses. The opening vertices have positive coordinate along the chosen radial axis; no opposite-side branch is constructed. Stable feature-local names are `RadialHole`, `RadialHole.OuterOpening`, `RadialHole.InnerOpening`, and `RadialHole.Wall`; `TopologyMap` resolves those names to this realization's loop, edge, and face IDs without making the numeric IDs the feature identity.

The host seam is placed a quarter turn from the opening, so a hole requested at the authored +X seam has one loop with **zero host seam splits**. There is one cutter seam edge with two periodic UV uses. The two exact intersection authorities each contribute one positive branch. Angles normalize modulo 2π; the 0 and 2π cases export byte-identical STEP. This is deterministic seam relocation, not duplicated `U=0`/`U=2π` topology. Face senses are assigned during constructive assembly. The STEP analyzer reports six derived face orientations agreeing with source; its one global shell flip also occurs on the earlier solid CrossHole STEP path, and no new orientation-repair pass was added.

## Exact geometry and certification

Both loops use the existing centerline-crossing, perpendicular cylinder-cylinder intersection authority with its **positive cutter-axis branch**: once at radius 56 mm and once at radius 55.2 mm. Each exact curve is realized as a qualified finite cubic B-spline, then given host and cutter pcurves by the existing adaptive realizer. Production topology does not use polylines, sampled mesh edges, voxels, or generic subtraction.

| Canonical bound | Outer | Inner |
| --- | ---: | ---: |
| 3D realized curve deviation | 9.8753 × 10⁻⁸ mm | 9.8828 × 10⁻⁸ mm |
| Host pcurve edge mismatch | 2.4091 × 10⁻⁷ mm | 2.4117 × 10⁻⁷ mm |
| Cutter pcurve edge mismatch | 4.5383 × 10⁻⁷ mm | 4.5920 × 10⁻⁷ mm |
| Cubic spans | 128 | 128 |

`BrepBindingValidator`, `BrepPcurveValidator` (every coedge), and `BrepExportPreflight` pass. The source and imported bodies have enclosed, consistently oriented shells and positive signed volume. CLI STEP analysis reports one enclosed-manifold body, the original 112 × 112 × 142 mm bounds, 6 faces, 11 edges, 8 vertices, 3 cylinders, 3 planes, 22 pcurves, and 3 seam pcurve pairs. The STEP file is 417,295 bytes.

An independent cross-sectional integral for the drilled region gives approximately **50.94 mm³** removed. The higher-resolution diagnostic triangulation estimates a **59.22 mm³** decrease, but its conservative error bounds are about 2,370 mm³ per body, so it is a trend check rather than an authoritative volume certificate. The default coarse triangulation exaggerates the difference and must not be used as a precise mass claim.

## Display and local artifacts

The supported BRep display tessellator returns nonempty patches for all six faces. The [shaded PNG](../../artifacts/local/hollow-radial-cut-x0/shaded.png) is a diagnostic raster of that display tessellation; it shows the one front-wall opening and an uncut opposite wall. The [wireframe PNG](../../artifacts/local/hollow-radial-cut-x0/wireframe.png) comes from the CLI's trim-aware STEP wireframe. The exact [STEP](../../artifacts/local/hollow-radial-cut-x0/single-hole.step), [SVG](../../artifacts/local/hollow-radial-cut-x0/wireframe.svg), and [analysis JSON](../../artifacts/local/hollow-radial-cut-x0/analysis.json) are local generated artifacts. SurfaceMeshIR still rejects trim topology on a cylindrical face in the `aetheris mesh` command; that path was not changed or used as a geometry fallback.

Reproduce the STEP and display OBJ by setting `AETHERIS_HOLLOW_RADIAL_CUT_STEP_OUTPUT` and `AETHERIS_HOLLOW_RADIAL_CUT_DISPLAY_OBJ_OUTPUT` to absolute paths, then running `dotnet test Aetheris.Kernel.Core.Tests -c Release --filter FullyQualifiedName~CanonicalLocalHole_IsManifoldAndRoundTrips`. Run `aetheris analyze` and `aetheris wireframe` on the STEP. The local `render_display.py` and `raster_wireframe.py` rasterize display output only; they do not construct or modify geometry.

## One-hole performance

One representative Release test run on this machine measured:

| Phase | Time |
| --- | ---: |
| Exact outer + inner intersections | 1.05 ms |
| Certified curve realization | 4.16 ms |
| Host + cutter pcurve realization | 3.87 ms |
| Topology and binding construction | 4.75 ms |
| Binding, pcurve, and export preflight validation | 20.90 ms |
| Complete `Build` including setup | 37.63 ms |
| BRep display tessellation | 725.79 ms |
| STEP export | 37.62 ms |
| STEP reimport | 516.94 ms |

These are single-run wall-clock observations, not throughput benchmarks. The current one-hole curve uses 128 cubic spans on each wall. No high-count optimization or patterning was attempted.

## Regression qualification

Focused Core tests pass for this feature, solid CrossHole, cylinder-cylinder intersection, and Hollow construction (36/36). The Core fast lane passes 970/970. The serial full solution gate completed with 7 failures: 2 CLI tests and 5 CTC03 SheetMetal tests; Core passed 1103/1103 and Firmament passed 1642/1642. The same seven tests fail with identical assertions on an isolated HEAD worktree, confirming they predate this change. They were not modified here. Logs remain under `artifacts/local/hollow-radial-cut-x0/`.
