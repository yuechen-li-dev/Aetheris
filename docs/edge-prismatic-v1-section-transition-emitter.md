# EDGE-PRISMATIC-V1 — Internal prismatic section-transition emitter seam

## 1. Purpose and scope

EDGE-PRISMATIC-V1 packages the lab-proven prismatic section-transition emitter behind an internal, production-adjacent seam named `PrismaticSectionTransitionEmitter`. The seam is intentionally bounded: it makes the admissibility, request/result model, diagnostics, topology summary, and optional STEP smoke explicit without routing any production operation through the emitter.

The component lives in `Aetheris.Kernel.Core.Brep.Prismatic` and is `internal`. It is visible to FrictionLab and test assemblies for controlled evidence only. There are no public API changes and no production route replacements.

## 2. References

- EDGE-PRISMATIC-A0: `docs/edge-prismatic-a0-section-transition-contract-audit.md`.
- EDGE-PRISMATIC-X1: `docs/frictionlab/edge-prismatic-x1-section-transition-emitter-lab.md`.
- EDGE-PRISMATIC-X2: `docs/frictionlab/edge-prismatic-x2-top-edge-chamfer-through-prismatic-emitter-lab.md`.
- EDGE-PRISMATIC-X3: `docs/frictionlab/edge-prismatic-x3-generic-line-profile-transition-lab.md`.
- V2 sweep-first architecture: `docs/aetheris-v2-sweep-first-architecture.md`.
- Resolved Profile2D contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`.
- Constructive chamfer reframing audit: `docs/edge-a2-constructive-chamfer-reframing-audit.md`.

## 3. Component and internal API shape

The production-adjacent seam is:

- `PrismaticSectionTransitionEmitter.Emit(PrismaticSectionTransitionRequest request)`.
- `PrismaticSectionTransitionRequest` with sections, explicit correspondence, options, and deterministic diagnostics produced in the result.
- `PrismaticSectionTransitionResult` with status, optional body, topology summary, optional STEP smoke summary, diagnostics, and recommendation.
- `PrismaticSection`, `PrismaticCorrespondenceMap`, `PrismaticSectionTransitionOptions`, `PrismaticTransitionTopologySummary`, and `PrismaticSectionTransitionStepSummary`.

Result statuses are `Succeeded`, `Rejected`, `Deferred`, and `Failed`. Successful first-scope requests return a closed `BrepBody`; rejected/deferred requests return no body.

## 4. Supported first-scope contract

V1 admits only:

- Z-axis stacked sections.
- Two or three sections.
- One line-only outer loop per section.
- No holes.
- No arcs.
- No multiple outer loops.
- Equal vertex/edge count across all sections.
- Explicit identity correspondence by index.
- Stable orientation across sections.
- Planar transition faces only.
- Deterministic orientation and deterministic diagnostics.
- Direct closed BRep emission.
- Optional STEP smoke through the existing `Step242Exporter`.

## 5. Data model

### Sections and profiles

A `PrismaticSection` contains a Z value and an ordered closed outer-loop vertex list in XY. The loop is implicit: the final profile edge connects the last vertex back to the first vertex. First scope profiles are line-only; holes, arcs, and multiple loops are represented as unsupported flags and deterministically deferred.

### Correspondence

`PrismaticCorrespondenceMap.Identity(n)` is the only admitted correspondence in V1. This makes the first vertex in each section correspond to the first vertex in every other section, the second to the second, and so on. Non-identity or missing correspondence is rejected before emission.

### Result and topology summary

The result reports topology counts for vertices, edges, cap faces, transition faces, planar/cylindrical faces, loops, coedges, stable-vs-changed interval faces, and bounds. This makes the seam machine-checkable without inspecting STEP text as the primary source of topology truth.

## 6. Emission model

For admitted requests the emitter:

1. Creates section vertices at each `(x, y, z)` profile point.
2. Creates profile edges within each section.
3. Creates transition edges between corresponding vertices across adjacent sections.
4. Creates one cap face at the first section and one cap face at the last section.
5. Creates one transition face per corresponding profile edge per interval.
6. Binds each edge to a line curve.
7. Binds each cap and transition face to a plane surface.
8. Preserves split faces at section boundaries.
9. Does not merge coplanar faces in V1.
10. Follows EDGE-PRISMATIC-X4 policy: section-boundary faces are semantic interval evidence, and any future coplanar merge must be a separate explicit post-emission mode rather than a silent emitter behavior change.

## 7. Success cases and topology formula

The focused tests cover:

1. Two-section rectangle -> centered inset rectangle.
2. Two-section scaled pentagon.
3. Two-section scaled hexagon.
4. Two-section asymmetric translated pentagon.
5. Three-section stable+transition rectangle.

For a two-section line-only profile with `n` vertices, observed and asserted counts are:

- vertices: `2n`;
- edges: `3n`;
- faces: `n + 2`;
- planar faces: `n + 2`;
- cylindrical faces: `0`;
- transition faces: `n`;
- cap faces: `2`;
- loops: `n + 2` under the current one-loop-per-face convention;
- coedges: `6n`, with `4n` transition coedges and `2n` cap coedges.

Observed V1 values:

| Case | n | Vertices | Edges | Faces | Planar | Cylindrical | Loops | Coedges |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| rectangle -> inset rectangle | 4 | 8 | 12 | 6 | 6 | 0 | 6 | 24 |
| scaled pentagon | 5 | 10 | 15 | 7 | 7 | 0 | 7 | 30 |
| scaled hexagon | 6 | 12 | 18 | 8 | 8 | 0 | 8 | 36 |
| asymmetric translated pentagon | 5 | 10 | 15 | 7 | 7 | 0 | 7 | 30 |
| three-section stable+transition rectangle | 4 | 12 | 20 | 10 | 10 | 0 | 10 | 40 |

The three-section rectangle preserves split interval faces: four stable lower interval faces and four changed upper transition faces.

## 8. STEP smoke findings

When `RunStepSmoke` is enabled the result records marker checks against the existing STEP exporter. Successful V1 cases require these markers to be present:

- `ISO-10303-21`;
- `MANIFOLD_SOLID_BREP`;
- `ADVANCED_FACE`;
- `PLANE`.

Successful V1 cases require these markers to remain absent:

- `CYLINDRICAL_SURFACE`;
- `BREP_WITH_VOIDS`.

STEP smoke is validation only. V1 does not change the STEP exporter or importer.

## 9. Invalid and deferred cases

V1 rejects deterministically for:

- fewer than two sections;
- non-finite Z;
- non-increasing Z, including zero interval span;
- invalid profile;
- self-intersecting profile;
- mismatched vertex count;
- missing correspondence;
- non-identity correspondence;
- reversed/unstable orientation;
- non-planar transition faces.

V1 defers deterministically for:

- more than three sections;
- holes;
- line+arc profiles;
- multiple outer loops.

Diagnostics are stable strings such as `edge-prismatic-v1-request-rejected:<reason>` and `edge-prismatic-v1-request-deferred:<reason>` plus specific machine-checkable diagnostics including `edge-prismatic-v1-invalid-profile-rejected`, `edge-prismatic-v1-mismatched-vertex-count-rejected`, and `edge-prismatic-v1-holes-deferred`.

## 10. No-trim/no-graft/no-forbidden-route guarantee

V1 emits the whole closed BRep directly from section evidence. It does not trim an existing body, graft topology, mutate a body, invoke AirEdgeSweep, invoke `BrepBoundedChamfer`, invoke 3D Boolean, use a sketch solver, use a clipping engine, or introduce NURBS/freeform support. Successful diagnostics include:

- `edge-prismatic-v1-no-air-edge-sweep-used`;
- `edge-prismatic-v1-no-brep-bounded-chamfer-used`;
- `edge-prismatic-v1-no-topology-graft-used`;
- `edge-prismatic-v1-no-3d-boolean-used`;
- `edge-prismatic-v1-no-production-route-replacement`.

## 11. Relationship to `ProfileStackExtrudeExecutor`

`ProfileStackExtrudeExecutor` is unchanged and is not replaced by V1. Current circular-hole interval behavior remains separate. V1 is only an internal production-adjacent seam around equal-count line-only outer section transitions, not a Firmament/ProfileStack production lowering route.

## 12. Relationship to chamfer architecture

Future history-known top/bottom/horizontal chamfers may be evaluated against this prismatic lane because they can be represented as explicit section evolution. Vertical-edge chamfers remain aligned with `ProfileVertexChamferExtrudeEmitter`. No-history/local-edge chamfers remain aligned with AirChamfer/AirEdgeSweep evidence and existing legacy bounded behavior. V1 changes none of those routes.

## 13. Non-goals

V1 does not add production integration, Firmament lowering, top-edge route replacement, arbitrary axes, inferred correspondence, coplanar merge policy, line+arc profiles, holes, square-to-round adapters, AirEdgeSweep/no-history chamfer routing, triangle migration, sketch solving, clipping, or NURBS/freeform support.

## 14. Tests run

Minimum targeted validation for the V1 seam includes:

- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "PrismaticSectionTransition"`.

The milestone gate also runs the requested FrictionLab, Kernel.Core, Kernel.Firmament, CLI, and full shared test commands after packaging.

## 15. Recommended next milestone

Recommended next steps are either:

- EDGE-PRISMATIC-V2 controlled top-edge chamfer route evaluation through this seam; or
- EDGE-PRISMATIC-X4 coplanar split/merge policy audit before any route replacement is considered.
