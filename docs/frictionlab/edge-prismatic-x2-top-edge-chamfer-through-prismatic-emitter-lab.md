# EDGE-PRISMATIC-X2 — Top-edge chamfer through the prismatic emitter lab

## 1. Purpose and scope

EDGE-PRISMATIC-X2 is a FrictionLab-only milestone that re-expresses the EDGE-PROFILE-X2 top `+X` horizontal edge chamfer witness through the EDGE-PRISMATIC-X1 `PrismaticSectionTransitionEmitter`.

The purpose is not to add a production chamfer route. The purpose is to prove that the top-edge chamfer witness is now a client of the prismatic section-transition lane rather than a new one-off BRep construction.

This milestone does **not** change production chamfer/fillet behavior, production route replacement, current `ProfileStackExtrudeExecutor` behavior, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, triangle migration, sketch solving, clipping, or NURBS/freeform support.

## 2. References

- EDGE-PROFILE-X2: `docs/frictionlab/edge-profile-x2-top-edge-chamfer-profile-stack-lab.md`
- EDGE-PRISMATIC-X1: `docs/frictionlab/edge-prismatic-x1-section-transition-emitter-lab.md`
- Prismatic contract audit: `docs/edge-prismatic-a0-section-transition-contract-audit.md`
- Sweep-first architecture doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- Resolved Profile2D contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- Constructive chamfer reframing audit: `docs/edge-a2-constructive-chamfer-reframing-audit.md`

## 3. X2 is not a new one-off witness

EDGE-PROFILE-X2 had two important outcomes:

1. Route A, the current profile-stack executor route, was blocked because the existing executor remains specialized around rectangular host extents and circular-hole/interval behavior rather than arbitrary polygon section profiles and ruled polygon-to-polygon transitions.
2. Route B proved the geometry with a lab-only one-off section-transition witness.

EDGE-PRISMATIC-X1 then introduced the reusable lab-only `PrismaticSectionTransitionEmitter` for line-only, equal-vertex-count, explicit-correspondence, Z-stacked sections.

EDGE-PRISMATIC-X2 uses that X1 emitter for the top-edge chamfer. It keeps the same canonical geometry and topology contract, but the candidate body comes from `PrismaticSectionTransitionEmitter.TryEmit(...)` rather than bespoke top-edge chamfer topology code.

## 4. Candidate geometry

Canonical case: `canonical-top-pos-x-edge`

- width = `10`
- depth = `8`
- height = `6`
- chamferDistance = `1`
- z0 = `0`
- z1 = `height - chamferDistance = 5`
- z2 = `height = 6`

Optional stable case: `larger-top-pos-x-edge`

- width = `10`
- depth = `8`
- height = `6`
- chamferDistance = `2`

## 5. Section stack and correspondence map

The canonical section stack is:

| Section | Z | Outer loop |
| --- | ---: | --- |
| lower | `0` | `(-5,-4)`, `(5,-4)`, `(5,4)`, `(-5,4)` |
| stable upper/lower transition boundary | `5` | `(-5,-4)`, `(5,-4)`, `(5,4)`, `(-5,4)` |
| top inset | `6` | `(-5,-4)`, `(4,-4)`, `(4,4)`, `(-5,4)` |

Correspondence is explicit identity correspondence by vertex and edge index: `[0, 1, 2, 3]`.

Edge index `1`, the `+X` side from `(5,-4)` to `(5,4)` at `z=5`, transitions to `(4,-4)` to `(4,4)` at `z=6`. That transition face is classified as the chamfer transition face.

## 6. Topology findings

The canonical X2 row succeeds through the prismatic emitter and preserves the EDGE-PROFILE-X2 split-face witness counts:

- vertices: `12`
- edges: `20`
- faces: `10`
- planar faces: `10`
- cylindrical faces: `0`
- lower prism side faces: `4`
- transition faces: `4`
- chamfer transition faces: `1`
- loops: `10`
- coedges: `40`
- bounds: `[-5,-4,0]..[5,4,6]`

The lower vertical side faces and upper transition faces remain split at `z=5`, including where adjacent portions are coplanar. This is intentional because it preserves the section-stack witness and transition-interval evidence. X2 does not merge coplanar faces.

## 7. STEP smoke findings

The body exports through the existing `Step242Exporter`.

Required markers present:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Required markers absent:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

## 8. Invalid/rejected cases

Invalid cases reject before emitter invocation:

- width/depth/height `<= 0` or non-finite: `edge-prismatic-x2-invalid-dimensions-rejected`
- chamfer distance `<= 0`, non-finite, greater than/equal to the conservative top inset bound, or greater than/equal to height: `edge-prismatic-x2-invalid-chamfer-distance-rejected`

The finite recommendation vocabulary is:

- `prismatic-top-edge-chamfer-ready-for-production-evaluation`
- `prismatic-top-edge-chamfer-needs-emitter-hardening`
- `prismatic-top-edge-chamfer-invalid-rejected`
- `prismatic-top-edge-chamfer-deferred`

## 9. No-trim/no-graft/no-legacy-route guarantee

Successful X2 rows record deterministic diagnostics confirming:

- `edge-prismatic-x2-no-air-edge-sweep-used`
- `edge-prismatic-x2-no-brep-bounded-chamfer-used`
- `edge-prismatic-x2-no-topology-graft-used`
- `edge-prismatic-x2-no-3d-boolean-used`

The candidate path has no BRep trim engine, no topology graft/body mutation, no AirEdgeSweep use, no `BrepBoundedChamfer` use, no 3D Boolean fallback, no sketch solver, no clipping engine, and no NURBS/freeform support.

## 10. Relationship to current ProfileStackExtrudeExecutor

`ProfileStackExtrudeExecutor` remains unchanged. X2 does not broaden it, route production profile-stack cases through the emitter, or replace current production behavior.

The X2 result specifically removes the EDGE-PROFILE-X2 one-off Route B witness as the motivating top-edge chamfer proof path: the proof now runs through the prismatic emitter lane. The existing profile-stack blocker remains documented for production because packaging, validation hardening, feature recognition, and route admission have not been done.

## 11. Recommendation for next milestone

Recommendation: `prismatic-top-edge-chamfer-ready-for-production-evaluation` for the bounded lab fixture.

Two reasonable follow-up milestones are:

1. **EDGE-PRISMATIC-V1 production-adjacent emitter packaging**: package the lab emitter behind explicit internal gating and production-adjacent validation, without changing default production chamfer/fillet authority.
2. **EDGE-PRISMATIC-X3 generic polygon transition lab**: broaden evidence first with additional line-only equal-count polygon transitions and harder correspondence diagnostics before production-adjacent packaging.

If broader evidence is desired before route admission, X3 is the safer next step. If the only target is the canonical history-known top-edge chamfer family, V1 packaging can be evaluated next with strict gating.
