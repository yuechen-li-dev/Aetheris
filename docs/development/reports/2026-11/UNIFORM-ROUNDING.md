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

## Stage 2: concave edges

Each sharp edge carries a side: +1 convex (ball inside the material), −1
concave (ball outside, filling the corner). The ball centre is where every
face's signed inward distance is side·r, so the same Newton solves, contacts
and tubes serve both; concave strips and corners are bound opposed to their
surfaces (the body faces the tube's axis). Corners must be one-sided: where
convex and concave edges meet, `uniform-rounding-mixed-vertex-unsupported`
names the vertex and each edge's kind (stage 3).

Found on the way, all in the input, not the rounding:

- Loops are not even self-consistent: a revolved sleeve's outer cylinder walks
  both cap circles the same way. Orientation is now decided per (face, edge) by
  stepping off the edge along n × t and testing the point against the face's
  own boundary (exact even-odd: lines and arcs in the plane; circles over the
  point's angle on cylinders and cones); loops are re-chained on the new
  vertices (closed circles first at a shared vertex).
- Face orientation flags can be wrong: Firmament's profile revolve writes an
  annular cap's inner loop first with no boundary roles, so the STEP reader takes
  the hole as the outer bound and flips the cap (a revolved sleeve re-imports
  measuring 1553 mm³ instead of 4712). Outward sides are now computed by ray
  parity against the faces' exact surfaces and boundaries; output faces carry
  explicit outer/inner boundary roles, outer first. The revolve emitter's own
  bug is not fixed here.
- The kernel's face-domain query misjudged a point far outside a counterbore's
  shoulder annulus as inside it; clearance uses the same exact point-in-face test.
- The rounding route now triggers whenever the AllEdges fillet is all that keeps
  the source from parsing (it parses without it), which takes in bodies whose
  construction admits no Modify (Revolve); it claims only finishes it reads
  whole (target, radius, kind Fillet), so invalid fields keep their own errors.

Results: `bushing-rounded` (6 tori, the shoulder's concave), `counterbore-rounded`
(4 tori, the step's concave, 12 strips, 8 corners), `blind-hole-rounded` (rim
and concave floor tori) build and validate through the CLI, mesh watertight and
outward, read back by the certificate at r, and Remove(Round(x)) restores x's
faces and edges. Concave fit sizes are exact: a Ø8 blind hole's floor refuses
r = 2.5 "under 2 mm" (major radius R − r must exceed r). `ell-mixed-corner`
refuses at the inner corner.

## Stage 3: tangent junctions and mixed corners

Every vertex gets a plan with one ball centre: kept; a sphere corner; a strip
end (one rounded edge whose strip runs square into the face across, which
takes the ball's cross-section into its loop where the vertex was); or a
junction (two rounded edges meeting where their other faces join tangentially,
sharing one cross-section; the tangent edge is trimmed to the ball's contact).
Edges a pass leaves alone, tangent or sharp, are trimmed to wherever their
ends went. Section directions come from their own ends, so the kinds compose.

Mixed corners (one concave edge, convex others): two passes, fillets before
rounds. Pass 1 fills only concave edges: at the mixed corner the fill ends
square on the top face, leaving a convex arc and a tangent junction. Pass 2
rounds everything still sharp on that body, whose orientation is trusted
(this construction built it, with explicit boundary roles). The other order
is not exact (crossing strips meet in an ellipse), so there is no competing
interpretation to judge: the order is a rule. Two concave edges with a convex
one stay refused; rounding the odd edge first would work there too, but the
same edge's far end would then become a rolled corner instead of a sphere, a
real choice of geometry left for when it is wanted.

The route also takes AllEdges fillets that parse but that the edge-finish
lowering refuses (Compose bodies), leaving the dedicated stock builder's own
refusals alone.

Results: `rounded-rect-rounded` (8 cylinder strips, 8 tori, no corners),
`ell-rounded` (the concave fill, 17 convex strips, 2 tori of major radius 2r
over the fill's ends, 10 spheres) build through the CLI, mesh watertight and
outward, read back at r, and Remove(Round(x)) restores x's faces and edges.
Refused by name: `boss-mixed-corner` (two concave and one convex at each foot
corner), `pocket-split-face` (the composed plate's walls are split in two
coplanar faces at the pocket's depth: needs a coplanar-face merge first).

## Stabilization

Swept every fixture plus off-centre variants (14 bodies) over r = 0.02 … 5 mm
through the real Firmament path (112 cases). Checked per success: every edge
used once each way after STEP, watertight and outward SurfaceMeshIR mesh,
certificate strips at r, STEP face count, volume change bounded by area·r;
per refusal: a named code, a reported fit not above r, and 0.98·fit building.

Found and fixed:

- A contact edge just over the construction tolerance but too short for a
  direction crashed `NewEdge` near the limit; degenerate edges now refuse as a
  size limit, so the fit bisection treats them like any other.
- Asking for exactly the limit read "largest that fits is under 2 mm" for r = 2;
  now says the radius asked for is the limit itself.
- Firmament's profile revolve wrote a full revolution's annular caps in profile
  order with no boundary roles, so an inward-walking profile put the hole first
  and STEP re-import flipped the cap (a sleeve measured 1553 mm³ for 4712; a
  stepped bushing did not mesh at all). Caps now put the larger circle first
  and bind outer/inner roles: the sleeve meshes at 4688.5 (tessellated) and
  re-imports with no orientation mismatch, either profile direction.

Limits are exact against hand checks: a R10 cylinder r < 5 (rim torus major
R − r > r), a 6 mm-cornered rounded rect r < 3, a 3 mm-walled bushing or
counterbore shoulder r < 1.5, a Ø8 blind floor r < 2, a Ø6 bore 10 mm from a
wall r < 3.5 and 8 mm off-centre r < 2.5. Volume losses follow Pappus (R10
cylinder, r = 2: 103 mm³ exact, 107 tessellated).

Wireframes (`aetheris wireframe … --view iso`) of the fixtures show the
expected strips, tori, corners and the L's filled inner corner. The renderer
draws no isolines on periodic faces with seams (cylinder sides, closed-rim
tori) even on unrounded bodies: a renderer limit, not the rounding.

Regression: `RadiusSweep_EitherSoundOrRefusedWithAFitThatBuilds` (9 fixtures ×
5 radii) and `RevolvedAnnulusCapTests`.
