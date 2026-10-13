using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepBodyCloneTests
{
    [Fact]
    public void CloneExportsIdenticalStepAndPreservesEnumerationOrder()
    {
        var body = BrepPrimitives.CreateBox(4d, 6d, 8d).Value;
        var clone = body.Clone();
        Assert.Equal(body.Topology.Faces.Select(f => f.Id), clone.Topology.Faces.Select(f => f.Id));
        Assert.Equal(body.Topology.Edges.Select(e => e.Id), clone.Topology.Edges.Select(e => e.Id));
        Assert.Equal(body.Bindings.FaceBindings, clone.Bindings.FaceBindings);
        Assert.Equal(body.Geometry.Surfaces.Select(s => s.Key), clone.Geometry.Surfaces.Select(s => s.Key));
        Assert.Equal(Step242Exporter.ExportBody(body).Value, Step242Exporter.ExportBody(clone).Value);
    }

    [Fact]
    public void AddingToACloneLeavesTheOriginalUnchanged()
    {
        var body = BrepPrimitives.CreateBox(4d, 6d, 8d).Value;
        var vertices = body.Topology.Vertices.Count();
        var curves = body.Geometry.Curves.Count();
        var clone = body.Clone();
        clone.Topology.AddVertex(new Vertex(new VertexId(1_000_000)));
        clone.Geometry.AddCurve(new Aetheris.Kernel.Core.Geometry.CurveGeometryId(1_000_000), body.Geometry.Curves.First().Value);
        Assert.Equal(vertices, body.Topology.Vertices.Count());
        Assert.Equal(curves, body.Geometry.Curves.Count());
        Assert.Equal(vertices + 1, clone.Topology.Vertices.Count());
    }
}
