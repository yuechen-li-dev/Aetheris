# BREP-BOOLEAN-STACK-A4: stepped repeated-subtract productionization (V13.3)

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

## Current bounds intentionally retained
- No arbitrary N-level stepped support.
- No unioned-tool route.
- No direct topology builder.
- No STEP exporter behavior change.
- No public API/CLI expansion.
