using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Geometry;

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

    [Theory]
    [InlineData("Top", 6d)]
    [InlineData("Bottom", 2d)]
    public void PlansTwoAdjacentConvexSegmentsAsTwoRollsAndOneSphere(string side, double expectedCenterZ)
    {
        var source = $"Selection Corner {{ Source: Bracket.Outer.[South, East] Require: ConnectedChain }} Modify Body {{ EdgeFinish Round {{ Target: Corner On: {side} Kind: Fillet Radius: 2mm EndClearance: 3mm }} }}";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(source, Profile(), "Bracket", out var target, out var radius, out var clearance, out var diagnostic), diagnostic);

        var result = ProfileFilletShellPlanner.TryPlan(Profile(), target!, radius, clearance);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Plan);
        Assert.Equal(2, result.Plan!.Rolls.Count);
        Assert.Equal(ProfileJunctionKind.ConvexProfileJunction, result.Plan.Junction.Classification.Classification);
        Assert.Equal(18d, result.Plan.Junction.Center.X, 8);
        Assert.Equal(2d, result.Plan.Junction.Center.Y, 8);
        Assert.Equal(expectedCenterZ, result.Plan.Junction.Center.Z, 8);
        Assert.Equal(2d, result.Plan.Junction.Radius, 8);
        var sphereToRollA = (result.Plan.Junction.SideAContact - result.Plan.Junction.Center) / result.Plan.Junction.Radius;
        var sphereToRollB = (result.Plan.Junction.SideBContact - result.Plan.Junction.Center) / result.Plan.Junction.Radius;
        Assert.InRange((sphereToRollA + result.Plan.Rolls[0].InwardNormal.ToVector()).Length, 0d, 1e-12d);
        Assert.InRange((sphereToRollB + result.Plan.Rolls[1].InwardNormal.ToVector()).Length, 0d, 1e-12d);
        Assert.Equal(2, result.Body!.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, result.Body.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Sphere));
        Assert.Equal(2, result.Correspondence!.Descendants.Count(item => item.Role is SemanticTopologyRole.StartTerminationFace or SemanticTopologyRole.EndTerminationFace));
        Assert.Contains(result.Correspondence.Descendants, item => item.Role == SemanticTopologyRole.ConvexJunctionPatch);
        Assert.DoesNotContain(result.Correspondence.Descendants, item => item.StableId.Contains("InternalTermination", StringComparison.Ordinal));
    }

    [Fact]
    public void PlansOrthogonalReflexAsTwoRollsAndOneHornTorusAndRejectsThreeSegmentChains()
    {
        const string reflex = "Selection Notch { Source: Bracket.Outer.[Inner, Upright] Require: ConnectedChain } Modify Body { EdgeFinish Round { Target: Notch On: Top Kind: Fillet Radius: 2mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(reflex, LProfile(), "Bracket", out var reflexTarget, out var reflexRadius, out var reflexClearance, out var diagnostic), diagnostic);
        var reflexPlan = ProfileFilletShellPlanner.TryPlan(LProfile(), reflexTarget!, reflexRadius, reflexClearance);
        Assert.True(reflexPlan.Succeeded, string.Join("; ", reflexPlan.Diagnostics));
        var junction = Assert.IsType<ProfileReflexFilletJunctionPlan>(reflexPlan.Plan!.Junction);
        Assert.Equal(ProfileJunctionKind.ReflexProfileJunction, junction.Classification.Classification);
        Assert.Equal(3d * Math.PI / 2d, junction.Classification.MaterialInteriorAngleRadians, 8);
        Assert.Equal(junction.Radius, junction.Torus.MajorRadius, 8);
        Assert.Equal(junction.Radius, junction.Torus.MinorRadius, 8);
        Assert.Equal(2, reflexPlan.Body!.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, reflexPlan.Body.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Torus));
        Assert.Contains(reflexPlan.Correspondence!.Descendants, item => item.Role == SemanticTopologyRole.ReflexJunctionPatch);
        Assert.Equal(2, reflexPlan.Correspondence.Descendants.Count(item => item.Role is SemanticTopologyRole.StartTerminationFace or SemanticTopologyRole.EndTerminationFace));

        const string three = "Selection Three { Source: Bracket.Outer.[South, East, North] Require: ConnectedChain } Modify Body { EdgeFinish Round { Target: Three On: Top Kind: Fillet Radius: 2mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(three, Profile(), "Bracket", out var threeTarget, out var threeRadius, out var threeClearance, out diagnostic), diagnostic);
        Assert.Contains("ProfileBoundaryFilletJunctionTopologyNotMaterialized", ProfileFilletShellPlanner.TryPlan(Profile(), threeTarget!, threeRadius, threeClearance).Diagnostics);
    }

    [Fact]
    public void CurvedWholeLoopFinishesNameTheFirstArcStationAndRequiredExactPlanner()
    {
        const string chamfer = "Modify Body { EdgeFinish TopBreak { Target: Bracket.Outer On: Top Kind: Chamfer Distance: 4mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBind(chamfer, CurvedProfile(), "Bracket", out var chamferTarget, out var distance, out var chamferDiagnostic), chamferDiagnostic);
        var chamferPlan = ProfileBoundaryChamferPlanner.TryPlan(CurvedProfile(), chamferTarget!, distance);
        Assert.False(chamferPlan.Succeeded);
        Assert.StartsWith("ProfileBoundaryChamferArcSegmentPlannerRequired:station=ReflexSmall:scope=Target:segment=ReflexSmallArc:sourceFamily=Arc:material=Reflex:sourceRadius=2:finishDistance=4:radiusRelation=LessThan:missingPlanner=ChamferArcDerivedExtrusionEdge", Assert.Single(chamferPlan.Diagnostics));

        const string fillet = "Modify Body { EdgeFinish TopRound { Target: Bracket.Outer On: Top Kind: Fillet Radius: 4mm } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(fillet, CurvedProfile(), "Bracket", out var filletTarget, out var radius, out var clearance, out var filletDiagnostic), filletDiagnostic);
        var filletPlan = ProfileFilletShellPlanner.TryPlan(CurvedProfile(), filletTarget!, radius, clearance);
        Assert.False(filletPlan.Succeeded);
        Assert.StartsWith("ProfileBoundaryFilletArcSegmentPlannerRequired:station=ReflexSmall:scope=Target:segment=ReflexSmallArc:sourceFamily=Arc:material=Reflex:sourceRadius=2:finishRadius=4:radiusRelation=LessThan:missingPlanner=FilletArcDerivedExtrusionEdge", Assert.Single(filletPlan.Diagnostics));
    }

    [Fact]
    public void ReflexSphereSeamCompatibilityIsExplicitAndNeverReplacesTheToroidalDefault()
    {
        const string compatibility = "Selection Notch { Source: Bracket.Outer.[Inner, Upright] Require: ConnectedChain } Modify Body { EdgeFinish Round { Target: Notch On: Top Kind: Fillet Radius: 2mm ReflexJunction: SphereSeamCompatibility } }";
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBindFillet(compatibility, LProfile(), "Bracket", out var target, out var radius, out var clearance, out var diagnostic), diagnostic);
        Assert.Equal(ProfileReflexJunctionStyle.SphereSeamCompatibility, target!.ReflexJunctionStyle);

        var result = ProfileFilletShellPlanner.TryPlan(LProfile(), target, radius, clearance);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.IsType<ProfileReflexSphereSeamCompatibilityJunctionPlan>(result.Plan!.Junction);
        Assert.Equal(2, result.Body!.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, result.Body.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Sphere));
        Assert.Equal(0, result.Body.Geometry.Surfaces.Count(item => item.Value.Kind == SurfaceGeometryKind.Torus));
        Assert.Contains(result.Correspondence!.ProvenanceChain, item => item == "ReflexSphereSeamCompatibility");

        const string unsupported = "Selection Notch { Source: Bracket.Outer.[Inner, Upright] Require: ConnectedChain } Modify Body { EdgeFinish Round { Target: Notch On: Top Kind: Fillet Radius: 2mm ReflexJunction: Legacy } }";
        Assert.False(ProfileBoundaryChamferSourceBinder.TryBindFillet(unsupported, LProfile(), "Bracket", out _, out _, out _, out diagnostic));
        Assert.Equal("ProfileBoundaryFilletReflexJunctionStyleUnsupported", diagnostic);
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

    private static ResolvedProfile2D CurvedProfile()
    {
        var segments = new ResolvedProfileSegment2D[]
        {
            Segment("Bottom", new LineArcLineSegment2D((0d, 0d), (20d, 0d))),
            Segment("Right", new LineArcLineSegment2D((20d, 0d), (20d, 10d))),
            Segment("TopLead", new LineArcLineSegment2D((20d, 10d), (10d, 10d))),
            Segment("ReflexSmallArc", new LineArcCircularArc2D((10d, 8d), 2d, Math.PI / 2d, -Math.PI / 2d)),
            Segment("Return", new LineArcLineSegment2D((12d, 8d), (0d, 0d)))
        };
        return new ResolvedProfile2D("Bracket", "XY", [new ResolvedProfileLoop2D("Outer", true, segments)], LocalStartDepth: 0d, LocalEndDepth: 8d);

        static ResolvedProfileSegment2D Segment(string name, LineArcProfileCurve2D geometry) =>
            new(name, geometry, new ProfileSegmentProvenance($"profile:Bracket.Outer.{name}", "test", "test", "test", "XY"));
    }
}
