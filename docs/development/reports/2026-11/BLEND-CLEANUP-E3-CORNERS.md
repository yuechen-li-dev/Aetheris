# Blend clean-up E3: corners removed with their strips

**Goal.** Remove blend networks that include corner patches (spheres, B-spline setbacks,
tori meeting only blends), which E1 refused (599 of 1,201 imported blend faces).

## Method

A blend face spanning exactly two anchors is a strip (E1). Any other blend face is a
**joiner**: it leaves no edge behind, so all its edges are transverse and its vertices
collapse into the corners around it. Each corner is the point where the planes around the
cluster meet (least squares over all of them, residual checked), so 3-, 4- and 5-valent
corners need no special case. The joiner's own surface is never read.

Two surgery bugs surfaced on imported parts and are fixed:

- Faces owning a re-trimmed straight edge (but no removed edge) kept their old pcurves;
  they are now rebuilt with the changed faces.
- Pcurves are rebuilt only on changed faces. Populating the whole body invented pcurves on
  untouched faces that never had them.

## Results

Authored ground truth: the box and all five Platonic solids with every edge rounded
(sphere corners where 3, 4 or 5 fillets meet) come back as the sharp solid: faces, edges,
every vertex within 1e-6 mm, volume, and the same after export and reimport. E1's
rectangle and polygon cases still pass (12 tests).

Imported parts, one region at a time (1,201 blend faces):

| Outcome | E1 | E3 |
|---|---|---|
| removed, valid, round-trips, volume change plausible | 20 | **308** (182 concave, 126 convex) |
| non-planar neighbour (E2) | 285 | 563 |
| blocked: B-spline neighbour / hole crossing a blend | 208 | 208 |
| neighbour loop collapses | 16 | 48 |
| pcurve rebuild failed on a changed face | 43 | 32 |
| other (parallel anchors, inverted edge, corner planes not concurrent) | 39 | 39 |

Corner regions that touched curved faces now refuse as non-planar, which is why that row
grew. Largest gains: CTC-04 (60 faces, pocket-floor fillets with corners), FTC-08 and STC-08
(101 each).

Round trip is checked without pcurves: FTC-08, FTC-11 and CTC-04 import with many invalid
source pcurves (1,688, 485 and 7 validator diagnostics before any edit, independent of the
FACE_BOUND fix). 67 of the 308 faces also pass the full pcurve round trip. Invalid source
pcurves on import are a separate issue.

## Next

- E2 (non-planar neighbours) is now the dominant refusal: 563 faces.
- Collapsing loops (48): a neighbour face bounded only by blend-adjacent edges.
- Import: investigate invalid source pcurves on FTC-08/FTC-11 (orientation and closure).
