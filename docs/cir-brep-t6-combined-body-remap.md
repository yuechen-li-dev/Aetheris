# CIR-BREP-T6: combined-body remap scaffold

- Reconciled T5 focused test gate: broad focused filter passes.
- Added `CombinedPatchBodyRemapper.TryCombine(...)` to copy emitted patch bodies into a deterministic combined partial `BrepBody`.
- Scope is copy/remap only: no shared-edge/coedge mutation and no edge/vertex coalescing.
- Remaps identity-map references (`face/loop/edge/coedge/vertex`) into combined-body ids and returns explicit remap records.
- Validation is bounded: copied topology + bindings are rebuilt deterministically; diagnostics report remap readiness.
- Stitch executor now reports `combined-body-remap-ready` when refs are concrete and remap succeeds, but still reports shared-edge mutation not implemented.
- Guardrails remain: `SharedEdgeMutationApplied=false`, `FullShellClaimed=false`, `StepExportAttempted=false`.

## Next

CIR-BREP-T7 should consume remapped references and apply one bounded shared-edge/coedge rewrite with topology invariant checks.
