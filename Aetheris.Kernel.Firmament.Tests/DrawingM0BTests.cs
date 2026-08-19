using System.Text;
using Aetheris.Kernel.Firmament.Drawing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DrawingM0BTests
{
    [Fact]
    public void AssemblyDrawing_PreservesOccurrences_BuildsBom_HlrZonesAndStructuredMetadata()
    {
        using var temporary = new TemporaryDirectory();
        var result = FirmamentDrawingCompiler.Compile(Fixture(), temporary.Path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var drawing = result.Artifacts!.Drawing;
        Assert.Equal("Assembly", drawing.Provenance.SourceKind);
        Assert.Equal("Machine", drawing.Provenance.SourceProductIdentity);
        Assert.Equal("MachineDrawingInfo", drawing.Metadata.StaticIdentity);
        Assert.Equal("1.1.0", drawing.Metadata.Revision!.ToString());
        Assert.Equal("2026-08-10", drawing.Metadata.Date);
        Assert.Contains("derivedFrom:AsterDrawingDefaults", drawing.Metadata.StaticProvenance, StringComparison.Ordinal);
        Assert.All(drawing.Pages, page => { Assert.Equal(24, page.ZoneScheme!.Zones.Count); Assert.NotNull(page.InformationBlock); Assert.Matches("^[A-D][1-6]$", page.InformationBlock!.Location.Zone); });

        var views = drawing.Pages.SelectMany(page => page.Views).ToArray();
        Assert.All(views, view => Assert.Equal(9, view.Primitives.Select(item => item.OccurrenceIdentity).Distinct(StringComparer.Ordinal).Count()));
        Assert.Contains(views.SelectMany(view => view.Primitives), item => item.OccurrenceIdentity == "Machine.LeftModule.Housing");
        Assert.Contains(views.SelectMany(view => view.Primitives), item => item.OccurrenceIdentity == "Machine.RightModule.Shaft");
        Assert.Contains(views, view => view.VisibilityEvidence!.HiddenSegments > 0);
        Assert.Contains(views, view => view.VisibilityEvidence!.SplitPointCount > 0);
        Assert.Contains(views.Single(view => view.Identity == "Front").Primitives, item => item.Kind == DrawingPrimitiveKind.Hidden);

        var bom = drawing.Pages.Single(page => page.Bom is not null).Bom!;
        Assert.Equal("Flattened leaf parts; aggregate identical definition identities; deterministic lexical ordering.", bom.FlatteningPolicy);
        Assert.Equal([3, 2, 4], bom.Items.Select(item => item.Quantity));
        Assert.Equal(DrawingTableKind.BillOfMaterials, bom.Table.Kind);
        Assert.Equal(9, bom.Items.Sum(item => item.Quantity));

        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(result.Artifacts.PdfPath));
        Assert.Contains("/BaseFont /Inter", pdf, StringComparison.Ordinal);
        Assert.Contains("/FontFile2", pdf, StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", pdf, StringComparison.Ordinal);
        Assert.Contains("[5 3] 0 d", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/Subtype /Image", pdf, StringComparison.Ordinal);
        Assert.Contains("class=\"hidden\"", File.ReadAllText(result.Artifacts.SvgPath), StringComparison.Ordinal);
    }

    [Fact]
    public void PortraitPage_HasExactA4AndStableZoneAddresses()
    {
        using var temporary = new TemporaryDirectory();
        var source = File.ReadAllText(Fixture()).Replace("Orientation: Landscape", "Orientation: Portrait", StringComparison.Ordinal);
        var path = Path.Combine(temporary.Path, "portrait.firmament"); File.WriteAllText(path, source);
        var result = FirmamentDrawingCompiler.Compile(path, Path.Combine(temporary.Path, "out"));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.All(result.Artifacts!.Drawing.Pages, page =>
        {
            Assert.Equal((210d, 297d), (page.WidthMillimetres, page.HeightMillimetres));
            Assert.Equal("A1", page.ZoneScheme!.Zones.First().Address);
            Assert.Equal("D6", page.ZoneScheme.Zones.Last().Address);
            Assert.All(page.Views.Select(view => view.Location).Concat(page.Tables.Select(table => table.Location)).Where(location => location is not null), location => Assert.Matches("^[A-D][1-6]$", location!.Zone));
        });
    }

    [Fact]
    public void VisibleOnly_OmitsHiddenIntervals_ButRetainsClassifierEvidence()
    {
        using var temporary = new TemporaryDirectory();
        var source = File.ReadAllText(Fixture()).Replace("HiddenLines: VisibleAndHidden", "HiddenLines: VisibleOnly", StringComparison.Ordinal);
        var path = Path.Combine(temporary.Path, "visible-only.firmament"); File.WriteAllText(path, source);
        var result = FirmamentDrawingCompiler.Compile(path, Path.Combine(temporary.Path, "out"));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var front = result.Artifacts!.Drawing.Pages.SelectMany(page => page.Views).Single(view => view.Identity == "Front");
        Assert.DoesNotContain(front.Primitives, item => item.Kind == DrawingPrimitiveKind.Hidden);
        Assert.True(front.VisibilityEvidence!.HiddenSegments > 0);
    }

    [Theory]
    [InlineData("Revision: 1.1.0", "Revision: 01.1.0", FirmamentDrawingCompiler.DrawingRevisionInvalid)]
    [InlineData("Date: 2026-08-10", "Date: 2026-02-30", FirmamentDrawingCompiler.DrawingDateInvalid)]
    public void DrawingInfo_RejectsInvalidVersionAndDate(string before, string after, string diagnostic)
    {
        using var temporary = new TemporaryDirectory(); var source = File.ReadAllText(Fixture()).Replace(before, after, StringComparison.Ordinal);
        var path = Path.Combine(temporary.Path, "invalid-metadata.firmament"); File.WriteAllText(path, source);
        var result = FirmamentDrawingCompiler.Compile(path, Path.Combine(temporary.Path, "out"));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, item => item.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void DrawingInfo_RejectsMissingRequiredField()
    {
        using var temporary = new TemporaryDirectory();
        var source = File.ReadAllText(Fixture())
            .ReplaceLineEndings("\n")
            .Replace("    Author: \"CODEX\"\n", "", StringComparison.Ordinal);
        var path = Path.Combine(temporary.Path, "missing-author.firmament"); File.WriteAllText(path, source);
        var result = FirmamentDrawingCompiler.Compile(path, Path.Combine(temporary.Path, "out"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.StartsWith(FirmamentDrawingCompiler.DrawingMetadataRequiredFieldMissing, StringComparison.Ordinal));
    }

    [Fact]
    public void AssemblyDrawing_IsDeterministic()
    {
        using var first = new TemporaryDirectory(); using var second = new TemporaryDirectory();
        var a = FirmamentDrawingCompiler.Compile(Fixture(), first.Path); var b = FirmamentDrawingCompiler.Compile(Fixture(), second.Path);
        Assert.True(a.IsSuccess, string.Join(Environment.NewLine, a.Diagnostics)); Assert.True(b.IsSuccess, string.Join(Environment.NewLine, b.Diagnostics));
        Assert.Equal(a.Artifacts!.DrawingIrSha256, b.Artifacts!.DrawingIrSha256); Assert.Equal(a.Artifacts.PdfSha256, b.Artifacts.PdfSha256);
    }

    private static string Fixture() => Path.Combine(RepositoryRoot(), "fixtures", "Regression", "Drawing", "machine-assembly-production-drawing-legacy-placement.firmament");
    private static string RepositoryRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException("Repository root not found."); }
    private sealed class TemporaryDirectory : IDisposable { public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aetheris-drawing-m0b-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); } public string Path { get; } public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); } }
}
