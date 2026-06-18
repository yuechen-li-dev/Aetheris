# AIR-REGION-X5 — Side-hole integration route decision scaffold

## Purpose and scope

AIR-REGION-X5 adds a trace-only integration route decision summary for the metadata-driven side-hole `FaceAttachedRegion` fixture:

`fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`

The purpose is to prevent side-hole regions from silently falling through to any backend. The trace now names candidate parent integration routes, gives deterministic statuses and reasons, and selects `DeferredIntegration`.

## Relationship to earlier milestones

AIR-A1 defines regions as scoped construction islands whose effects escape only through explicit yields. AIR-REGION-X1 introduced `RootRegion` and `FaceAttachedRegion` trace summaries. AIR-REGION-X2 established the side-hole subtractive yield and boundary contract. AIR-REGION-X3 added the CIR analysis mirror. AIR-REGION-X4 added BRepPlan boundary intent without materializing topology.

AIR-X2 route-selection doctrine says deterministic closed classifications should use switch/match rather than JudgmentUtility. X5 follows that doctrine: no competing admitted topology route exists, so the side-hole integration decision is a deterministic scaffold, not utility scoring.

## Why integration route decisions matter

A region yield is not an implicit command to mutate parent topology. X5 records what integration routes were considered and why each is unavailable, rejected, or analysis-only. This makes deferral explicit and machine-checkable.

## Candidate route taxonomy

| Candidate | Status | Reason |
| --- | --- | --- |
| `FaceAttachedConstructiveInsertion` | `Deferred` | No side-hole constructive insertion route has been implemented. |
| `LocalBRepPlanPatch` | `Deferred` | BRepPlan boundary intent exists, but patch/materialization is not implemented. |
| `BRepBooleanFallback` | `Rejected` | Boolean fallback is not admitted for region integration in this milestone. |
| `CirAnalysisMirrorOnly` | `AvailableForAnalysis` | CIR mirror can assist occupancy/containment/bounds analysis but cannot integrate topology. |
| `DeferredIntegration` | `Selected` | No topology integration route is admitted. |

## Decision result

The selected route is `DeferredIntegration` with selected status `Deferred`. Topology integration remains deferred.

## Boolean fallback rejection doctrine

Boolean is not the region model. Boolean fallback would need explicit admission plus provenance and topology-loss accounting. X5 does not admit that fallback and records `boolean-fallback-not-admitted`.

## CIR analysis-only candidate doctrine

The AIR-REGION-X3 CIR mirror remains an analysis side channel only. It has no topology authority, no face identity authority, and no STEP/export authority.

## Relationship to BRep boundary contract

The AIR-REGION-X4 BRepPlan boundary contract still records the affected `+X` face, circular entry loop intent, deferred opposite-side exit, deferred cylindrical cut wall intent, and future role names. X5 does not create BRepPlan elements or parent topology patches.

## Guarantees

X5 keeps these guarantees: no side-hole geometry, no Boolean invocation, no BRepPlan materialization, no parent topology mutation, no BRep emission, and no STEP smoke.

## Trace output example

Text trace includes a `Region integration decision` section with `Mode: SwitchMatch`, `Selected: DeferredIntegration`, `Status: Deferred`, and the candidate table above. JSON includes `regions.regions[].integrationDecision` for the side-hole `FaceAttachedRegion`.

## Non-goals

X5 does not implement side-hole geometry, Boolean integration, BRepPlan patch materialization, production route replacement, Firmament grammar expansion, CIR evaluator/tape changes, BRep topology changes, STEP exporter/importer changes, arbitrary graph support, import/recovery, triangle migration, or NURBS/freeform expansion.

## Tests run

Focused CLI trace tests were updated for text, JSON, non-integration guarantees, invalid implicit parent mutation, parser-backed root-region behavior, and deterministic output.

## Recommended next milestone

AIR-REGION-X6 should add region BRepPlan placeholder elements without parent mutation. X5 left the cleanest next blocker at patch/materialization intent: the boundary contract and route decision now agree that a local BRepPlan patch route is the likely topology-side next step, but it must still avoid parent mutation.

## AIR-REGION-X6 placeholder note

AIR-REGION-X6 preserves the X5 `DeferredIntegration` selection and Boolean rejection, then adds a parallel `brepPlaceholders` trace summary for the side-hole region. The placeholder plan has five deterministic elements (`AffectedParentFace`, `CutEntryLoop`, `CutExitLoop`, `CutWallFace`, and `RegionIntegrationPatch`) and zero materialized elements. The route decision is unchanged: placeholders prepare the future local BRepPlan patch/materialization boundary, but they do not admit integration.

## AIR-REGION-X7 note

AIR-REGION-X7 consumes the controlled side-hole placeholder plan for the `+X` fixture and materializes standalone patch evidence for the entry loop, exit loop, and cylindrical cut wall. Parent BRep integration remains deferred; CIR remains analysis-only; Boolean is not generally admitted; no production route replacement or general side-hole support is introduced.


## AIR-REGION-X8 route update note

AIR-REGION-X8 updates the side-hole trace decision to the controlled attempted route `ControlledSideHoleParentBRepIntegration` with status `Blocked`. The prior route scaffold is preserved as evidence; Boolean remains rejected/not generally admitted, and the blocker is parent face splitting plus loop insertion for the controlled fixture.
