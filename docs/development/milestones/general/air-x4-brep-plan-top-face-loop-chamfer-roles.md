# AIR-X4 — BRepPlan roles/provenance for top-face loop chamfer

## Purpose and scope

AIR-X4 extends the AIR-X3 BRepPlan layer so the proven Class B top-face boundary-loop uniform chamfer lane can preserve feature-specific roles and provenance while still reusing the prismatic section-transition plan and existing emitter path.

This is a planning/provenance milestone only. It is not a production route replacement, geometry rewrite, new emitter, STEP exporter milestone, topology rewrite, CIR mirror change, Firmament lowering change, Boolean change, AirEdgeSweep change, or BrepBoundedChamfer/BrepBoundedFillet change.

## Relationship to AIR-A0/AIR-X1/AIR-X2/AIR-X3

AIR-A0 defines Aetheris as a compiler for BRep: Firmament is source intent, AIR is constructive geometry MIR, BRepPlan is the backend topology emission plan, BRep is explicit topology/export authority, CIR is an evaluation side-channel, and STEP is serialization rather than construction truth.

AIR-X1 introduced thin AIR wrappers around proven lanes, including top-face loop chamfer, without replacing production routes.

AIR-X2 selected the top-face loop chamfer route by switch/match classification as a Class B `FaceBoundaryLoop` `UniformChamfer` routed to `TopFaceLoopChamferPrismatic`.

AIR-X3 introduced the prismatic section-transition BRepPlan for planned topology IDs, roles, provenance, summaries, validation, diagnostics, and guarantees without materializing BRep.

AIR-X4 layers feature role/provenance information over that prismatic BRepPlan rather than duplicating the whole planner.

## Why feature roles matter

A pure prismatic section transition and a top-face loop chamfer can have the same topology count family. That does not make them the same feature. The upper interval faces in the chamfer lane are simultaneously prismatic transition faces and chamfer faces, and the plan must not collapse the feature into four independent single-edge chamfers.

## Top-face loop chamfer feature context

The AIR-X4 feature context records:

- source AIR node kind: `TopFaceLoopChamfer`;
- route: `TopFaceLoopChamferPrismatic`;
- route selection mode: `SwitchMatch`;
- selection class: `FaceBoundaryLoop`;
- rule kind: `UniformChamfer`;
- construction history: `generated/history-known`;
- note: `not-four-independent-single-edge-chamfers`.

## BRepPlan role overlay / wrapper design

AIR-X4 adds a dedicated `AirTopFaceLoopChamferBRepPlanner` wrapper around `AirPrismaticSectionTransitionBRepPlanner`. The wrapper validates the feature context, builds the canonical prismatic section stack through the existing top-face loop chamfer prototype helper, obtains the AIR-X3 prismatic BRepPlan, and applies a minimal semantic role overlay to the upper transition face elements.

The existing primary role remains `TransitionFace`. The semantic role list records both `PrismaticTransitionFace` and `ChamferFace` on upper transition faces. This avoids a broad role-inference engine while preserving both topology-family and feature meaning.

## Canonical geometry and expected planned topology

Canonical dimensions are width `10`, depth `8`, height `6`, and chamfer distance `1`.

The section stack is:

- `z0 = 0`: `(-5,-4)`, `(5,-4)`, `(5,4)`, `(-5,4)`;
- `z1 = 5`: `(-5,-4)`, `(5,-4)`, `(5,4)`, `(-5,4)`;
- `z2 = 6`: `(-4,-3)`, `(4,-3)`, `(4,3)`, `(-4,3)`.

Expected planned topology is the AIR-X3 canonical count family: 12 vertices, 12 section edges, 8 transition edges, 20 total edges, 10 faces, 2 cap faces, 4 lower side faces, 4 upper transition/chamfer faces, 10 loops, 40 coedges, one shell, one body, 10 planar faces, no cylindrical faces, bounds `[-5,-4,0]..[5,4,6]`, and split policy `preserve-section-splits`.

## Chamfer-face role assignment

Only feature-context-backed top-face loop chamfer planning marks upper transition faces as chamfer faces. Pure prismatic planning continues to report zero chamfer faces.

## Provenance and guarantees

The AIR-X4 plan records the not-four-independent-single-edge-chamfers guarantee and route exclusions:

- no AirEdgeSweep;
- no BrepBoundedChamfer;
- no Boolean;
- no topology graft;
- no coplanar merge;
- no production route replacement;
- no emitter rewrite;
- no STEP exporter change;
- no BRep topology behavior change.

## Comparison against existing loop chamfer wrapper/prototype

The focused tests compare the AIR-X4 plan summary against the existing AIR-X1 wrapper/prototype summary. Counts, bounds, chamfer transition face count, and STEP smoke remain supplied by the existing wrapper/emitter path rather than by BRepPlan materialization.

## Invalid/rejected/deferred feature contexts

The wrapper rejects or defers deterministic invalid contexts:

- missing/non-top-face-loop provenance: `air-x4-missing-loop-chamfer-provenance-rejected`;
- non-`FaceBoundaryLoop` selection: `air-x4-non-face-boundary-loop-rejected`;
- non-`UniformChamfer` rule: `air-x4-non-uniform-chamfer-rule-rejected`;
- non-`TopFaceLoopChamferPrismatic` route: `air-x4-non-prismatic-lowering-deferred`;
- non-top-face loop requests when representable: `air-x4-non-top-face-loop-deferred`.

## Non-goals

AIR-X4 does not change production routing, emitters, STEP exporter/importer, BRep topology behavior, route selection/JudgmentUtility behavior, CIR mirror behavior, Firmament lowering, Boolean behavior, AirEdgeSweep behavior, BrepBoundedChamfer/BrepBoundedFillet behavior, chamfer/fillet/shell geometry, arbitrary graph support, import/recovery, triangle migration, or NURBS/freeform behavior.

## Tests run

- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "AirTopFaceLoopChamferBRepPlanner|AirPrismaticSectionTransitionBRepPlanner_PurePrismatic" -v:minimal`

## Recommended next milestone

Recommended: **AIR-X5 — BRepPlan materialization adapter for prismatic transition, still non-production**. AIR-X4 showed that roles/provenance can be layered without changing production emitters; the next useful evidence is a non-production adapter that consumes these plans while preserving BRep as topology authority and keeping production routes untouched.

## AIR-X5 status note

AIR-X5 adds an internal/test-visible AIR-to-CIR mirror adapter envelope for generated prismatic section transitions and top-face loop chamfers when existing convex polyhedron mirror evidence admits them. The adapter preserves AIR provenance metadata but explicitly denies CIR face identity, loop identity, topology parity, chamfer-face identity, feature labels, and BRepPlan role parity. It does not change production analyzer behavior, route selection, BRepPlan behavior, BRep topology, STEP import/export, CIR evaluator/tape behavior, Firmament lowering, Boolean behavior, or chamfer/fillet/shell geometry. See [AIR-X5 — AIR-to-CIR mirror adapter envelope](air-x5-air-to-cir-mirror-adapter-envelope.md).

## AIR-X6 trace reporting

`aetheris trace` reports the AIR-X4 top-face loop chamfer role overlay, including Class B face-boundary-loop provenance, UniformChamfer rule provenance, and the four chamfer-face semantic roles. This preserves the doctrine that the case is not four independent single-edge chamfers.

## AIR-X7 fixture note

The AIR-X7 valid Chamfer corpus includes `fixtures/Regression/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture`, which traces the Class B top-face boundary-loop uniform chamfer through the existing `TopFaceLoopChamferPrismatic` route and preserves the AIR-X4 BRepPlan chamfer-face role count of four.

## AIR-REGION-X4 contract-only region role note

AIR-REGION-X4 uses planned role strings inside the side-hole region `brepBoundary` trace summary instead of extending global BRepPlan role enums. These strings are future topology-planning intent only and do not materialize BRepPlan elements or claim emitted face/loop identity.
