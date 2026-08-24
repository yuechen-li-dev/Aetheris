# Wireframe SVG previews

`aetheris wireframe` renders a deterministic SVG inspection preview from an imported STEP BRep:

```powershell
aetheris wireframe model.step --out model.wireframe.svg --view iso --density 8 --json
```

Available views are `iso`, `front`, `top`, and `right`. `--density` controls the number of interior constant-parameter lines per face from `2` through `32`; `--samples` controls curve sampling from `8` through `256`.

The renderer draws two kinds of evidence:

- bright boundary lines sampled from the BRep's authoritative bound 3D edge curves;
- translucent constant-`u` and constant-`v` lines evaluated on exact face supports and clipped against face-local pcurve loops with the even/odd trim rule.

STEP imports that do not retain usable pcurve bindings are passed through Aetheris's bounded pcurve recovery before rendering. JSON reports the view, density, topology counts, face coverage, isoline/boundary polyline counts, surface-family inventory, pcurve recovery result, unsupported families, and deterministic SVG SHA-256.

The current exact-support evaluator covers planes, cylinders, cones, spheres, tori, linear extrusions, surfaces of revolution, and non-rational B-spline surfaces. A face without usable trim loops remains visible through its topology edges but does not receive speculative interior isolines.

This output is a diagnostic visualization. It is not a tessellated product representation, hidden-line-removed engineering drawing, shaded render, or substitute for STEP/BRep validation. All lines remain visible through the model, deliberately producing the technical “x-ray wireframe” appearance.
