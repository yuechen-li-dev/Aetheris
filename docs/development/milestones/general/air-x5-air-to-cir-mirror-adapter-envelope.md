# AIR-X5 — AIR-to-CIR mirror adapter envelope

## Purpose and scope

AIR-X5 adds a minimal internal/test-visible adapter that lets selected Constructive AIR nodes expose admitted CIR mirror metadata for analysis and evaluation side channels. It is intentionally an envelope around existing mirror evidence, not a CIR geometry expansion, analyzer replacement, topology parity milestone, or production route change.

The first supported source nodes are:

- `AirPrismaticSectionTransition` through the existing convex/all-planar prismatic mirror path.
- `AirTopFaceLoopChamfer` when the generated prismatic/chamfer section stack is convex/all-planar and can be admitted by the same mirror builder.

## Relationship to AIR-A0 through AIR-X4

AIR-A0 defines the authority boundary: AIR owns constructive intent, BRepPlan/BRep own explicit topology and export planning, and CIR is an admitted field/evaluation side-channel. AIR-X1 provided minimal wrappers over proven lanes, AIR-X2 documented route-selection/admissibility, AIR-X3 introduced prismatic BRepPlan topology roles, and AIR-X4 overlaid top-face loop chamfer Class B provenance and chamfer-face BRepPlan roles.

AIR-X5 consumes those facts without changing their authority. AIR provenance may be preserved in adapter metadata, but CIR mirror metadata remains evaluation-only and does not become topology or feature-role truth.

## AIR-to-CIR side-channel doctrine

The side channel is:

`Constructive AIR -> admitted CIR mirror -> analysis/map/containment/bounds/thickness checks`

It is not:

`CIR -> BRep topology`, `CIR -> STEP construction truth`, or `CIR -> feature/face identity authority`.

## Adapter model

The adapter model is implemented by `AirCirMirrorAdapter` and related internal records/enums under `Aetheris.Kernel.Core.Air`.

- Request: source AIR node kind, route kind, source kind, source node id, requested capabilities, selection class, rule kind, and construction-history kind.
- Result: success flag, stable summary, optional prismatic mirror result, and optional deterministic top-view mirror summary.
- Status: reuses existing CIR mirror statuses such as `mirror-admitted-exact`, `mirror-unavailable`, `mirror-rejected-unsupported-atom`, `mirror-rejected-lossy-for-request`, and `mirror-rejected-stale-or-mismatched`.
- Backend: admitted first-scope mirrors report `cir-convex-polyhedron`.
- Capabilities: occupancy, map, containment, bounds, and thickness summary are surfaced only when the existing convex mirror supports the underlying operations.
- Losses: face identity, loop identity, edge identity, topology parity, feature labels, chamfer-face identity, and BRepPlan role parity are explicit AIR-level losses.
- Provenance: generated/native AIR, source node id, route, selection class, rule kind, mirror-builder route, and recommendation are recorded as metadata.
- Diagnostics: stable AIR-X5 diagnostics describe adapter creation, mirror request creation, admission/rejection, authority denial, and no production-route/analyzer changes.
- Guarantees: evaluation side-channel only; no topology authority; no production analyzer, production route, or BRepPlan behavior change.

## Prismatic section transition adapter

The canonical prismatic case uses the existing three-section stack:

- width `10`, depth `8`, height `6`, inset/chamfer distance `1`;
- z0 = `0` full rectangle;
- z1 = `5` full rectangle;
- z2 = `6` inset rectangle.

AIR-X5 routes this through `CirPrismaticMirrorBuilder.BuildFromSections`. When the builder admits the convex polyhedron, the adapter reports the convex mirror backend and evaluation capabilities (occupancy/map/containment/bounds and thickness summary where available). Known losses include no face identity, no loop identity, no edge identity, no topology parity, and no feature labels.

## Top-face loop chamfer adapter

The top-face loop chamfer adapter builds the same generated prismatic/chamfer section stack used by the existing prototype. It preserves Class B provenance in AIR adapter metadata:

- source node kind: `TopFaceLoopChamfer`;
- route: `TopFaceLoopChamferPrismatic`;
- selection class: `FaceBoundaryLoop`;
- rule kind: `UniformChamfer`.

The admitted CIR backend is still the convex polyhedron mirror. Additional losses declare that CIR does not claim chamfer-face identity and does not claim BRepPlan role parity.

## Provenance metadata is not topology capability

AIR-X5 may record that a source operation was a generated Class B face-boundary loop uniform chamfer. That metadata helps downstream analysis understand the source path. It does not grant the CIR mirror the ability to identify chamfer faces, loops, BRepPlan roles, or topology parity. Those remain explicit losses until a future milestone admits them with evidence.

## Rejected and deferred cases

The adapter returns deterministic unavailable/rejected summaries for:

- imported or recovered STEP/BRep sources: no AIR mirror is inferred from imported topology;
- BRep-only bodies with no AIR source: mirror unavailable;
- unsupported AIR nodes or routes: mirror unavailable;
- non-convex or otherwise unsupported prismatic stacks: mirror rejected/unavailable according to existing builder evidence;
- missing top-face loop chamfer Class B provenance: stale/mismatched rejection;
- requests for face identity, topology parity, or chamfer-face identity: lossy-for-request rejection.

## No production analyzer behavior change

AIR-X5 does not replace production analyzer/map dispatch, production route selection, CIR evaluator/tape behavior, BRepPlan behavior, STEP exporter/importer behavior, BRep topology behavior, Firmament lowering behavior, Boolean behavior, AirEdgeSweep behavior, BrepBoundedChamfer/BrepBoundedFillet behavior, chamfer/fillet/shell geometry, arbitrary graph support, import/recovery, triangle migration, or NURBS/freeform behavior.

## No topology authority

CIR remains an evaluation mirror only. BRepPlan and BRep remain the authorities for planned and explicit topology.

## Tests run

Focused AIR-X5 tests were added to `Aetheris.Kernel.Core.Tests` for prismatic admission, top-face loop chamfer admission with Class B provenance, topology/role parity denial, imported/BRep-only rejection, unsupported AIR nodes, deterministic summaries, and non-convex rejection.

## Recommended next milestone

Recommended: **AIR-X6 — AIR/BRepPlan/CIR unified artifact summary for prismatic + loop chamfer**.

Implementation showed that AIR-X3/X4 already carry useful BRepPlan topology-role metadata while AIR-X5 now carries evaluation-mirror metadata. A unified non-production artifact summary would make the authority boundaries easier to inspect without changing production routes or claiming CIR topology parity.

## AIR-X6 trace reporting

`aetheris trace` reports AIR-X5 CIR mirror admission, backend, capabilities, provenance, and losses. CIR remains an evaluation side-channel and does not gain face identity, loop identity, topology parity, chamfer-face identity, or BRepPlan role parity authority.

## AIR-X7 fixture note

The AIR-X7 valid top-face loop chamfer fixture reaches the `cir-mirror` trace stage by reusing the existing admitted AIR-X5 top-face loop chamfer mirror envelope. The fixture does not make CIR a topology authority.


## AIR-REGION-X3 side-hole region mirror envelope

The side-hole `FaceAttachedRegion` now has an analysis-only AIR Region to CIR mirror envelope. It uses a summary backend named `cir-region-parent-minus-cylinder`, admits conservative occupancy/containment/bounds summary capabilities, and denies topology, face, loop, boundary patch, BRepPlan role, STEP/export, and production integration authority.
