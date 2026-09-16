using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidX4Tests
{
    private static HumanoidSurface Triangle() => new(
        "same-topology", "binding", "connectivity",
        [
            new(HumanoidSourcePoseComparison.KnownHipEdgeVertexA, new(0, 0, 0), HumanoidRegionKind.Pelvis, 0),
            new(HumanoidSourcePoseComparison.KnownHipEdgeVertexB, new(3, 0, 0), HumanoidRegionKind.LeftThigh, 1),
            new("third", new(0, 4, 0), HumanoidRegionKind.LeftThigh, 2)
        ],
        [new("face", 0, 1, 2, HumanoidRegionKind.LeftThigh, null)],
        [new("face", 0, 0, 1, 2)], null, [], [],
        new Dictionary<HumanoidRegionKind, HumanoidRegionKind>());

    [Fact]
    public void CorrespondingVertexDiffReportsExactMetricsAndKnownEdge()
    {
        var surface = Triangle();
        var source = surface.Vertices.Select(vertex => vertex.Position).ToArray();
        var aetheris = source.Select(point => point + new Vector3D(1, -2, 2)).ToArray();
        var result = HumanoidSourcePoseComparison.Compare(surface, source, aetheris);

        Assert.Equal(3, result.Vertices.Count);
        Assert.Equal(3, result.Vertices.RmsMm, 10);
        Assert.Equal(3, result.Vertices.P95Mm, 10);
        Assert.Equal(new Vector3D(1, -2, 2), result.Vertices.MeanSignedDeltaMm);
        Assert.Equal(3, result.KnownHipEdge!.RestLengthMm, 10);
        Assert.Equal(3, result.KnownHipEdge.SourceLengthMm, 10);
        Assert.Equal(3, result.KnownHipEdge.AetherisLengthMm, 10);
        Assert.Equal(1, result.SourceEdges.P50BidirectionalRatio, 10);
        Assert.Equal(0, result.AetherisEdges.OrientationReversals);
    }

    [Fact]
    public void DifferentVertexDomainAndOrientationReversalFailOrReportExplicitly()
    {
        var surface = Triangle();
        var source = surface.Vertices.Select(vertex => vertex.Position).ToArray();
        Assert.Throws<InvalidDataException>(() => HumanoidSourcePoseComparison.Compare(surface, source[..2], source));

        var reversed = new[] { source[0], source[2], source[1] };
        var result = HumanoidSourcePoseComparison.Compare(surface, source, reversed);
        Assert.Equal(1, result.AetherisEdges.OrientationReversals);
    }
}
