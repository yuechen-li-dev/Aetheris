# Mold M2 gaps: faces cut at the parting plane, slide pins

## What changed

- **Faces cut at the parting plane** (`MoldSplit`). When inserts are asked for, every face crossing the plane is cut
  there, and the cut part is reviewed again so each face is formed by one half; the inserts come from that review.
  - Edges: lines and circles are split at their exact crossings (other curve kinds crossing the plane refuse).
  - Faces: planes along their trace line; a cylinder across the pull along its two silhouette lines (its axis must lie
    on the plane); cylinders and cones about the pull along a circle. Crossing points are paired along the trace where
    the segment's midpoint lies on the face's mesh, so holes in a face are handled without a 2D classifier.
  - Pieces are walked off the boundary: along it on one side, across the cut where it crosses. A closed circle cut
    (a seamed bore) runs about the axis the way the boundary on the other side does, the boundary it stands in for.
    The first rule tried (mesh probe beside the cut) gave a twisted bore face that still passed the directed-edge
    manifold check; the shut-off orientation check caught it.
  - The second review turns "the cut leaves a face in the wrong half" into an undercut, and the inserts are refused
    with it (a wall drafted upwards, cut below its top).
- **Slide pins** (`MoldInserts`). An action's faces reversed plus a planar cap across their mouth make the pin; the
  half the mouth opens into gets the same cap in its own edge direction. The mouth must be one flat loop square to the
  action's pull and open into one half. The loop convention is the one read off the cavity's parting line.
  `Assemble` now takes a face predicate and optional caps, so the pin is the same assembly without a parting face.
- CLI writes `<part>.slide-<action>.step`; evidence gains `Slides` and `SplitFaces`.

## Evidence

| Fixture | Split faces | Pins | Block = cavity + core + part + pins |
| --- | --- | --- | --- |
| rod-split (cylinder across the pull, end discs) | 3 | – | 77760 = 77760.0 mm³ |
| block-mid-parting (4 walls, 4 corner cylinders, seamed bore; bore shut-off at the plane) | 9 | – | 224000 = 224000.0 mm³ |
| side-action-latch (blind drilled hole along −Y) | – | pinCore, 353.4 mm³, 3 faces | 120000 = 120000.0 mm³ |
| housing-inserts (unchanged) | – | – | exact to 1e-16 |

Every insert and pin re-imports from its STEP as an enclosed, consistently oriented solid with positive volume.
Refusals tested: a drafted cover parted mid-height (undercut for the core), the rod parted off its axis.

## Verdict

M2's insert gaps are closed for planes, cylinders and cones; spheres and freeform faces crossing the plane still
refuse, as do slides whose mouth is not flat (a hole drilled into a curved face).
