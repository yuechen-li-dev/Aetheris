# BREP-BOOLEAN-STACK-A1 N-level coaxial validator generalization

> Historical evidence; outcomes below are intentionally preserved. Current architecture: see [Current authoring and kernel boundaries](architecture/current-authoring-and-kernel-boundaries.md) and [BRep Boolean lessons](kernel/brep-boolean-lessons.md).

## Summary

A1 generalizes safe-composition continuation validation so N-level coaxial subtract stacks are classified before independent-hole interference rejection runs.

## What changed

- Refactored continuation handling in `BrepBooleanSafeCompositionGraphValidator` to use one continuation decision for all existing-hole counts, not only `count == 1` blind pair checks.
- Added `NLevelCoaxialSubtractStack` continuation family in the JudgmentEngine candidate ladder.
- Moved overlap/tangent interference checks under the independent-hole continuation branch only.
- Added `BrepBooleanCoaxialSubtractStackFamily.TryClassifyNLevel(...)` for bounded stepped-stack admissibility:
  - all cylinders, world-Z aligned,
  - all coaxial XY centers,
  - exactly one through segment,
  - one or more top-entry blind segments,
  - strictly increasing radii and strictly shallower blind depths toward top entry.

## Behavioral result

- Third stepped coaxial subtract no longer fails at `BrepBoolean.AnalyticHole.HoleInterference` in validator.
- Independent-hole overlap rejection remains in place for independent continuations.
- Existing two-level counterbore/coaxial coverage remains preserved by existing pair classifier and tests.

## Next step

- BREP-BOOLEAN-STACK-A2 should focus on any downstream builder/topology blocker only if present after validator admission.

## A1.1 compatibility cleanup

- Classified `Subtract_ComposedBlindPocketThenOffsetThrough_ReturnsCoaxialSteppedDiagnostic` as **A: stale diagnostic expectation**.
- The fixture is intentionally non-coaxial (`offsetThrough` translated by `X=1.5`) and strongly overlapping (`r=7` blind pocket vs `r=3.5` through), so independent-hole interference rejection is the correct bounded-family outcome.
- Updated the test to assert failure with `BrepBoolean.AnalyticHole.HoleInterference` and overlap wording, and renamed it to `Subtract_ComposedBlindPocketThenOffsetThrough_RejectsWithHoleInterferenceDiagnostic`.
- A1 N-level coaxial admission remains unchanged for the canonical coaxial stepped continuation path.


## A2 follow-through
A2 validated that post-admission stepped execution remains deferred with explicit downstream blocker diagnostics; see `docs/brep-boolean-stack-a2-stepped-downstream-blocker.md`.
