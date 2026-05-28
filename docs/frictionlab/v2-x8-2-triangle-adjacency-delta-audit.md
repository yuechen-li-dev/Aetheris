# V2-X8.2 — Triangle legacy-vs-linearc adjacency delta audit

## Purpose and scope
Lab-only forensic audit that compares the legacy `BrepPrimitives.CreateTriangularPrism(...)` topology against candidate `LineArcProfileExtrudeEmitter` topology to isolate first deterministic delta explaining bounded chamfer/corner recognition divergence. No production routing changes are included.

## Lineage
- V2-V5 migration reverted after chamfer regression.
- V2-X8 parity lab was insufficient for feature recognition.
- V2-X8.1 isolated adjacency/feature-recognition mismatch for all triangle cases.

## Captured ledger
Body/face/edge/vertex/loop/coedge ledger plus chamfer admissibility diagnostics for `XMaxYMaxZMax`.

## Findings summary
Across all three triangle cases, bodies are produced on both paths, ledgers are captured, and first concrete mismatch is adjacency-structural (edge/side ordering), followed by chamfer admissibility mismatch (`legacy=1/1`, `candidate=0/1`).

## Root-cause hypothesis
Emitter ordering/orientation convention at loop/coedge seam differs from legacy triangular prism contract, changing corner candidate incidence used by bounded chamfer recognition.

## Recommendation
Keep legacy route; harden emitter ordering/orientation parity before any production migration retry.

## Non-goals
No production migration, chamfer behavior change, STEP change, Boolean-core change, or hex/slot migration.

## Update note (V2-A3)
V2-A3 codifies this audit outcome into migration doctrine: geometry/STEP parity is necessary but insufficient when downstream recognizers depend on adjacency/corner contracts. Triangle production remains legacy-authoritative until replacement topology contract parity is demonstrated.

Reference: `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`.
