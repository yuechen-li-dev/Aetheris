# CIR-PRISMATIC-X1 — Prismatic CIR mirror feasibility lab

Status: **Lab/test-only prototype**. No production analyzer, CLI, STEP exporter/importer, Boolean core, BRep topology, AIR emitter, production prismatic route, or CIR-to-BRep extraction behavior changed.

## 1. Purpose and scope

CIR-PRISMATIC-X1 evaluates the first bounded CIR/FRep mirror strategy for prismatic section-transition bodies whose AIR-authored source is a stack of line-only convex sections with stable vertex correspondence. The lab answers a narrow feasibility question: can map occupancy and point containment be represented by an admitted mirror for prismatic corpus cases without claiming topology parity or production `analyze map` support?

The implementation is deliberately in `Aetheris.Firmament.FrictionLab.CIRLab` as `CirPrismaticMirrorLab`. It is not wired into `StepAnalyzer`, `aetheris analyze map`, production CIR dispatch, production BRep spatial queries, STEP import/export, Boolean operations, or AIR/prismatic emitters.

## 2. References

This milestone depends on the current Aetheris V2 and AIR/CIR/BRep authority split:

- `docs/development/milestones/general/aetheris-v2-sweep-first-architecture.md`
- `docs/development/milestones/general/air-cir-a0-authority-and-mirror-contract.md`
- `docs/development/milestones/general/air-cir-x1-mirror-metadata-prototype.md`
- `docs/development/milestones/general/cir-map-x1-primitive-map-prototype.md`
- `docs/development/milestones/general/cir-map-x2-mirror-aware-primitive-map-dispatch.md`
- `docs/development/milestones/general/edge-prismatic-x7-analyze-map-cir-frep-audit.md`

The key inherited constraint is that CIR/FRep may answer field questions such as containment or occupancy only when the mirror is explicitly admitted. BRep remains authority for explicit topology, face identity, loop identity, split-face lineage, and STEP exchange.

## 3. Strategies compared

### 3.1 Half-space / convex polyhedron mirror

The half-space prototype represents a convex all-planar section-transition body as the intersection of deterministic oriented planes:

- two Z cap half-spaces from the lowest and highest section;
- one side half-space per corresponding section edge per transition interval;
- signed containment as the maximum plane violation.

The planes are derived from AIR/prismatic section data, not from reverse-engineered STEP. This preserves the intended mirror provenance path and avoids introducing STEP importer/exporter coupling. For convex line-only stacks such as `rectangle-inset` and `top-edge-chamfer`, the lab admits the mirror as `mirror-admitted-exact` for point containment and map occupancy within the lab tolerance.

Future production shape options remain open: `CirHalfSpaceNode`, `CirConvexPolyhedronNode`, or a tape payload operation. X1 does not add any production CIR node kind.

### 3.2 Section-stack implicit evaluator

The section-stack prototype evaluates membership from the AIR-authored Z section stack:

- find the section interval containing the point Z;
- linearly interpolate corresponding vertices at that Z;
- classify the XY point against the interpolated convex polygon;
- combine that with cap-Z bounds.

This mirrors the construction intent and is easy to audit for prismatic routes with explicit correspondence. It is more specialized than a general convex-polyhedron evaluator, but it can encode the section-stack contract directly. X1 keeps it as a comparison evaluator rather than recommending it as the first general CIR node.

## 4. Case results

| Case | Strategy | Status | Point classification | Map-like summary | Recommendation |
| --- | --- | --- | --- | --- | --- |
| `rectangle-inset` | half-space convex polyhedron | `mirror-admitted-exact` | all expected inside/outside points match | 16×16 top-view grid, 256 occupied / 0 empty, thickness min 0.250, max 1.000 | `cir-prismatic-mirror-use-convex-polyhedron-first` |
| `rectangle-inset` | section-stack implicit | `mirror-admitted-exact` | all expected inside/outside points match | same stable summary as half-space | comparison path; keep as section-stack fallback/evaluator candidate |
| `top-edge-chamfer` | half-space convex polyhedron | `mirror-admitted-exact` | all expected inside/outside points match | 16×16 top-view grid, 256 occupied / 0 empty, thickness min 5.312, max 6.000 | `cir-prismatic-mirror-use-convex-polyhedron-first` |
| `top-edge-chamfer` | section-stack implicit | `mirror-admitted-exact` | all expected inside/outside points match | same stable summary as half-space | comparison path; keep as section-stack fallback/evaluator candidate |

Optional hexagon/pentagon cases were not implemented in X1. The strategy should extend to convex line-only stacks, but X1 intentionally stays bounded to the required `rectangle-inset` and `top-edge-chamfer` evidence.

## 5. Point classification findings

`rectangle-inset` includes these checks:

- center inside at mid-height;
- outside far +X;
- outside far +Y;
- inside near the lower full rectangle but outside the upper inset when sampled near the lower Z;
- outside at upper Z where the inset excludes the point;
- near-side-plane tolerance checks on the upper +X side.

`top-edge-chamfer` includes these checks:

- inside lower body below transition;
- outside above/inset excluded area near the +X top transition;
- inside below the chamfer plane near transition;
- outside beyond the chamfer plane;
- center inside.

Both strategies classify the required points deterministically.

## 6. Map-like summary findings

The lab uses a deterministic 16×16 top-view sampling grid over the AIR-authored prismatic bounds. For each XY sample it estimates Z thickness with a fixed 96-sample ray through the mirror field. The summary is intentionally map-like rather than production `analyze map`: it reports occupied/empty sample counts and approximate min/max/average thickness, but it does not produce face IDs, visible topology, or BRep raycast parity.

The top-view grids have no empty XY cells because both required cases retain a nonzero lower-body thickness throughout the full lower footprint. The minimum thickness is therefore near the most eroded top footprint corner/edge, not zero.

## 7. Mirror status and metadata policy

For both required cases, both strategies return:

- status: `mirror-admitted-exact`;
- capabilities: point containment, map occupancy, section sampling, approximate volume;
- known losses: face identity, loop identity, split-face lineage, feature role labels, topology parity;
- lossy topology/face-identity requests: `mirror-rejected-lossy-for-request`.

This is lab metadata only. AIR-CIR-X1 production admission still does not expose a general prismatic mirror to production map dispatch.

## 8. Known losses

The X1 mirrors answer field occupancy, not topology. Known losses are explicit and deterministic:

- face identity lost;
- loop identity lost;
- split-face lineage lost;
- chamfer transition face label lost unless a later milestone preserves it as metadata;
- exact topology parity unavailable.

Any request for face identity or topology parity is rejected as `mirror-rejected-lossy-for-request`.

## 9. Recommendation

Recommendation: **use the convex polyhedron / half-space strategy first** for the next implementation step.

Rationale:

- it covers both required cases exactly for containment/map occupancy;
- it naturally generalizes to convex all-planar prismatic bodies beyond section-stack-specific construction;
- it can be represented later as `CirHalfSpaceNode`, `CirConvexPolyhedronNode`, or a tape payload op without changing production behavior in X1;
- the section-stack evaluator remains valuable as an AIR-intent comparison oracle and possible specialized evaluator, but it is narrower and depends more directly on section correspondence semantics.

Recommended diagnostic token: `cir-prismatic-mirror-use-convex-polyhedron-first`.

## 10. Non-goals

X1 does not:

- change production analyzer behavior;
- change default CLI behavior;
- add public API;
- change STEP exporter/importer behavior;
- change Boolean core behavior;
- change BRep topology;
- change AIR emitter behavior;
- change production prismatic routes;
- perform CIR-to-BRep extraction;
- claim production prismatic map support;
- make gated artifact corpus stability tests run by default.

## 11. Tests run

Focused X1 validation:

```bash
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "CirPrismatic"
```

Required broader regression commands for this milestone were run after the implementation and documentation updates; see the PR/test summary for exact command outcomes.

## 12. Next milestone

The next milestone should be one of:

- **CIR-PRISMATIC-X2 first-class mirror node/evaluator**: introduce a first-class half-space/convex-polyhedron CIR mirror or tape payload with explicit admission metadata and tolerance policy; or
- **EDGE-PRISMATIC-X8 hybrid map dispatch with admitted mirror**: only after X2 provides an admitted prismatic mirror handle, route production map dispatch through the hybrid policy without losing BRep topology responsibilities.

Do not route production `analyze map` through this X1 lab prototype directly.

## CIR-PRISMATIC-X2 follow-up

CIR-PRISMATIC-X2 promotes the X1-recommended half-space / convex-polyhedron strategy into a reusable internal Core mirror component (`CirConvexPolyhedronMirror` built by `CirPrismaticMirrorBuilder`). The promoted seam remains test/lab-visible only: rectangle-inset and top-edge-chamfer section stacks can be admitted exact for point containment and occupancy-style summaries, while face identity and topology parity remain rejected as lossy requests. Production `analyze map` dispatch is still unchanged.
