using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentAssertionsTests
{
    private static string Source(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/Assertions/" + name + ".firmament"));

    [Fact]
    public void HistoricalFilletGeometryBuildsButAnUnprovableVolumeAssertionStillRejects()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/CanonicalGeometry/profile-fillet-convex-two-segment-top.firmament");
        var source = File.ReadAllText(path);
        var accepted = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(accepted.IsSuccess, string.Join("; ", accepted.Diagnostics.Select(d => d.Message)));
        source = source.Insert(source.LastIndexOf('}'), "Assert Volume Body { expected: 1mm^3 tolerance: 0mm^3 }\n");
        var rejected = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Diagnostics, d => d.Message.StartsWith("firmament-v2-assert-volume-measurement-unavailable:", StringComparison.Ordinal));
        Assert.Equal(accepted.Value.StepText, FirmamentBuildAndExport.CompileForMeasurement(source).Value.StepText);
    }

    [Fact]
    public void ExactVolumeBoundsAndTopologyGateTheRealCompilationWithoutChangingGeometry()
    {
        var source = Source("box");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.True(Assert.Single(built.Value.Assertions!).Passed);
        Assert.Equal("Passed", built.Value.Assertions![0].VerificationStatus);
        Assert.Equal(2, built.Value.GeometryAssertions.Count);
        Assert.All(built.Value.GeometryAssertions, a => Assert.True(a.Passed));
        var measured = FirmamentBuildAndExport.CompileForMeasurement(source);
        Assert.True(measured.IsSuccess);
        Assert.Equal(measured.Value.StepText, built.Value.StepText);
        var report = new FirmamentMeasurements(measured.Value);
        Assert.Equal("Body", report.TargetBodyId);
        Assert.Equal(480, report.Volume().ValueMm3);
        Assert.Equal([-5d, -4, 0], report.Bounds!.MinimumMm);
    }

    [Fact]
    public void AThroughHoleRemovesTheAnalyticCylinderVolumeAndPreservesStockBounds()
    {
        var source = Source("through-hole-plate");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var measured = new FirmamentMeasurements(built.Value);
        Assert.InRange(Math.Abs(measured.Volume().ValueMm3!.Value - (80 * 50 * 8 - Math.PI * 4 * 4 * 8)), 0, 1e-8);
        Assert.Equal([-40d, -25, 0], measured.Bounds!.MinimumMm);
        Assert.Equal([40d, 25, 8], measured.Bounds.MaximumMm);
        Assert.All(built.Value.GeometryAssertions, a => Assert.True(a.Passed));
        Assert.Equal(FirmamentBuildAndExport.CompileForMeasurement(source).Value.StepText, built.Value.StepText);
    }

    [Theory]
    [InlineData("expected: 480mm^3", "expected: 479mm^3", "firmament-v2-assert-volume-failed", "\n")]
    [InlineData("faces: 6", "faces: 5", "firmament-v2-assert-topology-failed", "\n")]
    [InlineData("max: [5mm", "max: [6mm", "firmament-v2-assert-bounds-failed", "\n")]
    [InlineData("expected: 480mm^3", "expected: 479mm^3", "firmament-v2-assert-volume-failed", "\r\n")]
    [InlineData("faces: 6", "faces: 5", "firmament-v2-assert-topology-failed", "\r\n")]
    [InlineData("max: [5mm", "max: [6mm", "firmament-v2-assert-bounds-failed", "\r\n")]
    public void FalseAssertionsFailAtTheirOwnSourceSpan(string before, string after, string code, string newline)
    {
        var source = Source("box").ReplaceLineEndings(newline).Replace(before, after, StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        var diagnostic = Assert.Single(built.Diagnostics.Where(d => d.Message.StartsWith(code, StringComparison.Ordinal)));
        Assert.NotNull(diagnostic.SourceSpan);
        Assert.StartsWith("Assert ", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.True(FirmamentBuildAndExport.CompileForMeasurement(source).IsSuccess);
    }

    [Fact]
    public void SourceLineEndingsDoNotChangeQualifiedGeometry()
    {
        var lf = FirmamentBuildAndExport.CompileSource(Source("box").ReplaceLineEndings("\n"));
        var crlf = FirmamentBuildAndExport.CompileSource(Source("box").ReplaceLineEndings("\r\n"));
        Assert.True(lf.IsSuccess);
        Assert.True(crlf.IsSuccess, string.Join("; ", crlf.Diagnostics.Select(d => d.Message)));
        Assert.Equal(lf.Value.StepText, crlf.Value.StepText);
    }

    [Fact]
    public void UnknownFieldsAndNonExportedTargetsNeverDisappear()
    {
        var unknown = FirmamentV2Parser.Parse(Source("box").Replace("tolerance: 0mm^3", "tolerence: 0mm^3", StringComparison.Ordinal));
        Assert.False(unknown.IsSuccess);
        Assert.Contains(unknown.Diagnostics, d => d.Contains("field-unknown", StringComparison.Ordinal));
        var wrong = FirmamentBuildAndExport.CompileSource(Source("two-bodies"));
        Assert.False(wrong.IsSuccess);
        Assert.Contains(wrong.Diagnostics, d => d.Message.StartsWith("firmament-v2-multiple-material-bodies-unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void SdfSphereAssertionUsesAnIntervalRatherThanMeshMass()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source("sphere"));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var field = new FirmamentMeasurements(built.Value).Volume(50, forceSdf: true);
        Assert.Equal("SdfAdaptiveOccupiedVolumeInterval", field.Method);
        Assert.InRange(4 * Math.PI * 1000 / 3, field.LowerBoundMm3!.Value, field.UpperBoundMm3!.Value);
        Assert.True(field.RegionsVisited > 1);
    }

    [Fact]
    public void DisplayEligibilityAloneCannotAuthorizeAnSdfVolumeAssertion()
    {
        var built = FirmamentBuildAndExport.CompileForMeasurement(Source("sphere"));
        Assert.True(built.IsSuccess);
        var displayOnly = built.Value with { Cir = built.Value.Cir! with { RuntimeRootIsExportedPrimitive = false } };
        var measurement = new FirmamentMeasurements(displayOnly).Volume(forceSdf: true);
        Assert.False(measurement.Authoritative);
        Assert.Null(measurement.ValueMm3);
        Assert.Contains("Display-only or legacy Boolean mirrors", measurement.Diagnostic);
    }

    [Fact]
    public void APartAssertionCannotDisappearInADedicatedRevolveOwner()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Revolve/sphere.firmament");
        var source = File.ReadAllText(path);
        Assert.True(FirmamentV2Parser.Parse(source).IsSuccess);
        source = source.Insert(source.LastIndexOf('}'), "Assert Topology Ball { shells: 42 }\n");
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Contains("assert-owner-unsupported", StringComparison.Ordinal)
            || d.Contains("declaration-unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void UncertaintyOverlappingTheLimitCannotPassOrPretendToBeADesignMismatch()
    {
        var assertion = new FirmamentV2VolumeAssertion("test", "Body", 480, 1, null, new(0, 1));
        var result = FirmamentV2VolumeAssertionComparer.Compare(assertion,
            new FirmamentVolumeMeasurement(480, 470, 490, "BoundedSdf", true));
        Assert.False(result.Passed);
        Assert.Equal("Unverified", result.VerificationStatus);
        Assert.StartsWith("firmament-v2-assert-volume-unverified:", result.Diagnostic);
    }

    [Fact]
    public void ValidationErrorsRemainErrorsInTheSdkProjection()
    {
        var adapted = Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop.FirmamentV2DiagnosticAdapter.FromValidationDiagnostic(
            new("firmament-v2-assert-volume-failed", "error", "The measured volume is outside the allowed range."));
        Assert.Equal(Aetheris.Forge.Abstractions.FirmamentInterop.FirmamentDiagnosticSeverity.Fatal, adapted.Severity);
    }
}
