# AIR-REGION-X2 — Side-hole yield boundary contract

## Purpose and scope

AIR-REGION-X2 refines the trace-only side-hole `FaceAttachedRegion` so its yielded value carries construction intent instead of only saying that a subtractive volume exists. The change is still a trace contract: it does not implement side-hole geometry, invoke Boolean, emit BRep, add BRepPlan integration, mirror to CIR, or expand Firmament grammar.

## Relationship to AIR-A1 and AIR-REGION-X1

AIR-A1 defines AIR Regions as scoped construction islands whose effects escape only through explicit yields. AIR-REGION-X1 added the minimal region trace model and metadata-driven fixtures. X2 keeps the same fixture route and deferred integration, but adds a structured yield summary for the side-hole contract.

## Why yield contracts enforce locality

A parent body should not observe arbitrary internal mutation from a nested region. The parent sees a typed yield with an explicit affected scope. For the side-hole fixture this means: a face-attached local frame constructs a circular through-hole intent, and the only escaping effect is a `YieldSubtractiveVolume` contract whose integration remains deferred.

## Fixture path

The side-hole fixture is `fixtures/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

## Yield contract fields

The JSON trace stores the contract under each region's `yield` field when a region has an escaping yield:

- `attachment`: face attachment, parent region/body, face selector, face role, local frame, attachment diagnostics.
- `profile`: profile kind, local center, radius, loop kind, and profile frame.
- `direction`: direction kind, axis, sense, through flag, depth, diagnostics.
- `affectedScope`: parent-body-local feature scope, affected face selector, sibling impact flag, and explicit-yield-only flag.
- `boundaryIntent`: through-cut boundary, entry loop intent, deferred exit boundary, rim intent, patch intent, diagnostics.
- `integrationStatus`: remains `Deferred` for the side-hole fixture.

## Locality guarantees

X2 records these guarantees for the side-hole yield:

- escapes only through explicit yield;
- parent body local feature scope;
- no implicit parent mutation;
- no Boolean;
- no BRep emission.

## Side-hole contract

First-scope side-hole values are symbolic and deterministic:

- attachment face: `+X`;
- profile: `Circle`, center `(0,0)`, radius `1` in the local face/profile frame;
- direction: through/inward along the face normal (`FaceNormal`, `LocalZ`, `Inward`, `IsThrough=true`, `Depth=Through`);
- boundary: `ThroughCut`;
- entry: circular entry loop on the `+X` face;
- exit: opposite-side exit boundary deferred;
- rim: circular rim intent;
- affected scope: parent body only, local feature scope;
- integration: deferred.

## Invalid implicit parent mutation fixture

`fixtures/Region/invalid/implicit-parent-mutation.invalid.firmfixture` still rejects direct parent mutation. X2 preserves the X1 rejection and adds diagnostics for missing explicit yield and missing boundary contract.

## Trace output examples

Text output includes a `Region yield` block with `Feature: SideHole`, `Profile: Circle(radius=1, center=(0,0))`, `Direction: through inward along face normal`, `Boundary: ThroughCut`, affected scope, deferred integration, and guarantees.

JSON output includes stable fields under `regions.regions[].yield`, including `featureKind`, `yieldKind`, `attachment`, `profile`, `direction`, `affectedScope`, `boundaryIntent`, and `integrationStatus`.

## Non-goals

- no side-hole geometry;
- no Boolean;
- no BRepPlan integration;
- no CIR mirror;
- no production route replacement;
- no grammar expansion;
- no STEP smoke for side-hole;
- no BRep topology changes.

## Tests run

The implementation was validated with CLI help, side-hole text and JSON traces, implicit-parent-mutation JSON trace, parser-backed box trace, focused CLI tests, and the requested filtered .NET test commands.

## Recommended next milestone

Recommended: **AIR-REGION-X3 — Region BRepPlan boundary contract for side-hole, no integration**. X2 now carries enough attachment/profile/direction/boundary/scope intent to define an analysis-only BRepPlan boundary contract without committing to Boolean integration or production side-hole geometry.


## AIR-REGION-X3 CIR mirror note

AIR-REGION-X3 preserves the X2 side-hole yield boundary contract and adds a trace-only CIR mirror admission summary for the same `FaceAttachedRegion`. The summary backend is `cir-region-parent-minus-cylinder`, records parent `Box` and subtract `Cylinder` fields, remains analysis-only, and keeps parent integration deferred with no Boolean, no BRep emission, no BRepPlan integration, and no STEP authority.

## AIR-REGION-X4 boundary-contract note

AIR-REGION-X4 builds on the X2 yield contract by adding a trace-only BRepPlan boundary contract summary under the side-hole `FaceAttachedRegion`. The summary reuses X2 attachment/profile/direction/scope facts and records future topology-planning roles as strings while keeping parent integration deferred and denying emitted entry loop, exit loop, cut-wall face, boundary patch, BRepPlan element, BRep, Boolean, and STEP identities.

## AIR-REGION-X5 note

AIR-REGION-X5 adds a trace-only side-hole integration route decision scaffold. The side-hole `FaceAttachedRegion` now reports deterministic candidate statuses, selects `DeferredIntegration`, rejects Boolean fallback as not admitted, keeps the CIR mirror analysis-only, and keeps the BRepPlan boundary contract as topology-side intent without materialization.
