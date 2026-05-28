# EDGE-V1 Convex Planar AirChamfer Prototype

## Purpose and scope
EDGE-V1 packages the proven EDGE-X3 -> EDGE-X6 friction-lab chain into a single **internal, production-adjacent prototype seam** for convex planar single-edge AirChamfer replacement evaluation. It is explicitly non-authoritative and does not mutate production bodies.

## References
- `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/frictionlab/edge-x3-convex-planar-air-chamfer-judgment-policy-lab.md`
- `docs/frictionlab/edge-x4-convex-planar-air-chamfer-topology-plan-lab.md`
- `docs/frictionlab/edge-x5-convex-planar-air-chamfer-geometry-artifact-lab.md`
- `docs/frictionlab/edge-x6-convex-planar-air-chamfer-closed-witness-lab.md`

## Component and internal API shape
Component: `AirChamferConvexPlanarPrototype` (internal friction-lab seam).

Request model: `AirChamferConvexPlanarPrototypeRequest`
- edge endpoints,
- adjacent planar normals,
- chamfer distance,
- convexity classification expectation,
- chain/corner flags,
- legacy dependency flag,
- route preference,
- optional artifact/witness materialization toggles.

Result model: `AirChamferConvexPlanarPrototypeResult`
- status: `Accepted | Rejected | Deferred | FallbackLegacy`,
- decision string,
- Judgment score + normalized considerations,
- optional topology plan,
- optional geometry artifact,
- optional closed witness,
- deterministic diagnostics.

## Supported scope
- convex planar single-edge requests with finite edge and positive finite distance,
- deterministic classification,
- no edge chain / corner chain,
- no legacy-dependent topology.

## Pipeline
1. **Judgment policy** via existing `AirChamferPolicyLab` (JudgmentEngine-backed decisioning).
2. **Topology plan** via `AirChamferTopologyPlanLab`.
3. **Geometry artifact** via `AirChamferGeometryArtifactLab`.
4. **Closed witness** via `AirChamferClosedWitnessLab` (optional in request).

## Diagnostics contract
The prototype emits deterministic `edge-v1-*` markers including:
- `edge-v1-air-chamfer-prototype-started`
- `edge-v1-judgment-engine-used`
- `edge-v1-policy-decision:<decision>`
- `edge-v1-topology-plan-created`
- `edge-v1-geometry-artifact-created`
- `edge-v1-closed-witness-created`
- `edge-v1-closed-witness-step-smoke-succeeded`
- `edge-v1-request-rejected:<reason>`
- `edge-v1-request-deferred:<reason>`
- `edge-v1-legacy-authority-preserved`
- `edge-v1-no-production-route-replacement`
- `edge-v1-no-3d-boolean-used`

## STEP smoke result
For admitted convex planar single-edge cases with witness enabled, the closed witness STEP smoke succeeds and includes:
- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`
and excludes:
- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

## Rejected/deferred/fallback cases
Covered in tests:
- unsafe envelope,
- invalid distance,
- invalid edge,
- invalid/missing adjacency,
- ambiguous classification,
- edge chain,
- corner chain,
- legacy-dependent triangle/chamfer fixture fallback.

## Relationship to BrepBoundedChamfer
`BrepBoundedChamfer` remains production-authoritative. EDGE-V1 does not replace production routing and does not alter public API, STEP exporter/importer behavior, boolean core behavior, or fillet geometry behavior.

## Tests run
- FrictionLab EDGE filters (policy/plan/artifact/witness/chamfer/fillet/edge-finish/CIRLab)
- Firmament chamfer/fillet/corner/primitive/export/materialization filters
- Core judgment/chamfer/fillet/corner/triangular-prism/extrude/boolean/safe-composition filters

## Limitations
- no production body mutation integration,
- no non-planar/cylindrical support,
- no edge/corner chain implementation,
- no variable distance,
- no fillet support,
- no legacy-dependent topology migration,
- no 3D boolean route.

## Next recommended milestone
Recommended next step: **EDGE-V2 production-adjacent real-body integration probe (still non-authoritative)**.

## EDGE-X7 note
EDGE-X7 adds controlled real-body selection/replacement probing around this EDGE-V1 prototype without changing production chamfer routing.


## EDGE-X8 note
EDGE-X8 consumes EDGE-V1 outputs (policy decision, topology plan, geometry artifact, closed witness) in a controlled local topology graft lab. This remains non-authoritative and does not replace production chamfer routing.
