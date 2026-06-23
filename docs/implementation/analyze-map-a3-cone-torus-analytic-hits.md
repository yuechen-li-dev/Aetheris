# ANALYZE-MAP-A3 Cone/Torus Analytic Ray Hits

ANALYZE-MAP-A3 extends the `aetheris analyze map` analytic-first ray backend from planes, cylinders, and spheres to conical and toroidal surfaces. The goal is to keep LLM-facing spatial measurements independent from display tessellation whenever the imported analytic surface is sufficient.

## Surfaces now analytic

- plane
- cylinder
- sphere
- cone
- torus

## Cone implementation summary

Cone hits use the imported `ConeSurface` representation: apex, axis, semi-angle, placement radius/origin, and reference axis. For each ray `P(t) = O + tD`, the implementation solves the infinite right-circular cone quadratic in apex/axis coordinates, filters nonnegative roots, filters roots to the face axial span derived from bounded face vertices, sorts hits by ray parameter, and computes normals from the radial direction minus the cone slope component.

Simple full conical/frustum faces with reliable vertex-derived axial bounds report:

```json
{
  "surfaceFamily": "cone",
  "intersectionMode": "analytic",
  "confidence": "exact",
  "diagnostics": []
}
```

If a future conical face cannot provide a reliable bounded span, the analytic candidate is not claimed as exact and the existing explicit tessellated fallback path remains responsible for diagnostics.

## Torus implementation summary

Torus hits use the imported `TorusSurface` center, axis, orthonormal local axes, major radius, and minor radius. The ray is evaluated in torus-local coordinates against the standard implicit equation:

```text
(x² + y² + z² + R² - r²)² - 4R²(x² + y²) = 0
```

A bounding sphere first limits the candidate interval. The current CLI path then isolates real roots numerically over that bounded interval with fixed sampling plus bisection on sign changes, using tolerances around `1e-9` for root detection and `1e-7` for duplicate suppression. This avoids depending on the torus display mesh for simple full-torus truth-pass probes.

Normals are computed from the gradient of the implicit torus equation and transformed back to model coordinates.

## Containment policy

- Cone: exact only when the conical face has a reliable finite axial span from its bounded topology. Angular trim handling is intentionally not generalized in A3.
- Torus: exact for the current single-face full-torus primitive/import shape. Trimmed torus patches are not claimed as exact by this milestone and may continue through explicit fallback diagnostics.
- Tessellated hits are not duplicated for faces that already produced analytic hits.

## Torus truth-pass tests

A3 adds focused CLI tests using a generated torus with major radius `3` and minor radius `1`:

- center-hole ray: a ray through the torus hole returns no torus hit and no tessellated false positive;
- ring ray: a ray through the ring returns an analytic torus hit with exact confidence;
- outside ray: a ray outside `major + minor` misses.

These tests specifically guard against display tessellation making the donut goblin lie to the LLM.

## Fixture paths

The focused tests generate STEP fixtures through existing primitive export paths at test runtime:

- cone/frustum: generated from `BrepRevolve.Create` in `Aetheris.CLI.Tests`;
- torus: generated from `BrepPrimitives.CreateTorus(3, 1)` in `Aetheris.CLI.Tests`.

The ruled/linear-extrusion fallback regression continues to use:

```text
testdata/step242/generated/ruled-a2/ellipse-linear-extrusion-production.step
```

## Surfaces still fallback

- linear-extrusion
- surface-of-revolution
- B-spline

## Limitations and non-scope

- Trimmed torus patches may still fallback.
- The torus solver is a bounded numeric isolation strategy with documented tolerances rather than a symbolic quartic solver.
- No volume integration behavior is changed.
- No linear-extrusion, surface-of-revolution, or B-spline analytic hits are added.
- No DisplayIR/frontend changes or new modeling features are included.
