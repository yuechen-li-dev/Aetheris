# AIR-FILLET-TRIHEDRAL-M5

M5 admits exactly one three-edge fillet junction: a history-known axis-aligned box at
the `(+X,+Y,+Z)` vertex, with `SharedEdge(+X,+Z)`, `SharedEdge(+Y,+Z)`, and
`SharedEdge(+X,+Y)` selected as constant, equal-radius fillets.  It is not a
general rolling-ball implementation.

## Exact construction

For box corner `O=(hx,hy,hz)` and radius `R`, the common center is
`C=O-R*(+X,+Y,+Z)`.  The XZ, YZ, and XY cylinders have axes `+Y`, `+X`, and
`+Z`, respectively, all with radius `R` and origins on the corresponding axis
through `C`.

Each cylinder intersects the sphere `|P-C|=R` in the coordinate plane through
`C` normal to its cylinder axis.  The admitted finite branch is a quarter circle:

- XZ: `y=Cy`, from `(hx,Cy,Cz)` to `(Cx,Cy,hz)`;
- YZ: `x=Cx`, from `(Cx,Cy,hz)` to `(Cx,hy,Cz)`;
- XY: `z=Cz`, from `(Cx,hy,Cz)` to `(hx,Cy,Cz)`.

These three curves close the positive spherical octant.  They are exact `CIRCLE`
trims shared by the spherical and cylindrical faces; no full sphere is emitted or
heuristically trimmed.

## AIR and plan ownership

Three semantic `AirEdgeFinishFeature` values lower together into one immutable
`LocalizedTrihedralFilletConstruction`.  It owns three cylindrical replacements,
the `SphericalCornerPatchConstruction`, three typed sphere-cylinder seam witnesses,
trimmed retained planes, remote endpoint arcs, material side, provenance, and one
`LocalizedEdgeJunctionTopologyPlan`.

The authoritative plan has 13 vertices, 21 edges, 10 loops/coedge loops, and 10
faces: six planes, three cylinders, and one sphere.  Stable roles identify the
three replacement faces, the three sphere-cylinder seams, retained `+X/+Y/+Z`
supports, remote endpoints, and the spherical corner patch.  The STEP emitter
consumes that one plan; it does not emit independent fillets and stitch them.

## Proof and preflight

The construction samples each exact seam midpoint and proves both sphere and
cylinder containment, with zero analytical residual.  Sphere and cylinder normals
are equal on the seam (`G0` exact and `G1` normal deviation zero); M5 does not
claim G2.  Standard Enforce preflight additionally verifies all topology vertex to
trim endpoint agreements, all trim/support containment checks, loop closure, and
shared edge ownership.  M5's hard admission verifies the one shared canonical
vertex, planar orthogonal box history, positive equal radii, extent fit, exact
seams, closed patch, and manifold plan before STEP emission.

STEP uses `SPHERICAL_SURFACE`, `CYLINDRICAL_SURFACE`, `PLANE`, and `CIRCLE` /
`TRIMMED_CURVE`.  The successful canonical fixture reimports as an enclosed
analytic manifold.  Its generated report records the deterministic plan signature
and SHA-256 for the STEP artifact.

The independent analytic removed-volume check is
`(1-pi/4) R^2 ((W-R)+(D-R)+(H-R)) + (1-pi/6) R^3`: three non-overlapping remote
cylindrical strips plus the local `R` cube less its spherical octant.  For the
canonical `10 x 8 x 6, R=1` fixture this is `4.983039793055287 mm^3` exactly in
terms of `pi` (the displayed decimal is numerical).

## Boundaries

Supported fixtures are `10 x 8 x 6, R=1`, `10 x 8 x 6, R=2`, and `12 x 5 x 7,
R=1` (millimetres).  Invalid radii, overlarge radii, non-sharing selections,
mixed finish families, imported/no-history bodies, concave/non-orthogonal/curved
supports, and valences outside this exact three-edge set are rejected before BRep
or STEP emission.

Unequal radii are deliberately deferred with
`localized-trihedral-fillet-unequal-radius-corner-surface-required`.  They are not
claimed to be toroidal: a future milestone must prove an appropriate distinct
corner surface and fixtures before admitting them.

The next recommended edge-finish milestone is a separately proven trihedral
orientation/history classifier (additional convex box corners), not unequal-radius
or generic rolling-ball support.
