# ANALYZE-MAP-A2 Cylinder/Sphere Analytic Ray Hits

`aetheris analyze map` now extends the analytic-first ray backend from bounded planes to common round analytic surfaces:

- plane
- cylinder
- sphere

The backend policy remains truth-first:

1. analytic exact intersection for supported bounded faces;
2. CIR/evaluable intersection later;
3. tessellated fallback, explicitly marked approximate.

## Cylinder formula

For a ray `P(t) = O + tD` and cylinder support with origin point `C`, unit axis `A`, and radius `r`, the implementation removes the component parallel to the axis:

- `D_perp = D - A * dot(D, A)`
- `O_perp = (O - C) - A * dot(O - C, A)`

It then solves:

`dot(D_perp, D_perp)t² + 2 dot(O_perp, D_perp)t + dot(O_perp, O_perp) - r² = 0`

Nonnegative roots are sorted by ray parameter. For each candidate, the hit point, face id, surface family, ray parameter, and radial normal are emitted.

## Sphere formula

For a ray `P(t) = O + tD` and sphere center `C` with radius `r`, the implementation solves:

`dot(D, D)t² + 2 dot(O - C, D)t + dot(O - C, O - C) - r² = 0`

Nonnegative roots are sorted by ray parameter. The normal is the normalized vector from the sphere center to the hit point.

## Containment policy

A2 only reports `confidence: exact` when the bounded-face containment policy is reliable for the current importer topology:

- Planes continue to use the existing planar face-bound polygon check.
- Cylinders use exact analytic infinite-cylinder math plus the finite axial span resolved from the cylindrical face vertices. The current exact path is intended for the full cylindrical side faces emitted by current primitive and hole fixtures; more complex cylindrical trims should remain on the explicit fallback path until stronger trim containment is added.
- Spheres are reported exact for loopless full spherical faces. Trimmed spherical patches are not promoted to exact by this milestone; they remain eligible for explicit tessellated fallback until exact spherical trim containment is added.

A2 intentionally does not claim exact bounded hits for unsupported trims. Tessellated fallback remains present and is still reported as `intersectionMode: tessellated-fallback` with `confidence: approximate`.

## JSON provenance examples

Exact cylinder/sphere hits use the same provenance fields introduced in A1:

```json
{
  "surfaceFamily": "cylinder",
  "intersectionMode": "analytic",
  "confidence": "exact",
  "diagnostics": []
}
```

Fallback remains explicit:

```json
{
  "surfaceFamily": "linear-extrusion",
  "intersectionMode": "tessellated-fallback",
  "confidence": "approximate",
  "diagnostics": [
    "Exact ray intersection unavailable for linear-extrusion; used tessellated fallback."
  ]
}
```

Top-level summaries continue to count `analyticHitCount`, `cirHitCount`, `tessellatedFallbackHitCount`, and per-sample `intersectionModes`.

## Still fallback / non-scope

A2 does not implement analytic hits for:

- cone
- torus
- linear-extrusion
- surface-of-revolution
- B-spline

It does not change `analyze volume`, DisplayIR/frontend behavior, modeling capabilities, CIR rewriting, volume integration, or feature reconstruction.

## Next planned truth pass

The next analytic map pass is expected to focus on cone and torus ray intersections plus a stronger trim-containment story for more complex bounded analytic patches.
