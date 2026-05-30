# EDGE-PRISMATIC-V2 — Controlled top-edge chamfer route through prismatic section transition

## 1. Purpose and scope

EDGE-PRISMATIC-V2 adds a controlled, internal, production-adjacent route named `PrismaticTopEdgeChamferPrototype` for the history-known rectangular-prism top `+X` horizontal side chamfer. The route proves that the known top-edge chamfer case can be expressed as a Z-axis prismatic section stack and emitted through `PrismaticSectionTransitionEmitter`.

This is not a default production route replacement. It is a bounded candidate path for evaluation and regression evidence only.

## 2. References

- EDGE-PRISMATIC-A0: `docs/edge-prismatic-a0-section-transition-contract-audit.md`.
- EDGE-PRISMATIC-X1: `docs/frictionlab/edge-prismatic-x1-section-transition-emitter-lab.md`.
- EDGE-PRISMATIC-X2: `docs/frictionlab/edge-prismatic-x2-top-edge-chamfer-through-prismatic-emitter-lab.md`.
- EDGE-PRISMATIC-X3: `docs/frictionlab/edge-prismatic-x3-generic-line-profile-transition-lab.md`.
- EDGE-PRISMATIC-V1: `docs/edge-prismatic-v1-section-transition-emitter.md`.
- V2 sweep-first architecture: `docs/aetheris-v2-sweep-first-architecture.md`.
- Resolved Profile2D contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`.
- Constructive chamfer reframing audit: `docs/edge-a2-constructive-chamfer-reframing-audit.md`.

## 3. Component and internal API shape

The route lives in `Aetheris.Kernel.Core.Brep.Prismatic` and is internal:

- `PrismaticTopEdgeChamferPrototype.Emit(PrismaticTopEdgeChamferRequest request)`.
- `PrismaticTopEdgeChamferRequest` accepts width, depth, height, chamfer distance, selected side, and optional STEP smoke export.
- `PrismaticTopEdgeChamferResult` returns status, optional `BrepBody`, topology summary, STEP smoke summary, deterministic diagnostics, and recommendation.
- `PrismaticTopEdgeChamferSelection.TopPositiveXSide` is the only admitted selection.

The route packages the existing V1 `PrismaticSectionTransitionEmitter` seam. It does not duplicate the prismatic emitter implementation.

## 4. Supported controlled case

The only supported case is:

- rectangular prism / box-like history-known body;
- top horizontal `+X` side chamfer;
- Z-axis section stack;
- full rectangle lower/stable sections;
- inset `+X` side top section;
- explicit identity correspondence by index;
- line-only profiles;
- no holes, arcs, chains, corners, fillets, or arbitrary edge selection;
- planar faces only;
- closed BRep candidate body;
- optional STEP smoke through the existing `Step242Exporter`.

Admissibility rejects non-finite or non-positive dimensions, non-finite or non-positive chamfer distance, chamfer distances greater than the conservative `min(width / 2, height)` bound, and any selection other than top `+X` side.

## 5. Section stack and correspondence map

For the canonical case:

- width = `10`;
- depth = `8`;
- height = `6`;
- chamfer distance = `1`;
- `z0 = 0`;
- `z1 = 5`;
- `z2 = 6`.

Section `z0` is the full rectangle:

1. `(-5, -4)`;
2. `( 5, -4)`;
3. `( 5,  4)`;
4. `(-5,  4)`.

Section `z1` repeats the full rectangle. Section `z2` is the top `+X` inset rectangle:

1. `(-5, -4)`;
2. `( 4, -4)`;
3. `( 4,  4)`;
4. `(-5,  4)`.

Correspondence is `PrismaticCorrespondenceMap.Identity(4)`. Edge index `1` is the top `+X` side and is classified as the single chamfer transition face in the upper interval.

## 6. Candidate topology contract

For the canonical controlled case, the expected and tested topology is:

| Metric | Expected |
| --- | ---: |
| vertices | 12 |
| edges | 20 |
| faces | 10 |
| planar faces | 10 |
| cylindrical faces | 0 |
| lower prism side faces | 4 |
| transition faces | 4 |
| chamfer transition faces | 1 |
| loops | 10 |
| coedges | 40 |
| bounds | `[-5,-4,0]..[5,4,6]` |

The route preserves the `z = 5` split between stable lower side faces and upper transition faces. It does not merge coplanar faces in this milestone. EDGE-PRISMATIC-X4 records this as policy for controlled prismatic chamfer output: split faces are semantic section-stack/transition-interval evidence, and any future merged output must be an explicitly selected compatibility mode with recognizer and diagnostics parity.

## 7. STEP smoke findings

When requested, the route asks the prismatic emitter to run STEP smoke through the existing exporter. The required present markers are:

- `ISO-10303-21`;
- `MANIFOLD_SOLID_BREP`;
- `ADVANCED_FACE`;
- `PLANE`.

The required absent markers are:

- `CYLINDRICAL_SURFACE`;
- `BREP_WITH_VOIDS`.

The canonical case and the larger valid `chamferDistance = 2` case both pass this smoke check in `PrismaticTopEdgeChamferPrototypeTests`.

## 8. Invalid and rejected cases

Focused tests cover rejection before emitter invocation for:

- width, depth, or height less than or equal to zero;
- non-finite dimensions;
- chamfer distance less than or equal to zero;
- non-finite chamfer distance;
- chamfer distance at or beyond the conservative bound;
- unsupported selected side.

Rejected requests return no body, no STEP export, `prismatic-top-edge-chamfer-invalid-rejected`, and deterministic diagnostics such as `edge-prismatic-v2-invalid-dimensions-rejected`, `edge-prismatic-v2-invalid-chamfer-distance-rejected`, or `edge-prismatic-v2-unsupported-selection-rejected`.

## 9. No-trim/no-graft/no-AirEdgeSweep/no-BrepBoundedChamfer/no-3D-Boolean guarantee

The candidate path constructs the section stack and invokes `PrismaticSectionTransitionEmitter`. It does not call AirEdgeSweep, BrepBoundedChamfer, topology graft/body mutation, trim/clipping, sketch solving, or 3D Boolean fallback. Successful diagnostics include:

- `edge-prismatic-v2-prismatic-emitter-invoked`;
- `edge-prismatic-v2-no-trim-used`;
- `edge-prismatic-v2-no-air-edge-sweep-used`;
- `edge-prismatic-v2-no-brep-bounded-chamfer-used`;
- `edge-prismatic-v2-no-topology-graft-used`;
- `edge-prismatic-v2-no-3d-boolean-used`;
- `edge-prismatic-v2-no-production-route-replacement`.

## 10. Relationship to `ProfileStackExtrudeExecutor`

`ProfileStackExtrudeExecutor` is unchanged and is not replaced. V2 consumes the already packaged prismatic emitter seam directly for this controlled history-known top-edge candidate.

## 11. Relationship to chamfer architecture

This route is a history-known top-edge chamfer via prismatic transition. It complements the profile-authored vertical-edge lane and the prismatic section-transition lane. AirEdgeSweep remains the no-history/local-edge architecture path and is not used by this candidate.

## EDGE-LOOP-A0 relationship

EDGE-LOOP-A0 identifies this controlled single top-edge prismatic route as a building block for the next Class B loop target: a uniform chamfer around the entire top face outer loop of a history-known rectangular prism. The loop target should generalize the section-stack idea from one changed top edge to an inset top section that changes all four top boundary edges uniformly, while retaining the same exclusions: no AirEdgeSweep, no `BrepBoundedChamfer`, no topology graft, no 3D Boolean, and no production route replacement.

## 12. Non-goals

EDGE-PRISMATIC-V2 does not add:

- default production routing;
- public API changes;
- arbitrary production body mutation;
- arbitrary body or edge selection;
- chains, corners, multiple chamfers, or fillets;
- holes, arcs, inferred correspondence, NURBS, or freeform support;
- STEP exporter/importer changes;
- Boolean core changes;
- AirEdgeSweep behavior changes;
- triangle migration retry;
- sketch solver or clipping engine support.

## 13. Tests run

- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --filter "PrismaticTopEdgeChamferPrototype"`.
- Required regression filters for FrictionLab, Kernel.Core, Kernel.Firmament, and CLI were run for this milestone.

## 14. Recommended next milestone

Because EDGE-PRISMATIC-V1 packaging already exists and V2 successfully consumes it, the next milestone should be one of:

1. EDGE-PRISMATIC-X4 coplanar split/merge policy audit, especially around when stable and transition interval faces may or may not merge;
2. EDGE-PROFILE/PRISMATIC artifact corpus for stable STEP/topology regression evidence if this controlled route remains stable;
3. a narrowly scoped route-hardening milestone that keeps production authority unchanged while adding feature-recognition/parity evidence for this exact history-known top-edge case.

## 11. EDGE-PRISMATIC-X5 corpus evidence note

EDGE-PRISMATIC-X5 includes the controlled top `+X` edge chamfer route in the split-preserving artifact corpus as `edge-prismatic-x5-top-edge-chamfer.step`. The corpus asserts the V2 topology contract of 12 vertices, 20 edges, 10 all-planar faces, 4 lower prism side faces, 4 transition faces, 1 chamfer transition face, 10 loops, and 40 coedges, with STEP smoke markers present and cylindrical/void markers absent. This remains experimental corpus evidence and does not change production chamfer routing.

## EDGE-LOOP-X1 relationship

The controlled single top-edge route remains a Class A proof and is not replaced. EDGE-LOOP-X1 reuses the same prismatic section-transition building block for a Class B face-boundary loop proof: the lower and pre-chamfer sections remain full rectangles, while the top section is inset on all four sides. That route classifies all four upper transition faces as chamfer faces and records loop-selection diagnostics, proving one top-face outer-loop operation rather than four unrelated single-edge operations. No production route, AIR emitter, STEP exporter, Boolean, BRep topology, or CIR analyzer behavior changes are implied by the loop lab.
