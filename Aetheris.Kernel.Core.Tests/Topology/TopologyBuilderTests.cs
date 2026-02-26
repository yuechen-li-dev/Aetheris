using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Topology;

public sealed class TopologyBuilderTests
{
    [Fact]
    public void Builder_CanCreateMinimalValidSingleFaceGraph()
    {
        var builder = new TopologyBuilder();

        var v1 = builder.AddVertex();
        var v2 = builder.AddVertex();
        var v3 = builder.AddVertex();

        var e1 = builder.AddEdge(v1, v2);
        var e2 = builder.AddEdge(v2, v3);
        var e3 = builder.AddEdge(v3, v1);

        var loopId = builder.AllocateLoopId();
        var c1 = builder.AllocateCoedgeId();
        var c2 = builder.AllocateCoedgeId();
        var c3 = builder.AllocateCoedgeId();

        builder.AddCoedge(new Coedge(c1, e1, loopId, c2, c3, false));
        builder.AddCoedge(new Coedge(c2, e2, loopId, c3, c1, false));
        builder.AddCoedge(new Coedge(c3, e3, loopId, c1, c2, false));

        builder.AddLoop(new Loop(loopId, [c1, c2, c3]));

        var faceId = builder.AddFace([loopId]);
        var shellId = builder.AddShell([faceId]);
        builder.AddBody([shellId]);

        var validation = TopologyGraphValidator.Validate(builder.Model);

        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Builder_CanCreateCubeLikeTopologySkeleton_ValidatorAccepts()
    {
        var builder = new TopologyBuilder();

        var vertices = Enumerable.Range(0, 8).Select(_ => builder.AddVertex()).ToArray();

        var edges = new Dictionary<string, EdgeId>
        {
            ["e0"] = builder.AddEdge(vertices[0], vertices[1]),
            ["e1"] = builder.AddEdge(vertices[1], vertices[2]),
            ["e2"] = builder.AddEdge(vertices[2], vertices[3]),
            ["e3"] = builder.AddEdge(vertices[3], vertices[0]),
            ["e4"] = builder.AddEdge(vertices[4], vertices[5]),
            ["e5"] = builder.AddEdge(vertices[5], vertices[6]),
            ["e6"] = builder.AddEdge(vertices[6], vertices[7]),
            ["e7"] = builder.AddEdge(vertices[7], vertices[4]),
            ["e8"] = builder.AddEdge(vertices[0], vertices[4]),
            ["e9"] = builder.AddEdge(vertices[1], vertices[5]),
            ["e10"] = builder.AddEdge(vertices[2], vertices[6]),
            ["e11"] = builder.AddEdge(vertices[3], vertices[7]),
        };

        var faceLoopEdges = new[]
        {
            new[] { "e0", "e1", "e2", "e3" },
            new[] { "e4", "e5", "e6", "e7" },
            new[] { "e0", "e9", "e4", "e8" },
            new[] { "e1", "e10", "e5", "e9" },
            new[] { "e2", "e11", "e6", "e10" },
            new[] { "e3", "e8", "e7", "e11" },
        };

        var faceIds = new List<FaceId>();

        foreach (var edgeNames in faceLoopEdges)
        {
            var loopId = builder.AllocateLoopId();
            var coedges = edgeNames.Select(_ => builder.AllocateCoedgeId()).ToArray();

            for (var i = 0; i < coedges.Length; i++)
            {
                var next = coedges[(i + 1) % coedges.Length];
                var prev = coedges[(i - 1 + coedges.Length) % coedges.Length];
                var edgeId = edges[edgeNames[i]];
                builder.AddCoedge(new Coedge(coedges[i], edgeId, loopId, next, prev, false));
            }

            builder.AddLoop(new Loop(loopId, coedges));
            faceIds.Add(builder.AddFace([loopId]));
        }

        var shellId = builder.AddShell(faceIds);
        builder.AddBody([shellId]);

        var validation = TopologyGraphValidator.Validate(builder.Model);

        Assert.True(validation.IsSuccess);
    }
}
