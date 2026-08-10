using System.Text;
using Aetheris.Kernel.Firmament.Drawing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DrawingM0Tests
{
    [Fact]
    public void DrawingTemplate_CompilesAuthoritativeProduct_ToTwoA4VectorPages()
    {
        using var temporary = new TemporaryDirectory();
        var result = FirmamentDrawingCompiler.Compile(Fixture(), temporary.Path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var artifacts = result.Artifacts!;
        Assert.Equal("BearingBlock", artifacts.Drawing.Provenance.SourceProductIdentity);
        Assert.Equal("StandardMachinedDrawing", artifacts.Drawing.Provenance.TemplateIdentity);
        Assert.Equal(2, artifacts.Drawing.Pages.Count);
        Assert.All(artifacts.Drawing.Pages, page => Assert.True(
            (page.WidthMillimetres, page.HeightMillimetres) is (297, 210) or (210, 297)));
        Assert.Equal(0, artifacts.Drawing.LayoutEvidence.TextModelCollisionsAfter);
        Assert.Equal(0, artifacts.Drawing.LayoutEvidence.TextTextCollisionsAfter);
        Assert.Equal(0, artifacts.Drawing.LayoutEvidence.FailedAnnotationCount);
        Assert.Contains(artifacts.Drawing.Pages.SelectMany(page => page.Annotations), annotation => annotation.SemanticReference == "MountDiameter" && annotation.EngineeringDisplay.Contains("8 mm", StringComparison.Ordinal));
        Assert.Contains(artifacts.Drawing.Pages.SelectMany(page => page.Tables), table => table.SourceIdentity == "BearingStandards" && table.Rows.Count == 3);

        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(artifacts.PdfPath));
        Assert.StartsWith("%PDF-1.4", pdf, StringComparison.Ordinal);
        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
        Assert.Contains("/MediaBox [0 0 841.89 595.276]", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/Subtype /Image", pdf, StringComparison.Ordinal);
        Assert.Contains("Bearing Block Production Drawing", pdf, StringComparison.Ordinal);
        Assert.Contains(artifacts.Drawing.Pages.SelectMany(page => page.Views), view => view.Identity == "ISO");
        Assert.Equal(15, artifacts.Drawing.Pages[0].ZoneScheme!.Zones[0].Bounds.Y);
    }

    [Fact]
    public void DrawingLayout_AndPdf_AreDeterministic()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var a = FirmamentDrawingCompiler.Compile(Fixture(), first.Path);
        var b = FirmamentDrawingCompiler.Compile(Fixture(), second.Path);

        Assert.True(a.IsSuccess, string.Join(Environment.NewLine, a.Diagnostics));
        Assert.True(b.IsSuccess, string.Join(Environment.NewLine, b.Diagnostics));
        Assert.Equal(a.Artifacts!.PdfSha256, b.Artifacts!.PdfSha256);
        Assert.Equal(a.Artifacts.DrawingIrSha256, b.Artifacts.DrawingIrSha256);
        Assert.Equal(
            a.Artifacts.Drawing.Pages.SelectMany(page => page.Annotations).Select(annotation => annotation.SelectedCandidate.Identity),
            b.Artifacts.Drawing.Pages.SelectMany(page => page.Annotations).Select(annotation => annotation.SelectedCandidate.Identity));
    }

    [Fact]
    public void UnsupportedPmiReference_IsTypedDiagnostic()
    {
        using var temporary = new TemporaryDirectory();
        var source = File.ReadAllText(Fixture()).Replace("PMI: [MountDiameter, A]", "PMI: [UnknownDimension]", StringComparison.Ordinal);
        var path = System.IO.Path.Combine(temporary.Path, "unknown-pmi.firmament");
        File.WriteAllText(path, source);

        var result = FirmamentDrawingCompiler.Compile(path, System.IO.Path.Combine(temporary.Path, "out"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.StartsWith(FirmamentDrawingCompiler.DrawingPmiUnknown, StringComparison.Ordinal));
    }

    [Fact]
    public void ConceptRequire_FailsWhenDrawingDoesNotCommunicateMaterial()
    {
        using var temporary = new TemporaryDirectory();
        var source = File.ReadAllText(Fixture())
            .Replace("Material: \"PER PRODUCT\"", "Material: \"\"", StringComparison.Ordinal)
            .Replace("Material: \"6061-T6 aluminium\"", "Material: \"\"", StringComparison.Ordinal);
        var path = System.IO.Path.Combine(temporary.Path, "missing-material.firmament");
        File.WriteAllText(path, source);

        var result = FirmamentDrawingCompiler.Compile(path, System.IO.Path.Combine(temporary.Path, "out"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("MachinedPartDrawing.Material", StringComparison.Ordinal));
    }

    private static string Fixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return System.IO.Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "fixtures", "DrawingM0", "bearing-block-drawing.firmament");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aetheris-drawing-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
