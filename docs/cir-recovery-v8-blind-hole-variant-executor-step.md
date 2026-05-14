# CIR-RECOVERY-V8: bounded blind-hole variant + executor + STEP smoke

## Scope

Supported bounded CIR shape (after optional identity/translation wrappers):

- `Subtract(Box, Cylinder)`
- box host only
- cylinder tool only
- Z-axis canonical orientation
- translation-only transforms
- cylinder intersects exactly one host Z face and terminates inside host (blind bottom inside)
- strict XY clearance (no tangent/grazing)

## Semantics

Admitted cases produce `HoleRecoveryPlan` with:

- `HoleKind = Blind`
- `DepthKind = Blind`
- `EntryFeature = Plain`
- `ExitFeature = ClosedBottom`
- single cylindrical profile segment from entry to blind bottom
- expected patches include retained host faces, cylindrical blind wall, and blind bottom cap
- expected trims include circular entry/bottom trims

## Execution

`HoleRecoveryExecutor` now supports bounded blind-hole plans directly:

1. `BrepPrimitives.CreateBox`
2. `BrepPrimitives.CreateCylinder(radius, depth)`
3. position tool by entry-side convention inferred from plan (`top(+Z)` or `bottom(-Z)`)
4. `BrepBoolean.Subtract`

No generic profile-stack executor was added.

## STEP smoke

Smoke validates export via existing API:

- `Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)`
- expected markers: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS` is not expected for this open blind depression

Exporter behavior is unchanged.

## Rejections

Stable rejection/defer diagnostics remain for:

- non-subtract, nested/composite booleans
- non-box host / non-cylinder tool
- unsupported transforms
- through-hole spans
- missing entry-face intersection
- bottom outside host
- tangent/grazing/outside XY radius clearance

## Non-goals kept

- no countersink work
- no counterbore changes
- no generic Boolean recovery
- no STEP exporter behavior changes
- no public CLI/API surface expansion

## Recommended V9

Generalize bounded blind-hole coverage to additional entry-side and transform compositions with explicit admissibility/score tuning and shape-lab updates before any generic stack executor work.
