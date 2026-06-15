# AIR-REGION-X1 — Region model skeleton and trace fixtures

AIR-REGION-X1 adds the first minimal AIR Region trace model. It implements trace-only `RootRegion` and metadata-driven `FaceAttachedRegion` summaries so AIR-A1 scoped construction islands are visible without changing production geometry.

## Scope

Implemented model fields cover region kind, effect kind, yield kind, boundary contract, local frame, and integration status. The DTOs currently live in `Aetheris.Kernel.Core.Air.Regions` and are consumed by `aetheris trace`.

## Relationship to AIR-A1

AIR-A1 says a different-axis feature is a scoped AIR Region with a local frame, explicit yield, and explicit parent integration route; it is not immediate global Boolean. X1 makes that doctrine inspectable in trace reports only.

## RootRegion

Parser-backed `fixtures/Firmament/Primitive/valid/box.valid.firmfixture` now reports a single `RootRegion` with a `WorldRoot` frame, `PureConstruction` effect, `YieldBody`, `YieldsBody`, and `NotRequired` integration. The fixture still reaches `emitted-brep` through the existing AIR-X11 box path.

## FaceAttachedRegion side-hole fixture

`fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture` is metadata-driven and trace-only. It reports `RootRegion` plus `FaceAttachedRegion` with `Subtractive` effect, `YieldSubtractiveVolume`, `YieldsCutVolume`, and `Deferred` integration. It does not parse new Firmament grammar and does not emit geometry.

## Local frame convention

For the +X face-attached mock, the frame origin is the deterministic placeholder `(5, 0, 0)`. The local Z axis follows the +X face normal `(1, 0, 0)`, the local X axis is `(0, 1, 0)`, and the local Y axis is `(0, 0, 1)`, making a right-handed conceptual frame for trace evidence only.

## Guarantees and non-goals

X1 guarantees no Boolean, no BRep emission, no STEP smoke, no production route replacement, no production Firmament grammar expansion, no BRepPlan region integration or semantic change, no CIR region mirror, and no side-hole geometry.

## Invalid fixture

`fixtures/Firmament/Region/invalid/implicit-parent-mutation.invalid.firmfixture` rejects a region that attempts implicit parent mutation without explicit yield. The actual stage is `region-rejected` and no geometry is emitted.

## Trace output additions

Text reports include a concise `Regions` section when a region trace summary exists. JSON reports include a stable `regions` object with `rootRegionId`, `regionCount`, `hasNestedRegions`, and region entries.

## Tests run during implementation

Focused CLI tests cover root-region box reporting, side-hole text and JSON, implicit parent mutation rejection, deterministic JSON, and existing fixture behavior.

## Recommended next milestone

AIR-REGION-X2 — Side-hole FaceAttachedRegion mock yield refinement and boundary contract. The implementation showed that trace contracts are now visible, but the next blocker is sharpening yield/boundary metadata before any BRepPlan or CIR analysis is attempted.
