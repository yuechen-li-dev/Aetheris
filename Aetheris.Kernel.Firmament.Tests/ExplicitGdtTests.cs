using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Visualization;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ExplicitGdtTests
{
    private static string Fixture(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, relative);
    }
    private static string Source() => File.ReadAllText(Fixture("fixtures/Canonical/PMI/explicit-gdt.firmament"));
    [Fact]
    public void ImportedNistPmiSurvivesAndAuthoredControlReusesItsExistingDatums()
    {
        var path = Fixture("fixtures/Canonical/PMI/nist-ctc01-passthrough.firmament");
        var build = FirmamentBuildAndExport.CompileSource(File.ReadAllText(path), Path.GetDirectoryName(path));
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        var inspection = Step242SemanticPmiInspector.Inspect(build.Value.StepText);
        // CTC-01 has four complex controls and two simple inherited tolerance entities.
        Assert.Equal(3, inspection.DatumCount); Assert.Equal(12, inspection.DimensionCount); Assert.Equal(7, inspection.GeometricToleranceCount);
        var added = Assert.Single(inspection.Items, i => i.Name == "RevisionProfile");
        Assert.Equal(new[] { "A", "B" }, added.DatumReferences); Assert.Equal(.1, added.Value);
        Assert.NotEmpty(added.GeometricFaceEntityIds); Assert.Contains("FREE_STATE", added.Modifiers);
    }
    [Fact]
    public void UnqualifiedImportedGeometryRemainsRejectedIndependentlyOfAuthoredPmi()
    {
        var path = Fixture("fixtures/Invalid/InlineStep/nist-ftc11-pmi-unqualified-geometry.invalid.firmfixture");
        var source = File.ReadAllText(path);
        foreach (var specimen in new[] { source, source[..source.IndexOf("    Pmi {", StringComparison.Ordinal)] + "}" })
        {
            var build = FirmamentBuildAndExport.CompileSource(specimen, Path.GetDirectoryName(path));
            Assert.False(build.IsSuccess); Assert.Contains(build.Diagnostics, d => d.Message.Contains("firmament-inline-step-import-unqualified"));
        }
    }
    [Fact]
    public void ExplicitControlsBuildWithoutRequirementsAndPreserveGeometryAndFaceAssociations()
    {
        var source = Source();
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        var inspection = Step242SemanticPmiInspector.Inspect(build.Value.StepText);
        Assert.True(inspection.Success); Assert.Equal(3, inspection.DatumCount); Assert.Equal(3, inspection.GeometricToleranceCount);
        var flatness = Assert.Single(inspection.Items, i => i.Kind == "Flatness"); Assert.Equal(.03, flatness.Value);
        var position = Assert.Single(inspection.Items, i => i.Kind == "Position");
        Assert.Equal(.0254, position.Value); Assert.Equal(new[] { "A", "B", "C" }, position.DatumReferences);
        Assert.Contains("MAXIMUM_MATERIAL_REQUIREMENT", position.Modifiers);
        Assert.Equal(new[] { "cylindrical or circular" }, position.ZoneForms);
        Assert.All(inspection.Items, i => Assert.NotEmpty(i.GeometricFaceEntityIds));
        var withoutPmi = source[..source.IndexOf("    Pmi {", StringComparison.Ordinal)] + "}";
        var baseline = FirmamentBuildAndExport.CompileSource(withoutPmi);
        Assert.True(baseline.IsSuccess);
        var body = Step242Importer.ImportBody(build.Value.StepText).Value;
        var original = Step242Importer.ImportBody(baseline.Value.StepText).Value;
        Assert.Equal(Step242Exporter.ExportBody(original).Value, Step242Exporter.ExportBody(body).Value);
        Assert.Equal(6, PmiDisplayProjection.FromStep(build.Value.StepText).Count);
    }
    [Theory]
    [InlineData("Flatness", false)] [InlineData("Straightness", false)] [InlineData("Roundness", false)] [InlineData("Cylindricity", false)]
    [InlineData("Parallelism", true)] [InlineData("Perpendicularity", true)] [InlineData("Angularity", true)]
    [InlineData("Position", true)] [InlineData("LineProfile", true)] [InlineData("SurfaceProfile", true)]
    [InlineData("CircularRunout", true)] [InlineData("TotalRunout", true)]
    public void EveryAdmittedControlHasSemanticReadback(string kind, bool datums)
    {
        var source = Source(); var start = source.IndexOf("        Flatness", StringComparison.Ordinal);
        var target = kind is "Roundness" or "Cylindricity" or "CircularRunout" or "TotalRunout" or "Position" ? "face(Mount.Wall)" : "face(+Z)";
        source = source[..start] + $"        {kind} Control {{ target: {target} tolerance: 0.0254mm {(datums ? "datumRefs: [A, B, C]" : "")} }}\n    }}\n}}";
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        var control = Assert.Single(Step242SemanticPmiInspector.Inspect(build.Value.StepText).Items, i => i.Kind == kind);
        Assert.Equal(.0254, control.Value); Assert.NotEmpty(control.GeometricFaceEntityIds);
        Assert.Equal(datums ? new[] { "A", "B", "C" } : [], control.DatumReferences);
    }
    [Theory]
    [InlineData("datumRefs: [A, B, C]", "datumRefs: [A, Missing]", "pmi-unknown-datum")]
    [InlineData("datumRefs: [A, B, C]", "datumRefs: [A, A]", "pmi-datum-precedence-invalid")]
    [InlineData("modifiers: [MaximumMaterial]", "modifiers: [MaximumMaterial, LeastMaterial]", "pmi-modifier-invalid")]
    [InlineData("tolerance: 0.0254mm", "tolerance: 0.001in", "pmi-tolerance")]
    [InlineData("target: face(+Z) tolerance: 0.03mm", "target: face(+Z) tolerance: 0.01mm datumRefs: [A]", "pmi-datum-not-applicable")]
    public void InvalidControlsRejectAdmission(string from, string to, string code)
    {
        var source = Source().Replace(from, to, StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess); Assert.Contains(parsed.Diagnostics, d => d.Contains(code, StringComparison.Ordinal));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }
    [Fact]
    public void NumericalPrecisionWitnessDoesNotRoundATinyToleranceToZero()
    {
        // Precision qualification only, not a proposed manufacturing specification.
        var build = FirmamentBuildAndExport.CompileSource(Source().Replace("tolerance: 0.03mm", "tolerance: 0.00000000000000001mm", StringComparison.Ordinal));
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        Assert.Equal(1e-17, Assert.Single(Step242SemanticPmiInspector.Inspect(build.Value.StepText).Items, i => i.Kind == "Flatness").Value);
    }
    [Theory]
    [InlineData("Cylindrical", "Ø0.0254")]
    [InlineData("Spherical", "SØ0.0254")]
    [InlineData("BetweenSurfaces", "BetweenSurfaces 0.0254")]
    public void EveryAuthoredZoneFormSurvivesPresentation(string zone, string text)
    {
        var build = FirmamentBuildAndExport.CompileSource(Source().Replace("zone: Cylindrical", "zone: " + zone, StringComparison.Ordinal));
        Assert.True(build.IsSuccess, string.Join(";", build.Diagnostics.Select(d => d.Message)));
        var label = Assert.Single(PmiDisplayProjection.FromStep(build.Value.StepText), item => item.Kind == "Position");
        Assert.Contains(text, label.Text);
    }
}
