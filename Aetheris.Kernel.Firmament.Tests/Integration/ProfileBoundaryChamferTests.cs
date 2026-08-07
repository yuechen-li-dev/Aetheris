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
    public void BindsWholeLoopProfileFilletBeforeReportingTheSpecificMaterializationBoundary()
    {
        const string source = "Modify Body { EdgeFinish TopRound { Target: Bracket.Outer On: Top Kind: Fillet Radius: 2mm } }";

        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(source, Profile(), "Bracket", out var target, out var radius, out var clearance, out var diagnostic), diagnostic);
        Assert.Equal(ProfileBoundaryChamferChainKind.ClosedLoop, target!.ChainKind);
        var plan = ProfileStraightEdgeFilletPlanner.TryPlan(Profile(), target, radius, clearance);

        Assert.False(plan.Succeeded);
        Assert.Contains("ProfileBoundaryFilletLoopTopologyNotMaterialized", plan.Diagnostics);
    }

    [Fact]
    public void BindsConnectedFilletSelectionInProfileOrderAndRejectsDisconnectedSelection()
    {
        const string chain = "Selection Corner { Source: Bracket.Outer.[East, South] Require: ConnectedChain } Modify Body { EdgeFinish CornerRound { Target: Corner On: Top Kind: Fillet Radius: 2mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(chain, Profile(), "Bracket", out var target, out _, out _, out var diagnostic), diagnostic);
        Assert.Equal(ProfileBoundaryChamferChainKind.OpenConnectedChain, target!.ChainKind);
        Assert.Equal(["South", "East"], target.SegmentIds);

        const string disconnected = "Selection Bad { Source: Bracket.Outer.[South, North] Require: ConnectedChain } Modify Body { EdgeFinish BadRound { Target: Bad On: Top Kind: Fillet Radius: 2mm } }";
        Assert.False(ProfileBoundaryChamferSourceBinder.TryBindFillet(disconnected, Profile(), "Bracket", out _, out _, out _, out diagnostic));
        Assert.Equal("ProfileBoundaryFilletDisconnectedChain", diagnostic);
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("Bottom")]
    public void PlansFiniteStraightProfileFilletWithExactCylindricalFace(string side)
    {
        var source = $"Modify Body {{ EdgeFinish Round {{ Target: Bracket.Outer.South On: {side} Kind: Fillet Radius: 2mm EndClearance: 3mm }} }}";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(source, Profile(), "Bracket", out var target, out var radius, out var clearance, out var diagnostic), diagnostic);
        var result = ProfileStraightEdgeFilletPlanner.TryPlan(Profile(), target!, radius, clearance);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var counts = result.Body!.Topology.Faces.SelectMany(face => face.LoopIds).SelectMany(id => result.Body.Topology.Loops.Single(loop => loop.Id == id).CoedgeIds).Select(id => result.Body.Topology.Coedges.Single(coedge => coedge.Id == id).EdgeId).GroupBy(id => id).ToDictionary(x => x.Key, x => x.Count());
        Assert.Equal(result.Body.Topology.Edges.Count(), counts.Count);
        Assert.All(counts, item => Assert.True(item.Value == 2, $"edge {item.Key.Value}: {item.Value}"));
        Assert.Contains(result.Correspondence!.Descendants, x => x.Role == SemanticTopologyRole.FilletSurface);
    }

    [Fact]
    public void FilletPlanUsesDocumentedInsetCenterlineAndTypedRejections()
    {
        const string source = "Modify Body { EdgeFinish Round { Target: Bracket.Outer.South On: Top Kind: Fillet Radius: 2mm EndClearance: 3mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(source, Profile(), "Bracket", out var target, out var radius, out var clearance, out var diagnostic), diagnostic);
        var result = ProfileStraightEdgeFilletPlanner.TryPlan(Profile(), target!, radius, clearance);
        Assert.True(result.Succeeded);
        Assert.Equal(3d, result.Plan!.SpanStart.X, 8);
        Assert.Equal(17d, result.Plan.SpanEnd.X, 8);
        Assert.Equal(2d, result.Plan.CylinderCenterlineStart.Y, 8);
        Assert.Equal(6d, result.Plan.CylinderCenterlineStart.Z, 8);

        Assert.False(ProfileBoundaryChamferSourceBinder.TryBindFillet("Modify Body { EdgeFinish Bad { Target: Bracket.Outer.South On: Top Kind: Fillet Radius: 0mm } }", Profile(), "Bracket", out _, out _, out _, out diagnostic));
        Assert.Equal("ProfileBoundaryFilletRadiusMustBePositive", diagnostic);
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet("Modify Body { EdgeFinish Bad { Target: Bracket.Outer On: Top Kind: Fillet Radius: 2mm } }", Profile(), "Bracket", out var loopTarget, out var loopRadius, out var loopClearance, out diagnostic), diagnostic);
        Assert.Contains("ProfileBoundaryFilletLoopTopologyNotMaterialized", ProfileStraightEdgeFilletPlanner.TryPlan(Profile(), loopTarget!, loopRadius, loopClearance).Diagnostics);
        var tooShort = ProfileStraightEdgeFilletPlanner.TryPlan(Profile(), target!, 2d, 10d);
        Assert.False(tooShort.Succeeded);
        Assert.Contains("ProfileBoundaryFilletSegmentTooShort", tooShort.Diagnostics);
        var tooLarge = ProfileStraightEdgeFilletPlanner.TryPlan(Profile(), target!, 8d, 3d);
        Assert.False(tooLarge.Succeeded);
        Assert.Contains("ProfileBoundaryFilletRadiusExceedsHost", tooLarge.Diagnostics);
    }

    [Fact]
    public void FilletComposeCorridorRejectsShaftBeforeComposeMaterialization()
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/FirmamentV2/Canonical/invalid/profile-straight-edge-fillet-shaft-collision.firmament");
        var build = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));
        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, x => x.Message.StartsWith("ProfileBoundaryFilletIntersectsShaft", StringComparison.Ordinal));
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
