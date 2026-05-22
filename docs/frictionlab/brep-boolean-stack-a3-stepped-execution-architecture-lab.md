# BREP-BOOLEAN-STACK-A3: stepped-hole execution architecture lab

## 1) Purpose and scope
FrictionLab-only architecture experiment to compare stepped-hole execution strategies without changing production executor behavior.

## 2) Code inspected
- `HoleRecoveryPolicy`, `SteppedHoleVariant`, `HoleRecoveryPlan`, `HoleRecoveryExecutor`.
- `BrepBoolean`, `BrepBoolean.Subtract/Union/Intersect`, `BrepBooleanSafeComposition`, graph validator/family/builders.
- `BrepPrimitives.CreateBox/CreateCylinder`, `Step242Exporter.ExportBody`.

## 3) Candidate strategies tested
- Hypothesis A: repeated subtract order variants.
- Hypothesis B: unioned tool then single subtract.
- Hypothesis C: direct N-level builder feasibility analysis.
- Hypothesis D: profile-stack tool-builder feasibility analysis.
- Hypothesis E: retain deferred baseline.

## 4) Scenario definition
Canonical bounded stepped hole:
- Host `box(30,30,20)`.
- Small through `r=2`.
- Medium blind `r=3`, depth 8.
- Large shallow `r=4`, depth 4.
- Coaxial around Z.

## 5) Strategy matrix results
Implemented in lab runner and asserted in tests. Successful strategies receive STEP smoke and topology summaries; failed/deferred strategies carry failure code/stage/diagnostics.

## 6) Repeated subtract order findings
All tested deterministic orders are represented and compared:
- `small->medium->large`
- `large->medium->small`
- `medium->large->small`

## 7) Unioned tool findings
Unioned tool route is executed when primitives/union/subtract succeed. Lab captures whether a single subtract is more stable and whether STEP markers remain valid.

## 8) Direct N-level builder feasibility
Feasible conceptually but not available as a current API surface in production codepaths; recorded as deferred analysis strategy.

## 9) Profile-stack tool builder feasibility
Feasible from `ProfileStack` data model and likely reusable for future variants; recorded as deferred analysis strategy.

## 10) STEP smoke results
For each successful strategy, smoke checks include:
- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `CYLINDRICAL_SURFACE`
- assert absence of `BREP_WITH_VOIDS`

## 11) SafeBooleanComposition findings
Lab records whether successful bodies preserve `SafeBooleanComposition` metadata.

## 12) Recommended production strategy
Lab returns an explicit recommendation token:
- `repeated-subtract-production`
- `unioned-tool-production`
- `n-level-builder-production`
- `profile-stack-tool-builder-production`
- `keep-deferred`

Current selection logic prefers successful unioned-tool, then successful repeated-subtract, else uses bounded JudgmentEngine fallback favoring profile-stack-builder analysis over n-level-builder, else keep-deferred.

## 13) Why rejected strategies were rejected
Each failed/deferred strategy reports explicit `FailureCode`, `FailureStage`, and diagnostics.

## 14) Risks and guardrails
- No production executor/boolean/exporter behavior changes.
- Lab-only code under FrictionLab folders.
- Deterministic matrix assertions included.

## 15) Recommended next production milestone
Promote winning strategy into production behind dedicated stability coverage and keep current deferred stepped execution until that milestone is complete.

## 16) Confidence ratings
- Strategy matrix plumbing: high.
- API-feasibility conclusions (C/D): medium.
- Production promotion readiness: medium-low pending broader stability corpus.
