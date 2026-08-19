# AIR-REGION-X10 — Side-hole exit loop insertion

## Purpose and scope

AIR-REGION-X10 is a controlled blocker-fix milestone for the existing side-hole `FaceAttachedRegion` fixture. It does not add general side-hole support, arbitrary face/axis support, production Boolean fallback, production route replacement, or broad BRep topology surgery.

## Relationship to AIR-REGION-X9

AIR-REGION-X9 cleared the generic `FaceSplitting` blocker by preserving the full AIR Region evidence chain and adding controlled `+X` parent-face split / entry-loop evidence. X9 intentionally stopped at `ExitLoopInsertion`.

X10 consumes that X9 entry-loop evidence and advances the blocker by creating controlled opposite-face exit-loop evidence for the same fixture.

## Controlled fixture geometry

Fixture: `fixtures/Regression/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

- Parent box: 10 × 8 × 6.
- Entry face: `+X`.
- Exit face: `-X` under the current fixture convention.
- Profile: `Circle(radius=1)`.
- Direction: through/inward along the local face normal/X axis.
- Feature: `SideHole`.
- Effect: subtractive.
- Route: region trace / controlled prototype evidence only.

## Exit loop route attempted

The implementation reuses the narrow X9 face-with-inner-loop evidence shape for the opposite side of the box. It deterministically selects the `-X` exit face for the controlled `+X` side-hole fixture, materializes the `CutExitLoop` placeholder as trace/evidence, and records bounded topology evidence for a face with one circular inner loop.

## Outcome A

Outcome A was achieved:

- Exit face selector: `-X`.
- Exit loop status: `ExitLoopMaterialized`.
- Exit loop profile: `Circle(radius=1)`.
- `CutExitLoop` placeholder: consumed/materialized as `region:side-hole:+x:exit-loop`.
- Topology evidence: two face loops, one inner loop, one circular edge for the controlled exit face evidence.
- Parent integration remains `PartiallyIntegrated`.
- The next blocker is `CutWallAttachment`, not `ExitLoopInsertion`.

## Placeholder consumption

X10 consumes `CutExitLoop` in the controlled parent integration path. X6 placeholder planning remains stable: the placeholder identity is still `region:side-hole:+x:exit-loop`, and X10 reports it as materialized in the exit-loop summary and parent-integration placeholder mappings.

## Entry-loop preservation

The X9 `faceSplit` section remains present. It still reports `+X`, `SplitCreated`, `EntryLoopMaterialized`, `Circle(radius=1)`, `CutEntryLoop`, and the bounded entry-loop topology evidence.

## Parent integration status

Parent integration remains partial. X10 does not claim cut-wall attachment, parent shell closure, full topology validation, or STEP export. The parent integration blocker advances to `CutWallAttachment` because entry and exit loop evidence now exist, but the preserved standalone cylindrical wall has not been attached into a closed parent shell.

## Boolean policy

No Boolean fallback is invoked or admitted. CIR remains analysis-only and does not gain topology authority.

## Topology and STEP evidence

X10 records trace/evidence topology for the controlled `-X` exit face loop: face loops, inner loops, and circular edge count. It does not emit STEP and does not claim STEP smoke success.

## Tests run

Validation included CLI build/help, side-hole text and JSON traces, invalid implicit-parent-mutation JSON trace, parser-backed box trace, and focused .NET test filters recorded in the PR summary.

## Recommended next milestone

Recommended: **AIR-REGION-X11 — Controlled cut-wall attachment / entry-exit wall integration**.

X11 should bind the preserved cylindrical cut-wall evidence to the controlled `+X` entry loop and `-X` exit loop, then truthfully report whether parent shell closure/topology validation becomes available.

## AIR-REGION-X11 update

AIR-REGION-X11 consumes the X9 entry-loop evidence and X10 exit-loop evidence, attaches the controlled cylindrical `CutWallFace` evidence, and advances the precise blocker to `ShellClosure`. Parent integration remains `PartiallyIntegrated`; STEP smoke remains unavailable until the controlled `RegionIntegrationPatch` is materialized and shell closure is validated.
