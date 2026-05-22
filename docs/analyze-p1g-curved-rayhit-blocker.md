# ANALYZE-P1g curved analytic surface ray-hit blocker report

Date: 2026-05-05
Outcome: Honest stop

## Scope inspected first

- `Aetheris.Kernel.Core/Brep/Queries/BrepSpatialQueries.cs`
- `Aetheris.Kernel.Core/Brep/Queries/FaceDomainQuery.cs`
- `Aetheris.Kernel.Core/Geometry/Surfaces/{CylinderSurface,ConeSurface,SphereSurface,TorusSurface}.cs`
- `Aetheris.Kernel.Core/Brep/BrepPrimitives.cs`
- `Aetheris.Kernel.Core/Brep/Queries/AnalyticDisplayQuery.cs`
- `Aetheris.Kernel.Core.Tests/Brep/Queries/{FaceDomainQueryTests,BrepSpatialQueriesRaycastTests}.cs`
- `Aetheris.CLI.Tests/CliBaselineTests.cs`

## Evidence

`BrepSpatialQueries.ClassifyPoint` currently gates `multi_axis_ray_consensus` behind `TryCreatePlanarRayProvider`, which explicitly rejects any non-planar face with reason:

> "Generic planar ray-hit provider requires all faces to be planar in this milestone."

`FaceDomainQuery` is currently planar-only for trim/domain membership and returns structured `Unsupported*` for cylinder/sphere/cone/torus surfaces.

The repo already contains curved ray/surface equations in `AnalyticDisplayQuery`, but this path is display-first and not wired to parity-safe containment quality rules used by `multi_axis_ray_consensus`.

## Why this blocks truthful P1g completion

For parity-safe containment on trimmed BReps, P1g requires:

1. trusted intersection candidates on curved surfaces,
2. trusted face-domain (trim) admission for those hit points,
3. deterministic seam/duplicate/tangent handling integrated into parity counting.

(1) exists in partial form (`AnalyticDisplayQuery`), but (2) is not implemented for curved faces in `FaceDomainQuery`, and (3) exists only in simplified planar-provider quality flags.

Implementing curved support in `BrepSpatialQueries` without first landing curved `FaceDomainQuery` trim classification would force either:

- counting infinite-surface hits without trim truth (geometry-incorrect), or
- silently downgrading to heuristic acceptance (non-deterministic and parity-unsafe).

Both violate the milestone's geometry-truthful requirement.

## Verified primitive semantics from source

From `BrepPrimitives` and analytic surface definitions:

- Cylinder: axis is world +Z, side surface origin at bottom center `(0,0,-h/2)`, bounded by planar caps at `z=±h/2`.
- Sphere: center at origin, radius is constructor argument.
- Torus: center at origin, axis world +Z, major/minor radii as arguments.
- Cone primitive constructor is not present in `BrepPrimitives` in current branch.

## Next minimal convergent step (recommended before re-attempting P1g)

1. Extend `FaceDomainQuery` with curved trim/domain membership for analytic faces (cylinder/cone/sphere/torus) using existing topological loops and robust UV projection.
2. Introduce a generic analytic ray provider in `BrepSpatialQueries` that delegates intersection to analytic equations and always requires successful `FaceDomainQuery` admission.
3. Port planar quality classification (`BoundaryHit`, `DuplicateOrCoincident`, `NearTangent`, `Ambiguous`) to curved hits with seam-aware coalescing.
4. Then enable `multi_axis_ray_consensus` for mixed planar/curved shells.

Without this order, any P1g patch would be either overbroad or brittle.
