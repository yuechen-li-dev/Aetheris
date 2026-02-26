using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Topology;

public sealed class TopologyGraphValidatorTests
{
    [Fact]
    public void Validator_FlagsDanglingFaceLoopReference()
    {
        var model = new TopologyModel();
        model.AddFace(new Face(new FaceId(1), [new LoopId(100)]));

        var result = TopologyGraphValidator.Validate(model);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Error && d.Message.Contains("Face 1 references missing child ID"));
    }

    [Fact]
    public void Validator_FlagsDanglingCoedgeReferences()
    {
        var model = new TopologyModel();

        model.AddLoop(new Loop(new LoopId(1), [new CoedgeId(1)]));
        model.AddCoedge(new Coedge(new CoedgeId(1), new EdgeId(5), new LoopId(1), new CoedgeId(2), new CoedgeId(3), false));

        var result = TopologyGraphValidator.Validate(model);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("references missing edge 5"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("references missing next coedge 2"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("references missing prev coedge 3"));
    }

    [Fact]
    public void Validator_FlagsEdgeMissingVertexReferences()
    {
        var model = new TopologyModel();
        model.AddEdge(new Edge(new EdgeId(1), new VertexId(10), new VertexId(11)));

        var result = TopologyGraphValidator.Validate(model);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("missing start vertex 10"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("missing end vertex 11"));
    }

    [Fact]
    public void Validator_FlagsLoopCoedgeMismatch()
    {
        var model = new TopologyModel();
        model.AddVertex(new Vertex(new VertexId(1)));
        model.AddVertex(new Vertex(new VertexId(2)));
        model.AddEdge(new Edge(new EdgeId(1), new VertexId(1), new VertexId(2)));

        model.AddLoop(new Loop(new LoopId(1), [new CoedgeId(1)]));
        model.AddLoop(new Loop(new LoopId(2), [new CoedgeId(2)]));

        model.AddCoedge(new Coedge(new CoedgeId(1), new EdgeId(1), new LoopId(2), new CoedgeId(2), new CoedgeId(2), false));
        model.AddCoedge(new Coedge(new CoedgeId(2), new EdgeId(1), new LoopId(2), new CoedgeId(2), new CoedgeId(2), false));

        var result = TopologyGraphValidator.Validate(model);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("belongs to loop 2, but is listed on loop 1"));
    }

    [Fact]
    public void Validator_ReturnsSuccessForValidGraph()
    {
        var builder = new TopologyBuilder();
        var v1 = builder.AddVertex();
        var v2 = builder.AddVertex();
        var edgeId = builder.AddEdge(v1, v2);
        var loopId = builder.AllocateLoopId();

        var c1 = builder.AllocateCoedgeId();
        builder.AddCoedge(new Coedge(c1, edgeId, loopId, c1, c1, false));
        builder.AddLoop(new Loop(loopId, [c1]));

        var faceId = builder.AddFace([loopId]);
        var shellId = builder.AddShell([faceId]);
        builder.AddBody([shellId]);

        var result = TopologyGraphValidator.Validate(builder.Model);

        Assert.True(result.IsSuccess);
    }
}
