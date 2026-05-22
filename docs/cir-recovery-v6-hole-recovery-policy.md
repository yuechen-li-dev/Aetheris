# CIR-RECOVERY-V6: HoleRecoveryPolicy scaffold with ThroughHoleVariant

## Summary
V6 replaces the default top-level exact semantic recovery policy from `ThroughHoleRecoveryPolicy` to `HoleRecoveryPolicy`, with a compositional variant architecture and one production variant: `ThroughHoleVariant`.

## Why this migration
This follows CIR-RECOVERY-X7's recommendation to avoid top-level policy explosion and keep hole-family growth local and diagnosable.

## Architecture
- `HoleRecoveryPolicy` (top-level policy)
- `IHoleRecoveryVariant` + `HoleRecoveryVariantEvaluation`
- `ThroughHoleVariant` (only implemented variant in V6)
- `HoleRecoveryPlan` + `HoleProfileSegment` as family plan contract

## Current plan shape (implemented)
- `HostKind = RectangularBox`
- `Axis = Z`
- `HoleKind = Through`
- `DepthKind = Through`
- `ProfileStack = [ Cylindrical ]`
- `EntryFeature = Plain`
- `ExitFeature = Plain`

## Executor compatibility strategy
V6 uses **Option A (adapter)**:
- `ThroughHoleRecoveryPlanAdapter.TryConvert(HoleRecoveryPlan)`
- Existing `ThroughHoleRecoveryExecutor` remains unchanged and continues executing the through-hole route.

## Default policy catalog (V6)
1. `HoleRecoveryPolicy` (`SemanticExact`)
2. `CirOnlyFallbackPolicy` (`CirOnlyFallback`)

`ThroughHoleRecoveryPolicy` remains in code for compatibility/direct use and tests, but is no longer in the default catalog.

## Fallback behavior
If no variant is admissible, `HoleRecoveryPolicy` rejects with aggregated variant reasons and catalog selection falls through to `CirOnlyFallbackPolicy`.

## Future extension pattern
To add counterbore/countersink/blind-hole:
1. add new `IHoleRecoveryVariant` implementation,
2. emit `HoleRecoveryPlan` segments/features,
3. register variant in `HoleRecoveryPolicy` deterministic list,
4. add focused variant + fallback diagnostics tests.

## Non-goals in V6
- No new counterbore/countersink/blind variants
- No new BRep executor algorithms
- No STEP exporter behavior changes
- No public CLI/API surface changes


## Update note (V7A)

`HoleRecoveryPolicy` now composes multiple hole-family variants (`ThroughHoleVariant`, `CounterboreVariant`) via JudgmentEngine, preserving the same policy-level integration boundary.
