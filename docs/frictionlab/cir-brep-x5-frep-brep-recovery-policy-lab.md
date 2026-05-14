# CIR-BREP-X5: JudgmentEngine FRep→BRep recovery policy lab

## 1) Purpose and scope
Lab-only prototype for selecting bounded FRep/CIR→BRep recovery strategies via JudgmentEngine under `Aetheris.Firmament.FrictionLab/CIRLab` and `Aetheris.FrictionLab.Tests/CIRLab`.

## 2) Analytic recovery lane findings
`Step242BsplineSurfaceRecoveryLane` uses bounded candidates (`analytic_cylinder`, `reject`) with explicit admissibility gates (rational-like + cylinder probe), fixed utility scores, and concrete rejection reasons. It probes geometric invariants (ring profile, radius consistency, rail parallelism) before candidate admission.

## 3) JudgmentEngine usage findings
`JudgmentEngine<TContext>` provides deterministic selection by score, then tie-break priority, then name, then declaration order. This is appropriate because X5 is a bounded competing-strategy decision problem.

## 4) Proposed FRep→BRep recovery policy architecture
Use policy registry of admissibility-scored candidates over a shared `FrepBrepRecoveryContext`, returning rich evaluations and a global decision trace.

## 5) Candidate policy interface/result shape
Implemented:
- `IFrepBrepRecoveryPolicy`
- `FrepBrepRecoveryContext`
- `FrepBrepRecoveryPolicyEvaluation`
- `FrepBrepRecoveryDecision`

## 6) Evidence categories and score policy
Prototype evidence buckets:
- semantic recognizer match
- transform admissibility
- through-hole + strict clearance
- replay consistency
- trim-oracle agreement (available/deferred)
- volume/field agreement (available/deferred)
- export capability
- topology-template readiness

## 7) BoxCylinderThroughHolePolicy behavior
Uses `CirBrepX4RecognizerSourceContractLab` (which wraps production-recognizer semantics) and admits only recognized canonical box-cylinder through-hole pattern. Recommended route: existing BRep primitives + `BrepBoolean.Subtract` exact path.

## 8) Competing/fallback policy behavior
- `GenericNumericalContourPolicy`: always admissible bounded fallback for preview/analysis, lower score.
- `CirOnlyFallbackPolicy`: always admissible intent-preserving lowest-score fallback.

## 9) Positive canonical decision result
Canonical and translated box-cylinder fixtures select `BoxCylinderThroughHolePolicy` with highest score.

## 10) Rejection/fallback decision results
Non-subtract, box-sphere, blind cylinder, tangent/grazing, and unsupported transform cases reject semantic policy with explicit recognizer-derived reasons, then select fallback policy.

## 11) Generalized hypothesis
Best recovery should choose highest-level admissible semantic policy first, and only then fall back to lower-level numerical/intent routes.

## 12) Recommended production architecture
Productionize this as a dedicated FRep→BRep policy planner that consumes existing CIR recognizers, replay diagnostics, trim-oracle channels, readiness analyzers, and exporter capability probes.

## 13) Recommended first production milestone
Implement production `BoxCylinderThroughHolePolicy` planner lane, wired to materialization readiness + trim-oracle diagnostics, without changing exporter fallback behavior.

## 14) Known blockers/deferred evidence channels
- trim-oracle agreement currently represented as external context flags in lab
- volume agreement currently external context flags
- topology-template readiness not yet auto-derived in lab

## 15) Confidence ratings
- Policy architecture: High
- Semantic box-cylinder route: High
- Deferred evidence channel wiring: Medium
