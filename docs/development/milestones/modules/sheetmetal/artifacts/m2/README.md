# AETHERIS-SHEETMETAL-M2 — CTC-03

This bundle demonstrates the intended authority chain:

1. `recovery-summary.json` and `recovered-draft.firmament` are deterministic forensic machine evidence.
2. `reconstruction-brief.md` is bounded context for an engineer/LLM.
3. `ctc03-idiomatic.firmament` is the checked-in reconstructed engineering interpretation. M3 subsequently promoted this same file to a self-contained Base/Flange/Cut model; the M2 evidence and outputs below remain a historical snapshot.
4. `ctc03-comparison.md` records deterministic compiler verification; the CLI can emit the same structured residuals as JSON on demand.
5. `ctc03-flat.step` is a 1.905 mm physical AP242 flat solid; `ctc03-flat.svg` is the review view.

The machine source has 264 lines and 24 region/bend/cut declarations. The reconstructed source has 103 lines and 24 explicitly accounted declarations. Its compression comes from moving 3D boundary coordinates and raw face IDs into forensic evidence while retaining semantic feature coverage.

Historical M2 result: `PassWithKnownDifferences`. Re-running the current self-contained M3 source now produces `NeedsReview`; see `../m3/` for current generated geometry and residuals. CTC-03 remains `Partial` as automatic recovery.

Reproduce:

```text
aetheris sheetmetal recover <ctc03.step> --out-dir docs/development/milestones/modules/sheetmetal/artifacts/m2
aetheris sheetmetal inspect docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament
aetheris sheetmetal compare <ctc03.step> docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament
aetheris sheetmetal flatten docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-idiomatic.firmament --step docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-flat.step --svg docs/development/milestones/modules/sheetmetal/artifacts/m2/ctc03-flat.svg
```
