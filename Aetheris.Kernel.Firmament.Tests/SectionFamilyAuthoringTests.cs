using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SectionFamilyAuthoringTests
{
    [Fact]
    public void GuitarNeckHasStableKeyedSectionsAndCorrespondingHeadRoot()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/Neck.firmament");
        var result = SectionChainAuthoringParser.CompileFile(path);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(new[] { "Sections_Heel", "Sections_HeelShoulder", "Sections_HeelBlend", "Sections_ShaftStart", "Sections_ShaftMiddle", "Sections_PreNut", "Sections_NutApproach", "Sections_Nut", "HeadBlend", "HeadMerge" }, result.Chain!.Sections.Select(s => s.SectionId));
        var pattern = Assert.Single(result.Patterns!);
        Assert.Equal(8, pattern.GeneratedCount);
        Assert.Equal("Nut", pattern.Associations![7].SourceEntry);
        foreach (var section in result.Chain.Sections)
        {
            Assert.Equal(new[] { "Edge0", "Edge1", "Edge2", "Edge3", "Edge4", "Edge5" }, section.Profile.Spans.Select(s => s.SpanId));
            if (section.SectionId == "HeadMerge")
            {
                Assert.All(section.Profile.Spans, span => Assert.IsType<SectionProfileCurve.Line>(span.Curve));
                continue;
            }
            var back = Assert.IsType<SectionProfileCurve.PolynomialBSpline>(section.Profile.Spans[0].Curve);
            Assert.InRange(back.ControlPoints[0].X, -28, -21);
            Assert.Equal(0, back.ControlPoints[0].Y);
        }
    }

    [Theory]
    [InlineData("MahoganyBack", 2)]
    [InlineData("IvoryBinding", 2)]
    [InlineData("CarvedMaple", 5)]
    public void BodyFamiliesPreserveOutlineIdentity(string name, int count)
    {
        var result = SectionChainAuthoringParser.CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/Canonical/AssemblyInterfaces/GuitarX0/{name}.firmament"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(count, result.Chain!.Sections.Count);
        Assert.Equal(count, Assert.Single(result.Patterns!).GeneratedCount);
        Assert.All(result.Chain.Sections, section => Assert.Equal("LowerTreble_Span0", section.Profile.SeamSpanId));
        Assert.All(result.BoundaryEdits!, section => Assert.All(section.Edits, edit => Assert.Equal("PeriodicHermite", edit.Construction)));
    }

    [Fact]
    public void DuplicateStationKeysFailBeforeGeometry()
    {
        var source = "Model X { Units: mm Record S { Z: Length } Static Sites: Set<S> { A => S { Z: 0mm } A => S { Z: 10mm } } SectionChain X { Pattern Sections Over Sites { s => Section Site { Frame: Plane { Origin: [0mm,0mm,s.Z]; Normal: [0,0,1]; Up: [0,1,0] } Profile: Missing Seam: Edge0 } } } }";
        var result = SectionChainAuthoringParser.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Contains("set-duplicate-entry"));
        Assert.Null(result.Materialization);
    }
}
