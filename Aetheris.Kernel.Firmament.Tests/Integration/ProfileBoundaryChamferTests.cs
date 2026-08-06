using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ProfileBoundaryChamferTests
{
    [Fact]
    public void BindsWholeLoopAndPlansExactTopSectionTransition()
    {
        var profile = Profile();
        const string source = "Modify Body { EdgeFinish TopBreak { Target: Bracket.Outer On: Top Kind: Chamfer Distance: 1mm } }";

        Assert.True(ProfileBoundaryChamferSourceBinder.TryBind(source, profile, "Bracket", out var target, out var distance, out var diagnostic), diagnostic);
        Assert.Equal(ProfileBoundaryChamferChainKind.ClosedLoop, target!.ChainKind);
        var result = ProfileBoundaryChamferPlanner.TryPlan(profile, target, distance);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Body);
        Assert.Equal(10, result.Body!.Topology.Faces.Count());
    }

    [Fact]
    public void BindsSingleSegmentAndRejectsDisconnectedSelection()
    {
        var profile = Profile();
        const string single = "Modify Body { EdgeFinish SouthBreak { Target: Bracket.Outer.South On: Top Kind: Chamfer Distance: 1mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBind(single, profile, "Bracket", out var singleTarget, out _, out var singleDiagnostic), singleDiagnostic);
        Assert.Equal(ProfileBoundaryChamferChainKind.SingleSegment, singleTarget!.ChainKind);

        const string disconnected = "Selection Bad { Source: Bracket.Outer.[South, North] Require: ConnectedChain } Modify Body { EdgeFinish BadBreak { Target: Bad On: Top Kind: Chamfer Distance: 1mm } }";
        Assert.False(ProfileBoundaryChamferSourceBinder.TryBind(disconnected, profile, "Bracket", out _, out _, out var diagnostic));
        Assert.Equal("ProfileBoundaryChamferDisconnectedChain", diagnostic);
    }

    [Fact]
    public void ClassifiesConvexAndReflexJunctionsFromLoopMaterialSide()
    {
        var profile = LProfile();

        var junctions = ProfileJunctionClassifier.Classify(profile, profile.Loops.Single()).ToDictionary(x => x.SuccessorSegmentId);

        Assert.Equal(ProfileJunctionKind.ConvexProfileJunction, junctions["East"].Classification);
        Assert.Equal(ProfileJunctionKind.ReflexProfileJunction, junctions["Upright"].Classification);
        Assert.Equal(90d, junctions["East"].MaterialInteriorAngleRadians * 180d / Math.PI, 8);
        Assert.Equal(270d, junctions["Upright"].MaterialInteriorAngleRadians * 180d / Math.PI, 8);
    }

    [Fact]
    public void ClassifiesInnerLoopUsingReversedMaterialSide()
    {
        var outer = Profile().Loops.Single();
        var inner = new ResolvedProfileLoop2D("Hole", false, outer.Segments);
        var profile = new ResolvedProfile2D("Plate", "XY", [outer, inner]);

        var junction = ProfileJunctionClassifier.Classify(profile, inner).Single(x => x.SuccessorSegmentId == "East");

        Assert.Equal(ProfileJunctionKind.ReflexProfileJunction, junction.Classification);
        Assert.Equal(270d, junction.MaterialInteriorAngleRadians * 180d / Math.PI, 8);
    }

    [Fact]
    public void PlansReflexChainAndPreservesClassificationDescendant()
    {
        var profile = LProfile();
        const string source = "Selection Notch { Source: Bracket.Outer.[Inner, Upright] Require: ConnectedChain } Modify Body { EdgeFinish NotchBreak { Target: Notch On: Top Kind: Chamfer Distance: 1mm } }";

        Assert.True(ProfileBoundaryChamferSourceBinder.TryBind(source, profile, "Bracket", out var target, out var distance, out var diagnostic), diagnostic);
        var result = ProfileBoundaryChamferPlanner.TryPlan(profile, target!, distance);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Body);
        Assert.Contains(result.Correspondence!.Descendants, x => x.StableId.Contains("ReflexProfileJunction", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsProfileFilletAtMaterializationBoundary()
    {
        const string source = "Modify Body { EdgeFinish TopRound { Target: Bracket.Outer On: Top Kind: Fillet Radius: 2mm } }";

        Assert.False(ProfileBoundaryChamferSourceBinder.TryBind(source, Profile(), "Bracket", out _, out _, out var diagnostic));

        Assert.Equal("ProfileBoundaryFilletNotMaterialized", diagnostic);
    }

    private static ResolvedProfile2D Profile()
    {
        var points = new[] { (0d, 0d), (20d, 0d), (20d, 10d), (0d, 10d) };
        var names = new[] { "South", "East", "North", "West" };
        var segments = points.Select((point, index) => new ResolvedProfileSegment2D(names[index], new LineArcLineSegment2D(point, points[(index + 1) % points.Length]), new ProfileSegmentProvenance($"profile:Bracket.Outer.{names[index]}", "test", "test", "test", "XY"))).ToArray();
        return new ResolvedProfile2D("Bracket", "XY", [new ResolvedProfileLoop2D("Outer", true, segments)], LocalStartDepth: 0d, LocalEndDepth: 8d);
    }

    private static ResolvedProfile2D LProfile()
    {
        var points = new[] { (0d, 0d), (40d, 0d), (40d, 10d), (10d, 10d), (10d, 40d), (0d, 40d) };
        var names = new[] { "South", "East", "Inner", "Upright", "North", "West" };
        var segments = points.Select((point, index) => new ResolvedProfileSegment2D(names[index], new LineArcLineSegment2D(point, points[(index + 1) % points.Length]), new ProfileSegmentProvenance($"profile:Bracket.Outer.{names[index]}", "test", "test", "test", "XY"))).ToArray();
        return new ResolvedProfile2D("Bracket", "XY", [new ResolvedProfileLoop2D("Outer", true, segments)], LocalStartDepth: 0d, LocalEndDepth: 8d);
    }
}
