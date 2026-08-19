# P2-SURFACE-MESH-IR-M2

M2 proves the first nontrivial exact-to-mesh path: an exact rectangular plate with a centered cylindrical ThroughAll hole, from Firmament through `SurfaceMeshDocument`, validated `TriangleMesh`, and binary STL.

## M1 blocker audit and M2 design

M1 already planned every B-rep edge once and supported Plane/Cylinder patches, but `TryBuildPlanePatch` rejected any face with more than one loop. A top or bottom cap with an inner circular trim therefore fell back before it could share its ring with the cylindrical wall. M1 also only produced per-face display patches: it had no final global triangle topology gate or exporter boundary.

M2 keeps the IR rather than replacing it. `SurfaceMeshTrimLoop` stores each loop identity, the shared vertex references, Plane-local `(u,v)` points, signed orientation, and whether it is an inner trim. The bounded planner accepts one rectangular outer loop plus one circular inner loop. It creates two deterministic quad bands: a coarse outer band and a graded inner band. The plane remains unrefined for curvature—the one band exists only for trim conformity and bounded aspect-ratio grading. Arbitrary polygons, non-rectangular outer loops, and multiple holes deliberately remain legacy work.

`BoundaryPolygonCell` remains the bounded exceptional-cell representation for planar side faces and ordinary non-rectangular simple faces. It lowers by a stable fan only at the final triangle stage; M2 does not claim a universal all-quad mesher.

## Shared boundaries and directional refinement

Each B-rep edge has one `SharedEdgeSamplePlan`; closed circles alias their final sample to their initial vertex ID. Cap trim loops and cylindrical strips read that exact ordered ring (reversing only per coedge use). There is no independently regenerated near-coincident circle and no welding stage.

Circle density is driven by exact chordal and normal bounds. The cylinder emits one axial row: curvature is angular, not axial. Plane interiors are never refined for geometric error. For the supported plate, line boundaries are split into nine shared segments only because their cap bands need matching side-face boundaries; a plain box still remains a single coarse quad per face.

## Validation and export

`SurfaceMeshIrValidator` now checks unique vertices, shared-edge sample/use contracts, closed-ring endpoint identity, cell references, and zero-area cells before lowering. `TriangleMeshValidator` then checks triangle references, duplicates, zero-area triangles, edge incidence, connectedness, and signed outward volume. Export is refused unless the mesh is closed and outward-oriented.

`TriangleMesh` is triangle-only and has no B-rep knowledge. It carries exact support-derived normals and hard-edge metadata (Plane/Cylinder support changes). `BinaryStlExporter` consumes only this validated final mesh.

CLI:

```text
aetheris mesh plate.firmament --format stl
aetheris mesh fixtures/Regression/Hole/valid/hole-x4-shaft-through.valid.firmfixture --output plate.stl --debug-ir plate.ir.json --json
```

Runtime LOD is explicitly not part of this design. The intended optimization is to compile one economical mesh and omit unnecessary flat-face polygons.

## Golden evidence

The canonical `hole-x4-shaft-through` Firmament fixture (100 x 60 x 12 mm plate, radius 4 mm hole) exported successfully through the new path:

| Measure | Value |
| --- | ---: |
| IR patches | 7 (2 trimmed caps, 4 sides, 1 cylindrical wall) |
| IR cells | 184 (180 quads, 4 boundary polygons) |
| Circular samples / axial cylinder rows | 36 / 1 |
| Final triangles | 504 |
| Final mesh vertices | 262 |
| Max exact chord deviation | 0.01522120763301782 mm |
| Normal deviation | 0 (analytic Plane/Cylinder normals) |
| Watertight / non-manifold edges / cracks | true / 0 / 0 |
| Deterministic mesh hash | `f505aa921fc9d890caaf309670672e92ffb329834ffadb4538f622effcea8663` |
| Approximate IR buffer bytes | bounded by vertex/cell arrays; no allocation tuning claim |
| STL bytes / SHA-256 | 25,284 / `fcc034d64252bc4f275c9c02f8039b645ac17e886d421fec3342f7a31d3e1a5a` |

The local IR dump, STL, and FreeCAD smoke script under `artifacts/local/evidence/preview2/surface-mesh-ir-m2/` are reproducible evidence. FreeCAD 1.0.2 imported the STL as one solid mesh: 504 facets, 252 points, `solid=True`, 100 x 60 x 12 mm bounds, and volume 72,200.078125 mm³ (binary STL float rounding). No repair/healing was requested.

The legacy tessellator is retained as a focused test oracle; the through-hole test asserts the new 504-triangle final mesh is strictly smaller on the same exact body. A timing/allocation benchmark and screenshot-based Cadmata capture are deferred because Cadmata currently consumes STEP/display packets rather than the new validated `TriangleMesh` export seam. This is an explicit integration limitation, not a hidden fallback.

The trim rings are cyclically phase-aligned before band construction. Orientation is insufficient by itself because the rectangular loop starts at a B-rep corner while the circular loop starts on the curve's reference axis; pairing those raw indices twists and overlaps the annular strip. M2 selects the stable cyclic shift with minimum total spoke length, and a regression checks that no cap triangle centroid enters the circular trim.

## Remaining limits and next milestone

- One rectangular outer loop plus one circular inner loop is the supported M2 trim family; multiple holes, arbitrary clipped cells, Cone/Sphere/Torus, and general planar polygon meshing remain legacy/M3 work.
- The line-boundary conformity rule is intentionally bounded and should evolve into per-edge compatible sampling rather than a global grid.
- Cadmata has not yet been migrated to render the validated final mesh directly; its established STEP display route remains intact.
- No runtime LOD, streaming refinement, mesh repair, glTF, 3MF, or volumetric meshing was introduced.

M3 has now added full circular-trim Cone strips, cube-chart Sphere primitives,
and doubly-periodic Torus primitives to the common IR. See
[surface-mesh-ir-m3.md](surface-mesh-ir-m3.md) for the supported boundary and
trim subset; general conic trim bands and HexBolt remain explicit follow-up work.
