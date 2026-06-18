using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242RationalBSplineCurveRecoveryTests
{
    [Fact]
    public void RecoversRationalQuadraticCircleAsCircle3()
    {
        var curve = CreateQuarterCircleSpline(System.Math.Sqrt(0.5d));
        var decision = Step242BsplineCurveRecoveryLane.Decide(CreateRationalCurveEntity(), curve, [1d, System.Math.Sqrt(0.5d), 1d]);

        Assert.Equal(Step242BsplineCurveRecoveryLane.AnalyticCircleCandidate, decision.CandidateName);
        Assert.NotNull(decision.RecoveredCurve);
        Assert.Equal(CurveGeometryKind.Circle3, decision.RecoveredCurve!.Kind);
        Assert.Equal(1d, decision.RecoveredCurve.Circle3!.Value.Radius, 6);
        AssertNear(Point3D.Origin, decision.RecoveredCurve.Circle3.Value.Center, 1e-6d);
    }

    [Fact]
    public void DoesNotIgnoreRationalWeights()
    {
        const double weight = 0.939858145324778d;
        var angle = 2d * System.Math.Acos(weight);
        var curve = new BSpline3Curve(
            2,
            [new Point3D(1d, 0d, 0d), new Point3D(1d, System.Math.Tan(angle / 2d), 0d), new Point3D(System.Math.Cos(angle), System.Math.Sin(angle), 0d)],
            [3, 3],
            [0d, 1d],
            "CIRCULAR_ARC",
            false,
            false,
            "UNSPECIFIED");

        var rationalMid = Step242BsplineCurveRecoveryLane.EvaluateRational(curve, [1d, weight, 1d], 0.5d);
        var unweightedMid = curve.Evaluate(0.5d);
        Assert.NotNull(rationalMid);
        Assert.InRange(System.Math.Abs((rationalMid.Value - Point3D.Origin).Length - 1d), 0d, 1e-12d);
        Assert.True(System.Math.Abs((unweightedMid - Point3D.Origin).Length - 1d) > 1e-3d);

        var decision = Step242BsplineCurveRecoveryLane.Decide(CreateRationalCurveEntity(), curve, [1d, weight, 1d]);
        Assert.Equal(CurveGeometryKind.Circle3, decision.RecoveredCurve!.Kind);
    }

    [Fact]
    public void RejectsRationalNonCircle()
    {
        var curve = new BSpline3Curve(
            2,
            [new Point3D(0d, 0d, 0d), new Point3D(0.3d, 0.9d, 0.2d), new Point3D(1d, 0d, 0d), new Point3D(1.5d, 0.4d, 0.6d)],
            [3, 1, 3],
            [0d, 0.5d, 1d],
            "UNSPECIFIED",
            false,
            false,
            "UNSPECIFIED");

        var decision = Step242BsplineCurveRecoveryLane.Decide(CreateRationalCurveEntity(), curve, [1d, 0.8d, 1.2d, 1d]);

        Assert.Equal(Step242BsplineCurveRecoveryLane.RejectCandidate, decision.CandidateName);
        Assert.Null(decision.RecoveredCurve);
        Assert.Contains("step242-rational-bspline-circle", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeavesNonRationalBSplineUnchanged()
    {
        var curve = CreateQuarterCircleSpline(System.Math.Sqrt(0.5d));
        var decision = Step242BsplineCurveRecoveryLane.Decide(CreateNonRationalCurveEntity(), curve, [1d, System.Math.Sqrt(0.5d), 1d]);

        Assert.Equal(Step242BsplineCurveRecoveryLane.RejectCandidate, decision.CandidateName);
        Assert.Null(decision.RecoveredCurve);
        Assert.Contains("not a rational", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlainCircleImportStillWorks()
    {
        var text = """
ISO-10303-21;
HEADER;
ENDSEC;
DATA;
#1=CARTESIAN_POINT('',(0.,0.,0.));
#2=DIRECTION('',(0.,0.,1.));
#3=DIRECTION('',(1.,0.,0.));
#4=AXIS2_PLACEMENT_3D('',#1,#2,#3);
#5=CIRCLE('',#4,2.5);
ENDSEC;
END-ISO-10303-21;
""";
        var parse = Step242SubsetParser.Parse(text);
        Assert.True(parse.IsSuccess);
        var circle = Step242SubsetDecoder.ReadCircleCurve(parse.Value, parse.Value.TryGetEntity(5).Value);
        Assert.True(circle.IsSuccess);
        Assert.Equal(2.5d, circle.Value.Radius, 6);
    }

    private static BSpline3Curve CreateQuarterCircleSpline(double middleWeight) => new(
        2,
        [new Point3D(1d, 0d, 0d), new Point3D(1d, 1d, 0d), new Point3D(0d, 1d, 0d)],
        [3, 3],
        [0d, 1d],
        "CIRCULAR_ARC",
        false,
        false,
        "UNSPECIFIED");

    private static Step242ParsedEntity CreateRationalCurveEntity() => new(
        1,
        new Step242ComplexEntityInstance([
            new Step242EntityConstructor("B_SPLINE_CURVE_WITH_KNOTS", []),
            new Step242EntityConstructor("RATIONAL_B_SPLINE_CURVE", [new Step242ListValue([])])]));

    private static Step242ParsedEntity CreateNonRationalCurveEntity() => new(
        1,
        new Step242SimpleEntityInstance(new Step242EntityConstructor("B_SPLINE_CURVE_WITH_KNOTS", [])));

    private static void AssertNear(Point3D expected, Point3D actual, double tolerance)
    {
        Assert.InRange((actual - expected).Length, 0d, tolerance);
    }
}
