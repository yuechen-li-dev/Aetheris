# BREP-BOOLEAN-STACK-A1 N-level coaxial validator generalization

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
