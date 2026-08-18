# V2-V5 — Triangle prism production migration via line/arc profile extrusion

## Purpose and scope
V2-V5 evaluated production migration of the triangle prism route to `LineArcProfileExtrudeEmitter` under strict parity constraints.

## References
- V2 doctrine: `docs/development/milestones/general/aetheris-v2-sweep-first-architecture.md`
- V2-A1 resolved profile contract: `docs/development/milestones/general/aetheris-v2-a1-resolved-profile2d-contract.md`
- V2-A2 migration audit: `docs/development/milestones/general/v2-a2-prismatic-operation-migration-audit.md`
- V2-X8 parity lab: `docs/development/milestones/frictionlab/v2-x8-triangle-hex-prism-profile-parity-lab.md`

## Outcome
**Acceptable Outcome B (honest stop): migration attempt was reverted.**

## What was attempted
- A Firmament execution seam migration was prototyped for `triangular_prism` to emit a one-loop, line-only profile through `LineArcProfileExtrudeEmitter` using the required coordinates:
  - `(-w/2, -d/2)`
  - `( +w/2, -d/2)`
  - `(0, +d/2)`
- Centered Z semantics were preserved in the attempted route.
- No 3D boolean operation was introduced in the attempted triangle route.

## Blocker classification
1. **Topology parity regression** and
6. **LineArcProfileExtrudeEmitter integration bug (for this production seam)**.

Observed effect:
- Non-orthogonal triangular prism chamfer scenarios regressed (`No bounded corner-resolution candidate was admissible`) in existing Firmament gates, indicating corner/adjacency semantics used by downstream bounded chamfer recognition changed relative to the legacy triangle primitive path.

## Decision
- To preserve production behavior and all hard constraints, the triangle production route was reverted to the existing `BrepPrimitives.CreateTriangularPrism(...)` path.
- No weakening of topology/STEP assertions was applied.
- No STEP exporter or boolean core changes were made.

## Validation/admissibility boundary
- Existing deterministic invalid-dimension behavior remains unchanged in production.

## Tests run
- Focused Core/Firmament/FrictionLab filtered gates were run.
- Core and FrictionLab focused suites passed.
- Firmament focused suite exposed the blocker and remains the evidence source for this stop.

## Non-goals (unchanged)
- No hex migration in V2-V5.
- No slot/capsule migration.
- No generic polygon prism migration.
- No STEP exporter/importer changes.
- No Boolean core changes.
- No sketch solver, clipping engine, or NURBS/freeform additions.

## Next recommended migration
- Resolve triangle chamfer adjacency/parity at the emitter-integration seam first.
- Keep V2-X8 hex parity continuity; then proceed to likely V2-V6 hex migration only after triangle seam parity strategy is proven safe.

## Update note (V2-X8.1)

A dedicated follow-up lab now probes triangle chamfer adjacency/corner feature-recognition parity between legacy and line-arc candidate paths: `docs/development/milestones/frictionlab/v2-x8-1-triangle-chamfer-adjacency-parity-lab.md`.



## Update note (V2-X8.2)
The V2-X8.2 forensic ledger audit identifies deterministic legacy-vs-candidate adjacency deltas and reinforces the V2-V5 honest stop: keep triangle production on legacy routing until seam parity is restored.

## Update note (V2-A3)
V2-A3 formalizes this result as doctrine: triangle remains on legacy production routing because topology contract parity for bounded feature recognition is not yet proven. The line-arc triangle route remains valid as a parallel V2 lane for scoped profile-first contexts, not as a silent replacement.

Reference: `docs/development/milestones/general/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`.


Update note (EDGE-A0): triangle migration remains blocked pending adjacency-contract hardening and future constructive edge-surfacing (`AirEdgeSweep`) contract proof.

