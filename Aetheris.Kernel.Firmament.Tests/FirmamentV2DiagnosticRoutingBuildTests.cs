using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2DiagnosticRoutingBuildTests
{
    [Fact]
    public void Build_RecognizedMalformedConceptStruct_ReturnsAuthoritativeV2Diagnostics()
    {
        var result = FirmamentBuildAndExport.Run(FixturePath());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message == FirmamentV2Parser.HoleConstructionPlaneCenterMissing);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.StartsWith("firmament-concept-missing-member:", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("FIRM-PARSE-0001", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("canonical TOON-style text", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""
model Broken {
  units mm
  solid base: Box { size: [10, 10] }
}
""", FirmamentV2Parser.BoxSizeArity)]
    [InlineData("""
Model Broken mm
Box Base { Size: [10mm, 10mm] }
Modify Base { EdgeFinish Break { Face: +Z Target: Boundary Kind: Chamfer Distance: 1mm } }
""", FirmamentV2Parser.Phase3EdgeFinishSyntaxInvalid)]
    public void Build_RecognizedMalformedLowercaseAndPascalCaseModel_ReturnsV2Diagnostics(string source, string expectedDiagnostic)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-v2-diagnostic-routing-{Guid.NewGuid():N}.firmament");
        File.WriteAllText(path, source);
        try
        {
            var result = FirmamentBuildAndExport.Run(path);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message == expectedDiagnostic);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("FIRM-PARSE-0001", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Build_ValidLegacyToon_RemainsEligibleForV1Fallback()
    {
        var path = Path.Combine(RepoRoot(), "testdata", "firmament", "examples", "box_basic.firmament");
        var v2 = FirmamentV2Parser.Parse(File.ReadAllText(path));
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-v1-fallback-{Guid.NewGuid():N}.step");
        try
        {
            var result = FirmamentBuildAndExport.Run(path, output);

            Assert.Equal(FirmamentV2ParseDisposition.NotRecognized, v2.Disposition);
            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        finally
        {
            File.Delete(output);
        }
    }

    private static string FixturePath() => Path.Combine(RepoRoot(), "fixtures", "FirmamentV2", "Language", "invalid", "concept-struct-diagnostic-routing-x1.invalid.firmfixture");
    private static string RepoRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
}
