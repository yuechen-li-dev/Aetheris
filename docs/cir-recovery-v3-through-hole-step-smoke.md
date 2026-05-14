# CIR-RECOVERY-V3 — bounded STEP smoke for through-hole recovery executor output

## Scope

CIR-RECOVERY-V3 validates a narrow internal smoke path:

`CIR -> ThroughHoleRecoveryPolicy -> ThroughHoleRecoveryPlan -> ThroughHoleRecoveryExecutor -> BrepBody -> Step242Exporter`

This milestone only proves that executor-produced BRep for canonical/translated `Subtract(Box,Cylinder)` can be exported by the **existing** STEP exporter APIs.

## Pipeline exercised

1. Build CIR root (`CirSubtractNode` with box host and cylinder tool).
2. Run `FrepMaterializerPlanner.Decide(..., [ThroughHoleRecoveryPolicy])`.
3. Extract `ThroughHoleRecoveryPlan` from selected policy evaluation.
4. Execute `ThroughHoleRecoveryExecutor.Execute(plan)` to produce exact `BrepBody`.
5. Export with unchanged exporter API:
   - `Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)`

## Success/failure policy

The smoke tests classify outcomes at pipeline boundaries:

- planner selected through-hole policy,
- executor produced body,
- STEP export attempted,
- STEP export succeeded,
- unsupported input rejected before export,
- STEP export skipped due no recovered body.

## Non-goals preserved

- no STEP exporter behavior changes,
- no special exporter route,
- no rematerializer/fall-forward wiring,
- no `NativeGeometryRematerializer` integration,
- no public CLI/API behavior change,
- no generic boolean or generic through-hole recovery expansion,
- no topology naming generation.

## SEM-A0 alignment

SEM-A0 guardrails remain preserved: no generated topology naming or new naming-provenance surface introduced in V3.

## Next milestone

CIR-RECOVERY-V4 should wire this bounded path into controlled rematerialization/fall-forward integration while preserving exporter behavior and keeping diagnostics explicit.
