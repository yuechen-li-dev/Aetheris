using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ValidationReportTests
{
    [Fact]
    public void ValidationReport_ValidAuthoringFixtureSurfacesDeferredExport()
    {
        var report = Report("Language/valid/v2-phase1-validation-report.valid.firmfixture");

        Assert.Equal("valid-with-deferred-export", report.Status);
        Assert.Equal(7, report.Summary.LetCount);
        Assert.Equal(4, report.Summary.TolerancedLetCount);
        Assert.Equal(2, report.Summary.ConceptCount);
        Assert.Equal(2, report.Summary.ValidConceptCount);
        Assert.Equal(5, report.Summary.PmiRecordCount);
        Assert.Equal(2, report.Summary.ExportSupportedPmiCount);
        Assert.Equal(3, report.Summary.ExportDeferredPmiCount);
        Assert.Equal(0, report.Summary.FatalDiagnosticCount);
        Assert.Contains(report.Lets, l => l.Name == "MountingPattern.holeDiameter" && l.Tolerance is { Kind: "bilateral" });
        Assert.Contains(report.Concepts, c => c.Kind == "feature" && c.Name == "mountHole" && c.Status == "valid" && c.Fields.Any(f => f.Name == "diameter" && f.HasTolerance));
        Assert.Contains(report.Pmi, p => p.Name == "mountHoleDiameter" && p.ExportSupport == "supported" && p.Dimension?.Tolerance is not null);
        Assert.Contains(report.Pmi, p => p.Name == "baseFlatness" && p.ExportSupport == "deferred");
        Assert.Contains(report.Pmi, p => p.Name == "topParallel" && p.ExportSupport == "deferred" && p.DatumRefs.Contains("A"));
        Assert.Contains(report.Diagnostics, d => d.Code == FirmamentV2Parser.ToleranceDroppedThroughArithmetic && d.Severity == "warning");
    }

    [Fact]
    public void ValidationReport_ExportableSubsetHasNoDeferredRecords()
    {
        var report = Report("Language/valid/v2-phase1-validation-report-exportable.valid.firmfixture");

        Assert.Equal("valid", report.Status);
        Assert.Equal(2, report.Summary.PmiRecordCount);
        Assert.Equal(2, report.Summary.ExportSupportedPmiCount);
        Assert.Equal(0, report.Summary.ExportDeferredPmiCount);
        Assert.DoesNotContain(report.Diagnostics, d => d.Severity == "fatal");
    }

    [Fact]
    public void ValidationReport_MissingToleranceIsFatalPmiDiagnostic()
    {
        var report = Report("Language/invalid/v2-phase1-validation-report-missing-tolerance.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.True(report.Summary.FatalDiagnosticCount > 0);
        Assert.Contains(report.Diagnostics, d => d.Code == FirmamentV2Parser.PmiDimensionMissingTolerance && d.Severity == "fatal");
        Assert.Contains(report.Pmi, p => p.Name == "d1" && p.Status == "invalid" && p.Diagnostics.Any(d => d.Code == FirmamentV2Parser.PmiDimensionMissingTolerance));
    }

    [Fact]
    public void ValidationReport_ForgeMissingFieldIsFatalConceptDiagnostic()
    {
        var report = Report("Language/invalid/v2-phase1-validation-report-forge-missing-field.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, d => d.Code == FirmamentV2Parser.ConceptMissingRequiredField && d.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationReport_UnknownDatumIsFatalPmiDiagnostic()
    {
        var report = Report("Language/invalid/v2-phase1-validation-report-unknown-datum.invalid.firmfixture");

        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, d => d.Code == FirmamentV2Parser.PmiUnknownDatum && d.Severity == "fatal");
    }

    private static FirmamentV2ValidationReport Report(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "fixtures/FirmamentV2", relative);
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path), Path.GetDirectoryName(path));
        return FirmamentV2ValidationReportBuilder.Build(parse, path);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Aetheris.slnx"))) dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new DirectoryNotFoundException("Could not find Aetheris.slnx");
    }
}
