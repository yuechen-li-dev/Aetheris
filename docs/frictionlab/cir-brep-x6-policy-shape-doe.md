# CIR-BREP-X6: policy-shape DoE for scalable FRep→BRep materialization

## 1. Purpose and scope
FrictionLab-only design-of-experiments to choose production policy abstraction shape for FRep/CIR→BRep recovery intent.

## 2. Source code / labs inspected
- `Aetheris.Kernel.Firmament/Materializer/FrepMaterializerPlanner.cs`
- `Aetheris.Kernel.Firmament/Materializer/CirBoxCylinderRecognizer.cs`
- `Aetheris.Firmament.FrictionLab/CIRLab/FrepBrepRecoveryPolicyLab.cs`
- `Aetheris.Kernel.Core/Step242/Step242BsplineSurfaceRecoveryLane.cs`
- `docs/frictionlab/cir-brep-x5-frep-brep-recovery-policy-lab.md`
- `docs/cir-recovery-v1a-frep-materializer-policy-scaffold.md`

## 3. Candidate policy shapes
A) `BoxCylinderThroughHolePolicyShape` (pair-specific)
B) `ThroughHoleRecoveryPolicyShape` (semantic-first, bounded specialization)
C) `CylindricalToolCutPolicyShape` (tool-family)
D) `CirBooleanRecoveryPolicyShape` (generic boolean)
E) Fallbacks: `GenericNumericalContourPolicy`, `CirOnlyFallbackPolicy`

## 4. Scenario matrix
Implemented groups A/B/C/D/E/F in CIRLab scenario matrix with mixed real CIR and deferred synthetic records.

## 5. Policy evaluation dimensions
Recorded per policy/scenario:
`Admissible`, `Score`, `Capability`, `RecoveryPlanShape`, `EvidenceUsed`, `RejectedReasons`, `OverAdmits`, `UnderAdmits`, `PairSpecificityRisk`, `FutureScalabilityScore`, `Diagnostics`.

## 6. JudgmentEngine decision results
JudgmentEngine-based ranking picks `ThroughHoleRecoveryPolicyShape` for canonical A-scenarios, rejects it for B/C/E/F, and routes unsupported/deferred to fallback outcomes deterministically.

## 7. Over-admission / under-admission analysis
- Pair-specific: low over-admission, high under-admission for future variants.
- Through-hole: good canonical precision and bounded extensibility.
- Cylindrical-tool: over-admits notch/pocket/groove-like classes.
- Generic-boolean: over-broad dispatcher risk.

## 8. Pair-specific explosion risk analysis
Pair-specific policy scales poorly (`Host×Tool×Feature` multiplication) and pushes architecture toward policy proliferation.

## 9. Future scalability recommendation
Adopt semantic family lanes (e.g., ThroughHole, Counterbore, Countersink) with bounded host/tool specializations as internal recognizers.

## 10. Recommended production policy shape
Use `ThroughHoleRecoveryPolicy` as first production semantic policy.

## 11. Recommended recovery plan shape
Use semantic plan (not pair-specific) such as:
- host family/kind
- tool family/kind
- axis/interval classification
- entry/exit participation
- hole profile + expected patch/trim contracts
- capability + diagnostics

## 12. How BoxCylinderThroughHole should appear in architecture
As internal specialization helper/recognizer branch under `ThroughHoleRecoveryPolicy`, not as top-level long-term policy family.

## 13. Deferred/fallback policy guidance
Keep `GenericNumericalContourPolicy` and `CirOnlyFallbackPolicy` as explicit bounded fallbacks with clear capability semantics.

## 14. Recommended production implementation milestone
Implement production `ThroughHoleRecoveryPolicy` using `CirBoxCylinderRecognizer` as first specialization, returning a diagnostic-rich `ThroughHoleRecoveryPlan` before execution wiring.

## 15. Confidence ratings
- Policy-shape recommendation: High
- Scenario coverage adequacy: Medium-High
- Generalization risk assessment: Medium
