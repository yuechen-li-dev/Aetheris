using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class BrepDisplayTessellatorBoxRenderingTests
{
    private const double Tolerance = 1e-6d;

    [Fact]
    public void CreateBox_TessellatesToSixPlanarPatches_WithExpectedBounds()
    {
        const double width = 3d;
        const double depth = 2d;
        const double height = 1d;

        var box = BrepPrimitives.CreateBox(width, depth, height);
        var result = BrepDisplayTessellator.Tessellate(box.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.FacePatches.Count);

        var allPositions = result.Value.FacePatches.SelectMany(p => p.Positions).ToArray();
        Assert.NotEmpty(allPositions);

        var halfWidth = width / 2d;
        var halfDepth = depth / 2d;
        var halfHeight = height / 2d;

        AssertAlmostEqual(-halfWidth, allPositions.Min(p => p.X));
        AssertAlmostEqual(halfWidth, allPositions.Max(p => p.X));
        AssertAlmostEqual(-halfDepth, allPositions.Min(p => p.Y));
        AssertAlmostEqual(halfDepth, allPositions.Max(p => p.Y));
        AssertAlmostEqual(-halfHeight, allPositions.Min(p => p.Z));
        AssertAlmostEqual(halfHeight, allPositions.Max(p => p.Z));

        foreach (var patch in result.Value.FacePatches)
        {
            Assert.NotEmpty(patch.TriangleIndices);
            Assert.Equal(0, patch.TriangleIndices.Count % 3);

            var planeNormal = patch.Normals[0];
            var planePoint = patch.Positions[0];
            foreach (var point in patch.Positions)
            {
                var distance = (point - planePoint).Dot(planeNormal);
                Assert.True(double.Abs(distance) <= 1e-5d);
            }

            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
            {
                var i0 = patch.TriangleIndices[i];
                var i1 = patch.TriangleIndices[i + 1];
                var i2 = patch.TriangleIndices[i + 2];

                Assert.InRange(i0, 0, patch.Positions.Count - 1);
                Assert.InRange(i1, 0, patch.Positions.Count - 1);
                Assert.InRange(i2, 0, patch.Positions.Count - 1);

                var area2 = (patch.Positions[i1] - patch.Positions[i0])
                    .Cross(patch.Positions[i2] - patch.Positions[i0])
                    .Length;
                Assert.True(area2 > Tolerance, $"Degenerate triangle detected in face {patch.FaceId.Value}.");
            }
        }
    }

    [Fact]
    public void CreateBox_PlanarLoopOrdering_IsContiguous()
    {
        var box = BrepPrimitives.CreateBox(3d, 2d, 1d);
        var result = BrepDisplayTessellator.Tessellate(box.Value);

        Assert.True(result.IsSuccess);

        var expectedEdgeLengths = new[] { 1d, 2d, 3d };
        foreach (var patch in result.Value.FacePatches)
        {
            Assert.Equal(4, patch.Positions.Count);

            for (var i = 0; i < patch.Positions.Count; i++)
            {
                var current = patch.Positions[i];
                var next = patch.Positions[(i + 1) % patch.Positions.Count];
                var edgeLength = (next - current).Length;
                Assert.True(
                    expectedEdgeLengths.Any(expected => double.Abs(edgeLength - expected) <= Tolerance),
                    $"Unexpected edge length {edgeLength} for face {patch.FaceId.Value}.");
            }

            Assert.True(IsConvex(patch.Positions, patch.Normals[0]), $"Face {patch.FaceId.Value} was not convex.");
        }
    }

    private static bool IsConvex(IReadOnlyList<Point3D> points, Vector3D normal)
    {
        var sign = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var c = points[(i + 2) % points.Count];
            var cross = (b - a).Cross(c - b);
            var dot = cross.Dot(normal);
            if (double.Abs(dot) <= Tolerance)
            {
                continue;
            }

            var nextSign = dot > 0d ? 1 : -1;
            if (sign == 0)
            {
                sign = nextSign;
            }
            else if (sign != nextSign)
            {
                return false;
            }
        }

        return sign != 0;
    }

    private static void AssertAlmostEqual(double expected, double actual)
        => Assert.True(double.Abs(expected - actual) <= Tolerance, $"Expected {expected} but found {actual}.");
}
