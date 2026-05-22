# BREP-BOOLEAN-STACK-A3: stepped-hole execution architecture lab (A3.1 diagnostic completion)

## 1) Purpose and scope
FrictionLab-only architecture experiment to compare stepped-hole execution strategies without changing production executor behavior.

## 2) Code inspected (source of truth)
- `Aetheris.Firmament.FrictionLab/CIRLab/SteppedHoleExecutionArchitectureLab.cs`
- `Aetheris.Firmament.FrictionLab/CIRLab/SteppedHoleExecutionArchitectureLabResult.cs`
- `Aetheris.FrictionLab.Tests/CIRLab/SteppedHoleExecutionArchitectureLabTests.cs`
- `Aetheris.Kernel.Firmament/Materializer/HoleRecoveryExecutor.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanSafeComposition.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanBoxCylinderHoleBuilder.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanCoaxialSubtractStackFamily.cs`
- `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`

## 3) Scenario definition
Canonical bounded stepped hole:
- Host `box(30,30,20)`.
- Small through `r=2`.
- Medium blind `r=3`, depth 8.
- Large shallow `r=4`, depth 4.
- Coaxial around Z.

## 4) Observed strategy matrix (canonical run)

| Strategy | Status | Executed? | BodyProduced? | StepSmokeAttempted? | StepSmokeSucceeded? | FailureStage | FailureCode | SafeBooleanCompositionPresent? | TopologyCounts | RecommendationWeight / Notes |
|---|---|---:|---:|---:|---:|---|---|---:|---|---|
| RepeatedSubtract_SmallMediumLarge (`repeated-subtract-small-medium-large`) | Succeeded | Yes | Yes | Yes | Yes | `none` | `none` | Yes | F=11, L=15, E=21, CE=42, V=20 | Successful candidate; export markers present. |
| RepeatedSubtract_LargeMediumSmall (`repeated-subtract-large-medium-small`) | Succeeded | Yes | Yes | Yes | Yes | `none` | `none` | Yes | F=11, L=15, E=21, CE=42, V=20 | Successful candidate; export markers present. |
| RepeatedSubtract_MediumLargeSmall (`repeated-subtract-medium-large-small`) | Succeeded | Yes | Yes | Yes | Yes | `none` | `none` | Yes | F=11, L=15, E=21, CE=42, V=20 | Successful candidate; export markers present. |
| UnionedToolThenSubtract (`unioned-tool-single-subtract`) | Failed | Yes | No | No | No | `boolean-union` | `union-small-medium-failed` | No | F=0, L=0, E=0, CE=0, V=0 | Union small+medium failed (`NotImplemented`), so route blocked before final subtract. |
| DirectNLevelBuilderAnalysis (`n-level-builder-analysis`) | Deferred | No | No | No | No | `analysis` | `n-level-builder-not-implemented` | No | F=0, L=0, E=0, CE=0, V=0 | Existing pair classifier/builder path is 2-level-focused; no direct N-level production builder entrypoint wired in runner path. |
| ProfileStackToolBuilderAnalysis (`profile-stack-tool-builder-analysis`) | Deferred | No | No | No | No | `analysis` | `profile-stack-tool-builder-missing` | No | F=0, L=0, E=0, CE=0, V=0 | `HoleRecoveryPlan.ProfileStack` contains tier stack data but no reusable profile-stack-to-tool builder utility exists in current path. |
| KeepDeferredBaseline (`deferred-baseline-current-production`) | Deferred | Yes | No | No | No | `executor` | `stepped-execution-deferred` | No | F=0, L=0, E=0, CE=0, V=0 | Current production executor still defers stepped path after large-subtract blocker (`SteppedHoleExecutionDeferredPostValidator: blocker-at-large-subtract`). |
| CounterboreBaseline (`counterbore-baseline`) | Succeeded | Yes | Yes | Yes | Yes | `none` | `none` | Yes | F=9, L=12, E=18, CE=36, V=16 | Confirms N=2 coaxial counterbore baseline remains healthy in current bounded flow. |

## 5) Repeated subtract variants (per-order outcomes)
- `small -> medium -> large`: **Succeeded**; STEP smoke attempted/succeeded; body exported; `SafeBooleanComposition` present.
- `large -> medium -> small`: **Succeeded**; STEP smoke attempted/succeeded; body exported; `SafeBooleanComposition` present.
- `medium -> large -> small`: **Succeeded**; STEP smoke attempted/succeeded; body exported; `SafeBooleanComposition` present.

All successful rows emitted canonical STEP smoke markers (`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CYLINDRICAL_SURFACE`) and omitted `BREP_WITH_VOIDS`.

## 6) Unioned tool route outcome
- Tool union was attempted (`small ∪ medium` first).
- First union failed at stage `boolean-union` with failure code `union-small-medium-failed`.
- Diagnostic payload includes `NotImplemented` from boolean diagnostics.
- Because union failed, final subtract and STEP smoke were not attempted.
- Exact analytic-surface preservation could not be observed on this route because route did not produce a body.
- `SafeBooleanComposition` metadata did not persist for this route (no result body).

## 7) Direct N-level builder feasibility outcome
- Direct N-level classifier feasibility exists (`BrepBooleanCoaxialSubtractStackFamily.TryClassifyNLevel`), but there is no production N-level builder route used by the lab to execute this strategy end-to-end.
- It would require generalized N-tier tool/profile construction + topology/surface binding generation beyond the current pair-focused execution path.
- A3 did not execute this route because it is analysis-only at this milestone (`n-level-builder-not-implemented`).

## 8) Profile-stack tool-builder feasibility outcome
- `HoleRecoveryPlan.ProfileStack` already carries tier radii/depth ordering needed to drive a stacked tool-builder concept.
- Missing piece: reusable profile-stack tool-builder utility wired into a strategy execution path.
- This path is judged more scalable than hand-specializing direct N-level topology for each tier count, but remains unimplemented in current runner.
- A3 marks this route deferred as `profile-stack-tool-builder-missing`.

## 9) Counterbore baseline outcome
- `counterbore-baseline`: **Succeeded** with STEP smoke success and body produced.
- Why it matters: it verifies the existing bounded N=2 coaxial stack behavior remains intact while stepped-hole architecture remains diagnostic/deferred.
- This is the control signal that 2-level coaxial flow is still healthy.

## 10) Selected recommendation
SelectedRecommendation = `repeated-subtract-production`

Reason = Unioned-tool route failed at boolean-union (`union-small-medium-failed`) while all three repeated-subtract order variants succeeded with STEP smoke and safe-composition presence. Deferred routes (direct N-level and profile-stack builder) are analysis-only in current milestone.

Confidence = **medium** (strong evidence for this canonical scenario; broader corpus still needed before production promotion).

## 11) Recommended production strategy and rejects
Recommended production strategy:
  `repeated-subtract-production`

Why:
  Matrix evidence shows 3/3 repeated-subtract variants succeeded end-to-end in canonical stepped scenario; unioned-tool route fails before subtract; deferred-analysis routes are not executable today.

Rejected strategies:
  - repeated subtract: not rejected in A3.1 (selected winner for next production route).
  - unioned tool: rejected for now due to concrete union blocker (`union-small-medium-failed`, `NotImplemented`).
  - n-level builder: rejected for immediate production promotion because execution path is not implemented in current lab/production wiring.
  - profile-stack builder: rejected for immediate promotion because builder utility is missing, despite favorable scalability potential.
  - keep deferred: rejected as final recommendation because executable repeated-subtract evidence exists in canonical case.

Next production milestone:
  **BREP-BOOLEAN-STACK-A4: repeated-subtract productionization gate** (scope: production stepped executor strategy wiring + stability corpus, without union/tool-builder refactors in same milestone).
