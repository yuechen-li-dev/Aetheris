# AIR-REGION-X3 — Side-hole CIR mirror analysis

## Purpose and scope

AIR-REGION-X3 adds trace-only evidence that the metadata-driven side-hole `FaceAttachedRegion` yield can be admitted to an analysis-side CIR mirror summary. The milestone does not implement side-hole BRep geometry, BRepPlan integration, production Boolean, STEP export, parser grammar, or production analyzer behavior.

## Relationship to AIR-A1, X1, X2, and AIR-X5

AIR-A1 defines AIR Regions as local construction-intent islands whose effects escape through explicit yields. AIR-REGION-X1 introduced the trace-only root/face-attached region skeleton, and AIR-REGION-X2 made the side-hole yield boundary contract explicit. AIR-X5 requires AIR-to-CIR mirrors to remain side-channel adapters with explicit status, capabilities, provenance, losses, and no topology authority.

X3 applies that envelope to the side-hole region yield: AIR owns the local side-hole intent, while the CIR mirror records an evaluation-field interpretation only.

## Side-hole CIR mirror doctrine

The side-hole region may mirror as:

```text
parent box field minus local cylinder field
```

This is analysis evidence only. It does not make CIR the owner of topology, face identity, entry/exit loop identity, boundary patch identity, BRepPlan roles, STEP/export authority, or production integration.

## Mirror model

The trace summary records:

- backend: `cir-region-parent-minus-cylinder`;
- status: `mirror-admitted-conservative`;
- effect: `Subtractive`;
- parent field: `Box`;
- subtract field: `Cylinder`;
- capabilities: `bounds`, `containment`, and `occupancy` as summary-level analysis claims;
- losses: topology, face, entry loop, exit loop, boundary patch, BRepPlan role parity, STEP/export authority, and production integration.

Actual CIR evaluator/tape composition is deferred. X3 intentionally implements a deterministic admission summary rather than constructing executable CIR nodes.

## Fixture path and expected trace stage

Fixture: `fixtures/Regression/Region/valid/side-hole-face-attached-region.valid.firmfixture`.

Expected stage advances to `region-cir-mirror` because the fixture now reports an admitted region CIR mirror summary. Parent integration remains `Deferred` on the region yield.

## Capabilities actually admitted

The summary admits only `occupancy`, `containment`, and `bounds`. It does not admit map, volume estimate, face identity, topology parity, loop identity, or patch identity.

## Known losses

The mirror explicitly records:

- no topology authority;
- no face identity;
- no entry-loop identity;
- no exit-loop identity;
- no boundary patch identity;
- no BRepPlan role parity;
- no STEP/export authority;
- no production integration.

## No Boolean, BRep, or STEP guarantees

The trace keeps the AIR-REGION-X2 guarantees: no production Boolean invocation, no side-hole BRep emission, no BRepPlan success, no STEP smoke, and no production route replacement.

## Actual CIR composition status

X3 is summary-only. The intended model is parent-box field minus cylinder field, but executable CIR primitive composition and sampling are deferred to a later milestone.

## Tests run

Validation used the focused CLI trace commands and filtered test commands recorded in the PR summary.

## Recommended next milestone

Recommended next milestone: **AIR-REGION-X4 — Region-to-CIR occupancy sample artifact**. X3 found the mirror summary boundary clean, so the next narrow step is to add an explicit trace/test-only sample artifact without changing production analyzer behavior or claiming topology authority.

## AIR-REGION-X4 boundary-contract note

AIR-REGION-X4 preserves the X3 CIR mirror as analysis-only and adds a separate `brepBoundary` trace summary for side-hole topology-side intent. The BRep boundary summary records affected `+X` parent face intent, circular entry-loop intent, deferred opposite-side exit intent, deferred cylindrical cut-wall intent, planned semantic role strings, losses, and guarantees. It does not grant CIR topology authority and does not materialize BRepPlan, BRep, Boolean, or STEP output.

## AIR-REGION-X5 note

AIR-REGION-X5 adds a trace-only side-hole integration route decision scaffold. The side-hole `FaceAttachedRegion` now reports deterministic candidate statuses, selects `DeferredIntegration`, rejects Boolean fallback as not admitted, keeps the CIR mirror analysis-only, and keeps the BRepPlan boundary contract as topology-side intent without materialization.

## AIR-REGION-X7 note

AIR-REGION-X7 consumes the controlled side-hole placeholder plan for the `+X` fixture and materializes standalone patch evidence for the entry loop, exit loop, and cylindrical cut wall. Parent BRep integration remains deferred; CIR remains analysis-only; Boolean is not generally admitted; no production route replacement or general side-hole support is introduced.
