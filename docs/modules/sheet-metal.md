# Sheet Metal Module design note

`Aetheris.SheetMetal` 0.1.0 is an architecture/design-pressure placeholder, not a Sheet Metal implementation. It depends explicitly on Core and Surfacing and publishes no M0 capability.

The future Module owns developability, neutral surface and thickness, formed/fabricated state, bends and bend allowance, seams/reliefs, and flat-pattern correspondence. Flattening must be based on proved developability and stable boundary/feature provenance, not on tessellated visual similarity.

Panel M0 makes that future seam explicit: `PanelIr` retains boundary provenance, stable directed semantic edges, optional thickness/material metadata, and first-class developability evidence. A future Sheet Metal lowering can therefore consume `Developable Panel + thickness + material` rather than reverse-engineering a mystery BRep. It must reject `NonDevelopable` and treat `Indeterminate` explicitly. M0 does not claim all ruled surfaces are developable, calculate general Gaussian curvature, infer neutral axes, or emit flat patterns.
