# CTC-03 intent decisions and iteration journal

## Structural interpretation

- Largest planar region → `MainDeck`.
- Four adjacent orthogonal regions → `FrontWall`, `RearWall`, `LeftWall`, `RightWall`.
- Two terminal regions beyond front/rear 90° bends → mounting flanges.
- The 45° region on the right-wall chain → `AngledServiceFlange`; the functional name is plausible but not proven by STEP history.
- Two equal rectangular openings → `VentPair` with `VentSlotLeft` and `VentSlotRight`. Pattern history remains a strong suggestion rather than a claimed source fact.
- No CTC-03 opening satisfies the bounded near-bend relief predicate, so no relief is asserted. Corner relationships remain `Unknown` where trimming does not justify a named family.

## Nominals

| Quantity | Measured | Proposed | Delta | Evidence | Decision |
|---|---:|---:|---:|---|---|
| Thickness | 1.907540000008 mm | 1.905 mm (0.075 in) | -0.002540 mm | decimal-inch candidate within 0.01 mm | accepted |
| Six bends | ~90° | 90° | < 1e-12° | repeated canonical angle | accepted |
| Service bend | 45.000000000002° | 45° | -2.4e-12° | canonical angle | accepted |
| Bend radii | 6.350000000025 mm | 6.35 mm (0.25 in) | about -2.54e-11 mm | seven repeats and inch fraction | accepted |
| K-factor | not established | 0.5 | n/a | M1 default only | retained as explicit assumption |

## Iterations

| Iteration | Change | Before | After | Reason |
|---|---|---|---|---|
| Draft 0 | M1 forensic emitter | 264 noisy lines; no source-vs-intent result | n/a | machine evidence baseline |
| Draft 1 | Named base/walls/flanges/bends/vent pair; accepted nominals | no comparison | formed boundary p95 0; flat contour p95 0.003990 mm | first engineering interpretation |
| Draft 2 | Classified thickness delta as accepted, not unexplained | overall `Pass` obscured nominal delta | `PassWithKnownDifferences` | authority/audit clarity |

Remaining ambiguity: the words “mounting” and “service” are engineering readings of geometry, not recovered authoring history; K-factor and original feature pattern remain unknown.
