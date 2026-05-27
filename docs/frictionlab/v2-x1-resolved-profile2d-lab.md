# V2-X1 — Minimal ResolvedProfile2D lab

## Purpose and scope
This lab introduces a **lab-only** minimal `ResolvedProfile2D` prototype for Aetheris V2. It validates explicit 2D region topology and emits deterministic diagnostics/artifacts for simple line/arc/circle loops.

Scope is intentionally narrow:
- one material outer loop,
- zero or more hole loops,
- bounded line/arc/full-circle curves,
- deterministic validation and recommendations.

## Architecture references
- V2 doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- V2-A1 profile contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`

This lab aligns to V2-A1’s conclusion that `ResolvedProfile2D` is a **validated constructive topology contract**, not a sketch/constraint solver and not a Boolean normalization system.

## Minimal curve set
- `LabAirLineSegment2D` (finite start/end, non-zero length)
- `LabAirCircularArc2D` (finite center, positive radius, non-zero bounded sweep)
- `LabAirFullCircle2D` (finite center, positive radius, explicit full-circle primitive)

## Validation rules
### Curve validation
- finite values,
- positive radius for arc/circle,
- non-zero line length,
- non-zero arc sweep.

### Loop validation
- non-empty loop,
- ordered curve chain,
- loop closure,
- no zero-length segments,
- orientation detection,
- self-intersection rejection for supported sampled line/arc/circle loops.

### Region validation
- exactly one material outer loop for V2-X1,
- holes inside outer,
- hole-hole non-overlap,
- hole-boundary non-touch/non-cross,
- multiple material outers deferred,
- nested island-in-hole deferred.

## Diagnostics contract
The lab emits deterministic machine-checkable diagnostics, including:
- `profile-loop-open`
- `profile-loop-zero-length-segment`
- `profile-loop-self-intersection`
- `profile-region-missing-outer-loop`
- `profile-region-hole-outside-outer`
- `profile-region-hole-overlaps-hole`
- `profile-region-hole-touches-boundary`
- `profile-region-multiple-outer-loops-deferred`
- `profile-region-nested-island-deferred`
- `profile-normalized-orientation`

## Test cases covered
Valid:
1. rectangle outer loop
2. full circle outer loop (explicit full-circle primitive)
3. rectangle + one circular hole
4. rectangle + two non-overlapping circular holes
5. reversed input orientation normalized deterministically

Invalid/deferred:
1. open loop
2. endpoint mismatch/open closure failure
3. zero-length line
4. invalid/zero-radius arc
5. zero-sweep arc
6. self-intersecting bow-tie
7. hole outside outer
8. hole touching outer boundary
9. overlapping holes
10. disjoint multiple outers deferred
11. nested island inside hole deferred

## Explicit non-goals
- no sketch solver,
- no coincident/tangent/dimension constraint solving,
- no 2D Boolean normalization,
- no BRep emission,
- no production AIR consumption/routing changes,
- no STEP importer/exporter changes,
- no NURBS/freeform curves.

## Findings
### What worked
- Deterministic case table generation with stable statuses/diagnostics.
- Explicit full-circle loop support as foundational primitive for hole/cylindrical profile families.
- Deterministic orientation normalization signaling via diagnostic.

### What was rejected/deferred
- multiple material outers: `profile-region-multiple-outer-loops-deferred`
- nested island topologies: `profile-region-nested-island-deferred`

### What remains deferred
- profile expression normalization / general 2D Boolean normalization,
- production AIR atom consumption and emission routing.

## Recommendation for next milestone
Recommended next step: **V2-X3 profile-with-hole extrude lab** if immediate AIR profile-hole execution evidence is preferred, or **V2-X2 circular-profile AirExtrude lab** if cylinder/counterbore-first path is preferred.
