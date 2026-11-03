# Blend clean-up E2a: strips and corners next to cylinders and cones

**Goal.** Remove blend networks whose neighbours are curved, which E3 refused as
"non-planar neighbour" (566 of 1,201 imported blend faces).

## Census first

`BlendE2Census` (gated) classified the refusals. Most needed little: 222 were ordinary
plane–plane strips refused only because a corner touched a cylinder. The curved cases were
plane–cylinder/cone across the axis (sharp edge is a circle: 178 faces), plane–cylinder
along the axis (a line: 65), and corners among planes and cylinders. Kept edges that move
were 597 lines and 135 circles; splines, spheres and tori as neighbours were rare.

## Method

- Neighbour faces are implicit functions (plane, cylinder, cone). Plane-only corners keep
  E1's exact solve; others use Gauss–Newton with the minimum-norm step from a vertex a kept
  edge leaves from, so an underdetermined corner (two faces) stays on that edge.
- A strip's sharp edge is the line between its corners (two planes, or a plane along a
  cylinder's axis) or the circle where a plane meets a cylinder or cone across its axis,
  taking the arc under the old edge. A ring blend (its own seam) becomes a closed circle.
- Kept circles that move are re-trimmed by angle, each end unwrapped next to where it was.
- Every new edge is checked on both its surfaces, every moved corner on its kept edges.
- Checks use the source's declared accuracy (10 µm when it declares none), as tolerant
  edges do on import. At 1e-6 mm, 178 faces refused for corners 3e-5 to 4e-4 mm off kept
  edges that are themselves that far off their surfaces in the source.

## Results

Authored ground truth (14 tests): E1/E3 cases unchanged; D-profile rim fillet and a rounded
rectangle with tangent corner arcs (fillet and chamfer) come back as the sharp part: faces,
edges, vertices within 1e-6 mm, volume, and after export and reimport.

Imported parts (1,201 blend faces):

| Outcome | E3 | E2a |
|---|---|---|
| removed, valid, round-trips, volume plausible | 308 | **750** (475 concave, 275 convex) |
| of which full pcurve round trip | 305 | 575 |
| non-planar neighbour / corner off kept edge | 566 | 0 |
| corner not common (two corners in one cluster) | 6 | 138 |
| blocked: freeform neighbour / hole crossing a blend | 208 | 142 |
| anchor pair unsupported or oblique | – | 74 |
| loop collapses | 48 | 60 |
| other (parallel planes, inverted edge, underdetermined) | 74 | 45 |

The FTC-06 "volume implausible" verdict (faces 112–113) is a measurement error in the
**source**: its tessellated face 24 (half-cylinder whose top circle is split into two arcs,
one trimmed from −6e-15) covers 302 mm² instead of 601. The removal itself is right: both
half-cylinders come back at exactly πrh. Display tessellator issue, not investigated.

## Next (E2b)

- Corners not common (138). Two patterns:
  - A strip ending on a curved face outside the network whose far end is already a sharp
    corner of the kept faces (CTC-01): that vertex must stay, and the end edge becomes the
    kept face's intersection with the anchor, instead of collapsing into one point.
  - Sphere joiners between cylinder fillets around a rounded pocket wall (FTC-10), and a
    fillet as large as its arc, where the blend touches the top at a point (chimera; the
    probe reads it as a 0-anchor joiner). Pinned by `FilletAsLargeAsTheArc_Refuses`.
- Anchor pairs not covered (74): cylinder–cylinder, cone–cylinder, oblique plane–cone.

## E2b: corners that do not meet

Framing: this is a clean-up toolbox for parts other systems blended badly. A refusal is
useful when it tells the caller what to select or what is missing.

- **Kept corners stay.** When a cluster's surfaces miss one point, vertices that are already
  exact sharp corners of three or more kept faces stay put; the rest collapse to the strip's
  corner, and the end edge between them becomes the kept face meeting the strip's anchor.
  Tried only after the full collapse fails, so earlier results are unchanged.
- **Ellipses.** A plane cutting a cylinder obliquely gives an elliptical sharp edge (CTC-01's
  end edges); moved kept ellipses are re-trimmed like circles.
- **Lone corners refuse with a selection hint.** A corner patch with no selected strip beside
  it (21 in CTC-02: B-spline joiners among fillet cylinders the probe put in other regions)
  names the faces it joins: "select the fillets it joins too".
- **Corner refusals name the curved faces they hit.** Most remaining ones are strips running
  into another blend that was not selected with them (CTC-04 R42: a straight chamfer ending
  on chamfer cones round rounded corners, which the probe region split off).
- **Tolerant results stay honest.** Where a vertex that stayed sits within the source's
  accuracy but off an anchor (5.5 µm in CTC-01), edges ending there are tagged
  `SourceTolerant` at their measured gap (vertex to curve, curve to face, vertex to face), so
  the export declares the uncertainty. The importer now projects circle and ellipse trim
  points to the file's declared accuracy (span thresholds keep the geometric tolerance).

Reach: 754 removed (578 full pcurve round trip); corner-not-common 138 → 114, plus 23 now
named lone corners. Remaining full-round-trip gap includes CTC-01 R2: a line rewritten through
its vertices on export lands 1.09e-5 mm from its trace (not chased).

Next: per-region reach undercounts a toolbox that selects whole networks. Merging probe
regions that meet at unselected blend faces (cones, cylinders tangent to the strip ends) is
the next lever; then cylinder–cylinder and cone–cylinder sharp edges (73 faces).
