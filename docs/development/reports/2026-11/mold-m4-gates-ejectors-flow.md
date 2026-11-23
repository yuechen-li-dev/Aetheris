# Mold M4: gates, ejectors, flow length, knit lines

## What changed

- **Declarations.** `Gate name { kind: Edge | Submarine | Pin | Direct; at: [x, y, z] }` and
  `Ejector name { at: [x, y, z]; diameter: 4mm }` in the Mold block; policy field `EjectorClearance` (0.5 mm).
- **Placement** (`MoldLayout`). Points snap to the part's mesh (0.1 mm + chord). Edge gates within 0.5 mm of the
  parting line (its edges, or a split face's silhouette); submarine gates on the −pull half; pin and direct gates on
  the +pull half; none on an action's face. Ejector pins on the −pull half, square to the pull within 1°, with the
  footprint and then the clearance ring sampled (32 points each) on −pull faces square to the pull. Contradictions
  (off the part, wrong half, edge gate off the line, footprint off the seat) are errors at every stage; a pin off
  square or inside its clearance, and gates and pins on cosmetic faces, are soft findings.
- **Fill** (`MoldFlow`). Voxelize the interior by ray parity along columns (new `MoldMeshRays.Crossings`), cell =
  max(thinnest wall / 3, cube root of box / 600k). Multi-source Dijkstra over 26 neighbours from the gates, cost =
  step / local wall (wall measured behind the nearest face); gives flow ratio (L/t) and flow length per cell, last
  point to fill, unreached fraction. Finding over the material's `MaximumFlowRatio`.
- **Knit lines.** A neighbour pair (all 13 forward of the 26), neither the other's parent, arriving more than 45° apart
  (meld threshold) is a meeting if it came from different gates, or if walking both fill paths back in lockstep the
  segment between them leaves the material while they are more than 4 cells apart: the front went round opposite
  sides of something. Clustered across 1-cell gaps, under 3 cells dropped; faces reached and cosmetic flag. Finding
  when a knit line reaches a cosmetic face.
- **Measurements** for `Require`: `gates`, `ejectors`, `flow.ratio` (Float), `flow.length`, `knit.count`,
  `knit.cosmetic`.

## Evidence

| Case | Cells (size) | Flow ratio / length | Knit lines | Build |
| --- | --- | --- | --- | --- |
| cover-tool-layout (2 mm hollow cover, edge gate on an end) | 58,872 (0.65 mm) | 57.6 / 115 mm | 2, down each side wall where the front over the top meets the one round the sides | ~2.4 s |
| gated-plate (120×60×2.5, Ø12 hole, gate at one end) | 30,468 (0.83 mm) | 52.2 / 130 mm | 1, x = −6.2 → −59.6 behind the hole, on the cosmetic top | ~2.1 s |
| plate, gates at both ends | | flow length halves (50–75 mm) | 2 gate-to-gate segments at x = 0, either side of the hole | |
| plate at 1.2 mm in PC | | > 100 | | flow-too-long finding |

The cover's side-wall knit lines were checked against the unfolded geodesics: at x = 0, round the side 64 mm, over the
top 65.6 mm, arriving about 90° apart, so the fronts do meet there.

Knit detection took three attempts, each kept only on evidence:
1. **Arrival angle > 120°.** Found only a 2 mm stub behind the hole: downstream the fronts meet at shallow angles.
2. **Doubling lookbacks.** Testing the segment at 4, 8, …, 128 steps back left a 14 mm gap, because the doublings skip
   the stretch where the segment crosses the hole.
3. **Lockstep walk, face neighbours only.** Still left gaps where the two fill trees meet across a diagonal. All 26
   neighbours closed them.

Grid ties make parallel staircase paths through open material diverge, so path separation alone is not a test. The
segment leaving the material is.

Tests: `MoldTests` 45 (layout, fill, knit behind a hole, two gates, flow over the ratio, five misplacement cases,
clearance, two refusal fixtures).

## Verdict

M4 done as a geometric proxy, which the public doc says plainly: no viscosity, pressure, freeze-off or race-tracking.
Ejector sleeves and blades, runner and cooling layout are not in. The suggestion phase (M6: gate and ejector placement
by Judgment Engine) can now score candidates with this fill.
