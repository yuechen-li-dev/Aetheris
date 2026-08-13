using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Piping;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class ParametricCurveTests
{
    private static readonly Direction3D X = Direction3D.Create(new(1, 0, 0));
    private static readonly Direction3D Z = Direction3D.Create(new(0, 0, 1));

    [Fact]
    public void DomainIsFiniteOrderedInclusiveAndDeterministic()
    {
        Assert.Throws<GeometryDefinitionException>(() => new ParameterDomain1(1, 1));
        Assert.Throws<GeometryDefinitionException>(() => new ParameterDomain1(double.NegativeInfinity, 1));
        var domain = new ParameterDomain1(-2, 3);
        Assert.True(domain.Contains(-2)); Assert.True(domain.Contains(3)); Assert.False(domain.Contains(4));
        Assert.Equal(domain, new ParameterDomain1(-2, 3));
        var curve = BoundedParametricCurve3.LineSegment("domain-line", new(0, 0, 0), new(1, 0, 0), "fixture");
        Assert.Throws<ArgumentOutOfRangeException>(() => curve.Evaluate(2));
    }

    [Fact]
    public void ExactKernelFamiliesExposePointsDerivativesTangentsAndDomains()
    {
        var line = Adapt("line", CurveGeometry.FromLine(new(new(1, 2, 3), X)), 0, 4);
        AssertPoint(line.Evaluate(4), 5, 2, 3); AssertVector(line.EvaluateJet1(2).Derivative, 1, 0, 0);

        var circle = Adapt("circle", CurveGeometry.FromCircle(new(Point3D.Origin, Z, 2, X)), 0, 2 * double.Pi);
        Assert.True(circle.IsPeriodic); AssertPoint(circle.Evaluate(0), 2, 0, 0); AssertVector(circle.EvaluateJet1(0).Derivative, 0, 2, 0);

        var ellipse = Adapt("ellipse", CurveGeometry.FromEllipse(new(Point3D.Origin, Z, 3, 2, X)), 0, double.Pi / 2);
        AssertPoint(ellipse.Evaluate(double.Pi / 2), 0, 2, 0); AssertVector(ellipse.EvaluateJet1(0).Derivative, 0, 2, 0);

        var hyperbola = Adapt("hyperbola", CurveGeometry.FromHyperbola(new(Point3D.Origin, Z, X, 2, 3, HyperbolaBranch.PositiveAxisU)), -1, 1);
        AssertPoint(hyperbola.Evaluate(0), 2, 0, 0); AssertVector(hyperbola.EvaluateJet1(0).Derivative, 0, 3, 0);

        var splineSupport = new BSpline3Curve(1, [new(0, 0, 0), new(2, 4, 0)], [2, 2], [0, 1], "POLYLINE_FORM", false, false, "UNIFORM_KNOTS");
        var spline = Adapt("spline", CurveGeometry.FromBSpline(splineSupport), splineSupport.DomainStart, splineSupport.DomainEnd);
        Assert.Equal(1, spline.Degree); AssertPoint(spline.Evaluate(.5), 1, 2, 0); AssertVector(spline.EvaluateJet1(.5).Derivative, 2, 4, 0);
        Assert.All(new[] { line, circle, ellipse, hyperbola, spline }, curve => Assert.Equal(DifferentialSingularityKind.Regular, curve.EvaluateJet1(curve.Domain.Minimum).Singularity));
    }

    [Fact]
    public void ReversedNativeTrimPreservesAuthoredOrientationWithoutChangingSupportIdentity()
    {
        var reversed = Adapt("reverse", CurveGeometry.FromLine(new(Point3D.Origin, X)), 5, 2);
        Assert.Equal(new ParameterDomain1(2, 5), reversed.Domain);
        AssertPoint(reversed.Evaluate(2), 5, 0, 0); AssertPoint(reversed.Evaluate(5), 2, 0, 0);
        AssertVector(reversed.EvaluateJet1(3).Derivative, -1, 0, 0);
    }

    [Fact]
    public void ExpressionsAutomaticallyDifferentiateParabolaSinusoidAndHelix()
    {
        var t = CurveExpression.T; var one = CurveExpression.Length(1);
        var parabola = Expression("parabola", new(CurveExpression.Multiply(one, t), CurveExpression.Multiply(one, CurveExpression.Power(t, 2)), CurveExpression.Length(0)), -2, 2);
        AssertPoint(parabola.Evaluate(1.5), 1.5, 2.25, 0); AssertVector(parabola.EvaluateJet1(1.5).Derivative, 1, 3, 0);
        var sinusoid = Expression("sinusoid", new(CurveExpression.Multiply(one, t), CurveExpression.Multiply(one, CurveExpression.Sin(t)), CurveExpression.Length(0)), -double.Pi, double.Pi);
        AssertVector(sinusoid.EvaluateJet1(0).Derivative, 1, 1, 0);
        var helix = Expression("helix", new(CurveExpression.Multiply(one, CurveExpression.Cos(t)), CurveExpression.Multiply(one, CurveExpression.Sin(t)), CurveExpression.Multiply(one, t)), 0, 2 * double.Pi);
        AssertPoint(helix.Evaluate(double.Pi / 2), 0, 1, double.Pi / 2); AssertVector(helix.EvaluateJet1(0).Derivative, 0, 1, 1);
        Assert.Throws<ArgumentException>(() => new BoundedParametricCurve3("bad-units", new(0, 1), new(t, one, one), "fixture"));
        var division = Expression("non-finite", new(CurveExpression.Divide(one, t), one, one), -1, 1);
        Assert.Throws<DivideByZeroException>(() => division.Evaluate(0));
    }

    [Fact]
    public void ZeroDerivativeHasExplicitSingularityStateAndNoTangent()
    {
        var curve = BoundedParametricCurve3.Procedural("singular", new(0, 1), t => (new(t, 0, 0), new(0, 0, 0)), "fixture");
        var jet = curve.EvaluateJet1(.5);
        Assert.Equal(DifferentialSingularityKind.Singular, jet.Singularity); Assert.Null(jet.UnitTangent);
    }

    [Fact]
    public void PanelEdgeAndPipeRouteUseTheSameDirectedPublicCurveLayer()
    {
        var panelResult = PanelFactory.FromRuled(RuledCanopyTemplate.Create("curve-panel", 20, 10, 2));
        Assert.True(panelResult.IsSuccess);
        var north = panelResult.Panel!["North"];
        AssertPoint(north.AuthoredCurve.Evaluate(north.AuthoredCurve.Domain.Minimum), north.Start.X, north.Start.Y, north.Start.Z);
        Assert.NotNull(north.AuthoredCurve.EvaluateJet1(north.AuthoredCurve.Domain.Minimum).UnitTangent);
        Assert.Equal(north.StableId, north.AuthoredCurve.Provenance.SemanticOwner);

        var routeResult = PipeRouteLowering.Lower(StandardPipeElbowTemplate.Create("route", 4, 10, 8));
        Assert.True(routeResult.IsSuccess);
        var pieces = routeResult.Ir!.CenterlineCurves;
        Assert.Equal(["Line3", "Circle3", "Line3"], pieces.Select(piece => piece.NativeFamily));
        Assert.Equal(routeResult.Ir.Elements.Select(element => element.StableId), pieces.Select(piece => piece.StableId));
        AssertPoint(pieces[0].Evaluate(pieces[0].Domain.Minimum), 0, 0, 0);
        AssertPoint(pieces[1].Evaluate(pieces[1].Domain.Maximum), 18, 8, 0);
        AssertPoint(pieces[2].Evaluate(pieces[2].Domain.Maximum), 18, 18, 0);
    }

    [Fact]
    public void ConstructiveConeSectionHyperbolaKeepsItsBoundedNonAuthoringRole()
    {
        var cone = new ConeSurface(Point3D.Origin, X, double.Pi / 4, Z);
        var construction = TransverseConePlaneIntersection.IntersectWorldZ(cone, 2);
        Assert.True(construction.IsSuccess);
        var bounded = Adapt("construction-hyperbola", CurveGeometry.FromHyperbola(construction.Value), -.5, .5);
        Assert.Equal("Hyperbola3", bounded.NativeFamily); Assert.Equal("construction:cone-world-z", bounded.Provenance.Source);
        Assert.Equal(DifferentialSingularityKind.Regular, bounded.EvaluateJet1(0).Singularity);
    }

    [Fact]
    public void FixedCurveInputsSerializeDeterministically()
    {
        var curve = BoundedParametricCurve3.LineSegment("stable", new(0, 0, 0), new(2, 0, 0), "fixture", "owner");
        var first = JsonSerializer.Serialize(new { curve.Identity, curve.Domain, curve.Provenance, Jet = curve.EvaluateJet1(1) });
        var second = JsonSerializer.Serialize(new { curve.Identity, curve.Domain, curve.Provenance, Jet = curve.EvaluateJet1(1) });
        Assert.Equal(first, second);
    }

    private static BoundedParametricCurve3 Adapt(string id, CurveGeometry curve, double start, double end) =>
        BoundedParametricCurve3.FromCurveGeometry(id, curve, start, end, id == "construction-hyperbola" ? "construction:cone-world-z" : "fixture:" + id);
    private static BoundedParametricCurve3 Expression(string id, CurvePointExpression expression, double start, double end) => new(id, new(start, end), expression, "fixture:" + id);
    private static void AssertPoint(Point3D actual, double x, double y, double z) { Assert.Equal(x, actual.X, 10); Assert.Equal(y, actual.Y, 10); Assert.Equal(z, actual.Z, 10); }
    private static void AssertVector(Vector3D actual, double x, double y, double z) { Assert.Equal(x, actual.X, 10); Assert.Equal(y, actual.Y, 10); Assert.Equal(z, actual.Z, 10); }
}
