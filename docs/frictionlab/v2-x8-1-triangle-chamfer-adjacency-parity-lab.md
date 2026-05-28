# V2-X8.1 — Triangle chamfer adjacency feature-recognition parity lab

## Purpose and scope
Lab-only diagnostic probe that compares legacy triangle prism construction against a `LineArcProfileExtrudeEmitter` candidate for downstream bounded chamfer/corner recognition parity. No production routing changes are included.

## Why this exists
V2-V5 attempted production migration and was reverted after non-orthogonal triangle chamfer gates failed with `No bounded corner-resolution candidate was admissible`. V2-X8 summary topology parity was therefore insufficient.

## Feature-recognition parity definition
For each case, parity means:
- comparable topology counts,
- comparable edge/vertex adjacency rows,
- comparable bounded-corner admissibility outcomes,
- no first divergence diagnostic.

## Construction compared
- Legacy: `BrepPrimitives.CreateTriangularPrism(width, depth, height)`.
- Candidate: line-only triangle loop with coordinates `(-w/2,-d/2)`, `(w/2,-d/2)`, `(0,d/2)` routed through `LineArcProfileExtrudeEmitter.TryEmit`.

## Chamfer path inspected
The lab invokes `BrepBoundedChamfer.ChamferTrustedPolyhedralSingleCorner(..., XMaxYMaxZMax, distance)` on both bodies and captures admissibility/rejections.

## Cases
- `triangle-basic`
- `triangle-non-orth-chamfer`
- `triangle-alt`

## Findings summary
The lab records deterministic diagnostics:
- `v2-x8-1-triangle-chamfer-adjacency-lab-started`
- `v2-x8-1-legacy-triangle-created`
- `v2-x8-1-linearc-triangle-created`
- `v2-x8-1-adjacency-summary-captured`
- `v2-x8-1-chamfer-candidates-captured`
- parity success/mismatch and first-divergence payloads
- `v2-x8-1-no-3d-boolean-used`
- blocker classification payload when mismatch exists

## Blocker classification and recommendation
This lab emits one of:
- `triangle-feature-recognition-parity-ready`
- `triangle-feature-recognition-needs-adjacency-parity`
- `triangle-feature-recognition-needs-corner-resolution-contract`
- `triangle-feature-recognition-keep-legacy-route`

## Non-goals
- No production migration.
- No chamfer test weakening.
- No STEP/Boolean core changes.
- No hex or slot migration.

## Next milestone
If parity rows stay green: prepare a tightly scoped renewed triangle production migration attempt with seam-hardening assertions.
If parity mismatches: isolate emitter ordering/adjacency convention deltas before any production retry.



## Update note (V2-X8.2)
A deterministic first-delta forensic audit now lives at `docs/frictionlab/v2-x8-2-triangle-adjacency-delta-audit.md`, replacing coarse blocker wording with concrete mismatch category/payload evidence.
