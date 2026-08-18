# EDGE-X1 — Chamfer/fillet capability and diagnostic matrix

## 1. Executive summary
This inventory captures the **current production chamfer/fillet behavior** and its diagnostics across Core + Firmament execution seams. No production behavior changes were made in this milestone. Current `BrepBoundedChamfer` and `BrepBoundedFillet` routes remain legacy-authoritative, and this matrix is intended to guide the first bounded AirEdgeSweep labs.

## 2. Source code/docs/tests inspected

### Core classes
- `Aetheris.Kernel.Core/Brep/EdgeFinishing/BrepBoundedChamfer.cs`
- `Aetheris.Kernel.Core/Brep/EdgeFinishing/BrepBoundedFillet.cs`
- `Aetheris.Kernel.Core/Brep/EdgeFinishing/BrepBoundedEdgeFinishingToolParser.cs`

### Firmament execution seams
- `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs`
  - `ExecuteBoundedChamferOnRecognizedOrthogonalRoot(...)`
  - `ExecuteBoundedManufacturingFilletOnRecognizedOrthogonalRoot(...)`

### Tests/fixtures inspected (edge-finish relevant)
- `Aetheris.Kernel.Core.Tests/Brep/Features/BrepBoundedChamferTests.cs`
- `Aetheris.Kernel.Firmament.Tests/FirmamentPrimitiveExecutionTests.cs`
- `Aetheris.Kernel.Firmament.Tests/FirmamentBooleanRequiredFieldValidationTests.cs`
- `Aetheris.Kernel.Firmament.Tests/FirmamentBuildAndExportTests.cs`
- `Aetheris.FrictionLab.Tests/CIRLab/TrianglePrismAdjacencyDeltaAuditLabTests.cs`

### Docs inspected
- `docs/development/milestones/general/aetheris-v2-sweep-first-architecture.md`
- `docs/development/milestones/general/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`
- `docs/development/milestones/general/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/development/milestones/general/v2-v5-triangle-prism-profile-extrude-production.md`
- `docs/development/milestones/frictionlab/v2-x8-1-triangle-chamfer-adjacency-parity-lab.md`
- `docs/development/milestones/frictionlab/v2-x8-2-triangle-adjacency-delta-audit.md`
- `docs/development/milestones/general/surface-feature-a0-architecture-audit.md`
- `docs/development/milestones/general/surface-feature-a1-descriptors.md`
- `docs/development/milestones/general/surface-feature-a2-planning-bridge.md`
- `docs/development/milestones/general/surface-feature-a3-planar-groove-dry-run.md`
- `docs/development/milestones/general/surface-feature-a4-planar-groove-evidence.md`

## 3. Current entry points

### Chamfer
- Core API entry points:
  - `ChamferAxisAlignedBoxVerticalEdge(...)` (convex external vertical edge token path)
  - `ChamferAxisAlignedBoxSingleCorner(...)` (axis-aligned corner path)
  - `ChamferTrustedPolyhedralSingleCorner(...)` (trusted body corner path, triangle/non-box included when admissible)
  - `ChamferTrustedPolyhedralIncidentEdgePair(...)` (explicit pair at a corner)
  - `ChamferTrustedPolyhedralSingleInternalConcaveEdge(...)` (internal concave edge path)
- Firmament route: `ExecuteBoundedChamferOnRecognizedOrthogonalRoot(...)`.
- Context requirements:
  - convex edge mode requires recognized box-root or orthogonal additive root;
  - internal concave tokens require occupied-cell additive/safe-subtract roots;
  - corner mode may fallback to trusted polyhedral if box-recognition fails.
- Policy/scoring:
  - `JudgmentEngine` chooses bounded corner / edge-pair / internal-concave candidates;
  - explicit reject candidates preserve deterministic diagnostics.

### Fillet
- Core API entry point:
  - `FilletTrustedPolyhedralSingleInternalConcaveEdge(...)`
- Firmament route: `ExecuteBoundedManufacturingFilletOnRecognizedOrthogonalRoot(...)`.
- Context requirements:
  - recognized orthogonal additive or safe-subtract roots;
  - explicit internal concave preflight selection;
  - one-edge and two-edge (chained interaction) bounded contexts only.
- Policy/scoring:
  - `JudgmentEngine` selects among
    - single-edge cylindrical,
    - chained same-radius cylindrical,
    - chained cylindrical termination,
    - reject fallback.

## 4. Capability matrix overview

| Feature type | Geometry context | Convex/concave | Current status | Entry point / test coverage | Expected analytic surface | Diagnostics on rejection | AirEdgeSweep relevance | Recommended next action |
|---|---|---|---|---|---|---|---|---|
| chamfer | planar-planar (single explicit vertical edge on box) | convex | supported | `ChamferAxisAlignedBoxVerticalEdge`; Core/Firmament chamfer execution tests | plane | distance-too-large / invalid token diagnostics | straight-profile edge sweep parity target | baseline replacement lab candidate |
| chamfer | planar-planar (single internal edge) | concave | supported | `ChamferTrustedPolyhedralSingleInternalConcaveEdge`; Firmament concave chamfer tests | plane | preflight + bounded concave edge rejection strings | direct straight-profile concave sweep relevance | first low-risk chamfer sweep lab |
| chamfer | orthogonal box corner | convex | supported | `ChamferAxisAlignedBoxSingleCorner`; Firmament corner tests | plane (tri-plane corner cut) | `No bounded corner-resolution candidate was admissible` path | corner sweep + corner-patch gating | preserve as legacy contract |
| chamfer | triangular prism corner (non-orthogonal trusted polyhedral) | mostly convex in test path | supported (bounded) | `ChamferTrustedPolyhedralSingleCorner`; Firmament triangular prism test + X8 docs | plane | corner resolution rejected diagnostics when inadmissible | high relevance for non-orthogonal edge-sweep parity | keep as load-bearing parity fixture |
| chamfer | edge chain (2-edge concave interaction fixture) | concave | supported in bounded pair fixture | internal concave two-edge candidate + Firmament E8b case | plane | bounded concave rejection diagnostics | chain sequencing precursor for sweep | capture deterministic chain fixtures |
| chamfer | corner chain (generic multi-corner) | mixed | deferred/unknown | no explicit multi-corner generic chamfer route found | unknown/deferred | parser/selector or candidate inadmissibility | likely later corner-patch lane | add explicit lab-only inventory fixture |
| fillet | planar-planar internal concave single edge | concave | supported | `FilletTrustedPolyhedralSingleInternalConcaveEdge`; canonical internal fillet test | cylinder | `Bounded fillet edge resolution rejected` | circular-profile edge sweep canonical target | EDGE-X4 first fillet sweep target |
| fillet | planar-planar internal concave chained pair | concave | supported | chained adjacent-pair test | cylinder (multiple) | same bounded fillet rejection family | chain handling needed for AirEdgeSweep | preserve chain fixture + diagnostics |
| fillet | cylindrical termination follow-on pair | concave with cylindrical source context | supported (bounded conditions) | chained cylindrical termination tests (same/cross-radius; anchor-smaller reject) | cylinder (+ source cylindrical adjacency) | termination support mismatch diagnostics | sweep termination policy parity target | add explicit parity envelope in labs |
| fillet | planar-planar convex external replacement | convex | rejected/unsupported | box-root reject test | unknown/deferred | bounded fillet rejected / scope diagnostics | future convex replacement track | EDGE-X5 after concave parity |
| fillet | plane-cylinder general | mixed | deferred/partial | only bounded chained cylindrical-termination evidence | cylinder/unknown | candidate inadmissibility diagnostics | medium relevance; constrain scope first | isolate explicit plane-cylinder fixtures |
| fillet | variable radius | mixed | unsupported | no variable-radius route; constant radius only | unknown/deferred | parse/validation/reject candidate path | out of first AirEdgeSweep scope | keep out-of-scope |

## 5. Chamfer matrix
- **Supported single-edge convex**: axis-aligned vertical external edge tokens are supported with strict bounded-distance checks.
- **Supported single-edge concave**: trusted polyhedral internal concave edge tokens are supported via occupied-cell preflight and deterministic candidate selection.
- **Supported corner cases**:
  - orthogonal box single-corner chamfer,
  - trusted polyhedral single-corner chamfer (including non-orthogonal triangular prism load-bearing case).
- **Supported pair interaction case**: internal concave two-edge interaction candidate (E8b-style load-bearing fixture).
- **Unsupported/rejected envelopes**:
  - overlarge distances,
  - invalid/unsupported token forms,
  - ineligible source-root context,
  - inadmissible corner candidate sets.
- **Diagnostics** are explicit for corner inadmissibility and concave-edge resolution failure; these should remain authoritative during AirEdgeSweep labs.

## 6. Fillet matrix
- **Supported internal concave constant-radius**:
  - single-edge planar-planar,
  - chained adjacent same-radius pair,
  - cylindrical-termination follow-on pair when support predicates pass.
- **Unsupported convex external replacement**: box-root convex replacement path is rejected.
- **Unsupported variable-radius**: no variable-radius route present.
- **Plane-cylinder evidence**: only bounded cylindrical-termination contexts are evidenced; generic plane-cylinder fillet coverage is not established.
- **Corner-chain coverage**: no generic corner-chain fillet capability identified in current bounded route.

## 7. Diagnostics inventory

| Diagnostic string | Source class/method area | Feature | Meaning | Typical trigger | Unsupported scope vs bug |
|---|---|---|---|---|---|
| `No bounded corner-resolution candidate was admissible` | `BrepBoundedChamfer` corner candidate evaluation | chamfer | no corner candidate passed admissibility guards | non-matching corner context / geometry / guards | usually unsupported scope (expected) |
| `Bounded chamfer corner resolution rejected: ...` | `BrepBoundedChamfer` corner paths | chamfer | wrapped corner rejection with candidate reasons | reject candidate selected or no admissible selection | unsupported scope unless contradicted by contract test |
| `Bounded concave edge resolution rejected: ...` | `BrepBoundedChamfer` internal concave path | chamfer | internal concave candidate set rejected | preflight or admissibility mismatch | mostly unsupported envelope / invalid request |
| `Bounded fillet edge resolution rejected: ...` | `BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge` | fillet | fillet candidate set rejected | non-concave, wrong count, non-bounded radius, unsupported termination | mostly unsupported envelope |
| `No bounded single-edge/chained fillet candidate was admissible.` | `BrepBoundedFillet.BuildRejectReason` | fillet | generic no-admissible fallback | candidate predicates all false | unsupported envelope |
| `Bounded fillet local builder could not construct the selected internal-edge cylindrical cut chain: ...` | `BrepBoundedFillet.BuildConcaveFilletBody` | fillet | local constructive step failed | loop/corner chain could not be applied | may indicate bug *or* unsupported topology nuance |

## 8. Existing fixture/test inventory

| Test / fixture | Scenario | Expected outcome | Feature | Geometry context | Preserve as legacy contract |
|---|---|---|---|---|---|
| `ChamferAxisAlignedBoxVerticalEdge_Succeeds_ForSingleExplicitConvexEdge` | box vertical external edge chamfer | success | chamfer | planar-planar convex | yes |
| `ChamferAxisAlignedBoxVerticalEdge_Rejects_OverlargeDistance` | overlarge distance | reject w/ diagnostics | chamfer | bounded limit | yes |
| `ChamferAxisAlignedBoxSingleCorner_Succeeds_ForBoundedCanonicalCorner` | canonical box corner | success | chamfer | orthogonal box corner | yes |
| `Compile_Executes_BoundedChamfer_SingleCorner_For_NonOrthogonalTriangularPrism` | non-orthogonal triangle prism corner | success | chamfer | triangular prism corner | yes (load-bearing) |
| `Compile_Executes_BoundedConcaveChamfer_For_CanonicalLRootInternalEdge` | internal concave edge | success | chamfer | planar-planar concave | yes |
| `Compile_Executes_BoundedConcaveChamfer_For_E8b_TwoEdgeInteraction_Firmament_Case` | concave two-edge interaction | success | chamfer | edge-chain concave | yes |
| `Compile_BoundedFilletCanonicalInternalCase_Executes_With_CylindricalFace` | single internal concave fillet | success | fillet | planar-planar concave | yes |
| `Compile_BoundedFilletChainedAdjacentPair_Executes_With_MultipleCylindricalFaces` | chained pair fillet | success | fillet | concave edge chain | yes |
| `Compile_Rejects_BoundedFillet_On_BoxRoot` | convex/box-root fillet request | reject | fillet | convex external envelope | yes |
| `Compile_Rejects_BoundedFillet_ThreeEdgeChain_Request` | unsupported chain width | reject | fillet | >2 edge chain | yes |
| `Compile_Executes_BoundedFillet_ChainedCylindricalTermination_ForSameRadiusFollowOnPair` | supported cylindrical termination | success | fillet | cylindrical termination | yes |
| `Compile_Rejects_BoundedFillet_ChainedCylindricalTermination_Attempt_ForMismatchedRadius` | unsupported termination radius relation | reject | fillet | cylindrical termination | yes |

## 9. Gap analysis
- **No explicit generic corner-chain tests** for chamfer/fillet beyond bounded single-corner / two-edge interactions.
- **Plane-cylinder fillet parity gap**: only special chained cylindrical-termination contexts are covered.
- **Convex fillet replacement gap**: current behavior is explicit rejection; no parity candidate yet.
- **Diagnostics without dedicated assertion tests** likely exist for some failure strings (especially deeper local-builder failure branches).
- **Topology-order dependency risk** remains for trusted polyhedral/corner resolution and triangle parity lanes.
- **Unknown/no-test cells** remain for cylinder-cylinder fillet/chamfer and broader multi-corner sequencing.

## 10. Recommended AirEdgeSweep lab order
Evidence-driven recommended order:
1. **EDGE-X2**: concave planar chamfer additive patch lab (lowest-risk parity target with strong current fixtures).
2. **EDGE-X3**: convex planar single-edge chamfer replacement lab.
3. **EDGE-X4**: planar-planar internal concave fillet cylindrical edge-sweep lab.
4. **EDGE-X5**: convex planar fillet replacement lab (currently explicit reject envelope).
5. **EDGE-X6**: corner patch audit/lab (including non-orthogonal triangle load-bearing paths).

## 11. Guardrails for future labs
- No production replacement until parity is proven against current bounded contracts.
- Legacy `BrepBoundedChamfer` / `BrepBoundedFillet` remain authoritative until explicit retirement criteria are met.
- Keep convex and concave tracks separate.
- Do not attempt generic rolling-ball fillet behavior.
- Do not widen into generic surfacing/NURBS/freeform.
- Preserve explicit diagnostics and deterministic rejection reasons.
- Require feature-recognition parity before any legacy path retirement.

## 12. Non-goals
- No behavior changes.
- No AirEdgeSweep implementation.
- No chamfer/fillet production migration.
- No triangle migration retry.
- No STEP importer/exporter changes.
- No Boolean core changes.
- No test weakening.


## EDGE-X2 note

EDGE-X2 starts the first constructive AirChamfer proof lane with a lab-only concave planar single-edge patch artifact (`docs/development/milestones/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`).

- EDGE-X2.1 status: policy scaffold lane started (lab-only), with deterministic accept/defer/reject routing and score breakdown.
