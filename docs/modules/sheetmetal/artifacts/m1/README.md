# AETHERIS-SHEETMETAL-M1 evidence

This bundle records compact, reproducible evidence for the first implemented Sheet Metal milestone. It intentionally excludes STEP entity dumps and generated heavyweight meshes.

| Evidence | Result |
|---|---|
| [CTC-03 geometry audit](ctc03-geometry-audit.md) | One enclosed manifold; 120 faces; analytic plane/cylinder support only. |
| [CTC-03 recognition](ctc03-recognition.md) | Partial, honest recovery: 1.90754 mm, 8 planar regions, 7 bends, 2 openings. |
| [Authored bracket](authored-bracket.md) | Exact closed formed BRep, two bends/two holes, valid deterministic flat. |
| [Flat validation](flat-pattern-validation.md) | Finite/closed/contained, no planar-region overlap in either canonical fixture. |
| [DFM summary](dfm-summary.md) | Parameterized provisional checks, no universal fabrication claims. |
| [Validation report](validation-report.md) | Build/test/CLI/determinism and known limitations. |

Primary recovered manufacturing artifacts:

- [`ctc03-flat.step`](ctc03-flat.step) — closed 1.90754 mm flat solid exported through the real AP242 BRep path and re-import validated.
- [`ctc03-recovered.firmament`](ctc03-recovered.firmament) — explicit recovered planar/cylindrical regions, seven bends, two cuts, source-face bindings, thickness, K-factor, and recovery status. Recompilation reproduces the flat hash.

Secondary review artifacts:

- [`authored-u-channel-flat.svg`](authored-u-channel-flat.svg) — cut contours and two labeled 90° bend lines.
- [`ctc03-flat.svg`](ctc03-flat.svg) — recovered region contours, two slot profiles, and seven labeled bend lines.
- [`ctc03-flat-preview.png`](ctc03-flat-preview.png) — raster QA render of the SVG; not geometry authority.
