# EDGE-PRISMATIC-X1 — Section-transition emitter lab

## 1. Purpose and scope

EDGE-PRISMATIC-X1 adds a **lab-only** first-scope `PrismaticSectionTransitionEmitter` proof. It consumes Z-axis stacked, line-only outer profile sections with explicit index correspondence and emits a closed planar BRep by deterministic cap and transition-face construction.

The milestone is intentionally narrow. It does not change production chamfer/fillet behavior, production route selection, the current `ProfileStackExtrudeExecutor`, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, triangle migration, the sketch solver, clipping, or NURBS/freeform support.

## 2. Relationship to EDGE-PRISMATIC-A0

EDGE-PRISMATIC-A0 identified that the EDGE-PROFILE-X2 top-edge chamfer witness needed a first-class prismatic section-transition contract before production-adjacent work. X1 implements the first lab proof of that contract: explicit sections, finite and increasing Z values, stable loop orientation, equal vertex counts, explicit correspondence, deterministic transition intervals, planar face classification, STEP smoke, and machine-checkable diagnostics.

## 3. First-scope emitter definition

The lab code lives under `Aetheris.Firmament.FrictionLab/CIRLab` and introduces:

- `PrismaticSectionTransitionEmitterLab` for deterministic case execution and diagnostics.
- `PrismaticSectionTransitionEmitter` for direct closed BRep emission.
- `PrismaticSectionTransitionCase` for named lab cases.
- `PrismaticSectionTransitionRow` for machine-checkable lab output.
- `PrismaticSection` for one Z-stacked line-only outer loop.
- `PrismaticCorrespondenceMap` for explicit vertex correspondence; X1 admits identity-by-index only.
- `PrismaticTransitionTopologySummary` for topology and face-family counts.

The emitter is not a loft, a trim, a graft, an edge mutation, a 3D Boolean fallback, or an AirEdgeSweep/BrepBoundedChamfer route. It builds topology directly from the supplied sections.

## 4. Data model

### Sections

A section has:

- a finite Z value;
- one closed outer line loop;
- stable vertex order;
- no holes;
- no arcs;
- exactly one outer loop.

X1 supports two or three sections. Section heights must be strictly increasing, and every interval span must be positive.

### Profiles

Profiles are line-only polygons represented as ordered XY vertices. The lab validates finite coordinates, non-zero edges, non-zero signed area, stable orientation across sections, and no self-intersection. X1 rejects invalid profiles and defers holes, arcs, and multiple loops.

### Correspondence

X1 requires an explicit `PrismaticCorrespondenceMap`. The admitted first-scope map is identity-by-index with the same vertex count in every section. Missing correspondence or non-identity/malformed correspondence is rejected or marked as needing correspondence hardening.

### Transition intervals

For every adjacent section pair, the emitter creates one interval. Each interval emits one quad transition face per corresponding profile edge and one transition edge per corresponding vertex. Stable and changed intervals are both preserved as separate topology; X1 does not merge adjacent coplanar faces.

## 5. Test cases and results

### Case 1 — rectangle to inset rectangle

Input:

- `z0 = 0`, rectangle width `10`, depth `8`;
- `z1 = 1`, inset rectangle width `8`, depth `6`;
- identity correspondence across four vertices.

Result:

- closed frustum-like planar BRep;
- 2 cap faces;
- 4 transition faces;
- all faces planar;
- no cylindrical surfaces;
- STEP smoke succeeds.

### Case 2 — three-section stable + transition rectangle

Input:

- `z0 = 0`, full rectangle width `10`, depth `8`;
- `z1 = 5`, full rectangle width `10`, depth `8`;
- `z2 = 6`, inset rectangle width `8`, depth `6`;
- identity correspondence across four vertices.

Result:

- bottom and top cap faces;
- the stable lower interval and the changed upper interval remain split;
- 8 interval transition faces total;
- 4 stable interval faces and 4 changed interval faces in the topology summary;
- all faces planar;
- STEP smoke succeeds.

### Case 3 — scaled pentagon

Input:

- two five-vertex regular polygon sections;
- scaled radius between sections;
- identity correspondence.

Result:

- closed planar BRep;
- deterministic topology counts under the same formula as the rectangle cases;
- STEP smoke succeeds.

## 6. Topology findings

For an admitted case with `S` sections and `N` vertices per section, X1 intentionally emits:

- vertices: `S * N`;
- profile edges: `S * N`;
- transition edges: `(S - 1) * N`;
- total edges: `(S * N) + ((S - 1) * N)`;
- cap faces: `2`;
- transition faces: `(S - 1) * N`;
- total faces/loops: `2 + ((S - 1) * N)`;
- coedges: `(2 * N) + (4 * (S - 1) * N)`.

The two-section rectangle-to-inset-rectangle case has:

- section count `2`;
- vertices `8`;
- bottom profile edges `4`;
- top profile edges `4`;
- transition edges `4`;
- total edges `12`;
- cap faces `2`;
- transition faces `4`;
- total faces `6`;
- planar faces `6`;
- cylindrical faces `0`;
- loops `6`;
- coedges `24`.

The three-section stable + transition rectangle case has:

- section count `3`;
- vertices `12`;
- bottom profile edges `4`;
- top profile edges `4`;
- transition edges `8`;
- total edges `20`;
- cap faces `2`;
- transition faces `8`;
- stable interval faces `4`;
- changed interval faces `4`;
- total faces `10`;
- planar faces `10`;
- cylindrical faces `0`;
- loops `10`;
- coedges `40`.

These counts are asserted in `PrismaticSectionTransitionEmitterLabTests` and document the intended split-face contract.

## 7. STEP smoke findings

The lab exports through the existing `Step242Exporter`. Successful cases require these markers:

- `ISO-10303-21`;
- `MANIFOLD_SOLID_BREP`;
- `ADVANCED_FACE`;
- `PLANE`.

Successful cases also require these markers to be absent:

- `CYLINDRICAL_SURFACE`;
- `BREP_WITH_VOIDS`.

## 8. Invalid and deferred cases

The lab has deterministic diagnostics for:

- non-increasing section heights: `edge-prismatic-x1-non-increasing-sections-rejected`;
- zero/non-positive spans through the same non-increasing check;
- mismatched vertex counts: `edge-prismatic-x1-mismatched-vertex-count-rejected`;
- missing correspondence: `edge-prismatic-x1-missing-correspondence-rejected`;
- invalid/self-intersecting profile: `edge-prismatic-x1-invalid-profile-rejected`;
- holes: `edge-prismatic-x1-holes-deferred`;
- line+arc profiles: `edge-prismatic-x1-line-arc-deferred`;
- multiple outer loops: `edge-prismatic-x1-multiple-loops-deferred`.

The finite recommendation vocabulary is:

- `prismatic-section-transition-ready-for-production-evaluation`;
- `prismatic-section-transition-needs-profile-validation-hardening`;
- `prismatic-section-transition-needs-correspondence-hardening`;
- `prismatic-section-transition-invalid-rejected`;
- `prismatic-section-transition-deferred`.

## 9. No-trim/no-graft/no-legacy-route guarantee

X1 emits direct topology and geometry from section rows. The success diagnostics include:

- `edge-prismatic-x1-no-air-edge-sweep-used`;
- `edge-prismatic-x1-no-brep-bounded-chamfer-used`;
- `edge-prismatic-x1-no-topology-graft-used`;
- `edge-prismatic-x1-no-3d-boolean-used`.

There is no trim engine, topology graft/body mutation, production chamfer/fillet call, `AirEdgeSweep`, `BrepBoundedChamfer`, 3D Boolean, sketch solver, clipping engine, or NURBS/freeform support in this lab.

## 10. Relationship to current ProfileStackExtrudeExecutor

`ProfileStackExtrudeExecutor` remains unchanged. X1 does not replace it, broaden its production behavior, or route production profile-stack cases through the new emitter. The lab exists to prove a generic polygon section-transition foundation that can later inform production-adjacent design without disturbing the current circular-hole profile-stack executor.

## 11. Recommendation for next milestone

X1 is strong enough to support one of two next steps:

1. **EDGE-PRISMATIC-X2**: express the top-edge chamfer case through this generic emitter rather than the one-off EDGE-PROFILE-X2 witness.
2. **EDGE-PRISMATIC-V1**: package the lab proof as a production-adjacent emitter candidate, if additional validation hardening and feature-recognition requirements are explicitly scoped.

The safer sequencing is EDGE-PRISMATIC-X2 first, because it would prove that the generic emitter materially subsumes the existing top-edge chamfer lab before any production-adjacent packaging.
