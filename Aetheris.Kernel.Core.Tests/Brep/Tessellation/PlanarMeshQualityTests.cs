using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class PlanarMeshQualityTests
{
    [Fact]
    public void SkewQuadrilateralFlipsItsBadDiagonalWithoutChangingBoundaryOrArea()
    {
        Point3D[] points = [new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(0, 3, 0)];
        int[] original = [0, 1, 3, 1, 2, 3];
        var improved = PlanarMeshQuality.Improve(points, original);
        Assert.Equal(improved.ToArray(), PlanarMeshQuality.Improve(points, original).ToArray());
        Assert.Equal(improved.ToArray(), PlanarMeshQuality.Improve(points, improved).ToArray());
        var counts = new Dictionary<(int, int), int>();
        double area = 0;
        for (int face = 0; face < improved.Count; face += 3)
        {
            var a = points[improved[face]];
            var b = points[improved[face + 1]];
            var c = points[improved[face + 2]];
            double twiceArea = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            Assert.True(twiceArea > 0);
            area += twiceArea * .5;
            for (int edge = 0; edge < 3; edge++)
            {
                int first = improved[face + edge];
                int second = improved[face + (edge + 1) % 3];
                var key = (System.Math.Min(first, second), System.Math.Max(first, second));
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }
        Assert.Equal(4, area);
        Assert.Equal(2, counts[(0, 2)]);
        Assert.False(counts.ContainsKey((1, 3)));
        Assert.All(new[] { (0, 1), (1, 2), (2, 3), (0, 3) }, edge => Assert.Equal(1, counts[edge]));
    }

    [Fact]
    public void InvalidMeshStopsRatherThanPretendingToImprove()
    {
        Point3D[] points = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];
        Assert.Throws<ArgumentException>(() => PlanarMeshQuality.Improve(points, [0, 2, 1]));
        Assert.Throws<ArgumentException>(() => PlanarMeshQuality.Improve(points, [0, 0, 1]));
        points[2] = new(double.NaN, 1, 0);
        Assert.Throws<ArgumentException>(() => PlanarMeshQuality.Improve(points, [0, 1, 2]));
    }
}
