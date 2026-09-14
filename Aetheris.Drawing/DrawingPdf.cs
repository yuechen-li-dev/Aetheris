using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using PDFtoImage;

namespace Aetheris.Drawing;

public static class DrawingPdf
{
    public const string RendererVersion = "PDFtoImage-5.4.0/PDFium";

    public static string Hash(string pdfPath) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdfPath))).ToLowerInvariant();

    public static DrawingSourceDocument Inspect(string pdfPath, string? title = null, string? visibleDate = null, string? visibleRevision = null, IReadOnlyList<string>? sheets = null)
    {
        RequirePdf(pdfPath);
        var bytes = File.ReadAllBytes(pdfPath);
        var count = Conversion.GetPageCount(bytes);
        var sizes = Conversion.GetPageSizes(bytes);
        var pages = sizes.Select((size, index) => new DrawingPage(index + 1, size.Width, size.Height)).ToArray();
        if (pages.Length != count) throw new DrawingNotesException("drawing-pdf-page-metadata-mismatch", "PDF renderer returned inconsistent page metadata.");
        return new(Path.GetFileName(pdfPath), Hash(pdfPath), count, pages, title, visibleDate, visibleRevision, sheets, "PDFium", RendererVersion);
    }

    public static DrawingNotesProject CreateProject(string pdfPath, string? name = null, string? title = null, string? visibleDate = null, string? visibleRevision = null, IReadOnlyList<string>? sheets = null)
    {
        var document = Inspect(pdfPath, title, visibleDate, visibleRevision, sheets);
        return new DrawingNotesProject { Name = name ?? Path.GetFileNameWithoutExtension(pdfPath) + " — Drawing Notes", Document = document };
    }

    public static void RenderPage(string pdfPath, int page, string outputPng, int dpi = 200)
    {
        var document = Inspect(pdfPath);
        RequirePage(document, page);
        if (dpi is < 36 or > 1200) throw new DrawingNotesException("drawing-render-dpi-invalid", "Render DPI must be between 36 and 1200.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPng))!);
        Conversion.SavePng(outputPng, File.ReadAllBytes(pdfPath), page - 1, options: new RenderOptions(Dpi: dpi, WithAnnotations: true, UseTiling: dpi >= 300));
    }

    public static DrawingCropMetadata Crop(string pdfPath, int page, DrawingBounds bounds, string name, string outputPng, int dpi = 300)
    {
        var document = Inspect(pdfPath);
        var pageMetadata = RequirePage(document, page);
        bounds.Validate(name);
        if (bounds.Right > pageMetadata.WidthPoints + 0.01 || bounds.Bottom > pageMetadata.HeightPoints + 0.01)
            throw new DrawingNotesException("drawing-bounds-outside-page", $"Crop {bounds} exceeds page {page}.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPng))!);
        var clip = new RectangleF((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
        Conversion.SavePng(outputPng, File.ReadAllBytes(pdfPath), page - 1, options: new RenderOptions(Dpi: dpi, Bounds: clip, DpiRelativeToBounds: true, WithAnnotations: true));
        var metadata = new DrawingCropMetadata(page, bounds, document.Sha256, RendererVersion, dpi, Path.GetFileName(outputPng));
        File.WriteAllText(Path.ChangeExtension(outputPng, ".crop.json"), JsonSerializer.Serialize(metadata, DrawingNotesPersistence.JsonOptions) + "\n");
        return metadata;
    }

    private static DrawingPage RequirePage(DrawingSourceDocument document, int page) => document.Pages.SingleOrDefault(x => x.Number == page)
        ?? throw new DrawingNotesException("drawing-page-invalid", $"Page {page} is outside 1..{document.PageCount}.");

    private static void RequirePdf(string path)
    {
        if (!File.Exists(path)) throw new DrawingNotesException("drawing-pdf-missing", $"PDF source was not found: {Path.GetFullPath(path)}");
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)) throw new DrawingNotesException("drawing-pdf-extension-invalid", "Drawing source must be a PDF file.");
    }
}

public sealed record DrawingCropMetadata(int Page, DrawingBounds Bounds, string DocumentSha256, string RendererVersion, int Dpi, string FileName);
