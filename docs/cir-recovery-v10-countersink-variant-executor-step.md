# CIR-RECOVERY-V10 — countersink variant/executor/STEP status

## Outcome for this iteration

**Meaningful progression**.

We added `CountersinkVariant` to the `HoleRecoveryPolicy` JudgmentEngine lane and made it emit stable, explicit rejection diagnostics that isolate the current blocker: CIR does not yet expose a cone primitive node, so there is no bounded way to recognize a countersink cone tool in the CIR tree.

## Blocker isolated

- Current CIR primitives are `Box`, `Cylinder`, `Sphere`, `Torus` (+ boolean and transform nodes).
- There is no `CirConeNode` / conical primitive available to represent the countersink entry tool in semantic recovery input.
- Because of that, V10 cannot safely implement bounded countersink recognition/execution from CIR without scope expansion into CIR primitive surface area.

## What landed now

- `HoleKind` gained `Countersink`.
- `HoleEntryFeatureKind` gained `Countersink`.
- `CountersinkVariant` now participates in `HoleRecoveryPolicy` variant evaluation.
- The variant returns deterministic rejection:
  - `UnsupportedMissingConePrimitiveInCir`
  - diagnostics clearly naming the missing cone primitive blocker.
- Added focused tests proving:
  - countersink variant is evaluated and reports the explicit blocker;
  - existing counterbore selection remains unchanged.

## Non-goals preserved

- No STEP exporter behavior changes.
- No `HoleRecoveryExecutor` countersink boolean path added yet.
- No CLI/API expansion.
- No generic profile-stack executor.

## Recommended V11

1. Introduce bounded CIR cone primitive support (`CirConeNode`) with translation-only transform conventions matching existing primitive lowering patterns.
2. Add `CountersinkVariant` bounded recognition for `Subtract(Subtract(Box,Cylinder),Cone)` with strict coaxial/entry/depth/radius checks.
3. Extend `HoleRecoveryExecutor` with cone subtract path using existing `BrepPrimitives.CreateCone` + `BrepBoolean.Subtract`.
4. Add countersink STEP smoke asserting `CONICAL_SURFACE`, `CYLINDRICAL_SURFACE`, and no `BREP_WITH_VOIDS`.


## Status update

Superseded in part by `docs/cir-recovery-v11-countersink-cone-execution-step.md`, which resolves the prior cone-primitive blocker for a bounded countersink shape.
