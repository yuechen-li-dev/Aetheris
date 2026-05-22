# CIR-RECOVERY-V7C: bounded counterbore STEP smoke / exporter regression

## Scope

V7C adds focused regression coverage proving the existing bounded counterbore semantic recovery path produces a STEP-exportable BRep via existing exporter APIs only:

`CIR -> HoleRecoveryPolicy -> CounterboreVariant -> HoleRecoveryExecutor -> BrepBody -> Step242Exporter.ExportBody(...)`.

No STEP exporter behavior was changed.

## Pipeline exercised

`CounterboreRecoveryStepSmokeTests` executes canonical and translated counterbore inputs through:

1. `FrepMaterializerPlanner.Decide(..., [new HoleRecoveryPolicy()])`
2. selected `HoleRecoveryPlan` (counterbore variant)
3. `HoleRecoveryExecutor.Execute(plan)`
4. `Step242Exporter.ExportBody(body, new Step242ExportOptions { ProductName = ... })`

The tests assert diagnostic breadcrumbs for policy selection, variant selection, executor output, and STEP export attempt/success.

## STEP exporter API

The smoke route uses the current API:

- `Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)`

No special exporter route or semantic-only export path is introduced.

## Marker assertions

Counterbore STEP smoke asserts output markers:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `CYLINDRICAL_SURFACE`

and explicitly asserts `BREP_WITH_VOIDS` is absent for this bounded through+entry-relief tunnel topology.

## Unsupported behavior coverage

A non-coaxial counterbore-like case is intentionally rejected before execution/export.

Assertions confirm:

- no selected policy,
- no plan/body,
- export skipped,
- diagnostics include the precise rejection cause (`UnsupportedNonCoaxialCylinders`).

## Manifold vs void policy

V7C keeps the existing STEP root behavior untouched:

- bounded counterbore tunnel exports as `MANIFOLD_SOLID_BREP`,
- `BREP_WITH_VOIDS` remains reserved for explicit inner-shell/void-shell representations.

## Non-goals reaffirmed

V7C does not implement:

- exporter behavior changes,
- countersink/blind-hole recovery,
- generic profile-stack execution,
- CLI/API expansion,
- topology naming changes.

## Next milestone candidate

CIR-RECOVERY-V8 can extend from this smoke baseline toward broader semantic-hole family coverage (for example bounded blind-hole candidates) while keeping exporter behavior-preserving regression gates.
