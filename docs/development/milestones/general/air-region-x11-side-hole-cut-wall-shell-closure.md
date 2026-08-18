# AIR-REGION-X11 — Side-hole cut-wall attachment and shell closure

## Purpose and scope

AIR-REGION-X11 directly addresses the AIR-REGION-X10 `CutWallAttachment` blocker for the controlled golden-path side-hole fixture only:

- parent box: `10 x 8 x 6`;
- entry face: `+X`;
- exit face: `-X`;
- circular through-hole radius: `1`;
- path: side-hole `FaceAttachedRegion` trace path only.

This milestone does not add general side-hole support, arbitrary face/axis support, production route replacement, grammar expansion, Boolean fallback, or STEP exporter/importer changes.

## Relationship to AIR-REGION-X10

X10 materialized the controlled opposite-side exit loop and advanced the blocker from `ExitLoopInsertion` to `CutWallAttachment`. X11 consumes that existing entry/exit-loop evidence and attaches the controlled cylindrical cut-wall evidence between them.

## Controlled fixture geometry

The fixture remains:

`fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture`

The trace preserves the full chain from `RootRegion` through side-hole yield, CIR mirror, BRep boundary contract, route decision, placeholders, standalone patch evidence, entry loop, exit loop, and now cut-wall attachment / shell-closure evidence.

## Cut-wall attachment route attempted

The bounded prototype route is `ControlledSideHoleParentBRepIntegration`. It consumes the X9 `CutEntryLoop`, the X10 `CutExitLoop`, and the X7 cylindrical cut-wall evidence. It reports `CutWallAttached` for the controlled cylindrical cut-wall face and keeps the route explicitly non-production.

## Placeholder consumption

- `CutEntryLoop`: preserved as materialized entry-loop evidence.
- `CutExitLoop`: preserved as materialized exit-loop evidence.
- `CutWallFace`: consumed/materialized as `CylindricalCutWallFace` evidence.
- `RegionIntegrationPatch`: still deferred; this is the precise shell-closure blocker.

## Outcome

Outcome is partial A: cut-wall attachment succeeds for the controlled evidence chain, but full parent shell closure is not claimed.

- Cut wall status: `CutWallAttached`.
- Parent integration status: `PartiallyIntegrated`.
- Blocker category: `ShellClosure`.
- Stage: `region-shell-closure-blocked`.

## Shell closure status

Shell closure is `Blocked`. The trace records that the controlled cylindrical cut wall is attached, but the parent shell is not closed because `RegionIntegrationPatch` remains deferred pending controlled parent face patch validation.

## STEP smoke status

STEP smoke is `Unavailable` because closed parent-shell/body evidence is not yet available. No STEP exporter/importer behavior was changed.

## CIR authority separation

CIR remains an analysis-only mirror. It provides occupancy/containment/bounds side-channel evidence and continues to deny topology authority, face identity, loop identity, BRepPlan role parity, and STEP export authority.

## Boolean policy

Boolean was not used. Boolean is not the AIR Region model, not generally admitted, and not used as a fallback in this milestone.

## Generalization meaning

This is meaningful progression toward a side-hole golden path: the specific X10 blocker (`CutWallAttachment`) is removed and the next blocker is narrowed to `ShellClosure` / controlled `RegionIntegrationPatch` validation. It remains controlled fixture-only work and is not general side-hole support.

## Tests run

- `dotnet build Aetheris.CLI/Aetheris.CLI.csproj -f net10.0`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture --json`
- focused CLI cut-wall/side-hole tests during development.

## Recommended next milestone

AIR-REGION-X12 — Fix the controlled `ShellClosure` / `RegionIntegrationPatch` blocker, then run STEP smoke only after closed parent-shell/body evidence is available.

## AIR-REGION-X12 follow-up

AIR-REGION-X12 consumes the previously deferred controlled `RegionIntegrationPatch`, preserves X9 entry-loop, X10 exit-loop, and X11 cylindrical cut-wall evidence, and reports the controlled parent shell as `Closed`. Parent integration advances to `Integrated` for the controlled fixture only, with STEP smoke recorded as `Succeeded` in trace evidence. CIR remains analysis-only, Boolean remains unused/not generally admitted, and no general side-hole support or production route replacement is claimed.
