# V2-X4 — Bounded 2D Profile Boolean Normalization Lab

## Purpose and scope
Lab-only experiment proving compile-time profile Boolean normalization for a bounded expression subset, producing `LabResolvedProfile2D` before any 3D emission.

References:
- V2 doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- V2-A1 resolved contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- V2-X1 profile contract lab: `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- V2-X3 profile-hole extrusion lab: `docs/frictionlab/v2-x3-profile-with-hole-extrude-lab.md`

## Compile-time Boolean thesis
This lab evaluates profile intent structurally in 2D and normalizes accepted cases into one outer loop plus hole loops before 3D topology. It intentionally avoids runtime 3D topology discovery and all 3D Boolean operations.

## Supported subset
- `Difference(Rectangle, CircleInside)`.
- `Difference(Rectangle, CircleInside...)` where circles are strictly inside, non-touching boundary, and non-overlapping.
- `Union(Rectangle, Rectangle)` only for identical or strict containment.
- `Intersect(Rectangle, Rectangle)` only for strict containment.

## Normalization rules
- Accepted difference cases emit rectangle outer loop and CW full-circle hole loops.
- Accepted union/intersection containment cases emit one rectangle loop.
- All accepted outputs include `profile-boolean-no-3d-boolean-used`.
- Unsupported topology is explicitly deferred with deterministic diagnostics.

## Diagnostics contract
Representative diagnostics:
- start/completion: `profile-boolean-normalization-started`, `profile-boolean-normalized-to-resolved-profile`.
- recognized: `profile-boolean-difference-rectangle-circle-recognized`, `profile-boolean-difference-rectangle-multicircle-recognized`.
- invalid: `profile-boolean-invalid-expression`, `profile-boolean-invalid-primitive`, `profile-boolean-circle-outside-rectangle`, `profile-boolean-circle-touches-boundary`, `profile-boolean-circles-overlap`.
- deferred: `profile-boolean-capsule-deferred`, `profile-boolean-union-normalization-deferred`, `profile-boolean-intersection-normalization-deferred`, `profile-boolean-multiple-islands-deferred`, `profile-boolean-nested-topology-deferred`.

Recommendations are finite:
- `profile_boolean_normalized`
- `profile_boolean_invalid_rejected`
- `profile_boolean_deferred_topology`
- `profile_boolean_needs_bounded_clipping_lab`

## Test cases and results
Implemented rows cover requested successful, invalid, and deferred scenarios in lab artifacts and tests (`ProfileBooleanNormalizationLabTests`). Successful normalized profiles are additionally validated through V2-X1 `ResolvedProfile2DLab.Evaluate`.

## Deferred/rejected cases
Deferred examples:
- rectangle subtraction by rectangle (multiple-island risk),
- disjoint/partial-overlap union,
- partial-overlap intersection,
- nested difference expression,
- capsule subtraction.

Rejected examples:
- invalid primitive dimensions,
- circles outside or touching rectangle boundary,
- overlapping circles,
- unsupported primitive.

## Optional chained evidence
Not implemented in this milestone. Chaining into V2-X3 profile-hole extrude remains optional and lab-only.

## Non-goals
- No production routing changes.
- No full 2D clipping engine.
- No sketch solver.
- No required BRep emission.
- No STEP importer/exporter changes.
- No Boolean core changes.

## Recommended next step
Run a bounded clipping lab for rectangle-rectangle overlap normalization, or a dedicated slot/capsule profile normalization+extrusion lab before any production expression-routing decisions.


## V2-V3 chaining note
V2-V3 now chains accepted V2-X4 normalized outputs into `ProfileHoleExtrudeEmitter` through a lab adapter (`ProfileBooleanExtrudePipelineLab`), preserving deferred/rejected stops before any BRep emission.


Update note (V2-V3): normalization lab remains the bounded proof source; production-adjacent orchestration now consumes equivalent bounded rules in Firmament internal front-door flow.
