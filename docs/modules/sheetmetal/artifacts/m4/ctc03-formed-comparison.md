# CTC-03 M4 formed comparison

CTC-03 remains independently constructed from `docs/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament`; no source STEP, evidence provider, raw face ID, or recovered region polygon is required for construction.

M3 -> M4 numerical comparison is unchanged because M4 deliberately did not alter already-matching bend geometry or guess historical formed trim intent:

- source -> generated RMS / p95 / max: `45.806580 / 114.786558 / 128.117359 mm`;
- generated -> source RMS / p95 / max: `11.124620 / 18.102257 / 51.535585 mm`;
- thickness residual: `0.002540 mm`;
- all seven bend axis/angle/radius/adjacency comparisons: `Pass`;
- both vent cuts: `Pass` (center residual `0.001270 mm`);
- overall: `NeedsReview`.

M4 adds stable paths for every authored flange/root/outer/bend and exact per-relief intent, but it does not fabricate historical local wall/mounting-flange trim dimensions that are absent from source intent. Formed curved relief wall materialization remains deferred.
