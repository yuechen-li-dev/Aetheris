# CTC-03 feature inventory

Inventory is from a fresh CLI recognition of `testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp`, AP242 entity inspection, and planar boundary evidence. `Recovered` means represented in semantic recovery/IR; `regenerated` means emitted from `ctc03-final.firmament` with the source STEP unavailable.

| Feature family | Source | Recovered | Regenerated | Status |
|---|---:|---:|---:|---|
| Planar sheet regions | 15 | 15 | 15 | Pass by count/adjacency; some trims simplified |
| Bends | 7 | 7 | 7 | 7/7 comparison pass |
| Main-deck Ø15.875 holes | 4 | 4 | 4 | Pass |
| Main-deck Ø50.8 opening | 1 | 1 | 1 | Pass |
| Main-deck Ø38.1 opening | 1 | 1 | 1 | Pass |
| Main-deck 19.05 x 88.9 slots | 2 | 2 | 2 | Pass |
| Mounting-flange Ø11.1252 holes | 4 | 4 | 4 | Pass |
| Service-flange Ø27.051 hole | 1 | 1 | 1 | Pass |
| Service-flange Ø4.7625 holes | 4 | 4 | 4 | Pass |
| Partial-span 45-degree service flange | 1, span 127 mm | 1 | 1 | Pass by bend/axis; surrounding right-wall trims simplified |
| Service outer tab | 1, 101.6 x 12.7 mm | 1 | 1 | Present; participates in exact blank |
| Automatic corner reliefs | four corner transitions | inferred | 4 bounded rectangular removals | Manufacturing interpretation; not source-trim parity |
| Wall-end chamfers/steps | present on front/rear/left/right contours | boundary evidence | simplified | Missing exact trim parity |
| Front/rear mounting-flange stepped/chamfered free-edge reliefs | 2 | boundary evidence | 2 semantic `SteppedNotch` programs | Pass for central edge programs; endpoint corner chamfers remain |
| Right-wall service attachment cutbacks | present | boundary evidence | partial-span attachment only | Missing exact trim parity |

## Opening detail

All 17 source opening matches pass. Center residuals are 0.000526–0.001270 mm and size residuals are below `3.57e-10` mm. Four base fasteners use 44.45 mm pitch; front/rear mounting pairs use 203.2 mm pitch; the two deck slots use 63.5 mm pitch; the service small-hole pairs use 38.1 mm pitch.

## Missing-feature audit

Nothing in the recognized opening inventory is absent. The remaining absent geometry is localized to outer boundaries:

- base/wall corner cutbacks and stepped transitions;
- front and rear wall end bands;
- mounting-flange endpoint corner chamfers (central 5/7-curve free-edge programs are regenerated);
- left-wall tapered ends;
- right-wall chamfers and service-attachment cutbacks.

These differences are explained but not accepted as equivalent. They drive the global formed and flat residuals and prevent a `Complete` verdict.
