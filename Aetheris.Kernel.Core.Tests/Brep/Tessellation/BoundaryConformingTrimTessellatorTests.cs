using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

/// <summary>
/// Regression coverage for the boundary-conforming trimmed-surface tessellation. The old uniform-grid mask left a
/// staircase along every trim curve and dropped up to one grid cell of area next to it (visible as jagged, gappy
/// gear teeth on imported B-spline / cone faces).
/// </summary>
public sealed class BoundaryConformingTrimTessellatorTests
{
    private static readonly DisplayTessellationOptions Options =
        DisplayTessellationOptions.Create(double.Pi / 12d, 0.05d, minimumSegments: 12, maximumSegments: 64).Value;

    [Fact]
    public void ConcaveToothedOutline_CoversExactlyThePolygon_WithBoundaryVertices()
    {
        var outer = CreateGearOutline(teeth: 12, rootRadius: 8d, tipRadius: 10d, pointsPerFlank: 7);

        var patch = Tessellate([outer]);

        AssertPatchCoversRegion(patch, outer, holes: []);
        AssertContainsVertices(patch, outer);
    }

    [Fact]
    public void ToothedOutlineWithHole_LeavesTheHoleEmpty()
    {
        var outer = CreateGearOutline(teeth: 9, rootRadius: 8d, tipRadius: 10d, pointsPerFlank: 5);
        var hole = CreateCircle(radius: 3d, count: 24, clockwise: true);

        var patch = Tessellate([outer, hole]);

        AssertPatchCoversRegion(patch, outer, [hole]);
        AssertContainsVertices(patch, hole);
    }

    [Fact]
    public void FinelySampledStraightEdges_WithManyCollinearVertices_StillTriangulate()
    {
        // A long thin slot sampled every 0.01 along its straight sides: thousands of exactly collinear vertices.
        var outline = new List<(double U, double V)>();
        for (var i = 0; i <= 400; i++)
        {
            outline.Add((i * 0.05d, 0d));
        }

        for (var i = 1; i <= 40; i++)
        {
            outline.Add((20d, i * 0.05d));
        }

        for (var i = 399; i >= 0; i--)
        {
            outline.Add((i * 0.05d, 2d));
        }

        for (var i = 39; i >= 1; i--)
        {
            outline.Add((0d, i * 0.05d));
        }

        // Make it non-rectangular so the mask fast path is not applicable.
        outline[10] = (outline[10].U, 0.3d);
        var patch = Tessellate([outline]);

        AssertPatchCoversRegion(patch, outline, holes: []);
    }

    [Fact]
    public void Tessellation_IsDeterministic()
    {
        var outer = CreateGearOutline(teeth: 7, rootRadius: 8d, tipRadius: 10d, pointsPerFlank: 4);

        var first = Tessellate([outer]);
        var second = Tessellate([outer]);

        Assert.Equal(first.Positions, second.Positions);
        Assert.Equal(first.TriangleIndices, second.TriangleIndices);
    }

    private static DisplayFaceMeshPatch Tessellate(IReadOnlyList<IReadOnlyList<(double U, double V)>> loops)
    {
        var result = TrimmedSurfaceTessellator.Tessellate(
            new FaceId(1),
            loops,
            (u, v) => new Point3D(u, v, 0d),
            (_, _) => new Vector3D(0d, 0d, 1d),
            Options,
            double.NegativeInfinity,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.PositiveInfinity,
            (message, source) => new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, message, source));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(result.Value.TriangleIndices);
        return result.Value;
    }

    private static void AssertPatchCoversRegion(
        DisplayFaceMeshPatch patch,
        IReadOnlyList<(double U, double V)> outer,
        IReadOnlyList<IReadOnlyList<(double U, double V)>> holes)
    {
        var expectedArea = System.Math.Abs(SignedArea(outer)) - holes.Sum(hole => System.Math.Abs(SignedArea(hole)));
        var actualArea = 0d;
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            var centroid = ((a.X + b.X + c.X) / 3d, (a.Y + b.Y + c.Y) / 3d);
            Assert.True(ContainsPoint(outer, centroid), "triangle centroid escaped the outer loop");
            Assert.All(holes, hole => Assert.False(ContainsPoint(hole, centroid), "triangle centroid entered a hole"));
            actualArea += System.Math.Abs(((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X))) * 0.5d;
        }

        // The mask tessellator lost up to a grid cell of area along the boundary; conforming output must not.
        Assert.Equal(expectedArea, actualArea, expectedArea * 1e-6d);
    }

    private static void AssertContainsVertices(DisplayFaceMeshPatch patch, IReadOnlyList<(double U, double V)> loop)
    {
        foreach (var (u, v) in loop)
        {
            Assert.Contains(patch.Positions, p => System.Math.Abs(p.X - u) <= 1e-9d && System.Math.Abs(p.Y - v) <= 1e-9d);
        }
    }

    private static List<(double U, double V)> CreateGearOutline(int teeth, double rootRadius, double tipRadius, int pointsPerFlank)
    {
        var outline = new List<(double U, double V)>();
        var pitch = 2d * double.Pi / teeth;
        for (var tooth = 0; tooth < teeth; tooth++)
        {
            var start = tooth * pitch;
            for (var i = 0; i < pointsPerFlank; i++)
            {
                var t = (double)i / pointsPerFlank;
                var angle = start + (pitch * 0.25d * t);
                var radius = rootRadius + ((tipRadius - rootRadius) * t);
                outline.Add((radius * double.Cos(angle), radius * double.Sin(angle)));
            }

            for (var i = 0; i < pointsPerFlank; i++)
            {
                var t = (double)i / pointsPerFlank;
                var angle = start + (pitch * 0.25d) + (pitch * 0.25d * t);
                outline.Add((tipRadius * double.Cos(angle), tipRadius * double.Sin(angle)));
            }

            for (var i = 0; i < pointsPerFlank; i++)
            {
                var t = (double)i / pointsPerFlank;
                var angle = start + (pitch * 0.5d) + (pitch * 0.25d * t);
                var radius = tipRadius - ((tipRadius - rootRadius) * t);
                outline.Add((radius * double.Cos(angle), radius * double.Sin(angle)));
            }

            for (var i = 0; i < pointsPerFlank; i++)
            {
                var t = (double)i / pointsPerFlank;
                var angle = start + (pitch * 0.75d) + (pitch * 0.25d * t);
                outline.Add((rootRadius * double.Cos(angle), rootRadius * double.Sin(angle)));
            }
        }

        return outline;
    }

    private static List<(double U, double V)> CreateCircle(double radius, int count, bool clockwise)
    {
        var points = new List<(double U, double V)>(count);
        for (var i = 0; i < count; i++)
        {
            var angle = 2d * double.Pi * i / count * (clockwise ? -1d : 1d);
            points.Add((radius * double.Cos(angle), radius * double.Sin(angle)));
        }

        return points;
    }

    private static double SignedArea(IReadOnlyList<(double U, double V)> polygon)
    {
        var area = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            area += (a.U * b.V) - (b.U * a.V);
        }

        return area * 0.5d;
    }

    private static bool ContainsPoint(IReadOnlyList<(double U, double V)> polygon, (double U, double V) point)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (((a.V > point.V) != (b.V > point.V))
                && (point.U < (((b.U - a.U) * (point.V - a.V)) / (b.V - a.V)) + a.U))
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
