using System.IO.Compression;
using System.Text;
using Aetheris.Kernel.Firmament.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DrawingPptxTests
{
    [Fact]
    public void PartDrawing_LowersToA4NativeNamedEditableShapesTablesAndReviewArtifacts()
    {
        using var temporary = new TemporaryDirectory();
        var result = FirmamentDrawingCompiler.Compile(Fixture(), temporary.Path);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var artifacts = result.Artifacts!;
        using var production = ZipFile.OpenRead(artifacts.PptxPath);
        var presentation = Read(production, "ppt/presentation.xml");
        Assert.Contains("cx=\"10692000\" cy=\"7560000\"", presentation, StringComparison.Ordinal); // A4 landscape
        var slide = string.Join("\n", production.Entries.Where(item => item.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && item.FullName.EndsWith(".xml", StringComparison.Ordinal)).Select(item => Read(production, item.FullName)));
        Assert.Contains("name=\"View.Front.Geometry\"", slide, StringComparison.Ordinal);
        Assert.Contains("name=\"PMI.", slide, StringComparison.Ordinal);
        Assert.Contains("name=\"Metadata.DrawingInfo\"", slide, StringComparison.Ordinal);
        Assert.Contains("name=\"Table.Design.", slide, StringComparison.Ordinal);
        Assert.Contains("<a:tbl>", slide, StringComparison.Ordinal);
        Assert.Contains("typeface=\"Inter\"", slide, StringComparison.Ordinal);
        Assert.Contains("<p:grpSp>", slide, StringComparison.Ordinal);
        Assert.Contains("<p:sp>", slide, StringComparison.Ordinal);
        Assert.DoesNotContain("<p:pic>", slide, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Review.", slide, StringComparison.Ordinal);

        Assert.NotNull(artifacts.ReviewPptxPath); Assert.NotNull(artifacts.DfmReviewPptxPath); Assert.NotNull(artifacts.ReviewIrPath);
        using var review = ZipFile.OpenRead(artifacts.ReviewPptxPath!);
        Assert.Contains("name=\"Review.DFM-004.Callout\"", Read(review, "ppt/slides/slide1.xml"), StringComparison.Ordinal);
        using var dfm = ZipFile.OpenRead(artifacts.DfmReviewPptxPath!);
        Assert.Contains("cx=\"12192001\" cy=\"6858000\"", Read(dfm, "ppt/presentation.xml"), StringComparison.Ordinal);
        Assert.Equal(2, dfm.Entries.Count(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)));

        var authoritative = artifacts.Drawing.Pages.SelectMany(page => page.Annotations).Single(item => item.SemanticReference == "MountDiameter");
        Assert.Equal("Ø8 mm +0.05/-0.02", authoritative.EngineeringDisplay);
        var proposal = artifacts.Drawing.Reviews!.Threads.Single().Entries.Single(item => item.Kind == Aetheris.Collaboration.ReviewEntryKind.Proposal).Proposal!;
        Assert.Equal("PlusMinus(0.010mm)", proposal.ProposedValue);
        Assert.Equal("Ø8 mm +0.05/-0.02", authoritative.EngineeringDisplay);

        foreach (var path in new[] { artifacts.PptxPath, artifacts.ReviewPptxPath!, artifacts.DfmReviewPptxPath! })
        {
            using var document = PresentationDocument.Open(path, false);
            var validationErrors = new OpenXmlValidator().Validate(document).ToArray();
            Assert.True(validationErrors.Length == 0, string.Join(Environment.NewLine, validationErrors.Select(error => $"{error.Part?.Uri}: {error.Path?.XPath}: {error.Description}")));
        }
    }

    [Fact]
    public void PptxPackage_IsDeterministic()
    {
        using var first = new TemporaryDirectory(); using var second = new TemporaryDirectory();
        var a = FirmamentDrawingCompiler.Compile(Fixture(), first.Path); var b = FirmamentDrawingCompiler.Compile(Fixture(), second.Path);
        Assert.True(a.IsSuccess); Assert.True(b.IsSuccess); Assert.Equal(a.Artifacts!.PptxSha256, b.Artifacts!.PptxSha256);
    }

    private static string Read(ZipArchive archive, string name) { using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8); return reader.ReadToEnd(); }
    private static string Fixture() => Path.Combine(RepositoryRoot(), "fixtures", "Canonical", "Drawings", "bearing-block-production-drawing.firmament");
    private static string RepositoryRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException(); }
    private sealed class TemporaryDirectory : IDisposable { public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aetheris-pptx-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); } public string Path { get; } public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); } }
}
