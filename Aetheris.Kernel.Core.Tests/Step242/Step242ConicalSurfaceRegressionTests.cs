using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ConicalSurfaceRegressionTests
{
    [Fact]
    public void Step242_ConicalSurfaceDecoder_AcceptsZeroRadiusWithDegreeSemiAngle()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n"
            + "#10=CONICAL_SURFACE('',#20,0.,59.);\n"
            + "#20=AXIS2_PLACEMENT_3D('',#21,#22,#23);\n"
            + "#21=CARTESIAN_POINT('',(1.,2.,3.));\n"
            + "#22=DIRECTION('',(0.,0.,1.));\n"
            + "#23=DIRECTION('',(1.,0.,0.));\n"
            + "ENDSEC;\nEND-ISO-10303-21;";

        var parse = Step242SubsetParser.Parse(text);
        Assert.True(parse.IsSuccess);

        var coneEntityResult = parse.Value.TryGetEntity(10, "CONICAL_SURFACE");
        Assert.True(coneEntityResult.IsSuccess);

        var cone = Step242SubsetDecoder.ReadConicalSurface(parse.Value, coneEntityResult.Value);
        Assert.True(cone.IsSuccess);

        Assert.Equal(1d, cone.Value.Apex.X, 9);
        Assert.Equal(2d, cone.Value.Apex.Y, 9);
        Assert.Equal(3d, cone.Value.Apex.Z, 9);
        Assert.Equal(59d * (double.Pi / 180d), cone.Value.SemiAngleRadians, 12);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp", "", "Audit.None", "No diagnostics.")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp", "exporter", "Face:47", "Unsupported surface kind 'Sphere'.")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp", "", "", "")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", "tessellator", "", "Face 1 curved tessellation supports repeated cone/revolved families with mixed line/circle loops; this topology family is still unsupported. Observed")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp", "tessellator", "", "Face 2 curved tessellation does not support this torus/revolved boundary topology yet. Observed")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp", "tessellator", "", "Face 73 curved tessellation does not support this torus/revolved boundary topology yet. Observed")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", "tessellator", "", "Face 14 curved tessellation supports repeated torus/revolved families with mixed line/circle loops; this topology family is still unsupported. Observed")]
    [InlineData("testdata/step242/nist/STC/nist_stc_09_asme1_ap242-e3.stp", "", "", "")]
    public void Step242_NistCurvedRevolvedTargets_AdvancePastOldTopologyFamily_AndReportDeterministicNextBlocker(
        string relativePath,
        string expectedLayer,
        string expectedSource,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-conical-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.DoesNotContain("CONICAL_SURFACE radius", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("expected mirrored line uses for this cone/revolved topology", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("supports four-coedge torus/revolved loop layouts", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported surface kind 'Cylinder'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported surface kind 'Cone'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported curve kind 'Circle3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsupported curve kind 'BSpline3'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
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
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
        Assert.False(string.IsNullOrWhiteSpace(first.FirstDiagnostic.MessagePrefix));
    }

    [Theory]
    [InlineData(
        "testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp",
        "exporter",
        "Face:47",
        "Unsupported surface kind 'Sphere'.")]
    [InlineData(
        "testdata/step242/nist/STC/nist_stc_10_asme1_ap242-e2.stp",
        "importer-topology",
        "Importer.LoopRole.InnerDisjointAfterNormalization",
        "Inner loop could not be normalized")]
    public void Step242_NistTargets_AdvancePastCircleTrim_AndKeepDeterministicNextBlocker(
        string relativePath,
        string expectedLayer,
        string expectedSource,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-circle-trim-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

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
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }

}
