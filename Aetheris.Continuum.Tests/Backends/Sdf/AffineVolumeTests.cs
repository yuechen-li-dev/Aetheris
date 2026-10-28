using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class AffineVolumeTests
{
    [Theory]
    [InlineData(1, 0, 0, .3, .3)]
    [InlineData(1, 1, 0, 1, .5)]
    [InlineData(1, 2, 3, 1, 1d / 36)]
    [InlineData(1, 1e-7, 0, .3, .3 - .5e-7)]
    [InlineData(1, 1e-5, 2e-5, .3, .3 - 1.5e-5)]
    public void AxisTriangleAndSkinnyPlaneCutsEncloseIndependentIntegrals(double x, double y, double z, double offset, double expected)
    {
        var normal = new Vector3D(x, y, z);
        var length = normal.Length; normal /= length;
        var tangent = new Vector3D(-normal.Y, normal.X, 0);
        tangent /= tangent.Length;
        var other = normal.Cross(tangent);
        var shift = normal * (offset / length - 50);
        var transform = Transform3D.FromRowMajor([normal.X, normal.Y, normal.Z, 0,
            tangent.X, tangent.Y, tangent.Z, 0, other.X, other.Y, other.Z, 0, shift.X, shift.Y, shift.Z, 1]);
        var node = new SdfTransformNode(new SdfBoxNode(100, 100, 100), transform);
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(node), new(new(0, 0, 0), new(1, 1, 1)), 1e-6, 1);
        Assert.InRange(expected, result.LowerBound, result.UpperBound);
        Assert.False(result.BudgetExhausted);
        Assert.Equal(1, result.AffineCutRegions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObliquePlaneCutHasIndependentTetrahedralVolumeInOneRegion(bool complement)
    {
        var n = 1 / Math.Sqrt(3); var u = 1 / Math.Sqrt(2); var v = 1 / Math.Sqrt(6);
        var shift = (n - 50) * n;
        var transform = Transform3D.FromRowMajor([n, n, n, 0, -u, u, 0, 0, -v, -v, 2 * v, 0, shift, shift, shift, 1]);
        SdfNode node = new SdfTransformNode(new SdfBoxNode(100, 100, 100), transform);
        if (complement) node = new SdfSubtractNode(new SdfBoxNode(20, 20, 20), node);
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(node), new(new(0, 0, 0), new(1, 1, 1)), 1e-8, 1);
        var expected = complement ? 5d / 6 : 1d / 6;
        Assert.InRange(expected, result.LowerBound, result.UpperBound);
        Assert.InRange(result.ErrorBound, 0, 1e-8);
        Assert.False(result.BudgetExhausted);
        Assert.Equal(1, result.AffineCutRegions);
    }

    [Fact]
    public void RegionAffineEnclosuresContainActualFieldValuesAcrossPrimitiveAndCsgBranches()
    {
        var random = new Random(20261028);
        var transform = Transform3D.CreateScale(new Vector3D(1.7, .6, 1.2))
            * Transform3D.CreateRotationY(.47) * Transform3D.CreateTranslation(new(17, -23, 4));
        var nodes = new List<(SdfNode Node, Func<double, double, Point3D> Surface)>
        {
            (new SdfSphereNode(10), (a,z) => new(10*Math.Cos(a)*Math.Sqrt(1-z*z),10*Math.Sin(a)*Math.Sqrt(1-z*z),10*z)),
            (new SdfCylinderNode(5,12), (a,z) => new(5*Math.Cos(a),5*Math.Sin(a),5*z)),
            (new SdfCylinderNode(5,12), (a,z) => new(2*Math.Cos(a),2*Math.Sin(a),6)),
            (new SdfCylinderNode(5,12), (a,z) => new(2*Math.Cos(a),2*Math.Sin(a),-6)),
            (new SdfBoxNode(12,14,16), (a,z) => new(6,5*Math.Sin(a),6*z)),
            (new SdfConeNode(6,2,12), (a,z) => new((4-5*z/3)*Math.Cos(a),(4-5*z/3)*Math.Sin(a),5*z)),
            (new SdfConeNode(2,6,12), (a,z) => new((4+5*z/3)*Math.Cos(a),(4+5*z/3)*Math.Sin(a),5*z)),
            (new SdfConeNode(6,2,12), (a,z) => new(Math.Cos(a),Math.Sin(a),6)),
            (new SdfConeNode(6,2,12), (a,z) => new(Math.Cos(a),Math.Sin(a),-6)),
            (new SdfTorusNode(10,2), (a,z) => new((10+2*Math.Cos(z*Math.PI))*Math.Cos(a),(10+2*Math.Cos(z*Math.PI))*Math.Sin(a),2*Math.Sin(z*Math.PI)))
        };
        nodes.Add((new SdfSubtractNode(new SdfSphereNode(10), new SdfSphereNode(5)), nodes[0].Surface));
        nodes.Add((new SdfSubtractNode(new SdfSphereNode(10), new SdfSphereNode(5)),
            (a,z) => new(5*Math.Cos(a)*Math.Sqrt(1-z*z),5*Math.Sin(a)*Math.Sqrt(1-z*z),5*z)));
        var checkedCount = 0;
        foreach (var item in nodes)
        foreach (var placement in new[] { Transform3D.Identity, transform, Transform3D.CreateScale(new Vector3D(-1, 1, 1)) * transform })
        {
            var tape = SdfTapeLowerer.Lower(new SdfTransformNode(item.Node, placement));
            var admitted = 0;
            for (var iteration = 0; iteration < 120; iteration++)
            {
                var center = item.Surface(random.NextDouble()*2*Math.PI, random.NextDouble()*1.8-.9);
                center = placement.Apply(center);
                var extent = new Vector3D(.02+random.NextDouble()*.35,.02+random.NextDouble()*.35,.02+random.NextDouble()*.35);
                var region = new SdfBounds(center - extent, center + extent);
                var affine = tape.EvaluateRegionEnclosure(region).Affine;
                if (affine is not {} e) continue;
                admitted++;
                for (var sample = 0; sample < 24; sample++)
                {
                    var delta = sample < 8 ? new Vector3D((sample&1)==0?-extent.X:extent.X,(sample&2)==0?-extent.Y:extent.Y,(sample&4)==0?-extent.Z:extent.Z)
                        : new Vector3D((2*random.NextDouble()-1)*extent.X,(2*random.NextDouble()-1)*extent.Y,(2*random.NextDouble()-1)*extent.Z);
                    var value = tape.Evaluate(center + delta);
                    var linear = e.Value + e.Gradient.Dot(delta);
                    Assert.InRange(value, linear - e.LowerError - 1e-10, linear + e.UpperError + 1e-10);
                    checkedCount++;
                }
            }
            Assert.True(admitted >= 10, $"Too few admitted {item.Node.Kind} enclosures: {admitted}.");
        }
        Assert.True(checkedCount > 10_000);
    }

    [Fact]
    public void SphereAndCylinderReachTightBoundsWithoutTheOldMillionCellBoundaryBand()
    {
        foreach (var (node, expected) in new (SdfNode, double)[] { (new SdfSphereNode(10), 4*Math.PI*1000/3), (new SdfCylinderNode(5,20),Math.PI*25*20) })
        {
            var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(node), node.Bounds, expected*.001, 100001);
            Assert.InRange(expected, result.LowerBound, result.UpperBound);
            Assert.False(result.BudgetExhausted);
            Assert.True(result.AffineCutRegions > 0);
            Assert.True(result.RegionsVisited < 100001);
        }
    }

    [Fact]
    public void ADiscontinuousLegacyConeDistanceCannotCertifyBoundedVolume()
    {
        var cone = new SdfConeNode(6, 6, 1e-10);
        var tape = SdfTapeLowerer.Lower(cone);
        // The scalar projection shortcut jumps across this wall; a small
        // interval around the positive center value would omit negative values.
        var inside = tape.Evaluate(new(6 - 1e-12, 0, 0));
        var outside = tape.Evaluate(new(6 + 1e-12, 0, 0));
        Assert.True(outside - inside > 2e-12);
        var error = Assert.Throws<InvalidOperationException>(() =>
            SdfAdaptiveVolumeEstimator.EstimateBounded(tape, cone.Bounds, 1e-12));
        Assert.StartsWith("sdf-volume-cone-scale-unsupported:", error.Message);
    }

    [Fact]
    public void ThinShellWithNoInsideCornersCannotDisappearWhenBudgetIsSmall()
    {
        var shell = new SdfSubtractNode(new SdfSphereNode(10), new SdfSphereNode(9.99));
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(shell), shell.Bounds, .001, 31);
        var expected = 4*Math.PI*(1000-Math.Pow(9.99,3))/3;
        Assert.InRange(expected, result.LowerBound, result.UpperBound);
        Assert.True(result.BudgetExhausted);
        Assert.True(result.IntervalFallbackRegions > 0);
    }
}
