# M2 validation report

- CTC-03 recovery: 15 regions, seven bends, two cuts; status `Partial`; deterministic evidence ID `3dd6f4dd338cfded7467f3aded3e0bd4e32315f40cf3db0d117e22b0687cadab`.
- Reconstructed source: compiles; 103 lines versus 264 machine lines; raw coordinates/face IDs stay in evidence.
- Comparison: `PassWithKnownDifferences`; formed boundary p95 0 mm; flat contour p95 0.003990 mm; all bends/cuts localized and passing.
- Flat AP242: one body, one shell, enclosed manifold, 90 planar faces, 264 edges, 176 vertices, physical thickness 1.905 mm; ordinary re-import succeeds.
- Deterministic idiomatic flat hash: `ab5a744dd0326512832b3dc5df2201aa7dfa75844527b55f0763200fcbe96eba`.
- Representative warm timings: STEP recovery about 180–280 ms including import; comparison about 250 ms; idiomatic flatten below 1 ms. Timings are observational and excluded from hashes.
- Full solution restore/build succeeds with zero warnings/errors; all 2,826 discovered tests pass. Focused Sheet Metal tests cover recovery layers/brief/nominals/grouping, accepted deltas, wrong bend/cut localization, ordered concave source boundaries, M1 behavior, flat validation, DFM, and AP242.
- A final parallel rerun transiently failed one unrelated load-sensitive Core performance assertion (`cir` direct-recipe timing). The exact three-case test passed twice in isolation, and the complete 959-test Core project then passed. No Sheet Metal, CLI, Geometry, STEP, Firmament, DFM, or module test failed.

Known limit: formed comparison currently operates on recovered semantic boundary samples for reconstructed evidence-bound models. Independent arbitrary authored BRep surface sampling and exact analytic global blank stitching are the largest remaining verification/topology gaps.
