# Flat-pattern validation

| Fixture | Status | Envelope (mm) | Bend lines | Cuts | Planar overlap | Hash |
|---|---|---:|---:|---:|---|---|
| Authored U-channel, K=.42 | Valid | 146.262388679 × 60 | 2 | 2 circular | none | `a5ce4b0368a7…` |
| NIST CTC-03, K=.5 | Partial | 404.754237721 × 625.305580354 | 7 | 2 profile/slot | none | `2a7809249922…` |

Validation checks finite coordinates, minimum closed-loop cardinality, owning-region feature containment, positive-area convex planar overlap (shared boundaries are allowed), bend-line correspondence, and deterministic ordering/hash. Cylindrical regions lower to rectangles whose width is `angle × (inside radius + K × thickness)`; this is manufacturing neutral-axis development, not projection of either physical cylinder skin.

The CTC-03 SVG was raster-rendered in a headless browser and visually inspected: the dominant central panel, seven unfolded bend strips/lines, attached planar regions, and two red opening contours are visible. Bend text is isolated in a `stroke="none"` label layer with collision offsets and a translucent knockout, preventing the inherited-stroke glyph distortion that was found during review. Axis-length lookup is geometry-based, so bend lines cannot silently collapse through an ID mismatch.

The CTC-03 flat STEP re-imports as one enclosed manifold: one body, one shell, 62 planar faces, 180 edges, 120 vertices, bounds `(10,10,0)`–`(414.754237721,635.305580354,1.90754)` mm. The 10 mm XY translation avoids signed-zero topology buckets and does not alter the manufactured shape or dimensions.

The imported outer boundary is currently a deterministic set of per-region convex analytic contours plus bend strips, not one exact stitched production/nesting loop. Consequently CTC-03 remains `Partial` even though the recovered bend component flattens without overlap.
