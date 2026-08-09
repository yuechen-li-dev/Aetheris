# P2-MESH-DCC-EXPORT-M1 — direct SurfaceMeshIR OBJ export

## Scope and format decision

M1 selects Wavefront OBJ. It is the smallest broadly interoperable DCC format
that retains polygons (including quads and n-gons), per-corner normals and
parameter-space coordinates, and deterministic object/group names. PLY has no
standard per-corner UV/normal model; glTF is deliberately triangle-oriented;
and a Blender plug-in would add product-specific scope without improving the
canonical lowering.

`SurfaceMeshIR -> OBJ` is direct. `QuadCell` writes one `f` quad,
`TriangleCell` writes one triangle, and `BoundaryPolygonCell` writes one OBJ
polygon. STL remains a separate, deterministic `SurfaceMeshIR -> TriangleMesh
-> binary STL` lowering.

## Attribute and topology contract

The OBJ `v` table is exactly the deterministic, ID-ordered SurfaceMeshIR
position table. Shared BRep edge samples therefore remain shared positions.
OBJ corners independently refer to `vt` and `vn` tables. This retains a single
geometric vertex while allowing an analytic normal, UV-chart seam, hard crease,
or semantic boundary to have a distinct corner attribute. Normals are evaluated
on Plane, Cylinder, Cone, Sphere, and Torus supports, never polygon-averaged.

For a smooth continuation, equal analytic corner normals are interned into the
same normal entry. At a hard support discontinuity, the two adjacent corners
carry their respective support normals. The exporter does not infer hard edges
from facet angle. Local coordinates are stable support parameters: plane `(u,v)`;
cylinder/cone `(azimuth, axial)`; sphere `(azimuth, elevation)`; and torus
`(major, minor)`. They are coherent chart data, not artist UV unwraps.

Each file has one safe object name, `face_<FaceId>` groups, and additionally a
`semantic_<owner>` group when `SurfacePatch.SemanticOwner` exists. Imported
foreign STEP does not receive invented semantic owners.

## CLI

```text
aetheris mesh model.step --format obj
aetheris mesh model.firmament --format obj --output model.obj --json
```

The default is an adjacent `.obj`. JSON reports patches, cells, polygons,
quads, triangles, boundary polygons, UV/normal/position counts, deterministic
OBJ hash, bytes, watertight lower-validation, and maximum chordal error.

## Evidence

The repeatable HexBolt evidence is in
[`evidence/mesh-dcc-export-m1`](evidence/mesh-dcc-export-m1/). The direct OBJ
has 1,052 shared positions, 981 polygons (896 quads, 77 triangles, 8 boundary
polygons), 91.34% quads, and SHA-256
`048e4134128d2b486f1dc9af058afbd580cdabbb80c4e59a8dbd2bb40af3287c`.
The validated triangle target remains watertight with 2,116 triangles; it is not
used to construct the OBJ.

CTC-01 is intentionally included as a failed real-world coverage result rather
than substituted with a reconstruction. The imported artifact is one enclosed
manifold body: 117 faces (56 planar, 57 cylindrical, 4 conical), 318 edges, and
206 vertices. Its trims include 214 lines, 90 circles, and 14 B-splines. The
first generic blocker is B-spline edge 87. Fourteen B-spline edges touch 18
faces. M1 only samples exact Line, Circle, and Hyperbola edge contracts, so the command fails explicitly without legacy
fallback or a misleading partially-retopologized OBJ. See
[`ctc-01.metrics.json`](evidence/mesh-dcc-export-m1/ctc-01.metrics.json).

## Limitations and next milestone

This is a successful direct structured DCC lowering for the current analytic
SurfaceMeshIR subset, proved by HexBolt. It does **not** yet establish the
requested CTC-01 Blender result: a generic B-spline edge sampler plus bounded
analytic-face trim planning is needed before the whole foreign model can be
covered. That is the next mesh milestone; it should retain shared BRep sample
authority and add per-face coverage accounting before attempting broad trimmed
surface remeshing. No Blender or FreeCAD executable was available in this
environment, so their visual/interop screenshots and the deferred STL smoke
closeout remain unperformed.
