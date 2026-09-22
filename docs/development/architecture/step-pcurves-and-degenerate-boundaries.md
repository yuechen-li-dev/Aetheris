# STEP Pcurves and Degenerate Boundaries

STEP face boundaries carry two related facts. The 3D `EDGE_CURVE` is the model-space locus; a face-local `PCURVE` is that edge use in the support surface's parameter space. Aetheris preserves both. A `CoedgePcurveBinding` binds a UV curve to a specific coedge, face, and support-surface identity, so repeated uses of one edge are not collapsed.

## Import path

`SURFACE_CURVE` and `SEAM_CURVE` associated geometry is traversed through `PCURVE` and `DEFINITIONAL_REPRESENTATION`. The bounded decoder accepts:

- `LINE` and `POLYLINE`
- `CIRCLE` and `ELLIPSE`, including the 2D placement axis
- `B_SPLINE_CURVE_WITH_KNOTS` and its ordinary `B_SPLINE_CURVE` constructor data
- complex `RATIONAL_B_SPLINE_CURVE`, retaining degree, controls, weights, knots, and domain
- parameter-trimmed `TRIMMED_CURVE`, retaining its interval and traversal sense

An unrepresentable associated curve fails with `Importer.Pcurve.UnsupportedCurve`; it is not silently discarded. Cartesian-point trims and `COMPOSITE_CURVE` remain outside this bounded decoder. No encountered qualified fixture requires either form.

Pcurve coordinates are UV values and are never length-normalized. Imported bindings retain the STEP pcurve, source curve, source support entity IDs, and original curve type. `BrepPcurveValidator` separately compares the surface-evaluated UV curve with the 3D edge at the import tolerance and checks loop closure modulo `SurfacePeriodicity`. Sub-tolerance drift is accepted; material disagreement is diagnostic failure.

For a periodic seam, the same 3D edge can have two coedge uses and two pcurves on different periodic sides. Import assigns distinct pcurve entity identities to repeated edge uses and minimizes loop-local raw UV discontinuity. Equal competing assignments are rejected as ambiguous. Source face `same_sense` is never a tie-breaker.

## Degenerate loops

`LoopKind.Edge` owns ordered coedge uses. `LoopKind.Vertex` owns exactly one real vertex and no coedges. `VertexLoopParameterBinding` relates that loop and vertex to its owning face, support surface, optional UV location, and STEP provenance.

The STEP importer admits a `VERTEX_LOOP` only at a supported collapsed boundary (currently a sphere pole or cone apex within the import tolerance). Other uses fail explicitly. Export writes the actual `VERTEX_LOOP` and `VERTEX_POINT`; it never creates a zero-radius circle, epsilon line, or synthetic edge. Display and mass consumers treat the zero-dimensional boundary as topology rather than as a trim curve.

## Export policy boundary

Pcurve encoding and vertex-loop serialization are deterministic. JudgmentEngine remains restricted to the existing bounded choice among admissible face-boundary serialization policies: complete explicit boundary roles, authored-order fallback when roles are absent, or rejection of partial/contradictory evidence. It does not decide equivalence, closure, winding, seam side, or whether a vertex loop exists.
