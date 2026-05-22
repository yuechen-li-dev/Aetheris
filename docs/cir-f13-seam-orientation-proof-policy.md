# CIR-F13 — seam self-closure + orientation proof policy (`Subtract(Box,Cylinder)`)

## Purpose

CIR-F13 strengthens shell-stitching dry-run evidence for bounded `Subtract(Box,Cylinder)` by adding:

- cylindrical seam self-closure accounting,
- orientation compatibility evidence for token-paired planar/cylindrical circular boundaries,
- conservative readiness promotion rules backed by explicit blockers.

No shell assembly is implemented.

## Seam self-closure evidence

`ShellStitchingDryRunResult` now emits `SeamClosureEvidence` entries with:

- `CylindricalSelfSeam` when cylindrical emission diagnostics include seam convention evidence (`seam-convention-applied`),
- `NoSeamRequired` for non-cylindrical patches,
- `SeamDeferred` when seam proof metadata is missing.

This uses internal emission diagnostics only (no geometry coincidence inference).

## Orientation compatibility evidence

`ShellStitchingDryRunResult` now emits `OrientationEvidence` entries for each planned pair:

- `Compatible` when pairing orientation policy is compatible,
- `Deferred` when pairing exists but policy-level compatibility is not proven,
- `Incompatible` reserved for explicit non-complementary policy evidence.

## Unpaired boundary reclassification

Unpaired boundaries are reclassified so cylindrical boundaries are not reported as generic blockers when a cylindrical patch already has `CylindricalSelfSeam` evidence.

## Readiness promotion rule

Dry-run readiness can become `ReadyForAssemblyEvidence` only when:

- no unsupported patches,
- no unpaired boundary blockers,
- no seam deferred/unsupported evidence,
- no deferred/incompatible orientation evidence,
- at least one pair with compatible orientation evidence.

Otherwise readiness remains `Deferred`.

## Example (`Subtract(Box,Cylinder)`)

Expected bounded outcome:

- planar retained patch-set evidence present,
- cylindrical retained wall patch evidence present,
- seam accounted when seam convention metadata is present,
- planar/cylindrical pair orientation evidence emitted,
- shell assembly still explicitly not implemented.

## JudgmentEngine usage decision

JudgmentEngine was not added in CIR-F13 because seam/orientation/readiness are deterministic reductions over existing bounded evidence states with no competing candidate strategies.

## SEM-A0 guardrail status

Preserved:

- no generated topology naming,
- no public topology selector changes,
- internal diagnostics/evidence only.

## Next step

Implement actual shell assembly as a separate milestone only after CIR-F13 evidence is stable across broader bounded subtract variants.
