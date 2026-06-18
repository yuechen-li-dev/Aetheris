# AIR-REGION-X4 — Side-hole BRepPlan boundary contract

## Purpose and scope

AIR-REGION-X4 adds a trace-only topology-side boundary contract for the metadata-driven side-hole `FaceAttachedRegion` fixture at `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

The contract records what a future BRepPlan integration would need to know without performing that integration. It does not implement side-hole geometry, invoke Boolean, emit BRep, emit STEP, mutate parent topology, or materialize BRepPlan elements.

## Relationship to prior doctrine

- AIR-A1 defines regions as scoped construction islands whose effects escape through explicit yields.
- AIR-REGION-X1 introduced trace-only `RootRegion` and `FaceAttachedRegion` summaries.
- AIR-REGION-X2 made the side-hole yield contract explicit: +X attachment, circular profile, through/inward subtractive effect, through-cut boundary intent, parent-body-local scope, and deferred integration.
- AIR-REGION-X3 added an analysis-only CIR mirror and denied topology authority.
- AIR-X3/AIR-X4 define BRepPlan as a backend topology emission plan that can preserve semantic roles before materialization.

X4 bridges those doctrines by adding region-local BRepPlan boundary intent while preserving deferred integration.

## Why boundary contracts matter

A side-hole region should not force future lowering to rediscover feature topology from raw geometry or immediately choose Boolean. The boundary contract carries the affected parent face, intended entry and exit boundaries, cut-wall intent, planned roles, and explicit losses so a future integration route can be selected with provenance intact.

## Boundary contract fields

The side-hole region now reports `brepBoundary` in JSON and a `Region BRepPlan boundary` block in text.

- `affectedParent`: root box, parent region `region:root`, affected face selector `+X`, face role `SideFace`, scope `ParentBodyLocalFeature`, parent-body locality with no sibling effects.
- `entryBoundary`: `CircularEntry` sourced from the side-hole circular yield profile in `frame:side-hole:+x`, with `CircularEntryLoop` / `EntryLoopIntent` intent.
- `exitBoundary`: through-cut opposite-side exit intent, status `Deferred`, not materialized.
- `cutWallIntent`: cylindrical cut-wall intent from the circular profile in the through/inward direction, status `Deferred`.
- `patchIntent`: `Deferred`.
- `plannedRoles`: semantic role strings, not global BRepPlan enum additions: `AffectedParentFace`, `CutBoundaryPatch`, `CutEntryLoop`, `CutExitLoop`, `CutWallFace`, `DeferredIntegration`, `RegionIntegrationPatch`, and `SideHoleFeature`.
- `deferredElements`: entry loop identity, exit loop identity, cut wall face identity, boundary patch identity, parent topology mutation, BRepPlan element materialization, Boolean invocation, BRep emission, and STEP smoke.
- `integrationStatus`: `Deferred`.

## Topology-side locality

The contract is local to the parent body and identifies the affected parent face selector. It is not a parent topology mutation. No BRepPlan patch element, BRep face, loop, coedge, or STEP entity identity is emitted by this milestone.

## Relationship to CIR mirror

The X3 CIR mirror remains analysis-only. It can summarize occupancy/containment/bounds behavior for a parent-box-minus-cylinder interpretation, but it still denies topology authority, face identity, loop identity, boundary patch identity, BRepPlan role parity, STEP/export authority, and production integration authority.

The X4 BRep boundary contract is topology-side intent only. It does not change CIR evaluator or tape behavior.

## Guarantees

For the side-hole fixture, X4 guarantees:

- no parent topology mutation;
- no BRepPlan elements materialized;
- no Boolean;
- no BRep emission;
- no STEP smoke;
- no side-hole geometry implementation;
- no production route replacement;
- no Firmament grammar expansion.

## Trace examples

Text trace includes:

```text
Region BRepPlan boundary
  Region: region:side-hole:+x
  Feature: SideHole
  Status: PlannedContractOnly
  Affected parent: root box
  Affected face: +X
  Entry boundary: circular entry loop intent
  Exit boundary: opposite-side exit deferred
  Cut wall: cylindrical cut wall intent deferred
  Planned roles: AffectedParentFace, CutBoundaryPatch, CutEntryLoop, CutExitLoop, CutWallFace, DeferredIntegration, RegionIntegrationPatch, SideHoleFeature
  Integration: Deferred
```

JSON trace includes stable `regions.regions[].brepBoundary` fields such as `status`, `affectedParent`, `entryBoundary`, `exitBoundary`, `cutWallIntent`, `plannedRoles`, `integrationStatus`, `knownLosses`, and `guarantees`.

## Non-goals

- no side-hole geometry;
- no Boolean;
- no BRepPlan integration or materialization;
- no CIR topology authority;
- no production route replacement;
- no grammar expansion;
- no STEP/export support for side-hole.

## Tests run

- `dotnet build Aetheris.CLI/Aetheris.CLI.csproj -f net10.0`
- `dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Trace|Fixture|FirmFixture|Region|AirRegion|RegionYield|RegionCir|RegionBRep|SideHole|ParserBacked|Firmament|FeatureAir|ConstructiveAir|ProfileEmission|ProfileExtrude|AIR|Air|CIR|Cir|Mirror|BRepPlan|BrepPlan|Analyze|Map|CliBaseline|Step|Prismatic|AirChamfer|Experimental|Corpus"`

## Recommended next milestone

Recommended: **AIR-REGION-X5 — Side-hole integration route decision scaffold, no integration**.

X4 now records enough topology-planning intent to evaluate future routes without immediately materializing BRepPlan elements. The next blocker is route decision scaffolding that can compare direct BRepPlan placeholder planning, constructive side-hole insertion, or Boolean fallback as explicit deferred candidates without changing production behavior.

## AIR-REGION-X5 note

AIR-REGION-X5 adds a trace-only side-hole integration route decision scaffold. The side-hole `FaceAttachedRegion` now reports deterministic candidate statuses, selects `DeferredIntegration`, rejects Boolean fallback as not admitted, keeps the CIR mirror analysis-only, and keeps the BRepPlan boundary contract as topology-side intent without materialization.

## AIR-REGION-X6 placeholder note

AIR-REGION-X6 keeps this X4 boundary contract as intent and adds deterministic placeholder elements derived from it. The X6 placeholders make the future materialization boundary explicit (`+X` parent face reference, entry loop, exit loop, cut wall face, and integration patch), while still recording zero materialized BRepPlan elements and no parent topology mutation.

## AIR-REGION-X7 note

AIR-REGION-X7 consumes the controlled side-hole placeholder plan for the `+X` fixture and materializes standalone patch evidence for the entry loop, exit loop, and cylindrical cut wall. Parent BRep integration remains deferred; CIR remains analysis-only; Boolean is not generally admitted; no production route replacement or general side-hole support is introduced.
