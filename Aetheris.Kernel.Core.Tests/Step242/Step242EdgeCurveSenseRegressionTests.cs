using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242EdgeCurveSenseRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp", "Importer.LoopRole.CylinderNonNormalizableDegenerateProjection", "Cylinder loop normalization failed")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp", "Importer.LoopRole.InnerCrossesOuterAfterNormalization", "Inner loop could not be normalized")]
    public void Step242_NistEdgeCurveSenseTargets_AdvanceToNextBlocker_Deterministically(
        string relativePath,
        string expectedSource,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-edge-orientation-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.NotEqual("Importer.Orientation.EdgeCurveSense", first.FirstDiagnostic.Source);
        Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
        Assert.StartsWith(expectedMessagePrefix, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
