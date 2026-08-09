using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class BSplineTrimSamplerTests
{
    [Fact]
    public void EvaluatesDegreeOnePositionAndTangent()
    {
        var curve = Create(1, [new(0, 0, 0), new(2, 4, 0)], [2, 2], [0d, 1d]);

        AssertPoint(new Point3D(1, 2, 0), curve.Evaluate(0.5d));
        AssertVector(new Vector3D(2, 4, 0), curve.EvaluateTangent(0.5d));
    }

    [Fact]
    public void EvaluatesQuadraticAndCubicBezierEndpoints()
    {
        var quadratic = Create(2, [new(0, 0, 0), new(1, 2, 0), new(2, 0, 0)], [3, 3], [0d, 1d]);
        var cubic = Create(3, [new(0, 0, 0), new(1, 3, 0), new(2, -1, 0), new(4, 0, 0)], [4, 4], [0d, 1d]);

        AssertPoint(new Point3D(1, 1, 0), quadratic.Evaluate(0.5d));
        AssertPoint(new Point3D(0, 0, 0), cubic.Evaluate(0d));
        AssertPoint(new Point3D(4, 0, 0), cubic.Evaluate(1d));
        AssertVector(new Vector3D(3, 9, 0), cubic.EvaluateTangent(0d));
        AssertVector(new Vector3D(6, 3, 0), cubic.EvaluateTangent(1d));
    }

    [Fact]
    public void ReportsNonZeroSpansAcrossRepeatedKnot()
    {
        var curve = Create(2,
            [new(0, 0, 0), new(1, 1, 0), new(2, 1, 0), new(3, 0, 0), new(4, 0, 0)],
            [3, 2, 3], [0d, 0.5d, 1d]);

        Assert.Equal([new ParameterInterval(0d, 0.5d), new ParameterInterval(0.5d, 1d)], curve.GetNonZeroKnotSpans(new ParameterInterval(0d, 1d)));
        AssertPoint(curve.Evaluate(0.5d), curve.Evaluate(0.5d));
    }

    [Fact]
    public void SamplesKnotSpansAdaptivelyAndDeterministically()
    {
        var curve = Create(3,
            [new(0, 0, 0), new(1, 3, 0), new(2, -2, 0), new(3, 2, 0), new(4, 0, 0)],
            [4, 1, 4], [0d, 0.5d, 1d]);
        var policy = new SurfaceMeshPolicy(0.01d, 0.08d, 12, 4096);

        var first = BSplineTrimSampler.Sample(curve, new ParameterInterval(0d, 1d), policy);
        var second = BSplineTrimSampler.Sample(curve, new ParameterInterval(0d, 1d), policy);

        Assert.True(first.IsSuccess, first.Failure);
        Assert.Equal(first.Samples, second.Samples);
        Assert.Contains(first.Samples, sample => sample.Parameter == 0.5d);
        Assert.True(first.MaxChordalDeviation <= policy.TargetChordalError);
        Assert.True(first.MaxTangentDeviationRadians <= policy.TargetNormalErrorRadians);
    }

    [Fact]
    public void FailsExplicitlyWhenToleranceCannotBeMetWithinBounds()
    {
        var curve = Create(3, [new(0, 0, 0), new(0, 5, 0), new(5, 5, 0), new(5, 0, 0)], [4, 4], [0d, 1d]);

        var result = BSplineTrimSampler.Sample(curve, new ParameterInterval(0d, 1d), new SurfaceMeshPolicy(1e-12d, 1e-12d, 0, 16));

        Assert.False(result.IsSuccess);
        Assert.Contains("refinement depth", result.Failure, StringComparison.Ordinal);
    }

    private static BSpline3Curve Create(int degree, IReadOnlyList<Point3D> points, IReadOnlyList<int> multiplicities, IReadOnlyList<double> knots)
        => new(degree, points, multiplicities, knots, "UNSPECIFIED", false, false, "UNSPECIFIED");

    private static void AssertPoint(Point3D expected, Point3D actual, double tolerance = 1e-12d)
        => Assert.True((expected - actual).Length <= tolerance, $"Expected {expected}; actual {actual}.");

    private static void AssertVector(Vector3D expected, Vector3D actual, double tolerance = 1e-12d)
        => Assert.True((expected - actual).Length <= tolerance, $"Expected {expected}; actual {actual}.");
}
