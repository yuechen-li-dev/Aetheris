# EDGE-X4 — Convex Planar AirChamfer Replacement Topology-Plan Lab

## Purpose and scope
EDGE-X4 adds a lab-only topology planning stage for convex planar single-edge AirChamfer requests after Judgment-backed policy admission. It deliberately stops before production BRep mutation and before convex geometry emission.

## References
- `docs/development/milestones/general/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/development/milestones/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`
- `docs/development/milestones/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`
- `docs/development/milestones/frictionlab/edge-x2-1-air-chamfer-policy-scaffold-lab.md`
- `docs/development/milestones/frictionlab/edge-x2-2-nonorthogonal-concave-air-chamfer-policy-patch-lab.md`
- `docs/development/milestones/frictionlab/edge-x3-convex-planar-air-chamfer-judgment-policy-lab.md`

## Why topology planning comes before geometry emission
Convex replacement requires coordinated edge reclassification, adjacent-face trimming, chamfer-face insertion, and endpoint transition edges. EDGE-X4 proves that this replacement intent can be modeled deterministically first, so EDGE-X5 can focus on geometry construction against a stable topology contract.

## Judgment-backed policy flow
EDGE-X4 reuses the EDGE-X3 `JudgmentEngine` policy evaluator (`AirChamferPolicyLab`). Convex requests admitted by policy previously deferred on geometry now advance to a plan decision (`plan-convex-replacement-topology`) while still preserving no-geometry-emission guarantees.

## Supported convex scope
- convex planar single edge,
- two adjacent planar faces,
- finite nonzero edge,
- finite positive distance,
- deterministic classification,
- no edge/corner chain,
- no legacy-dependent topology.

## Topology replacement plan model
`AirChamferTopologyPlan` captures:
- input entities (edge endpoints, normals, direction, distance, classification),
- replacement entities (two offset curves, one chamfer face, two transition edges),
- operation intent (trim both adjacent faces, mark original edge for replacement),
- invariant counts for single-edge convex planar replacement mode.

Expected artifact:
- target edges: 1
- offset curves: 2
- chamfer faces: 1
- transition edges: 2
- adjacent faces affected: 2
- corner patches: 0 (deferred)
- replacement mode: `single-edge-convex-planar`

## Fixture cases and outcomes
Accepted/planned:
1. canonical orthogonal convex planar single-edge case -> topology plan created.
2. non-orthogonal convex planar single-edge case -> topology plan created when policy admits.

Rejected/deferred before plan:
- unsafe offset envelope,
- invalid distance,
- invalid edge,
- invalid or missing face adjacency,
- ambiguous classification,
- edge chain,
- corner chain,
- legacy-dependent triangle/legacy route preference.

## Diagnostics contract
EDGE-X4 emits deterministic diagnostics including:
- `edge-x4-topology-plan-lab-started`
- `edge-x4-judgment-engine-used`
- `edge-x4-policy-admitted-convex-plan`
- `edge-x4-policy-rejected-before-plan:<reason>`
- `edge-x4-policy-deferred-before-plan:<reason>`
- `edge-x4-offset-curve-a-planned`
- `edge-x4-offset-curve-b-planned`
- `edge-x4-original-edge-marked-for-replacement`
- `edge-x4-adjacent-face-a-trim-planned`
- `edge-x4-adjacent-face-b-trim-planned`
- `edge-x4-chamfer-face-planned`
- `edge-x4-transition-edges-planned`
- `edge-x4-corner-patches-deferred`
- `edge-x4-topology-plan-created`
- `edge-x4-no-geometry-emission`
- `edge-x4-no-production-behavior-changed`
- `edge-x4-no-3d-boolean-used`

## Guarantees and non-goals
- No production behavior changes.
- No convex geometry emission.
- No fillet geometry.
- No edge-chain/corner-chain implementation.
- No STEP importer/exporter changes.
- No Boolean core changes.
- No triangle migration retry.
- No sketch solver/clipping/NURBS/freeform.

## Recommended next milestone
- EDGE-X5: convex planar chamfer replacement geometry artifact lab.
- If blockers appear, topology-plan hardening and policy refinement before geometry work.


## EDGE-X5 follow-on note
EDGE-X5 consumes this topology plan to build an open/local convex replacement geometry artifact (trimmed adjacent patches + chamfer face + transition/offset edges), while still stopping before production body mutation.
