using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class SurfaceMeshObjExporterTests
{
    [Fact]
    public void Export_PreservesQuadTriangleAndBoundaryPolygon_WithoutTriangleLowering()
    {
        var export = SurfaceMeshObjExporter.Export(CreateDocument());

        Assert.Equal(4, export.PolygonCount);
        Assert.Equal(1, export.QuadCount);
        Assert.Equal(2, export.TriangleCount);
        Assert.Equal(1, export.BoundaryPolygonCount);
        Assert.Equal(5, export.VertexCount); // shared geometric positions, not one vertex per face corner
        Assert.Equal(4, export.Text.Split('\n').Count(line => line.StartsWith("f ", StringComparison.Ordinal)));
        Assert.Contains("f 1/1/1 2/2/1 3/3/1 4/4/1", export.Text);
        Assert.Contains("g face_7 semantic_Head.TopFlat", export.Text);
    }

    [Fact]
    public void Export_UsesExactCornerNormalsAndStableBytes()
    {
        var first = SurfaceMeshObjExporter.Export(CreateDocument());
        var second = SurfaceMeshObjExporter.Export(CreateDocument());

        Assert.Equal(first.DeterministicHash, second.DeterministicHash);
        Assert.Equal(first.Text, second.Text);
        Assert.Contains("vn 0 0 1", first.Text);
        Assert.Contains("vn 0 -1 0", first.Text);
        Assert.Equal(2, first.NormalCount); // shared position 1 has separate plane-support corner normals.
    }

    private static SurfaceMeshDocument CreateDocument()
    {
        var xy = new PlaneSurface(new Point3D(0d, 0d, 0d), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var xz = new PlaneSurface(new Point3D(0d, 0d, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        return new SurfaceMeshDocument(
            [
                new SurfaceMeshVertex(0, new Point3D(0d, 0d, 0d)), new SurfaceMeshVertex(1, new Point3D(1d, 0d, 0d)),
                new SurfaceMeshVertex(2, new Point3D(1d, 1d, 0d)), new SurfaceMeshVertex(3, new Point3D(0d, 1d, 0d)),
                new SurfaceMeshVertex(4, new Point3D(0d, 0d, 1d))
            ],
            [
                new SurfacePatch(new FaceId(7), new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, Plane: xy), [], [new QuadCell([0, 1, 2, 3]), new TriangleCell([0, 2, 3]), new BoundaryPolygonCell([0, 1, 2, 3, 4])], true, "Head.TopFlat"),
                new SurfacePatch(new FaceId(8), new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, Plane: xz), [], [new TriangleCell([0, 4, 1])], true)
            ],
            [],
            new SurfaceMeshMetrics(2, 0, 4, 1, 3, 2, 0, 0d, 0, 0d, 0d, "fixture"));
    }
}
