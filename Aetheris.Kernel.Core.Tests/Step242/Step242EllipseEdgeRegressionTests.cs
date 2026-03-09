using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242EllipseEdgeRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", "exporter", "Face:47", "Unsupported surface kind 'Sphere'.")]
    public void Step242_NistEllipseTargets_AdvancePastUnsupportedEllipse_AndRemainDeterministic(string relativePath, string expectedLayer, string expectedSource, string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-ellipse-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("EDGE_CURVE geometry 'ELLIPSE' is unsupported.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.Equal(expectedLayer, first.FirstFailureLayer);
        if (!string.IsNullOrEmpty(expectedSource))
        {
            Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
        }

        if (!string.IsNullOrEmpty(expectedMessagePrefix))
        {
            Assert.StartsWith(expectedMessagePrefix, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        if (string.Equals(relativePath, "testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", StringComparison.Ordinal))
        {
            Assert.NotEqual("Viewer.Tessellation.PlanarCurveFlatteningUnsupported", first.FirstDiagnostic.Source);
            Assert.DoesNotContain("planar curve flattening does not support curve kind 'BSpline3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
