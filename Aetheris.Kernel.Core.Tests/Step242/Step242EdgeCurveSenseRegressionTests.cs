using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242EdgeCurveSenseRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp")]
    public void Step242_NistEdgeCurveSenseTargets_AdvanceToNextBlocker_Deterministically(
        string relativePath)
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
        Assert.NotEqual("Importer.LoopRole.CylinderNonNormalizableDegenerateProjection", first.FirstDiagnostic.Source);
        Assert.NotEqual("Importer.LoopRole.UnsupportedSurfaceForHoles", first.FirstDiagnostic.Source);
        Assert.False(string.IsNullOrWhiteSpace(first.FirstDiagnostic.MessagePrefix));

        if (string.Equals(relativePath, "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp", StringComparison.Ordinal))
        {
            Assert.StartsWith("Importer.LoopRole.UnsupportedSurfaceForHoles.", first.FirstDiagnostic.Source, StringComparison.Ordinal);
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
