using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalUnifiedProfileTests
{
    private static string Source(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine(directory!.FullName, "fixtures/Regression/SheetMetalSyntax", file + ".firmament"));
    }

    [Fact]
    public void SharedCompactProfilePreservesFormedStepAndFlatMaterial()
    {
        var modern = SheetMetalFirmament.Compile(Source("modern-bracket"));
        var legacy = SheetMetalFirmament.Compile(Source("legacy-bracket"));
        Assert.True(modern.IsSuccess, string.Join("\n", modern.Diagnostics.Select(d => d.Message)));
        Assert.True(legacy.IsSuccess, string.Join("\n", legacy.Diagnostics.Select(d => d.Message)));
        Assert.Equal(Step242Exporter.ExportBody(legacy.Part!.FormedBody!).Value, Step242Exporter.ExportBody(modern.Part!.FormedBody!).Value);
        Assert.Equal(legacy.FlatPattern!.MaterialArea, modern.FlatPattern!.MaterialArea);
        Assert.Equal(legacy.FlatPattern.Bounds, modern.FlatPattern.Bounds);
        Assert.Equal(legacy.Part.Regions.Select(r => r.StableId), modern.Part.Regions.Select(r => r.StableId));
        Assert.True(FirmamentBuildAndExport.CompileSource(Source("mechanical")).IsSuccess);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(Source("modern-bracket"), "blank", out var errors);
        Assert.Empty(errors);
        Assert.Equal(4000, ResolvedProfile2DValidator.Validate(profile!).SignedArea, 8);
    }

    [Fact]
    public void ModernSlotHasExactRoundedGeometryAndLegacySlotStaysRectangular()
    {
        var source = Source("modern-bracket");
        var flange = "  Flange wall { from: deck.Rear; length: 30mm; angle: 90deg; insideRadius: 2mm; direction: Up }";
        source = source.Replace(flange, "", StringComparison.Ordinal);
        var rounded = SheetMetalFirmament.Compile(source.Replace("Rect2 { center: [0mm, 0mm]; size: [12mm, 6mm] }",
            "Slot2 { center: [0mm, 0mm]; length: 12mm; width: 6mm }", StringComparison.Ordinal));
        Assert.True(rounded.IsSuccess, string.Join("\n", rounded.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4000 - (6 * 6 + Math.PI * 9), rounded.FlatPattern!.MaterialArea!.Value, 7);
        Assert.Equal(2, Assert.Single(rounded.Part!.Features).ExactContour!.OuterLoop.Segments.Count(s => s.Geometry is LineArcCircularArc2D));
        Assert.True(Step242Exporter.ExportBody(rounded.Part.FormedBody!).IsSuccess);
        var legacy = SheetMetalFirmament.Compile(Source("legacy-bracket").Replace("Profile: Rectangle { Length: 12mm; Width: 6mm; }", "Profile: Slot { Length: 12mm; Width: 6mm; }", StringComparison.Ordinal));
        var rectangular = SheetMetalFirmament.Compile(Source("legacy-bracket"));
        Assert.True(legacy.IsSuccess);
        Assert.Equal(rectangular.FlatPattern!.MaterialArea, legacy.FlatPattern!.MaterialArea);
    }

    [Theory]
    [InlineData("size: [80mm, 50mm]", "szie: [80mm, 50mm]", "szie")]
    [InlineData("profile: opening", "profile: missing", "missing")]
    [InlineData("at: Point2(30mm, 20mm)", "at: Point2(30mm, 20mm); heigth: 2mm", "heigth")]
    [InlineData("Base deck {", "Base deck on XZ {", "placement")]
    [InlineData("Base deck {", "Base: Rectangle(1mm, 1mm); Base deck {", "one Base")]
    [InlineData("Rect2 { center: [40mm, 25mm]; size: [80mm, 50mm] }", "Circle2 { center: [40mm, 25mm]; radius: 20mm }", "rectangular")]
    [InlineData("Rect2 { center: [0mm, 0mm]; size: [12mm, 6mm] }", "Ellipse2 { center: [0mm, 0mm]; axisLengths: [12mm, 6mm] }", "ellipses")]
    public void UnsupportedOrUnknownProfileIntentCannotBuild(string before, string after, string detail)
    {
        var result = SheetMetalFirmament.Compile(Source("modern-bracket").Replace(before, after, StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Part);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains(detail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProfileOriginBecomesPhysicalBaseOriginRatherThanBeingRecentered()
    {
        var source = Source("modern-bracket").Replace("center: [40mm, 25mm]", "center: [50mm, 45mm]", StringComparison.Ordinal);
        var result = SheetMetalFirmament.Compile(source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(10, result.Spec!.Base.Origin.X);
        Assert.Equal(20, result.Spec.Base.Origin.Y);
    }

    [Fact]
    public void CompactProfileSpecializesThroughTheExistingTemplateOwner()
    {
        var source = Source("mechanical-template");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
    }

    [Fact]
    public void ReusedProfileGetsDistinctCutIdentitiesAndRetainsSourceProvenance()
    {
        var source = Source("modern-bracket").Replace("Cut port {", "Cut secondPort { on: deck; profile: opening; at: Point2(60mm, 20mm) }\n  Cut port {", StringComparison.Ordinal);
        var result = SheetMetalFirmament.Compile(source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var segments = result.Part!.Features.SelectMany(f => f.ExactContour!.OuterLoop.Segments).ToArray();
        Assert.Equal(8, segments.Select(s => s.StableId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(segments, s => Assert.NotNull(s.Provenance.TracedFrom));
    }

    [Fact]
    public void XzProfileCannotSilentlyBecomeAnXySheetBase()
    {
        var result = SheetMetalFirmament.Compile(Source("unsupported-xz"));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Part);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("sheetmetal-profile-frame-unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyCircleCannotSilentlyDiscardRectangularDimensions()
    {
        var source = Source("legacy-bracket").Replace("Rectangle { Length: 12mm; Width: 6mm; }", "Circle { Diameter: 6mm; Width: 3mm; }", StringComparison.Ordinal);
        var result = SheetMetalFirmament.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Width", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Circle")]
    [InlineData("Slot")]
    [InlineData("Rectangle")]
    public void DeclaredProfileNamesResolveBeforeLegacyShapeTags(string name)
    {
        var result = SheetMetalFirmament.Compile(Source("modern-bracket").Replace("opening", name, StringComparison.Ordinal));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(Assert.Single(result.Part!.Features).ExactContour);
    }

    [Theory]
    [InlineData("Circle2 { center: [0mm, 0mm]; radius: 10mm }", 314.1592653589793)]
    [InlineData("Slot2 { center: [0mm, 0mm]; length: 20mm; width: 8mm }", 146.2654824574367)]
    [InlineData("RoundedRect2 { center: [0mm, 0mm]; size: [20mm, 10mm]; radius: 2mm }", 196.5663706143592)]
    public void CompactMechanicalShapesUseTheirExistingExactArea(string shape, double expectedArea)
    {
        var source = Source("mechanical").Replace("Rect2 { center: [40mm, 25mm]; size: [80mm, 50mm] }", shape, StringComparison.Ordinal);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "blank", out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.Equal(expectedArea, ResolvedProfile2DValidator.Validate(profile!).SignedArea, 8);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
    }
}
