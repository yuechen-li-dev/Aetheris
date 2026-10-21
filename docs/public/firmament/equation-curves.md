# Equation-driven curves

A Function defines a pure mathematical relationship. Curve2 or Curve3 binds that
relationship to a finite parameter interval and a geometric representation.
Neither declaration creates material. Profiles and other admitted consumers own
material construction. There is no separate Equation keyword or implicit solver.

## Reusable typed Functions

Functions can return scalars, Point2, Point3 or a declared Record. Parameters can
use the same types. Records must be composed from these supported scalar/point/Record types. Record fields come from the ordinary Record schema; a result
must supply every field once, with no unknown fields. Functions use explicit
arguments, not implicit capture of surrounding Statics. Dependencies must be
acyclic. Record and point member access works on Function results.

```firmament
Record ArchSpec { span: Length; rise: Length }
Function Arch(t: Float, p: ArchSpec) -> Point2 =
    Point2(p.span * t, 4 * p.rise * t * (1 - t));
```

A Record result can bundle a point and related dimensions. This is explicit
computation, not a simultaneous system of constraints. Scalar, point and Record
results use the existing Function binder and shared numeric expression evaluator.
Manufacturing tolerances cannot be silently erased into equation coordinates.

## Trigonometric units

`Sin` and `Cos` return dimensionless Float values. A bare number means radians;
`rad` explicitly means radians; `deg` explicitly means degrees:

```firmament
Function RadianCos(x: Float) -> Float = Cos(x);
Function AngleCos(a: Angle) -> Float = Cos(a);
// Cos(3.141592653589793), Cos(3.141592653589793rad),
// and Cos(180deg) evaluate to -1 within floating-point precision.
```

Lengths are invalid trigonometric arguments. No conversion is guessed from the
magnitude of the number. Sin/Cos are shared scalar intrinsics, not curve-only math.

## An exact planar arch

The complete [arch fixture](../../../fixtures/Canonical/EquationCurves/arch.firmament)
contains a curved crown, an explicit straight baseline, a Profile and an Extrude.
Its defining declarations are:

```firmament
Function Arch(t: Float, span: Length, rise: Length) -> Point2 =
    Point2(span * t, 4 * rise * t * (1 - t));

// Inside Concept Struct Layout:
Curve2 Crown {
    parameter: t;
    domain: [0, 1];
    from: Arch(t, 80mm, 20mm);
    on: XZ;
    representation: Exact;
}
```

Use qualified references such as `Layout.Crown` in Profile pipelines. An equation
curve carries local Length coordinates and its explicit plane. A consuming
Profile without a placement inherits the common curve plane. An explicitly
placed Profile must agree with it. Mixing planes fails. XY, XZ, YZ and named
Concept planes are supported. Curve2 results remain Point2; querying one never
silently lifts it into Point3.

A pipeline can trace open equation curves alongside other named guides.
`Close` checks that the declared segments close; it does not add a missing edge.
`TraceLoop` requires an explicitly closed equation chain or a recognized full
circle/ellipse. `Reverse` reverses an open chain. Complete circles/ellipses use `TraceLoop`;
open conic trimming is a separate consumer capability.

## Spatial laws and parameter queries

```firmament
Function Helix(t: Float, radius: Length, pitch: Length) -> Point3 =
    Point3(radius * Cos(t * 360deg), radius * Sin(t * 360deg), pitch * t);

// Inside a plain Concept Struct Layout:
Curve3 Coil {
    parameter: t;
    domain: [0, 2];
    from: Helix(t, 12mm, 8mm);
    on: World;
    representation: Approximate;
    maxDeviation: 0.01mm;
}
```

`Layout.Coil.At(1)` returns the equation's Point3 at parameter 1, not a point
interpolated from its approximating polyline. `.X`, `.Y` and `.Z` can project
coordinates. Calls outside the declared domain fail; no clamping or wrapping
occurs. Calls work in keyed Points/Series recipes and ordinary geometry inputs.
The [helix fixture](../../../fixtures/Canonical/EquationCurves/helix.firmament)
uses its keyed turn heights to drive a real Box dimension.

The parameter is dimensionless and is not arc length. Equal parameter increments
do not promise equal physical spacing. Curve3 currently uses World in the owning
definition's coordinates; assembly occurrence placement remains separate. Other
Curve3 frames reject explicitly. General spatial curve sweeps are not admitted by
this change: existing WireRoute/SectionChain capabilities remain their owners.

## Exactness and approximation

`Exact` is the default and rejects `maxDeviation`. Admitted exact laws are
polynomials of degree at most three, lowered by coefficient proof to lines/cubic
Bezier geometry, and recognized axis-aligned full circles/ellipses in Curve2.
Conic recognition requires matching affine Sin/Cos arguments over exactly one
turn and positive radii. Other exact requests fail with an explanation.

`Approximate` requires a positive Length `maxDeviation`. The first representation
is a polyline. Whole-interval derivative bounds establish its geometric deviation;
endpoint evaluation uncertainty is included. Sampling alone does not establish
that bound. Unsupported analysis, singularities, non-finite results or exhausted
budgets reject the declaration. This is a representation error budget, not a
manufacturing tolerance, and is not projected as PMI.

`closed: true` requires coincident endpoints and inserts no segment. `periodic:
true` additionally requires matching first derivatives; the initial periodicity
proof admits polynomial laws and recognized exact full conics. More general
periodicity requests reject. No higher-order smoothness is implied.

Compilation is bounded to 128 equation curves and 24 approximation subdivision
levels. Approximation uses a 4,096-segment admission budget, including segments
already admitted from preceding curves. Existing Function expression/work/dependency
limits also apply. Approximation supports arithmetic, Sin/Cos and bounded integer
Pow exponents; other evaluable scalar laws can still be rejected when a geometric
error bound cannot be established.

`aetheris inspect file.firmament --json` reports `equationCurves`, including stable
names, original declaration spans, authored equations, parameter domains, frames,
representation, local segment data and error bounds. Exact BRep export of a
polyline approximant does not make that polyline an exact equation curve.
