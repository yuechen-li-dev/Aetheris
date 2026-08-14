# AETHERIS-SHEETMETAL-M2 — CTC-03

This bundle demonstrates the intended authority chain:

1. `recovery-summary.json` and `recovered-draft.firmament` are deterministic forensic machine evidence.
2. `reconstruction-brief.md` is bounded context for an engineer/LLM.
3. `ctc03-idiomatic.firmament` is the checked-in reconstructed engineering interpretation.
4. `ctc03-comparison.md` records deterministic compiler verification; the CLI can emit the same structured residuals as JSON on demand.
5. `ctc03-flat.step` is a 1.905 mm physical AP242 flat solid; `ctc03-flat.svg` is the review view.

The machine source has 264 lines and 24 region/bend/cut declarations. The reconstructed source has 103 lines and 24 explicitly accounted declarations. Its compression comes from moving 3D boundary coordinates and raw face IDs into forensic evidence while retaining semantic feature coverage.

Result: `PassWithKnownDifferences`. CTC-03 remains `Partial` as automatic recovery because exact global blank stitching, arbitrary analytic outer arcs, and authoritative corner/relief intent remain bounded gaps.

Reproduce:

```text
aetheris sheetmetal recover <ctc03.step> --out-dir docs/modules/sheetmetal/artifacts/m2
aetheris sheetmetal inspect docs/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament
aetheris sheetmetal compare <ctc03.step> docs/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament
aetheris sheetmetal flatten docs/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament --step docs/modules/sheetmetal/artifacts/m2/ctc03-flat.step --svg docs/modules/sheetmetal/artifacts/m2/ctc03-flat.svg
```
