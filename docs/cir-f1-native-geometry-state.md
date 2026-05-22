# CIR-F1: native geometry state + replay log skeleton

## What this milestone adds

CIR-F1 introduces internal execution-state scaffolding for the future BRep-first / CIR fall-forward architecture described in CIR-F0, while preserving current runtime behavior.

Added model elements:

- Execution mode: `BRepActive`, `CirOnly`, `Failed`
- Materialization authority: `BRepAuthoritative`, `CirIntentOnly`, `PendingRematerialization`
- `NativeGeometryState` envelope attached to primitive execution results
- `NativeGeometryReplayLog` with normalized per-operation entries
- `NativeGeometryTransitionEvent` with explicit reason categories

## What is intentionally inactive in CIR-F1

- No production transition to `CirOnly` is performed.
- No CIR replay execution is performed.
- No CIR-to-BRep materialization is performed.
- No change to Firmament public CLI behavior or STEP export semantics.

## Relationship to CIR-F0

CIR-F0 proposed state-machine and replay-first architecture as the first safe step before any runtime fall-forward.
CIR-F1 implements that recommendation by:

1. defining state/authority/transition diagnostics,
2. producing deterministic replay log records from existing lowering plans,
3. proving current successful execution is representable as `BRepActive`.

## Replay log usage (future)

The replay log is intended to become the deterministic source for lazy CIR reconstruction when future milestones enable fall-forward. In this milestone the log is write-only and diagnostic-focused.

## Behavior change statement

This milestone is behavior-preserving:

- existing eager BRep primitive/boolean execution remains authoritative,
- existing STEP export path remains authoritative,
- existing success/failure semantics remain unchanged.

## Recommended CIR-F2

CIR-F2 should consume this scaffold to add a bounded CIR mirror + differential harness for a minimal supported subset (without enabling production fall-forward yet).
