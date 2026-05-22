# CIR-BREP-X3.1 — Firmament lowering shape and recognition entrypoint verification lab

## 1) Setup verification results

- Verified prior CIRLab code exists under `Aetheris.Firmament.FrictionLab/CIRLab` and prior tests exist under `Aetheris.FrictionLab.Tests/CIRLab`.
- Verified prior CIR-BREP-X3 report exists under tests reports and mirrored it into `docs/frictionlab/cir-brep-x3-box-cylinder-recognition-lab.md` to keep docs path coherent.
- Verified no production behavior change was required for setup corrections.

## 2) Exact code paths inspected

- `Aetheris.Kernel.Firmament/FirmamentCompiler.cs`
- `Aetheris.Kernel.Firmament/Lowering/FirmamentPrimitiveLowerer.cs`
- `Aetheris.Kernel.Firmament/Lowering/FirmamentPrimitiveLoweringPlan.cs`
- `Aetheris.Kernel.Firmament/Lowering/FirmamentCirLowerer.cs`
- `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs`
- `Aetheris.Kernel.Firmament/Execution/NativeGeometryState.cs`
- `Aetheris.Kernel.StandardLibrary/StandardLibraryReusableParts.cs`
- `Aetheris.Kernel.StandardLibrary/StandardLibraryPrimitives.cs`
- `Aetheris.Firmament.FrictionLab/CIRLab/*`
- `Aetheris.FrictionLab.Tests/CIRLab/*`

## 3) Experiment 1: real Firmament fixture lowering shape

Fixture: `testdata/firmament/examples/boolean_box_cylinder_hole.firmament`.

Result:
- Lowered CIR root is `CirSubtractNode`.
- Both operands are `CirTransformNode` wrappers (not raw primitive direct children).
- Wrapped children are `CirBoxNode` (lhs) and `CirCylinderNode` (rhs).
- Replay operation for `hole` is `boolean:subtract`, source `base`, tool kind `cylinder`.

Recommendation:
- Production recognizer must support wrapper normalization for translated operands from day one.

## 4) Experiment 2: reusable part / library path

`StandardLibraryReusableParts.CreateCubeWithCylindricalHole()` returns a reusable part with a materialized body and part name, i.e. a direct reusable geometry path, not a Firmament primitive-lowering-plan-first path.

Recommendation:
- Reusable part path should not define v1 CIR recognizer entrypoint scope.
- It can provide independent regression fixtures but is not the primary production recognizer input for Firmament canonical subtract.

## 5) Experiment 3: raw node vs plan vs replay recognizer comparison

| Source | Detect subtract box-cylinder | Recover dimensions/translation | Unsupported transform handling | Notes |
|---|---|---|---|---|
| Raw `CirNode` lowered tree | Yes | Yes | Yes (by transform inspection) | Most direct for geometric-shape recognition. |
| `FirmamentPrimitiveLoweringPlan` | Yes (feature/op semantics) | Partial (placement fields, not final composed CIR tree) | Semantic placement validation available | Good companion context; less direct for shape matching. |
| `NativeGeometryReplayLog` | Yes (`boolean:subtract`, tool kind) | Placement translation available in resolved form | Yes (resolved placement kind/diagnostic) | Strong diagnostics and provenance; not full CSG shape alone. |

Recommended first production entrypoint: node-first recognizer on lowered CIR tree, with optional replay diagnostics channel.

## 6) Experiment 4: transform wrapper normalization evidence

Existing lab normalizer successfully recognizes the real lowered fixture tree where both subtract operands are transform wrapped.

Recommendation:
- Production should accept translation transform wrappers in v1.
- Reject non-translation transforms for canonical v1 path.

## 7) Experiment 5: replay log usefulness

Replay log includes:
- operation index,
- feature id,
- operation kind,
- source feature id,
- tool kind/tool id,
- placement summary,
- resolved placement object.

Recommendation:
- v1 may ignore replay for decision-making if node tree is available.
- v1 should optionally include replay-backed diagnostics.
- v2 can add replay adapter as robustness/telemetry aid.

## 8) Experiment 6: NativeGeometryState / CIR mirror usefulness

`NativeGeometryState` exposes replay log, CIR intent root reference, and `CirMirror` summary state.

Observations:
- CIR mirror is useful for bounds/volume diagnostics and availability status.
- CIR mirror does not by itself replace explicit subtract operand typing needed for canonical box-cylinder recognition.

Recommendation:
- v1 recognizer should not consume `NativeGeometryState` as primary entrypoint.
- `NativeGeometryState` is useful as optional diagnostics envelope around node-first recognition.

## 9) Experiment 7: remaining uncertainty sweep

Remaining uncertainties:
1. Additional subtract lowering variants may exist for authored fixtures beyond current canonical fixture set.
2. Around-axis/rotational placement lowering is explicitly unsupported for CIR-M2 path and remains out-of-scope for canonical v1.
3. Some library/reusable part construction paths can bypass Firmament lowering, so fixtures should be separated by path type when used for production recognizer validation.

No blocker found for canonical v1 node-first recognizer scope.

## 10) Updated answer for item 6

Updated finding: **High confidence** that real `boolean_box_cylinder_hole` lowering shape is subtract with transform-wrapped box/cylinder operands. Wrapper normalization is required.

## 11) Updated answer for item 7

Updated finding: **High confidence** that canonical v1 production recognizer should be **raw lowered `CirNode` tree entrypoint first**; plan/replay/native-state adapters are optional support layers, not primary entrypoints.

## 12) Final production recommendation

- Entrypoint: lowered CIR node tree.
- Accepted shapes: `CirSubtractNode` with lhs box and rhs cylinder after translation-wrapper normalization.
- Wrappers: required for translation wrappers; reject rotation/non-translation in v1 canonical path.
- Adapters: replay and native-state optional diagnostics now; plan/replay adapter as v2 hardening.

## 13) Recommended production implementation milestone

Implement production canonical recognizer for:
- subtract(box, cylinder),
- translation-wrapper normalization,
- deterministic rejection diagnostics,
- optional replay-log attachment in diagnostic payload.

## 14) Confidence ratings

- Item 6 (lowering shape): **High**.
- Item 7 (entrypoint): **High**.
- Replay/native-state as primary recognizer source: **Medium** (useful diagnostics, insufficient as sole geometric source).
