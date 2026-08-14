using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Surgery;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Surgery;

public sealed class BrepSurgeryTests
{
    [Fact]
    public void CreateKnownLoop_ConstructsClosedRectangularCycleWithDeterministicLinks()
    {
        var builder = new TopologyBuilder();
        var vertices = Enumerable.Range(0, 4).Select(_ => builder.AddVertex()).ToArray();
        var edges = new[]
        {
            builder.AddEdge(vertices[0], vertices[1]),
            builder.AddEdge(vertices[1], vertices[2]),
            builder.AddEdge(vertices[2], vertices[3]),
            builder.AddEdge(vertices[3], vertices[0]),
        };

        var result = BrepLoopBuilder.CreateKnownLoop(builder, edges.Select(BrepEdgeUse.Forward).ToArray());

        Assert.True(result.IsSuccess);
        var loop = builder.Model.GetLoop(result.Value);
        Assert.Equal(4, loop.CoedgeIds.Count);
        for (var index = 0; index < loop.CoedgeIds.Count; index++)
        {
            var coedge = builder.Model.GetCoedge(loop.CoedgeIds[index]);
            Assert.Equal(loop.CoedgeIds[(index + 1) % 4], coedge.NextCoedgeId);
            Assert.Equal(loop.CoedgeIds[(index + 3) % 4], coedge.PrevCoedgeId);
        }
    }

    [Fact]
    public void CreateKnownLoop_RejectsOpenAndRepeatedDirectedUsesWithoutMutation()
    {
        var builder = new TopologyBuilder();
        var v1 = builder.AddVertex();
        var v2 = builder.AddVertex();
        var v3 = builder.AddVertex();
        var e1 = builder.AddEdge(v1, v2);
        var e2 = builder.AddEdge(v3, v1);

        var open = BrepLoopBuilder.CreateKnownLoop(builder, [BrepEdgeUse.Forward(e1), BrepEdgeUse.Forward(e2)]);
        var repeated = BrepLoopBuilder.CreateKnownLoop(builder, [BrepEdgeUse.Forward(e1), BrepEdgeUse.Forward(e1)]);

        Assert.False(open.IsSuccess);
        Assert.Contains(open.Diagnostics, diagnostic => diagnostic.Message.Contains("open", StringComparison.OrdinalIgnoreCase));
        Assert.False(repeated.IsSuccess);
        Assert.Contains(repeated.Diagnostics, diagnostic => diagnostic.Message.Contains("repeats", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(builder.Model.Loops);
        Assert.Empty(builder.Model.Coedges);
    }

    [Fact]
    public void CreateKnownFace_PreservesCallerSuppliedOuterThenInnerLoopRoles()
    {
        var builder = new TopologyBuilder();
        var vertices = Enumerable.Range(0, 4).Select(_ => builder.AddVertex()).ToArray();
        var outerEdges = new[]
        {
            builder.AddEdge(vertices[0], vertices[1]),
            builder.AddEdge(vertices[1], vertices[2]),
            builder.AddEdge(vertices[2], vertices[3]),
            builder.AddEdge(vertices[3], vertices[0]),
        };
        var periodicVertex = builder.AddVertex();
        var circularTrimEdge = builder.AddEdge(periodicVertex, periodicVertex);

        var result = BrepFaceBuilder.CreateKnownFace(
            builder,
            outerEdges.Select(BrepEdgeUse.Forward).ToArray(),
            [[BrepEdgeUse.Reversed(circularTrimEdge)]]);

        Assert.True(result.IsSuccess);
        var face = builder.Model.GetFace(result.Value);
        Assert.Equal(2, face.LoopIds.Count);
        var innerCoedge = builder.Model.GetCoedge(builder.Model.GetLoop(face.LoopIds[1]).CoedgeIds.Single());
        Assert.True(innerCoedge.IsReversed);
    }

    [Fact]
    public void CreateClosedBody_AssemblesKnownCubeAndRejectsOpenFaceSet()
    {
        var builder = new TopologyBuilder();
        var vertices = Enumerable.Range(0, 8).Select(_ => builder.AddVertex()).ToArray();
        var edges = new[]
        {
            builder.AddEdge(vertices[0], vertices[1]), builder.AddEdge(vertices[1], vertices[2]),
            builder.AddEdge(vertices[2], vertices[3]), builder.AddEdge(vertices[3], vertices[0]),
            builder.AddEdge(vertices[4], vertices[5]), builder.AddEdge(vertices[5], vertices[6]),
            builder.AddEdge(vertices[6], vertices[7]), builder.AddEdge(vertices[7], vertices[4]),
            builder.AddEdge(vertices[0], vertices[4]), builder.AddEdge(vertices[1], vertices[5]),
            builder.AddEdge(vertices[2], vertices[6]), builder.AddEdge(vertices[3], vertices[7]),
        };

        var faceUseSets = new IReadOnlyList<BrepEdgeUse>[]
        {
            [BrepEdgeUse.Forward(edges[0]), BrepEdgeUse.Forward(edges[1]), BrepEdgeUse.Forward(edges[2]), BrepEdgeUse.Forward(edges[3])],
            [BrepEdgeUse.Reversed(edges[7]), BrepEdgeUse.Reversed(edges[6]), BrepEdgeUse.Reversed(edges[5]), BrepEdgeUse.Reversed(edges[4])],
            [BrepEdgeUse.Forward(edges[8]), BrepEdgeUse.Forward(edges[4]), BrepEdgeUse.Reversed(edges[9]), BrepEdgeUse.Reversed(edges[0])],
            [BrepEdgeUse.Forward(edges[9]), BrepEdgeUse.Forward(edges[5]), BrepEdgeUse.Reversed(edges[10]), BrepEdgeUse.Reversed(edges[1])],
            [BrepEdgeUse.Forward(edges[10]), BrepEdgeUse.Forward(edges[6]), BrepEdgeUse.Reversed(edges[11]), BrepEdgeUse.Reversed(edges[2])],
            [BrepEdgeUse.Forward(edges[11]), BrepEdgeUse.Forward(edges[7]), BrepEdgeUse.Reversed(edges[8]), BrepEdgeUse.Reversed(edges[3])],
        };
        var faces = faceUseSets.Select(uses => BrepFaceBuilder.CreateKnownFace(builder, uses)).ToArray();
        Assert.All(faces, face => Assert.True(face.IsSuccess));

        var openResult = BrepShellAssembler.CreateClosedBody(builder, faces.Take(5).Select(face => face.Value).ToArray());
        var closedResult = BrepShellAssembler.CreateClosedBody(builder, faces.Select(face => face.Value).ToArray());

        Assert.False(openResult.IsSuccess);
        Assert.Contains(openResult.Diagnostics, diagnostic => diagnostic.Message.Contains("expected exactly 2", StringComparison.Ordinal));
        Assert.True(closedResult.IsSuccess);
        Assert.Single(builder.Model.Shells);
        Assert.Single(builder.Model.Bodies);
    }

    [Fact]
    public void ValidateBody_RejectsNonFiniteVertexGeometryWithTypedDiagnostic()
    {
        var source = BrepPrimitives.CreateBox(2d, 3d, 4d).Value;
        var points = source.Topology.Vertices.ToDictionary(
            vertex => vertex.Id,
            vertex => source.TryGetVertexPoint(vertex.Id, out var point) ? point : Point3D.Origin);
        points[points.Keys.OrderBy(id => id.Value).First()] = new Point3D(double.NaN, 0d, 0d);
        var invalid = new BrepBody(source.Topology, source.Geometry, source.Bindings, points);

        var result = BrepSurgeryValidation.ValidateBody(invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Source == "Brep.Surgery.Validation"
            && diagnostic.Message.Contains("finite geometry point", StringComparison.Ordinal));
    }
}
