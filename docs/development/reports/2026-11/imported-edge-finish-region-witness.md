# Single-edge finishes on imported STEP solids

The kernel witness starts with a qualified imported BRep and one selected edge.
It derives the working frame from the edge and its two outward planar normals.
The endpoint fan supplies the two caps and four straight neighboring edges. The
witness requires a convex dihedral and two trivalent endpoints with perpendicular
planar caps. The dihedral angle and frame orientation are measured from the
body. The rest of the solid may have a different construction, nonconvex
geometry, or existing features.

For a chamfer, the size is the equal setback measured along both supports. For a
fillet, the size is the cylinder radius. The setback and circular sweep follow
from that radius and the measured dihedral. A conservative convex prism encloses
the material cut. Projection tests prove that other trims and faces clear it by
the requested gap plus import tolerance. A bound that cannot prove separation
causes a refusal. Full-support bounds can refuse some otherwise valid edits.

The splice shortens four edge trims on their original line curves, retires the
selected edge and its old endpoints, and inserts one face. It rewrites the four
outer loops, builds pcurves for new coedges, and retains the original face
bindings, inner loops, and unaffected topology. The bore witness and the edge
witness use the same closed-shell, pcurve and STEP preflight gate.

## Evidence

- A FreeCAD-exported block accepted a planar chamfer and a cylindrical fillet.
  The Aetheris CLI reopened both as enclosed manifolds with 7 faces, 15 edges,
  and 10 vertices. The chamfer has 7 planes. The fillet has 6 planes, a cylinder
  and two circular edges.
- Both finishes round-tripped on a convex edge with a non-right dihedral in a
  rotated nonconvex prism. The witness checks the setbacks and the fillet's
  tangent normals at both support boundaries.
- A plate with an existing through-bore accepted a nearby finish and preserved
  the bore's topology and analytic support. After reimport it accepted a
  second, different finish at the opposite edge. A cut that left only 0.05 mm
  clearance was refused against the requested 0.1 mm, as was an intersecting
  cut. The original STEP export matched byte for byte after the attempts.
- A reentrant edge and an edge with oblique endpoint caps received distinct
  refusals before mutation.

Run the kernel witnesses with:

    dotnet test Aetheris.Kernel.Core.Tests -c Release --filter 'FullyQualifiedName~BrepImportedEdgeFinishTests|FullyQualifiedName~BrepThroughHole'

The sample STEP results, CLI analysis, and test logs are under
artifacts/local/imported-edge-region/.

The Windows Release build and final serial solution test passed. The suite recorded 6,795 passes, 2 skips, and 0 failures. The combined focused run passed 21 cases.

## Generalization and Concepts

The splice is reusable wherever the solver can prove the local replacement
boundary. The evidence covers arbitrary orientation, non-right dihedrals,
nonconvex stock elsewhere, and retained bores and finishes. Changing the
endpoint policy is the next geometric boundary. On an oblique planar cap, a
chamfer can terminate in a line, where a circular fillet needs a cylinder-plane
section, generally an ellipse, and corresponding pcurves. A multi-face endpoint
needs its own corner transition. Curved and reentrant supports require a new
section and material-side proof.

A Firmament Concept Struct could own named support selection and the local
reference frame, derived from the current pinned import. That would give
authors a stable name for the edge-finish intent across rebuilds. Region
admission would resolve the current adjacency, check clearance, and issue
the exact patch. The current witness exercises this kernel boundary directly.
