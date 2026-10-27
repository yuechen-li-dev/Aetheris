using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFeatureAccountingTests
{
    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures", relative));

    [Theory]
    [InlineData("Regression/FrontendX4/cases/e01-pocket-through.firmament", "Body", "Pocket", "Slot")]
    [InlineData("Regression/DemoRegression/concept-materialization-m2.firmament", "Base", "Hole", "LeftMount")]
    [InlineData("Canonical/Pipeline/manual-profile-parity.firmament", "Body", "Remove", "ThroughCutout")]
    [InlineData("Canonical/Pipeline/profile-pipeline-flagship.firmament", "Body", "Remove", "ThroughCutout")]
    [InlineData("Regression/FeatureAccounting/extrude-hole.firmament", "Body", "Hole", "Mount")]
    [InlineData("Canonical/Features/Composition/box-counterbore-chamfer.firmament", "Base", "EdgeFinish", "TopBreak")]
    [InlineData("Regression/CanonicalGeometry/profile-compose-l-bracket-external-modify-counterbore.firmament", "BracketBody", "Hole", "LocatingPin")]
    [InlineData("Regression/FeatureAccounting/coaxial.firmament", "Body", "EdgeFinish", "tip")]
    [InlineData("Canonical/Features/EdgeFinish/semantic-selection-chamfer.firmament", "MainPlate", "EdgeFinish", "TopEdgeBreak")]
    [InlineData("Canonical/Patterns/four-hole-pattern.firmament", "Plate", "Hole", "MountPattern_0")]
    [InlineData("Regression/CanonicalGeometry/profile-chamfer-reflex-junction-top.firmament", "Body", "EdgeFinish", "ReflexTopBreak")]
    [InlineData("Canonical/Perforation/cylindrical-grid-8.firmament", "Body", "Perforation", "Vents")]
    public void RealRoutesAccountForExecutedFeatures(string relative, string host, string kind, string name)
    {
        var path = Fixture(relative);
        var source = File.ReadAllText(path);
        var built = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(path));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.Contains(new FirmamentFeatureReceipt(host, kind, name), built.Value.MaterializedFeatures);
        var validation = FirmamentBuildAdmissionValidation.Admit(
            FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source), path), source, Path.GetDirectoryName(path));
        Assert.NotEqual("invalid", validation.Status);
    }

    [Fact]
    public void CompatibilityKindCasingDoesNotDuplicateMissingFeatureDiagnostics()
    {
        var source = File.ReadAllText(Fixture("Regression/DemoRegression/concept-materialization-m2.firmament"));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var result = FirmamentFeatureAccounting.Verify(FirmamentV2Parser.Parse(source), source,
            built.Value with { MaterializedFeatures = built.Value.MaterializedFeatures.Where(r => r.Name != "LeftMount").ToArray() });
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("LeftMount", diagnostic.Message);
    }

    [Fact]
    public void ImportedGeometryAndPmiDoNotInventMaterialFeatures()
    {
        var path = Fixture("Regression/ImportedPartPmi/annotated-part.firmament");
        var source = File.ReadAllText(path);
        var built = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(path));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.Empty(built.Value.MaterializedFeatures);
        Assert.NotEmpty(built.Value.DatumInspection!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OldBareExtrudeResultCannotSatisfyHoleSourceEvenWithAuthoredDisplayReports(bool importedLibrary)
    {
        var source = (importedLibrary ? "Use Profile.Modifications;\n" : "")
            + File.ReadAllText(Fixture("Regression/FeatureAccounting/extrude-hole.firmament"));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess);
        var full = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(full.IsSuccess, string.Join("; ", full.Diagnostics.Select(d => d.Message)));
        var bare = FirmamentBuildAndExport.CompileSource(source[..source.IndexOf("  Modify", StringComparison.Ordinal)] + "}");
        Assert.True(bare.IsSuccess);
        Assert.NotEqual(full.Value.StepText, bare.Value.StepText);
        // Simulates the old producer returning the prism and attaching source-derived reports.
        var rejected = FirmamentFeatureAccounting.Verify(parsed, source, bare.Value with
        { Features = full.Value.Features, EngineeringFeatures = full.Value.EngineeringFeatures });
        Assert.False(rejected.IsSuccess);
        var error = Assert.Single(rejected.Diagnostics);
        Assert.StartsWith("firmament-feature-not-materialized:Hole:Mount:", error.Message);
        Assert.NotNull(error.OriginalSourceSpan);
        Assert.Equal("Mount", source.Substring(error.OriginalSourceSpan.Start, error.OriginalSourceSpan.Length));
    }

    [Fact]
    public void DifferentFeatureKindsWithTheSameNameNeedIndependentReceipts()
    {
        var source = File.ReadAllText(Fixture("Regression/FeatureAccounting/same-name-different-kinds.firmament"));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.Contains(new FirmamentFeatureReceipt("Body", "Hole", "Mount"), built.Value.MaterializedFeatures);
        Assert.Contains(new FirmamentFeatureReceipt("Body", "EdgeFinish", "Mount"), built.Value.MaterializedFeatures);
        foreach (var kind in new[] { "Hole", "EdgeFinish" })
        {
            var result = FirmamentFeatureAccounting.Verify(FirmamentV2Parser.Parse(source), source,
                built.Value with { MaterializedFeatures = built.Value.MaterializedFeatures.Where(r => r.Kind != kind).ToArray() });
            Assert.False(result.IsSuccess);
            Assert.StartsWith("firmament-feature-not-materialized:" + kind + ":Mount:", Assert.Single(result.Diagnostics).Message);
        }
    }

    [Fact]
    public void AReceiptForAnotherHostOrAnotherPatternInstanceCannotCoverAnOmittedHole()
    {
        var path = Fixture("Canonical/Patterns/four-hole-pattern.firmament");
        var source = File.ReadAllText(path);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4, built.Value.MaterializedFeatures.Count);
        var first = built.Value.MaterializedFeatures[0];
        var incomplete = built.Value with { MaterializedFeatures = built.Value.MaterializedFeatures.Skip(1).Append(first with { Host = "Other" }).ToArray() };
        var result = FirmamentFeatureAccounting.Verify(FirmamentV2Parser.Parse(source), source, incomplete);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(first.Name, StringComparison.Ordinal));
    }
}
