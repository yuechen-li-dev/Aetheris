using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Visualization;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ComposedHoleFamilyTests
{
    private static string Source => FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/ComposeHoleFamilies/l-bracket-countersink.firmament");

    [Theory]
    [InlineData(90d)]
    [InlineData(82d)]
    [InlineData(120d)]
    public void CountersinkOnTracedL_IsAnExactClosedConeAndShaft(double angle)
    {
        var source = Source.Replace("90deg", FormattableString.Invariant($"{angle}deg"));
        var result = Admit(source, true);
        var body = Assert.IsType<BrepBody>(result.RuntimeBody);
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var hole = Assert.Single(stack.Feature.CountersinkHoles!);
        var coneDepth = 3d / Math.Tan(angle * Math.PI / 360d);
        var expected = 28800d - Math.PI * 9d * 12d - Math.PI * coneDepth * 12d;
        Assert.Equal(expected, stack.AnalyticVolume, 8);
        Assert.Equal(coneDepth, hole.ConeDepth, 10);
        CheckBody(body, expected, angle);
        var imported = Step242Importer.ImportBody(result.StepText);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        CheckBody(imported.Value, expected, angle);
        Assert.Contains(result.RuntimeCorrespondence!.Descendants, d => d.SourceStableId == hole.StableId && d.Role == SemanticTopologyRole.HoleCountersinkWallFace);
        Assert.Contains(result.RuntimeCorrespondence.Descendants, d => d.SourceStableId == hole.StableId && d.Role == SemanticTopologyRole.HoleWallFace);
        Assert.Contains(result.RuntimeCorrespondence.Descendants, d => d.SourceStableId == hole.StableId && d.Role == SemanticTopologyRole.HoleEntryLoop);
        Assert.Equal(result.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Theory]
    [InlineData("Point2(20mm, -10mm)", "Point2(20mm, -17mm)", "ComposeCountersinkFootprintLeavesProfileMaterial")]
    [InlineData("90deg", "20deg", "ComposeCountersinkDepthExceedsHost")]
    [InlineData("on: +Z", "on: +X", "ComposeCountersinkAxisUnsupported")]
    [InlineData("end: ThroughAll", "end: Blind", "")]
    [InlineData("countersinkDiameter: 12mm", "countersinkDiameter: 5mm", "")]
    public void UnsupportedCountersinks_RejectInValidateAndBuild(string before, string after, string diagnostic)
    {
        var source = Source.Replace(before, after);
        Admit(source, false);
        if (diagnostic.Length > 0) Assert.Contains(FirmamentBuildAndExport.CompileSource(source).Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Hole<Countersink>", "Hole<Shaft>")]
    [InlineData("countersinkDiameter", "countersinkDiametre")]
    [InlineData("countersinkDiameter: 12mm", "counterboreDepth: 2mm; countersinkDiameter: 12mm")]
    public void UnknownOrInapplicableFields_NeverDisappear(string before, string after)
    {
        Admit(Source.Replace(before, after), false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CavityCollision_DoesNotDependOnDeclarationOrder(bool sinkFirst)
    {
        const string shaft = "Hole<Shaft> Other { on: +Z; center: Point2(27mm, -10mm); diameter: 4mm; end: ThroughAll }";
        var source = Source.Replace("        Hole<Countersink>", sinkFirst ? "        Hole<Countersink>" : shaft + "\n        Hole<Countersink>");
        if (sinkFirst) source = source.Replace("end: ThroughAll }", "end: ThroughAll }\n" + shaft);
        Admit(source, false);
        Assert.Contains(FirmamentBuildAndExport.CompileSource(source).Diagnostics, d => d.Message.Contains("ComposeCountersinkIntersectsExistingCavity", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("counterbore")]
    [InlineData("capsule")]
    [InlineData("rounded-slot")]
    public void DisjointFeatures_CoexistWithoutOmission(string kind)
    {
        var feature = kind switch
        {
            "counterbore" => "Hole<Counterbore> Other { on: +Z; center: Point2(-30mm, 20mm); diameter: 4mm; counterboreDiameter: 8mm; counterboreDepth: 2mm; end: ThroughAll }",
            "capsule" => "Slot<Capsule> Other { center: Point2(-30mm, 20mm); direction: [0, 1]; length: 12mm; width: 4mm; extent: ThroughAll }",
            _ => "Slot<RoundedRectangle> Other { center: Point2(-30mm, 20mm); direction: [0, 1]; length: 12mm; width: 4mm; cornerRadius: 1mm; extent: ThroughAll }"
        };
        var source = Source.Replace("        Hole<Countersink>", feature + "\n        Hole<Countersink>");
        var built = Admit(source, true);
        Assert.Contains(built.RuntimeCorrespondence!.Descendants, d => d.SourceStableId.EndsWith(".Other", StringComparison.Ordinal));
        Assert.True(BrepExportPreflight.Validate(built.RuntimeBody!).IsValid);
        Assert.True(Step242Importer.ImportBody(built.StepText).IsSuccess);
    }

    [Theory]
    [InlineData("PlusMinus(0.1mm)")]
    [InlineData("PlusMinus(0.1mm, 0.1mm)")]
    public void ShaftPmi_UsesOnlyShaftPatches_AndGlbPresentationRetainsEveryPatch(string tolerance)
    {
        var source = Source.Insert(Source.LastIndexOf('}'), $$"""
            Pmi {
                Datum A { target: face(+Z) }
                HoleDiameter ShaftSize { target: Mount; value: 6mm; tolerance: {{tolerance}}; }
            }
            """);
        var product = Admit(source, true);
        var inspection = Step242SemanticPmiInspector.Inspect(product.StepText);
        Assert.True(inspection.Success);
        var diameter = Assert.Single(inspection.Items, p => p.Kind == "Diameter");
        Assert.Equal(.1, diameter.TolerancePlus); Assert.Equal(.1, diameter.ToleranceMinus);
        var body = Step242Importer.ImportBody(product.StepText).Value;
        var faces = body.Bindings.FaceBindings.Where(b => b.SourceStepEntityId.HasValue && diameter.GeometricFaceEntityIds.Contains(b.SourceStepEntityId.Value)).ToArray();
        Assert.Equal(4, faces.Length);
        Assert.All(faces, f => Assert.Equal(3d, body.GetFaceSurface(f.FaceId).Cylinder!.Value.Radius, 9));
        var display = Assert.Single(PmiDisplayProjection.FromStep(product.StepText), p => p.Kind == "Diameter");
        Assert.Equal(diameter.GeometricFaceEntityIds, display.StepFaceEntities);
        Assert.Equal("CylinderWall", display.Target!.Kind);
        Assert.True(display.Lines.Count >= 4);
        var feature = Assert.Single(product.Features!);
        Assert.Equal("Countersink", feature.StackKind);
        Assert.Equal(4, feature.CylindricalFaces); Assert.Equal(4, feature.ConicalFaces);
    }

    [Fact]
    public void DifferentCylinderAxes_AreNotCollapsedIntoOnePmiWall()
    {
        var source = Source.Replace("        Hole<Countersink>", "Hole<Shaft> Other { on: +Z; center: Point2(-30mm, 20mm); diameter: 6mm; end: ThroughAll }\n        Hole<Countersink>");
        var product = Admit(source, true); var body = product.RuntimeBody!;
        var faces = body.Topology.Faces.Where(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder).Select(f => f.Id.Value).ToArray();
        var step = Step242Exporter.ExportBody(body, [new Step242SemanticPmiHole("group", 6d, null, "test", .1, .1) { GeometricFaceIds = faces }]);
        Assert.True(step.IsSuccess);
        var failure = Assert.Throws<InvalidOperationException>(() => PmiDisplayProjection.FromStep(step.Value));
        Assert.StartsWith("pmi-display-target-multiple-unsupported:", failure.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARecessNearTheEntry_CannotSilentlyClipTheLargerCavity(bool counterbore)
    {
        var source = Source.Replace("Point2 Notch", "Rect2 Relief { center: [26mm, -10mm]; size: [2mm, 2mm] }\n        Point2 Notch")
            .Replace("    Struct BracketBody", "Profile ReliefProfile using Layout { Loop Outer { Relief |> TraceLoop } }\n    Struct BracketBody")
            .Replace("Base Stock { Profile: Bracket; From: 0mm; To: 12mm; Role: Stock }", "Base Stock { Profile: Bracket; From: 0mm; To: 12mm; Role: Stock }\nRemove Recess { profile: ReliefProfile; from: 8mm; to: 12mm }");
        if (counterbore) source = source.Replace("Hole<Countersink>", "Hole<Counterbore>").Replace("countersinkDiameter: 12mm; countersinkAngle: 90deg", "counterboreDiameter: 12mm; counterboreDepth: 3mm");
        Admit(source, false);
        Assert.Contains(FirmamentBuildAndExport.CompileSource(source).Diagnostics, d => d.Message.Contains(counterbore ? "ComposeCounterboreFootprintLeavesProfileMaterial" : "ComposeCountersinkFootprintLeavesProfileMaterial", StringComparison.Ordinal));
    }

    [Fact]
    public void OrientedHoleSupport_RemainsAnExplicitAdmissionBoundary()
    {
        var source = Source.Replace("On XY", "On XZ");
        Admit(source, false);
        Assert.Contains(FirmamentBuildAndExport.CompileSource(source).Diagnostics, d => d.Message.StartsWith("compose-oriented-feature-not-admitted:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Distance: 1mm", true, "")]
    [InlineData("Distance: 3mm", false, "ProfileBoundaryChamferCountersinkStationOverlapUnsupported")]
    public void CountersinkAndDisjointPerimeterChamfer_PreserveTheCone(string distance, bool accepted, string diagnostic)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/CanonicalGeometry/profile-compose-reflex-chamfer-with-counterbore.firmament")
            .Replace("Hole<Counterbore>", "Hole<Countersink>").Replace("CounterboreDiameter: 12mm; CounterboreDepth: 4mm", "CountersinkDiameter: 12mm; CountersinkAngle: 90deg")
            .Replace("Distance: 1mm", distance);
        var built = Admit(source, accepted);
        if (accepted)
        {
            var body = Step242Importer.ImportBody(built.StepText).Value;
            Assert.True(BrepMassProperties.Evaluate(body).IsEnclosed);
            Assert.Contains(body.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cone);
            Assert.Contains(body.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        }
        else Assert.Contains(FirmamentBuildAndExport.CompileSource(source).Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void ComposedFillet_CannotRebuildFreshStockAndLoseTheCountersink()
    {
        var source = Source.Insert(Source.LastIndexOf('}'), "Modify BracketBody { EdgeFinish Round { Target: Bracket.Outer.South; On: Top; Kind: Fillet; Radius: 1mm; EndClearance: 2mm } }\n");
        Admit(source, false);
        Assert.Contains(FirmamentBuildAndExport.CompileSource(source).Diagnostics, d => d.Message.StartsWith("ProfileBoundaryFilletComposeUnsupported:", StringComparison.Ordinal)
            && d.Message.Contains("retains its existing material intervals and cavities", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A4LocalPlateEntriesAndPerimeter_AreNotLiftedOntoAnUnrelatedBoss(bool countersink)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Integration/machined-mounting-block.firmament");
        if (countersink) source = source.Replace("Hole<Counterbore>", "Hole<Countersink>")
            .Replace("counterboreDiameter: 11mm", "countersinkDiameter: 11mm").Replace("counterboreDepth: 3mm", "countersinkAngle: 90deg");
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        if (countersink) Assert.All(parsed.Feature!.CountersinkHoles!, h => Assert.Equal(12d, h.To));
        else Assert.All(parsed.Feature!.CounterboreHoles!, h => Assert.Equal(12d, h.To));
        var built = Admit(source, true);
        var body = Step242Importer.ImportBody(built.StepText).Value;
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        var mass = BrepMassProperties.Evaluate(body, new(.001, Math.PI / 360d, .001, MinimumSegments: 64, MaximumSegments: 256));
        Assert.True(mass.IsEnclosed);
        var removedEntries = countersink ? 2d * Math.PI * 2.5d * (5.5d * 5.5d + 5.5d * 3d - 18d) / 3d : 2d * Math.PI * (5.5d * 5.5d - 9d) * 3d;
        var expected = 70d * 44d * 12d + 14d * 14d * 6d - 20d * 16d * 4d
            - Math.PI * (9d * 18d + 2d * 9d * 12d) - removedEntries - (70d + 44d - 4d / 3d);
        var stack = PrismaticSectionStackCompiler.Normalize(parsed, out _)!;
        var profile = parsed.Profiles["BaseProfile"];
        Assert.True(ProfileBoundaryChamferSourceBinder.TryBind(source, profile, stack.Feature.Name, out var target, out var distance, out _));
        var finished = ComposedProfileBoundaryChamferStackPlanner.TryApply(stack, profile, target!, distance, out var planDiagnostics);
        Assert.Empty(planDiagnostics);
        Assert.Equal(expected, finished!.AnalyticVolume, 8);
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expected), 0d, Math.Min(mass.ErrorBound ?? 0d, .01d));
        Assert.All(built.Features!.Where(h => h.Name is "MountNorth" or "MountSouth"), h =>
        {
            Assert.True(h.CylindricalFaces > 0);
            if (countersink) Assert.True(h.ConicalFaces > 0);
        });
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(8, false)]
    public void CountersinkEntryMustFitTheExposedPocketFloor(int size, bool accepted)
    {
        var source = Source.Replace("Point2 Notch", $"Rect2 Relief {{ center: [20mm, -10mm]; size: [{size}mm, {size}mm] }}\n        Point2 Notch")
            .Replace("    Struct BracketBody", "Profile ReliefProfile using Layout { Loop Outer { Relief |> TraceLoop } }\n    Struct BracketBody")
            .Replace("Base Stock { Profile: Bracket; From: 0mm; To: 12mm; Role: Stock }", "Base Stock { Profile: Bracket; From: 0mm; To: 12mm; Role: Stock }\nRemove Recess { profile: ReliefProfile; from: 8mm; to: 12mm }");
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        if (!accepted) { Admit(source, false); Assert.Contains(parsed.Diagnostics, d => d.Contains("entry-footprint-is-not-contained", StringComparison.Ordinal)); return; }
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(8d, Assert.Single(parsed.Feature!.CountersinkHoles!).To);
        var built = Admit(source, true);
        var body = Step242Importer.ImportBody(built.StepText).Value;
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        Assert.Contains(body.Geometry.Curves, c => c.Value.Circle3 is { } arc && Math.Abs(arc.Center.Z - 8d) < 1e-9 && Math.Abs(arc.Radius - 6d) < 1e-9);
    }

    private static void CheckBody(BrepBody body, double expectedVolume, double angle)
    {
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        // The generic cone verifier is a bounded tessellation estimate. Exact
        // volume is checked against the construction integral above; never
        // pretend its display-mesh sanity estimate has analytic precision.
        Assert.InRange(Math.Abs(mass.AbsoluteVolume - expectedVolume), 0d, Math.Min(mass.ErrorBound ?? 0d, expectedVolume * 2e-5));
        var cones = body.Geometry.Surfaces.Where(s => s.Value.Kind == SurfaceGeometryKind.Cone).ToArray();
        Assert.NotEmpty(cones);
        Assert.All(cones, c => Assert.Equal(angle * Math.PI / 360d, c.Value.Cone!.Value.SemiAngleRadians, 10));
        var coneFrom = 12d - 3d / Math.Tan(angle * Math.PI / 360d);
        foreach (var face in body.Topology.Faces.Where(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cone))
        {
            var surface = body.GetFaceSurface(face.Id).Cone!.Value;
            Assert.Equal(20d, surface.PlacementOrigin.X, 9);
            Assert.Equal(-10d, surface.PlacementOrigin.Y, 9);
            Assert.Equal(coneFrom, surface.PlacementOrigin.Z, 9);
            var arcs = face.LoopIds.SelectMany(id => body.Topology.Loops.Single(l => l.Id == id).CoedgeIds)
                .Select(id => body.Topology.Coedges.Single(c => c.Id == id).EdgeId)
                .Select(id => { Assert.True(body.TryGetEdgeCurveGeometry(id, out var curve)); return curve!; }).Where(c => c.Kind == CurveGeometryKind.Circle3).Select(c => c.Circle3!.Value).ToArray();
            Assert.Contains(arcs, c => Math.Abs(c.Radius - 3d) < 1e-9 && Math.Abs(c.Center.Z - coneFrom) < 1e-9);
            Assert.Contains(arcs, c => Math.Abs(c.Radius - 6d) < 1e-9 && Math.Abs(c.Center.Z - 12d) < 1e-9);
        }
        Assert.Contains(body.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cylinder && Math.Abs(s.Value.Cylinder!.Value.Radius - 3d) < 1e-9);
        Assert.Equal(-40d, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).Min(p => p.X), 9);
        Assert.Equal(40d, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).Max(p => p.X), 9);
        Assert.Equal(-20d, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).Min(p => p.Y), 9);
        Assert.Equal(40d, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).Max(p => p.Y), 9);
        Assert.Equal(0d, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).Min(p => p.Z), 9);
        Assert.Equal(12d, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).Max(p => p.Z), 9);
    }

    private static FirmamentStepExportResult Admit(string source, bool accepted)
    {
        var validation = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(validation.Status == (accepted ? "valid" : "invalid"), string.Join("; ", build.Diagnostics.Select(d => d.Message)));
        Assert.True(build.IsSuccess == accepted, string.Join("; ", build.Diagnostics.Select(d => d.Message)));
        return accepted ? build.Value : null!;
    }
}
