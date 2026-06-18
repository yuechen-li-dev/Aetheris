# STEP AP242 rational B-spline circle recovery

## Bug summary

Some STEP producers encode exact circular trim curves as complex B-spline entities that combine `B_SPLINE_CURVE_WITH_KNOTS` with a sibling `RATIONAL_B_SPLINE_CURVE`. The previous curve import path only decoded the non-rational B-spline constructor, so the rational weights were ignored and exact circular arcs could display as visibly bulged or faceted spline trims on toroidal/fillet-like geometry.

## Why weights matter

A rational quadratic B-spline represents a circular arc by weighting its middle control point. Treating the same poles as an ordinary unweighted quadratic changes the evaluated curve and moves sampled points off the intended circle. The STEP importer now reads the sibling `RATIONAL_B_SPLINE_CURVE` weights before allowing the bounded rational quadratic recovery path to decide whether the curve is an analytic circle.

## Recovery policy

Aetheris still does not expose a general `RationalBSpline3`/NURBS curve primitive. The importer instead follows the existing analytic-recovery precedent from `Step242BsplineSurfaceRecoveryLane`: detect the rational STEP encoding, evaluate the rational curve in the recovery lane, fit and verify an analytic primitive, and recover only when the bounded analytic interpretation is admissible.

`Step242BsplineCurveRecoveryLane` currently supports rational quadratic circle/arc recovery. It samples the rational curve over the knot domain, fits a common plane and circle through representative samples, and verifies sampled plane/radius residuals against tolerance before returning `Circle3Curve`.

## Diagnostics and limits

Stable reason strings include:

- `step242-rational-bspline-circle-recovered`
- `step242-rational-bspline-circle-rejected`
- `step242-rational-bspline-weights-missing`
- `step242-rational-bspline-unsupported-degree`
- `step242-rational-bspline-circle-fit-residual-exceeded`

Known limits:

- No public/kernel rational NURBS curve representation is added.
- Non-rational B-spline import remains on the existing `BSpline3Curve` path.
- Plain STEP `CIRCLE` import remains unchanged.
- STEP export behavior is unchanged.
- Firmament V2, AIR Region behavior, and broad BRep topology behavior are not changed.
