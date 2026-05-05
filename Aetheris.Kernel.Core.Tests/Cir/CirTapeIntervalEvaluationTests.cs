using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirTapeIntervalEvaluationTests
{
    [Fact]
    public void Interval_Box_ContainsSampledEvaluations()
    {
        AssertIntervalContainsSamples(new CirBoxNode(6d, 4d, 8d), new CirBounds(new Point3D(-4d, -3d, -5d), new Point3D(4d, 3d, 5d)));
    }

    [Fact]
    public void Interval_Cylinder_ContainsSampledEvaluations()
    {
        AssertIntervalContainsSamples(new CirCylinderNode(3d, 7d), new CirBounds(new Point3D(-4d, -4d, -4d), new Point3D(4d, 4d, 4d)));
    }

    [Fact]
    public void Interval_Sphere_ContainsSampledEvaluations()
    {
        AssertIntervalContainsSamples(new CirSphereNode(2.5d), new CirBounds(new Point3D(-3d, -3d, -3d), new Point3D(3d, 3d, 3d)));
    }

    [Fact]
    public void Interval_BoxMinusCylinder_ContainsSampledEvaluations()
    {
        var node = new CirSubtractNode(new CirBoxNode(8d, 8d, 8d), new CirCylinderNode(2d, 10d));
        AssertIntervalContainsSamples(node, new CirBounds(new Point3D(-5d, -5d, -5d), new Point3D(5d, 5d, 5d)));
    }

    [Fact]
    public void Interval_ClassifiesFullyInsideRegion()
    {
        var tape = CirTapeLowerer.Lower(new CirBoxNode(10d, 10d, 10d));
        var classification = tape.ClassifyRegion(new CirBounds(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d)), ToleranceContext.Default);
        Assert.Equal(CirRegionClassification.Inside, classification);
    }

    [Fact]
    public void Interval_ClassifiesFullyOutsideRegion()
    {
        var tape = CirTapeLowerer.Lower(new CirBoxNode(2d, 2d, 2d));
        var classification = tape.ClassifyRegion(new CirBounds(new Point3D(3d, 3d, 3d), new Point3D(4d, 4d, 4d)), ToleranceContext.Default);
        Assert.Equal(CirRegionClassification.Outside, classification);
    }

    [Fact]
    public void Interval_ClassifiesMixedRegion()
    {
        var tape = CirTapeLowerer.Lower(new CirBoxNode(4d, 4d, 4d));
        var classification = tape.ClassifyRegion(new CirBounds(new Point3D(1d, -0.5d, -0.5d), new Point3D(3d, 0.5d, 0.5d)), ToleranceContext.Default);
        Assert.Equal(CirRegionClassification.Mixed, classification);
    }

    [Fact]
    public void Interval_TransformedPrimitive_IsConservative()
    {
        var transformed = new CirTransformNode(
            new CirCylinderNode(1.75d, 6d),
            Transform3D.CreateTranslation(new Vector3D(1d, -2d, 0.5d)) * Transform3D.CreateRotationY(double.Pi / 7d) * Transform3D.CreateRotationX(double.Pi / 9d));

        AssertIntervalContainsSamples(transformed, new CirBounds(new Point3D(-3d, -5d, -4d), new Point3D(5d, 2d, 4d)));
    }

    private static void AssertIntervalContainsSamples(CirNode node, CirBounds region)
    {
        var tape = CirTapeLowerer.Lower(node);
        var interval = tape.EvaluateInterval(region);

        foreach (var sample in SamplePoints(region))
        {
            var value = tape.Evaluate(sample);
            Assert.True(value >= interval.MinValue - 1e-9d, $"Value {value:R} below interval min {interval.MinValue:R} at point {sample}.");
            Assert.True(value <= interval.MaxValue + 1e-9d, $"Value {value:R} above interval max {interval.MaxValue:R} at point {sample}.");
        }
    }

    private static IEnumerable<Point3D> SamplePoints(CirBounds region)
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
