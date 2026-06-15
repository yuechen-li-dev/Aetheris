# AIR-X2 Route Selection and Admissibility

## Purpose and scope

AIR-X2 adds a minimal internal route-selection and admissibility layer for AIR. Its purpose is to make route choice explicit, deterministic, inspectable, and testable while leaving production behavior unchanged.

AIR-X2 is not a production route replacement, not a geometry milestone, not BRepPlan, and not a full JudgmentEngine integration.

## Relationship to AIR-A0 and AIR-X1

AIR-A0 defines Aetheris as a compiler for BRep: Firmament is source intent, AIR is constructive geometry MIR, BRep is explicit topology authority, CIR is an admitted evaluation side-channel, and STEP is serialization rather than construction truth.

AIR-X1 introduced thin AIR wrappers around proven lanes. AIR-X2 adds route decisions in front of those wrappers for test/lab inspection only.

## Route-selection modes

- `Direct`: used when an already-canonical Constructive AIR node implies exactly one route. There is no scoring and no candidate competition.
- `SwitchMatch`: used when route choice is a closed, finite, deterministic structural classification. There is no utility scoring.
- `JudgmentUtility`: reserved for competing admissible routes where policy tradeoffs require scored diagnostics and explicit rejection reasons.
- `Unsupported`: used when AIR-X2 has no admissible route.

## Choosing the mode

Use direct selection for constructive nodes such as profile extrusion and prismatic section transition. Use switch/match selection for finite edge-finish classifications such as face-boundary-loop uniform chamfer versus arbitrary graph. Use JudgmentUtility only when multiple plausible routes compete.

JudgmentEngine should not be used for deterministic enum dispatch because it would obscure simple structural decisions behind unnecessary scoring. Conversely, switch/match must not replace utility policy when route tradeoffs genuinely compete, because fallback-free scored diagnostics are needed in that case.

## AIR-X2 model

The model records candidates, statuses, decisions, diagnostics, guarantees, known losses, and provenance. A successful decision has exactly one selected route. Rejected, deferred, unavailable, and not-applicable candidates retain stable reason codes. Silent fallback is forbidden.

## Implemented direct-selection examples

- `AirPrismaticSectionTransition` selects `PrismaticSectionTransitionEmitter`.
- `AirProfileExtrude` selects `ProfileExtrudeEmitter` without introducing a Core-to-Firmament dependency cycle.

## Implemented switch/match examples

- `FaceBoundaryLoop + UniformChamfer + history-known top-face loop` admits `TopFaceLoopChamferPrismatic`.
- `ArbitraryGraph + UniformChamfer` is rejected as `arbitrary-graph-unsupported`.
- `FaceBoundaryLoop + ConstantRadiusFillet` is deferred until single-edge fillet/corner evidence exists.
- `FaceBoundaryLoop + non-uniform/mixed rule` is rejected as `non-uniform-rule-unsupported`.

## JudgmentUtility status

AIR-X2 represents `JudgmentUtility` but defers wiring it to AIR-X3 policy work. The current direct and switch/match requests do not need scored route competition, so no production JudgmentEngine integration is forced.

## Relationship to production routes

There is no production route replacement and no default CLI behavior change. AIR-X2 decisions are internal and test-visible for now.

## Relationship to BRepPlan

BRepPlan is not implemented in AIR-X2. Future route decisions can select a Constructive AIR to BRepPlan path.

## Relationship to CIR

AIR-X2 does not change CIR mirror behavior. Future utility decisions may consider mirror availability as a scoring or capability input.

## Tests run

Focused AIR route-selection tests were added under `Aetheris.Kernel.Core.Tests`. The milestone validation commands are recorded in the implementation PR summary.

## Recommended next milestone

Recommended next milestone: AIR-X3 JudgmentUtility integration for one contested edge-finish route. AIR-X2 found direct and switch/match selection clean enough that the remaining new evidence is in scored competing-route policy rather than another deterministic selector.

## AIR-X3 direct-route BRepPlan proof

AIR-X3 proves that the direct `AirPrismaticSectionTransition` route can produce a planned topology summary before invoking the existing emitter. Route selection behavior is unchanged: direct selection remains appropriate for this named constructive route, and JudgmentUtility remains deferred.


## AIR-X4 evidence note

The AIR-X2 switch/match-selected `TopFaceLoopChamferPrismatic` route now has AIR-X4 BRepPlan evidence carrying `FaceBoundaryLoop`, `UniformChamfer`, `SwitchMatch`, and not-four-independent-single-edge-chamfers provenance. Route selection and JudgmentUtility behavior are unchanged.
