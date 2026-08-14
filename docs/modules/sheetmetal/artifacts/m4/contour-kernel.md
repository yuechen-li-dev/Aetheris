# 2D profile audit and exact contour API

## Audit

Reusable machinery already existed in `Aetheris.Kernel.Firmament.Materializer`:

- `ResolvedProfile2D` and ordered line/arc loops;
- `ProfileArrangementBuilder` analytic line-line, line-circle, and circle-circle intersections;
- exact parameter splitting, classification, material-side reconstruction, and region composition;
- profile extrusion planning/materialization preserving planar and cylindrical support geometry;
- semantic per-segment provenance and normal Profile Concept Paths.

Sheet Metal duplicated point-polygon normalization, convex hull fallback, shared-edge cancellation, overlap checks, and circle sampling. M4 does not add a Sheet-Metal-only geometry engine. It publishes the reusable profile operations and introduces a shared contour contract over the existing native curves.

## Final API

`PlanarContour2` owns:

- `StableId`, `PlaneFrame`, and `Provenance`;
- one ordered `PlanarContourLoop2 OuterLoop`;
- ordered inner loops;
- ordered `PlanarContourSegment2` values with stable ID, `ProfileSegmentProvenance`, and native geometry.

Supported native geometry is `LineArcLineSegment2D`, `LineArcCircularArc2D`, and `LineArcFullCircle2D`. Ellipses and NURBS are not admitted.

Bounded operations:

- `ProfileArrangementBuilder.IntersectBounded` returns normalized parameters, tangent/coincident evidence, and positive bounded-overlap evidence;
- `SplitBounded` and `TrimBounded` retain native curve kinds;
- `PlanarContourKernel.Offset` handles explicit left/right line/arc chains with miter support intersections;
- `ProfileArrangementBuilder.Compose` remains the known-material-topology stitch/composition authority;
- `PlanarContourKernel.FromResolvedProfile` / `ToResolvedProfile` connect normal Profiles, Sheet Metal, Drawing-ready consumers, and future DXF serialization.

Validation checks finite support, closure, endpoint authority, segment ID uniqueness, non-adjacent bounded intersection/overlap, exact signed winding (outer CCW, inner CW), nonzero area, and inner-loop nesting. Impossible/collapsed offsets are typed rejections; no silent fix or generic BRep Boolean is used.

The compatibility point boundary remains in `SheetMetalFlatPatternIr` for older consumers, but authored flat regions/cuts/reliefs now carry exact contours and manufacturing output consumes them first.
