# AIR-REGION-X12 — Controlled side-hole shell closure

## Purpose and scope

AIR-REGION-X12 addresses the precise AIR-REGION-X11 blocker: `ShellClosure` for the metadata-driven controlled side-hole fixture at `fixtures/Regression/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

The scope remains the golden-path fixture only: a `10 × 8 × 6` parent box, `+X` entry face, `-X` exit face, radius-1 circular through side-hole, and the existing side-hole `FaceAttachedRegion` trace path. This is not general side-hole support, arbitrary face/axis support, arbitrary planar face splitting, production route replacement, Boolean fallback admission, or a Firmament grammar expansion.

## Relationship to AIR-REGION-X11

X11 consumed the X9 entry loop and X10 exit loop evidence, attached the cylindrical `CutWallFace`, and stopped at `ShellClosure` because `RegionIntegrationPatch` was still deferred. X12 consumes that controlled integration patch as evidence for the parent face patch connection and records a closed parent shell for this fixture.

## Controlled fixture geometry

- Parent: box `width=10`, `depth=8`, `height=6`.
- Region: `region:side-hole:+x`.
- Entry face: `+X`.
- Exit face: `-X`.
- Profile: `Circle(radius=1)`.
- Direction: through/inward.

## Shell closure route attempted

The route remains `ControlledSideHoleParentBRepIntegration`. It preserves the full trace chain from `RootRegion` and `FaceAttachedRegion` through yield contract, CIR mirror summary, BRep boundary contract, route decision, BRepPlan placeholders, standalone patch materialization, entry loop evidence, exit loop evidence, and cut-wall attachment evidence.

X12 adds only controlled shell closure evidence on that route. It does not bypass the placeholder plan and does not derive topology authority from CIR.

## Placeholder and evidence consumption

- `CutEntryLoop`: materialized from X9 and preserved.
- `CutExitLoop`: materialized from X10 and preserved.
- `CutWallFace`: materialized from X11 as `CylindricalCutWallFace` and preserved.
- `AffectedParentFace`: represented by controlled `+X` and `-X` parent face patch evidence.
- `RegionIntegrationPatch`: consumed as `RegionIntegrationPatchConsumed` for the controlled parent shell.

## Outcome

Outcome A succeeded for the controlled trace path:

- Shell closure status: `Closed`.
- Parent integration status: `Integrated`.
- `RegionIntegrationPatch` status: `Consumed`.
- Topology summary: parent body exists, closed shell is true, and cylindrical face count is at least one.
- STEP smoke status: `Succeeded` in the controlled trace smoke evidence.
- Blocker: none.

## Parent integration status

The side-hole fixture now reports `region-parent-integrated` and `parentIntegration.status = Integrated`. The route is still explicitly controlled and fixture-only.

## STEP smoke status

The trace records `stepSmoke.status = Succeeded` only for the controlled shell-closure evidence path. No STEP exporter/importer behavior was changed.

## CIR authority separation

The X3 CIR mirror remains analysis-only. It continues to deny topology, face, loop, BRepPlan, STEP/export, and production integration authority. X12 integration does not come from CIR.

## Boolean policy

No Boolean-like backend is used. Boolean fallback remains rejected/not generally admitted, and Boolean is not the AIR Region model.

## Generalization meaning

This is a golden-path proof point for future generalization through AIR Regions, BRepPlan placeholders, and explicit BRep evidence. It remains controlled fixture only and does not claim arbitrary side-hole support.

## Tests run

- `dotnet build Aetheris.CLI/Aetheris.CLI.csproj -f net10.0`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Regression/Region/valid/side-hole-face-attached-region.valid.firmfixture`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Regression/Region/valid/side-hole-face-attached-region.valid.firmfixture --json`
- `dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "ShellClosure|SideHole"`

## Recommended next milestone

Recommended: **AIR-REGION-X13 — Side-hole golden path artifact corpus / trace + STEP fixture**. The next work should freeze the controlled closed-shell JSON/text evidence and STEP smoke artifact expectations before any controlled generalization begins.

## AIR-REGION-X13 artifact generation

The X12 closed-shell evidence is now locked by the X13 generated-on-demand artifact command:

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- trace \
  --fixture fixtures/Regression/Region/valid/side-hole-face-attached-region.valid.firmfixture \
  --out-dir artifacts/air-region-x13/side-hole
```

Open `artifacts/air-region-x13/side-hole/side-hole.step` for manual inspection. The generated directory also contains `side-hole.trace.json`, `side-hole.trace.txt`, and `manifest.json`.
