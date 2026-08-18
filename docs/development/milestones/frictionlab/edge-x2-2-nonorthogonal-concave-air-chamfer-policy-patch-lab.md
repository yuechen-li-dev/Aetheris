# EDGE-X2.2 — Non-orthogonal concave planar AirChamfer policy+patch lab

## Purpose and scope
EDGE-X2.2 extends the EDGE-X2.1 lab-only policy scaffold to admit a bounded non-orthogonal concave planar single-edge case, then constructs a deterministic ruled planar chamfer patch artifact for accepted cases.

This remains **lab-only**:
- no production route changes,
- no public API changes,
- no STEP importer/exporter changes,
- no Boolean core changes,
- no production chamfer/fillet behavior changes.

## References
- `docs/development/milestones/general/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/development/milestones/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`
- `docs/development/milestones/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`
- `docs/development/milestones/frictionlab/edge-x2-1-air-chamfer-policy-scaffold-lab.md`

## Why this expansion
EDGE-X2 proved canonical orthogonal concave planar patch construction and EDGE-X2.1 proved deterministic policy/score gating. Non-orthogonal concave planar single-edge chamfer is the next bounded expansion because it exercises realistic planar adjacency variation while still avoiding convex replacement, chains/corners, and production topology rewrites.

## Policy changes in EDGE-X2.2
- Safe non-orthogonal concave planar requests are now admissible (`accept-air-chamfer-patch`).
- Shallow/unstable non-orthogonal cases defer (`defer-nonorthogonal-policy`).
- Near-parallel adjacency remains rejected as invalid face adjacency (`reject-invalid-face-adjacency`).
- Convex, chain, corner, unsupported face family, and legacy-dependent cases remain deferred/fallback/rejected as before.

Deterministic score fields are preserved:
- `GeometrySupportScore`
- `TopologyRiskScore`
- `OffsetStabilityScore`
- `CornerPolicyScore`
- `LegacyDependencyScore`
- `OverallUtility`

## Angle/offset admissibility rules
- face normals must be finite and unit-normalizable,
- edge direction must be finite and non-zero,
- planar adjacency must not be parallel/anti-parallel within tolerance,
- non-orthogonal face-normal angle must be stable (not shallow / not near-parallel),
- offset directions must be finite/normalizable,
- patch area must be finite and strictly positive above tolerance.

## Fixtures and results
Added fixtures include:
- `nonorthogonal-concave-planar-safe` => accepted.
- `nonorthogonal-concave-planar-shallow` => deferred with explicit policy diagnostic.
- `nonorthogonal-concave-planar-near-parallel` => rejected as invalid adjacency.
- existing `ambiguous-classification` => rejected.

## Topology/artifact findings
For accepted non-orthogonal cases, patch artifact remains deterministic:
- vertices: 4
- edges: 4
- faces: 1
- planar faces: 1
- boundary loops: 1
- coedges: 4

Diagnostics include:
- `edge-x2-2-nonorthogonal-offset-curves-constructed`
- `edge-x2-2-nonorthogonal-patch-constructed`
- `edge-x2-2-nonorthogonal-patch-planarity-validated`

## STEP/export status
Open patch STEP export remains deferred (`edge-x2-step-smoke-deferred:open-patch-export-unsupported`). No exporter/importer behavior changed.

## No-3D-Boolean guarantee
Accepted geometry paths still emit `edge-x2-no-3d-boolean-used` and policy rows still emit `edge-x2-1-no-3d-boolean-used`.

## Invalid/deferred cases
- Invalid distance/edge/face adjacency remain deterministic rejects.
- Ambiguous classification remains deterministic reject.
- Shallow unstable non-orthogonal remains deterministic defer before geometry path commitment.

## Non-goals
- convex replacement geometry
- fillet geometry
- edge chains/corner chains
- triangle migration retry
- sketch solver/clipping engine/NURBS/freeform
- production AirEdgeSweep chamfer routing

## Recommendation for next milestone
Progress to convex planar replacement policy lab (EDGE-X3 lane) while keeping concave non-orthogonal proven path bounded and deterministic.

> Update (EDGE-X3): concave acceptance lanes from EDGE-X2.2 are now regression-covered under a JudgmentEngine-backed policy evaluator while convex replacement remains deferred.
