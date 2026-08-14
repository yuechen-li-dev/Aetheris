# M1 validation report

Implementation evidence:

- Whole-solution restore/build succeeds across 60 projects with zero warnings and zero errors.
- Module 0.2.0 advertises four bounded capabilities: constant thickness, bend recovery, flat pattern, and authored bracket.
- Sheet Metal domain tests cover allowance, authored formed/flat/STEP, imported self-recognition, CTC-03 recognition/determinism, overlap/touching, invalid radius/policy, double-curvature rejection, disconnected graph, and reference re-fold.
- CLI tests cover CTC-03 inspect JSON, authored SVG layers and label-stroke regression, authored real STEP build, and a three-artifact CTC-03 run whose flat STEP re-imports and recovered Firmament recompiles.
- CTC-03 source is read/imported but never mutated; its source PMI remains in place. Recognition does not reinterpret PMI.
- Repeated recognition preserves part/region/bend IDs; repeated flat lowering preserves byte-derived deterministic hashes.
- Representative CTC-03 timings on the development machine: import about 118 ms, recognition about 58 ms, flatten about 17 ms. Timings are observational; hashes exclude them.
- CTC-03 flat STEP validation: one enclosed manifold, one body/shell, 62 planar faces, 180 edges, 120 vertices; AP242 export and ordinary Aetheris re-import succeed.
- Recovered Firmament validation: 15 regions, seven bends, two cuts, `Partial` status, and deterministic flat hash equal to direct STEP recovery.
- The parallel whole-solution test run passed 2,819 tests but transiently failed two unrelated Core timing/display tests under load. Both failing cases passed immediately in isolation, the display case passed twice, and the full 959-test Core suite then passed in isolation. All 368 CLI tests, 37 module tests, and nine Sheet Metal tests passed in the whole-solution run.

Artifact SHA-256:

- `ctc03-flat.step`: `50d95d01cae9c272725df7c3564241ac94714848aefb0385fde6379e86b942dc`
- `ctc03-recovered.firmament`: `0c8f378a9766b5cec7d2d6d1a1144a21bfdb6509be75ded3e11a63a49d5f7dc6`
- `authored-u-channel-flat.svg`: `21b4e69a94d6fb8734e4770fc586e4873fe8c372c9df25c520fa3956b6d82fb5`
- `ctc03-flat.svg`: `0701af1c21d58c6173964ad535f0372e0b0d6d49fbbff87e963e3ff0fb599319`
- `ctc03-flat-preview.png`: `0abff4d299f853cc89c51f6ab3b68fb8e7cf8e8efd848ae614b523b410784ad3`

Known boundary: exact source-edge blank stitching, corner/relief semantics, and robust feature ownership across split faces are not yet production-complete. M1 returns `Partial` rather than claiming an exact nest-ready CTC-03 blank.
