# RULED-TOOLING-A0 Inline STEP probe harness

RULED-ANALYZE-A0 updated analyze/reporting so exact swept surface kinds are recognized in analyzer summaries. `SURFACE_OF_LINEAR_EXTRUSION` imports now appear as `linear-extrusion`, and `SURFACE_OF_REVOLUTION` imports now appear as `surface-of-revolution`.

Open ruled/swept surface bodies still do not have exact volume integration. `aetheris analyze volume` reports that state as unsupported instead of throwing a dictionary-key exception; this milestone did not add ruled-surface area or volume integration.

## Spatial probe follow-up

`aetheris analyze map` can now spatially inspect ruled and swept STEP artifacts as a ray/height-map probe. For example:

```bash
aetheris analyze map testdata/step242/generated/ruled-a2/ellipse-linear-extrusion-production.step --plane xy --direction -z --resolution 8x8 --json
```

Linear-extrusion faces are reported through the tessellated fallback path unless an exact intersection path is added later; the JSON diagnostics make that fallback explicit.
