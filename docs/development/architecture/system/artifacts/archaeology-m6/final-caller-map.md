# Final production BrepBoolean caller map

Direct call sites outside Core, tests, and FrictionLab:

| Milestone | Direct sites | Caller classes |
|---|---:|---:|
| M1 baseline | 15 | 6 |
| M5 | 12 | 4 |
| M6 | 12 | 4 |

M6 deliberately adds no Recipe merely to reduce the count.

| Owner | Sites | Classification | Accepted reason |
|---|---:|---|---|
| `FirmamentPrimitiveExecutor.ExecuteBoolean` | 3 | `LEGACY_V1_COMPATIBILITY` and bounded `CURRENT_SEMANTIC_CALLER` bridge | V1 operation-shaped execution is retained. V2 known through-holes route directly to a Recipe before this bridge; remaining generic add/subtract/intersect operations have no matching semantic Recipe and retain typed rejection. |
| `HoleRecoveryExecutor` | 5 | `UNMIGRATED_SPECIALIZED_FAMILY` | Blind, counterbore, countersink/chamfered, and stepped history need separate topology/history contracts. Profile-stack is preferred where admitted; no cosmetic Recipes were created. |
| `CirBrepMaterializer` box/box subtract | 1 | `GENERIC_CIR_COMPATIBILITY` | Import/recovery has recognized operands but no named reusable construction contract. The recognized box/cylinder path already uses the through-hole Recipe. |
| server `KernelEndpoints` | 3 | `SERVER_EXTERNAL_API` | Public external-body union/subtract/intersect compatibility endpoint. It promises bounded support and surfaces typed `NotImplemented` rejection, not universal Boolean success. |

No `ACCIDENTAL / OBSOLETE` direct caller remains. `ClassifyBooleanCase` has no production caller, but its public signature is preserved for M6 compatibility. Test and FrictionLab callers remain behavior contracts or historical evidence.

No new dispatcher family was added in M6.
