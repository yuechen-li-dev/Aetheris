using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CombinedFilletHoleTests
{
    [Theory]
    [InlineData("Regression/FrontendX4/cases/d05-fillet-with-hole.firmament", 11, 5, 4d, 0d)]
    [InlineData("Regression/Fillet/top-boundary-with-counterbore.firmament", 14, 7, 3d, 2d)]
    public void AuthoredFilletAndHoles_AdmitTogetherAndSurviveStep(string file, int faces, int cylinders, double shaftA, double shaftB)
    {
        var source = Source(file);
        var result = AdmitAndBuild(source, true);
        var combined = Assert.IsType<FirmamentCombinedFeaturePlanReport>(result.Combined);
        Assert.Equal("Disjoint", combined.Interaction);
        Assert.Equal(4, combined.EdgeFinishDescendantCount);
        Assert.Equal(FirmamentV2Parser.Parse(source).Document!.ModelName, result.ExportedFeatureId);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(result.RuntimeCorrespondence);
        var body = Assert.IsType<BrepBody>(result.RuntimeBody);
        foreach (var replacement in correspondence.Descendants.Where(d => d.Role == SemanticTopologyRole.EdgeFinishReplacementFace))
        {
            var binding = body.Bindings.FaceBindings.Single(b => b.FaceId == replacement.Face);
            Assert.Equal(SurfaceGeometryKind.Cylinder, body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind);
        }
        var walls = correspondence.Descendants.Where(d => d.Role == SemanticTopologyRole.HoleWallFace).ToArray();
        var shaftRadii = new List<double>();
        foreach (var wall in walls)
        {
            var binding = body.Bindings.FaceBindings.Single(b => b.FaceId == wall.Face);
            var radius = body.Geometry.GetSurface(binding.SurfaceGeometryId).Cylinder!.Value.Radius;
            if (wall.StableId.Contains("counterbore-wall")) Assert.Equal(4d, radius, 9);
            else shaftRadii.Add(radius);
        }
        Assert.Equal((shaftB > 0 ? new[] { shaftA, shaftB } : new[] { shaftA }).Order(), shaftRadii.Order());
        var expectedHost = 24000 - 200 * (1 - Math.PI / 4) + 4 * (5d / 3 - Math.PI / 2);
        var expectedRemoved = Math.PI * (shaftA * shaftA + shaftB * shaftB) * 10;
        if (shaftB > 0) expectedRemoved += Math.PI * (16 - shaftB * shaftB) * 3;
        Assert.Equal(expectedHost - expectedRemoved, combined.FinalAnalyticVolume, 8);
        var imported = Step242Importer.ImportBody(result.StepText);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(faces, imported.Value.Topology.Faces.Count());
        Assert.Equal(cylinders, imported.Value.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(4, imported.Value.Geometry.Curves.Count(c => c.Value.Kind == CurveGeometryKind.Ellipse3));
        Assert.True(BrepMassProperties.Evaluate(imported.Value).IsEnclosed);
        Assert.True(BrepExportPreflight.Validate(imported.Value).IsValid);
        var repeated = FirmamentBuildAndExport.CompileSource(source);
        Assert.Equal(result.StepText, repeated.Value.StepText);
    }

    [Theory]
    [InlineData("Center: Point2(0mm, 0mm)", "Center: Point2(24mm, 0mm)", "Radius: 1mm", "Radius: 1mm", true)]
    [InlineData("Center: Point2(0mm, 0mm)", "Center: Point2(24mm, 0mm)", "Radius: 1mm", "Radius: 2mm", false)]
    [InlineData("Center: Point2(0mm, 0mm)", "Center: Point2(25mm, 0mm)", "Radius: 1mm", "Radius: 2mm", false)]
    [InlineData("Center: Point2(0mm, 0mm)", "Center: Point2(0mm, 0mm)", "Radius: 1mm", "Radius: 10mm", false)]
    public void InteractionAndRadiusBoundaries_AgreeBetweenValidateAndBuild(string oldCenter, string center, string oldRadius, string radius, bool accepted)
    {
        var source = Source("Regression/FrontendX4/cases/d05-fillet-with-hole.firmament").Replace(oldCenter, center).Replace(oldRadius, radius);
        AdmitAndBuild(source, accepted);
        if (accepted) return;
        var build = FirmamentBuildAndExport.CompileSource(source);
        var presented = build.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "test.firmament", source)).ToArray();
        Assert.Contains(presented, d => d.Line == 6 && d.Message.Length > d.Code.Length);
    }

    [Fact]
    public void RemovingTheHole_PreservesTheExactFilletSurfaceFamilyAndStockFrame()
    {
        var source = Source("Regression/FrontendX4/cases/d05-fillet-with-hole.firmament");
        var withoutHole = string.Join('\n', source.Split('\n').Where(line => !line.Contains("Hole<Shaft>")));
        var result = AdmitAndBuild(withoutHole, true);
        Assert.Equal("TopBoundaryFillet", result.ExportedFeatureKind);
        var body = result.RuntimeBody!;
        Assert.Equal(10, body.Topology.Faces.Count());
        Assert.Equal(4, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(0, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p.Z; }).Min(), 9);
        Assert.Equal(10, body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p.Z; }).Max(), 9);
    }

    [Fact]
    public void FilletAndHoles_PreserveExplicitAp242Pmi()
    {
        var source = Source("Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament")
            .Replace("Kind: Chamfer Distance: 1mm", "Kind: Fillet Radius: 1mm");
        var result = AdmitAndBuild(source, true);
        var pmi = Step242SemanticPmiInspector.Inspect(result.StepText);
        Assert.True(pmi.Success, string.Join("; ", pmi.Diagnostics));
        Assert.Equal(1, pmi.DatumCount);
        Assert.Equal(2, pmi.DimensionCount);
        Assert.Equal(2, result.DimensionInspection!.Count);
    }

    [Theory]
    [InlineData("shaft-tangent", "CombinedFeaturePlanChainInvalid", 5)]
    [InlineData("unsupported-selector", "CombinedFeatureSelectionLost", 6)]
    [InlineData("overlapping-holes", "CombinedFeatureInteractionUnsupported", 7)]
    public void UnadmittedCombinations_KeepActionableLocatedDiagnostics(string variant, string code, int line)
    {
        var source = Source("Regression/FrontendX4/cases/d05-fillet-with-hole.firmament");
        source = variant switch
        {
            "shaft-tangent" => source.Replace("Point2(0mm, 0mm)", "Point2(26mm, 0mm)"),
            "unsupported-selector" => source.Replace("Face: +Z", "Face: +X"),
            _ => source.Replace("    EdgeFinish", "    Hole<Shaft> Other { on: +Z; center: [1mm,0mm]; diameter: 4mm; end: ThroughAll }\n    EdgeFinish")
        };
        AdmitAndBuild(source, false);
        var build = FirmamentBuildAndExport.CompileSource(source);
        var diagnostics = build.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "test.firmament", source)).ToArray();
        Assert.Contains(diagnostics, d => d.Code == code && d.Line == line && d.SourceSpan is not null);
    }

    [Fact]
    public void CounterboreEntryRadius_DeterminesFinishClearance()
    {
        var source = Source("Regression/Fillet/top-boundary-with-counterbore.firmament")
            .Replace("[12mm, 0mm]", "[25mm, 0mm]");
        AdmitAndBuild(source, false);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("finish band", StringComparison.Ordinal));
    }

    private static FirmamentStepExportResult AdmitAndBuild(string source, bool accepted)
    {
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(accepted == build.IsSuccess, string.Join("; ", build.Diagnostics.Select(d => d.Message)));
        Assert.Equal(accepted ? "valid" : "invalid", report.Status);
        return accepted ? build.Value : null!;
    }

    private static string Source(string file) => FirmamentCorpusHarness.ReadFixtureText("fixtures/" + file);
}
