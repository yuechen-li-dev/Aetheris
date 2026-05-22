# CIR-BREP-T1: Planar patch-set tiered oracle trim preference

## Purpose
Integrate tiered oracle analytic-circle trim consumption into the real planar patch-set emission path, preferring admissible oracle evidence before binder-derived fallback.

## Policy
For planar retained candidates with one admissible inner-loop trim:
1. Attempt `EmitRectangleWithTieredInnerCircle(...)` when strong oracle evidence is present and admissible.
2. If it succeeds, mark route as `TieredOracleTrim` and record consumption diagnostics.
3. If rejected, preserve rejection diagnostics and attempt binder fallback only when binder circle evidence is independently admissible.
4. If binder succeeds, mark route as `BinderFallback`.
5. If neither path is admissible, skip with explicit diagnostics.

## Route diagnostics
`PlanarPatchSetEntry` now carries an internal route marker:
- `TieredOracleTrim`
- `BinderFallback`
- `UntrimmedRectangle`
- `Skipped`

Diagnostics distinguish oracle availability, rejection, consumption, and fallback.

## Identity metadata behavior
Oracle-route emission still passes through the bounded planar rectangle+inner-circle topology path, preserving emitted identity metadata for inner circular trim when token evidence exists. Missing token remains diagnosed and is never fabricated.

## Rejection cases
Oracle route remains rejected for deferred/unsupported/not-strong evidence, non-analytic-circle representation, unsafe UV/world conversion, or forbidden representation flags (`ExactStepExported`, `BRepTopologyEmitted`).

## Scope guardrails
- No shell assembly behavior change.
- No STEP export behavior change.
- No generic trim-to-BRep conversion.

## Next step
Use the route marker and diagnostics to drive bounded shell pairing analysis for planar/cylindrical emitted token match-up without broadening topology assembly scope.
