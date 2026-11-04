# Uniform rounding: "fillet everything" on convex planar bodies

## Method

Rounding every convex edge of a body with a ball of radius r is a morphological
opening: erode by r, then dilate by the ball. On a convex body bounded by planes
the erosion is the intersection of the face half-spaces moved in by r (vertices
by triple-plane enumeration), and the dilation is the existing exact ball sweep
(`ConvexStockPrimitives`: planes back out by r, cylinders on edges, spheres on
vertices). There is no corner policy to choose: a vertex where four or more faces
meet unevenly opens into short edges, each its own strip, which is what a rolled
ball does. `UniformRounding.RoundAllEdges` in Core.

Refusals, in order, each naming what was found:

- spline / elliptical edges (`uniform-rounding-spline-edge-unsupported`,
  `-elliptical-edge-unsupported`): counts and the first edge ids of each;
- circular, other curved edges, curved faces;
- concave (a vertex beyond a face plane, by how much);
- radius too large / consuming a face whole: with the largest radius that fits,
  by bisection (both predicates are monotone in r).

`ConvexStockPrimitives.Hull` no longer assumes the origin is inside (orientation
and coplanar ordering use the centroid); `CreateHull` is public.

## Firmament

`EdgeFinish<Fillet> { target: AllEdges }` that the Box/Polyhedron stock route
refuses (`firmament-v2-uniform-stock-fillet-unsupported`) is routed: the finish
(and a Modify it leaves empty) is removed from the source, the rest is built
through the ordinary pipeline, and the result is rounded and re-exported with an
EdgeFinish receipt. Rounding refusals surface as `firmament-uniform-rounding` at
the finish. `validate` (not `--parse-only`) answers the same way as build. The
admitted document is the stripped one, so assertions measure the rounded body —
first wiring silently skipped them (the full source does not parse), caught by a
test asserting the sharp counts must fail.

Chamfer-all and several AllEdges finishes keep the old refusal.

## Results

- Core: regular solids 4/6/8/12/20 and the box equal the dedicated builders
  (counts, vertex points, volume); square pyramid, skew-cut prism and an
  irregular high-valence hull round and read back by the rolling-ball
  certificate (one strip per edge at r, one joiner per corner); fit size is
  sharp (0.99·fit builds, 1.01·fit refuses); SO-101 names splines and ellipses.
- Firmament: extruded triangle prism r 2 → 5 planes, 9 cylinders, 6 spheres,
  deterministic STEP; L-profile refuses as concave; r 30 refuses with "under
  6 mm"; Cylinder, Box+Hole refuse as circular; Ellipsoid as elliptical; FTC-09
  via InlineStep as "N spline edges (ids…)".
- Round trip: Remove(Round(prism)) is the prism (points and edge count).

## Open

- Circular edges (cylinders, holes): the erosion is no longer polyhedral.
- Concave edges: closing by the ball (the dual operation) is not built.
- Spline / elliptical edges: no plan yet; the message says so to be fed back.

## Stage 1: circular edges (local rolling ball)

`LocalRounding` builds the rounding on the body's own face graph for small r:
faces keep their surfaces and are re-trimmed; each convex edge's strip is the
tube around the ball centre's path (cylinder around a line, torus around a
circle); each three-edge corner a sphere. Ball centres: 3×3 Newton on the faces'
distance functions at corners (exact in one step on planes), 2×2 in the meridian
for closed circles, at the seam's angle so rebuilt seams stay generators.
Convex planar bodies still go to the opening (it handles uneven high-valence
corners); `LocalRounding_MatchesTheOpening` checks the two agree (counts,
points, volume to 1e-4) on box, tetrahedron, dodecahedron and a skew prism.

Found on the way:

- Input loops are not oriented: STEP-imported and primitive bodies walk shared
  edges the same way on both faces, and point containment answers Boundary even
  at a tetrahedron's centre (primitives only). Orientation and convexity are
  therefore computed, not read: each loop's exact signed area (shoelace plus
  circular segments in the plane; the (angle, height) chart on cylinders and
  cones, where circles and generators are straight) sets outer loops CCW about
  the outward normal, and convexity follows from the oriented coedges. Output is
  consistently oriented (every edge used once each way), STEP round trip too.
- `BrepPrimitives.CreateCylinder` leaves seam vertices unplaced; positions come
  from the curves' ends.
- A ball that only touches a third face pinches a face to nothing (a bore 2 mm
  from a wall at r = 1 built with zero volume); touching is now refused.
- SurfaceMeshIR: a rim rounded all the way round is a torus band whose closed
  rings run around the major direction (the swept-bend case had only minor
  rings); and the four-sided torus patch interpolated V (the minor angle)
  linearly, folding any strip whose tube crosses V = 0 (the bottom rim of an
  extruded arc). Both fixed; the four fixtures mesh watertight and outward.

Results: `fixtures/Regression/UniformRounding` — cylinder (2 tori), frustum (2
tori on a cone), block with a through bore (12 cylinder strips, 2 tori, 8
spheres), D-profile (4 cylinder strips, 2 tori, 4 spheres). Each validates and
builds through the CLI, round-trips STEP, meshes watertight, reads back by the
certificate at r, and Remove(Round(x)) restores x's faces and edges. Fit sizes
are exact where checkable: a Ø6 bore in a 20 mm block refuses r = 4 "under
3.5 mm" (10 − r − 3 = r); a bore 2 mm from a wall refuses r = 1 "under 1 mm".

Next: concave edges (stage 2), mixed vertices and tangent junctions (stage 3).
