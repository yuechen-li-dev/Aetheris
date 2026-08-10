# Sheet Metal Module design note

`Aetheris.SheetMetal` 0.1.0 is an architecture/design-pressure placeholder, not a Sheet Metal implementation. It depends explicitly on Core and Surfacing and publishes no M0 capability.

The future Module owns developability, neutral surface and thickness, formed/fabricated state, bends and bend allowance, seams/reliefs, and flat-pattern correspondence. Flattening must be based on proved developability and stable boundary/feature provenance, not on tessellated visual similarity.

This pressure changed Surfacing M0 in three ways: `RuledSurfaceIr` retains boundary provenance; ruled construction identity survives exact lowering; and developability evidence has a first-class preservation flag. A future Sheet Metal lowering can distinguish an admitted developable ruled transition from a merely visually similar spline. M0 does not claim all ruled surfaces are developable, calculate Gaussian curvature, infer neutral axes, or emit flat patterns.
