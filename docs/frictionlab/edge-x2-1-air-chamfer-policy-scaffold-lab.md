# EDGE-X2.1: AirChamfer policy scaffold lab

## Purpose and scope
EDGE-X2.1 is a **lab-only** policy scaffold that separates admissibility/route judgment from chamfer geometry construction. It adds deterministic fixture-driven scoring and decision output for bounded chamfer requests, while preserving current production chamfer/fillet behavior.

## References
- `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`
- `docs/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`
- `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`

## Why policy before more geometry
AirEdgeSweep completion risk is mostly in admissibility and route policy (valid edge, supported adjacent faces, concave/convex handling, safe offset, corner/chain policy, and legacy topology dependencies), not in patch construction itself. EDGE-X2 proved canonical concave planar patch construction; EDGE-X2.1 now formalizes policy gates first.

## Request/case model
`AirChamferPolicyRequest` captures edge endpoints, adjacent normals, distance, face family, chain/corner flags, legacy dependency, route preference, expected classification, and orthogonality indicator.

## Scoring model
Deterministic utility-like score fields:
- `GeometrySupportScore`
- `TopologyRiskScore`
- `OffsetStabilityScore`
- `CornerPolicyScore`
- `LegacyDependencyScore`
- `OverallUtility`

Scores are bounded, inspectable, and emitted as machine-checkable diagnostics.

## Decision vocabulary
Finite decisions:
- `accept-air-chamfer-patch`
- `fallback-legacy-chamfer`
- `defer-nonorthogonal-policy`
- `defer-convex-replacement-policy`
- `defer-edge-chain-policy`
- `defer-corner-policy`
- `defer-unsupported-face-family`
- `defer-legacy-dependent-topology`
- `reject-invalid-distance`
- `reject-invalid-edge`
- `reject-invalid-face-adjacency`
- `reject-ambiguous-classification`

## Fixture cases and results
Included fixtures cover accepted canonical concave planar, deferred non-orthogonal/convex/edge-chain/corner-chain/unsupported-face-family, legacy-dependent fallback, and invalid request rejection cases.

## Accepted/deferred/rejected examples
- Accepted: canonical orthogonal concave planar single edge (`accept-air-chamfer-patch`) and optional EDGE-X2 patch call.
- Deferred: non-orthogonal planar, convex replacement, edge chain, corner chain, unsupported face family.
- Rejected: invalid distance, edge, face adjacency, ambiguous classification.

## Relationship to JudgmentEngine
This lab is policy-shaped scaffolding intended for future production integration with JudgmentEngine-style admissibility + scoring + deterministic tie-break behavior. Geometry emitter(s) are intentionally behind the policy decision boundary.

## Non-goals
- No production behavior changes.
- No new geometry expansion beyond EDGE-X2 canonical patch.
- No convex replacement geometry.
- No fillet geometry changes.
- No chain/corner implementation.
- No STEP import/export changes.
- No Boolean core changes.

## Recommended next milestone
Based on policy results:
- EDGE-X2.2 non-orthogonal concave policy+geometry lab, **or**
- EDGE-X3 convex planar replacement policy lab.

> Update (EDGE-X2.2): safe non-orthogonal concave planar single-edge fixtures are now admitted in lab policy when angle/offset admissibility checks pass; shallow/near-parallel cases remain deferred/rejected with explicit diagnostics.
