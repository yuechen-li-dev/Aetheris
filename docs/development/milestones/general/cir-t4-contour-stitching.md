# CIR-T4 — deterministic contour stitching into chains/loops

## Purpose

CIR-T4 consumes CIR-T3 marching-squares contour segments and deterministically stitches them into UV contour chains with conservative closure/ambiguity classification.

## Input and non-goals

Input is only `SurfaceTrimContourExtractionResult` from T3; no resampling and no rerun of marching squares.

Still deferred:
- analytic snap / exact curve recognition,
- BRep topology emission,
- STEP export changes,
- seam-wrapped parameter domains.

## Endpoint clustering policy

- Endpoints are processed in stable order (`V`, then `U`, then source segment/index endpoint label).
- Endpoints within tolerance are clustered into one node.
- Tolerance policy: use provided `ToleranceContext`; otherwise `ToleranceContext.Default.Linear` in UV-space.
- Nodes with degree >2 are diagnosed as ambiguous branch nodes.

## Graph traversal policy

- Node = endpoint cluster; edge = contour segment.
- Connected components are collected deterministically from ascending segment index.
- Chain point ordering is deterministic and stable for repeated runs on identical input.
- Ambiguous branching is not force-resolved into “clean loops”; the chain is marked ambiguous.

## Classification policy

- `ClosedLoop`: no open endpoints (or start/end re-close within tolerance), no branch ambiguity.
- `BoundaryTouching`: open chain and at least one open endpoint lies on domain boundary (`u=0|1` or `v=0|1` within tolerance).
- `OpenChain`: open chain not touching domain boundary.
- `Ambiguous`: degree>2 branch or odd-degree topology inconsistent with clean chain semantics.
- `Degenerate`: insufficient distinct points or near-zero length.
- `Empty`: no chain points (empty input path).

Classification is intentionally conservative.

## Diagnostics

Stitching diagnostics include:
- stitching start/input segment count,
- endpoint cluster count,
- ambiguous cluster count,
- closed/open/boundary/ambiguous/degenerate counts,
- explicit deferred flags for analytic snap, BRep topology, exact export.

## Numerical status

This remains numerical UV polyline stitching only; it does not claim exact analytic contour identity.

## SEM-A0 status

CIR-T4 adds no generated topology naming and no selector/provenance expansion; SEM-A0 guardrails remain preserved.

## Next (CIR-T5)

Add bounded analytic snap candidates (circle/line/etc.) over stitched chains with explicit admissibility/rejection diagnostics while keeping topology/export behavior unchanged.
