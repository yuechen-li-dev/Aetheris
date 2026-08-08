# P2-SURFACE-MESH-IR-M3

M3 extends the derived `SurfaceMeshDocument` path from Plane/Cylinder to the
currently representable exact analytic supports: Cone, Sphere, and Torus. The
B-rep remains authoritative for topology, trim curves, volume, and mass.
Triangles remain a deterministic lowering/export representation.

## Audit and boundary authority

The legacy `BrepDisplayTessellator` is still the broad fallback. Its curved
paths project trim loops into support UV, normalize periodic seams, and emit
triangles directly. Projection and periodic trim topology are genuine geometric
work; choosing grid diagonals inside the same triangle-native routine is legacy
output complexity. M3 keeps the former in the exact B-rep/IR contract and keeps
cells structured until final lowering.

Each B-rep edge has one `SharedEdgeSamplePlan`, ordered by exact curve parameter
and consumed in coedge orientation. M3 adds bounded deterministic `Hyperbola3`
sampling: parameter intervals split at their midpoint while mid-chord deviation
exceeds policy. Lines remain minimal; circle density remains chordal/normal-error
driven. Hyperbola samples are one shared boundary authority, though arbitrary
cone/plane conic trim-band covering remains an explicit legacy case.

## Support topology

| Support | M3 topology | Seam/normal policy |
| --- | --- | --- |
| Cone | Full circular-trim frusta use one angular × generator quad strip. | Circular trim rings are shared B-rep samples; angular closure is explicit; `ConeSurface.Normal` is used. |
| Sphere | Six cube charts, each a 6 × 6 structured quad grid. | Integer cube-grid coordinates reuse IDs at chart seams; no latitude/longitude pole fan; normal is exact radial support normal. |
| Torus | Major-angle × minor-angle periodic quad grid. | Both wraps reuse grid IDs; B-rep seam plans are injected into matching grid locations; `TorusSurface.Normal` is used. |

Sphere chart seams are implementation seams, not B-rep cracks: they use the
same `SurfaceMeshVertex` IDs and analytic support, so lowering has no duplicate
visible seam or hard edge. Torus resolution is directional: major loops use
`R+r` as chord radius while minor loops use `r`, rather than applying a single
isotropic density rule.

## Validation and evidence

`SurfaceMeshIrValidator` rejects invalid shared boundaries, missing/repeated
cell vertices, and zero-area cells. `TriangleMeshValidator` then verifies
closed incidence, duplicates, zero-area triangles, connectedness, and outward
orientation. Exact support normals now cover Plane, Cylinder, Cone, Sphere,
and Torus; normals are never averaged from triangle faces.

Focused `SurfaceMeshIrTests` prove a 36-cell all-quad Cone strip with watertight
planar caps, a watertight six-chart Sphere with shared seam IDs and unit analytic
normals, and a watertight all-quad doubly-periodic Torus that consumes both B-rep
seam identities. Existing M2 through-hole/STL coverage remains in the suite.

The IR reports patch/cell/quad/exception counts, pre-lowering triangle count,
maximum sampled chord deviation, and a deterministic hash. It does not yet
compute curved-cell aspect/sliver histograms, per-stage timing, or a Cadmata
pre-triangulation debug overlay. Vertices retain parameter coordinates and exact
normal support; future DCC export still needs explicit material-group metadata.

## Limits and next work

The default Cadmata/display route remains the legacy display packet. The new
route is explicitly selectable through `TessellateSurfaceMeshIr`; it succeeds
only for the documented complete subset and otherwise reports fallback rather
than silently mixing pipelines. Binary STL continues to consume validated final
`TriangleMesh`.

M3 does not claim general conic trimming, root-fillet/whole-HexBolt migration,
arbitrary spherical/toroidal trims, runtime LOD, Blender/glTF export, or FEA
volume meshing. The structured surface patches are nevertheless a viable seam
for future boundary-conforming/cut-cell FEA and DCC polygon export without
making either future consumer the present mesh authority.

Recommended next milestone: **P2-SURFACE-MESH-IR-M4** for bounded conic trim
bands (Cone/Plane Hyperbola, root fillet, HexBolt), then
**P2-MESH-DCC-EXPORT-M1** once quad/polygon metadata is complete.
