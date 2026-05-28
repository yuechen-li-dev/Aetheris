# EDGE-A0 / V2-A4 — AirEdgeSweep fillet/chamfer architecture audit

## 1. Executive summary
Fillets/chamfers in current Aetheris production behavior are implemented as legacy bounded BRep feature operations (`BrepBoundedChamfer`, `BrepBoundedFillet`) routed through Firmament bounded execution seams.

V2 evidence from V2-V5, V2-X8.1, and V2-X8.2 confirms that bounded chamfer recognition is load-bearing for some legacy topology routes (notably triangle prism), and that summary/STEP parity is insufficient when adjacency/corner contracts diverge.

The forward architecture direction should treat chamfers/fillets as constructive edge sweeps (`AirEdgeSweep`) rather than discovered topology repair.

This milestone is a decision-grade state audit and bounded roadmap proposal only. It does not change production behavior.

## 2. Why this audit exists
Triangle production migration to `LineArcProfileExtrudeEmitter` was attempted and reverted because bounded chamfer/corner recognition depended on legacy adjacency/corner topology conventions that were not preserved.

V2-A3 doctrine formalized the rule that load-bearing topology remains legacy-authoritative until a replacement contract is explicitly proven (including feature-recognition parity).

`AirEdgeSweep` is the likely replacement contract for future constructive chamfer/fillet behavior, so this audit exists to define admissible scope, risk boundaries, and first-lab sequencing.

## 3. Current code/docs inspected
### Core/docs
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`
- `docs/v2-v5-triangle-prism-profile-extrude-production.md`
- `docs/frictionlab/v2-x8-1-triangle-chamfer-adjacency-parity-lab.md`
- `docs/frictionlab/v2-x8-2-triangle-adjacency-delta-audit.md`
- `docs/surface-feature-a0-architecture-audit.md`
- `docs/surface-feature-a1-descriptors.md`
- `docs/surface-feature-a2-planning-bridge.md`
- `docs/surface-feature-a3-planar-groove-dry-run.md`
- `docs/surface-feature-a4-planar-groove-evidence.md`

### Code/test surfaces examined
- `Aetheris.Kernel.Core/Brep/EdgeFinishing/BrepBoundedChamfer.cs`
- `Aetheris.Kernel.Core/Brep/EdgeFinishing/BrepBoundedFillet.cs`
- `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs`
- chamfer/fillet/corner/triangle-related tests under:
  - `Aetheris.Kernel.Firmament.Tests`
  - `Aetheris.Kernel.Core.Tests`
  - `Aetheris.FrictionLab.Tests`

### Diagnostics searched
- `No bounded corner-resolution candidate was admissible`
- `Bounded fillet edge resolution rejected`

## 4. Current chamfer architecture
### Entry points
- Firmament execution routes chamfer booleans through bounded chamfer execution (`ExecuteBoundedChamferOnRecognizedOrthogonalRoot`) and then into `BrepBoundedChamfer` candidate families.

### Bounded corner resolution / candidate selection
- Bounded chamfer uses explicit bounded candidates (single edge, incident edge-pair corner, and single-corner modes depending on recognized context and selectors).
- Admissibility and rejection reasons are explicit and diagnostic-first.

### Diagnostics
- Failure reports include bounded context/source diagnostics and corner-resolution rejection details, including the known message `No bounded corner-resolution candidate was admissible` in failed parity routes.

### Assumptions about adjacency/corner ordering
- Chamfer admissibility is sensitive to emitted local topology conventions (loop/coedge ordering/orientation and corner incidence semantics).
- Triangle migration labs show this sensitivity is currently load-bearing.

### Supported/unsupported envelopes (current)
- Supported: bounded, explicit selector-driven chamfer cases within recognized root contexts (including specific orthogonal and trusted-polyhedral bounded lanes).
- Unsupported/deferred: generic open-ended chamfer surfacing, unconstrained multi-corner chain resolution, and broad freeform edge modification.

### Relationship to triangle primitive
- For chamfer-sensitive triangle production, legacy `BrepPrimitives.CreateTriangularPrism` remains authoritative until adjacency contract parity is proven.

### Separation
- **Construction intent**: explicit bounded chamfer operation with constrained selectors.
- **Topology discovery**: still dependent on existing emitted topology contracts rather than a fully declared constructive edge-sweep contract.
- **Legacy adjacency assumptions**: load-bearing in triangle/corner parity evidence.
- **Corner patch policy**: bounded candidate-based resolution with explicit rejection when no admissible corner candidate exists.

## 5. Current fillet architecture
### Entry points
- Firmament bounded fillet route (`ExecuteBoundedManufacturingFilletOnRecognizedOrthogonalRoot`) dispatches to `BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge` and candidate families.

### Supported analytic cases / construction style
- Current bounded fillet is centered on constant-radius cylindrical internal concave bounded cases with explicit admissibility.
- Candidate families include single-edge and chained same-radius cylindrical lanes, plus bounded cylindrical-termination contexts.

### Diagnostics
- Explicit rejection diagnostics for context/radius/edge validity and candidate admissibility.
- Representative rejection: `Bounded fillet edge resolution rejected: ...`.

### Supported/unsupported envelope
- Supported: bounded internal concave edge selections with deterministic local loop/corner construction constraints.
- Unsupported/deferred: broad convex edge replacement families, variable-radius generalization, unconstrained corner-networks, and generic rolling-ball reconstruction.

### Relationship to analytic surface families
- Current bounded fillet route materially emits/depends on cylindrical fillet faces for admitted scopes.
- Broader analytic families (plane/cylinder/cone/sphere/torus) remain relevant for future classification but are not all currently generalized under one constructive fillet contract.

### Separation
- **Construction intent**: bounded constant-radius fillet intent over selected edges.
- **Topology discovery**: partially local constructive rebuild with bounded assumptions, still constrained by legacy metadata/topology contracts.
- **Legacy assumptions**: occupied-cell safe-composition metadata and local incidence conventions are required.
- **Corner patch policy**: bounded one/two-corner chain behavior with deterministic rejection outside scope.

## 6. AirEdgeSweep concept
Proposed `AirEdgeSweep` should be a declared constructive edge-surface operation contract.

Required conceptual inputs:
- host body / host feature context,
- target edge or edge chain,
- adjacent faces,
- sweep profile:
  - straight line for chamfer,
  - circular arc for fillet,
  - variable profile explicitly deferred for first production scopes,
- offset rule:
  - distance,
  - radius,
  - face-relative offsets,
  - tangent constraints,
- convex/concave classification,
- corner policy,
- admissibility diagnostics,
- fallback/legacy behavior policy.

Contract aliases:
- `AirChamfer` = constrained `AirEdgeSweep` with straight-line profile.
- `AirFillet` = constrained `AirEdgeSweep` with circular-arc profile.

## 7. Chamfer as ruled edge transition
Conceptually:
- Construct offset edge curves on the two adjacent host faces.
- Connect the two offset curves with straight generators.
- In planar-planar straight-edge cases, resulting chamfer face is planar/ruled.
- Adjacent host faces are trimmed/replaced according to a declared topology plan.

First-scope boundary:
- planar-planar straight edge,
- constant chamfer distance,
- no corner chain,
- no variable distance.

## 8. Fillet as circular-profile edge sweep
Conceptually:
- Construct tangent/offset guide curves from adjacent faces.
- Sweep a circular arc profile along the target edge path.
- Planar-planar constant-radius fillet reduces to cylindrical face emission.
- Plane-cylinder contexts may classify to bounded toroidal/other analytic families when explicitly admitted.
- Corner patches must be explicit constructions, not discovered late-stage rolling-ball repairs.

First-scope boundary:
- planar-planar straight edge,
- constant radius,
- convex/concave separated,
- no variable radius.

## 9. Convex vs concave policy
Architectural distinction:
- Concave fillets/chamfers can often be additive transition patches in bounded contexts and are typically lower risk.
- Convex fillets/chamfers replace/remove sharp external edge topology and require deterministic trim/replacement semantics.
- Convex cases are the primary anti-subtract target and must be treated as explicit replacement construction.
- Both lanes should be constructive; first lab ordering can differ for risk control.

Recommended lab tracks:
- concave additive patch lab first (safest proof lane),
- convex planar chamfer replacement lab next,
- convex planar fillet replacement after.

## 10. Corner patch policy
Corners are where legacy/discovery-heavy approaches tend to fail due to ambiguity in chain interaction and termination.

Provisional policy:
- Equal-radius convex triple-edge fillet corners may be admissible via explicit sphere patch construction.
- Unequal-radius/mixed-family corner cases are deferred.
- Chamfer triple-corner cases may require explicit planar/triangular corner patch construction.
- Initial labs should isolate or avoid corner chains until single-edge semantics are proven.

First-scope limitations (explicit):
- no broad multi-edge corner-network solver,
- no unequal-radius convex corner blending,
- no implicit rolling-ball corner discovery.

## 11. Proposed AirEdgeSweep admissibility ladder
1. host/edge/faces are analytic and in supported families,
2. edge chain is simple and bounded,
3. adjacent faces are known and oriented,
4. convex/concave classification is deterministic,
5. offset/tangent curves are constructible,
6. sweep profile is admissible,
7. corner policy is known for this request,
8. topology replacement/addition plan is deterministic,
9. STEP/export analytic-family output is supported for the admitted scope or explicitly deferred with diagnostics.

## 12. Relationship to V2-A3 parallel lanes
- `BrepBoundedChamfer` remains legacy-authoritative for currently chamfer-sensitive production routes.
- `AirEdgeSweep` should be developed in parallel lab lanes.
- Legacy chamfer/fillet routes must not be retired before feature-recognition parity is proven for affected scopes.
- Triangle production migration may be revisited only after AirEdgeSweep or explicit adjacency-contract hardening closes known deltas.

## 13. Relationship to ruled surfaces and surface offsets
- Chamfer is closely aligned with an `AirRuledTransition`-like edge transition family.
- Fillet is a circular-profile edge sweep and is not strictly ruled.
- Surface offset is related but higher risk (self-intersection/trim stability) and should remain separately bounded.
- Do not collapse this work into generic UV/NURBS surfacing; preserve no-NURBS guardrail.

## 14. Recommended first labs
Proposed sequence:
- **EDGE-X1**: Chamfer/fillet capability and diagnostic matrix inventory (code + fixtures + explicit unsupported map).
- **EDGE-X2**: Concave planar chamfer additive patch lab (simplest constructive edge-sweep proof).
- **EDGE-X3**: Convex planar single-edge chamfer replacement lab (offset + chamfer face + deterministic local topology rebuild).
- **EDGE-X4**: Planar-planar constant-radius fillet cylindrical edge-sweep lab.
- **EDGE-X5**: Three-edge equal-radius convex corner patch audit/lab.
- **EDGE-V1**: Production-adjacent `AirChamfer` for first bounded admitted case.

Rationale for sequence: starts with currently lower-risk constructive/additive geometry, then moves to replacement-heavy convex semantics.

## 15. Migration checklist for future AirEdgeSweep PRs
Each future `AirEdgeSweep` PR should include:
- supported edge and adjacent-face families,
- convex/concave scope,
- offset/tangent construction contract,
- profile construction contract,
- corner policy,
- topology replacement/addition plan,
- resulting analytic surface-family declaration,
- STEP smoke markers for admitted cases,
- recognizer parity evidence (candidate/admissible counts and first-delta diagnostics),
- assertion that no unintended Boolean/subtract fallback was introduced,
- explicit legacy fallback/authority policy.

## 16. Risks and guardrails
### Risks
- Rebuilding rolling-ball complexity under a new name.
- Scope drift into generic surfacing/NURBS.
- Regressing existing chamfer behavior while pursuing migration.
- Corner patch logic becoming unbounded/underdetermined.
- Continued hidden dependency on face/edge ordering without explicit contract.
- Underestimating convex replacement complexity vs concave additive cases.

### Guardrails
- Bounded analytic first scopes only.
- Lab-first, production-second sequencing.
- Explicit admissibility and rejection diagnostics.
- Separate convex vs concave tracks.
- No generic UV/NURBS expansion.
- Legacy remains authoritative until parity is proven.
- No test weakening.

## 17. Non-goals
- no `AirEdgeSweep` implementation,
- no production route changes,
- no change to existing chamfer/fillet behavior,
- no triangle migration retry,
- no STEP/exporter changes,
- no Boolean core changes,
- no generic surfacing expansion,
- no NURBS/freeform expansion,
- no sketch-solver/clipping-engine expansion.


## EDGE-X1 follow-up note
- EDGE-X1 added a decision-grade chamfer/fillet capability and diagnostic matrix inventory: `docs/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`.
- This confirms current bounded chamfer/fillet routes remain legacy-authoritative and defines first AirEdgeSweep lab sequencing from observed support/reject envelopes.


## EDGE-X2 follow-on

A lab-only constructive concave planar AirChamfer patch proof was added in EDGE-X2 (`docs/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`) without production chamfer/fillet migration.

- EDGE-X2.1 introduces a lab-only AirChamfer policy scaffold to gate admissibility/route decisions before broader geometry work.

- EDGE-X4 update: convex planar single-edge requests can now produce a lab-only replacement topology plan after Judgment admission; production geometry emission remains deferred.

- EDGE-V1 (2026-05-28): internal production-adjacent convex planar single-edge AirChamfer prototype seam added; legacy BrepBoundedChamfer remains authoritative.

## EDGE-A1 compatibility matrix note

EDGE-A1 adds `docs/edge-a1-chamfer-fillet-support-compatibility-matrix.md` as the durable support/readiness matrix for chamfer and fillet work. Future AirEdgeSweep, AirChamfer, and AirFillet milestones should update that matrix when evidence changes support status, and production migration proposals should cite the relevant row IDs and readiness gates instead of relying on this audit alone.
