# CIR-RECOVERY-V7A: CounterboreVariant recognition + HoleRecoveryPlan only

## Scope

V7A adds a bounded `CounterboreVariant` under `HoleRecoveryPolicy`.

- Included: CIR recognition for canonical counterbore-like nested subtracts and semantic `HoleRecoveryPlan` emission.
- Excluded: counterbore BRep executor, STEP export updates, public API changes.

## Supported CIR shape

Primary admitted shape:

`Subtract(Subtract(Box, SmallThroughCylinder), LargeShallowCylinder)`

With constraints:

- host is `CirBoxNode` (translation wrappers allowed),
- both tools are `CirCylinderNode` (translation wrappers allowed),
- cylinders are coaxial in XY,
- `Large.Radius > Small.Radius`,
- small cylinder is through-hole relative to host depth,
- large cylinder is blind/shallow and touches one entry side of host.

## Semantic plan shape

Admitted variant produces `HoleRecoveryPlan` with:

- `HoleKind = Counterbore`
- `DepthKind = ThroughWithEntryRelief`
- `EntryFeature = Counterbore`
- profile stack of two cylindrical segments:
  1. larger-radius shallow segment,
  2. smaller-radius through segment.

Expected topology semantics include:

- retained host planar faces,
- counterbore floor annulus,
- counterbore wall,
- through cylindrical wall,
- circular rim trims.

## Rejection behavior

`CounterboreVariant` rejects:

- non-nested subtract shapes,
- non-box host or non-cylinder tools,
- unsupported transforms,
- non-coaxial cylinders,
- `Large.Radius <= Small.Radius`,
- large cylinder through full depth,
- large cylinder that does not touch an entry face,
- any inner small-cylinder case that is not a valid through-hole.

## Rematerializer behavior in V7A

The rematerializer may select `CounterboreVariant` and extract a `HoleRecoveryPlan`, but conversion to `ThroughHoleRecoveryExecutor` contract intentionally fails with unsupported-plan diagnostics. No false BRep success is claimed.

## Locality of change

V7A remains local to hole policy variant composition:

- add `CounterboreVariant`,
- register in `HoleRecoveryPolicy` variant list,
- extend semantic hole enums minimally for counterbore plan vocabulary,
- add focused tests.

No planner, catalog architecture, or executor implementation changes are required.

## V7B recommendation

Implement a dedicated counterbore executor adapter/contract that consumes `HoleRecoveryPlan(HoleKind.Counterbore)` and emits bounded exact BRep for the admitted canonical shape.
