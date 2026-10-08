# Wireframe SVG previews

`aetheris wireframe` renders a deterministic SVG inspection preview from an imported STEP BRep:

```powershell
aetheris wireframe model.step --out model.wireframe.svg --view iso --density 8 --json
```

Available views are `iso`, `iso-z` (Z-up isometric), `front`, `top`, and `right`. `--density` controls the number of interior constant-parameter lines per face from `2` through `32`; `--samples` controls curve sampling from `8` through `256`.

The renderer draws two kinds of evidence:

- bright boundary lines sampled from the BRep's authoritative bound 3D edge curves;
- translucent constant-`u` and constant-`v` lines evaluated on exact face supports and clipped against face-local pcurve loops with the even/odd trim rule.

STEP imports that do not retain usable pcurve bindings are passed through Aetheris's bounded pcurve recovery before rendering. JSON reports the view, density, topology counts, face coverage, isoline/boundary polyline counts, surface-family inventory, pcurve recovery result, unsupported families, and deterministic SVG SHA-256.

The current exact-support evaluator covers planes, cylinders, cones, spheres, tori, linear extrusions, surfaces of revolution, and non-rational B-spline surfaces. A face without usable trim loops remains visible through its topology edges but does not receive speculative interior isolines.

This output is a diagnostic visualization. It is not a tessellated product representation, hidden-line-removed engineering drawing, shaded render, or substitute for STEP/BRep validation. All lines remain visible through the model, deliberately producing the technical “x-ray wireframe” appearance.

Native asset generators can use `BrepWireframeOptions` to set dimensions, monochrome colors, stroke widths, centered framing and label visibility. `BrepWireframeSvgRenderer.RenderEdges` uses the same view/framing projection for already sampled topology polylines. Its caller owns occurrence placement and provenance; it does not infer surface isolines or topology from triangle meshes. Scene rectangular panels and window pieces publish their twelve boundary edges alongside display faces.
