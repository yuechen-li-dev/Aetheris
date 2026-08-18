# CTC-03 formed and flat comparison after Profile-M2 edge composition

Command:

```text
aetheris sheetmetal compare testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp docs/development/milestones/modules/sheetmetal/artifacts/m8/ctc03-final.firmament --json
```

## Formed

Overall status is `NeedsReview`.

| Measure | Result |
|---|---:|
| Thickness residual | 0.0025400000 mm |
| Source → intent RMS / p95 / max | 10.6140 / 19.0713 / 52.8161 mm |
| Intent → source RMS / p95 / max | 6.75046 / 8.31005 / 19.0713 mm |
| Bend matches | 7/7 Pass |
| Opening matches | 17/17 Pass |

Bend axis residual is at most 0.002542 mm; bend-axis angle residual is zero; bend-angle and radius residuals are numerical noise; all adjacency checks pass. Profile-M2 reconstructs the front and rear mounting-flange free-edge programs as semantic `SteppedNotch` fragments. Against the M8/Profile-M1 baseline, source-to-intent RMS fell from 19.4627 to 10.6140 mm and p95 from 52.4846 to 19.0713 mm. Remaining maximum error is localized to wall-end/corner topology.

## Flat

| Measure | Result |
|---|---:|
| Generated bounds | 404.751790441 x 612.597760706 mm |
| Width residual | 0.00244728 mm |
| Height residual | 0.00781965 mm |
| Outer-contour RMS / p95 / max | 12.0385 / 19.0475 / 19.0475 mm |
| Inner cuts | 17/17 Pass |
| Bend-line count delta | 0 |
| Overlap | none |
| Generated exact contour | Valid |
| Source comparison status | Fail |

The generated blank is internally valid, connected, non-self-intersecting, and exact on the authored model. Mounting-flange reconstruction removes essentially all height error (12.7078 to 0.00782 mm). It is not yet tolerance-equivalent because the remaining wall corner/end transitions require cross-edge corner ownership rather than a baseline-returning single-edge fragment.

## Independent regeneration and reimport

The final source reads no STEP path or recovered topology identifiers. Tests copy it into an isolated temporary directory before compilation. Generated formed and flat STEP files reimport as enclosed manifold bodies:

| Artifact | Bodies / shells | Faces | Edges | Vertices | Supports |
|---|---:|---:|---:|---:|---|
| Formed | 1 / 1 | 113 | 260 | 166 | 84 plane, 29 cylinder |
| Flat | 1 / 1 | 112 | 330 | 220 | 82 plane, 30 cylinder |

DFM is `Pass`. Restoring the semantic mounting-flange outlines raises the two front mounting-hole edge clearances from the simplified 0.7849 mm result to 5.2324 mm without moving the source-matched holes.
