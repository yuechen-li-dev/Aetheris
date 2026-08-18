# CIR-RECOVERY-V18: placement-driven hole executor harmonization

## Purpose
Harden executor contract consumption so hole-family execution uses explicit `HoleProfileSegment` placement (`AnchorSide`, `ZMin`, `ZMax`, `IsThrough`, `PlacementDiagnostics`) as plan source-of-truth, instead of recomputing placement from variant folklore.

## Executor placement contract
- Tool height = `segment.ZMax - segment.ZMin`.
- Tool center Z = `(segment.ZMin + segment.ZMax)/2`.
- XY translation remains from plan `ToolTranslation`.
- Through semantics must be explicit (`IsThrough=true`, `AnchorSide=Through`, host z coverage).
- Blind semantics must be explicit (`AnchorSide=Top|Bottom`).

## Helper shape
`HoleRecoveryExecutor` now includes compact internal placement helpers:
- `TryValidateExecutablePlacement(...)`
- `TryBuildPlacementCylinderTool(...)`
- `TryBuildPlacementConeTool(...)`

## Converted executor paths
- Blind-hole cylinder path consumes `ZMin/ZMax`.
- Counterbore small-through + large-relief cylinders consume `ZMin/ZMax`.
- Countersink and chamfered-entry cylinder + cone segments consume `ZMin/ZMax`.

## Unchanged paths
- Through-hole still delegates through `ThroughHoleRecoveryPlanAdapter` to `ThroughHoleRecoveryExecutor` for low-risk behavior preservation in V18.
- Stepped-hole remains intentionally not production-enabled; V18 keeps explicit placement validation diagnostics and current deferred/non-success execution behavior.

## Validation and diagnostics
Validation occurs before boolean subtract on converted paths; failures return `UnsupportedPlan` with explicit `hole-executor: placement-validation-failed ...` diagnostics.

Stable breadcrumb:
`hole-executor: placement-driven segment=<role> anchor=<side> zMin=<...> zMax=<...>`

## Future author rules
Executor rules:
1. Use segment ZMin/ZMax to construct tool height and center.
2. Validate placement before Boolean.
3. Do not infer anchor side from radius order or variant name.
4. Unknown anchor is not executable.
5. New variants must add placement-driven executor tests.

## Non-goals
- No new hole variant.
- No stepped-hole re-enable.
- No generic profile-stack executor.
- No STEP exporter behavior/API changes.
