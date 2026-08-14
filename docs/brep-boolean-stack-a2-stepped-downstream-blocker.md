# BREP-BOOLEAN-STACK-A2 stepped-hole downstream blocker after A1/A1.1 validator fix

> Historical evidence; outcomes below are intentionally preserved. Current architecture: see [Current authoring and kernel boundaries](architecture/current-authoring-and-kernel-boundaries.md) and [BRep Boolean lessons](kernel/brep-boolean-lessons.md).

## Outcome
A2 confirms validator admission succeeds but bounded stepped execution still fails downstream in repeated subtract execution.

## Observed behavior
- Validator continues admitting canonical N-level coaxial stepped continuation.
- Executor probe reaches small subtract but remains deferred with explicit post-validator blocker diagnostics.
- Execution remains deferred (`UnsupportedPlan`) with no returned body.
- STEP smoke is skipped for stepped because execution remains deferred and executor performs no STEP export.

## Safety boundary preserved
- Non-coaxial overlapping independent-hole stacks remain rejected with hole interference diagnostics.
- Counterbore N=2 remains green.
- No STEP exporter behavior changes.

## Scope guard
This milestone does not introduce generic N-level executor architecture; it restores bounded stepped execution only for canonical three-level cylindrical stepped-hole plans.
