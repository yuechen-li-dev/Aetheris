using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PlanarTextProfilesTests
{
    [Fact]
    public void InterUsesOwnedFontAndPreservesOCounterAsExactCurves()
    {
        var text = PlanarTextProfiles.Build("CO", 3, PlanarTextAlignment.Center, featureId: "MakerMark");
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        Assert.Equal(2, text.Glyphs.Count);
        Assert.Equal("CO".Select(c => (int)c), text.Glyphs.Select(g => g.Codepoint));
        Assert.True(text.CounterCount >= 1);
        Assert.Contains(text.Regions, r => r.Name.Contains("Glyph[1]", StringComparison.Ordinal) && r.Loops.Any(l => !l.IsOuter));
        Assert.Contains(text.Regions.SelectMany(r => r.Loops).SelectMany(l => l.Segments),
            s => s.Geometry is LineArcCubicBezier2D);
        Assert.Contains(text.Regions.SelectMany(r => r.Loops).SelectMany(l => l.Segments),
            s => s.Provenance.SourceSpan == "source-char:1..2"
                && s.Provenance.Derivation.Contains("Glyph[1]:U+004F", StringComparison.Ordinal));
        Assert.All(text.Regions, r => Assert.True(ResolvedProfile2DValidator.Validate(r).IsValid,
            string.Join("; ", ResolvedProfile2DValidator.Validate(r).Diagnostics)));
    }

    [Fact]
    public void AdvanceAndHeightAreDeterministic()
    {
        var first = PlanarTextProfiles.Build("CO", 3);
        var second = PlanarTextProfiles.Build("CO", 3);
        var taller = PlanarTextProfiles.Build("CO", 6);
        Assert.True(first.Succeeded, string.Join("; ", first.Diagnostics));
        Assert.Equal(first.Advance, second.Advance);
        Assert.Equal(first.Glyphs.Select(g => g.BaselineX), second.Glyphs.Select(g => g.BaselineX));
        Assert.Equal(first.Advance * 2, taller.Advance, 8);
        Assert.Equal(first.ContourCount, taller.ContourCount);
    }

    [Fact]
    public void InterXSelfCrossingNormalizesToAValidRegion()
    {
        var text = PlanarTextProfiles.Build("X", 3);
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        Assert.True(text.ResolvedIntersections > 0);
        Assert.Single(text.Regions);
        Assert.All(text.Regions, r => Assert.True(ResolvedProfile2DValidator.Validate(r).IsValid));
    }

    [Fact]
    public void InterDOverlapNormalizesWithItsCounter()
    {
        var text = PlanarTextProfiles.Build("D", 3);
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        Assert.True(text.ResolvedIntersections > 0);
        Assert.Single(text.Regions);
        Assert.Equal(1, text.CounterCount);
        Assert.Equal(2, text.Regions[0].Loops.Count);
    }

    [Theory]
    [InlineData("CODEX")]
    [InlineData("AETHERIS")]
    public void FullWordsNormalizeToValidExactProfiles(string word)
    {
        var text = PlanarTextProfiles.Build(word, 3, PlanarTextAlignment.Center);
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        Assert.Equal(word.Length, text.Glyphs.Count);
        Assert.Equal(word.Length, text.Regions.Count);
        Assert.Equal(2, text.CounterCount);
        Assert.Equal(word == "CODEX" ? 7 : 10, text.ContourCount);
        Assert.Equal(word == "CODEX" ? 6 : 9, text.ResolvedIntersections);
        Assert.All(text.Regions, r => Assert.True(ResolvedProfile2DValidator.Validate(r).IsValid,
            string.Join("; ", ResolvedProfile2DValidator.Validate(r).Diagnostics)));
        Assert.Contains(text.Regions.SelectMany(r => r.Loops).SelectMany(l => l.Segments),
            s => s.Geometry is LineArcCubicBezier2D);
    }

    [Fact]
    public void CodexRegionsExtrudeThroughTheExactProfileEmitter()
    {
        var text = PlanarTextProfiles.Build("CODEX", 2.5, PlanarTextAlignment.Center);
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        foreach (var region in text.Regions)
        {
            var extrusion = ResolvedProfile2DValidator.Extrude(region, 0.2);
            Assert.True(extrusion.Status == LineArcProfileExtrudeStatus.Succeeded,
                region.Name + ": " + string.Join("; ", extrusion.Diagnostics));
            Assert.NotNull(extrusion.Body);
        }
    }

    [Theory]
    [InlineData("D", 743589.4166666667)]
    [InlineData("X", 607590.0809529623)]
    public void NormalizedAreaMatchesIndependentSkiaPathOpsOracle(string glyph, double fontUnitsSquared)
    {
        var text = PlanarTextProfiles.Build(glyph, 3);
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        double LoopArea(ResolvedProfileLoop2D loop) => Math.Abs(loop.Segments.Sum(s =>
            ResolvedProfile2DValidator.SignedAreaContribution(s.Geometry)));
        var area = text.Regions.Sum(region => LoopArea(region.Loops.Single(loop => loop.IsOuter))
            - region.Loops.Where(loop => !loop.IsOuter).Sum(LoopArea));
        Assert.Equal(fontUnitsSquared * Math.Pow(3d / 2048d, 2), area, 4);
    }

    [Fact]
    public void DStrokeCounterAndExteriorMatchIndependentFillOracle()
    {
        var region = Assert.Single(PlanarTextProfiles.Build("D", 3).Regions);
        (double X, double Y) Scale(int x, int y) => (x * 3d / 2048d, y * 3d / 2048d);
        Assert.True(GlyphRegionNormalizer.Contains(region, Scale(250, 1000)));
        Assert.False(GlyphRegionNormalizer.Contains(region, Scale(500, 1000)));
        Assert.False(GlyphRegionNormalizer.Contains(region, Scale(-100, 1000)));
    }

    [Fact]
    public void SectionArrangementAcceptsItsExactCubicBoundary()
    {
        var profile = Assert.Single(PlanarTextProfiles.Build("O", 3).Regions);
        var operation = new PrismaticProfileOperation("Text", PrismaticProfileIntent.Add,
            profile.Name, 0, 0.2, "Text", "test");
        var result = ProfileArrangementBuilder.Compose("XY", [operation],
            new Dictionary<string, ResolvedProfile2D> { [profile.Name] = profile }, "text-profile-probe");
        Assert.NotNull(result.Region);
        Assert.DoesNotContain(result.Arrangement.Diagnostics,
            d => d.StartsWith("arrangement-rejected", StringComparison.Ordinal));
    }

    [Fact]
    public void OCounterExtrudesThroughExistingExactProfilePath()
    {
        var text = PlanarTextProfiles.Build("O", 3);
        Assert.True(text.Succeeded, string.Join("; ", text.Diagnostics));
        var region = Assert.Single(text.Regions);
        Assert.Equal(2, region.Loops.Count);
        var result = ResolvedProfile2DValidator.Extrude(region, 0.25);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, result.Status);
        Assert.NotNull(result.Body);
    }

    [Fact]
    public void UnicodeAndInvalidInputsHaveControlledResults()
    {
        var accented = PlanarTextProfiles.Build("É", 3);
        Assert.True(accented.Succeeded, string.Join("; ", accented.Diagnostics));
        Assert.Single(accented.Glyphs);
        Assert.Contains("text-height-must-be-positive", PlanarTextProfiles.Build("A", 0).Diagnostics);
        Assert.Contains("text-multiline-unsupported-x0", PlanarTextProfiles.Build("A\nB", 3).Diagnostics);
    }
}
