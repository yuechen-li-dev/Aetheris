# AIR-REGION-X8 — Side-hole parent integration attempt

## Purpose and scope

AIR-REGION-X8 performs the first controlled parent-body BRep integration attempt for the metadata-driven side-hole `FaceAttachedRegion` fixture. The scope is intentionally narrow: the existing `side-hole-face-attached-region.valid.firmfixture`, a 10 × 8 × 6 parent box, the +X attached face, a radius-1 circular profile, and a through/inward subtractive `SideHole` feature.

## Relationship to AIR-A1 and AIR-REGION-X1 through X7

The trace preserves the existing region chain: `RootRegion`, `FaceAttachedRegion`, the X2 side-hole yield contract, the X3 CIR mirror summary, the X4 BRep boundary contract, the X5 route decision scaffold, the X6 BRepPlan placeholders, and the X7 standalone patch materialization evidence. X8 adds a `parentIntegration` section after consuming those prior artifacts.

## Controlled fixture geometry

- Parent box: 10 × 8 × 6.
- Attached face: +X.
- Profile: circle, radius 1, center `(0, 0)` in the face-attached local frame.
- Direction: through/inward along the local face normal/X axis.
- Effect: subtractive.
- Target: parent body.

## Integration route attempted

The attempted route is `ControlledSideHoleParentBRepIntegration`. It is an internal/test-visible controlled prototype path, not a production route replacement and not general side-hole support.

## Backend path used

Outcome B is implemented. No Boolean-like backend is used. The attempt consumes X6 placeholders and X7 local patch evidence, then blocks before mutating the parent shell because no bounded parent-face splitting and loop-insertion adapter exists for this exact fixture.

## Placeholder consumption

The X8 parent integration attempt maps all X6 placeholders:

| Placeholder | X8 mapping |
| --- | --- |
| `AffectedParentFace` | deferred; requires parent face split |
| `CutEntryLoop` | deferred; requires loop insertion |
| `CutExitLoop` | deferred; requires opposite-face loop insertion |
| `CutWallFace` | preserved from X7 standalone cylindrical wall evidence |
| `RegionIntegrationPatch` | deferred; blocked before parent consumption |

## Parent integration status

Status is `Blocked`. The blocker category is `FaceSplitting`, with code `controlled-side-hole-parent-face-splitting-missing`.

## Topology evidence or blocker

The X7 topology evidence is preserved: a standalone cylindrical cut-wall face and two local patch loops. X8 does not claim a closed parent shell or integrated parent topology. Parent topology counts remain unavailable except for preserved cylindrical wall evidence.

## CIR authority separation

CIR remains analysis-only. The parent integration does not claim topology authority, face identity, entry-loop identity, exit-loop identity, or boundary-patch identity from CIR.

## Boolean policy

Boolean was not used. Boolean remains not generally admitted for AIR Region integration, and X8 does not make Boolean the AIR Region model.

## Non-goals

- No general side-hole support.
- No arbitrary face/axis support.
- No production route replacement.
- No production Firmament grammar expansion.
- No STEP exporter/importer behavior change.
- No BRep topology behavior change outside this controlled prototype trace path.

## Tests run

The implementation was validated with CLI build/help, side-hole text and JSON traces, invalid implicit-parent-mutation JSON trace, parser-backed box trace, and focused filtered .NET tests recorded in the PR summary.

## Recommended next milestone

Recommended: **AIR-REGION-X9 — Controlled side-hole parent face split and loop insertion adapter**. The next milestone should directly address the reported `FaceSplitting` blocker by adding the smallest controlled adapter for the exact +X through-hole fixture, then binding the preserved X7 cylindrical wall evidence into the parent shell.
