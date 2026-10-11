using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class PlanarMeshQualityTests
{
    [Fact]
    public void InteriorFillPreservesConcaveDomainAndBoundaryWhileImprovingPointDistribution()
    {
        Point3D[] points = [new(0, 0, 0), new(100, 0, 0), new(100, 40, 0),
            new(40, 40, 0), new(40, 100, 0), new(0, 100, 0)];
        int[] seed = [0, 1, 3, 1, 2, 3, 0, 3, 5, 3, 4, 5];
        var filled = PlanarMeshQuality.FillInterior(points, seed, 20);
        var repeated = PlanarMeshQuality.FillInterior(points, seed, 20);
        Assert.Equal(filled.Points, repeated.Points);
        Assert.Equal(filled.Indices, repeated.Indices);
        Assert.Equal(points, filled.Points.Take(points.Length));
        Assert.True(filled.Points.Count > points.Length);
        var boundary = new Dictionary<(int, int), int>();
        double area = 0;
        for (int face = 0; face < filled.Indices.Count; face += 3)
        {
            int a = filled.Indices[face];
            int b = filled.Indices[face + 1];
            int c = filled.Indices[face + 2];
            var pa = filled.Points[a];
            var pb = filled.Points[b];
            var pc = filled.Points[c];
            double cross = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
            Assert.True(cross > 0);
            area += cross * .5;
            Assert.False((pa.X + pb.X + pc.X) / 3 > 40 && (pa.Y + pb.Y + pc.Y) / 3 > 40);
            int[] triangle = [a, b, c];
            for (int edge = 0; edge < 3; edge++)
            {
                int first = triangle[edge];
                int second = triangle[(edge + 1) % 3];
                var key = (System.Math.Min(first, second), System.Math.Max(first, second));
                boundary[key] = boundary.GetValueOrDefault(key) + 1;
            }
        }
        Assert.Equal(6400, area, 7);
        Assert.Equal(6, boundary.Count(entry => entry.Value == 1));
        for (int edge = 0; edge < points.Length; edge++)
        {
            int other = (edge + 1) % points.Length;
            Assert.Equal(1, boundary[(System.Math.Min(edge, other), System.Math.Max(edge, other))]);
        }
    }

    [Fact]
    public void FillRejectsExcessiveWorkAndNonplanarGeometryWithoutMutatingInputs()
    {
        Point3D[] points = [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0)];
        int[] indices = [0, 1, 2];
        Assert.Throws<ArgumentNullException>(() => PlanarMeshQuality.FillInterior(null!, indices, 20));
        Assert.Throws<ArgumentNullException>(() => PlanarMeshQuality.FillInterior(points, null!, 20));
        Assert.Throws<ArgumentException>(() => PlanarMeshQuality.FillInterior(points, [0, 1, 2, 0, 1, 2, 0, 1, 2], 20));
        Assert.Throws<ArgumentException>(() => PlanarMeshQuality.FillInterior(points, indices, double.NaN));
        Assert.Throws<InvalidOperationException>(() => PlanarMeshQuality.FillInterior(points, indices, .01));
        Assert.Throws<InvalidOperationException>(() => PlanarMeshQuality.FillInterior(points, indices, 20, 3));
        Assert.Equal(3, points.Length);
        Assert.Equal(new Point3D(100, 0, 0), points[1]);
        points[1] = new(100, 0, 1);
        Assert.Throws<ArgumentException>(() => PlanarMeshQuality.FillInterior(points, indices, 20));
    }

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
