using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class PlanarPolygonTriangulatorTests
{
    [Fact]
    public void TryTriangulateWithHoles_RectangleWithRectangularHole_PreservesHoleDeterministically()
    {
        var outer = Rectangle(0d, 0d, 10d, 10d);
        var hole = Rectangle(3d, 3d, 7d, 7d);

        var first = TryTriangulateWithHoles(outer, [hole]);
        var second = TryTriangulateWithHoles(outer, [hole]);

        Assert.Equal(first.Points, second.Points);
        Assert.Equal(first.Indices, second.Indices);
        Assert.Equal(24, first.Indices.Count);
        Assert.All(GetTriangleCentroids(first.Points, first.Indices), centroid =>
            Assert.False(PointInPolygon2D(centroid, hole), $"triangle centroid {centroid} fell inside the rectangular hole"));
    }

    [Fact]
    public void TryTriangulateWithHoles_RectangleWithCircularHole_PreservesHole()
    {
        var outer = Rectangle(0d, 0d, 20d, 20d);
        var hole = ApproximateCircle(new Point3D(10d, 10d, 0d), radius: 3d, segmentCount: 16);

        var result = TryTriangulateWithHoles(outer, [hole]);

        Assert.NotEmpty(result.Indices);
        Assert.All(GetTriangleCentroids(result.Points, result.Indices), centroid =>
            Assert.False(PointInPolygon2D(centroid, hole), $"triangle centroid {centroid} fell inside the circular hole"));
    }

    [Fact]
    public void TryTriangulateWithHoles_RectangleWithMultipleHoles_PreservesAllHolesDeterministically()
    {
        var outer = Rectangle(0d, 0d, 20d, 12d);
        var holes = new[]
        {
            Rectangle(2d, 2d, 5d, 5d),
            Rectangle(8d, 3d, 11d, 6d),
            Rectangle(14d, 2d, 18d, 8d),
        };

        var first = TryTriangulateWithHoles(outer, holes);
        var second = TryTriangulateWithHoles(outer, holes);

        Assert.Equal(first.Points, second.Points);
        Assert.Equal(first.Indices, second.Indices);
        Assert.All(GetTriangleCentroids(first.Points, first.Indices), centroid =>
        {
            Assert.DoesNotContain(holes, hole => PointInPolygon2D(centroid, hole));
        });
    }

    [Fact]
    public void TryTriangulate_SimpleSingleLoop_RegressionPathRemainsValid()
    {
        var polygon = new[]
        {
            new Point3D(0d, 0d, 0d),
            new Point3D(5d, 0d, 0d),
            new Point3D(5d, 2d, 0d),
            new Point3D(3d, 2d, 0d),
            new Point3D(3d, 5d, 0d),
            new Point3D(0d, 5d, 0d),
        };

        var firstSuccess = PlanarPolygonTriangulator.TryTriangulate(
            polygon,
            new Vector3D(0d, 0d, 1d),
            out var firstIndices,
            out var firstFailure);
        var secondSuccess = PlanarPolygonTriangulator.TryTriangulate(
            polygon,
            new Vector3D(0d, 0d, 1d),
            out var secondIndices,
            out var secondFailure);

        Assert.True(firstSuccess);
        Assert.True(secondSuccess);
        Assert.Null(firstFailure);
        Assert.Null(secondFailure);
        Assert.Equal(firstIndices, secondIndices);
        Assert.Equal((polygon.Length - 2) * 3, firstIndices.Count);
    }

    private static (IReadOnlyList<Point3D> Points, IReadOnlyList<int> Indices) TryTriangulateWithHoles(
        IReadOnlyList<Point3D> outer,
        IReadOnlyList<IReadOnlyList<Point3D>> holes)
    {
        var success = PlanarPolygonTriangulator.TryTriangulateWithHoles(
            outer,
            holes,
            new Vector3D(0d, 0d, 1d),
            out var points,
            out var indices,
            out var failure);

        Assert.True(success, failure?.ToString());
        Assert.Null(failure);
        return (points, indices);
    }

    private static Point3D[] Rectangle(double minX, double minY, double maxX, double maxY)
        =>
        [
            new Point3D(minX, minY, 0d),
            new Point3D(maxX, minY, 0d),
            new Point3D(maxX, maxY, 0d),
            new Point3D(minX, maxY, 0d),
        ];

    private static Point3D[] ApproximateCircle(Point3D center, double radius, int segmentCount)
    {
        var points = new Point3D[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var angle = (2d * double.Pi * i) / segmentCount;
            points[i] = new Point3D(
                center.X + (radius * double.Cos(angle)),
                center.Y + (radius * double.Sin(angle)),
                center.Z);
        }

        return points;
    }

    private static IEnumerable<Point3D> GetTriangleCentroids(IReadOnlyList<Point3D> points, IReadOnlyList<int> indices)
    {
        for (var i = 0; i < indices.Count; i += 3)
        {
            var p0 = points[indices[i]];
            var p1 = points[indices[i + 1]];
            var p2 = points[indices[i + 2]];
            yield return new Point3D(
                (p0.X + p1.X + p2.X) / 3d,
                (p0.Y + p1.Y + p2.Y) / 3d,
                (p0.Z + p1.Z + p2.Z) / 3d);
        }
    }

    private static bool PointInPolygon2D(Point3D point, IReadOnlyList<Point3D> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            if (((current.Y > point.Y) != (next.Y > point.Y))
                && point.X < (((next.X - current.X) * (point.Y - current.Y)) / (next.Y - current.Y + double.Epsilon)) + current.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
