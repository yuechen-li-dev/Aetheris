using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class AnalyticIsoParametricPcurveTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    [InlineData(2, 1)]
    [InlineData(2, -1)]
    [InlineData(3, 1)]
    [InlineData(3, -1)]
    public void RotatedAnalyticEdges_HaveExactLinearUvIncludingMultipleTurns(int family, int sense)
    {
        var origin = new Point3D(11, -7, 19);
        var axis = Direction3D.Create(new Vector3D(1, 2, 3));
        var reference = Direction3D.Create(new Vector3D(2, -1, 0));
        var cylinder = new CylinderSurface(origin, axis, 2, reference);
        var torus = new TorusSurface(origin, axis, 5, 2, reference);
        var radial = torus.XAxis.ToVector() * double.Cos(.8) + torus.YAxis.ToVector() * double.Sin(.8);
        var curve = family switch
        {
            0 => CurveGeometry.FromLine(new(cylinder.Evaluate(.8, 3), Direction3D.Create(axis.ToVector() * sense))),
            1 => CurveGeometry.FromCircle(new(origin + axis.ToVector() * 3, Direction3D.Create(axis.ToVector() * sense), 2, reference)),
            2 => CurveGeometry.FromCircle(new(origin + axis.ToVector() * (2 * double.Sin(.6)), Direction3D.Create(axis.ToVector() * sense), 5 + 2 * double.Cos(.6), reference)),
            _ => CurveGeometry.FromCircle(new(origin + radial * 5, Direction3D.Create(radial.Cross(axis.ToVector()) * sense), 2, Direction3D.Create(radial))),
        };
        var surface = family < 2 ? SurfaceGeometry.FromCylinder(cylinder) : SurfaceGeometry.FromTorus(torus);
        var interval = new ParameterInterval(-.9, -.9 + 1.5 * double.Tau);
        var uv = Assert.IsType<PcurveGeometry>(BrepPcurveRecovery.TryBuildIsoParametricPcurve(surface, curve, interval));
        Assert.Equal(PcurveGeometryKind.Line, uv.Kind);
        Assert.Null(uv.PolynomialCurve);
        for (var i = 0; i <= 1000; i++)
        {
            var t = interval.Start + (interval.End - interval.Start) * i / 1000d;
            var p = uv.Evaluate(t);
            var lifted = family < 2 ? cylinder.Evaluate(p.U, p.V) : torus.Evaluate(p.U, p.V);
            var expected = family == 0 ? curve.Line3!.Value.Evaluate(t) : curve.Circle3!.Value.Evaluate(t);
            Assert.True((lifted - expected).Length < 1e-11, $"family {family}, sense {sense}, t={t:R}");
        }
    }

    [Fact]
    public void OffSupportOrObliqueEdges_DoNotTakeTheExactPath()
    {
        var axis = Direction3D.Create(new Vector3D(0, 0, 1));
        var reference = Direction3D.Create(new Vector3D(1, 0, 0));
        var cylinder = SurfaceGeometry.FromCylinder(new(new(0, 0, 0), axis, 2, reference));
        var torus = SurfaceGeometry.FromTorus(new(new(0, 0, 0), axis, 5, 2, reference));
        var domain = new ParameterInterval(0, double.Tau);
        // Even a sub-import-tolerance mismatch must not be called exact.
        var wrongRadius = CurveGeometry.FromCircle(new(new(0, 0, 0), axis, 2.00001, reference));
        var oblique = CurveGeometry.FromCircle(new(new(5, 0, 0), Direction3D.Create(new Vector3D(0, 1, .1)), 2, reference));
        Assert.Null(BrepPcurveRecovery.TryBuildIsoParametricPcurve(cylinder, wrongRadius, domain));
        Assert.Null(BrepPcurveRecovery.TryBuildIsoParametricPcurve(torus, oblique, domain));
    }
}
