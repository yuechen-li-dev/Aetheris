# CIR-F2: bounded CIR mirror + state-backed differential harness

## Summary

CIR-F2 adds a bounded, non-authoritative CIR mirror to `NativeGeometryState` for models that already succeed in the production BRep-first path.

Production remains unchanged:

- Firmament lowering and eager BRep execution are authoritative.
- STEP export still reads the materialized BRep path.
- CIR mirror failures never fail production success in this milestone.

## Mirror model

`NativeGeometryState` now carries `CirMirror` with:

- `Status`: `NotAttempted`, `Available`, `Unsupported`, `Failed`
- optional summary (bounds + estimated volume)
- diagnostics list

## Construction path

For successful primitive execution, executor attempts mirror construction by:

1. lowering the existing `FirmamentPrimitiveLoweringPlan` through `FirmamentCirLowerer`,
2. running `CirNativeAnalysisService.AnalyzeNode` when lowering succeeds,
3. persisting bounded mirror diagnostics in state.

Replay-log reconstruction is intentionally deferred; this milestone uses existing lowering plan directly to avoid duplicating lowerer behavior.

## Supported vs unsupported behavior

- Supported subset: mirror is `Available` and includes bounded summary diagnostics.
- Unsupported CIR subset (for example rounded-corner box): mirror is `Unsupported` with lowerer diagnostics.
- Analysis-only failure after successful lowering: mirror is `Failed` with analysis diagnostics.

In all three cases, successful BRep execution remains successful and authoritative.

## Differential harness

A state-backed helper compares BRep materialized bounds against mirrored CIR bounds + mirror volume summary by consuming `NativeGeometryState` directly.

This is diagnostic-focused and keeps CIR-side comparison attached to execution state, not a separate ad hoc reconstruction path.

## Relation to fall-forward

This milestone is preparation for future fall-forward work only:

- no `CirOnly` transition enablement,
- no CIR→BRep rematerializer,
- no export contract changes,
- no public CLI behavior change.

## Recommended CIR-F3

Implement the first tightly-bounded runtime fall-forward transition for one proven unsupported-materialization family, gated by explicit state transitions and preserved SEM-A0 provenance guardrails.
