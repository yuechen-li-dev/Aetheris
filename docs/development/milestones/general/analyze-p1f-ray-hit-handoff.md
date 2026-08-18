# ANALYZE-P1f ray-hit handoff (post P1e)

Date: 2026-05-05

## What landed in P1e

`FaceDomainQuery.TryClassifyPointOnFace(...)` now provides a query-layer contract to classify a point projected onto a face as:

- `Inside`
- `OnBoundary`
- `Outside`
- `Ambiguous`
- `Unsupported`

For each query, it also returns structured diagnostics needed by P1f (`SurfaceKind`, projected UV when available, boundary distance, ambiguity flags, and stable source/reason strings).

## P1f consumption pattern

For each ray/surface candidate hit in generic provider:

1. Compute geometric hit on candidate surface.
2. Call `FaceDomainQuery.TryClassifyPointOnFace(body, faceId, hitPoint, tolerance)`.
3. Accept/reject candidate using result classification:
   - **Admissible:** `Inside`
   - **Conditionally admissible:** `OnBoundary` (keep with boundary ambiguity flag and dedupe handling)
   - **Inadmissible:** `Outside`
   - **Inadmissible / needs fallback:** `Ambiguous`, `Unsupported`
4. Feed `NearEdge`, `NearVertex`, `SeamDuplicateRisk`, and `BoundaryDistance` into duplicate-hit collapse and tie-breaking.
5. Carry `Source`/`Reason` into containment diagnostics for `multi_axis_ray_consensus` traceability.

## Current implementation bounds

- **Implemented:** planar faces (`SurfaceGeometryKind.Plane`), with trimmed-domain evaluation via `AnalyticPlanarFaceDomain`.
- **Deferred:** cylinder/sphere/cone/torus/bspline face trim-domain classification.

Deferred reasons are explicit and returned as `Unsupported` with stable source values:

- `FaceDomainQuery.UnsupportedCylinder`
- `FaceDomainQuery.UnsupportedSphere`
- `FaceDomainQuery.UnsupportedCone`
- `FaceDomainQuery.UnsupportedTorus`
- `FaceDomainQuery.UnsupportedBSpline`

## Planar behavior details relied on by P1f

- Hit point is projected to plane local UV.
- Classification is against the trimmed face domain (not infinite plane).
- Multi-loop planar domains include outer + hole exclusions where `AnalyticPlanarFaceDomain` can resolve loops.
- Near-boundary hits are promoted to `OnBoundary` (not inside/outside).
- If planar trim loops cannot be resolved, result is `Ambiguous` with `TrimUnavailable=true` and source `FaceDomainQuery.Planar.TrimUnavailable`.

## Remaining work in P1f/P1g

1. Add robust per-surface projection + trim-space membership for cylindrical/spherical (then cone/torus).
2. Add seam-aware UV canonicalization and duplicate collapse rules for periodic surfaces.
3. Wire `FaceDomainQuery` results into generic ray provider and then activate `multi_axis_ray_consensus` candidate in `BrepSpatialQueries.ClassifyPoint`.
