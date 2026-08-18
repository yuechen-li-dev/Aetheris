# Bounded parametric curves

`Aetheris.Geometry.BoundedParametricCurve3` is the public, domain-neutral contract for authored one-parameter geometry in three dimensions. It is the curve sibling of `BoundedParametricPatch3`; it is not necessarily a B-rep edge.

## Contract

Every curve has a stable `GeometryIdentity`, finite increasing `ParameterDomain1`, `GeometryProvenance`, and `GeometryRepresentationKind`. `Evaluate(t)` returns `P(t)`. `EvaluateJet1(t)` returns the raw derivative `dP/dt`, an optional normalized `UnitTangent`, and an explicit `DifferentialSingularityKind`. A zero derivative is `Singular`, not an unexplained null tangent. Out-of-domain and non-finite evaluations are rejected.

Native adapters cover bounded trims of Kernel.Core `Line3`, `Circle3`, `Ellipse3`, `Hyperbola3`, and non-rational `BSpline3`. Line segments retain distance parameterization. Circle and ellipse trims use radians and report `IsPeriodic` only for a complete `2*pi` domain. The endpoints of that domain are the same locus but remain two legal seam parameters. A reversed trim maps the increasing public domain to decreasing native parameters, so derivatives and unit tangents follow authored edge orientation.

```csharp
var segment = BoundedParametricCurve3.LineSegment(
    "guide", new Point3D(0, 0, 0), new Point3D(20, 0, 0),
    "feature:guide", semanticOwner: "feature");
var midpoint = segment.Evaluate(10);
var jet = segment.EvaluateJet1(10);
```

## Expression-backed curves

`CurvePointExpression` uses the same unit-aware scalar expression and forward automatic-differentiation engine as patches. `CurveExpression.T` is initially dimensionless. All three coordinates must have Length dimension; trigonometric arguments must be dimensionless. Canonical parabola, sinusoid, and helix fixtures are covered by the public API tests and persisted evidence.

```csharp
var t = CurveExpression.T;
var mm = CurveExpression.Length(1);
var helix = new BoundedParametricCurve3("helix", new(0, 2 * double.Pi),
    new CurvePointExpression(
        CurveExpression.Multiply(mm, CurveExpression.Cos(t)),
        CurveExpression.Multiply(mm, CurveExpression.Sin(t)),
        CurveExpression.Multiply(mm, t)),
    "scientific:calibration");
```

No Firmament `ParametricCurve` syntax was added in M1. Existing Profiles, paths, Panel boundaries, Surfacing expressions, and PipeRoute already own useful authoring intent; a general textual curve would add syntax without an immediate materialization contract. The public expression API is the future language seam.

## CAD adapters and authority

`PanelEdgeIr.AuthoredCurve` exposes the exact semantic edge direction and owner without duplicating its Kernel.Core support. `PipeRouteIr.CenterlineCurves` exposes stable ordered line–arc–line pieces while `PipeRouteIr` remains the route-intent authority. Constructive bounded hyperbolas can be adapted without becoming a generic intersection or trimming API.

Representation and predicate evidence remain independent. An analytic representation does not imply that a query result is certified. M1 does not add generic closest-point, distance, intersection, contact, NURBS, second-jet, or curvature behavior, and it never promotes numerical intersections into semantic topology.
