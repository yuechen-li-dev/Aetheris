# G2 planar plateaus

A `Plateau` modifies the top planar face of one ordinary Profile extrusion. It retains the base shell, trims a hole into the base top face, and adds a transition band and a raised planar center. The contacts and trims share edges; no overlapping solids or cleanup boolean is used.

```firmament
Model GenericG2Plateau {
 Units: mm
 Struct Part {
  Rect2 BaseBoundary { Center: [0mm,0mm] Size: [100mm,80mm] }
  Profile BaseSection { Loop Outer { BaseBoundary |> TraceLoop } }
  Extrude Body { Profile: BaseSection From: 0mm To: 5mm }
  SmoothRoundedRect2 Outline { Center: [0mm,0mm] Size: [60mm,40mm] CornerExtent: 8mm }
  Profile Footprint { Loop Outer { Outline |> TraceLoop } }
  Plateau Raised {
   Base: Body
   Footprint: Footprint
   Height: 3mm
   Width: 2mm
   Inset: Homothetic
   Continuity: G2
  }
 }
}
```

[Runnable example](../../../fixtures/Canonical/Surfacing/g2-planar-plateau.firmament).

The footprint is the **outer/base contact boundary**. `Width` is the inward displacement on its shorter bounding-box half-axis. `Inset: Homothetic` explicitly selects uniform scaling about the footprint bounding-box center: the raised top has scale `1 - Width / min(halfWidth, halfHeight)`. Longer-axis and corner displacements follow that scale. This is an exact polynomial construction, not an approximation to a constant-distance normal offset. Width must be positive and less than the shorter half-axis. Height is measured above the host top face.

The section reuses the qualified quintic fillet quarter twice, with a concave entry and convex exit. Both planar support joins have G2 continuity. Smooth footprint joins retain G2 throughout the band. Sharp corners in a straight-only polygon retain visible G0 creases in the band; support joins still meet their planes with G2 continuity. Source order and span names survive. The seam defaults to the first straight span; `Seam: Bottom` may select another existing span.

X1 admits line and single-span cubic polynomial footprints with provably monotone angular progression around the center. Bernstein control bounds prove regularity, one winding, nonintersection, and nested contacts. Unsupported or unproved footprints fail typed. The host must be one unmodified extrusion with a planar top and one outer trim loop whose boundary consists of lines or convex arcs. Contact control hulls must remain strictly within that face. Existing face/edge IDs outside the new band are retained.

3D contacts retain the footprint degree, knots, parameter direction, and control provenance. Plane pcurves are affine transforms of those controls; band pcurves are exact parameter-domain lines. AP242 writes polynomial pcurves as 2D B-splines. Independent edge-versus-surface checks validate the trims. `aetheris inspect model.firmament --json` and `aetheris build ... --json` expose contacts, patches, support/internal continuity, face identities, pcurve residuals, counts, and timings.

To replace a separate abrupt plateau occurrence, author this operation inside the body Struct, retaining the existing body extrusion and adding the plateau footprint. Remove the old separate plateau occurrence, and keep its height and feature placements consistent with the new top plane. This produces one body with a trimmed surrounding base face. Other assembly occurrences remain separate parts.
