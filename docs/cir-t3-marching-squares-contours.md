# CIR-T3 — Deterministic marching-squares contour extraction over restricted-field grids

## Purpose

CIR-T3 adds a deterministic, numerical contour extraction stage that consumes the CIR-T2 `RestrictedFieldSampleGrid` and computes approximate zero-crossing segments in source UV space.

This milestone answers: **where does `g(u,v)=0` approximately cross the sampled planar parameter domain?**

## Scope and constraints

Implemented:
- Marching squares over planar rectangular restricted-field grids.
- Deterministic cell scan order (row-major by `CellJ`, then `CellI`).
- Linear interpolation on crossed cell edges using sampled scalar values.
- Deterministic ambiguity diagnostics and pair selection.
- Explicit non-goal diagnostics (stitching/snap/export unavailable).

Not implemented (deferred):
- contour stitching into loops,
- analytic snap / exact curve recognition,
- BRep topology emission,
- STEP export behavior changes,
- seam-aware parameter domains and non-planar source parameter spaces.

## Marching-squares policy

- Input is **only** `RestrictedFieldSampleGrid` from CIR-T2. No resampling is performed.
- Cells classified `Inside` / `Outside` are skipped.
- `Unknown` cells are skipped and counted in diagnostics.
- `Mixed` and `Boundary` cells are processed for possible edge intersections.
- Edge crossings are found from endpoint values/signs with tolerance-aware boundary handling.

## Interpolation policy

For edge endpoints `(a, b)` with scalar values `(va, vb)`:
- if one endpoint is boundary-valued, use that endpoint directly,
- if signs differ, compute `t = va / (va - vb)`,
- if denominator is near-zero, use midpoint `t=0.5`,
- clamp `t` to `[0,1]` defensively.

The extracted UV location is linearly interpolated from the edge endpoint UVs.

## Ambiguous / boundary handling

Cells with more than two edge intersections are treated as ambiguous saddles.

T3 resolves these deterministically using a center-value decider (`avg(corner values)`), records ambiguity diagnostics, and emits deterministic segment pairings. This keeps behavior stable while preserving explicit diagnostics for future refinement in T4.

## Output model

Extraction returns `SurfaceTrimContourExtractionResult` containing:
- method (`MarchingSquares`),
- ordered 2D segments (`SurfaceTrimContourSegment2D`),
- optional lifted 3D endpoints when parameterization is provided,
- aggregate diagnostics,
- explicit `ContourStitchingImplemented=false`, `AnalyticSnapImplemented=false`, `ExactExportAvailable=false`.

## Numerical status

This is a numerical trim-oracle output only.
Contours are approximate polyline segments in UV; no exact analytic claim is made.

## SEM-A0 status

CIR-T3 introduces no generated topology naming and no provenance model expansion. SEM-A0 guardrails remain preserved.

## Recommended CIR-T4

Add deterministic contour stitching from segments into open/closed polyline chains, including junction diagnostics and continuity tolerances, while keeping analytic snap explicitly deferred.
