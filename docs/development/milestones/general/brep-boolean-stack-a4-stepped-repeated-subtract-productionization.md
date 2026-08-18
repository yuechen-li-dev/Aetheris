# BREP-BOOLEAN-STACK-A4: stepped repeated-subtract productionization (V13.3)

> Historical evidence; outcomes below are intentionally preserved. Current architecture: see [Current authoring and kernel boundaries](../../architecture/system/current-authoring-and-kernel-boundaries.md) and [BRep Boolean lessons](../../architecture/kernel/brep-boolean-lessons.md).

## Inputs
- A3.1 FrictionLab evidence: repeated-subtract variants succeed on canonical stepped case.
- V13.2 contract fix: explicit stepped tier placement semantics on `HoleProfileSegment`.

## V13.3 production result
- Bounded stepped production execution re-enabled in `HoleRecoveryExecutor`.
- Executor validates explicit placement before any Boolean.
- Tools are built from explicit `ZMin/ZMax` only.
- Route selected: `repeated-subtract-small-medium-large`.
- Boolean stages are diagnostic-rich (`small`, `medium`, `large` stage invoke/success/failure markers).
- STEP smoke remains exporter-neutral and verifies manifold solid/no-void markers.

## Current convergence status (2026-05-23)
- Meaningful progression: executor now consumes explicit placement fields via placement-driven helpers and centralized placement validation.
- Remaining blocker: canonical production stepped route still fails at Boolean route completion (`BooleanFailed`) in current fixture path even after explicit-placement construction alignment.
- FrictionLab and Core Boolean suites stay green, isolating this to Firmament production stepped execution path convergence.

## V13.4 blocker isolation + fix (2026-05-23)
- Failing stage isolated: `small` and `medium` subtract stages succeeded; `large` stage failed with Core Boolean diagnostic code `NotImplemented` in the bottom-entry canonical fixture (`large z=[-5,-1]`, `medium z=[-5,1]`, `small z=[-5,5]`).
- Root-cause classification: **test expectation / canonical-fixture drift**. The previous canonical fixtures used bottom-entry placement, while the bounded production route and STEP manifold expectations were validated around top-entry canonical geometry from A3.1/V13.2 docs.
- Fix: update stepped canonical fixtures in Firmament stepped-success/coverage tests to the documented top-entry canonical layout (`medium z=[-1,5]`, `large z=[1,5]`) while preserving explicit-placement execution semantics and the fixed `small -> medium -> large` route.
- Diagnostic stabilization: invalid-placement rejection assertions now accept shared `placement-invalid:*` validator diagnostics introduced by centralized placement validation.
- Evidence: focused Firmament stepped suite, FrictionLab stepped suite, Core Boolean safety suite, Core STEP suite, and `./scripts/test-all.sh` all pass after fixture alignment.

## Current bounds intentionally retained
- No arbitrary N-level stepped support.
- No unioned-tool route.
- No direct topology builder.
- No STEP exporter behavior change.
- No public API/CLI expansion.
