using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ProfileEdgeFinishMixedShellPlanTests
{
    [Fact]
    public void SevenStationChamfer_ExportsOneDeterministicMixedAnalyticShell()
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/FirmamentV2/Canonical/valid/profile-edgefinish-chimera-chamfer.firmament");
        var first = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));
        var second = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(x => x.Message)));
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(x => x.Message)));
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
        var imported = Step242Importer.ImportBody(first.Value.Export.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
        Assert.NotNull(imported.Value);
        Assert.Equal(5, imported.Value!.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cone));
        Assert.Equal(0, imported.Value.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots));
    }

    [Fact]
    public void SevenStationChamfer_ProducesOneClosedSourceOrderedPlaneConePlan()
    {
        var (profile, target) = ReleaseCard("profile-edgefinish-chimera-chamfer.firmament", ProfileEdgeFinishKind.Chamfer);

        var result = ProfileEdgeFinishMixedShellPlanner.TryPlan(profile, target, ProfileEdgeFinishKind.Chamfer, 4d);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        var plan = Assert.IsType<ProfileEdgeFinishMixedShellPlan>(result.Plan);
        Assert.Equal(profile.Loops.Single().Segments.Select(x => x.Name), plan.OrderedPatches.Select(x => x.SegmentId));
        Assert.Equal(17, plan.OrderedPatches.Count);
        Assert.Equal(12, plan.OrderedPatches.OfType<PlanarChamferPatch>().Count());
        Assert.Equal(5, plan.OrderedPatches.OfType<ConicalChamferPatch>().Count());
        var apex = Assert.Single(plan.OrderedPatches.OfType<ConicalChamferPatch>(), x => x.SegmentId == "ConvexMediumArc");
        Assert.Equal(ProfileEdgeFinishRegularity.BoundedDegenerate, apex.Regularity);
        Assert.Equal(0d, apex.InsetRadius);
        Assert.Contains("ConeApex:ConvexMediumArc:vertex=ConvexMediumArc:cap", plan.DegenerateVertices);
        Assert.Equal(17, plan.OrderedSeams.Count);
        Assert.Equal(10, plan.OrderedSeams.OfType<PlaneConeSeam>().Count());
        Assert.Equal("LeftSide", plan.OrderedSeams[^1].PredecessorPatchId.Split(':').Last());
        Assert.Equal("Bottom", plan.OrderedSeams[^1].SuccessorPatchId.Split(':').Last());
        Assert.All(plan.OrderedSeams, seam => Assert.True(seam.TraversesWithCurveParameter));
        Assert.DoesNotContain(plan.OrderedPatches.SelectMany(x => x.SemanticDescendants), x => x.Contains("Termination", StringComparison.Ordinal));
    }

    [Fact]
    public void RoundedConcaveChamfer_OwnsOneConicalFrustumSectorInsteadOfAPlanarMiter()
    {
        var (profile, target) = ReleaseCard("profile-edgefinish-chimera-chamfer.firmament", ProfileEdgeFinishKind.Chamfer);
        var planned = ProfileEdgeFinishMixedShellPlanner.TryPlan(profile, target, ProfileEdgeFinishKind.Chamfer, 4d);

        Assert.True(planned.Succeeded, string.Join(Environment.NewLine, planned.Diagnostics));
        var plan = Assert.IsType<ProfileEdgeFinishMixedShellPlan>(planned.Plan);
        var reflexSmall = Assert.Single(plan.OrderedPatches.OfType<ConicalChamferPatch>(), x => x.SegmentId == "ReflexSmallArc");
        Assert.Equal(ConicalChamferTrimTopology.FrustumSector, reflexSmall.TrimTopology);
        Assert.Equal(2d, reflexSmall.SourceRadius);
        Assert.Equal(6d, reflexSmall.InsetRadius);

        var emitted = ProfileEdgeFinishMixedShellMaterializer.TryMaterializeChamfer(profile, target, plan);
        Assert.True(emitted.Succeeded, string.Join(Environment.NewLine, emitted.Diagnostics));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(emitted.Correspondence);
        var coneFaceId = Assert.Single(correspondence.Descendants,
            x => x.Kind == "Face" && x.StableId.EndsWith(":chamfer:ReflexSmallArc", StringComparison.Ordinal)).Face;
        Assert.True(coneFaceId.HasValue);
        var coneFace = body.Topology.GetFace(coneFaceId.Value);
        var coneLoop = body.Topology.GetLoop(Assert.Single(coneFace.LoopIds));
        var boundaryCurves = coneLoop.CoedgeIds
            .Select(body.Topology.GetCoedge)
            .Select(x => body.Geometry.GetCurve(body.Bindings.GetEdgeBinding(x.EdgeId).CurveGeometryId))
            .ToArray();

        Assert.Equal(2, boundaryCurves.Count(x => x.Kind == CurveGeometryKind.Circle3));
        Assert.Equal(2, boundaryCurves.Count(x => x.Kind == CurveGeometryKind.Line3));
        Assert.Equal([2d, 6d], boundaryCurves
            .Where(x => x.Kind == CurveGeometryKind.Circle3)
            .Select(x => Assert.IsType<Aetheris.Kernel.Core.Geometry.Curves.Circle3Curve>(x.Circle3).Radius)
            .Order()
            .ToArray());
        Assert.Contains(correspondence.Descendants, x => x.Kind == "Edge" && x.GeometryPreview == "PlaneConeSeam:Line:sameSense=True");
    }

    [Fact]
    public void SevenStationFillet_ProducesOneClosedSourceOrderedCylinderTorusSpherePlan()
    {
        var (profile, target) = ReleaseCard("profile-edgefinish-chimera-fillet.firmament", ProfileEdgeFinishKind.Fillet);

        var result = ProfileEdgeFinishMixedShellPlanner.TryPlan(profile, target, ProfileEdgeFinishKind.Fillet, 4d);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        var plan = Assert.IsType<ProfileEdgeFinishMixedShellPlan>(result.Plan);
        Assert.Equal(12, plan.OrderedPatches.OfType<CylindricalFilletPatch>().Count());
        Assert.Single(plan.OrderedPatches.OfType<SphericalFilletPatch>());
        var tori = plan.OrderedPatches.OfType<ToroidalFilletPatch>().OrderBy(x => x.SegmentId).ToArray();
        Assert.Equal(4, tori.Length);
        Assert.Equal(ProfileEdgeFinishTorusRegime.Horn, Assert.Single(tori, x => x.SegmentId == "ConvexLargeArc").Regime);
        Assert.All(tori.Where(x => x.SegmentId.StartsWith("Reflex", StringComparison.Ordinal)), x => Assert.Equal(ProfileEdgeFinishTorusRegime.Ring, x.Regime));
        Assert.Contains("SphereLimit:ConvexMediumArc:vertex=ConvexMediumArc:cap", plan.DegenerateVertices);
        Assert.Equal(8, plan.OrderedSeams.OfType<CylinderTorusSeam>().Count());
        Assert.Equal(2, plan.OrderedSeams.OfType<CylinderSphereSeam>().Count());
        Assert.All(plan.OrderedSeams.Where(x => x is CylinderTorusSeam or CylinderSphereSeam), x => Assert.Equal("Circle", x.CurveFamily));
        Assert.DoesNotContain(plan.OrderedPatches.SelectMany(x => x.SemanticDescendants), x => x.Contains("Termination", StringComparison.Ordinal));
    }

    [Fact]
    public void SevenStationFillet_ConsumesContactPlanAndEmitsOneManifoldAnalyticShell()
    {
        var (profile, target) = ReleaseCard("profile-edgefinish-chimera-fillet.firmament", ProfileEdgeFinishKind.Fillet);
        var mixed = ProfileEdgeFinishMixedShellPlanner.TryPlan(profile, target, ProfileEdgeFinishKind.Fillet, 4d);

        Assert.True(mixed.Succeeded, string.Join(Environment.NewLine, mixed.Diagnostics));
        var contacts = ProfileFilletContactShellPlanner.TryPlan(profile, target, Assert.IsType<ProfileEdgeFinishMixedShellPlan>(mixed.Plan));

        Assert.True(contacts.Succeeded, string.Join(Environment.NewLine, contacts.Diagnostics));
        var contactPlan = Assert.IsType<ProfileFilletContactShellPlan>(contacts.Plan);
        Assert.Equal(17, contactPlan.SideContactChains.Count);
        Assert.Equal(17, contactPlan.SourceSideTrims.Count);
        Assert.True(ProfileFilletContactGraphValidator.Validate(contactPlan).Succeeded);

        var emitted = ProfileFilletContactShellMaterializer.TryMaterialize(profile, target,
            Assert.IsType<ProfileEdgeFinishMixedShellPlan>(mixed.Plan), contactPlan);

        Assert.True(emitted.Succeeded, string.Join(Environment.NewLine, emitted.Diagnostics));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        AssertClosedManifold(body);
        Assert.Equal(0, body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots));
        Assert.Equal(6, body.Geometry.Curves.Count(curve => curve.Value.Kind == CurveGeometryKind.Ellipse3));
        var ellipseEdges = body.Bindings.EdgeBindings
            .Where(binding => body.Geometry.GetCurve(binding.CurveGeometryId).Kind == CurveGeometryKind.Ellipse3)
            .ToArray();
        Assert.Equal(6, ellipseEdges.Length);
        foreach (var ellipseEdge in ellipseEdges)
        {
            var adjacentLoops = body.Topology.Coedges.Where(coedge => coedge.EdgeId == ellipseEdge.EdgeId).Select(coedge => coedge.LoopId).ToHashSet();
            var adjacentFaces = body.Topology.Faces.Where(face => face.LoopIds.Any(adjacentLoops.Contains)).ToArray();
            Assert.Equal(2, adjacentFaces.Length);
            Assert.All(adjacentFaces, face => Assert.Equal(SurfaceGeometryKind.Cylinder,
                body.Geometry.GetSurface(body.Bindings.GetFaceBinding(face.Id).SurfaceGeometryId).Kind));
        }
        Assert.Equal(0, Assert.IsType<SemanticTopologyCorrespondence>(emitted.Correspondence).Descendants.Count(descendant =>
            descendant.Role is SemanticTopologyRole.StartTerminationFace or SemanticTopologyRole.EndTerminationFace));
    }

    [Theory]
    [InlineData("profile-edgefinish-chimera-fillet.firmament", 1, 5)]
    [InlineData("profile-edgefinish-chimera-reflex-sphere-compat.firmament", 2, 4)]
    public void SevenStationFillet_ExportsDeterministicallyWithExpectedSphereTorusDelta(string fixture, int spheres, int tori)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixture}");
        var first = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));
        var second = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
        var imported = Step242Importer.ImportBody(first.Value.Export.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(imported.Value);
        AssertClosedManifold(body);
        Assert.Equal(spheres, body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Sphere));
        Assert.Equal(tori, body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Torus));
        Assert.Equal(6, body.Geometry.Curves.Count(curve => curve.Value.Kind == CurveGeometryKind.Ellipse3));
        Assert.Equal(0, body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots));
    }

    [Theory]
    [InlineData("profile-edgefinish-chimera-fillet.firmament")]
    [InlineData("profile-edgefinish-chimera-reflex-sphere-compat.firmament")]
    public void SevenStationFillet_ReimportedMassIsNonAuthoritativeSanityEvidence(string fixture)
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixture}");
        var built = FirmamentBuildAndExport.Run(source, Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.step"));
        Assert.True(built.IsSuccess, string.Join(Environment.NewLine, built.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var imported = Step242Importer.ImportBody(built.Value.Export.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var mass = BrepMassProperties.Evaluate(Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(imported.Value));
        Assert.False(mass.IsAuthoritativeForVolumeAssertion);
        Assert.True(mass.IsTessellatedSanityEstimate);
        var sanityComparison = FirmamentV2VolumeAssertionComparer.Compare(
            new FirmamentV2VolumeAssertion("sanity", "Body", 1d, 0d, null, new FirmamentV2SourceSpan(0, 0)),
            mass);
        Assert.False(sanityComparison.MeasurementAuthoritative);
        Assert.False(sanityComparison.Passed);
    }

    private static (ResolvedProfile2D Profile, ProfileBoundaryChamferTarget Target) ReleaseCard(string fixture, ProfileEdgeFinishKind kind)
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/FirmamentV2/Canonical/valid/{fixture}"));
        var parsed = ProfileAuthoringParser.Parse(source);
        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        var bound = kind == ProfileEdgeFinishKind.Chamfer
            ? ProfileBoundaryChamferSourceBinder.TryBind(source, profile, profile.Name, out var target, out _, out var diagnostic)
            : ProfileBoundaryChamferSourceBinder.TryBindFillet(source, profile, profile.Name, out target, out _, out _, out diagnostic);
        Assert.True(bound, diagnostic);
        return (profile, Assert.IsType<ProfileBoundaryChamferTarget>(target));
    }

    private static void AssertClosedManifold(Aetheris.Kernel.Core.Brep.BrepBody body)
    {
        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        foreach (var edge in body.Topology.Edges)
        {
            var uses = body.Topology.Coedges.Where(coedge => coedge.EdgeId == edge.Id).ToArray();
            Assert.Equal(2, uses.Length);
            Assert.NotEqual(uses[0].IsReversed, uses[1].IsReversed);
        }
    }
}
