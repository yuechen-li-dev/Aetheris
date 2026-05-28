# EDGE-X5 — Convex planar AirChamfer replacement geometry artifact lab

## Purpose and scope
EDGE-X5 consumes the EDGE-X4 topology replacement plan and creates a **lab-only, open/local geometry witness** for a convex planar single-edge AirChamfer replacement. No production BRep mutation is performed.

## References
- `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`
- `docs/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`
- `docs/frictionlab/edge-x3-convex-planar-air-chamfer-judgment-policy-lab.md`
- `docs/frictionlab/edge-x4-convex-planar-air-chamfer-topology-plan-lab.md`

## Why geometry artifact comes after topology plan
EDGE-X4 proved deterministic replacement topology planning (counts and replacement mode) without geometry emission. EDGE-X5 uses that deterministic plan as the input contract, then materializes geometry witness pieces while preserving policy-first gating.

## Judgment-backed policy flow
1. Evaluate admissibility with EDGE-X3 judgment-backed policy.
2. If convex-planar admission reaches `defer-convex-replacement-geometry`, consume EDGE-X4 to build `plan-convex-replacement-topology`.
3. Build geometry artifact only from admitted plan.

## Topology plan consumed
`single-edge-convex-planar` replacement mode, with:
- target edge: 1
- offset curves: 2
- chamfer faces: 1
- transition edges: 2
- affected adjacent faces: 2
- corner patches: 0/deferred

## Geometry artifact model
`AirChamferGeometryArtifactLab` builds:
- trimmed adjacent face patch A (planar)
- trimmed adjacent face patch B (planar)
- chamfer face (planar)
- offset curve A (line segment)
- offset curve B (line segment)
- two transition edges (line segments)
- original edge replacement marker
- corner patches deferred marker

## Fixture cases and results
Accepted/artifact path:
- canonical orthogonal convex planar single-edge
- safe non-orthogonal convex planar single-edge (if admitted by EDGE-X4)

Rejected/deferred before artifact:
- unsafe envelope
- invalid distance
- invalid edge
- invalid/missing/parallel adjacency
- ambiguous classification
- edge chain
- corner chain
- legacy-dependent topology fixture

## Topology/artifact counts
For produced artifact:
- faces: 3
- planar faces: 3
- new chamfer faces: 1
- affected adjacent faces: 2
- offset curves: 2
- transition edges: 2
- corner patches: 0 (deferred)
- original edge removed/replaced: true (marker)

## Orientation and area validation
Lab validates:
- finite normals for all faces
- positive area for trimmed patches and chamfer face
- offset curves parallel to original edge direction
- transition edges connect matching offset endpoints
- deterministic orientation diagnostics emitted

## STEP/export status
Artifact is open/local witness, so STEP smoke is explicitly deferred with deterministic diagnostic:
- `edge-x5-step-smoke-deferred:open-local-artifact-export-unsupported`

## No-3D-Boolean guarantee
EDGE-X5 emits `edge-x5-no-3d-boolean-used` and does not route through Boolean geometry fallback.

## Invalid/deferred cases
Invalid/deferred/legacy cases stop before artifact construction and produce recommendation-aligned outcomes.

## Non-goals
- no production chamfer/fillet behavior changes
- no full body mutation
- no fillet geometry
- no edge/corner chain implementation
- no STEP exporter/importer changes
- no Boolean core changes

## Recommended next milestone
Proceed to EDGE-X6 closed local witness lab. Production-adjacent convex AirChamfer prototype (EDGE-V1) should wait until closed witness evidence is sufficient.


> EDGE-X6 note: open/local EDGE-X5 artifacts are now wrapped into a synthetic closed witness lab for manifold topology + STEP smoke validation.
