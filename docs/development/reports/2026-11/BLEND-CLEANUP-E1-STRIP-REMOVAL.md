# Blend clean-up E1: strip-network removal between planes

**Goal.** Delete blend strips classified by E0 and rebuild the sharp geometry from the
extended neighbouring planes, verified against parts authored without the finish.

**Code.** `Aetheris.Kernel.Firmament.Tests/Experiments/BlendNetworkRemoval.cs` (experimental),
tests in `BlendNetworkRemovalTests.cs`; reach run `BlendNetworkRemovalReach` with
`AETHERIS_EXPERIMENTS=1`, output `artifacts/local/blend-probe/e1-reach.txt`.

## Method

- Each strip spans exactly two **anchors** from classification: its tangent neighbours
  (fillet) or the neighbours it leans 20–70° against (chamfer). A first version guessed
  anchors by longest shared boundary and picked a thin part's parallel top and bottom faces
  instead of the walls a vertical fillet blends.
- Vertices joined by a strip's transverse edges (end arcs, miters) collapse into one corner,
  placed where the planes around them meet (checked to 1e-6 mm; two planes give their line's
  point nearest the original vertex).
- Each strip becomes one straight edge along its anchors' intersection; neighbouring loops
  are rewired, kept straight edges re-trimmed to the moved corners, pcurves rebuilt.
- Anything else refuses with a code: non-planar neighbour, corner patch, seam edge, planes
  not meeting, loop collapsing, pcurve rebuild failing.

## Results

Authored ground truth (finish removed by surgery vs part authored without it): rectangle
top fillet, top chamfer, bottom fillet, angled polygon rim fillet. Face, edge and vertex
counts equal; every vertex within 1e-6 mm; exact volume equal to 6 decimals; same after
export and reimport. D-profile (cylindrical neighbour) and chimera (corner joiner) refuse.

Imported parts (17 NIST + SO-101, 1,201 classified blend faces), one region at a time:

| Outcome | Faces |
|---|---|
| removed, valid, reimports, volume change plausible | 20 (12 concave, 8 convex) |
| region contains corner joiners (E3) | 599 |
| non-planar neighbour (E2) | 285 |
| blocked: B-spline neighbour | 123 |
| blocked: hole crosses blend | 85 |
| pcurve rebuild failed on a curved neighbour's loop | 43 |
| strip spans other than two anchors | 27 |
| a neighbour loop collapses | 16 |
| corner planes not concurrent | 3 |

Removals land on CTC-03, FTC-09 and STC-09. Volume is exact where available, otherwise the
tessellated evaluation; a concave blend's removal lowers volume, a convex one raises it.

## Findings

1. **Strip surgery is sound where it is admitted.** Exact on authored parts, valid on the
   NIST regions it touches.
2. **Real parts are corners and curved neighbours.** Half the blend faces sit in regions
   with joiners and a quarter border cylinders, cones or tori. E1 alone reaches 2% of
   imported blend faces; E2 and E3 decide usefulness.
3. **Anchors must come from classification, not size.** Shared-boundary length misled on
   thin parts; tangency (fillet) and lean angle (chamfer) do not.

## Next

- E2: extend non-planar neighbours (plane–cylinder, coaxial cylinders, plane–cone); new
  edges become circles, lines or conics; rebuild pcurves on curved faces (the 43 failures).
- E3: corner joiners: delete them with their strips; corners from three neighbours.
- Investigate the 16 collapsing loops (a neighbour face made only of blend-adjacent edges).
