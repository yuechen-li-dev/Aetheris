using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class GeneralTrimMaterializationTests
{
    [Fact]
    public void GeneralIrBoundaryWithoutLocalChart_RejectsRatherThanInventingAnAverageCentre()
    {
        var points = new[] { new Point3D(1, 0, 0), new Point3D(0, 1, 0), new Point3D(-1, 0, 0),
            new Point3D(0, -1, 0), new Point3D(1, 0, 1) };
        var vertices = points.Select((p, i) => new SurfaceMeshVertex(i, p)).ToArray();
        var cylinder = new CylinderSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)),
            1, Direction3D.Create(new Vector3D(1, 0, 0)));
        var patch = new SurfacePatch(new FaceId(9), new(SurfaceMeshSupportKind.Cylinder, Cylinder: cylinder), [],
            [new BoundaryPolygonCell([0, 1, 2, 3, 4])], true);
        var document = new SurfaceMeshDocument(vertices, [patch], [], new(1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, ""));
        Assert.False(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out _));
        Assert.Empty(mesh.TriangleIndices);
        Assert.Equal(points.Length, mesh.Positions.Count);
    }

    [Fact]
    public void DisplayClassification_IsIndependentOfSupportAndEngineeringQualification()
    {
        Assert.Equal(BoundaryConformingTrimTessellator.TrimClass.RectangularGridLike,
            BoundaryConformingTrimTessellator.Classify([new() { (0, 0), (1, 0), (1, 1), (0, 1) }], 0));
        Assert.Equal(BoundaryConformingTrimTessellator.TrimClass.SimpleConvex,
            BoundaryConformingTrimTessellator.Classify([new() { (0, 0), (1, 0), (.5, 1) }], 0));
        Assert.Equal(BoundaryConformingTrimTessellator.TrimClass.SimpleConcave,
            BoundaryConformingTrimTessellator.Classify([new() { (0, 0), (2, 0), (1, .1), (2, 1), (0, 1) }], 0));
        Assert.Equal(BoundaryConformingTrimTessellator.TrimClass.Pathological,
            BoundaryConformingTrimTessellator.Classify([new() { (0, 0), (.5, .5), (1, 1) }], 0));
    }

    [Fact]
    public void ConcaveCylinderChartAcrossAngularSeam_HasNoCrossingOrDuplicateRegion()
    {
        List<(double U, double V)> loop = [(6.2, 0), (6.5, 0), (6.5, 1), (6.35, 1), (6.35, .5), (6.2, .5)];
        var patch = BoundaryConformingTrimTessellator.TryTessellate(new FaceId(3), [loop], 0,
            (u, v) => new Point3D(double.Cos(u), double.Sin(u), v),
            (u, _) => new Vector3D(double.Cos(u), double.Sin(u), 0), .025, .1,
            DisplayTessellationOptions.Default, null, out var incomplete,
            grid: (Lines(6.2, 6.5, 12), Lines(0, 1, 10)));
        Assert.NotNull(patch); Assert.False(incomplete);
        var area = 0d;
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var p = patch.TriangleIndices.Skip(i).Take(3).Select(n => patch.Positions[n]).ToArray();
            Assert.All(p, point => Assert.InRange(point.X, .976, 1));
            Assert.InRange((p[1] - p[0]).Length, 0, .11);
            area += (p[1] - p[0]).Cross(p[2] - p[0]).Length * .5;
        }
        Assert.InRange(area, .224, .226); // Exact cylindrical area .225, no duplicated/fill region.
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LocalDecomposition_PreservesConcavityHoleAreaAndWinding(bool reversed, bool hole)
    {
        List<(double U, double V)> outer = [(0, 0), (4, 0), (4, 1), (1, 1), (1, 4), (0, 4)];
        List<(double U, double V)> inner = [(.2, .2), (.8, .2), (.8, .8), (.2, .8)];
        if (reversed) { outer.Reverse(); inner.Reverse(); }
        List<List<(double U, double V)>> loops = [outer];
        if (hole) loops.Add(inner);
        var patch = BoundaryConformingTrimTessellator.TryTessellate(new FaceId(42), loops, 0,
            (u, v) => new Point3D(u, v, 0), (_, _) => new Vector3D(0, 0, 1), .25, .25,
            DisplayTessellationOptions.Default, null, out var incomplete,
            grid: (Lines(0, 4, 16), Lines(0, 4, 16)));
        Assert.NotNull(patch); Assert.False(incomplete); Assert.Equal(new FaceId(42), patch.FaceId);
        var area = 0d;
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            var cross = (b - a).Cross(c - a);
            Assert.True(cross.Z < 0); // Common UV convention; wrapper applies resolved BRep orientation.
            area += cross.Length * .5;
            var x = (a.X + b.X + c.X) / 3; var y = (a.Y + b.Y + c.Y) / 3;
            Assert.False(x > 1 && y > 1);
            if (hole) Assert.False(x > .2 && x < .8 && y > .2 && y < .8);
        }
        Assert.Equal(hole ? 6.64 : 7, area, 8);
    }

    [Fact]
    public void WindingStrip_LocalKnotCellsPreventWholeTurnAliasing()
    {
        // Redistributable witness: the real failure class is a many-turn support
        // with a concave almost-rectangular trim, not a vendor-specific solid.
        List<(double U, double V)> outer = [(0, 0), (1, 0), (1, 1), (.6, 1), (.6, .95), (.4, .95), (.4, 1), (0, 1)];
        Point3D At(double u, double v) => new((2 + u) * double.Cos(24 * double.Pi * v),
            (2 + u) * double.Sin(24 * double.Pi * v), 20 * v);
        var patch = BoundaryConformingTrimTessellator.TryTessellate(new FaceId(7), [outer], 0, At,
            (_, v) => new Vector3D(double.Cos(24 * double.Pi * v), double.Sin(24 * double.Pi * v), 0),
            1d / 12, 1d / 192, DisplayTessellationOptions.Default, null, out var incomplete,
            maximumTriangles: 65_536, perceptualNormals: true,
            grid: (Lines(0, 1, 12), Lines(0, 1, 192)));
        Assert.NotNull(patch); Assert.False(incomplete);
        Assert.InRange(patch.TriangleIndices.Count / 3, 1, 65_536);
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var p = patch.TriangleIndices.Skip(i).Take(3).Select(n => patch.Positions[n]).ToArray();
            Assert.InRange(p.Max(x => x.Z) - p.Min(x => x.Z), 0, 20d / 192 + 1e-10);
        }
    }

    [Fact]
    public void SelfCrossedAndCollapsedPolygons_AreRejected()
    {
        foreach (var loop in new List<(double U, double V)>[] {
            [(0, 0), (1, 1), (0, 1), (1, 0)], [(0, 0), (.5, .5), (1, 1)] })
        {
            string? reason = null;
            var patch = BoundaryConformingTrimTessellator.TryTessellate(new FaceId(1), [loop], 0,
                (u, v) => new Point3D(u, v, 0), (_, _) => new Vector3D(0, 0, 1), .1, .1,
                DisplayTessellationOptions.Default, null, out _, reportFailure: r => reason = r);
            Assert.Null(patch); Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }

    private static List<double> Lines(double start, double end, int count) =>
        Enumerable.Range(0, count + 1).Select(i => start + (end - start) * i / count).ToList();
}
