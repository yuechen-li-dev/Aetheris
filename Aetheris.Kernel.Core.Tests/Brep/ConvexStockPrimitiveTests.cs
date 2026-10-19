using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class ConvexStockPrimitiveTests
{
    [Theory]
    [InlineData(4, 4, 6)]
    [InlineData(6, 8, 12)]
    [InlineData(8, 6, 12)]
    [InlineData(12, 20, 30)]
    [InlineData(20, 12, 30)]
    public void RegularStocksAndUniformFilletsHaveClosedSharedTopology(int faces, int vertices, int edges)
    {
        foreach (var radius in new[] { 0d, 1d })
        {
            var result = ConvexStockPrimitives.CreateRegular(faces, 20, radius);
            Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
            var body = result.Value;
            Assert.Equal(radius == 0 ? faces : faces + edges + vertices, body.Topology.Faces.Count());
            if (radius == 0)
            {
                Assert.Equal(vertices, body.Topology.Vertices.Count()); Assert.Equal(edges, body.Topology.Edges.Count());
                foreach (var e in body.Topology.Edges)
                {
                    Assert.True(body.TryGetVertexPoint(e.StartVertexId, out var a)); Assert.True(body.TryGetVertexPoint(e.EndVertexId, out var b));
                    Assert.Equal(20, (a - b).Length, 8);
                }
            }
            else
            {
                Assert.Equal(edges, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
                Assert.Equal(vertices, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Sphere));
            }
            Check(body);
            var step = Step242Exporter.ExportBody(body);
            Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics));
            var imported = Step242Importer.ImportBody(step.Value);
            Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
            Check(imported.Value);
            Assert.Equal(body.Topology.Faces.Count(), imported.Value.Topology.Faces.Count());
            var mesh = BrepDisplayTessellator.Tessellate(imported.Value);
            Assert.True(mesh.IsSuccess, string.Join("\n", mesh.Diagnostics));
            Assert.Equal(body.Topology.Faces.Count(), mesh.Value.FacePatches.Count(p => p.TriangleIndices.Count > 0));
        }
    }

    [Fact]
    public void WholeBoxRoundingPreservesExtentsAndHasEveryCorner()
    {
        var result = ConvexStockPrimitives.CreateRoundedBox(20, 30, 40, 2);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var body = result.Value; Check(body);
        Assert.Equal(26, body.Topology.Faces.Count());
        var points = body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).ToArray();
        Assert.Equal(-10, points.Min(p => p.X), 8); Assert.Equal(10, points.Max(p => p.X), 8);
        Assert.Equal(-15, points.Min(p => p.Y), 8); Assert.Equal(15, points.Max(p => p.Y), 8);
        Assert.Equal(-20, points.Min(p => p.Z), 8); Assert.Equal(20, points.Max(p => p.Z), 8);
        var tooLarge = ConvexStockPrimitives.CreateRoundedBox(20, 30, 40, 10);
        Assert.False(tooLarge.IsSuccess); Assert.Contains(tooLarge.Diagnostics, d => d.Message.StartsWith("UniformFilletRadiusTooLarge:"));
        var mesh = BrepDisplayTessellator.TessellateSurfaceMeshIr(body);
        Assert.True(mesh.IsSuccess, string.Join("\n", mesh.Diagnostics));
        var expected = 16d * 26 * 36 + 4d * (16 * 26 + 26 * 36 + 16 * 36)
            + double.Pi * 4 * (16 + 26 + 36) + 4d / 3 * double.Pi * 8;
        Assert.InRange(SignedMeshVolume(mesh.Value) / expected, .997, 1.001);
    }

    [Theory]
    [InlineData(18, 12, 25)]
    [InlineData(5, 9, 3)]
    [InlineData(10, 10, 10)]
    [InlineData(10, 10, 20)]
    public void EllipsoidIsAnExactQuadricAcrossTheWholeDomainAndStepRoundTrip(double radiusX, double radiusY, double radiusZ)
    {
        var result = EllipsoidPrimitive.Create(radiusX, radiusY, radiusZ); Assert.True(result.IsSuccess);
        var step = Step242Exporter.ExportBody(result.Value); Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics));
        var import = Step242Importer.ImportBody(step.Value); Assert.True(import.IsSuccess, string.Join("\n", import.Diagnostics));
        foreach (var body in new[] { result.Value, import.Value })
        {
            Check(body);
            foreach (var entry in body.Geometry.Surfaces)
            {
                Assert.True(entry.Value.Kind == SurfaceGeometryKind.Ellipsoid
                    || radiusX == radiusY && radiusY == radiusZ && entry.Value.Kind == SurfaceGeometryKind.Sphere);
                var surface = entry.Value.Ellipsoid?.ToStepPatch();
                if (surface is not null) Assert.NotNull(surface.Weights);
                for (var u = 0; u <= 28; u++) for (var v = 0; v <= 28; v++)
                {
                    var p = surface is not null ? surface.Evaluate(u / 28d, v / 28d) : entry.Value.Sphere!.Value.Evaluate(u / 28d, v / 28d);
                    Assert.Equal(1, p.X * p.X / (radiusX * radiusX) + p.Y * p.Y / (radiusY * radiusY) + p.Z * p.Z / (radiusZ * radiusZ), 11);
                }
            }
            var mesh = BrepDisplayTessellator.Tessellate(body);
            Assert.True(mesh.IsSuccess, string.Join("\n", mesh.Diagnostics));
            Assert.Equal(8, mesh.Value.FacePatches.Count(p => p.TriangleIndices.Count > 0));
            var sharedMesh = BrepDisplayTessellator.TessellateSurfaceMeshIr(body);
            Assert.True(sharedMesh.IsSuccess, string.Join("\n", sharedMesh.Diagnostics));
            Assert.InRange(SignedMeshVolume(sharedMesh.Value) / (4d / 3 * double.Pi * radiusX * radiusY * radiusZ), .995, 1.001);
        }
    }

    [Fact]
    public void EllipsoidRecoveryRequiresItsExactNetAndWorksInARigidFrame()
    {
        var patch = new Aetheris.Kernel.Core.Geometry.Surfaces.EllipsoidSurface(new(13, -5, 7),
            Direction3D.Create(new Vector3D(1, 1, 1)), Direction3D.Create(new Vector3D(1, -1, 0)), 18, 12, 25, true);
        Assert.True(Aetheris.Kernel.Core.Geometry.Surfaces.EllipsoidSurface.TryRecover(patch.ToStepPatch(), out var recovered));
        Assert.Equal(0, (patch.Center - recovered.Center).Length, 8);
        Assert.Equal(18, recovered.RadiusX, 8); Assert.Equal(12, recovered.RadiusY, 8); Assert.Equal(25, recovered.RadiusZ, 8);
        var exact = patch.ToStepPatch(); var altered = exact.ControlPoints.Select(row => row.ToArray()).ToArray();
        altered[1][1] += new Vector3D(.001, 0, 0);
        var freeform = new Aetheris.Kernel.Core.Geometry.Surfaces.BSplineSurfaceWithKnots(2, 2, altered,
            "UNSPECIFIED", false, false, false, [3, 3], [3, 3], [0, 1], [0, 1], "UNSPECIFIED", exact.Weights);
        Assert.False(Aetheris.Kernel.Core.Geometry.Surfaces.EllipsoidSurface.TryRecover(freeform, out _));
    }

    private static double SignedMeshVolume(DisplayTessellationResult mesh)
    {
        var volume = 0d;
        foreach (var patch in mesh.FacePatches)
            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[i]] - Point3D.Origin;
                var b = patch.Positions[patch.TriangleIndices[i + 1]] - Point3D.Origin;
                var c = patch.Positions[patch.TriangleIndices[i + 2]] - Point3D.Origin;
                volume += a.Dot(b.Cross(c)) / 6;
            }
        return volume;
    }

    private static void Check(BrepBody body)
    {
        var preflight = BrepExportPreflight.Validate(body);
        Assert.True(preflight.IsValid, string.Join("\n", preflight.Diagnostics));
        foreach (var edge in body.Topology.Edges)
        {
            var uses = body.Topology.Coedges.Where(c => c.EdgeId == edge.Id).ToArray();
            Assert.Equal(2, uses.Length); Assert.NotEqual(uses[0].IsReversed, uses[1].IsReversed);
        }
        foreach (var loop in body.Topology.Loops)
        {
            var uses = loop.CoedgeIds.Select(body.Topology.GetCoedge).ToArray();
            for (var i = 0; i < uses.Length; i++)
            {
                var use = uses[i]; var next = uses[(i + 1) % uses.Length];
                var a = body.Topology.GetEdge(use.EdgeId); var b = body.Topology.GetEdge(next.EdgeId);
                Assert.Equal(use.IsReversed ? a.StartVertexId : a.EndVertexId, next.IsReversed ? b.EndVertexId : b.StartVertexId);
            }
        }
    }
}
