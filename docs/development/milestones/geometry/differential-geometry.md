# Differential geometry

M2 adds a public, bounded second-order layer in `Aetheris.Geometry`. It is geometry infrastructure, not Surfacing or topology code.

## Jets and capability

`CurveJet1` contains point and raw first derivative. `CurveJet2` contains point, raw first derivative, raw second derivative, and singularity. `SurfaceDifferential` is the patch first jet; `PatchJet2` adds `Duu`, `Duv`, and `Dvv`. Raw derivatives are retained because normalization would discard parameter-speed information needed by later queries.

`BoundedParametricCurve3.SupportsSecondJet` and `BoundedParametricPatch3.SupportsSecondJet` distinguish capability from representation. Expression geometry differentiates `+`, `-`, `*`, `/`, integer powers, sine, and cosine through second order. The patch AD value carries one mixed partial, so users do not author both `Puv` and `Pvu`. Lines, circles/arcs, ellipses, hyperbolas, non-rational B-splines, and ruled patches with supported boundary curves expose analytic/double-evaluated second jets. A first-jet-only procedural evaluator remains first-jet-only.

Division by zero, a singular negative power, and non-finite evaluation are not accepted as useful derivatives. Direct evaluation throws a typed arithmetic failure; evidence-producing curvature queries translate these cases to `Unknown`.

## Curve curvature

For regular curves, `CurvatureQuery.Curve` evaluates:

```text
k = |P' × P''| / |P'|³
```

It returns curvature, radius, evidence, status, singularity, and diagnostic. A straight line has zero curvature and infinite radius. A tiny or non-finite first derivative returns `Unknown` rather than a large quotient.

## Patch curvature

For a regular patch, the oriented normal is `normalize(Du × Dv)`. The first form is `(E,F,G) = (Du·Du, Du·Dv, Dv·Dv)` and the second is `(e,f,g) = (N·Duu, N·Duv, N·Dvv)`. `CurvatureQuery.Patch` reports Gaussian curvature, mean curvature, principal curvatures ordered `K1 >= K2`, and principal directions when the eigen-directions are stable. At an umbilic the curvature values can remain available while directions are indeterminate.

`NormalCurvature` accepts a geometric tangent vector, resolves its coordinates through the metric, and evaluates the second-form quotient. It does not compare raw parameter derivatives.

## Evidence and conditioning

`DifferentialPolicy` centralizes minimum tangent/normal magnitudes, metric determinant and condition limits, and curvature tolerance. A successful finite floating-point differential calculation is `ToleranceBounded`; it is not `Certified`. Panel seam sampling is always `Sampled`. `Unknown` is expected for unavailable jets, singular parameterizations, ill-conditioned metrics, or insufficient seam correspondence.

Differential evidence changes no topology and proves no general contact order. The local parabola fixture exposes a stationary scalar component and positive second derivative as observations only.
