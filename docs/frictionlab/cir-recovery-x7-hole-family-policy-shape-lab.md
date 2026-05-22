# CIR-RECOVERY-X7: semantic hole-family policy-shape lab

## 1. Purpose and scope
This FrictionLab-only architecture/design lab evaluates scalable semantic policy-family shapes for hole-like recovery (through, blind, counterbore, countersink, stepped) without changing production policy/catalog/executor behavior.

## 2. Source code/docs inspected
- `Aetheris.Kernel.Firmament/Materializer/FrepMaterializerPolicyCatalog.cs`
- `Aetheris.Kernel.Firmament/Materializer/ThroughHoleRecoveryPolicy.cs`
- `Aetheris.Kernel.Firmament/Materializer/ThroughHoleRecoveryExecutor.cs`
- `Aetheris.Kernel.Firmament/Materializer/FrepMaterializerPlanner.cs`
- `Aetheris.Kernel.Firmament/Materializer/CirBoxCylinderRecognizer.cs`
- `docs/frictionlab/cir-brep-x5-frep-brep-recovery-policy-lab.md`
- `docs/frictionlab/cir-brep-x6-policy-shape-doe.md`
- `docs/cir-recovery-v5-policy-catalog.md`
- `docs/cir-recovery-v1b-through-hole-policy.md`
- `docs/surface-feature-a0-architecture-audit.md` through `surface-feature-a4-planar-groove-evidence.md`
- `docs/boolean-deferred.md`, `docs/groove-a0-bounded-revolved-groove-audit.md`

## 3. Existing hole-like vocabulary inventory
- Through-hole: first-class semantic recovery policy (`ThroughHoleRecoveryPolicy`) and plan/executor path in production.
- Blind hole: represented in docs/fixtures and deferred boolean families; not first-class policy in catalog.
- Counterbore/countersink: present in FrictionLab fixtures and docs vocabulary; not first-class in production recovery catalog.
- Stepped hole/chamfered entry: conceptually present via feature language/docs; not first-class recovery policies.
- Threaded hole: explicitly deferred/forge-like path.
- Slot/keyway: present as bounded subtract/prismatic families (non-hole-family subsystem).
- Round groove/surface features: active surface-feature descriptor/planning/evidence track (A0-A4), distinct from hole-family policy lane.

## 4. Candidate architecture shapes
- **A SeparatePolicySet**: one top-level policy per hole type.
- **B MonolithicHolePolicy**: one large top-level policy with variant branch maze.
- **C CompositionalHolePolicy**: one top-level hole policy delegating to host/axis/depth/entry/profile classifiers + plan builder.
- **D ProfileStackPolicy**: axial segment/profile-stack policy.

## 5. Scenario matrix
Implemented groups A-E with required scenarios:
- A: currently supported through-hole variants (A1-A3)
- B: likely next exact families (blind/counterbore/countersink/stepped/chamfered-entry)
- C: future host/tool variants
- D: should defer to Forge/deferred
- E: non-hole families (groove, keyway, notch, sphere cavity, torus subtract)

## 6. Evaluation dimensions
Per architecture/scenario scoring includes:
- `AdmitsScenario`
- `CorrectlyRejectsScenario`
- `PolicyCountGrowth`
- `BranchComplexity`
- `LocalityOfChangeScore`
- `DiagnosticClarityScore`
- `FutureScalabilityScore`
- `RiskOfOverAdmission`
- `RiskOfUnderAdmission`
- `PlanShapeQuality`
- `ExecutorReusability`

## 7. JudgmentEngine/meta-scoring method
`JudgmentEngine` is used for bounded meta-selection among architecture candidates with deterministic aggregate scoring. This matches AGENTS guidance (multiple bounded strategies, explicit admissibility/scoring/tie-break).

## 8. Results summary
Across canonical, future, deferred, and non-hole scenarios:
- **CompositionalHolePolicy** consistently wins on locality/generalizability/diagnostic clarity balance with bounded admission.
- **SeparatePolicySet** is precise but trends toward policy-count growth and cross-policy drift.
- **MonolithicHolePolicy** performs poorly on branch complexity and locality-of-change.
- **ProfileStackPolicy** is expressive but over-admits cylindrical non-hole cases unless tightly gated by host/entry/depth semantics.

## 9. Over/under-admission analysis
- Over-admission risk highest in profile/tool-stack-only interpretation (e.g., side-notch cylindrical subtract).
- Under-admission risk highest in separate-policy set for future variants before policy additions.
- Compositional policy best balances bounded admissibility with extension points.

## 10. Locality-of-change analysis
Compositional shape supports additive module growth (new classifier/plan-segment builder + registration) with minimal edits; monolithic shape requires central branch edits and larger retest surface.

## 11. Future scalability recommendation
Adopt **Option C** as production trajectory:
- top-level `HoleRecoveryPolicy`
- internal classifier + variant modules
- explicit rejected/deferred reasons
- shared plan contracts and executor-reuse seam

## 12. Recommended production policy shape
```text
HoleRecoveryPolicy
  HostClassifier
  AxisClassifier
  HoleDepthClassifier
  EntryFeatureClassifier
  ToolProfileClassifier
  RecoveryPlanBuilder
```

## 13. Recommended production plan shape
```csharp
HoleRecoveryPlan
  HostKind
  Axis
  HoleKind
  DepthKind
  ProfileStack
  EntryFeature
  ExitFeature
  ExpectedSurfacePatches
  ExpectedTrimCurves
  Capability
  Diagnostics

HoleProfileSegment
  SegmentKind: Cylinder | Cone | Chamfer | ThreadDeferred
  RadiusStart
  RadiusEnd
  DepthStart
  DepthEnd
  SurfaceFamily
```
Mapping from current `ThroughHoleRecoveryPlan`: becomes `HoleKind=Through`, `DepthKind=Through`, one cylindrical segment, circular entry/exit rims, same expected patch/trim evidence.

## 14. What happens to current `ThroughHoleRecoveryPolicy`
Short-term keep as top-level production policy for stability; next milestone wrap/migrate semantics into `HoleRecoveryPolicy` first variant (`ThroughHoleVariant`) while preserving score band and diagnostics.

## 15. Recommended next production milestone
Introduce production `HoleRecoveryPolicy` scaffold with only `ThroughHoleVariant` implemented and catalog-registered above fallback, while leaving behavior-equivalent output to today; then add counterbore/countersink variants as local modules.

## 16. Confidence ratings
- Policy-family shape recommendation: **High**
- Scenario matrix sufficiency: **Medium-High**
- Plan-shape migration confidence: **Medium**
- Over/under-admission risk characterization: **Medium-High**
