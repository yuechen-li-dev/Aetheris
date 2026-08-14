# BREP-BOOLEAN-STACK-A0 stepped coaxial stack root-cause verification

> Historical evidence; outcomes below are intentionally preserved. Current architecture: see [Current authoring and kernel boundaries](architecture/current-authoring-and-kernel-boundaries.md) and [BRep Boolean lessons](kernel/brep-boolean-lessons.md).

## 1) Purpose and scope

This milestone verifies (without fixing behavior) whether stepped-hole boolean failure is caused by hard N=2 cliffs in:

- validator (`BrepBooleanSafeCompositionGraphValidator`),
- builder (`BrepBooleanBoxCylinderHoleBuilder`).

Out of scope for A0: validator refactor, N-level classifier implementation, N-level topology/builder rewrite, policy/exporter/public-API changes.

## 2) Code/docs inspected

- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanSafeCompositionGraphValidator.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanBoxCylinderHoleBuilder.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanCoaxialSubtractStackFamily.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanSafeComposition.cs`
- `Aetheris.Kernel.Core/Brep/Boolean/BooleanDiagnostic.cs`
- Existing boolean tests under `Aetheris.Kernel.Core.Tests/Brep/Boolean/`
- `docs/cir-recovery-v13-stepped-hole-variant-executor-step.md`
- `docs/frictionlab/cir-brep-x8-generic-cir-brep-executor-lab.md`

`Claude on Stepped Boolean Root Cause.txt` was not present in repository at verification time.

## 3) Validator cliff result

Confirmed.

Observed behavior for stepped-like 3-stage coaxial stack on box root:

1. subtract through hole (`r=2`, `h=30`) succeeds,
2. subtract blind continuation (`r=3`, top-entry depth 6) succeeds,
3. subtract larger shallower blind (`r=4`, top-entry depth 3) fails.

Observed diagnostic on step 3:

- `Code`: `NotImplemented` (kernel-level mapping),
- `Source`: `BrepBoolean.AnalyticHole.HoleInterference`,
- message contains overlapping-hole rejection.

Mechanically this aligns with validator loop behavior:

- coaxial family classification is gated inside `if (composition.Holes.Count == 1 && (existingHole.IsBlind || nextHole.IsBlind))`,
- by step 3, `composition.Holes.Count == 2`, so no coaxial-stack classification path is executed,
- default pair overlap checks execute and reject as interference.

## 4) Builder cliff result

Hypothesis **not** confirmed as stated.

Builder probe using an injected N=3 coaxial composition (through + blind + blind) succeeded in current `BuildComposition` path and passed binding validation in this test harness.

So in this A0 probe, the builder did **not** emit `UnsupportedBlindHoleComposition` for N=3 shape data. That means validator cliff is clearly observed, while builder-side rejection did not reproduce with this controlled composition input.

## 5) `skipPairChecks` inspection

Current production flow:

- produced in `TryValidateBlindContinuationCandidate(..., out bool skipPairChecks, ...)`,
- consumed in `TryValidateNextSubtract` loop with `if (skipPairChecks) continue;`.

Behavior in current implementation is effectively pair-local, per existing hole iteration:

- only evaluated in the `composition.Holes.Count == 1` blind-continuation gate,
- for step-2 two-hole compositions it can skip remaining checks for that one pair,
- there is no current mixed-family partial iteration state for N>=3 because the blind continuation classifier gate does not run once hole count exceeds 1.

This means the feared partial-loop mixed-family dependency is not exercised in the current stepped third-subtract path; the cliff is earlier (gate condition itself).

## 6) Hypothesis status

**Outcome B — partially confirmed.**

- Validator cliff: confirmed.
- Builder cliff as explicit N>=3 rejection: not reproduced in this A0 probe; observed behavior differed (successful build for injected N=3 composition).

## 7) Recommended next milestone

Proceed to:

1. `BREP-BOOLEAN-STACK-A1`: validator continuation gate/generalization with explicit candidate scoring/diagnostics for N-level coaxial families.
2. `BREP-BOOLEAN-STACK-A2`: dedicated N-level coaxial stepped builder/topology path, bounded and test-driven.

Keep both milestones behavior-bounded and diagnostic-forward.
