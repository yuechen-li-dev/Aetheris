# P2-SURFACE-MESH-IR-M4

M4 moves the reference HexBolt's exact analytic B-rep through
`SurfaceMeshDocument` and deterministic triangle lowering without a legacy
face fallback. The direct materialized body and the Firmament -> STEP -> import
route are both covered by `SurfaceMeshIrM4HexBoltTests`.

## Bounded trim bands

`TrimBandPlan` defines a deliberately small four-sided analytic-patch contract:
one structured guide boundary, one exact trim, two side boundaries, paired
sample counts, and analytic interior evaluation. It is not an arbitrary trimmed
surface remesher.

For each head chamfer sector the top circular arc and lower exact Hyperbola are
sampled by the shared-edge planner with the same segment count. Correspondence
is their ordered B-rep parameter sequence; interior points interpolate the
matched angular and axial cone coordinates. This gives an angular x generator
quad band and the planar hex face consumes precisely the same Hyperbola vertex
IDs. No positional welding or independent face sampling occurs.

The planar underside's unequal-count convex hex/circle contact is handled by a
localized annular zipper: matching normalized boundary progress produces quads
where possible and only boundary triangles for count mismatch. Large planar
regions otherwise remain coarse.

## Root fillet and edge sense

The root blend is a four-directed-edge bounded Torus patch, not a clipped full
torus. The planner takes boundary order from the face's coedges and applies
`IsReversed` before assigning top/right/bottom/left. This matters after STEP
round-trip: the two contact circles and two minor arcs can be represented with
different edge orientations. Opposing boundaries share a sample count, while
major and minor directions retain independent density. The patch therefore
keeps the dense direction around the contact loop and the sparse direction
across the 0.2 mm fillet radius.

`SurfaceMeshIrM4HexBoltTests.HexBolt_RootFilletRespectsDirectedContactBoundaries_Deterministically`
guards this route. It explicitly requires bounded (non-periodic) Torus patches
with quad cells and equal deterministic hashes across repeated builds.

## Evidence

The CLI path generated [SurfaceMeshIR JSON](evidence/surface-mesh-ir-m4/hexbolt-surface-mesh-ir.json)
and [binary STL](evidence/surface-mesh-ir-m4/hexbolt-surface-mesh-ir.stl).
The deterministic mesh hash is
`6e14353827839fd10eb5ca6a07b8ff3a15f0bba1028b66aedb8024e6ed286eef`; the
STL SHA-256 is
`776051751bf41cd6744bc3f7d063ed57d072cd5335f044fadd542723ace086ee`.

At the default policy the imported reference HexBolt has 21 patches, 981 cells,
896 quads (91.3%), 85 exceptional planar cells, 1,060 vertices and 2,116 final
triangles. It is watertight with zero non-manifold edges, zero cracks, zero
normal deviation from analytic support normals, and maximum sampled boundary
chord deviation 0.03593158223 mm.

## Remaining scope

M4 intentionally still does not generalize this to arbitrary non-convex planar
holes, arbitrary multi-loop conic/toroidal trims, runtime LOD, or DCC export.
Cadmata's pre-triangulation topology overlay and external FreeCAD screenshot
evidence remain separate viewer/evidence work; the CLI export route itself is
now SurfaceMeshIR-only for the reference HexBolt.
