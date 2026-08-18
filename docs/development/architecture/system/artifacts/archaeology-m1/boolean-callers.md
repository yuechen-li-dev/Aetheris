# BRep Boolean caller map

## Public surface

`BrepBoolean.Union`, `Subtract`, and `Intersect` are thin calls to `Execute(BooleanRequest)`. `Execute` validates, classifies with `JudgmentEngine`, computes bounded evidence, selects a supported family, rebuilds explicit topology, and validates bindings/manifold structure. `ClassifyBooleanCase` is public but no external production caller was found; `Execute` is called through the operation wrappers.

## Production callers

| Caller | Operation | Intent | Current assumption | Future destination | Migration risk |
|---|---|---|---|---|---|
| `FirmamentPrimitiveExecutor.ExecuteBoolean` | union/subtract/intersect | `LEGACY_EXECUTION` plus shared V2 primitive bridge | generic-looking op over bodies; succeeds only for admitted dispatcher families | compatibility facade during M2-M5; V2 callers move to construction recipes | high: removing fallback breaks V1 build and some V2 primitive lowering |
| `ThroughHoleRecoveryExecutor` | box - cylinder | `KNOWN_CONSTRUCTION` | exact box host and through-cylinder tool are already known | `ThroughHoleRecipe` -> Surgery | low: strongest first recipe migration candidate |
| `HoleRecoveryExecutor` counterbore/stepped/blind/countersink variants | repeated subtract | `KNOWN_CONSTRUCTION` | policy already knows end condition, coaxial tiers, placement, and tool type | family recipes (`BlindHole`, `Counterbore`, `SteppedCoaxial`, `Countersink`) -> Surgery | medium/high: composition order/history and STEP regressions matter |
| `CirBrepMaterializer` recognized box-cylinder and box-box nodes | subtract | `IMPORT_RECOVERY` / recognized construction | recognizer has already bounded operand kinds and translations | recognized CIR recovery recipe or compatibility facade | medium: recursive CIR shape does not supply missing topology intent |
| `StandardLibraryReusableParts.CreateCubeWithHole` | subtract | `KNOWN_CONSTRUCTION` | fixed standard cube-with-through-hole recipe | standard part construction recipe -> Surgery | low; preserve exact canonical output |
| server `KernelEndpoints` Boolean endpoint | all three | `GENERIC_USER_BOOLEAN`, `SERVER_EXPERIMENTAL` | clients can submit two primitive descriptors and an op, while actual kernel is bounded | compatibility facade with explicit supported-family capability response; eventually advanced/unsafe API or remove | high: externally visible generic promise exceeds implementation |
| `ProfileStackExtrudeExecutor` | no direct call, constructs `SafeBooleanComposition` and calls builder | `KNOWN_CONSTRUCTION` bypassing facade | complete interval/profile stack is already known | direct hole-stack recipe -> Surgery | medium; evidence that intent can bypass general dispatch |

No direct production `BrepBoolean` call was found in `Aetheris.Forge.Host` or `Aetheris.Forge.KernelSDK`. Forge currently consumes standard construction IR or validated exact BRep, which is the correct separation.

## Experimental and test callers

| Caller group | Classification | Preservation |
|---|---|---|
| `GenericCirBrepExecutorLab` | `FRICTIONLAB_ONLY` | keep as evidence that recursive generic execution does not generalize topology reconstruction |
| `SteppedHoleExecutionArchitectureLab` | `FRICTIONLAB_ONLY` | keep as worked strategy comparison; repeated subtract succeeded where unioned composite tool failed |
| `FrepBrepRecoveryPolicyLab` | `FRICTIONLAB_ONLY` | keep as policy evidence; do not turn wording into a generic capability claim |
| `Aetheris.Kernel.Core.Tests/Brep/Boolean/*` | `TEST_ONLY`, behavior/historical/educational | preserve hard family and rejection regressions; reorganize by recipe during M4 |
| tessellation, enclosed-void, STEP inner-shell tests that construct Boolean results | `TEST_ONLY` downstream integration | retain as compatibility contracts for canonical bodies |
| Firmament Boolean/primitive execution tests | `TEST_ONLY` legacy behavior contract | keep in opt-in V1 lane until production caller migration, then retain representative recipe regressions |

## Caller policy during migration

- Existing callers may continue through the facade while their output is parity-tested.
- A new caller must declare construction intent and choose an existing recognized recipe. It may not use the server-style generic escape hatch simply because `Union/Subtract/Intersect` exists.
- A new surface/tool family is not added to the central dispatcher unless needed for a bounded migration or a critical regression in already-supported behavior.
- Generic user Boolean remains an explicitly bounded compatibility capability, not a promise of arbitrary BRep support.
