using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class CylinderCylinderIntersectionCurveTests
{
    private readonly ITestOutputHelper _output;

    public CylinderCylinderIntersectionCurveTests(ITestOutputHelper output) => _output = output;

    private static readonly Direction3D X = Direction3D.Create(new Vector3D(1, 0, 0));
    private static readonly Direction3D Y = Direction3D.Create(new Vector3D(0, 1, 0));
    private static readonly Direction3D Z = Direction3D.Create(new Vector3D(0, 0, 1));

    [Theory]
    [InlineData(CylinderIntersectionBranch.PositiveCutterAxis)]
    [InlineData(CylinderIntersectionBranch.NegativeCutterAxis)]
    public void ExactAuthority_SatisfiesBothCylindersAndBothPcurves(CylinderIntersectionBranch branch)
    {
        var intersection = Create(0.79375, 0.5953125, branch);
        var priorHostU = intersection.EvaluateHostPcurve(0).U;
        for (var i = 0; i <= 1024; i++)
        {
            var t = 2d * double.Pi * i / 1024d;
            var point = intersection.Evaluate(t);
            var hostUv = intersection.EvaluateHostPcurve(t);
            var toolUv = intersection.EvaluateToolPcurve(t);
            Assert.True(double.Abs(point.X * point.X + point.Y * point.Y - 0.79375d * 0.79375d) < 2e-15);
            Assert.True(double.Abs(point.Y * point.Y + (point.Z - 3d) * (point.Z - 3d) - 0.5953125d * 0.5953125d) < 2e-15);
            Assert.True((intersection.Host.Evaluate(hostUv.U, hostUv.V) - point).Length < 2e-15);
            Assert.True((intersection.Tool.Evaluate(toolUv.U, toolUv.V) - point).Length < 2e-15);
            Assert.True(double.Abs(hostUv.U - priorHostU) < 0.02d);
            Assert.True(double.Abs(toolUv.U - t) < 2e-15);
            priorHostU = hostUv.U;
        }
    }

    [Fact]
    public void Derivative_AgreesWithCenteredDifference()
    {
        var intersection = Create(2d, 0.8d, CylinderIntersectionBranch.PositiveCutterAxis);
        const double h = 1e-6d;
        foreach (var t in new[] { 0.1d, 0.8d, 2d, 3.4d, 5.9d })
        {
            var difference = (intersection.Evaluate(t + h) - intersection.Evaluate(t - h)) / (2d * h);
            Assert.True((difference - intersection.Derivative(t)).Length < 1e-9d);
        }
    }

    [Fact]
    public void RotatedRadialCutter_PreservesBothSurfaceFrames()
    {
        const double angle = 1.1d;
        var radial = Direction3D.Create(new Vector3D(double.Cos(angle), double.Sin(angle), 0d));
        var host = new CylinderSurface(Point3D.Origin, Z, 2d, Y);
        var tool = new CylinderSurface(new Point3D(0, 0, 3), radial, 0.5d, Z);
        var intersection = CylinderCylinderIntersectionCurve.Create(host, tool, new Point3D(0, 0, 3),
            CylinderIntersectionBranch.NegativeCutterAxis, new ParameterInterval(0d, 2d * double.Pi)).Value;
        for (var i = 0; i <= 256; i++)
        {
            var t = 2d * double.Pi * i / 256d;
            var point = intersection.Evaluate(t);
            var hostUv = intersection.EvaluateHostPcurve(t);
            var toolUv = intersection.EvaluateToolPcurve(t);
            Assert.True((host.Evaluate(hostUv.U, hostUv.V) - point).Length < 3e-14d);
            Assert.True((tool.Evaluate(toolUv.U, toolUv.V) - point).Length < 3e-14d);
        }
    }

    [Theory]
    [InlineData(0.1d)]
    [InlineData(0.5953125d)]
    [InlineData(0.75d)]
    public void AdaptiveRealization_BoundsTheWholeCurveAndBothSurfaceResiduals(double cutterRadius)
    {
        var intersection = Create(0.79375d, cutterRadius, CylinderIntersectionBranch.PositiveCutterAxis);
        var realized = CylinderIntersectionCurveRealizer.Realize(intersection, ToleranceContext.Default);
        Assert.True(realized.IsSuccess, string.Join("; ", realized.Diagnostics.Select(d => d.Message)));
        var result = realized.Value;
        var observedMax = 0d;
        var observedHostResidual = 0d;
        var observedToolResidual = 0d;
        Assert.True(result.CertifiedDeviationBoundMm <= ToleranceContext.Default.Linear);
        Assert.Equal(result.SegmentCount * 4, result.Curve.ControlPoints.Count);
        var stored = CurveGeometry.FromCertifiedIntersection(result);
        Assert.Same(result, stored.CertifiedIntersection);
        Assert.Equal(result.Curve, stored.BSpline3!.Value);
        for (var i = 0; i <= 2048; i++)
        {
            var t = 2d * double.Pi * i / 2048d;
            var finite = result.Curve.Evaluate(t);
            var exact = intersection.Evaluate(t);
            observedMax = double.Max(observedMax, (finite - exact).Length);
            observedHostResidual = double.Max(observedHostResidual,
                double.Abs(double.Sqrt(finite.X * finite.X + finite.Y * finite.Y) - intersection.Host.Radius));
            observedToolResidual = double.Max(observedToolResidual,
                double.Abs(double.Sqrt(finite.Y * finite.Y + (finite.Z - 3d) * (finite.Z - 3d)) - cutterRadius));
            var hostUv = intersection.EvaluateHostPcurve(t);
            var toolUv = intersection.EvaluateToolPcurve(t);
            Assert.True((finite - exact).Length <= result.CertifiedDeviationBoundMm + 1e-12d);
            Assert.True((finite - intersection.Host.Evaluate(hostUv.U, hostUv.V)).Length <= result.CertifiedDeviationBoundMm + 1e-12d);
            Assert.True((finite - intersection.Tool.Evaluate(toolUv.U, toolUv.V)).Length <= result.CertifiedDeviationBoundMm + 1e-12d);
        }
        _output.WriteLine($"r={cutterRadius:G17} segments={result.SegmentCount} controls={result.Curve.ControlPoints.Count} analyticBoundMm={result.AnalyticErrorBoundMm:G17} totalBoundMm={result.CertifiedDeviationBoundMm:G17} sampledMaxMm={observedMax:G17} hostResidualMm={observedHostResidual:G17} toolResidualMm={observedToolResidual:G17}");
    }

    [Fact]
    public void TighterTolerance_RefinesRepresentation()
    {
        var intersection = Create(0.79375d, 0.5953125d, CylinderIntersectionBranch.PositiveCutterAxis);
        var loose = CylinderIntersectionCurveRealizer.Realize(intersection, new ToleranceContext(1e-4d, 1e-9d)).Value;
        var tight = CylinderIntersectionCurveRealizer.Realize(intersection, new ToleranceContext(1e-7d, 1e-9d)).Value;
        Assert.True(tight.SegmentCount > loose.SegmentCount);
        Assert.True(tight.CertifiedDeviationBoundMm < loose.CertifiedDeviationBoundMm);
    }

    [Theory]
    [InlineData(0.1d)]
    [InlineData(0.5953125d)]
    [InlineData(0.75d)]
    public void FinitePcurves_LiftToQualified3dEdge(double cutterRadius)
    {
        var intersection = Create(0.79375d, cutterRadius, CylinderIntersectionBranch.NegativeCutterAxis);
        var edge = CylinderIntersectionCurveRealizer.Realize(intersection).Value;
        var pcurves = CylinderIntersectionPcurveRealizer.Realize(edge);
        Assert.True(pcurves.IsSuccess, string.Join("; ", pcurves.Diagnostics.Select(d => d.Message)));
        Assert.True(pcurves.Value.HostEdgeMismatchBoundMm <= ToleranceContext.Default.Linear);
        Assert.True(pcurves.Value.ToolEdgeMismatchBoundMm <= ToleranceContext.Default.Linear);
        var host = PcurveGeometry.Polynomial(intersection.Domain, pcurves.Value.Host);
        var tool = PcurveGeometry.Polynomial(intersection.Domain, pcurves.Value.Tool);
        var hostMax = 0d;
        var toolMax = 0d;
        for (var i = 0; i <= 2048; i++)
        {
            var t = 2d * double.Pi * i / 2048d;
            var edgePoint = edge.Curve.Evaluate(t);
            var h = host.Evaluate(t);
            var c = tool.Evaluate(t);
            hostMax = double.Max(hostMax, (intersection.Host.Evaluate(h.U, h.V) - edgePoint).Length);
            toolMax = double.Max(toolMax, (intersection.Tool.Evaluate(c.U, c.V) - edgePoint).Length);
        }
        Assert.True(hostMax <= pcurves.Value.HostEdgeMismatchBoundMm + 1e-12d);
        Assert.True(toolMax <= pcurves.Value.ToolEdgeMismatchBoundMm + 1e-12d);
        _output.WriteLine($"r={cutterRadius:G17} hostSegments={pcurves.Value.HostSegmentCount} toolSegments={pcurves.Value.ToolSegmentCount} hostBoundMm={pcurves.Value.HostEdgeMismatchBoundMm:G17} toolBoundMm={pcurves.Value.ToolEdgeMismatchBoundMm:G17} sampledHostMm={hostMax:G17} sampledToolMm={toolMax:G17}");
    }

    [Fact]
    public void UnsupportedAxesAndTangency_FailExplicitly()
    {
        var host = new CylinderSurface(Point3D.Origin, Z, 1d, X);
        var oblique = new CylinderSurface(Point3D.Origin,
            Direction3D.Create(new Vector3D(1, 0, 1)), 0.2d, Y);
        var eccentric = new CylinderSurface(new Point3D(0, 0.1, 0), X, 0.2d, Y);
        var almostEccentric = new CylinderSurface(new Point3D(0, 1e-8d, 0), X, 0.2d, Y);
        var tangent = new CylinderSurface(Point3D.Origin, X, 1d, Y);
        var domain = new ParameterInterval(0d, 2d * double.Pi);
        Assert.Contains("ObliqueAxes", CylinderCylinderIntersectionCurve.Create(host, oblique,
            Point3D.Origin, CylinderIntersectionBranch.PositiveCutterAxis, domain).Diagnostics[0].Source);
        Assert.Contains("EccentricAxes", CylinderCylinderIntersectionCurve.Create(host, eccentric,
            Point3D.Origin, CylinderIntersectionBranch.PositiveCutterAxis, domain).Diagnostics[0].Source);
        Assert.Contains("EccentricAxes", CylinderCylinderIntersectionCurve.Create(host, almostEccentric,
            Point3D.Origin, CylinderIntersectionBranch.PositiveCutterAxis, domain).Diagnostics[0].Source);
        Assert.Contains("TangentOrOversize", CylinderCylinderIntersectionCurve.Create(host, tangent,
            Point3D.Origin, CylinderIntersectionBranch.PositiveCutterAxis, domain).Diagnostics[0].Source);
    }

    private static CylinderCylinderIntersectionCurve Create(double hostRadius, double toolRadius, CylinderIntersectionBranch branch)
    {
        var host = new CylinderSurface(Point3D.Origin, Z, hostRadius, X);
        var tool = new CylinderSurface(new Point3D(0, 0, 3), X, toolRadius, Y);
        return CylinderCylinderIntersectionCurve.Create(host, tool, new Point3D(0, 0, 3), branch,
            new ParameterInterval(0d, 2d * double.Pi)).Value;
    }
}
