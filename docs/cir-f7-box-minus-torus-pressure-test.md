# CIR-F7: replay-guided pressure test for `subtract(box, torus)`

## Outcome

**Success (Path B: valid CIR intent + explicit unsupported CIR→BRep materializer).**

`subtract(box, torus)` now lowers into CIR and can execute in `CirOnly` fall-forward when BRep subtract rejects the family. CIR analysis remains available. Rematerialization/STEP export remain blocked with explicit strategy-level diagnostics until an exact torus subtract materializer exists.

## Layer audit

- **Firmament:** torus primitive/tool support already existed in parsing, validation, lowering plan, selector contracts, and primitive execution paths.
- **CIR (before F7):** torus was missing from `CirNode`/tape lowering and therefore blocked CIR fallback for torus families.
- **CIR (F7):** added bounded `CirTorusNode` + tape opcode/payload lowering/evaluation so `subtract(box,torus)` is representable and analyzable in CIR.
- **BRep primitives / STEP:** torus primitive and torus surface export already exist.
- **Boolean subtract:** no safe exact `box - torus` path is currently available in the bounded subtract-family materializers.
- **Replay-guided materializer registry (F6):** extended with named strategy `subtract_box_torus` that recognizes the replay/CIR family and emits precise unsupported diagnostics.

## Chosen F7 path

**Path B (CIR intent only, no exact materializer yet).**

- `subtract(box,torus)` is accepted as valid intent.
- BRep-first subtract failure can fall-forward into `CirOnly`.
- CIR analysis works on that state.
- Rematerialization/export report explicit unsupported materializer strategy.

## Diagnostics shape

The system now differentiates:

- replay/CIR family recognition success (`subtract_box_torus` selected), and
- exact materialization unsupported (`materialization-unsupported`) with message:
  - replay-guided pattern recognized
  - exact torus subtract materializer not implemented.

## Intentional non-generalization

- No generic F-Rep→BRep extractor.
- No broad torus boolean kernel expansion.
- No public CLI behavior change.
- No topology naming generation (SEM-A0 preserved).

## Recommended F8

Implement a narrowly-scoped **exact** `subtract_box_torus` materializer only if it can be grounded in existing safe analytic subtract infrastructure (or add a dedicated safe-family validator + constructor first). Keep registry diagnostics as the fallback contract.
