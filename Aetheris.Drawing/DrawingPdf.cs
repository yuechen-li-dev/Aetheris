using System.Security.Cryptography;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Tokens;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Parser.Parts;

namespace Aetheris.Drawing;

public static class DrawingPdf
{
    public const string RendererVersion = "PDF.js-6.4.299";
    public const string InspectorVersion = "PdfPig-0.1.16";

    public static string Hash(string pdfPath) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdfPath))).ToLowerInvariant();

    public static DrawingSourceDocument Inspect(string pdfPath, string? title = null, string? visibleDate = null, string? visibleRevision = null, IReadOnlyList<string>? sheets = null)
    {
        RequirePdf(pdfPath);
        var bytes = File.ReadAllBytes(pdfPath);
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            var pages = pdf.GetPages().Select(page =>
            {
                // Read the page-tree boxes explicitly: PdfPig 0.1.16's Page.Width
                // loses an inherited CropBox. Its tokenizer remains the PDF authority.
                var unit = 1d;
                if (page.Dictionary.TryGet(NameToken.Create("UserUnit"), out var token))
                {
                    var number = Resolve<NumericToken>(pdf, token, page.Number);
                    if (!double.IsFinite(number.Double) || number.Double <= 0)
                        throw new DrawingNotesException("drawing-pdf-user-unit-unsupported", $"Page {page.Number} has an unsupported UserUnit. A positive number is required.");
                    unit = number.Double;
                }
                var (w, h) = VisibleSize(pdf, page);
                var width = w * unit;
                var height = h * unit;
                if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                    throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page.Number} has invalid visible bounds.");
                return new DrawingPage(page.Number, width, height, page.Rotation.Value, unit);
            }).ToArray();
            if (pages.Length == 0) throw new DrawingNotesException("drawing-pdf-empty", "PDF has no pages.");
            return new(Path.GetFileName(pdfPath), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), pages.Length, pages, title, visibleDate, visibleRevision, sheets, "PDF.js", RendererVersion, "PdfPig", InspectorVersion);
        }
        catch (Exception e) when (e is not DrawingNotesException && (e is UglyToad.PdfPig.Core.PdfDocumentFormatException or InvalidOperationException or ArgumentException or NotSupportedException))
        {
            throw new DrawingNotesException("drawing-pdf-inspection-failed", $"Cannot inspect PDF '{Path.GetFileName(pdfPath)}': {e.Message}");
        }
    }

    private static (double Width, double Height) VisibleSize(PdfDocument pdf, Page page)
    {
        var media = Box(NameToken.MediaBox) ?? throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page.Number} has no MediaBox.");
        var crop = Box(NameToken.CropBox) ?? media;
        var width = Math.Min(media[2], crop[2]) - Math.Max(media[0], crop[0]);
        var height = Math.Min(media[3], crop[3]) - Math.Max(media[1], crop[1]);
        if (width <= 0 || height <= 0) throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page.Number} has an empty visible CropBox.");
        return page.Rotation.Value is 90 or 270 ? (height, width) : (width, height);

        double[]? Box(NameToken key)
        {
            var dictionary = page.Dictionary;
            var visited = new HashSet<DictionaryToken>();
            while (visited.Add(dictionary))
            {
                if (dictionary.TryGet(key, out var token))
                {
                    var array = Resolve<ArrayToken>(pdf, token, page.Number);
                    if (array.Length != 4) throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page.Number} has an invalid {key.Data}.");
                    var values = array.Data.Select(t => Resolve<NumericToken>(pdf, t, page.Number).Double).ToArray();
                    if (values.Any(x => !double.IsFinite(x)) || values[2] <= values[0] || values[3] <= values[1])
                        throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page.Number} has invalid {key.Data} coordinates.");
                    return values;
                }
                if (!dictionary.TryGet(NameToken.Parent, out var parent)) return null;
                dictionary = Resolve<DictionaryToken>(pdf, parent, page.Number);
            }
            throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page.Number} has a cyclic page tree.");
        }
    }

    private static T Resolve<T>(PdfDocument pdf, IToken token, int page) where T : class, IToken =>
        DirectObjectFinder.Get<T>(token, pdf.Structure.TokenScanner)
        ?? throw new DrawingNotesException("drawing-pdf-page-invalid", $"Page {page} contains a missing or invalid {typeof(T).Name} in its page metadata.");

    public static DrawingNotesProject CreateProject(string pdfPath, string? name = null, string? title = null, string? visibleDate = null, string? visibleRevision = null, IReadOnlyList<string>? sheets = null)
    {
        var document = Inspect(pdfPath, title, visibleDate, visibleRevision, sheets);
        return new DrawingNotesProject { Name = name ?? Path.GetFileNameWithoutExtension(pdfPath) + " — Drawing Notes", Document = document };
    }

    public static void RenderPage(string pdfPath, int page, string outputPng, int dpi = 200, IDrawingPdfRasterizer? rasterizer = null)
    {
        var document = Inspect(pdfPath);
        var metadata = RequirePage(document, page);
        RequireDpi(dpi);
        RequireRasterizer(rasterizer).Render(pdfPath, metadata, outputPng, dpi, null);
    }

    public static DrawingCropMetadata Crop(string pdfPath, int page, DrawingBounds bounds, string name, string outputPng, int dpi = 300, IDrawingPdfRasterizer? rasterizer = null)
    {
        var document = Inspect(pdfPath);
        var pageMetadata = RequirePage(document, page);
        RequireDpi(dpi);
        bounds.Validate(name);
        if (bounds.Right > pageMetadata.WidthPoints || bounds.Bottom > pageMetadata.HeightPoints)
            throw new DrawingNotesException("drawing-bounds-outside-page", $"Crop {bounds} exceeds page {page}.");
        var renderer = RequireRasterizer(rasterizer);
        renderer.Render(pdfPath, pageMetadata, outputPng, dpi, bounds);
        var metadata = new DrawingCropMetadata(page, bounds, document.Sha256, renderer.Version, dpi, Path.GetFileName(outputPng));
        File.WriteAllText(Path.ChangeExtension(outputPng, ".crop.json"), JsonSerializer.Serialize(metadata, DrawingNotesPersistence.JsonOptions) + "\n");
        return metadata;
    }

    private static IDrawingPdfRasterizer RequireRasterizer(IDrawingPdfRasterizer? renderer) => renderer ?? throw new DrawingNotesException("drawing-renderer-required", "PNG rendering requires an explicitly selected renderer. Use the Drawing Notes browser UI (PDF.js), or pass --renderer <path-to-pdftoppm> for headless rendering. No native renderer is bundled.");

    private static void RequireDpi(int dpi)
    {
        if (dpi is < 36 or > 1200) throw new DrawingNotesException("drawing-render-dpi-invalid", "Render DPI must be between 36 and 1200.");
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
