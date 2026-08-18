# ANALYZE-P1d-impl-2 blocker report (honest stop)

Date: 2026-05-05

## Outcome

Honest stop.

## Why implementation was stopped

A bounded, trustworthy `multi_axis_ray_consensus` implementation requires a generic face-hit provider that can prove trim inclusion for arbitrary imported faces. Current core topology/geometry bindings do not persist face-level 2D trim loops or robust surface-domain evaluators needed to classify ambiguous seam/vertex/edge/tangent hits deterministically for non-primitive BRep bodies.

## Evidence from code inspection

- `BrepSpatialQueries.Raycast` currently hard-gates to primitive recognition (`TryResolvePrimitive`) and has no non-primitive per-face hit contract.
- `ClassifyPoint` has judgment candidates scaffolded, but only `primitive_analytic` is executable; `multi_axis_ray_consensus` is intentionally disabled (`GenericRayHitProviderAvailable: false`).
- Face bindings map only `FaceId -> SurfaceGeometryId`; no persisted per-face UV boundary domain/trim representation is available in query-layer APIs.
- Topology loops/coedges encode adjacency but not canonical per-face 2D trim-space loops needed for robust in-trim tests after analytic ray/surface intersections.

## Narrow next blocker

Implement/queryable face-trim projection contract (planar/cylindrical/spherical first) that can answer:

1. hit point projection validity in surface parameter space,
2. inside/on/outside trim classification with tolerance,
3. edge/vertex ambiguity detection and seam duplicate resolution.

Once available, deterministic six-axis parity can be safely activated without guessing through tangent/boundary cases.
