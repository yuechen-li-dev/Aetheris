using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2VolumeAssertionTests
{
    [Fact]
    public void ParsesLiteralVolumeAssertionAndBindsMaterialBody()
    {
        const string source = """
            Model AssertBox {
                Units: mm
                Box Body { Size: [10mm, 8mm, 6mm] }
                Assert Volume Body {
                    Expected: 480mm^3
                    Tolerance: 0mm^3
                    Note: "box"
                }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var assertion = Assert.Single(parsed.Document!.VolumeAssertions!);
        Assert.Equal("Body", assertion.TargetBodyId);
        Assert.Equal(480d, assertion.ExpectedMm3);
        Assert.Equal(0d, assertion.ToleranceMm3);
        Assert.Equal("box", assertion.Note);
        Assert.Contains(parsed.Document.SymbolTable!.Bindings, binding => binding.Relation == "Target" && binding.TargetCanonicalId == "Body:Body");
    }

    [Theory]
    [InlineData("10mm", FirmamentV2Parser.AssertVolumeExpectedDimensionMismatch)]
    [InlineData("10mm^2", FirmamentV2Parser.AssertVolumeExpectedDimensionMismatch)]
    public void RejectsNonVolumeExpectedValue(string expected, string diagnostic)
    {
        var parsed = FirmamentV2Parser.Parse(Source(expected, "0mm^3"));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, item => item.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsNegativeToleranceAndUnknownBody()
    {
        var negative = FirmamentV2Parser.Parse(Source("480mm^3", "-1mm^3"));
        Assert.False(negative.IsSuccess);
        Assert.Contains(negative.Diagnostics, item => item.StartsWith(FirmamentV2Parser.AssertVolumeToleranceNegative, StringComparison.Ordinal));

        var unknown = FirmamentV2Parser.Parse(Source("480mm^3", "0mm^3").Replace("Assert Volume Body", "Assert Volume Missing", StringComparison.Ordinal));
        Assert.False(unknown.IsSuccess);
        Assert.Contains(unknown.Diagnostics, item => item.StartsWith("firmament-v2-assert-volume-target-unknown:Missing", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildEvaluatesDeclaredAssertionWithoutChangingStep()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-assert-volume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var withAssertion = Path.Combine(directory, "with.firmament");
            var withoutAssertion = Path.Combine(directory, "without.firmament");
            File.WriteAllText(withAssertion, Source("480mm^3", "0mm^3"));
            File.WriteAllText(withoutAssertion, Source("480mm^3", "0mm^3").Replace("    Assert Volume Body {\n        Expected: 480mm^3\n        Tolerance: 0mm^3\n        Note: \"box\"\n    }\n", string.Empty, StringComparison.Ordinal));
            var asserted = FirmamentBuildAndExport.Run(withAssertion, Path.Combine(directory, "with.step"));
            var plain = FirmamentBuildAndExport.Run(withoutAssertion, Path.Combine(directory, "without.step"));
            Assert.True(asserted.IsSuccess, string.Join(Environment.NewLine, asserted.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.True(plain.IsSuccess, string.Join(Environment.NewLine, plain.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var result = Assert.Single(asserted.Value.Export.Assertions!);
            Assert.True(result.Passed, result.Diagnostic);
            Assert.Equal(File.ReadAllText(plain.Value.OutputPath), File.ReadAllText(asserted.Value.OutputPath));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BuildReportsExpectedMeasuredDeltaAndToleranceForMismatch()
    {
        var path = Path.Combine(Path.GetTempPath(), "aetheris-assert-volume-" + Guid.NewGuid().ToString("N") + ".firmament");
        try
        {
            File.WriteAllText(path, Source("479mm^3", "0mm^3"));
            var build = FirmamentBuildAndExport.Run(path, Path.ChangeExtension(path, ".step"));
            Assert.False(build.IsSuccess);
            var diagnostic = Assert.Single(build.Diagnostics).Message;
            Assert.StartsWith("firmament-v2-assert-volume-failed:", diagnostic, StringComparison.Ordinal);
            Assert.Contains("expectedMm3=479", diagnostic, StringComparison.Ordinal);
            Assert.Contains("measuredMm3=480", diagnostic, StringComparison.Ordinal);
            Assert.Contains("toleranceMm3=0", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string Source(string expected, string tolerance) => $$"""
        Model AssertBox {
            Units: mm
            Box Body { Size: [10mm, 8mm, 6mm] }
            Assert Volume Body {
                Expected: {{expected}}
                Tolerance: {{tolerance}}
                Note: "box"
            }
        }
        """;
}
