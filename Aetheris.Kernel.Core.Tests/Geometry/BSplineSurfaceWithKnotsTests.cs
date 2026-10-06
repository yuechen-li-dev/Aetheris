using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class BSplineSurfaceWithKnotsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveSpanMatchesIndependentTensorEvaluationAtKnotsAndClampedEnds(bool rational)
    {
        int[] mu = [4, 1, 2, 4], mv = [3, 2, 3];
        double[] ku = [0, .2, .55, 1], kv = [0, .6, 1];
        var net = Enumerable.Range(0, 7).Select(i => (IReadOnlyList<Point3D>)Enumerable.Range(0, 5)
            .Select(j => new Point3D(i * .7, j * .3, System.Math.Sin(i + j * .5))).ToArray()).ToArray();
        var weights = Enumerable.Range(0, 7).Select(i => (IReadOnlyList<double>)Enumerable.Range(0, 5)
            .Select(j => 1d + (i + j) * .13).ToArray()).ToArray();
        var surface = new BSplineSurfaceWithKnots(3, 2, net, "UNSPECIFIED", false, false, false,
            mu, mv, ku, kv, "UNSPECIFIED", rational ? weights : null);
        Point3D Tensor(IReadOnlyList<IReadOnlyList<Point3D>> controls, double u, double v)
        {
            var rows = controls.Select(row => new BSpline3Curve(2, row, mv, kv,
                "UNSPECIFIED", false, false, "UNSPECIFIED").Evaluate(v)).ToArray();
            return new BSpline3Curve(3, rows, mu, ku, "UNSPECIFIED", false, false, "UNSPECIFIED").Evaluate(u);
        }
        foreach (var u in new[] { -.1, 0, .199999, .2, .35, .55, .999999, 1, 1.1 })
            foreach (var v in new[] { -.1, 0, .3, .6, .999999, 1, 1.1 })
            {
                var expected = Tensor(net, u, v);
                if (rational)
                {
                    var numerator = Tensor(net.Select((row, i) => (IReadOnlyList<Point3D>)row.Select((p, j) =>
                        new Point3D(p.X * weights[i][j], p.Y * weights[i][j], p.Z * weights[i][j])).ToArray()).ToArray(), u, v);
                    var weight = Tensor(weights.Select(row => (IReadOnlyList<Point3D>)row.Select(w => new Point3D(w, 0, 0)).ToArray()).ToArray(), u, v).X;
                    expected = new(numerator.X / weight, numerator.Y / weight, numerator.Z / weight);
                }
                Assert.Equal(expected, surface.Evaluate(u, v));
            }
    }

    [Fact]
    public void Evaluate_AtDomainCorners_ReturnsCornerControlPoints()
    {
        var surface = CreateBilinearSurface();

        Assert.Equal(new Point3D(0d, 0d, 0d), surface.Evaluate(surface.DomainStartU, surface.DomainStartV));
        Assert.Equal(new Point3D(0d, 1d, 0d), surface.Evaluate(surface.DomainStartU, surface.DomainEndV));
        Assert.Equal(new Point3D(1d, 0d, 0d), surface.Evaluate(surface.DomainEndU, surface.DomainStartV));
        Assert.Equal(new Point3D(1d, 1d, 1d), surface.Evaluate(surface.DomainEndU, surface.DomainEndV));
    }

    [Fact]
    public void Evaluate_IsDeterministic_ForRepeatedCalls()
    {
        var surface = CreateBilinearSurface();
        var u = (surface.DomainStartU + surface.DomainEndU) * 0.5d;
        var v = (surface.DomainStartV + surface.DomainEndV) * 0.25d;

        var first = surface.Evaluate(u, v);
        var second = surface.Evaluate(u, v);

        Assert.Equal(first, second);
    }

    private static BSplineSurfaceWithKnots CreateBilinearSurface()
    {
        var controlPoints = new[]
        {
            new[] { new Point3D(0d, 0d, 0d), new Point3D(0d, 1d, 0d) },
            new[] { new Point3D(1d, 0d, 0d), new Point3D(1d, 1d, 1d) }
        };

        return new BSplineSurfaceWithKnots(
            degreeU: 1,
            degreeV: 1,
            controlPoints: controlPoints,
            surfaceForm: "UNSPECIFIED",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: new[] { 2, 2 },
            knotMultiplicitiesV: new[] { 2, 2 },
            knotValuesU: new[] { 0d, 1d },
            knotValuesV: new[] { 0d, 1d },
            knotSpec: "UNSPECIFIED");
    }
}
