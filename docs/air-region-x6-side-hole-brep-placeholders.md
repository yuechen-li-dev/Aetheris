# AIR-REGION-X6 — Side-hole BRepPlan placeholders

## Purpose and scope

AIR-REGION-X6 adds trace-only BRepPlan placeholder elements for the metadata-driven side-hole `FaceAttachedRegion` fixture at `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

This is scaffolding for future topology work. It does not materialize side-hole BRepPlan patches, emit BRep, invoke Boolean, smoke STEP, mutate parent topology, change production routing, or expand Firmament grammar.

## Relationship to AIR-A1/X1/X2/X3/X4/X5

- AIR-A1 defines AIR Regions as scoped construction islands whose effects escape through explicit yields and route-selected parent integration.
- X1 made root and face-attached regions visible in trace output.
- X2 made the side-hole yield contract explicit: +X face attachment, circular radius-1 profile, through/inward direction, subtractive effect, through-cut boundary, parent-body-local scope, and deferred integration.
- X3 added a CIR analysis mirror and denied topology authority.
- X4 added the BRepPlan boundary contract: affected +X parent face, circular entry loop intent, opposite-side exit deferred, cylindrical cut wall intent deferred, planned role strings, and no materialization.
- X5 selected `DeferredIntegration` while rejecting Boolean fallback and deferring constructive/local BRepPlan patch routes.
- X6 now turns the X4 role intent into deterministic placeholder elements while preserving the X5 deferred integration decision.

## Why placeholders are needed before materialization

A side-hole materializer should not rediscover topology work from raw geometry or infer role identity after mutation. X6 records the planned topology boundary as stable, inspectable placeholders so X7 can materialize from an explicit contract: which parent face is affected, which entry/exit loops are required, which cut wall face is required, and which integration patch remains deferred.

## Placeholder model

The trace model uses `AirRegionBRepPlaceholderPlan` with:

- `planId`, source region/yield IDs, source region kind, and feature kind;
- `placeholderStatus` (`PlaceholderOnly` for X6);
- `elements` carrying stable IDs, kind, role, semantic role strings, source region/yield, parent reference, materialization status, and diagnostics;
- `summary` with role/count totals and materialized/not-materialized counts;
- `validation` for required roles;
- diagnostics, known losses, and no-materialization guarantees.

Roles remain strings/semantic roles rather than global BRepPlan enum expansion. This keeps the X6 surface narrow and non-breaking.

## Stable IDs and roles

The side-hole placeholder IDs are deterministic and human-readable:

| ID | Kind | Role | Materialization |
| --- | --- | --- | --- |
| `region:side-hole:+x:parent-face:+x` | `AffectedParentFaceReference` | `AffectedParentFace` | `ReferenceOnly` |
| `region:side-hole:+x:entry-loop` | `EntryLoopPlaceholder` | `CutEntryLoop` | `NotMaterialized` |
| `region:side-hole:+x:exit-loop` | `ExitLoopPlaceholder` | `CutExitLoop` | `NotMaterialized` |
| `region:side-hole:+x:cut-wall` | `CutWallFacePlaceholder` | `CutWallFace` | `NotMaterialized` |
| `region:side-hole:+x:integration-patch` | `IntegrationPatchPlaceholder` | `RegionIntegrationPatch` | `NotMaterialized` |

Semantic roles include `SideHoleFeature`, `DeferredIntegration`, and `NotMaterialized`.

## Counts and expected roles

Expected X6 counts for the side-hole fixture:

- placeholder elements: 5;
- affected parent face references: 1;
- entry loop placeholders: 1;
- exit loop placeholders: 1;
- cut wall face placeholders: 1;
- integration patch placeholders: 1;
- materialized elements: 0;
- not-materialized/reference-only elements: 5.

Required roles are `AffectedParentFace`, `CutEntryLoop`, `CutExitLoop`, `CutWallFace`, and `RegionIntegrationPatch`.

## No materialization guarantees

Every successful side-hole placeholder trace records:

- no parent topology mutation;
- no BRepPlan materialization;
- no BRep emission;
- no STEP smoke;
- no Boolean;
- no production route replacement;
- integration still deferred;
- prepared for future X7 materialization.

These placeholders do not imply emitted face, loop, coedge, edge, vertex, or STEP entity identity.

## Relationship to the BRep boundary contract

X4 describes boundary intent. X6 creates placeholder element scaffolding from that intent. The placeholders are still not materialized BRepPlan elements: they are the deterministic shape of future topology work.

## Relationship to X7

If this scaffolding remains stable, the recommended next milestone is **AIR-REGION-X7 — Golden-path side-hole placeholder materialization to BRep**. X7 can use the placeholder set as the input boundary for a first constrained BRep materialization path. If that proves too risky, the fallback X7 should be **Placeholder corpus/golden summaries** to expand deterministic fixtures before materialization.

## Non-goals

- no side-hole geometry;
- no Boolean;
- no BRepPlan materialization;
- no BRep emission;
- no STEP;
- no production route replacement;
- no CIR evaluator/tape changes;
- no BRep topology behavior changes;
- no Firmament grammar expansion.

## Tests run

The implementation was validated with CLI help/build, side-hole text/JSON traces, invalid implicit-parent-mutation JSON trace, parser-backed box trace, and focused filtered .NET tests recorded in the PR summary.

## AIR-REGION-X7 note

AIR-REGION-X7 consumes the controlled side-hole placeholder plan for the `+X` fixture and materializes standalone patch evidence for the entry loop, exit loop, and cylindrical cut wall. Parent BRep integration remains deferred; CIR remains analysis-only; Boolean is not generally admitted; no production route replacement or general side-hole support is introduced.
