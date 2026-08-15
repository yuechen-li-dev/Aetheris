# CTC-03 formed and flat comparison

Command:

```text
aetheris sheetmetal compare testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp docs/modules/sheetmetal/artifacts/m8/ctc03-final.firmament --json
```

## Formed

Overall status is `NeedsReview`.

| Measure | Result |
|---|---:|
| Thickness residual | 0.0025400000 mm |
| Source → intent RMS / p95 / max | 19.4627 / 52.4846 / 56.6946 mm |
| Intent → source RMS / p95 / max | 6.66049 / 12.7357 / 19.0713 mm |
| Bend matches | 7/7 Pass |
| Opening matches | 17/17 Pass |

Bend axis residual is at most 0.002542 mm; bend-axis angle residual is zero; bend-angle and radius residuals are numerical noise; all adjacency checks pass. The large global surface residual is not an unexplained opening error. It is localized to the simplified outer trims enumerated in the feature inventory.

## Flat

| Measure | Result |
|---|---:|
| Generated bounds | 404.751790441 x 612.597760706 mm |
| Width residual | 0.00244728 mm |
| Height residual | 12.7078196 mm |
| Outer-contour RMS / p95 / max | 12.3763 / 19.0475 / 19.0475 mm |
| Inner cuts | 17/17 Pass |
| Bend-line count delta | 0 |
| Overlap | none |
| Generated exact contour | Valid |
| Source comparison status | Fail |

The generated blank is internally valid, connected, non-self-intersecting, and exact on the authored model. It is not tolerance-equivalent to the historical source outer blank because the remaining edge trims affect overall height and sampled contour positions.

## Independent regeneration and reimport

The final source reads no STEP path or recovered topology identifiers. Tests copy it into an isolated temporary directory before compilation. Generated formed and flat STEP files reimport as enclosed manifold bodies:

| Artifact | Bodies / shells | Faces | Edges | Vertices | Supports |
|---|---:|---:|---:|---:|---|
| Formed | 1 / 1 | 99 | 218 | 138 | 70 plane, 29 cylinder |
| Flat | 1 / 1 | 98 | 288 | 192 | 68 plane, 30 cylinder |

DFM is `Warning`, caused only by the two front mounting holes having 0.7849 mm clearance to the simplified authored rectangle versus a 2.8575 mm bounded policy. Moving the holes would destroy source parity; exact mounting-flange contour support is the correct repair.
