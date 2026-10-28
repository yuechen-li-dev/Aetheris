using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPathFeatureTests
{
    private static string Root
    {
        get
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
            return root!.FullName;
        }
    }
    private static string Fixture(string file) => File.ReadAllText(Path.Combine(Root, "fixtures/Canonical/Features/PathFeatures", file));
    private static FirmamentV2ValidationReport Validate(string source) => FirmamentBuildAdmissionValidation.Admit(
        FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source), "path-feature.firmament", sourceText: source), source);
    private static string Messages<T>(Aetheris.Kernel.Core.Results.KernelResult<T> result) => string.Join("; ", result.Diagnostics.Select(d => d.Message));

    [Theory]
    [InlineData("manual-rib.firmament", "Stiffener", 16)]
    [InlineData("plate-seal-groove.firmament", "SealSeat", 8)]
    [InlineData("shaft-seal-groove.firmament", "SealSeat", 40)]
    [InlineData("bore-seal-groove.firmament", "SealSeat", 40)]
    [InlineData("arc-rib.firmament", "Stiffener", 14)]
    public void AuthoredFeatureBuildsThroughUnifiedAdmission(string fixture, string name, double maximumZ)
    {
        var source = Fixture(fixture); var validated = Validate(source);
        Assert.Equal("valid", validated.Status); Assert.Equal("Built", validated.ValidationLevel);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.Equal("UnifiedMechanical", parsed.ParserRoute);
        var declared = Assert.Single(parsed.Document!.PathFeatures);
        Assert.EndsWith(fixture is "manual-rib.firmament" or "arc-rib.firmament" ? "Spine" : "Seal", declared.GuideReference);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, Messages(built));
        Assert.Equal(parsed.Document.ModelName, built.Value.ExportedFeatureId);
        Assert.Equal(name, Assert.Single(built.Value.MaterializedFeatures).Name);
        Assert.NotEmpty(built.Value.RuntimeCorrespondence!.Descendants);
        Assert.Equal(declared.GuideReference, Assert.Single(built.Value.EngineeringFeatures!).Profile);
        Assert.All(built.Value.RuntimeCorrespondence.Descendants, d => Assert.Contains(name, d.StableId));
        var body = built.Value.RuntimeBody!;
        var points = body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).ToArray();
        Assert.Equal(0, points.Min(p => p.Z), 8); Assert.Equal(maximumZ, points.Max(p => p.Z), 8);
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        Assert.DoesNotContain("B_SPLINE_SURFACE", built.Value.StepText, StringComparison.Ordinal);
        Assert.All(body.Geometry.Curves, c => Assert.Null(c.Value.BSpline3));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess, Messages(imported));
        var mass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent);
    }

    [Fact]
    public void TwoDifferentChannelSectionsBuildWithoutDroppingEither()
    {
        var source = Fixture("plate-channels.firmament"); Assert.Equal("valid", Validate(source).Status);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, Messages(built)); Assert.Equal(2, built.Value.MaterializedFeatures.Count);
        Assert.Contains(built.Value.RuntimeBody!.Geometry.Surfaces, s => s.Value.Cylinder is not null);
        Assert.Contains(built.Value.RuntimeBody!.Geometry.Surfaces, s => s.Value.Sphere is not null);
        Assert.True(BrepMassProperties.Evaluate(built.Value.RuntimeBody!).IsEnclosed);
    }

    [Theory]
    [InlineData("shaft-seal-groove.firmament", "station: 20mm", "station: 1mm")]
    [InlineData("shaft-seal-groove.firmament", "radius: 15mm }", "radius: 14mm }")]
    [InlineData("bore-seal-groove.firmament", "radius: 2mm", "radius: 5mm")]
    [InlineData("plate-seal-groove.firmament", "radius: 1.5mm", "radius: 1.5mm; depth: 2mm")]
    public void GrooveContractsRejectWithoutWritingPartialGeometry(string fixture, string before, string after)
    {
        var source = Fixture(fixture).Replace(before, after, StringComparison.Ordinal);
        Assert.Equal("invalid", Validate(source).Status);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("thickness: 3mm", "thikness: 3mm", "canonical-field-unknown")]
    [InlineData("draft: 2deg", "draft: 30deg", "draft-collapse")]
    [InlineData("height: 10mm", "height: 0mm", "dimensions")]
    [InlineData("height: 10mm", "height: 10deg", "unit-mismatch")]
    [InlineData("on: face(Plate.Top)", "on: +X", "contract")]
    [InlineData("on XY", "on XZ", "frame")]
    [InlineData("path: Layout.Spine", "path: Missing", "guide")]
    [InlineData("Rib<Rectangular>", "Rib<Semicircular>", "contract")]
    [InlineData("ends: Round", "ends: Guess", "contract")]
    public void UnsupportedIntentNeverValidatesOrBuilds(string before, string after, string code)
    {
        var source = Fixture("manual-rib.firmament").Replace(before, after, StringComparison.Ordinal);
        var validated = Validate(source); var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.Equal("invalid", validated.Status); Assert.False(built.IsSuccess);
        Assert.Contains(validated.Diagnostics, d => d.Code.Contains(code, StringComparison.Ordinal));
        Assert.Contains(validated.Diagnostics, d => d.SourceSpan is not null && d.Line > 0);
    }

    [Fact]
    public void ScalarBindingsAndOpenPathRetainTheirEngineeringMeaning()
    {
        const string source = """
            Model ParameterizedRib {
                units: mm
                Static H: Length = 8mm
                Path Spine { start: Point2(-15mm,0mm); heading: 0deg; Line Run { length: 30mm } }
                Box Plate { size: [50mm,40mm,5mm] }
                Modify Plate { Rib<Rectangular> Web { on: +Z; path: Spine; thickness: 3mm; height: H; draft: 1deg } }
            }
            """;
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, Messages(result));
        var body = result.Value.RuntimeBody!;
        var top = body.Topology.Vertices.Select(v => { body.TryGetVertexPoint(v.Id, out var p); return p.Z; }).Max();
        Assert.Equal(13, top, 8);
    }

    [Fact]
    public void MultipleManualRibsRemainNamedAfterReordering()
    {
        const string prefix = """
            Model Ribs {
            units: mm
            Path Left { start: Point2(-15mm,-8mm); heading: 0deg; Line RunL { length: 30mm } }
            Path Right { start: Point2(-15mm,8mm); heading: 0deg; Line RunR { length: 30mm } }
            Box Plate { size: [50mm,40mm,5mm] }
            Modify Plate {
            """;
        const string a = "Rib<Rectangular> Alpha { on: +Z; path: Left; thickness: 3mm; height: 8mm }";
        const string b = "Rib<Rectangular> Beta { on: +Z; path: Right; thickness: 3mm; height: 8mm }";
        var first = FirmamentBuildAndExport.CompileSource(prefix + a + b + "}}");
        var second = FirmamentBuildAndExport.CompileSource(prefix + b + a + "}}");
        Assert.True(first.IsSuccess, Messages(first)); Assert.True(second.IsSuccess, Messages(second));
        Assert.Equal(first.Value.RuntimeCorrespondence!.Descendants.Select(d => d.StableId).Order(),
            second.Value.RuntimeCorrespondence!.Descendants.Select(d => d.StableId).Order());
        Assert.Equal(2, second.Value.MaterializedFeatures.Count);
        Assert.Equal(BrepMassProperties.Evaluate(first.Value.RuntimeBody!).SignedVolume,
            BrepMassProperties.Evaluate(second.Value.RuntimeBody!).SignedVolume, 6);
        var crossing = prefix.Replace("Point2(-15mm,8mm)", "Point2(-15mm,-8mm)", StringComparison.Ordinal) + a + b + "}}";
        var rejected = FirmamentBuildAndExport.CompileSource(crossing);
        Assert.False(rejected.IsSuccess); Assert.Contains("junction-unqualified", Messages(rejected));
    }

    [Fact]
    public void QualifiedGuideCannotCaptureAnotherLayoutsPointDependencies()
    {
        var source = Fixture("manual-rib.firmament").Replace("Box Plate", """
            Concept Struct Other on XY {
                Point2 Start { position: [1mm,1mm] }
                Point2 End { position: [2mm,1mm] }
            }
            Box Plate
            """, StringComparison.Ordinal);
        Assert.Equal("invalid", Validate(source).Status);
        var rejected = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(rejected.IsSuccess); Assert.Contains("guide-ambiguous", Messages(rejected));
    }
}
