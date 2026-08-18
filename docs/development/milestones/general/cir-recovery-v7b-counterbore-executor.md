# CIR-RECOVERY-V7B: Counterbore executor for bounded HoleRecoveryPlan

## Scope

V7B adds bounded execution for `HoleRecoveryPlan` counterbores only:

- `HoleKind.Counterbore`
- `DepthKind.ThroughWithEntryRelief`
- `HostKind.RectangularBox`
- `Axis=Z`
- exactly two cylindrical profile segments:
  - entry relief (larger, shallow)
  - through core (smaller, through)

Non-goals: countersink, blind-hole, generic profile-stack execution, STEP behavior changes, new topology builders.

## Execution route

`HoleRecoveryExecutor.Execute(plan)` now executes hole-family plans.

- Through-hole plans are delegated to `ThroughHoleRecoveryExecutor` via `ThroughHoleRecoveryPlanAdapter`.
- Counterbore plans are validated then executed by exact boolean route:
  1. `CreateBox`
  2. `CreateCylinder` for small through core
  3. `BrepBoolean.Subtract(box, small)`
  4. `CreateCylinder` for large shallow relief
  5. `BrepBoolean.Subtract(result, large)`

## Geometry mapping and entry-side convention

Current bounded convention is **entry on host min-Z face** (topological entry face at `hostCenterZ - hostSizeZ/2`), matching the canonical V7A counterbore output.

- Small through cylinder
  - radius = smaller profile segment radius
  - height = `max(ThroughLength, HostSizeZ)`
  - center translation = `ToolTranslation`
- Large counterbore cylinder
  - radius = larger profile segment radius
  - height = shallow segment depth (`DepthEnd - DepthStart`)
  - center z = `entryFaceMinZ + shallowDepth/2`
  - x/y = tool translation x/y

Unsupported entry-side conventions are rejected explicitly.

## Diagnostics

Executor diagnostics include:

- executor started
- no STEP export attempted
- plan kind inspection and acceptance/rejection
- through-hole delegation
- counterbore profile validation
- small tool build + first subtract outcome
- large tool build + second subtract outcome
- body produced

Rematerializer now records that hole-family plan execution was attempted through `HoleRecoveryExecutor`.

## V7C recommendation

Add counterbore STEP smoke coverage without changing exporter behavior; keep execution bounded and exact-BRep first.
