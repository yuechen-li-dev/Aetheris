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
