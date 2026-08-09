using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class SdfTapeIntervalEvaluationTests
{
    [Fact]
    public void Interval_Box_ContainsSampledEvaluations()
    {
        AssertIntervalContainsSamples(new SdfBoxNode(6d, 4d, 8d), new SdfBounds(new Point3D(-4d, -3d, -5d), new Point3D(4d, 3d, 5d)));
    }

    [Fact]
    public void Interval_Cylinder_ContainsSampledEvaluations()
    {
        AssertIntervalContainsSamples(new SdfCylinderNode(3d, 7d), new SdfBounds(new Point3D(-4d, -4d, -4d), new Point3D(4d, 4d, 4d)));
    }

    [Fact]
    public void Interval_Sphere_ContainsSampledEvaluations()
    {
        AssertIntervalContainsSamples(new SdfSphereNode(2.5d), new SdfBounds(new Point3D(-3d, -3d, -3d), new Point3D(3d, 3d, 3d)));
    }

    [Fact]
    public void Interval_BoxMinusCylinder_ContainsSampledEvaluations()
    {
        var node = new SdfSubtractNode(new SdfBoxNode(8d, 8d, 8d), new SdfCylinderNode(2d, 10d));
        AssertIntervalContainsSamples(node, new SdfBounds(new Point3D(-5d, -5d, -5d), new Point3D(5d, 5d, 5d)));
    }

    [Fact]
    public void Interval_ClassifiesFullyInsideRegion()
    {
        var tape = SdfTapeLowerer.Lower(new SdfBoxNode(10d, 10d, 10d));
        var classification = tape.ClassifyRegion(new SdfBounds(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d)), ToleranceContext.Default);
        Assert.Equal(SdfRegionClassification.Inside, classification);
    }

    [Fact]
    public void Interval_ClassifiesFullyOutsideRegion()
    {
        var tape = SdfTapeLowerer.Lower(new SdfBoxNode(2d, 2d, 2d));
        var classification = tape.ClassifyRegion(new SdfBounds(new Point3D(3d, 3d, 3d), new Point3D(4d, 4d, 4d)), ToleranceContext.Default);
        Assert.Equal(SdfRegionClassification.Outside, classification);
    }

    [Fact]
    public void Interval_ClassifiesMixedRegion()
    {
        var tape = SdfTapeLowerer.Lower(new SdfBoxNode(4d, 4d, 4d));
        var classification = tape.ClassifyRegion(new SdfBounds(new Point3D(1d, -0.5d, -0.5d), new Point3D(3d, 0.5d, 0.5d)), ToleranceContext.Default);
        Assert.Equal(SdfRegionClassification.Mixed, classification);
    }

    [Fact]
    public void Interval_TransformedPrimitive_IsConservative()
    {
        var transformed = new SdfTransformNode(
            new SdfCylinderNode(1.75d, 6d),
            Transform3D.CreateTranslation(new Vector3D(1d, -2d, 0.5d)) * Transform3D.CreateRotationY(double.Pi / 7d) * Transform3D.CreateRotationX(double.Pi / 9d));

        AssertIntervalContainsSamples(transformed, new SdfBounds(new Point3D(-3d, -5d, -4d), new Point3D(5d, 2d, 4d)));
    }

    private static void AssertIntervalContainsSamples(SdfNode node, SdfBounds region)
    {
        var tape = SdfTapeLowerer.Lower(node);
        var interval = tape.EvaluateInterval(region);

        foreach (var sample in SamplePoints(region))
        {
            var value = tape.Evaluate(sample);
            Assert.True(value >= interval.MinValue - 1e-9d, $"Value {value:R} below interval min {interval.MinValue:R} at point {sample}.");
            Assert.True(value <= interval.MaxValue + 1e-9d, $"Value {value:R} above interval max {interval.MaxValue:R} at point {sample}.");
        }
    }

    private static IEnumerable<Point3D> SamplePoints(SdfBounds region)
    {
        var xs = BuildSamples(region.Min.X, region.Max.X);
        var ys = BuildSamples(region.Min.Y, region.Max.Y);
        var zs = BuildSamples(region.Min.Z, region.Max.Z);
        foreach (var x in xs)
        foreach (var y in ys)
        foreach (var z in zs)
        {
            yield return new Point3D(x, y, z);
        }
    }

    private static double[] BuildSamples(double min, double max)
    {
        var mid = (min + max) * 0.5d;
        return [min, mid, max, min + ((max - min) * 0.25d), min + ((max - min) * 0.75d)];
    }
}
