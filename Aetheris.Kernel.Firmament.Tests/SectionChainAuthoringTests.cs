using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SectionChainAuthoringTests
{
    [Theory]
    [InlineData(60, 40, 8, 0)]
    [InlineData(77.98, 163.43, 19.43, 0)]
    [InlineData(64, 44, 9, 27)]
    public void SmoothCornersRemoveFootprintCurvatureJump(double width, double height, double extent, double rotation)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/smooth-corner-sections.firmament")
            .Replace("Size: [60mm,40mm] CornerExtent: 8mm", FormattableString.Invariant($"Size: [{width}mm,{height}mm] CornerExtent: {extent}mm Rotation: {rotation}deg"));
        var result = SectionChainAuthoringParser.Compile(source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(12, result.Chain!.Sections[0].Profile.Spans.Count);
        Assert.Equal(36, result.Materialization!.GeometricJoins.Count);
        Assert.All(result.Materialization.GeometricJoins, join =>
        {
            Assert.Equal("G2WithinSampledTolerance", join.Status);
            Assert.True(join.MaximumShapeOperatorResidual < 1e-9);
        });
        Assert.True(result.Materialization.Pcurves!.LoopClosureValid);
    }

    [Theory]
    [InlineData("0mm")]
    [InlineData("-1mm")]
    [InlineData("20mm")]
    [InlineData("21mm")]
    public void SmoothCornersRejectDegenerateSideSpans(string extent)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/smooth-corner-sections.firmament")
            .Replace("CornerExtent: 8mm", "CornerExtent: " + extent);
        var result = SectionChainAuthoringParser.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains("firmament-boundary2-invalid-dimensions:Outline", result.Diagnostics);
        Assert.Null(result.Materialization);
    }

    [Fact]
    public void RoundedProfileNormalizationReachesStepAndReportsRealCurvatureJump()
    {
        var result = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText(
            "fixtures/Canonical/SectionChain/normalized-rounded-sections.firmament"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(12, result.Materialization!.ProfileNormalization!.Curves.Count);
        Assert.All(result.Materialization.GeometricJoins.Where(j => j.BoundaryKind == "NeighboringProfileSpans"), j =>
        {
            Assert.Equal("G1", j.Status);
            Assert.True(j.MaximumPositionError < 1e-10);
            Assert.True(j.MaximumNormalAngleDegrees < 1e-8);
            Assert.InRange(j.MaximumShapeOperatorResidual!.Value, 0.125-1e-9, 0.125+1e-9);
        });
        Assert.All(result.Materialization.GeometricJoins.Where(j => j.BoundaryKind == "InternalSection"),
            j => Assert.Equal("G2WithinSampledTolerance", j.Status));
    }

    [Fact]
    public void G2RequestIsNotSilentlyDowngradedToNormalizedG1()
    {
        var result = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText(
            "fixtures/Canonical/SectionChain/normalized-rounded-sections.firmament").Replace("Continuity: G1", "Continuity: G2"));
        Assert.False(result.IsSuccess);
        Assert.Contains("section-chain-continuity-invalid:G2", result.Diagnostics);
        Assert.Null(result.Materialization);
    }

    [Theory]
    [InlineData("two-section-ruled.firmament", 2, SectionTermination.Cap, SectionTermination.Cap)]
    [InlineData("six-section-ergonomic.firmament", 6, SectionTermination.Cap, SectionTermination.Cap)]
    [InlineData("eight-section-ergonomic.firmament", 8, SectionTermination.Cap, SectionTermination.Cap)]
    [InlineData("open-chain.firmament", 2, SectionTermination.Open, SectionTermination.Open)]
    [InlineData("g1-two-transition.firmament", 3, SectionTermination.Cap, SectionTermination.Cap)]
    [InlineData("g0-explicit-ruled.firmament", 2, SectionTermination.Cap, SectionTermination.Cap)]
    public void CanonicalFixtureCorpusQualifies(string name, int count, SectionTermination start, SectionTermination end)
    {
        var result = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/" + name));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(count, result.Chain!.Sections.Count);
        Assert.Equal(start, result.Chain.StartTermination);
        Assert.Equal(end, result.Chain.EndTermination);
    }

    [Fact]
    public void ContinuityIntentSelectsTheSeparatedTransitionLaw()
    {
        var g1 = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/g1-two-transition.firmament"));
        var g0 = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/g0-explicit-ruled.firmament"));
        Assert.True(g1.IsSuccess, string.Join(Environment.NewLine, g1.Diagnostics));
        Assert.True(g0.IsSuccess, string.Join(Environment.NewLine, g0.Diagnostics));
        Assert.Equal(SectionChainContinuity.G1, g1.Chain!.Continuity);
        Assert.Equal(SectionTransitionPolicy.SmoothPolynomial, g1.Chain.TransitionPolicy);
        Assert.Equal(SectionChainContinuity.G0, g0.Chain!.Continuity);
        Assert.Equal(SectionTransitionPolicy.Ruled, g0.Chain.TransitionPolicy);
    }

    [Fact]
    public void InvalidFoldoverFixtureFailsBeforeAnyStepArtifact()
    {
        var result = SectionChainAuthoringParser.Compile(FirmamentCorpusHarness.ReadFixtureText("fixtures/Invalid/SectionChain/transition-foldover.firmament"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.StartsWith("section-chain-transition-foldover:A->B/", StringComparison.Ordinal));
        Assert.Null(result.Materialization?.Body);
    }

    [Fact]
    public void CanonicalConceptPathsLowerThroughSemanticSectionChainIrAndMaterialize()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/two-section-ruled.firmament");
        var result = SectionChainAuthoringParser.Compile(source);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal("TwoSectionRuled", result.Chain!.StableId);
        Assert.Equal(2, result.Chain.Sections.Count);
        Assert.All(result.Chain.Sections, section => Assert.Equal(["Bottom", "Right", "Top", "Left"], section.Profile.Spans.Select(span => span.SpanId)));
        Assert.Equal(SectionTermination.Cap, result.Chain.StartTermination);
        Assert.Equal(SectionTermination.Cap, result.Chain.EndTermination);
        Assert.NotNull(result.Materialization?.Body);
        Assert.Equal(result.Materialization!.Body!.Topology.Coedges.Count(), result.Materialization.Pcurves!.PcurveCount);
        Assert.True(result.Materialization.Pcurves.LoopClosureValid);
    }

    [Fact]
    public void DuplicateExplicitTargetCorrespondenceFailsBeforeMaterialization()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/two-section-ruled.firmament")
            .Replace("Start: Cap", "Correspond Bad {\n From: Start\n To: End\n Bottom -> Bottom\n Right -> Bottom\n Top -> Top\n Left -> Left\n }\n Start: Cap", StringComparison.Ordinal);
        var result = SectionChainAuthoringParser.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("section-chain-correspondence-duplicate:Start:End:Bottom", StringComparison.Ordinal));
        Assert.Null(result.Materialization);
    }
}
