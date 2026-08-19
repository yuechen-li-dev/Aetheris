using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ForgeCsA3RuntimeConceptValidationTests
{
    [Fact]
    public void ForgeCsA3_ValidBuiltInConceptFixture_RemainsValidAndReportsRuntimeProvider()
    {
        var report = ReportFixture("Regression/Language/valid/concept-applications-forge.valid.firmfixture");

        Assert.Equal("valid", report.Status);
        Assert.DoesNotContain(report.Diagnostics, diagnostic => diagnostic.Severity == "fatal");
        Assert.All(report.Concepts, concept =>
        {
            Assert.NotNull(concept.RuntimeValidation);
            Assert.Equal("Aetheris.Standard", concept.RuntimeValidation!.Provider);
            Assert.Equal("valid", concept.RuntimeValidation.Status);
            Assert.Empty(concept.RuntimeValidation.Diagnostics);
        });
    }

    [Fact]
    public void ForgeCsA3_CountersinkDiameterOrder_IsInvalid()
    {
        var report = ReportFixture("Compatibility/LegacyAliases/Invalid/Language/concept-countersink-diameter-order.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "forge.hole.countersink.diameter-order" && diagnostic.Severity == "fatal");
        Assert.Contains(report.Concepts, concept => concept.Name == "badHole" && concept.Status == "invalid");
    }

    [Fact]
    public void ForgeCsA3_CountersinkAngleRange_IsInvalid()
    {
        var report = ReportFixture("Compatibility/LegacyAliases/Invalid/Language/concept-countersink-angle-range.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "forge.hole.countersink.angle-range" && diagnostic.Severity == "fatal");
    }

    [Fact]
    public void ForgeCsA3_CounterboreDiameterOrder_IsInvalid()
    {
        var report = ReportFixture("Compatibility/LegacyAliases/Invalid/Language/concept-counterbore-diameter-order.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "forge.hole.counterbore.diameter-order" && diagnostic.Severity == "fatal");
    }

    [Theory]
    [InlineData(
        """
        model ZeroShaft {
            units mm
            solid part: Box { size: [20, 20, 6] }
            feature h: hole<Shaft> {
                target: part.region("holeA")
                diameter: 0.0mm
            }
        }
        """,
        "forge.hole.shaft.diameter-positive")]
    [InlineData(
        """
        model ZeroCounterbore {
            units mm
            solid part: Box { size: [20, 20, 6] }
            feature h: hole<Counterbore> {
                target: part.region("holeA")
                diameter: 6.0mm tol 0.05mm
                counterboreDiameter: 8.0mm tol 0.05mm
                counterboreDepth: 0.0mm
            }
        }
        """,
        "forge.hole.counterbore.counterbore-depth-positive")]
    [InlineData(
        """
        model ZeroCountersink {
            units mm
            solid part: Box { size: [20, 20, 6] }
            feature h: hole<Countersink> {
                target: part.region("holeA")
                diameter: 0.0mm
                countersinkDiameter: 8.0mm tol 0.05mm
                angle: 90deg
            }
        }
        """,
        "forge.hole.countersink.diameter-positive")]
    public void ForgeCsA3_PositiveDimensionChecks_AreReported(string source, string expectedCode)
    {
        var report = ReportSource(source);

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == expectedCode && diagnostic.Severity == "fatal");
    }

    [Fact]
    public void ForgeCsA3_MissingToleranceRecommendation_IsWarningOnly()
    {
        var report = ReportFixture("Regression/Language/valid/concept-shaft-missing-tolerance-warning.valid.firmfixture");

        Assert.Equal("valid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "forge.hole.shaft.diameter-tolerance-recommended" && diagnostic.Severity == "warning");
        Assert.Contains(report.Concepts, concept => concept.Name == "h"
            && concept.RuntimeValidation is not null
            && concept.RuntimeValidation.Status == "valid"
            && concept.RuntimeValidation.Diagnostics.Any(diagnostic => diagnostic.Code == "forge.hole.shaft.diameter-tolerance-recommended"));
    }

    [Fact]
    public void ForgeCsA3_CncMinimumToolRadius_IsInvalid()
    {
        var report = ReportFixture("Compatibility/LegacyAliases/Invalid/Language/concept-cnc-minimum-tool-radius.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "forge.process.cnc.minimum-tool-radius-positive" && diagnostic.Severity == "fatal");
    }

    [Fact]
    public void ForgeCsA3_RuntimeValidation_UsesBuiltInStandardPackOnly()
    {
        var runtime = FirmamentV2RuntimeConceptValidation.Validate(ParseFixture("Regression/Language/valid/concept-applications-forge.valid.firmfixture").Document);

        Assert.Equal("Aetheris.Standard", runtime.ForgeRuntime.BuiltInPack);
        Assert.Empty(runtime.ForgeRuntime.ExternalPacks);
        Assert.Equal(2, runtime.Concepts.Count);
        Assert.All(runtime.Concepts, concept => Assert.Equal("Aetheris.Standard", concept.Provider));
    }

    private static FirmamentV2ValidationReport ReportFixture(string relative) => Build(ParseFixture(relative), relative);

    private static FirmamentV2ValidationReport ReportSource(string source) => Build(FirmamentV2Parser.Parse(source), "inline");

    private static FirmamentV2ValidationReport Build(FirmamentV2ParseResult parse, string source)
    {
        Assert.NotNull(parse.Document);
        return FirmamentV2ValidationReportBuilder.Build(parse, source);
    }

    private static FirmamentV2ParseResult ParseFixture(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "fixtures", relative);
        return FirmamentV2Parser.Parse(File.ReadAllText(path), Path.GetDirectoryName(path));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
