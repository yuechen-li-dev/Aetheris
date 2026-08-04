using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PrismaticProfileCompositionRoundTripTests
{
    [Fact]
    public void MixedLineArcAdditiveOverlap_PreservesAnalyticCurvesAcrossStepRoundTrip()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "FirmamentV2", "ProfileComposition", "mixed-line-arc-additive-overlap.firmament")));
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var top = Assert.Single(stack.Slabs.Where(x => x.From == 10d && x.To == 15d));
        Assert.Equal(458.72298071147134d, PrismaticSectionStackCompiler.Area(top.Region), 8);
        Assert.NotNull(top.Arrangement);
        Assert.Equal(4, top.Region.Outer.Loops[0].Segments.Count(x => x.Geometry is LineArcCircularArc2D));
        Assert.True(top.Arrangement!.AtomicFragments.Count > top.Arrangement.RetainedBoundaryFragmentCount);
        Assert.Contains(top.Arrangement.AtomicFragments, x => x.Source.Segment == "ArcEast" && x.StableId.EndsWith("part1", StringComparison.Ordinal));

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.DoesNotContain(emitted.Diagnostics, x => x.Contains("rejected", StringComparison.Ordinal));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var inMemory = BrepMassProperties.Evaluate(body);
        Assert.True(inMemory.IsEnclosed, string.Join(" | ", inMemory.Diagnostics));
        Assert.InRange(Math.Abs(inMemory.AbsoluteVolume - stack.AnalyticVolume), 0d, 0.01d);
        Assert.Equal(4, body.Geometry.Surfaces.Count(x => x.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder));
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        Assert.DoesNotContain("B_SPLINE", step.Value, StringComparison.OrdinalIgnoreCase);
        var reimported = Step242Importer.ImportBody(step.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(x => x.Message)));
        var reimportedMass = BrepMassProperties.Evaluate(reimported.Value);
        Assert.True(reimportedMass.IsEnclosed, string.Join(" | ", reimportedMass.Diagnostics));
        Assert.InRange(Math.Abs(reimportedMass.AbsoluteVolume - stack.AnalyticVolume), 0d, 0.01d);
    }

    [Theory]
    [InlineData("additive", 6400d, 480d, 0)]
    [InlineData("removal", 5520d, 304d, 1)]
    public void ProperOverlaps_NormalizeToOneExactSectionStack(string kind, double expectedVolume, double expectedTopArea, int expectedHoles)
    {
        var parsed = PrismaticProfileCompositionParser.Parse(OverlapSource(kind));
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var top = Assert.Single(stack.Slabs.Where(x => x.From == 10d && x.To == 15d));
        Assert.Equal(expectedTopArea, PrismaticSectionStackCompiler.Area(top.Region), 6);
        Assert.Equal(expectedHoles, top.Region.Holes.Count);
        Assert.NotNull(top.Arrangement);
        Assert.True(top.Arrangement!.IntersectionVertices.Count > 0);
        Assert.True(top.Arrangement.RetainedBoundaryFragmentCount < top.Arrangement.AtomicFragments.Count || top.Arrangement.CoincidentFragmentCount > 0);
        Assert.Equal(expectedVolume, stack.AnalyticVolume, 6);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        Assert.Equal(expectedVolume, BrepMassProperties.Evaluate(body).AbsoluteVolume, 6);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        var reimported = Step242Importer.ImportBody(step.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(x => x.Message)));
        Assert.Equal(expectedVolume, BrepMassProperties.Evaluate(reimported.Value).AbsoluteVolume, 6);
    }

    [Theory]
    [InlineData("shallow-pocket", 3808d)]
    [InlineData("through-cut", 3640d)]
    public void NestedRect2Composition_PreservesAnalyticVolumeAcrossStepRoundTrip(string kind, double expectedVolume)
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source(kind));
        Assert.Empty(parsed.Diagnostics);

        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var normalizeDiagnostics));
        Assert.Empty(normalizeDiagnostics);
        Assert.Equal(expectedVolume, stack.AnalyticVolume, 6);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.DoesNotContain(emitted.Diagnostics, d => d.StartsWith("compose-rejected", StringComparison.Ordinal));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(emitted.Body);
        var inMemory = BrepMassProperties.Evaluate(body);
        Assert.True(inMemory.IsEnclosed, string.Join(" | ", inMemory.Diagnostics));
        Assert.Equal(expectedVolume, inMemory.AbsoluteVolume, 6);

        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        var reimported = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(reimported.IsEnclosed, string.Join(" | ", reimported.Diagnostics));
        Assert.Equal(expectedVolume, reimported.AbsoluteVolume, 6);
    }

    private static string Source(string kind) => $$"""
        Concept Struct Layout On XY {
            Rect2 Base { Center: [0mm, 0mm]; Size: [20mm, 20mm]; Role: ProfileGuide }
            Rect2 Inner { Center: [0mm, 0mm]; Size: [{{(kind == "shallow-pocket" ? 8 : 6)}}mm, {{(kind == "shallow-pocket" ? 8 : 6)}}mm]; Role: ProfileGuide }
        }
        Profile Plate Using Layout { Loop Outer {
            Segment South { Trace: Base.Bottom; From: Base.BottomLeft; To: Base.BottomRight }
            Segment East { Trace: Base.Right; From: Base.BottomRight; To: Base.TopRight }
            Segment North { Trace: Base.Top; From: Base.TopRight; To: Base.TopLeft }
            Segment West { Trace: Base.Left; From: Base.TopLeft; To: Base.BottomLeft }
        } }
        Profile Void Using Layout { Loop Outer {
            Segment South { Trace: Inner.Bottom; From: Inner.BottomLeft; To: Inner.BottomRight }
            Segment East { Trace: Inner.Right; From: Inner.BottomRight; To: Inner.TopRight }
            Segment North { Trace: Inner.Top; From: Inner.TopRight; To: Inner.TopLeft }
            Segment West { Trace: Inner.Left; From: Inner.TopLeft; To: Inner.BottomLeft }
        } }
        Struct Composition { Compose Body {
            Base Stock { Profile: Plate; From: 0mm; To: 10mm; Role: Stock }
            Remove Cut { Profile: Void; From: {{(kind == "shallow-pocket" ? 7 : 0)}}mm; To: 10mm; Role: Relief }
        } }
        """;

    private static string OverlapSource(string kind) => $$"""
        Concept Struct Layout On XY {
            Rect2 BaseGuide { Center: [0mm, 0mm]; Size: [20mm, 20mm]; Role: ProfileGuide }
            Rect2 LeftGuide { Center: [-3mm, 10mm]; Size: [10mm, 10mm]; Role: ProfileGuide }
            Rect2 RightGuide { Center: [3mm, 10mm]; Size: [10mm, 10mm]; Role: ProfileGuide }
            Rect2 VoidLeftGuide { Center: [-2mm, 0mm]; Size: [8mm, 8mm]; Role: ProfileGuide }
            Rect2 VoidRightGuide { Center: [2mm, 0mm]; Size: [8mm, 8mm]; Role: ProfileGuide }
        }
        {{RectProfile("BaseProfile", "BaseGuide")}}
        {{RectProfile("LeftProfile", kind == "additive" ? "LeftGuide" : "VoidLeftGuide")}}
        {{RectProfile("RightProfile", kind == "additive" ? "RightGuide" : "VoidRightGuide")}}
        Struct Composition { Compose Body {
            Base Stock { Profile: BaseProfile; From: 0mm; To: 15mm; Role: Stock }
            {{(kind == "additive" ? "Add Left { Profile: LeftProfile; From: 10mm; To: 15mm; Role: Pad }\n            Add Right { Profile: RightProfile; From: 10mm; To: 15mm; Role: Pad }" : "Remove Left { Profile: LeftProfile; From: 10mm; To: 15mm; Role: Relief }\n            Remove Right { Profile: RightProfile; From: 10mm; To: 15mm; Role: Relief }")}}
        } }
        """;

    private static string RectProfile(string profile, string guide) => $$"""
        Profile {{profile}} Using Layout { Loop Outer {
            Segment South { Trace: {{guide}}.Bottom; From: {{guide}}.BottomLeft; To: {{guide}}.BottomRight }
            Segment East { Trace: {{guide}}.Right; From: {{guide}}.BottomRight; To: {{guide}}.TopRight }
            Segment North { Trace: {{guide}}.Top; From: {{guide}}.TopRight; To: {{guide}}.TopLeft }
            Segment West { Trace: {{guide}}.Left; From: {{guide}}.TopLeft; To: {{guide}}.BottomLeft }
        } }
        """;
}
