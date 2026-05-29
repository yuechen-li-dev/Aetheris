# EDGE-PRISMATIC-X3 — Generic line-only profile transition lab

## 1. Purpose and scope

EDGE-PRISMATIC-X3 broadens the lab evidence for `PrismaticSectionTransitionEmitter` beyond the top-edge chamfer witness. The lab remains intentionally bounded: it covers Z-axis, line-only, one-outer-loop polygon section transitions with equal vertex counts and explicit identity-by-index correspondence.

This is lab-only evidence. It does not production-route the emitter and does not change production chamfer/fillet behavior, public APIs, the STEP exporter/importer, Boolean core, AirEdgeSweep, `BrepBoundedChamfer`, current `ProfileStackExtrudeExecutor` behavior, triangle migration, sketch solving, clipping, or NURBS/freeform support.

## 2. References

- `docs/edge-prismatic-a0-section-transition-contract-audit.md` defines the prismatic section-transition contract and first-scope exclusions.
- `docs/frictionlab/edge-prismatic-x1-section-transition-emitter-lab.md` introduces the lab-only emitter and the rectangle/three-section first proof.
- `docs/frictionlab/edge-prismatic-x2-top-edge-chamfer-through-prismatic-emitter-lab.md` routes the top `+X` chamfer witness through that emitter.

## 3. Generic proof claim

X3 proves that the emitter is generic for bounded equal-count line-only prismatic section transitions, not merely a top-edge chamfer path. The successful rows all use the same direct emitter route:

1. create a finite section stack;
2. require one line-only outer loop per section;
3. require equal vertex counts;
4. require explicit identity correspondence;
5. verify every transition quad is planar;
6. emit a closed BRep directly;
7. export through the existing `Step242Exporter`;
8. assert topology and STEP smoke markers.

The lab records `edge-prismatic-x3-prismatic-emitter-invoked` for successful rows and records no-use diagnostics for AirEdgeSweep, `BrepBoundedChamfer`, topology grafting, and 3D Boolean routes.

## 4. Cases tested

### Case A — rectangle to centered inset rectangle

- `z0 = 0`, bottom rectangle width `10`, depth `8`.
- `z1 = 1`, top rectangle width `8`, depth `6`.
- Vertex count: `n = 4`.
- Result: `8` vertices, `12` edges, `6` faces, `6` planar faces, `0` cylindrical faces, `2` cap faces, `4` transition faces, `24` coedges.

### Case B — pentagon to scaled pentagon

- `z0 = 0`, deterministic regular pentagon radius `5`.
- `z1 = 2`, same-center pentagon radius `4`.
- Vertex count: `n = 5`.
- Result: `10` vertices, `15` edges, `7` faces, `7` planar faces, `0` cylindrical faces, `2` cap faces, `5` transition faces, `30` coedges.

### Case C — hexagon to scaled hexagon

- `z0 = 0`, deterministic regular hexagon radius `6`.
- `z1 = 2`, same-center hexagon radius `4.5`.
- Vertex count: `n = 6`.
- Result: `12` vertices, `18` edges, `8` faces, `8` planar faces, `0` cylindrical faces, `2` cap faces, `6` transition faces, `36` coedges.

### Case D — asymmetric equal-count polygon

- `z0 = 0`, deterministic five-vertex asymmetric polygon.
- `z1 = 2`, the same polygon translated in XY by `(0.75, -0.35)`.
- Vertex count: `n = 5`.
- Result: `10` vertices, `15` edges, `7` faces, `7` planar faces, `0` cylindrical faces, `2` cap faces, `5` transition faces, `30` coedges.

Case D is intentionally not a regular or uniform-scale shape. It demonstrates that explicit correspondence, not symmetry, drives emission. The translation keeps corresponding edges parallel, so each transition quad remains planar under the X3 first-scope rules.

### Three-section retained evidence

The X1 three-section stable-lower-interval plus changed-upper-profile row remains in `RunAll()` and continues to validate split interval topology. X3's generic formula assertions focus on two-section cases; the three-section row preserves the broader section-stack evidence without changing production routing.

## 5. Topology formula

For every successful two-section line-only transition with `n` vertices:

- vertices: `2n`;
- edges: `3n`;
  - `n` bottom profile edges;
  - `n` top profile edges;
  - `n` transition edges;
- faces: `n + 2`;
  - `2` cap faces;
  - `n` transition faces;
- planar faces: `n + 2`;
- cylindrical faces: `0`;
- loops: `n + 2`, because the lab emits one loop per face;
- coedges: `6n`, because the lab emits `4n` coedges for the quad transition faces and `2n` coedges for the two polygon caps.

The test suite asserts this formula for rectangle, pentagon, hexagon, and asymmetric polygon rows and records `edge-prismatic-x3-topology-formula-validated` for successful rows.

For `S` sections, the current emitter's documented convention remains:

- vertices: `S * n`;
- profile edges: `S * n`;
- transition edges: `(S - 1) * n`;
- total edges: `(S * n) + ((S - 1) * n)`;
- faces/loops: `2 + ((S - 1) * n)`;
- coedges: `(2 * n) + (4 * (S - 1) * n)`.

## 6. STEP smoke findings

Successful X3 rows export through the existing `Step242Exporter` without exporter changes.

Required markers present:

- `ISO-10303-21`;
- `MANIFOLD_SOLID_BREP`;
- `ADVANCED_FACE`;
- `PLANE`.

Required markers absent:

- `CYLINDRICAL_SURFACE`;
- `BREP_WITH_VOIDS`.

The lab records `edge-prismatic-x3-step-smoke-succeeded` for successful rows.

## 7. Invalid and deferred cases

Invalid/deferred rows are classified before emitter invocation. The machine-checkable X3 diagnostics are:

- mismatched vertex counts: `edge-prismatic-x3-mismatched-vertex-count-rejected`;
- missing correspondence: `edge-prismatic-x3-missing-correspondence-rejected`;
- invalid/self-intersecting profile: `edge-prismatic-x3-invalid-profile-rejected`;
- non-increasing or zero interval span: `edge-prismatic-x3-non-increasing-sections-rejected`;
- holes: `edge-prismatic-x3-holes-deferred`;
- line+arc profiles: `edge-prismatic-x3-line-arc-deferred`;
- multiple outer loops: `edge-prismatic-x3-multiple-loops-deferred`.

The finite recommendation vocabulary now includes the generic success recommendation:

- `prismatic-section-transition-generic-ready-for-production-evaluation`;
- `prismatic-section-transition-ready-for-production-evaluation` for the older X1 vocabulary entry;
- `prismatic-section-transition-needs-correspondence-hardening`;
- `prismatic-section-transition-needs-profile-validation-hardening`;
- `prismatic-section-transition-invalid-rejected`;
- `prismatic-section-transition-deferred`.

## 8. No-trim/no-graft/no-legacy-route guarantee

Successful rows include deterministic diagnostics confirming:

- `edge-prismatic-x3-no-air-edge-sweep-used`;
- `edge-prismatic-x3-no-brep-bounded-chamfer-used`;
- `edge-prismatic-x3-no-topology-graft-used`;
- `edge-prismatic-x3-no-3d-boolean-used`.

The lab does not use trim/graft/body mutation, AirEdgeSweep, `BrepBoundedChamfer`, 3D Boolean, a sketch solver, a clipping engine, arcs, holes, inferred correspondence, or NURBS/freeform support.

## 9. Relationship to current ProfileStackExtrudeExecutor

`ProfileStackExtrudeExecutor` remains unchanged and is not replaced. X3 is evidence for a lab-only section-transition emitter and does not route production profile-stack, chamfer, or fillet behavior through it.

## 10. Recommendation for next milestone

X3 supports `prismatic-section-transition-generic-ready-for-production-evaluation` for the bounded line-only equal-count cases. Recommended next steps are:

1. **EDGE-PRISMATIC-V1 production-adjacent emitter packaging** if production-adjacent gating, validation hardening, ownership, and route admission are explicitly scoped without changing default behavior.
2. **EDGE-PRISMATIC-X4 coplanar split/merge policy audit** if the next uncertainty is whether stable and changed coplanar interval faces should remain split or be merged for production-adjacent output.

Do not broaden to arcs, holes, different vertex counts, inferred correspondence, clipping, sketch solving, or NURBS/freeform support as part of this X3 result.

## V1 packaging status

EDGE-PRISMATIC-V1 packages the generic X3 evidence into the internal production-adjacent `PrismaticSectionTransitionEmitter` seam. The rectangle, scaled pentagon, scaled hexagon, and asymmetric translated pentagon cases remain regression evidence for the equal-count line-only first-scope contract.
