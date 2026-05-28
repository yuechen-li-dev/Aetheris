# EDGE-X6 — Convex planar AirChamfer closed witness lab

## Purpose and scope
EDGE-X6 is lab-only: it wraps EDGE-X5 open/local convex AirChamfer replacement artifacts into a synthetic **closed** witness BRep so manifold topology and STEP/AP242 smoke can be validated without any production chamfer route changes.

## References
- EDGE-A0 audit: `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- EDGE-X1 capability matrix: `docs/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`
- EDGE-X3 policy lab: `docs/frictionlab/edge-x3-convex-planar-air-chamfer-judgment-policy-lab.md`
- EDGE-X4 topology plan lab: `docs/frictionlab/edge-x4-convex-planar-air-chamfer-topology-plan-lab.md`
- EDGE-X5 geometry artifact lab: `docs/frictionlab/edge-x5-convex-planar-air-chamfer-geometry-artifact-lab.md`

## Why closed witness follows open/local artifact
EDGE-X5 intentionally stopped at local/open replacement evidence. EDGE-X6 adds closure so existing `Step242Exporter` can be smoke-tested on a manifold witness without mutating any production body.

## Pipeline
1. Reuse EDGE-X3 Judgment-backed admissibility.
2. Consume EDGE-X4 topology plan.
3. Consume EDGE-X5 geometry artifact.
4. Construct synthetic closed witness body (planar-only) for accepted convex single-edge cases.
5. Validate topology summary and run STEP smoke.

## Closed witness model
Current witness is a synthetic planar closed host generated for admitted convex single-edge cases. It is a minimal closed planar manifold witness for exporter/topology smoke and not a production replacement body mutation path.

## Diagnostics and guarantees
- Includes deterministic `edge-x6-*` diagnostics for policy use, plan/artifact consumption, witness build, topology/orientation checks, STEP smoke status.
- Explicitly records:
  - `edge-x6-no-production-behavior-changed`
  - `edge-x6-no-3d-boolean-used`

## Fixture outcomes
- Accepted: canonical convex planar single-edge (and safe non-orthogonal case where admitted).
- Rejected/deferred pre-witness: invalid distance/edge/adjacency, unsafe envelope, ambiguous class, edge chain, corner chain, legacy-dependent path.

## STEP smoke findings
Expected markers are validated for witness output:
- present: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`
- absent: `CYLINDRICAL_SURFACE`, `BREP_WITH_VOIDS`

## Non-goals
- no production chamfer/fillet behavior changes
- no production body mutation
- no fillet geometry
- no edge/corner chain implementation
- no STEP exporter changes
- no Boolean core changes

## Recommended next milestone
- EDGE-V1 production-adjacent convex planar AirChamfer prototype, or
- additional closed witness hardening if a topology/robustness blocker appears.
