using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CombinedCountersinkTests
{
    private static string Source(string name) => FirmamentCorpusHarness.ReadFixtureText($"fixtures/Regression/CombinedFeatures/{name}.firmament");

    [Theory]
    [InlineData("Fillet", 60d)]
    [InlineData("Fillet", 90d)]
    [InlineData("Fillet", 120d)]
    [InlineData("Chamfer", 60d)]
    [InlineData("Chamfer", 90d)]
    [InlineData("Chamfer", 120d)]
    public void CountersinkRetainsItsConeShaftAndEngineeringVolume(string kind, double angle)
    {
        var source = Source("countersink-fillet").Replace("90deg", $"{angle}deg")
            .Replace("kind: Fillet; radius:", $"kind: {kind}; {(kind == "Fillet" ? "radius" : "distance")}:");
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(';', result.Diagnostics.Select(d => d.Message)));
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source);
        Assert.Equal("valid", report.Status);
        Assert.Equal("Built", report.ValidationLevel);
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess);
        var body = imported.Value;
        Assert.Equal(12, body.Topology.Faces.Count());
        var cone = Assert.Single(body.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cone).Value.Cone!.Value;
        Assert.Equal(angle * Math.PI / 360d, cone.SemiAngleRadians, 12);
        var depth = 3 / Math.Tan(angle * Math.PI / 360d);
        Assert.Equal(10 - depth, cone.PlacementOrigin.Z, 9);
        Assert.Equal(3d, cone.PlacementRadius, 9);
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        Assert.True(result.Value.Combined!.ReimportedManifold);
        var host = kind == "Fillet" ? 24000 - 200 * (1 - Math.PI / 4) + 4 * (5d / 3 - Math.PI / 2)
            : 60 * 40 * 9 + (2400d + 4 * 59 * 39 + 58 * 38) / 6d;
        var removed = Math.PI * 9 * 10 + Math.PI * depth / 3 * (36 + 18 - 18);
        Assert.Equal(host - removed, result.Value.Combined!.FinalAnalyticVolume, 8);
        // The interactive verifier defaults to sixteen segments; use an
        // independent, finer boundary estimate for engineering-volume proof.
        var mass = BrepMassProperties.Evaluate(body, new(.01, Math.PI / 90, .01, MinimumSegments: 64, MaximumSegments: 256));
        Assert.True(mass.IsEnclosed);
        Assert.InRange(mass.AbsoluteVolume, (host - removed) * .9999, (host - removed) * 1.0001);
        var feature = Assert.Single(result.Value.Features!);
        Assert.Equal(1, feature.ConicalFaces);
        Assert.Equal(1, feature.CylindricalFaces);
        var walls = result.Value.RuntimeCorrespondence!.Descendants.Where(d => d.Role == SemanticTopologyRole.HoleWallFace).ToArray();
        Assert.Equal(2, walls.Length);
        Assert.Contains(walls, d => result.Value.RuntimeBody!.GetFaceSurface(d.Face!.Value).Kind == SurfaceGeometryKind.Cone);
        Assert.Equal(result.Value.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Theory]
    [InlineData("center: Point2(0mm, 0mm)", "center: Point2(25mm, 0mm)")]
    [InlineData("countersinkAngle: 90deg", "countersinkAngle: 20deg")]
    [InlineData("end: ThroughAll", "end: Blind(8mm)")]
    [InlineData("on: +Z", "on: -Z")]
    public void UnsupportedInteractionsRejectAtTheFeature(string oldText, string newText)
    {
        var source = Source("countersink-fillet").Replace(oldText, newText);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        var diagnostics = result.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "test.firmament", source));
        Assert.All(diagnostics, d => Assert.True(d.Line > 1, d.Message));
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source);
        Assert.Equal("invalid", report.Status);
    }

    [Fact]
    public void MixedHoleStacksKeepEachFeatureAndItsCorrespondence()
    {
        var source = Source("countersink-fillet").Replace("        EdgeFinish", """
                Hole<Shaft> Plain { on: +Z; center: [-15mm,0mm]; diameter: 4mm; end: ThroughAll }
                Hole<Counterbore> Bore { on: +Z; center: [15mm,0mm]; diameter: 3mm; counterboreDiameter: 8mm; counterboreDepth: 2mm; end: ThroughAll }
                EdgeFinish
        """.TrimEnd());
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(';', built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(3, built.Value.Features!.Count);
        var descendants = built.Value.RuntimeCorrespondence!.Descendants;
        Assert.Equal(3, descendants.Count(d => d.Role == SemanticTopologyRole.HoleEntryLoop));
        Assert.Equal(3, descendants.Count(d => d.Role == SemanticTopologyRole.HoleExitLoop));
        foreach (var feature in built.Value.Features)
        {
            var walls = descendants.Where(d => d.Role == SemanticTopologyRole.HoleWallFace && d.ParentStableId == feature.FeatureId).ToArray();
            Assert.Equal(feature.CylindricalFaces + feature.ConicalFaces, walls.Length);
            Assert.Equal(feature.ConicalFaces, walls.Count(w => built.Value.RuntimeBody!.GetFaceSurface(w.Face!.Value).Kind == SurfaceGeometryKind.Cone));
        }
        var host = 24000 - 200 * (1 - Math.PI / 4) + 4 * (5d / 3 - Math.PI / 2);
        var removed = Math.PI * (9 + 4 + 2.25) * 10 + Math.PI * 36 + Math.PI * (16 - 2.25) * 2;
        Assert.Equal(host - removed, built.Value.Combined!.FinalAnalyticVolume, 8);
    }

    [Fact]
    public void CountersinkFinishPreservesExplicitAp242DiameterPmi()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament")
            .Replace("Hole<Shaft> MountLeft {", "Hole<Countersink> MountLeft { CountersinkDiameter: 12mm CountersinkAngle: 90deg");
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join(';', build.Diagnostics.Select(d => d.Message)));
        var pmi = Step242SemanticPmiInspector.Inspect(build.Value.StepText);
        Assert.True(pmi.Success);
        Assert.Equal(1, pmi.DatumCount);
        Assert.Equal(2, pmi.DimensionCount);
        Assert.Equal(2, build.Value.DimensionInspection!.Count);
        Assert.All(build.Value.DimensionInspection!, dimension => Assert.Equal(8d, dimension.Value));
        Assert.Equal(1, build.Value.Features!.Sum(f => f.ConicalFaces));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwoMaterialBodiesCannotSilentlyExportOnlyTheLast(bool features)
    {
        var source = Source("two-bodies");
        if (!features) source = source[..source.IndexOf("    Modify Base", StringComparison.Ordinal)] + "}";
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        var diagnostic = Assert.Single(FirmamentLanguageAnalysisService.Analyze(source, "two.firmament", "1").Diagnostics);
        Assert.Equal("firmament-v2-multiple-material-bodies-unsupported", diagnostic.Code);
        Assert.Equal(source.IndexOf("Box Extra", StringComparison.Ordinal), diagnostic.Start);
        Assert.Contains("Assembly", diagnostic.Message);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source);
        Assert.Equal("invalid", report.Status);
    }

    [Fact]
    public void CompatibilitySpellingRejectsIndependentBodiesButKeepsRecordDerivation()
    {
        var source = Source("two-bodies-compatibility");
        var rejected = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(rejected.IsSuccess);
        var diagnostic = Assert.Single(FirmamentLanguageAnalysisService.Analyze(source, "two.firmament", "1").Diagnostics);
        Assert.Equal("firmament-v2-multiple-material-bodies-unsupported", diagnostic.Code);
        Assert.Equal(source.IndexOf("solid Extra", StringComparison.Ordinal), diagnostic.Start);
        var legitimate = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture");
        var derived = FirmamentBuildAndExport.CompileSource(legitimate);
        Assert.True(derived.IsSuccess, string.Join(';', derived.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, FirmamentV2Parser.Parse(legitimate).Document!.Solids.Count);
    }
}
