# CTC-03 source → reconstructed intent comparison

Policy: position 0.05 mm; bend axis 0.05 mm; bend angle 0.05°; bend radius 0.05 mm; flat 0.1 mm; feature 0.1 mm.

Overall: **PassWithKnownDifferences**. Accepted difference: 1.907540000008 mm measured thickness → 1.905 mm nominal, delta 0.002540 mm.

Formed reference-boundary residuals are 0 mm RMS / p95 / max in both directions across 136 samples because reconstructed regions bind directly to the immutable recovered source boundaries. This verifies the interpretation layer’s coverage; it is not a claim of independent BRep regeneration.

| Bend | Axis mm | Axis angle | Bend angle | Radius mm | Adjacency | Status |
|---|---:|---:|---:|---:|---|---|
| FrontLipBend | 0 | 0° | 0° | 2.54e-11 | match | Pass |
| RearLipBend | 0 | 0° | 2.04e-13° | 2.54e-11 | match | Pass |
| ServiceRampBend | 0 | 0° | 2.39e-12° | 2.54e-11 | match | Pass |
| RightWallBend | 0 | 0° | 0° | 2.54e-11 | match | Pass |
| FrontWallBend | 0 | 0° | 7.12e-13° | 2.54e-11 | match | Pass |
| LeftWallBend | 0 | 0° | 8.14e-13° | 2.54e-11 | match | Pass |
| RearWallBend | 0 | 0° | 7.12e-13° | 2.54e-11 | match | Pass |

Both vent cuts match at 0 mm center and size residual. Flat dimensions change by 0.004987 mm × 0.007980 mm due to the accepted thickness nominal; contour RMS/p95/max is 0.003573/0.003990/0.003990 mm. Seven bend lines and two cuts correspond; no overlap is reported.
