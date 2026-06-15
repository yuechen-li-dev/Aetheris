# AIR-X3 — BRepPlan for PrismaticSectionTransition

## Purpose and scope

AIR-X3 introduces the first minimal BRepPlan layer for one already-proven Constructive AIR lane:

`AirPrismaticSectionTransition -> BRepPlan -> existing PrismaticSectionTransitionEmitter/BRep`

The milestone proves that Aetheris can represent planned explicit topology before materializing BRep while preserving AIR provenance, topology roles, expected counts, split policy, diagnostics, and test summaries.

## Relationship to AIR-A0, AIR-X1, and AIR-X2

AIR-A0 defines Aetheris as a compiler for BRep: Firmament is source intent, AIR is constructive geometry MIR, BRepPlan is the backend topology emission plan, BRep is the explicit topology backend and STEP/export authority, CIR is an evaluation side-channel, and STEP is serialization.

AIR-X1 added thin AIR wrappers around proven lanes without replacing production routes and without implementing BRepPlan.

AIR-X2 made route selection explicit and admissibility-driven. AIR-X3 does not alter that behavior: `AirPrismaticSectionTransition` still uses direct selection because the AIR node already names its constructive route. JudgmentUtility remains deferred.

## BRepPlan definition

BRepPlan is lower-level than AIR and richer than raw BRep. It carries deterministic planned IDs, element kinds, topology roles, source AIR node IDs, provenance, expected topology counts, bounds, split policy, diagnostics, and route-exclusion guarantees.

The minimal AIR-X3 model is internal/test-visible and prismatic-focused. It defines plan kinds, element kinds, roles, stable IDs, elements, summaries, validation results, and plans.

## BRepPlan is not AIR and not BRep

BRepPlan is not source/authoring intent and is not a replacement for Constructive AIR. It is also not materialized explicit topology and does not own STEP/export authority. The existing `PrismaticSectionTransitionEmitter` remains the BRep materialization path.

## Why prismatic section transition is first

The prismatic section transition lane already has a constrained, proven emitter and canonical artifact corpus. Its topology formula is deterministic for a three-section, four-vertex, split-preserving stack, making it suitable for the first planned-topology proof without rewriting geometry.

## Planner model

The planner uses `AirBRepPlanKind.PrismaticSectionTransition`, element kinds for vertex/curve/edge/coedge/loop/surface/face/shell/body, and roles including section vertices/edges, vertical transition edges, cap faces, side faces, transition faces, body shell, and body.

Stable IDs are deterministic strings such as `v:s0:0`, `e:section:0:1`, `e:transition:1:2`, `f:cap:bottom`, `f:transition:interval1:edge2`, `shell:body:0`, and `body:0`.

The summary records counts, bounds, split policy (`preserve-section-splits`), diagnostics, and guarantees. The validation result reports success/failure, diagnostics, errors, warnings, expected summary, and optional actual emitted topology summary.

## Canonical stack and expected planned topology

The canonical stack uses width `10`, depth `8`, height `6`, inset `1`, sections at `z=0`, `z=5`, and `z=6`, with a full rectangle on the first two sections and an inset rectangle on the top section.

Expected planned topology:

- vertices: `12`
- section edges: `12`
- transition edges: `8`
- total edges: `20`
- curves: `20`
- faces: `10`
- cap faces: `2`
- lower side interval faces: `4`
- upper transition interval faces: `4`
- chamfer faces: `0` for pure prismatic transition
- loops: `10`
- coedges: `40`
- planar surfaces: `10`
- cylindrical faces: `0`
- shell: `1`
- body: `1`
- bounds: `[-5,-4,0]..[5,4,6]`
- split policy: `preserve-section-splits`

## Comparison against existing emitter

AIR-X3 compares the BRepPlan summary against the existing emitter result for the canonical case. Counts and bounds match the emitted BRep topology summary, and STEP smoke remains produced by the existing emitter path, not by BRepPlan.

## Validation, rejection, and deferred cases

The planner validates three line-only sections, equal vertex counts, at least three vertices per section, identity correspondence, increasing Z coordinates, finite coordinates, no holes, no arcs, one outer loop, and split-preserving topology.

Deterministic diagnostics cover rejected/deferred cases including invalid section count, mismatched vertex count, non-increasing sections, non-finite coordinate, holes, arcs, multiple loops, and non-identity correspondence.

## Route-exclusion guarantees

AIR-X3 records guarantees that it performs no production route replacement, no emitter rewrite, no STEP exporter change, no BRep topology behavior change, no coplanar merge, no AirEdgeSweep, no Boolean, and no topology graft.

## Non-goals

AIR-X3 does not replace production routes, rewrite emitters, change STEP exporter/importer behavior, change BRep topology behavior, change route selection or JudgmentUtility, change CIR mirror behavior, change Firmament lowering, implement new geometry, change Boolean/chamfer/fillet/shell behavior, add arbitrary graph support, add import/recovery, retry triangle migration, or expand NURBS/freeform support.

## Tests run

Focused AIR-X3 tests validate canonical plan counts and diagnostics, deterministic IDs, comparison against the existing emitter summary, and invalid/deferred diagnostics. The broader required gate list was also run for this implementation.

## Recommended next milestone

Recommended next milestone: **AIR-X4 — BRepPlan roles/provenance for top-face loop chamfer**. AIR-X3 showed that the pure prismatic transition can preserve structural roles and match emitted topology without production changes; the next useful increment is preserving Class B `FaceBoundaryLoop` / `UniformChamfer` provenance and marking the four upper transition faces as `ChamferFace` in a similarly non-production plan.


## AIR-X4 extension note

AIR-X4 extends this prismatic BRepPlan through a feature role overlay rather than duplicating the planner. The top-face loop chamfer wrapper preserves primary prismatic transition roles and adds `ChamferFace` semantic roles to upper transition faces only when the feature context is the Class B top-face loop chamfer.
