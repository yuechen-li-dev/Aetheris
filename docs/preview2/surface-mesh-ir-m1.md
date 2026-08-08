# P2-SURFACE-MESH-IR-M1

## Audit before migration

The previous display path is in `Aetheris.Kernel.Core/Brep/Tessellation` (about 7,128 C# lines). Its 4,237-line `BrepDisplayTessellator` owns face dispatch, analytic sampling, loop flattening, trim projection, surface-family special cases, triangulation, normals, and edge display polylines. Supporting helpers contain planar loop classification (231 lines), planar polygon triangulation (933), trimmed UV tessellation (449), UV trim extraction/masking (342), curve sampling (169), and bounded display fallback/budgets (228). It dispatches Plane, Cylinder, Cone, Sphere, Torus, and B-spline faces directly to triangle patches. Display-facing `DisplayFaceMeshPatch` is triangle-native; Cadmata and the browser mapper consume those triangles. B-rep mass properties are separately exact/verification code and are not fed by display meshes.

| Responsibility | Current ownership before M1 |
| --- | --- |
| Boundary sampling | `CurveSampler`, per-face flattening, and `TessellateEdge` |
| Surface-domain meshing/refinement | `BrepDisplayTessellator`, `TrimmedSurfaceTessellator` |
| Stitching | implicit coordinate agreement and display edge polylines |
| Triangulation | planar triangulator and individual analytic face routines |
| Attributes | each face routine emits normals |
| Render/export adaptation | `DisplayTessellationResult`, server DTO, Cadmata client mapper |

The main hotspots are planar triangulation with holes, UV trim extraction/classification, and family-specific curved trim recovery. The old path remains the migration oracle/fallback.

## M1 surface mesh layer

The new typed IR is `SurfaceMeshDocument`, with `SurfacePatch`, `SurfaceMeshVertex`, `SharedEdgeSamplePlan`, `FaceBoundaryUse`, `SurfaceMeshSupport`, and explicit `QuadCell`, `TriangleCell`, `BoundaryPolygonCell`, and `SingularCell` records. `SurfaceMeshPolicy` carries chordal/normal bounds, refinement depth, boundary budget, and downstream intent (`Presentation`, `Manufacturing`, `Fea`). It is derived-only: no API uses it for B-rep topology, volume, or mass authority.

`SurfaceMeshIrTessellator` plans all line/circle B-rep edges first, in sorted edge order. Endpoint vertex identities are reused, each face use records its coedge orientation, and both incident patches read the same ordered sample sequence. A reversed coedge reads that sequence backwards; no coordinate sorting is used. The debug representation is `SurfaceMeshIrDebug.ToJson(document)`.

For Plane M1, one-loop four-line faces become a single ordered quad; circular/simple non-rectangular one-loop faces are retained as a boundary polygon and only fanned during lowering. Plane geometric error is zero in its interior. For Cylinder M1, a full-turn pair of sampled circular trims produces an angular-by-axial quad strip. Its periodic seam is explicit (`HasPeriodicUSeam`); the closing circle sample aliases the start vertex and is not emitted as an independently owned strip vertex. Angular density is selected from exact circle chordal and normal bounds; axial curvature is zero, so no global axial over-refinement occurs.

Quad lowering is a separate pass. It chooses the shorter diagonal, breaking ties as 0--2, then emits stable patch/cell order. Boundary polygons are deterministic fans. Plane and cylinder normals are evaluated from their exact supports, never averaged from adjacent triangles; hard boundaries remain split per patch. Validation rejects invalid/repeated cell references before lowering. The M1 lowerer reports `DisplayMeshPipeline.SurfaceMeshIr` and `SurfaceMeshMetrics` including cell/quad/triangle counts, edge range, maximum exact chord deviation, crack count, and a deterministic SHA-256 hash.

## Current capability and evidence

The SurfaceMeshIR path is explicitly selectable through `BrepDisplayTessellator.TessellateSurfaceMeshIr` for closed Plane/Cylinder bodies whose trims are line/circle loops supported by M1; the established default route intentionally remains legacy during this migration step. A box materializes as six quad patches with twelve shared line plans. A cylinder materializes as two boundary-polygon caps plus a periodic all-quad side strip with two shared circular plans. Existing display tessellation tests (33) and focused IR tests (5) pass. The focused tests cover line plans, circle plans, shared reuse, opposite consumption orientation through `FaceBoundaryUse`, box quads, cylinder seam/quad strips, exact radial normals, deterministic lowering/hash, legacy/new route identity, and IR validation.

The first refinement hook is represented by per-cell refinement metadata and policy bounds; M1's implemented curved refinement is the deterministic angular edge/strip density selection. General in-patch 1-to-4 and directional cell subdivision are deliberately deferred rather than pretending that boundary resampling is a full adaptive refinement system.

On the Debug `net10.0` build, a 100-iteration in-process comparison (not a benchmark claim) produced the following directional evidence. Box has the same 12 output triangles in both paths; the cylinder has 144 lowered triangles in IR versus 3,884 in legacy because its exact circular boundaries and zero axial curvature are retained instead of forcing a dense triangle-native route. The cylinder's measured exact chord bound was `0.00570795`, below the default `0.05` target. Allocation measurements were not collected in M1.

| Fixture | Legacy mean | IR mean | Legacy triangles | IR triangles | IR hash prefix |
| --- | ---: | ---: | ---: | ---: | --- |
| Box 3×2×1 | 0.318 ms | 0.484 ms | 12 | 12 | `7d1692acf07d5d0e` |
| Cylinder r1.5 h4 | 1.187 ms | 0.319 ms | 3,884 | 144 | `70113ee8662b6c33` |

Multi-loop planar trims (including the through-hole plate fixture), arbitrary clipped boundary cells, Cone, Sphere, Torus, and B-spline patches remain on `LegacyTessellator`. That fallback is intentional and observable through `meshPipeline`; it is not a new mesh authority. Therefore this M1 slice should be treated as an architectural migration baseline, not a global replacement or an FEA/printing certification. Cadmata remains triangle-facing and receives the lowered `DisplayFaceMeshPatch` contract unchanged.

Recommended next milestone: **P2-SURFACE-MESH-IR-M2 — Cone/Sphere/Torus and harder trimmed analytic patches**, beginning with multi-loop planar clipping/through-hole ownership and true deterministic quad subdivision.

## Migration status update

M2 subsequently added the multi-loop through-hole plate and validated STL path.
M3 now extends the common IR to full circular-trim Cone strips, six-chart Sphere
primitives, and doubly-periodic Torus primitives. See
[surface-mesh-ir-m2.md](surface-mesh-ir-m2.md) and
[surface-mesh-ir-m3.md](surface-mesh-ir-m3.md); general conic trim bands and
arbitrary trimmed curved patches remain deliberate legacy fallbacks.
