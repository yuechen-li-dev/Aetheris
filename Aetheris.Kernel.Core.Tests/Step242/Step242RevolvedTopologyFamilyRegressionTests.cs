using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242RevolvedTopologyFamilyRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", "Face 43 curved tessellation supports selected repeated torus/revolved boundary subfamilies; unsupported subfamily 'other (coedges=6, uniqueEdges=6)'. Observed")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", "Face 25 curved tessellation does not support this torus/revolved boundary topology yet. Observed")]
    public void Step242_RepeatedCurvedRevolvedTargets_AdvanceWithExplicitDeterministicNextBlocker(
        string relativePath,
        string expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-revolved-family-regression",
            Notes: "advanced repeated family targets",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);

        Assert.DoesNotContain("supports repeated torus/revolved families with mixed line/circle loops; this topology family is still unsupported", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("supports repeated cone/revolved families with mixed line/circle loops; this topology family is still unsupported", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("unsupported subfamily 'other (coedges=3, uniqueEdges=3)'", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.StartsWith(expectedMessagePrefix, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/STC/nist_stc_08_asme1_ap242-e3.stp")]
    public void Step242_RevolvedTopologyFamilyBlockers_StayDeterministic_AndRemainExplicit(string relativePath)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-revolved-family-regression",
            Notes: "explicit remaining blockers",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal("tessellator", first.FirstFailureLayer);
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);

        Assert.Contains("curved tessellation", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.Contains("Observed", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(first.FirstDiagnostic.MessagePrefix));
    }
}
