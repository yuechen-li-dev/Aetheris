# AIR-REGION-X7 — Side-hole materialization golden path

## Purpose and scope

AIR-REGION-X7 adds the first controlled materialization evidence for the AIR Region side-hole path. Scope is intentionally narrow: one metadata fixture, one 10 × 8 × 6 box, one +X face-attached through side-hole, radius 1, and one local face-normal direction.

This milestone does not add general side-hole support, arbitrary face/axis support, production route replacement, production Boolean behavior, STEP exporter/importer behavior, or BRep topology behavior changes.

## Relationship to AIR-A1 and AIR-REGION-X1 through X6

The path remains the AIR Region path:

1. `RootRegion` owns the box context.
2. `FaceAttachedRegion` carries local side-hole intent.
3. The side-hole effect escapes through an explicit subtractive yield contract.
4. The CIR mirror remains an analysis-only side-channel.
5. The BRep boundary contract records planned roles.
6. The integration decision records selected/deferred routes.
7. The X6 placeholder plan is consumed by X7 materialization.

## Golden-path fixture geometry

The controlled fixture is `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`:

- parent box: width 10, depth 8, height 6;
- attachment: `+X` side face;
- profile: circle, radius 1;
- cut: through/inward along the local face normal convention.

## Placeholder-to-materialization mapping

X7 consumes the X6 placeholder plan and emits deterministic mappings:

| Placeholder role | X7 status | Materialized role |
| --- | --- | --- |
| `AffectedParentFace` | `Materialized` as reference evidence | `AffectedParentFaceReference` |
| `CutEntryLoop` | `Materialized` | `MaterializedEntryLoop` |
| `CutExitLoop` | `Materialized` | `MaterializedExitLoop` |
| `CutWallFace` | `Materialized` | `CylindricalCutWallFace` |
| `RegionIntegrationPatch` | `Deferred` | `ParentIntegrationDeferred` |

## Materialization route used

The route is `ControlledSideHolePatchMaterialization`. It is a controlled standalone patch/body artifact route and not a production parent-topology integration route.

## Parent BRep integration status

Outcome B is implemented. The side-hole entry loop, exit loop, and cylindrical cut wall are materialized as local evidence, but the parent box BRep is not split or mutated. The exact blocker is parent BRep integration: there is no bounded, non-general parent side-hole topology integration route available in this milestone.

## Topology evidence

The trace reports a topology summary for the standalone local side-hole patch:

- body evidence exists;
- face count: 1;
- loop count: 2;
- cylindrical face count: 1;
- closed: false;
- bounds label: `local-cylinder-patch:x=+5..-5,r=1`.

STEP smoke is reported as unavailable because parent integration remains deferred and this milestone does not change the STEP exporter.

## CIR authority separation

The CIR mirror remains analysis-only. It does not provide face identity, loop identity, topology parity, BRepPlan role parity, parent integration, or STEP authority.

## Boolean policy

No Boolean backend is used. Boolean remains not generally admitted as the AIR Region model or as a fallback integration route.

## Guarantees and non-goals

- no production route replacement;
- controlled fixture only;
- no general side-hole support;
- no arbitrary face/axis support;
- no parent topology mutation;
- no import/recovery;
- no CIR topology authority;
- Boolean not generally admitted;
- no STEP exporter/importer changes.

## Tests run

- `dotnet build Aetheris.CLI/Aetheris.CLI.csproj -f net10.0`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture --json`
- Focused xUnit filters documented in the PR summary.

## Recommended next milestone

Recommended next: **AIR-REGION-X8 — Side-hole materialization artifact corpus / golden trace summaries**. The parent integration blocker should be approached only after the controlled patch evidence has a stable corpus and golden JSON/text trace summaries.
