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
