# CIR-RECOVERY-V1A: FrepMaterializer policy scaffold

## Why this exists

FRep/CIR to BRep is an intent recovery (decompilation) problem, not a direct syntax translation problem. Multiple bounded strategies may be plausible for one input, so policy scoring and admissibility must be explicit and deterministic.

## What this milestone adds

This milestone introduces production scaffolding under `Aetheris.Kernel.Firmament.Materializer`:

- `IFrepMaterializerPolicy`
- `FrepMaterializerContext`
- `FrepMaterializerCapability`
- `FrepMaterializerPolicyEvaluation`
- `FrepMaterializerDecisionStatus`
- `FrepMaterializerDecision`
- `FrepMaterializerPlanner`

The planner evaluates all policies, retains all evaluations (including rejected ones), and selects an admissible policy via `JudgmentEngine<TContext>`.

## Context shape (v1)

The context is node-first and intentionally minimal:

- `CirNode Root`
- optional `NativeGeometryReplayLog`
- optional `SourceLabel`

No lowering plan or execution-state dependency is required in V1A.

## JudgmentEngine usage

`FrepMaterializerPlanner` maps each policy evaluation to a `JudgmentCandidate<FrepMaterializerContext>` and uses score as utility. Determinism follows `JudgmentEngine` ordering: score, tie-break priority, name, declaration order.

## Not included in V1A

- no production `BoxCylinderThroughHolePolicy`
- no BRep body emission
- no STEP export wiring
- no rematerializer integration changes

## Next milestone

CIR-RECOVERY-V1B should add a first production semantic policy (`BoxCylinderThroughHolePolicy`) that emits diagnostic-rich evaluation only (or bounded exact path if explicitly scoped), while preserving existing behavior gates.
