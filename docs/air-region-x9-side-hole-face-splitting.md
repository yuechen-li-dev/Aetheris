# AIR-REGION-X9 — Controlled side-hole face splitting

## Purpose and scope

AIR-REGION-X9 addresses the exact AIR-REGION-X8 blocker for the metadata-driven side-hole fixture: controlled parent face splitting for the `+X` face of the `10 × 8 × 6` box.

This is not general side-hole support, arbitrary planar face splitting, arbitrary face selection, arbitrary profiles, or Boolean fallback. The path remains scoped to `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

## Relationship to AIR-REGION-X8

X8 truthfully stopped at generic `FaceSplitting`: the controlled parent integration attempt had X2 yield evidence, X3 CIR mirror evidence, X4 BRep boundary intent, X5 route decision evidence, X6 placeholders, and X7 standalone patch materialization, but it could not show that the parent `+X` face accepted a circular entry loop.

X9 narrows that blocker by adding a controlled face-split evidence summary for the same fixture. The generic face-splitting blocker is cleared; parent integration remains partial.

## Controlled fixture geometry

- Parent body: box `width=10`, `depth=8`, `height=6`.
- Affected parent face: `+X` only.
- Entry profile: `Circle(radius=1)` in `frame:side-hole:+x`.
- Placeholder consumed: `CutEntryLoop`.
- Existing standalone patch evidence preserved: entry loop, exit loop, and cylindrical cut-wall evidence from X7.

## Face-split route attempted

The implementation adds a controlled prototype evidence path in `AirSideHoleParentBRepIntegrationPrototype.SplitEntryLoop`. It records a `faceSplit` summary with:

- `affectedFaceSelector = +X`;
- `faceSplitStatus = SplitCreated`;
- `entryLoopStatus = EntryLoopMaterialized`;
- `entryLoopProfile = Circle(radius=1)`;
- `entryLoopRadius = 1`;
- `materializedPlaceholderIds = ["region:side-hole:+x:entry-loop"]`;
- topology evidence for two face loops, one inner loop, and one circular edge.

The evidence is bounded to the controlled fixture and does not mutate production BRep topology.

## Outcome A

Outcome A is implemented as trace/evidence materialization:

- controlled `+X` face split evidence exists;
- `CutEntryLoop` is consumed/materialized as entry-loop evidence;
- inner-loop evidence exists for the affected face;
- the generic X8 `FaceSplitting` blocker is cleared;
- the remaining parent integration blocker is more specific: `ExitLoopInsertion`.

## Placeholder consumption

`CutEntryLoop` now appears in the X9 face split summary as consumed/materialized for the controlled `+X` face. X6 placeholder identity remains stable, and X7 standalone patch evidence remains preserved.

## Parent integration status

Parent integration is still not complete. The trace reports `PartiallyIntegrated` for the controlled parent integration attempt, with the remaining blocker `ExitLoopInsertion`. Exit loop insertion, cut-wall attachment to the parent shell, shell closure, and STEP export remain deferred.

## Boolean policy

No Boolean route is used or admitted. The X9 diagnostics include `air-region-x9-no-boolean`, and the Boolean fallback remains rejected by the earlier route decision evidence.

## Topology and STEP evidence

The face split summary records controlled topology evidence only:

- face loops: `2`;
- inner loops: `1`;
- circular edges: `1`.

This is not a closed parent BRep and not STEP face-with-hole export. STEP smoke is deferred because exit loop insertion, cut-wall attachment, and shell closure remain incomplete.

## Tests run

X9 was validated with the requested CLI commands and focused .NET test filters. The focused tests assert text/JSON face split output, blocker advancement from generic `FaceSplitting` to `ExitLoopInsertion`, no premature full integration claim, no Boolean use, implicit-parent-mutation rejection, parser-backed box stability, and deterministic JSON.

## Recommended next milestone

Recommended: **AIR-REGION-X10 — Controlled exit loop / cut-wall parent integration**.

X9 cleared the entry face-splitting blocker for the controlled fixture. The next blocker is no longer generic face splitting; it is controlled opposite-face exit loop insertion plus attaching the preserved cylindrical cut-wall evidence and closing the parent shell.


## AIR-REGION-X10 follow-up

AIR-REGION-X10 preserves the X9 `+X` entry-loop evidence and adds controlled opposite-side `-X` exit-loop evidence. The `CutExitLoop` placeholder is now consumed/materialized in the controlled trace path, and the parent-integration blocker advances from `ExitLoopInsertion` to `CutWallAttachment`. Parent integration remains partial; cut-wall attachment, shell closure, topology validation, STEP/export, Boolean integration, and general side-hole support are still not claimed.
