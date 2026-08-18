# AIR-X2 blind/counterbore interval semantics lab (EVT)

## Purpose and EVT scope
Evaluate blind/counterbore AIR interval representations in lab-only code, without production route migration.

## Code/docs inspected
`AirProfileStackExtrude`, `ProfileStackExtrudeExecutor`, `ProfileStackExtrudePlanAdapter`, `HoleRecoveryPlan`, `HoleRecoveryExecutor`, `Step242Exporter`, AIR-X1/AIR-V1/AIR-V2A docs listed in milestone prompt.

## Blind-hole candidate matrix
- B1 NullInnerLoopSolidLayer: fails in `ProfileStackExtrudeExecutor` unsupported-shape gate.
- B2 ZeroRadiusInnerLoop: fails profile-stack validation invalid inner radius.
- B3 ExplicitBlindPocketDescriptor: skipped; current lab lacks AIR descriptor-lowering API.
- B4 SplitCapTransitionModel: skipped; cap transition metadata has no emitter.
- B5 LegacyBooleanBaseline: succeeds, BRep+STEP smoke green.

## Counterbore candidate matrix
- C1 ContiguousLayerRadii: succeeds with current profile-stack executor.
- C2 OverlappingToolIntervals: skipped; overlapping interval lowering adapter absent.
- C3 NormalizedSteppedStack: succeeds (same executable shape as C1).
- C4 DirectSafeCompositionDescriptor: skipped; no direct AIR descriptor API at lab surface.
- C5 LegacyBooleanBaseline: succeeds, BRep+STEP smoke green.

## STEP smoke results
For successful rows: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CYLINDRICAL_SURFACE` present; `BREP_WITH_VOIDS` absent.

## Failure analysis
Primary blocker for blind AIR in current model is `ProfileStackExtrudeExecutor` requirement that all layers must have positive inner radius.

## Semantic role clarity analysis
- Blind intent requires explicit "solid/no-hole" interval.
- Counterbore intent is clear with contiguous layered radii.

## Production migration complexity
- Blind requires model/emitter extension.
- Counterbore contiguous layers can migrate with lowest risk.

## Recommendations
- Blind AIR-V2B: **extend profile-stack layer model with explicit no-hole regions**.
- Counterbore AIR-V2B: **use contiguous layers**.

## What current AirProfileStackExtrude lacks
A first-class executable no-hole/solid interval consumable by `ProfileStackExtrudeExecutor`.

## Required AIR-V2B production changes
1. Add executable solid interval semantics in profile-stack emitter path.
2. Keep explicit diagnostics/failure-stage reporting.
3. Preserve legacy fallback until parity gates pass.

## Risks and guardrails
- Do not alter STEP exporter/core booleans in AIR-V2B.
- Keep deterministic matrix tests as migration guardrail.

## Confidence ratings
- Blind recommendation confidence: medium-high.
- Counterbore recommendation confidence: high.
