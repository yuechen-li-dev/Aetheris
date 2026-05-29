# CIR-PRISMATIC-X2 — Convex Polyhedron CIR Mirror

## 1. Purpose and scope

CIR-PRISMATIC-X2 promotes the CIR-PRISMATIC-X1 half-space feasibility proof into a reusable, internal/test-visible Core component. The new seam models admitted convex, all-planar prismatic section transitions as a convex polyhedron whose field value is the maximum signed half-space violation.

The milestone is intentionally bounded:

- the mirror is internal/test-visible only;
- no production `analyze map` dispatch changes;
- no default CLI behavior changes;
- no public API changes;
- no STEP importer/exporter, Boolean, BRep topology, AIR emitter, or production prismatic route changes;
- no CIR-to-BRep extraction;
- no production support claim for prismatic BRep maps.

## 2. References

This work follows the authority split and mirror vocabulary established by:

- `docs/air-cir-a0-authority-and-mirror-contract.md`;
- `docs/air-cir-x1-mirror-metadata-prototype.md`;
- `docs/cir-map-x1-primitive-map-prototype.md`;
- `docs/cir-map-x2-mirror-aware-primitive-map-dispatch.md`;
- `docs/cir-prismatic-x1-prismatic-mirror-feasibility.md`.

CIR-PRISMATIC-X1 compared two bounded prismatic strategies: a convex polyhedron/half-space mirror and a section-stack implicit evaluator. X2 implements the half-space strategy first because it naturally covers convex all-planar prismatic bodies and is not tied to one emitter-specific section-stack evaluator.

## 3. Component/API shape

The reusable internal component lives in `Aetheris.Kernel.Core.Cir.Mirrors`:

- `CirHalfSpacePlane` stores a normalized oriented plane/half-space;
- `CirConvexPolyhedronMirror` stores the half-space set, bounds, metadata admission result, and deterministic diagnostics;
- `CirPrismaticMirrorBuilder` builds the mirror from `PrismaticSection` source data and optional `PrismaticCorrespondenceMap`;
- `CirPrismaticMirrorResult` reports success/rejection status, metadata, diagnostics, and recommendation;
- `CirPrismaticMirrorSummary` reports deterministic top-view occupancy/thickness summaries;
- `CirConvexPointClassification` reports `Inside`, `Boundary`, or `Outside`;
- `CirPrismaticMirrorRequestKind` models accepted analysis requests and lossy topology requests.

The component is internal and currently exercised by `Aetheris.Kernel.Core.Tests`; it is not wired into production analyzer dispatch.

## 4. Half-space sign convention

Each half-space uses this convention:

```text
plane-value(point) = normal.X*x + normal.Y*y + normal.Z*z + offset
inside if max(plane-value(point)) <= tolerance
outside if max(plane-value(point)) > tolerance
boundary if max violation is within +/- tolerance
```

`CirConvexPolyhedronMirror.Evaluate(point)` returns the maximum plane violation. This makes containment deterministic and makes rejection of unsupported/non-convex inputs explicit.

## 5. Builder input contract

`CirPrismaticMirrorBuilder.BuildFromSections` accepts AIR/prismatic source data, not STEP or recovered BRep topology. The supported input contract is:

- at least two `PrismaticSection` instances;
- each section has one outer loop;
- line-only loops (`HasArcs == false`);
- no holes;
- equal vertex counts across sections;
- strictly increasing `Z` values;
- finite coordinates;
- convex counter-clockwise section loops;
- optional correspondence must be a deterministic bijection over the vertex range.

The builder creates two cap half-spaces and one side half-space per profile edge per section interval. Plane orientation is deterministic: side planes are derived from corresponding section edges, then flipped if needed so the section-stack centroid is inside.

Unsupported or ambiguous inputs reject deterministically with `mirror-rejected-unsupported-atom` and a `cir-prismatic-x2-mirror-rejected-unsupported:<reason>` diagnostic.

## 6. Supported cases

Required cases are implemented and covered by focused tests:

| Case | Status | Half-spaces | Notes |
| --- | --- | ---: | --- |
| `rectangle-inset` | `mirror-admitted-exact` | 6 | two Z caps plus four sloped side planes |
| `top-edge-chamfer` | `mirror-admitted-exact` | 10 | two Z caps plus eight interval side planes, including the chamfer transition plane |

The optional polygon-scaled cases are not claimed by X2 tests, but the builder is intentionally general for convex, line-only, equal-vertex section stacks.

## 7. Point classification findings

`rectangle-inset` classifications match the X1 motivating cases:

- center point: inside;
- far +X point: outside;
- far +Y point: outside;
- lower-Z point admitted by the lower full rectangle: inside;
- upper-Z point excluded by the inset top rectangle: outside.

`top-edge-chamfer` classifications match the X1 motivating cases:

- lower body below the transition: inside;
- near +X top transition where the inset excludes it: outside;
- below the chamfer plane: inside;
- beyond the chamfer plane: outside;
- center: inside.

## 8. Map-like summary findings

The X2 mirror provides a deterministic 16x16 top-view summary helper. It intersects each vertical sample ray with the half-space set and reports occupied/empty counts plus min/max/average thickness.

Stable X2 summaries from the focused tests:

| Case | Grid | Occupied | Empty | Thickness min | Thickness max | Thickness avg |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| `rectangle-inset` | 16x16 top | 256 | 0 | approximately 0.75 | 4.0 | approximately 3.05 |
| `top-edge-chamfer` | 16x16 top | 256 | 0 | approximately 3.3125 | 4.0 | approximately 3.9531 |

These are occupancy-style summaries for the admitted mirror, not production `analyze map` results.

## 9. Known losses and rejected requests

The admitted mirror is exact for bounded containment and map occupancy, but intentionally lossy for topology authority. Known losses are surfaced in metadata and diagnostics:

- face identity lost;
- loop identity lost;
- split-face lineage lost;
- feature role labels lost;
- exact topology parity unavailable.

Requests for face identity or topology parity return `mirror-rejected-lossy-for-request`; no map summary is returned as a substitute for those lossy requests.

## 10. Relationship to section-stack evaluator

The X1 section-stack evaluator remains useful as an AIR-intent comparison oracle and as a possible future specialized evaluator. X2 does not make it the primary mirror. The reusable Core seam is the convex half-space mirror; section-stack comparison remains X1 evidence unless a future milestone adds a clean oracle adapter.

## 11. Relationship to `analyze map`

CIR-PRISMATIC-X2 is not integrated with production `analyze map` or with CIR-MAP-X2 dispatch. Future hybrid dispatch must be an explicit follow-on milestone, such as EDGE-PRISMATIC-X8, after admission, drift/parity policy, and CLI behavior are separately reviewed.

## 12. Non-goals

X2 explicitly does not implement:

- production analyzer behavior changes;
- default CLI behavior changes;
- new production map support for prismatic BReps;
- topology/face identity parity;
- split-face lineage preservation as topology;
- non-convex prismatic bodies;
- holes, arcs, or multiple loops;
- inferred correspondence;
- imported STEP-to-CIR mirror recovery;
- CIR-to-BRep extraction;
- STEP importer/exporter changes;
- Boolean core changes;
- BRep topology changes;
- AIR emitter behavior changes;
- production prismatic route changes.

## 13. Tests run

Focused validation run during X2 development:

```text
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --filter "CirPrismaticX2"
```

Broader regression commands are recorded in the PR/test summary for the implementation change.

## 14. Next milestone

Likely next milestones are:

1. **EDGE-PRISMATIC-X8** — hybrid map dispatch using admitted prismatic mirrors, still without topology claims;
2. **CIR-PRISMATIC-X3** — add tape payload/lowering for admitted prismatic mirrors;
3. **AIR-CIR-A1** — drift/parity policy for mirror freshness and authority boundaries.

## 12. EDGE-PRISMATIC-X8 consumption note

EDGE-PRISMATIC-X8 consumes the X2 `CirConvexPolyhedronMirror` in a lab/test-only hybrid map dispatcher. Generated `rectangle-inset` and `top-edge-chamfer` source sections are admitted through the X2 builder, select the `cir-convex-polyhedron` backend for occupancy/thickness summaries, and still reject face-identity/topology-parity requests as lossy. This consumption does not wire the mirror into production `StepAnalyzer.AnalyzeMap`, does not add a default CLI route, does not infer mirrors from imported STEP, and does not perform CIR-to-BRep extraction.

## EDGE-PRISMATIC-X9 CLI consumption note

EDGE-PRISMATIC-X9 consumes the CIR-PRISMATIC-X2 convex-polyhedron mirror from an explicit experimental CLI route: `aetheris experimental prismatic-map --case <case> --rows <n> --cols <n> --json`. The route admits generated prismatic sources for map occupancy only and reports known losses for face identity, loop identity, split-face lineage, feature role labels, and topology parity. It does not add CIR-to-BRep extraction and does not infer mirrors from imported STEP.
