# Code Native Design: first hosted part build

Outcome: **Success**. The real downstream GitHub Actions path builds two
parametric parts on Ubuntu, exports their engineering/display artifacts, and
checks reconstruction of each final PDF before uploading. No kernel changes
were needed for this integration.

## Revisions and run

- Repository: https://github.com/yuechen-li-dev/CodeNativeDesignDemo
- Demo revision: `64ee70615d5144199b73e717bd568f987d893538` (`main`).
- Aetheris revision: `81918d9c14a624f1168cd3919c2df4cc0c8b27ba`.
- Managed compiler dependency: `70b6cbe742ecfb2947c9c6a31cfa8e1d3a89067b`,
  checked against Aetheris's existing reviewed source hash before packing.
- Hosted run: https://github.com/yuechen-li-dev/CodeNativeDesignDemo/actions/runs/39706776957
- Environment: `ubuntu-24.04`, .NET SDK `10.0.401`, Python `3.12`, pypdf `6.10.0`.
- Hosted regression result: **35 passed, 0 failed, 0 skipped**.

The short consumer calls the existing `MakeBracket` twice with named millimetre
dimensions. The existing typed Record/Template owns two Rect2 guides, six-edge
trace, extrusion and countersunk through hole. Compiler, kernel, drawing layout
and exporters remain in Aetheris. The demo does not duplicate these owners.

## Delivered evidence

The uploaded `bracket-64ee70615d5144199b73e717bd568f987d893538` artifact contains
nominal and resized cases, each with final STEP AP242, GLB, PDF, SVG, PPTX, source,
Drawing IR, built-validation report, qualification/hash report, PDF-only source
reconstruction and its compiled STEP. `run.json` records the revisions, clean demo
source and hosted run URL. Intermediate `drawing-base` projections are excluded.
The separate test artifact contains the actual TRX report. Retention is 30 days.

| Witness | Nominal | Resized |
| --- | --- | --- |
| Overall bounds, mm | 80 × 60 × 12 | 100 × 75 × 15 |
| Through / entry diameters, mm | 6 / 12 | 8 / 14 |
| Hole X / Y from O, mm | 60 / 10 | 70 / 12 |
| Countersink included angle | 90° | 90° |
| Built validation | UnifiedMechanical | UnifiedMechanical |
| PDF-only reconstruction | Matching geometry | Matching geometry |
| Incomplete drawing mutations rejected | 9 | 9 |
| Supported PMI in STEP / GLB | Datum + shaft diameter | Datum + shaft diameter |

Both downloaded STEPs reimported through the real CLI as enclosed manifold
single bodies: 22 faces, 50 edges, 30 vertices, with plane/cylinder/cone supports.
Both downloaded PDFs were independently reconstructed again on Windows and
visually rendered/reviewed. The PDF reader freezes its Path-based reconstruction
before opening the STEP reference and compares bounds, topology, vertices,
analytic cylinder/cone supports and evaluated volume.

## Cross-platform comparison

Against the previously qualified Windows rehearsal, for both cases:

- STEP and PDF files are byte-identical.
- SVG text is identical after newline normalization; file bytes differ by line endings.
- GLB binary mesh buffers are byte-identical, with no JSON structural differences.
- GLB JSON differences are confined to numeric PMI display metadata. Maximum
  absolute differences are `7.105427357601002e-15` for nominal and
  `1.7763568394002505e-15` for resized. No differences occur outside PMI extras.

These display-metadata differences do not change engineering geometry. They are
not evidence of cross-platform byte determinism for every export format.

## Qualification boundaries

The exact Aetheris implementation had already passed its full local solution
qualification (5,914 tests across 20 assemblies) in the preceding composed-hole
repair. Its separate [upstream Linux full-solution run](https://github.com/yuechen-li-dev/Aetheris/actions/runs/39706010507)
also completed successfully: **5,906 passed, 0 failed, 7 skipped, 5,913 total,
20 assemblies**. The skips are existing local 3DM fixture tests: the private local
Cartesian fixture is absent on the hosted runner. These are reported as skips,
not counted as passing tests. The full suite was run by upstream CI, while this
downstream job ran its 35 focused regressions and actual artifact integration checks.

The drawing defines nominal outline, thickness, hole location/type/extent/axis,
countersink entry/angle, linear ±0.1 mm and angular ±0.5° limits, material and
origin. Supported datum/shaft PMI is in AP242 and GLB; additional drawing limits
are not claimed as fully encoded AP242 PMI. Numerical mass and analytic
construction volume remain separately identified.

This remains an unsigned engineering-review artifact with a bounded breakout/
remaining-shaft tolerance screen on ideal orthogonal stock. Process capability,
stock form, inspection acceptance and manufacturing approval remain unqualified.
Checked/approved names and dates are blank. No composed-fillet or general body
editing capability was added by the demo.

## Local evidence

Ignored evidence root: `artifacts/local/code-native-design-first-run/`:

- `actions.log`: downloaded real hosted job log.
- `aetheris-full-actions.log`, `upstream-full-summary.json`: separate upstream
  full-solution CI results for the pinned compiler revision.
- `download/`: original hosted artifacts and TRX, including original `run.json`.
- `download-check.json`: artifact hash, validation and PMI checks after download.
- `cross-platform-parity.json`: per-format Windows/Linux comparison.
- `windows-reconstruction/`: independent reconstructions from downloaded PDFs.
- `nominal-inspect.json`, `variant-inspect.json`: real CLI STEP inspection.
- `nominal-drawing.png`, `variant-drawing.png`: rendered downloaded PDFs.

The demo checkout is separate from Aetheris. No external demo repository was
modified during the preceding rehearsal; this run was the explicitly authorized
first publication and real hosted execution.
