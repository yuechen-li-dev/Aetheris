# EDGE-PROFILE-V1 — Profile-authored chamfer emitter

## 1. Purpose and scope

EDGE-PROFILE-V1 packages the EDGE-PROFILE-X1 vertical-edge chamfer proof into an internal, production-adjacent emitter named `ProfileVertexChamferExtrudeEmitter`.

The supported motivating case is a history-known rectangular prism profile where one convex profile corner is replaced by a bevel segment before extrusion. The emitter constructs the final chamfered profile first and then emits the prism directly. It does not production-route chamfers and does not replace any production primitive, chamfer, or fillet route.

## 2. References

- `docs/aetheris-v2-sweep-first-architecture.md` defines the sweep/profile-first V2 doctrine.
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md` defines the resolved profile contract used as the profile-validation backdrop.
- `docs/edge-a2-constructive-chamfer-reframing-audit.md` reclassifies history-known vertical extrusion-edge chamfers as profile modifications.
- `docs/frictionlab/edge-profile-x1-vertical-edge-chamfer-profile-extrude-lab.md` proves the rectangle-corner-to-pentagon extrusion path.

## 3. Component and internal API shape

Implementation:

- `Aetheris.Kernel.Firmament/Materializer/ProfileVertexChamferExtrudeEmitter.cs`

Internal request shape:

- line-only outer `ProfileVertices`;
- `SelectedVertexIndex`;
- `ChamferDistance`;
- `ExtrusionHeight`;
- optional rectangle metadata from `ProfileVertexChamferExtrudeRequest.Rectangle(...)`;
- `RunStepSmoke` for deterministic STEP marker smoke.

Internal result shape:

- `Status`: `Succeeded`, `Rejected`, `Deferred`, or `Failed`;
- `ChamferedProfile`;
- emitted `BrepBody` when successful;
- `ProfileVertexChamferTopologySummary`;
- `ProfileVertexChamferStepSummary`;
- deterministic diagnostics;
- recommendation.

## 4. Supported scope

Required support is implemented:

- centered rectangle profile;
- one selected convex corner, currently the `+X,+Y` rectangle corner via the rectangle factory;
- finite positive chamfer distance;
- finite positive Z extrusion height;
- direct one-corner vertical-edge chamfered rectangular prism emission.

Carefully bounded generic support is also present as evaluation-only support:

- simple line-only convex polygon outer profile;
- one selected convex vertex;
- chamfer distance shorter than both adjacent edges;
- linear extrusion along Z.

Generic polygon support is not a production migration path and is not triangle/hex route replacement.

## 5. Admissibility rules

The emitter rejects when:

- dimensions or profile coordinates are non-finite;
- extrusion height is not greater than zero;
- chamfer distance is not greater than zero;
- selected vertex index is missing;
- the selected vertex is not convex under the input profile orientation;
- adjacent selected-vertex edges are zero-length or too short;
- rectangle chamfer distance is at or beyond `min(width, depth) / 2`;
- resulting profile has fewer than three vertices;
- resulting profile has zero-length edges;
- resulting profile self-intersects.

The milestone remains line-only. Holes, slots, arcs, NURBS, freeform curves, profile stacks, sketch solving, clipping, and 3D Booleans are out of scope.

## 6. Candidate construction

The construction sequence is deliberately narrow:

1. validate request and selected vertex;
2. move from the selected profile vertex toward each adjacent vertex by the chamfer distance;
3. replace the original selected vertex with the two offset points;
4. validate the resulting line-only profile;
5. call `BrepExtrude.Create` on the final chamfered profile.

The sharp body is never emitted first.

## 7. Topology findings

The required rectangle cases produce the expected one-corner vertical-edge chamfered rectangular prism topology:

- profile vertices: `5`;
- body vertices: `10`;
- edges: `15`;
- faces: `7`;
- cap faces: `2`;
- side faces: `5`;
- chamfer/bevel side faces: `1`;
- planar faces: `7`;
- cylindrical faces: `0`.

The generic convex pentagon evaluation case produces a six-vertex chamfered profile and the expected prism pattern of `2n` vertices, `3n` edges, and `n + 2` faces for `n = 6`.

## 8. STEP smoke findings

The emitter can optionally run STEP smoke using `Step242Exporter.ExportBody`. The supported cases require these markers to be present:

- `ISO-10303-21`;
- `MANIFOLD_SOLID_BREP`;
- `ADVANCED_FACE`;
- `PLANE`.

The supported profile-authored chamfer cases require these markers to be absent:

- `CYLINDRICAL_SURFACE`;
- `BREP_WITH_VOIDS`.

## 9. Invalid and deferred cases

Deterministic rejection diagnostics include:

- `edge-profile-v1-invalid-dimensions-rejected`;
- `edge-profile-v1-invalid-chamfer-distance-rejected`;
- `edge-profile-v1-chamfer-distance-too-large-rejected`;
- `edge-profile-v1-selected-vertex-not-convex-rejected`;
- `edge-profile-v1-adjacent-edge-too-short-rejected`;
- `edge-profile-v1-profile-self-intersection-rejected`;
- `edge-profile-v1-request-rejected:<reason>`.

No deferred generic-polygon diagnostic is emitted because bounded convex polygon evaluation support is implemented. Production migration for generic prisms remains deferred by scope rather than by this emitter's local geometry capability.

## 10. No-trim/no-graft/no-legacy guarantee

The candidate path records these positive diagnostics on success:

- `edge-profile-v1-no-air-edge-sweep-used`;
- `edge-profile-v1-no-brep-bounded-chamfer-used`;
- `edge-profile-v1-no-topology-graft-used`;
- `edge-profile-v1-no-3d-boolean-used`.

The implementation calls `BrepExtrude.Create` only after the chamfered profile is built. It does not trim, graft, mutate an existing body, invoke AirEdgeSweep, invoke `BrepBoundedChamfer`, or use 3D Boolean fallback.

## 11. Relationship to AirEdgeSweep

AirEdgeSweep remains the no-history/local-edge lane. EDGE-PROFILE-V1 is the construction-history/profile lane for the specific case where the vertical prism edge is known to originate from a profile vertex. The two lanes should not be conflated: AirEdgeSweep can continue to explore local edge replacement, while this emitter proves the sharp vertical edge can be omitted entirely when profile history is available.

## 12. Non-goals

EDGE-PROFILE-V1 does not change:

- production chamfer or fillet behavior;
- production route selection;
- primitive route selection;
- STEP exporter/importer behavior;
- Boolean core behavior;
- AirEdgeSweep behavior;
- triangle migration;
- sketch solving;
- clipping;
- NURBS/freeform support;
- top/horizontal edge chamfers;
- corner-chain or multi-edge chamfers.

## 13. Tests run

Focused tests were added in `Aetheris.Kernel.Firmament.Tests/Integration/ProfileVertexChamferExtrudeEmitterTests.cs` for:

- canonical rectangle: width `10`, depth `8`, height `6`, chamfer `1`;
- larger valid rectangle chamfer: width `10`, depth `8`, height `6`, chamfer `2`;
- non-square rectangle: width `12`, depth `5`, height `7`, chamfer `1`;
- invalid zero/negative chamfer distance;
- invalid/too-large rectangle dimensions and chamfer distance;
- generic convex pentagon evaluation-only support;
- generic concave selected vertex rejection;
- generic adjacent-edge-too-short rejection.

The milestone validation commands are recorded in the PR summary/final response.

## 14. Recommended next milestone

Two next steps are reasonable:

1. **EDGE-PROFILE-X2**: profile-stack top-edge chamfer lab for horizontal/top extrusion edges.
2. **EDGE-PROFILE-V2**: controlled Firmament/profile route evaluation if this internal emitter remains stable under broader route-readiness tests.
