using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242EllipseEdgeRegressionTests
{
    [Theory]
    [InlineData(0.0000006, 0.0000006, true)]
    [InlineData(0.0000008, 0.0000008, false)]
    [InlineData(0.0000011, 0, false)]
    public void EllipseTrimQualifiesTheFullEndpointDistanceInMillimetres(double radialOffset, double normalOffset, bool accepted)
    {
        var ellipse = new Ellipse3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)),
            0.3d, 0.15d, Direction3D.Create(new Vector3D(1, 0, 0)));
        var start = new Point3D(ellipse.MajorRadius + radialOffset, 0, normalOffset);
        var trim = Step242Importer.ComputeEllipseTrim(ellipse, start, ellipse.Evaluate(double.Pi / 2), edgeSameSense: true);

        Assert.Equal(accepted, trim.IsSuccess);
        if (accepted)
            Assert.Equal(double.Pi / 2, trim.Value.End, 12);
        else
            Assert.Equal("Importer.Geometry.EllipseTrim.OffRadius", Assert.Single(trim.Diagnostics).Source);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", "", "Audit.None", "No diagnostics.")]
    [Trait("Category", "SlowCorpus")]
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

        Assert.DoesNotContain("EDGE_CURVE geometry 'ELLIPSE' is unsupported.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported curve kind 'Ellipse3'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(expectedLayer))
        {
            Assert.Equal(expectedLayer, first.FirstFailureLayer);
        }

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
            Assert.DoesNotContain("Unsupported surface kind 'Sphere'.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

    }
}
