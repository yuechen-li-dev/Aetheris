using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242LoopRoleNormalizationRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", "Importer.LoopRole.InnerCrossesOuterAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp", "Importer.LoopRole.InnerCrossesOuterAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", "Importer.LoopRole.InnerCrossesOuterAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", "Importer.LoopRole.InnerCrossesOuterAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp", "Importer.LoopRole.InnerCrossesOuterAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp", null, null)]
    [InlineData("testdata/step242/nist/STC/nist_stc_09_asme1_ap242-e3.stp", null, null)]
    public void Step242_NistLoopRoleTargets_ProgressDeterministically_WithExplicitNormalizedBoundaries(
        string relativePath,
        string? expectedSource,
        string? expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-loop-role-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        if (expectedSource is null)
        {
            Assert.DoesNotContain("Loop projection is degenerate.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Inner loop is not contained by outer loop.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Importer.LoopRole.DegenerateLoop", first.FirstDiagnostic.Source, StringComparison.Ordinal);
            Assert.DoesNotContain("Importer.LoopRole.InnerNotContained", first.FirstDiagnostic.Source, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
            Assert.StartsWith(expectedMessagePrefix!, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
