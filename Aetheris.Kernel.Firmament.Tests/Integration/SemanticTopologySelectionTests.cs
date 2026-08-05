using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class SemanticTopologySelectionTests
{
    [Fact]
    public void ProfileSegments_ResolveToClosedTopBoundaryChain_WithoutGeometryRediscovery()
    {
        var profile = Rectangle();
        var emitted = ResolvedProfile2DValidator.Extrude(profile, 5);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(emitted.Correspondence);
        var request = new SemanticSelectionRequest("selection:top", "Top", "Main", profile.Loops[0].Segments.Select(x => x.Provenance.StableId).ToArray(), SemanticTopologyRole.TopBoundary, SemanticSelectionRequirement.ClosedLoop, "test");

        var result = SemanticTopologySelectionResolver.Resolve(body, correspondence, request);

        Assert.True(result.Succeeded);
        Assert.True(result.IsConnected); Assert.True(result.IsClosed);
        Assert.Equal(4, result.OrderedChain.Count);
        Assert.All(result.Descendants, x => Assert.Equal(SemanticTopologyRole.TopBoundary, x.Role));
    }

    [Fact]
    public void MissingSource_IsTypedFailure()
    {
        var profile = Rectangle(); var emitted = ResolvedProfile2DValidator.Extrude(profile, 5);
        var result = SemanticTopologySelectionResolver.Resolve(emitted.Body!, emitted.Correspondence!, new("selection:missing", "Missing", "Main", ["profile:Main.Outer.Nope"], SemanticTopologyRole.TopBoundary, SemanticSelectionRequirement.OneOrMore, "test"));
        Assert.False(result.Succeeded); Assert.Equal(SemanticSelectionFailure.SemanticSourceNotFound, result.Failure);
    }

    private static ResolvedProfile2D Rectangle()
    {
        var points = new[] { (-10d, -5d), (10d, -5d), (10d, 5d), (-10d, 5d) };
        var names = new[] { "South", "East", "North", "West" };
        var segments = Enumerable.Range(0, 4).Select(i => new ResolvedProfileSegment2D(names[i], new LineArcLineSegment2D(points[i], points[(i + 1) % 4]), new($"profile:Main.Outer.{names[i]}", $"concept:Rectangle.{names[i]}", "test", "test", "XY"))).ToArray();
        return new("Main", "XY", [new ResolvedProfileLoop2D("Outer", true, segments)]);
    }
}
