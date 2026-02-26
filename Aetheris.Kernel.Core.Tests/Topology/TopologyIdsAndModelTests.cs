using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Topology;

public sealed class TopologyIdsAndModelTests
{
    [Fact]
    public void TypedIds_CompareAndHashByValue()
    {
        var idA = new VertexId(7);
        var idB = new VertexId(7);
        var idC = new VertexId(8);

        Assert.Equal(idA, idB);
        Assert.Equal(idA.GetHashCode(), idB.GetHashCode());
        Assert.NotEqual(idA, idC);
        Assert.False(VertexId.Invalid.IsValid);
    }

    [Fact]
    public void TopologyModel_CanStoreAndRetrieveById()
    {
        var model = new TopologyModel();
        var vertex = new Vertex(new VertexId(1));

        model.AddVertex(vertex);

        Assert.True(model.TryGetVertex(vertex.Id, out var found));
        Assert.Equal(vertex, found);
        Assert.Equal(vertex, model.GetVertex(vertex.Id));
    }

    [Fact]
    public void TopologyModel_MissingTryGetReturnsFalse_AndGetThrows()
    {
        var model = new TopologyModel();
        var missingId = new EdgeId(99);

        Assert.False(model.TryGetEdge(missingId, out var edge));
        Assert.Null(edge);
        Assert.Throws<KeyNotFoundException>(() => model.GetEdge(missingId));
    }
}
