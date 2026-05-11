# CIR-T8: Tiered trim integration into retained-loop/materialization evidence

## Purpose

Integrate `TieredTrimCurveRepresentation` (CIR-T7) into retained-loop descriptors and readiness evidence without emitting BRep topology or STEP entities.

## Integration shape

- `RetainedRegionLoopDescriptor` now carries:
  - `OppositeSurfaceProvenance`
  - `OracleTrimRepresentation` (optional `TieredTrimCurveRepresentation`)
- `RetainedLoopTrimOracleIntegrator` runs inside candidate generation for subtract roots and attaches restricted-field trim oracle evidence per retained loop.
- `MaterializationReadinessAnalyzer` adds `trim-oracle-evidence` layer.

## Behavior

- Box face vs cylinder: analytic-circle candidate + numerical contour evidence attached; export candidate only; not exported.
- Box face vs sphere: analytic-circle candidate + numerical contour evidence attached; export candidate only; not exported.
- Box face vs torus: numerical/deferred/candidate-only route accepted; diagnostics explicitly preserve "torus generic exactness not claimed".

## Exactness/export policy

- `ExactStepExported` remains `false`.
- `BRepTopologyEmitted` remains `false`.
- No STEP curve entities are created.
- No BRep edge/coedge/loop emission is added.

## Non-goals

- topology emission
- STEP export behavior changes
- BSpline fitting
- torus exact/quartic recognition
- boolean/materializer behavior expansion

## Next (CIR-T9)

Add deterministic loop-to-opposite provenance matching for multi-opposite fields so attached tiered trim evidence can be pair-specific when a source face sees multiple opposite operands.
